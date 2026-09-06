using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using SereinFlow.Contracts;
using SereinFlow.Library;

namespace SereinFlow.Worker.Runner;

public sealed partial class WorkerMessageService
{
    private interface IEndpointTopic
    {
        MessageChannelOptions Options { get; }

        bool TryMarkRegistered();
    }

    private sealed class QueueTopic : IEndpointTopic
    {
        private readonly Channel<MessageEnvelope> _channel;
        private int _registered;

        public QueueTopic(MessageChannelOptions options)
        {
            Options = options;
            _channel = Channel.CreateBounded<MessageEnvelope>(new BoundedChannelOptions(options.Capacity)
            {
                FullMode = ToFullMode(options.OverflowStrategy),
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
        }

        public MessageChannelOptions Options { get; }

        public ChannelReader<MessageEnvelope> Reader => _channel.Reader;

        public bool TryWrite(MessageEnvelope envelope) => _channel.Writer.TryWrite(envelope);

        public void Complete() => _channel.Writer.TryComplete();

        public bool TryMarkRegistered() => Interlocked.Exchange(ref _registered, 1) == 0;
    }

    private sealed class EventTopic : IEndpointTopic
    {
        private readonly ConcurrentDictionary<Guid, EventSubscriptionCore> _subscriptions = new();
        private int _registered;

        public EventTopic(MessageChannelOptions options) => Options = options;

        public MessageChannelOptions Options { get; }

        public bool TryPublish(MessageEnvelope envelope)
        {
            var accepted = true;
            foreach (var subscription in _subscriptions.Values)
            {
                if (!subscription.TryWrite(envelope))
                    accepted = false;
            }
            return accepted;
        }

        public IEventSubscription<T> Subscribe<T>(WorkerMessageService owner, string topic, CancellationToken cancellationToken)
        {
            var subscription = new EventSubscription<T>(owner, topic, Options, cancellationToken);
            _subscriptions[subscription.Id] = subscription.Core;
            return subscription;
        }

        public void Remove(Guid id, EventSubscriptionCore subscription)
        {
            if (_subscriptions.TryRemove(new KeyValuePair<Guid, EventSubscriptionCore>(id, subscription)))
                subscription.Complete();
        }

        public void Complete()
        {
            foreach (var subscription in _subscriptions.Values)
                subscription.Complete();
            _subscriptions.Clear();
        }

        public bool TryMarkRegistered() => Interlocked.Exchange(ref _registered, 1) == 0;
    }

    private sealed class QueueView(WorkerMessageService owner, MessageChannelOptions options) : IMessageQueue
    {
        public ValueTask SendAsync(string topic, object message, CancellationToken cancellationToken = default)
            => owner.SendQueueAsync(topic, message, options, cancellationToken);

        public ValueTask<T> ReceiveAsync<T>(string topic, CancellationToken cancellationToken = default)
            => owner.ReceiveQueueAsync<T>(topic, options, cancellationToken);
    }

    private sealed class EventBusView(WorkerMessageService owner, MessageChannelOptions options) : IEventBus
    {
        public ValueTask PublishAsync(string topic, object message, CancellationToken cancellationToken = default)
            => owner.PublishEventAsync(topic, message, options, cancellationToken);

        public IEventSubscription<T> Subscribe<T>(string topic, CancellationToken cancellationToken = default)
            => owner.SubscribeEvent<T>(topic, options, cancellationToken);
    }

    private sealed class EventSubscription<T> : IEventSubscription<T>
    {
        private readonly WorkerMessageService _owner;
        private readonly string _topic;
        private readonly CancellationTokenRegistration _cancellationRegistration;
        private int _disposed;

        public EventSubscription(
            WorkerMessageService owner,
            string topic,
            MessageChannelOptions options,
            CancellationToken cancellationToken)
        {
            _owner = owner;
            _topic = topic;
            Id = Guid.NewGuid();
            Core = new EventSubscriptionCore(options);
            if (cancellationToken.CanBeCanceled)
                _cancellationRegistration = cancellationToken.Register(static state => ((EventSubscription<T>)state!).DisposeCore(), this);
        }

        public Guid Id { get; }

        public EventSubscriptionCore Core { get; }

        public async IAsyncEnumerable<T> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (true)
            {
                MessageEnvelope envelope;
                try
                {
                    envelope = await Core.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (ChannelClosedException)
                {
                    yield break;
                }

                if (envelope.IsExpired(DateTimeOffset.UtcNow))
                    continue;
                yield return Deserialize<T>(envelope, _topic);
            }
        }

        public async ValueTask<T> NextAsync(CancellationToken cancellationToken = default)
        {
            MessageEnvelope envelope;
            try
            {
                envelope = await Core.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ChannelClosedException exception)
            {
                throw new MessageServiceException(
                    MessageErrorCodes.SubscriptionClosed,
                    "The event subscription is closed. 事件订阅已关闭。",
                    _topic,
                    exception);
            }

            if (envelope.IsExpired(DateTimeOffset.UtcNow))
                return await NextAsync(cancellationToken).ConfigureAwait(false);
            return Deserialize<T>(envelope, _topic);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCore();
            return ValueTask.CompletedTask;
        }

        private void DisposeCore()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;
            _cancellationRegistration.Dispose();
            _owner.RemoveSubscription(_topic, Id, Core);
        }
    }

    private sealed class EventSubscriptionCore
    {
        private readonly Channel<MessageEnvelope> _channel;

        public EventSubscriptionCore(MessageChannelOptions options)
        {
            _channel = Channel.CreateBounded<MessageEnvelope>(new BoundedChannelOptions(options.Capacity)
            {
                FullMode = ToFullMode(options.OverflowStrategy),
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
        }

        public ChannelReader<MessageEnvelope> Reader => _channel.Reader;

        public bool TryWrite(MessageEnvelope envelope) => _channel.Writer.TryWrite(envelope);

        public void Complete() => _channel.Writer.TryComplete();
    }

    private sealed record MessageEnvelope(
        Guid MessageId,
        string Topic,
        MessageSerializationMode Mode,
        object? Value,
        string? JsonPayload,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ExpiresAt)
    {
        public static MessageEnvelope Direct(Guid id, string topic, object value, DateTimeOffset createdAt, DateTimeOffset? expiresAt)
            => new(id, topic, MessageSerializationMode.DirectObject, value, null, createdAt, expiresAt);

        public static MessageEnvelope Json(Guid id, string topic, string json, DateTimeOffset createdAt, DateTimeOffset? expiresAt)
            => new(id, topic, MessageSerializationMode.Json, null, json, createdAt, expiresAt);

        public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } expiresAt && expiresAt <= now;
    }

    private static BoundedChannelFullMode ToFullMode(MessageOverflowStrategy strategy)
        => strategy switch
        {
            MessageOverflowStrategy.Reject => BoundedChannelFullMode.Wait,
            MessageOverflowStrategy.DropOldest => BoundedChannelFullMode.DropOldest,
            MessageOverflowStrategy.DropNewest => BoundedChannelFullMode.DropWrite,
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unsupported message overflow strategy.")
        };
}

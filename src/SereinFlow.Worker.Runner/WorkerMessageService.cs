using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using SereinFlow.Contracts;
using SereinFlow.Library;
using SereinFlow.Worker.Protocol;

namespace SereinFlow.Worker.Runner;

/// <summary>
/// Run-scoped in-memory message service. All queue and event-bus views created
/// from this service share the same broker and are disposed with the Worker run.
/// 运行级内存消息服务；由此服务创建的所有队列和事件总线视图共享同一个 Broker，
/// 并随 Worker 运行一起释放。
/// </summary>
public sealed partial class WorkerMessageService : IMessageService, IAsyncDisposable
{
    private const int MaximumDeduplicationEntries = 4096;
    private static readonly TimeSpan DeduplicationTtl = TimeSpan.FromMinutes(10);
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateWebOptions();

    private readonly Guid _runId;
    private readonly Action<WorkerMessageEndpointDto>? _endpointRegistered;
    private readonly Action<WorkerMessageEndpointDto>? _endpointUnregistered;
    private readonly ConcurrentDictionary<string, QueueTopic> _queues = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, EventTopic> _events = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, WorkerMessageEndpointDto> _registeredEndpoints = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, DateTimeOffset> _acceptedMessageIds = [];
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _deduplicationGate = new();
    private int _disposed;

    public WorkerMessageService(
        Guid runId,
        Action<WorkerMessageEndpointDto>? endpointRegistered = null,
        Action<WorkerMessageEndpointDto>? endpointUnregistered = null)
    {
        if (runId == Guid.Empty)
            throw new ArgumentException("A Worker message service requires a run ID. Worker 消息服务需要运行 ID。", nameof(runId));
        _runId = runId;
        _endpointRegistered = endpointRegistered;
        _endpointUnregistered = endpointUnregistered;
    }

    public IMessageQueue CreateMessageQueue(MessageChannelOptions? options = null)
        => new QueueView(this, NormalizeOptions(options));

    public IMessageQueue CreateMessageQueue(bool useJson)
        => CreateMessageQueue(new MessageChannelOptions
        {
            SerializationMode = useJson ? MessageSerializationMode.Json : MessageSerializationMode.DirectObject
        });

    public IEventBus CreateEventBus(MessageChannelOptions? options = null)
        => new EventBusView(this, NormalizeOptions(options));

    public WorkerMessageDeliveryResult TryDeliver(WorkerMessageDeliveryDto delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        var headerRejection = ValidateDeliveryHeader(delivery);
        if (headerRejection is not null)
            return headerRejection;

        var topic = NormalizeTopic(delivery.Topic);
        var endpoint = FindEndpoint(delivery.ChannelKind, topic);
        if (endpoint is null)
            return Reject(delivery, MessageErrorCodes.EndpointNotReady, "The message endpoint is not registered. 消息入口尚未注册。");

        var endpointRejection = ValidateEndpoint(delivery, endpoint);
        if (endpointRejection is not null)
            return endpointRejection;

        var payloadRejection = ValidatePayload(delivery, endpoint.Options);
        if (payloadRejection is not null)
            return payloadRejection;

        var options = endpoint.Options;
        var createdAt = delivery.CreatedAt == default ? DateTimeOffset.UtcNow : delivery.CreatedAt;
        DateTimeOffset? expiresAt = delivery.ExpiresAt;
        if (expiresAt is null && options.MessageTtl is { } ttl)
            expiresAt = createdAt + ttl;
        if (expiresAt is not null && expiresAt <= DateTimeOffset.UtcNow)
            return Reject(delivery, MessageErrorCodes.Expired, "The message has expired. 消息已过期。");

        var envelope = MessageEnvelope.Json(
            delivery.MessageId,
            topic,
            delivery.PayloadJson ?? string.Empty,
            createdAt,
            expiresAt);
        return AcceptDelivery(delivery, endpoint, envelope, topic);
    }

    private WorkerMessageDeliveryResult? ValidateDeliveryHeader(WorkerMessageDeliveryDto delivery)
    {
        if (delivery.ProtocolVersion != WorkerProtocolConstants.Version)
            return Reject(delivery, MessageErrorCodes.ProtocolMismatch, "The message protocol version is not supported. 消息协议版本不受支持。");
        if (delivery.RunId != _runId)
            return Reject(delivery, MessageErrorCodes.RunMismatch, "The message belongs to another Worker run. 消息属于其他 Worker 运行。");
        if (delivery.MessageId == Guid.Empty)
            return Reject(delivery, MessageErrorCodes.IdRequired, "MessageId is required. MessageId 不能为空。");
        if (string.IsNullOrWhiteSpace(delivery.Topic))
            return Reject(delivery, MessageErrorCodes.TopicRequired, "Message topic is required. 消息主题不能为空。");
        if (delivery.SerializationMode != WorkerMessageSerializationModeDto.Json)
            return Reject(delivery, MessageErrorCodes.ExternalJsonRequired, "External ingress only accepts JSON messages. 外部入口只接受 JSON 消息。");
        if (!Enum.IsDefined(delivery.ChannelKind))
            return Reject(delivery, MessageErrorCodes.ChannelInvalid, "The message channel kind is invalid. 消息通道类型无效。");
        return null;
    }

    private IEndpointTopic? FindEndpoint(WorkerMessageChannelKindDto kind, string topic)
        => kind switch
        {
            WorkerMessageChannelKindDto.Queue => _queues.TryGetValue(topic, out var queue) ? queue : null,
            WorkerMessageChannelKindDto.EventBus => _events.TryGetValue(topic, out var bus) ? bus : null,
            _ => null
        };

    private static WorkerMessageDeliveryResult? ValidateEndpoint(
        WorkerMessageDeliveryDto delivery,
        IEndpointTopic endpoint)
    {
        var options = endpoint.Options;
        if (!options.ExternalIngress)
            return Reject(delivery, MessageErrorCodes.EndpointForbidden, "The message endpoint is not open to external ingress. 消息入口未开放外部投递。");
        if (options.SerializationMode != MessageSerializationMode.Json)
            return Reject(delivery, MessageErrorCodes.EndpointJsonRequired, "The external endpoint must use JSON serialization. 外部入口必须使用 JSON 序列化。");
        if (!string.Equals(options.ContractId, delivery.ContractId, StringComparison.Ordinal))
            return Reject(delivery, MessageErrorCodes.ContractMismatch, "The message contract does not match the registered endpoint. 消息合同与已注册入口不匹配。");
        return null;
    }

    private static WorkerMessageDeliveryResult? ValidatePayload(
        WorkerMessageDeliveryDto delivery,
        MessageChannelOptions options)
    {
        var payloadBytes = Encoding.UTF8.GetByteCount(delivery.PayloadJson ?? string.Empty);
        if (payloadBytes > options.MaxPayloadBytes)
            return Reject(delivery, MessageErrorCodes.PayloadTooLarge, $"Message payloads are limited to {options.MaxPayloadBytes} bytes. 消息载荷不能超过 {options.MaxPayloadBytes} 字节。");

        try
        {
            using var document = JsonDocument.Parse(delivery.PayloadJson ?? string.Empty);
        }
        catch (JsonException exception)
        {
            return Reject(delivery, MessageErrorCodes.PayloadInvalid, $"The message payload is not valid JSON. 消息载荷不是有效 JSON。 {exception.Message}");
        }

        return null;
    }

    private WorkerMessageDeliveryResult AcceptDelivery(
        WorkerMessageDeliveryDto delivery,
        IEndpointTopic endpoint,
        MessageEnvelope envelope,
        string topic)
    {
        lock (_deduplicationGate)
        {
            PruneAcceptedMessageIds(DateTimeOffset.UtcNow);
            if (_acceptedMessageIds.ContainsKey(delivery.MessageId))
            {
                return new WorkerMessageDeliveryResult(
                    true,
                    true,
                    null,
                    "The message was already accepted. 消息此前已经被接受。",
                    delivery.MessageId,
                    topic);
            }

            var accepted = delivery.ChannelKind switch
            {
                WorkerMessageChannelKindDto.Queue => ((QueueTopic)endpoint).TryWrite(envelope),
                WorkerMessageChannelKindDto.EventBus => ((EventTopic)endpoint).TryPublish(envelope),
                _ => false
            };
            if (!accepted)
                return Reject(delivery, MessageErrorCodes.ChannelFull, "The message channel is full. 消息通道已满。");

            RememberAcceptedMessage(delivery.MessageId);
        }

        return new WorkerMessageDeliveryResult(true, false, null, "The message was accepted. 消息已接受。", delivery.MessageId, topic);
    }

    private void RememberAcceptedMessage(Guid messageId)
    {
        if (_acceptedMessageIds.Count >= MaximumDeduplicationEntries)
        {
            var oldest = _acceptedMessageIds.MinBy(static item => item.Value);
            if (oldest.Key != Guid.Empty)
                _acceptedMessageIds.Remove(oldest.Key);
        }

        _acceptedMessageIds[messageId] = DateTimeOffset.UtcNow + DeduplicationTtl;
    }

    internal async ValueTask SendQueueAsync(
        string topic,
        object message,
        MessageChannelOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureOpen();
        var normalizedTopic = NormalizeTopic(topic);
        var channel = GetQueue(normalizedTopic, options);
        var envelope = CreateEnvelope(normalizedTopic, message, options);
        if (!channel.TryWrite(envelope))
        {
            throw new MessageServiceException(
                MessageErrorCodes.QueueFull,
                "The message queue is full. 消息队列已满。",
                normalizedTopic);
        }
        await ValueTask.CompletedTask;
    }

    internal ValueTask<T> ReceiveQueueAsync<T>(
        string topic,
        MessageChannelOptions options,
        CancellationToken cancellationToken)
        => ReceiveQueueCoreAsync<T>(NormalizeTopic(topic), options, cancellationToken);

    private async ValueTask<T> ReceiveQueueCoreAsync<T>(
        string topic,
        MessageChannelOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureOpen();
        var channel = GetQueue(topic, options);
        while (true)
        {
            MessageEnvelope envelope;
            try
            {
                envelope = await channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ChannelClosedException exception)
            {
                throw new MessageServiceException(
                    MessageErrorCodes.QueueClosed,
                    "The message queue was closed with the Worker run. 消息队列已随 Worker 运行关闭。",
                    topic,
                    exception);
            }

            if (envelope.IsExpired(DateTimeOffset.UtcNow))
                continue;
            return Deserialize<T>(envelope, topic);
        }
    }

    internal async ValueTask PublishEventAsync(
        string topic,
        object message,
        MessageChannelOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureOpen();
        var normalizedTopic = NormalizeTopic(topic);
        var eventTopic = GetEvent(normalizedTopic, options);
        var envelope = CreateEnvelope(normalizedTopic, message, options);
        if (!eventTopic.TryPublish(envelope) && options.OverflowStrategy == MessageOverflowStrategy.Reject)
        {
            throw new MessageServiceException(
                MessageErrorCodes.EventFull,
                "At least one event subscription is full. 至少一个事件订阅者的缓冲区已满。",
                normalizedTopic);
        }
        await ValueTask.CompletedTask;
    }

    internal IEventSubscription<T> SubscribeEvent<T>(
        string topic,
        MessageChannelOptions options,
        CancellationToken cancellationToken)
    {
        EnsureOpen();
        var normalizedTopic = NormalizeTopic(topic);
        var eventTopic = GetEvent(normalizedTopic, options);
        var subscription = eventTopic.Subscribe<T>(this, normalizedTopic, cancellationToken);
        RegisterEndpoint(normalizedTopic, WorkerMessageChannelKindDto.EventBus, options, eventTopic);
        return subscription;
    }

    private void RemoveSubscription(string topic, Guid subscriptionId, EventSubscriptionCore subscription)
    {
        if (_events.TryGetValue(topic, out var eventTopic))
            eventTopic.Remove(subscriptionId, subscription);
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return ValueTask.CompletedTask;

        _shutdown.Cancel();
        foreach (var endpoint in _registeredEndpoints.Values)
        {
            try
            {
                _endpointUnregistered?.Invoke(endpoint);
            }
            catch
            {
            }
        }
        _registeredEndpoints.Clear();
        foreach (var queue in _queues.Values)
            queue.Complete();
        foreach (var eventTopic in _events.Values)
            eventTopic.Complete();
        _queues.Clear();
        _events.Clear();
        _shutdown.Dispose();
        return ValueTask.CompletedTask;
    }

    private QueueTopic GetQueue(string topic, MessageChannelOptions options)
    {
        var channel = _queues.GetOrAdd(topic, _ => new QueueTopic(options));
        EnsureCompatible(channel.Options, options, topic);
        RegisterEndpoint(topic, WorkerMessageChannelKindDto.Queue, options, channel);
        return channel;
    }

    private EventTopic GetEvent(string topic, MessageChannelOptions options)
    {
        var channel = _events.GetOrAdd(topic, _ => new EventTopic(options));
        EnsureCompatible(channel.Options, options, topic);
        return channel;
    }

    private void RegisterEndpoint(
        string topic,
        WorkerMessageChannelKindDto kind,
        MessageChannelOptions options,
        IEndpointTopic endpoint)
    {
        if (!options.ExternalIngress || !endpoint.TryMarkRegistered())
            return;

        try
        {
            var registration = new WorkerMessageEndpointDto(
                WorkerProtocolConstants.Version,
                _runId,
                topic,
                kind,
                WorkerMessageSerializationModeDto.Json,
                options.ContractId,
                ExternalIngress: true);
            _registeredEndpoints[GetEndpointKey(kind, topic)] = registration;
            _endpointRegistered?.Invoke(registration);
        }
        catch
        {
            // Endpoint registration is an observability/control-plane signal;
            // the local broker remains usable if protocol output is closing.
            // 端点注册属于可观测/控制面信号；协议输出正在关闭时本地 Broker 仍可用。
        }
    }

    private static MessageEnvelope CreateEnvelope(string topic, object message, MessageChannelOptions options)
    {
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        DateTimeOffset? expiresAt = options.MessageTtl is { } ttl ? createdAt + ttl : null;
        if (options.SerializationMode == MessageSerializationMode.DirectObject)
            return MessageEnvelope.Direct(id, topic, message, createdAt, expiresAt);

        try
        {
            var json = JsonSerializer.Serialize(message, JsonOptions);
            if (Encoding.UTF8.GetByteCount(json) > options.MaxPayloadBytes)
                throw new MessageServiceException(
                    MessageErrorCodes.PayloadTooLarge,
                    $"Message payloads are limited to {options.MaxPayloadBytes} bytes. 消息载荷不能超过 {options.MaxPayloadBytes} 字节。",
                    topic);
            return MessageEnvelope.Json(id, topic, json, createdAt, expiresAt);
        }
        catch (MessageServiceException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new MessageServiceException(
                MessageErrorCodes.SerializationFailed,
                "The message could not be serialized as JSON. 消息无法序列化为 JSON。",
                topic,
                exception);
        }
    }

    private static T Deserialize<T>(MessageEnvelope envelope, string topic)
    {
        if (envelope.Mode == MessageSerializationMode.DirectObject)
        {
            if (envelope.Value is T value)
                return value;
            if (envelope.Value is null && default(T) is null)
                return default!;
            throw new MessageTypeMismatchException(topic, typeof(T), envelope.Value?.GetType());
        }

        try
        {
            return JsonSerializer.Deserialize<T>(envelope.JsonPayload!, JsonOptions)
                ?? (default(T) is null
                    ? default!
                    : throw new MessageServiceException(
                        MessageErrorCodes.DeserializationNull,
                        "The JSON message deserialized to null for a non-nullable target. JSON 消息反序列化为 null，但目标类型不可为空。",
                        topic));
        }
        catch (MessageServiceException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new MessageServiceException(
                MessageErrorCodes.DeserializationFailed,
                "The JSON message could not be converted to the requested type. JSON 消息无法转换为请求的类型。",
                topic,
                exception);
        }
    }

    private static MessageChannelOptions NormalizeOptions(MessageChannelOptions? options)
    {
        var normalized = options ?? new MessageChannelOptions();
        if (!Enum.IsDefined(normalized.SerializationMode))
            throw new ArgumentOutOfRangeException(nameof(options), "Message serialization mode is invalid. 消息序列化模式无效。");
        if (!Enum.IsDefined(normalized.OverflowStrategy))
            throw new ArgumentOutOfRangeException(nameof(options), "Message overflow strategy is invalid. 消息溢出策略无效。");
        if (normalized.Capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "Message channel capacity must be positive. 消息通道容量必须为正数。");
        if (normalized.MaxPayloadBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "Message payload size must be positive. 消息载荷大小必须为正数。");
        if (normalized.MessageTtl is { } ttl && ttl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "Message TTL must be positive. 消息有效期必须为正数。");
        if (normalized.ContractId is { Length: > 128 })
            throw new ArgumentException("Message contract IDs cannot exceed 128 characters. 消息合同 ID 不能超过 128 个字符。", nameof(options));
        if (normalized.ExternalIngress && normalized.SerializationMode != MessageSerializationMode.Json)
            throw new ArgumentException("External ingress requires JSON serialization. 外部入口必须使用 JSON 序列化。", nameof(options));
        return normalized;
    }

    private static void EnsureCompatible(MessageChannelOptions expected, MessageChannelOptions actual, string topic)
    {
        if (expected != actual)
        {
            throw new MessageServiceException(
                MessageErrorCodes.ChannelOptionsConflict,
                "The same topic was created with conflicting channel options. 同一主题使用了冲突的通道选项。",
                topic);
        }
    }

    private void EnsureOpen()
    {
        if (Volatile.Read(ref _disposed) == 1)
            throw new MessageServiceException(MessageErrorCodes.ServiceClosed, "The Worker message service is closed. Worker 消息服务已关闭。");
    }

    private static string NormalizeTopic(string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        var normalized = topic.Trim();
        if (normalized.Length > 256 || normalized.Contains('\r') || normalized.Contains('\n'))
            throw new ArgumentException("Message topics must be between 1 and 256 characters and cannot contain newlines. 消息主题长度必须在 1 到 256 个字符之间且不能包含换行。", nameof(topic));
        return normalized;
    }

    private static string GetEndpointKey(WorkerMessageChannelKindDto kind, string topic)
        => $"{kind}:{topic}";

    private static WorkerMessageDeliveryResult Reject(WorkerMessageDeliveryDto delivery, string code, string message)
        => new(false, false, code, message, delivery.MessageId, delivery.Topic?.Trim() ?? string.Empty);

    private void PruneAcceptedMessageIds(DateTimeOffset now)
    {
        foreach (var item in _acceptedMessageIds.Where(item => item.Value <= now).ToArray())
            _acceptedMessageIds.Remove(item.Key);
    }

}

public sealed record WorkerMessageDeliveryResult(
    bool Accepted,
    bool Duplicate,
    string? Code,
    string Message,
    Guid MessageId,
    string Topic);

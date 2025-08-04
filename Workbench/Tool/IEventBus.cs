using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Serein.Library.Api
{
#if false

    #region 事件总线接口
    public interface IEvent
    {

    }

    public interface IAsyncEventHandler<TEvent> where TEvent : IEvent
    {
        Task HandleAsync(TEvent @event);

        void HandleException(TEvent @event, Exception ex);
    }

    public interface IEventBus
    {
        void Publish<TEvent>(TEvent @event) where TEvent : IEvent;
        Task PublishAsync<TEvent>(TEvent @event) where TEvent : IEvent;

        void OnSubscribe<TEvent>() where TEvent : IEvent;
    }
    public interface ILocalEventBusManager<in TEvent> where TEvent : IEvent
    {
        void Publish(TEvent @event);
        Task PublishAsync(TEvent @event);

        void AutoHandle();
    }

    #endregion
    #region 事件总线实现类
    public class LocalEventBusManager<TEvent>(IServiceProvider serviceProvider) : ILocalEventBusManager<TEvent>
    where TEvent : IEvent
    {
        readonly IServiceProvider _servicesProvider = serviceProvider;

        private readonly Channel<TEvent> _eventChannel = Channel.CreateUnbounded<TEvent>();

        public void Publish(TEvent @event)
        {
            Debug.Assert(_eventChannel != null, nameof(_eventChannel) + " != null");
            _eventChannel.Writer.WriteAsync(@event);
        }

        private CancellationTokenSource Cts { get; } = new();

        public void Cancel()
        {
            Cts.Cancel();
        }

        public async Task PublishAsync(TEvent @event)
        {
            await _eventChannel.Writer.WriteAsync(@event);
        }

        public void AutoHandle()
        {
            // 确保只启动一次
            if (!Cts.IsCancellationRequested) return;

            Task.Run(async () =>
            {
                while (!Cts.IsCancellationRequested)
                {
                    var reader = await _eventChannel.Reader.ReadAsync();
                    await HandleAsync(reader);
                }
            }, Cts.Token);
        }

        async Task HandleAsync(TEvent @event)
        {
            var handler = _servicesProvider.GetService<IAsyncEventHandler<TEvent>>();

            if (handler is null)
            {
                throw new NullReferenceException($"No handler for event {@event.GetType().Name}");
            }
            try
            {
                await handler.HandleAsync(@event);
            }
            catch (Exception ex)
            {
                handler.HandleException(@event, ex);
            }
        }
    }

    public sealed class LocalEventBusPool(IServiceProvider serviceProvider)
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;

        private class ChannelKey
        {
            public required string Key { get; init; }
            public int Subscribers { get; set; }

            public override bool Equals(object? obj)
            {
                if (obj is ChannelKey key)
                {
                    return string.Equals(key.Key, Key, StringComparison.OrdinalIgnoreCase);
                }

                return false;
            }

            public override int GetHashCode()
            {
                return 0;
            }
        }

        private Channel<IEvent> Rent(string channel)
        {
            _channels.TryGetValue(new ChannelKey() { Key = channel }, out var value);

            if (value != null) return value;
            value = Channel.CreateUnbounded<IEvent>();
            _channels.TryAdd(new ChannelKey() { Key = channel }, value);
            return value;
        }

        private Channel<IEvent> Rent(ChannelKey channelKey)
        {
            _channels.TryGetValue(channelKey, out var value);
            if (value != null) return value;
            value = Channel.CreateUnbounded<IEvent>();
            _channels.TryAdd(channelKey, value);
            return value;
        }

        private readonly ConcurrentDictionary<ChannelKey, Channel<IEvent>> _channels = new();

        private CancellationTokenSource Cts { get; } = new();

        public void Cancel()
        {
            Cts.Cancel();
            _channels.Clear();
            Cts.TryReset();
        }

        public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : IEvent
        {
            await Rent(typeof(TEvent).Name).Writer.WriteAsync(@event);
        }

        public void Publish<TEvent>(TEvent @event) where TEvent : IEvent
        {
            Rent(typeof(TEvent).Name).Writer.TryWrite(@event);
        }

        public void OnSubscribe<TEvent>() where TEvent : IEvent
        {
            var channelKey = _channels.FirstOrDefault(x => x.Key.Key == typeof(TEvent).Name).Key ??
                             new ChannelKey() { Key = typeof(TEvent).Name };
            channelKey.Subscribers++;

            Task.Run(async () =>
            {
                try
                {
                    while (!Cts.IsCancellationRequested)
                    {
                        var @event = await ReadAsync(channelKey);

                        var handler = _serviceProvider.GetService<IAsyncEventHandler<TEvent>>();
                        if (handler == null) throw new NullReferenceException($"No handler for Event {typeof(TEvent).Name}");
                        try
                        {
                            await handler.HandleAsync((TEvent)@event);
                        }
                        catch (Exception ex)
                        {
                            handler.HandleException((TEvent)@event, ex);
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException("Error on onSubscribe handler", e);
                }
            }, Cts.Token);
        }

        private async Task<IEvent> ReadAsync(string channel)
        {
            return await Rent(channel).Reader.ReadAsync(Cts.Token);
        }

        private async Task<IEvent> ReadAsync(ChannelKey channel)
        {
            return await Rent(channel).Reader.ReadAsync(Cts.Token);
        }
    }



    #endregion
    #region 测试方法
    public class EventBusTest
    {
        private readonly LocalEventBusPool _localEventBusPool;
        private readonly LocalEventBusManager<TestEvent> _localEventBusManager;
        public EventBusTest(IServiceProvider serviceProvider)
        {
            /*collection.AddSingleton<IAsyncEventHandler<TestEvent>, TestHandler>();
            EventBusTest eventBusTest = new EventBusTest(services);
            _ = eventBusTest.Run();*/

            _localEventBusPool = new LocalEventBusPool(serviceProvider);
            _localEventBusManager = new Api.LocalEventBusManager<TestEvent>(serviceProvider);

        }

        public async Task Run()
        {
            var @event = new TestEvent { Name = "Test Event Async" };

            _localEventBusPool.OnSubscribe<TestEvent>();
            _localEventBusPool.Publish(@event);
            await _localEventBusPool.PublishAsync(@event);

            //_localEventBusManager.AutoHandle();
            _localEventBusManager.Publish(@event);
            await _localEventBusManager.PublishAsync(@event);
        }
    }

    public class TestEvent : IEvent
    {
        public string Name { get; set; } = "Test Event";
    }



    public class TestHandler : IAsyncEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent @event)
        {

            Debug.WriteLine($"Handling event: {@event.Name}");
            return Task.CompletedTask;
        }

        public void HandleException(TestEvent @event, Exception ex)
        {
            Debug.WriteLine($"Exception occurred while handling event {@event.GetType().Name}: {ex.Message}");
        }
    }

    #endregion

#endif
}

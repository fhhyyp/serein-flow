using System.Collections.Concurrent;
using System.Text.Json;
using SereinFlow.Contracts;
using SereinFlow.Library;
using SereinFlow.Worker.Runner;

namespace SereinFlow.Worker.IntegrationTests;

public sealed class WorkerMessageServiceTests
{
    [Fact]
    public async Task QueueUsesCompetitiveFifoDeliveryAcrossViews()
    {
        var newGuidResult = Guid.NewGuid();
        await using var service = new WorkerMessageService(newGuidResult);
        var options = new MessageChannelOptions
        {
            Capacity = 128
        };
        var first = service.CreateMessageQueue(options);
        var second = service.CreateMessageQueue(options);
        var received = new ConcurrentBag<int>();
        var consumers = Enumerable.Range(0, 4)
            .Select(_ =>
            {
                var runResult = Task.Run(async () =>
                {
                    for (var index = 0; index < 25; index++)
                    {
                        var receiveAsyncResult = await (index % 2 == 0 ? first : second).ReceiveAsync<int>("numbers");
                        received.Add(receiveAsyncResult);
                    }
                });
                return runResult;
            })
            .ToArray();
        for (var index = 0; index < 100; index++)
        {
            await first.SendAsync("numbers", index);
        }

        await Task.WhenAll(consumers);
        Assert.Equal(100, received.Count);
        var rangeResult1 = Enumerable.Range(0, 100);
        var orderByResult = received.OrderBy(static item => item);
        Assert.Equal(rangeResult1, orderByResult);
    }

    [Fact]
    public async Task EventBusBroadcastsOnlyToSubscriptionsActiveAtPublishTime()
    {
        var newGuidResult2 = Guid.NewGuid();
        await using var service = new WorkerMessageService(newGuidResult2);
        var messageChannelOptions = new MessageChannelOptions
        {
            Capacity = 8
        };
        var bus = service.CreateEventBus(messageChannelOptions);
        await using var first = bus.Subscribe<int>("events");
        await using var second = bus.Subscribe<int>("events");
        await bus.PublishAsync("events", 42);
        var nextAsyncResult = await first.NextAsync();
        Assert.Equal(42, nextAsyncResult);
        var nextAsyncResult3 = await second.NextAsync();
        Assert.Equal(42, nextAsyncResult3);
        await using var late = bus.Subscribe<int>("events");
        await bus.PublishAsync("events", 7);
        var nextAsyncResult4 = await first.NextAsync();
        Assert.Equal(7, nextAsyncResult4);
        var nextAsyncResult5 = await second.NextAsync();
        Assert.Equal(7, nextAsyncResult5);
        var nextAsyncResult6 = await late.NextAsync();
        Assert.Equal(7, nextAsyncResult6);
    }

    [Fact]
    public async Task DirectObjectReportsTypeMismatchAndJsonCrossesTypeBoundary()
    {
        var newGuidResult8 = Guid.NewGuid();
        await using var service = new WorkerMessageService(newGuidResult8);
        var direct = service.CreateMessageQueue(MessageChannelOptions.DirectObject);
        await direct.SendAsync("direct", "text");
        var mismatch = await Assert.ThrowsAsync<MessageTypeMismatchException>(() =>
        {
            var receiveAsyncResult7 = direct.ReceiveAsync<int>("direct");
            var asTaskResult = receiveAsyncResult7.AsTask();
            return asTaskResult;
        });
        Assert.Equal(MessageContractErrorCodes.TypeMismatch, mismatch.Code);
        var json = service.CreateMessageQueue(MessageChannelOptions.Json);
        var messageDto = new MessageDto("hello", 3);
        await json.SendAsync("json", messageDto);
        var result = await json.ReceiveAsync<MessageDto>("json");
        var messageDto9 = new MessageDto("hello", 3);
        Assert.Equal(messageDto9, result);
    }

    [Fact]
    public async Task BoundedQueueDropsOldestAndRunShutdownWakesReaders()
    {
        var newGuidResult10 = Guid.NewGuid();
        await using var service = new WorkerMessageService(newGuidResult10);
        var messageChannelOptions11 = new MessageChannelOptions
        {
            Capacity = 2,
            OverflowStrategy = MessageOverflowStrategy.DropOldest
        };
        var queue = service.CreateMessageQueue(messageChannelOptions11);
        await queue.SendAsync("bounded", 1);
        await queue.SendAsync("bounded", 2);
        await queue.SendAsync("bounded", 3);
        var receiveAsyncResult12 = await queue.ReceiveAsync<int>("bounded");
        Assert.Equal(2, receiveAsyncResult12);
        var receiveAsyncResult13 = await queue.ReceiveAsync<int>("bounded");
        Assert.Equal(3, receiveAsyncResult13);
        var receiveAsyncResult14 = queue.ReceiveAsync<string>("shutdown");
        var waiting = receiveAsyncResult14.AsTask();
        await service.DisposeAsync();
        var closed = await Assert.ThrowsAsync<MessageServiceException>(() => waiting);
        Assert.Equal(MessageErrorCodes.QueueClosed, closed.Code);
    }

    [Fact]
    public async Task ExternalDeliveryRequiresRegisteredJsonEndpointAndDeduplicates()
    {
        var registrations = new List<WorkerMessageEndpointDto>();
        var runId = Guid.NewGuid();
        await using var service = new WorkerMessageService(runId, registrations.Add);
        var messageChannelOptions15 = new MessageChannelOptions
        {
            ExternalIngress = true,
            ContractId = "test.text"
        };
        var queue = service.CreateMessageQueue(messageChannelOptions15);
        var receiveAsyncResult16 = queue.ReceiveAsync<string>("external");
        var waiting = receiveAsyncResult16.AsTask();
        Assert.Contains(registrations, endpoint => endpoint.Topic == "external");
        var messageId = Guid.NewGuid();
        var serializeResult = JsonSerializer.Serialize("hello");
        var delivery = new WorkerMessageDeliveryDto(WorkerProtocol.Version, runId, messageId, "external", WorkerMessageChannelKindDto.Queue, WorkerMessageSerializationModeDto.Json, "test.text", serializeResult, DateTimeOffset.UtcNow);
        var accepted = service.TryDeliver(delivery);
        var duplicate = service.TryDeliver(delivery);
        Assert.True(accepted.Accepted);
        Assert.False(accepted.Duplicate);
        Assert.True(duplicate.Accepted);
        Assert.True(duplicate.Duplicate);
        Assert.Equal("hello", await waiting);
    }

    [Fact]
    public async Task ConcurrentExternalDeliveryAcceptsAnIdempotencyKeyOnlyOnce()
    {
        var registrations = new List<WorkerMessageEndpointDto>();
        var runId = Guid.NewGuid();
        await using var service = new WorkerMessageService(runId, registrations.Add);
        var messageChannelOptions18 = new MessageChannelOptions
        {
            ExternalIngress = true,
            ContractId = "test.text",
            Capacity = 2
        };
        var queue = service.CreateMessageQueue(messageChannelOptions18);
        var receiveAsyncResult19 = queue.ReceiveAsync<string>("external.concurrent");
        var waiting = receiveAsyncResult19.AsTask();
        Assert.Contains(registrations, endpoint => endpoint.Topic == "external.concurrent");
        var newGuidResult20 = Guid.NewGuid();
        var serializeResult21 = JsonSerializer.Serialize("hello");
        var delivery = new WorkerMessageDeliveryDto(WorkerProtocol.Version, runId, newGuidResult20, "external.concurrent", WorkerMessageChannelKindDto.Queue, WorkerMessageSerializationModeDto.Json, "test.text", serializeResult21, DateTimeOffset.UtcNow);
        var selectResult23 = Enumerable.Range(0, 32)
            .Select(_ =>
            {
                var runResult17 = Task.Run(() =>
                {
                    var tryDeliverResult = service.TryDeliver(delivery);
                    return tryDeliverResult;
                });
                return runResult17;
            });
        var results = await Task.WhenAll(selectResult23);
        Assert.All(results, result =>
        {
            Assert.True(result.Accepted);
        });
        Assert.Single(results, result => !result.Duplicate);
        var countResult = results.Count(result => result.Duplicate);
        Assert.Equal(31, countResult);
        Assert.Equal("hello", await waiting);
    }

    private sealed record MessageDto(string Text, int Number);
}

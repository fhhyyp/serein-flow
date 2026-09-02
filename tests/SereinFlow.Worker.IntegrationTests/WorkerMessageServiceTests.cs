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
        await using var service = new WorkerMessageService(Guid.NewGuid());
        var options = new MessageChannelOptions { Capacity = 128 };
        var first = service.CreateMessageQueue(options);
        var second = service.CreateMessageQueue(options);
        var received = new ConcurrentBag<int>();

        var consumers = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(async () =>
            {
                for (var index = 0; index < 25; index++)
                    received.Add(await (index % 2 == 0 ? first : second).ReceiveAsync<int>("numbers"));
            }))
            .ToArray();

        for (var index = 0; index < 100; index++)
            await first.SendAsync("numbers", index);

        await Task.WhenAll(consumers);

        Assert.Equal(100, received.Count);
        Assert.Equal(Enumerable.Range(0, 100), received.OrderBy(static item => item));
    }

    [Fact]
    public async Task EventBusBroadcastsOnlyToSubscriptionsActiveAtPublishTime()
    {
        await using var service = new WorkerMessageService(Guid.NewGuid());
        var bus = service.CreateEventBus(new MessageChannelOptions { Capacity = 8 });
        await using var first = bus.Subscribe<int>("events");
        await using var second = bus.Subscribe<int>("events");

        await bus.PublishAsync("events", 42);

        Assert.Equal(42, await first.NextAsync());
        Assert.Equal(42, await second.NextAsync());

        await using var late = bus.Subscribe<int>("events");
        await bus.PublishAsync("events", 7);
        Assert.Equal(7, await first.NextAsync());
        Assert.Equal(7, await second.NextAsync());
        Assert.Equal(7, await late.NextAsync());
    }

    [Fact]
    public async Task DirectObjectReportsTypeMismatchAndJsonCrossesTypeBoundary()
    {
        await using var service = new WorkerMessageService(Guid.NewGuid());
        var direct = service.CreateMessageQueue(MessageChannelOptions.DirectObject);
        await direct.SendAsync("direct", "text");

        var mismatch = await Assert.ThrowsAsync<MessageTypeMismatchException>(
            () => direct.ReceiveAsync<int>("direct").AsTask());
        Assert.Equal("message.type_mismatch", mismatch.Code);

        var json = service.CreateMessageQueue(MessageChannelOptions.Json);
        await json.SendAsync("json", new MessageDto("hello", 3));
        var result = await json.ReceiveAsync<MessageDto>("json");
        Assert.Equal(new MessageDto("hello", 3), result);
    }

    [Fact]
    public async Task BoundedQueueDropsOldestAndRunShutdownWakesReaders()
    {
        await using var service = new WorkerMessageService(Guid.NewGuid());
        var queue = service.CreateMessageQueue(new MessageChannelOptions
        {
            Capacity = 2,
            OverflowStrategy = MessageOverflowStrategy.DropOldest
        });

        await queue.SendAsync("bounded", 1);
        await queue.SendAsync("bounded", 2);
        await queue.SendAsync("bounded", 3);

        Assert.Equal(2, await queue.ReceiveAsync<int>("bounded"));
        Assert.Equal(3, await queue.ReceiveAsync<int>("bounded"));

        var waiting = queue.ReceiveAsync<string>("shutdown").AsTask();
        await service.DisposeAsync();
        var closed = await Assert.ThrowsAsync<MessageServiceException>(() => waiting);
        Assert.Equal("message.queue_closed", closed.Code);
    }

    [Fact]
    public async Task ExternalDeliveryRequiresRegisteredJsonEndpointAndDeduplicates()
    {
        var registrations = new List<WorkerMessageEndpointDto>();
        var runId = Guid.NewGuid();
        await using var service = new WorkerMessageService(runId, registrations.Add);
        var queue = service.CreateMessageQueue(new MessageChannelOptions
        {
            ExternalIngress = true,
            ContractId = "test.text"
        });

        var waiting = queue.ReceiveAsync<string>("external").AsTask();
        Assert.Contains(registrations, endpoint => endpoint.Topic == "external");

        var messageId = Guid.NewGuid();
        var delivery = new WorkerMessageDeliveryDto(
            WorkerProtocol.Version,
            runId,
            messageId,
            "external",
            WorkerMessageChannelKindDto.Queue,
            WorkerMessageSerializationModeDto.Json,
            "test.text",
            JsonSerializer.Serialize("hello"),
            DateTimeOffset.UtcNow);

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
        var queue = service.CreateMessageQueue(new MessageChannelOptions
        {
            ExternalIngress = true,
            ContractId = "test.text",
            Capacity = 2
        });
        var waiting = queue.ReceiveAsync<string>("external.concurrent").AsTask();
        Assert.Contains(registrations, endpoint => endpoint.Topic == "external.concurrent");

        var delivery = new WorkerMessageDeliveryDto(
            WorkerProtocol.Version,
            runId,
            Guid.NewGuid(),
            "external.concurrent",
            WorkerMessageChannelKindDto.Queue,
            WorkerMessageSerializationModeDto.Json,
            "test.text",
            JsonSerializer.Serialize("hello"),
            DateTimeOffset.UtcNow);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 32).Select(_ => Task.Run(() => service.TryDeliver(delivery))));

        Assert.All(results, result => Assert.True(result.Accepted));
        Assert.Single(results, result => !result.Duplicate);
        Assert.Equal(31, results.Count(result => result.Duplicate));
        Assert.Equal("hello", await waiting);
    }

    private sealed record MessageDto(string Text, int Number);
}

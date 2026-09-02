using System.Text.Json;
using SereinFlow.Api;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Worker.Client;
using SereinFlow.Worker.Protocol;

namespace SereinFlow.Api.IntegrationTests;

public sealed class RunMessageDeliveryServiceTests
{
    [Fact]
    public async Task AcceptedDeliveryBuildsJsonWorkerMessageAndUsesStablePlainIdempotencyKey()
    {
        var run = CreateRunningRun();
        var worker = new RecordingWorkerClient();
        var service = new RunMessageDeliveryService(new TestRunStore(run), worker);
        var payload = JsonSerializer.Deserialize<JsonElement>("[null,\"订单\",42,true]");
        var command = new RunMessageDeliveryCommand(
            run.Id,
            "  order.created  ",
            payload,
            IdempotencyKey: "agent-call-1",
            ContractId: "order.created.v1",
            ChannelKind: WorkerMessageChannelKindDto.EventBus);

        var first = await service.DeliverAsync(command);
        var second = await service.DeliverAsync(command);

        Assert.True(first.IsAccepted);
        Assert.Equal(202, first.StatusCode);
        Assert.NotNull(first.Response);
        Assert.Equal(first.Response!.MessageId, second.Response!.MessageId);
        Assert.Equal("order.created", worker.Deliveries[0].Topic);
        Assert.Equal(WorkerMessageChannelKindDto.EventBus, worker.Deliveries[0].ChannelKind);
        Assert.Equal(WorkerMessageSerializationModeDto.Json, worker.Deliveries[0].SerializationMode);
        Assert.Equal("[null,\"订单\",42,true]", worker.Deliveries[0].PayloadJson);
    }

    [Fact]
    public async Task ServiceValidatesRunTopicPayloadChannelAndMessageIdBeforeCallingWorker()
    {
        var run = CreateRunningRun();
        var worker = new RecordingWorkerClient();
        var service = new RunMessageDeliveryService(new TestRunStore(run), worker);

        var missingRun = await service.DeliverAsync(new RunMessageDeliveryCommand(
            Guid.NewGuid(), "topic", JsonSerializer.SerializeToElement(new { value = 1 })));
        var inactiveRun = CreateRunningRun();
        inactiveRun.Cancel("test", DateTimeOffset.UtcNow);
        var inactive = await new RunMessageDeliveryService(new TestRunStore(inactiveRun), worker)
            .DeliverAsync(new RunMessageDeliveryCommand(inactiveRun.Id, "topic", JsonSerializer.SerializeToElement(new { value = 1 })));
        var invalidTopic = await service.DeliverAsync(new RunMessageDeliveryCommand(
            run.Id, "  ", JsonSerializer.SerializeToElement(new { value = 1 })));
        var missingPayload = await service.DeliverAsync(new RunMessageDeliveryCommand(
            run.Id, "topic", default));
        var invalidChannel = await service.DeliverAsync(new RunMessageDeliveryCommand(
            run.Id, "topic", JsonSerializer.SerializeToElement(new { value = 1 }), ChannelKind: (WorkerMessageChannelKindDto)99));
        var invalidMessageId = await service.DeliverAsync(new RunMessageDeliveryCommand(
            run.Id, "topic", JsonSerializer.SerializeToElement(new { value = 1 }), MessageId: "not-a-guid"));

        Assert.Equal((RunMessageDeliveryDisposition.RunNotFound, "run.not_found", 404),
            (missingRun.Disposition, missingRun.ErrorCode, missingRun.StatusCode));
        Assert.Equal((RunMessageDeliveryDisposition.WorkerNotActive, "worker.not_active", 409),
            (inactive.Disposition, inactive.ErrorCode, inactive.StatusCode));
        Assert.Equal("message.topic_invalid", invalidTopic.ErrorCode);
        Assert.Equal("message.payload_required", missingPayload.ErrorCode);
        Assert.Equal("message.channel_invalid", invalidChannel.ErrorCode);
        Assert.Equal("message.id_invalid", invalidMessageId.ErrorCode);
        Assert.Empty(worker.Deliveries);
    }

    [Fact]
    public async Task WorkerRejectionIsMappedToStableDispositionAndSafeMessage()
    {
        var run = CreateRunningRun();
        var worker = new RecordingWorkerClient
        {
            ResponseFactory = delivery => new WorkerMessageDeliveryResponseDto(
                WorkerMessageDeliveryStatusDto.Rejected,
                WorkerProtocol.Version,
                delivery.RunId,
                delivery.MessageId,
                delivery.Topic,
                "message.endpoint_forbidden",
                "a transport detail that must not cross the application boundary")
        };
        var service = new RunMessageDeliveryService(new TestRunStore(run), worker);

        var result = await service.DeliverAsync(new RunMessageDeliveryCommand(
            run.Id,
            "topic",
            JsonSerializer.SerializeToElement("value")));

        Assert.Equal(RunMessageDeliveryDisposition.EndpointForbidden, result.Disposition);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("message.endpoint_forbidden", result.ErrorCode);
        Assert.DoesNotContain("transport detail", result.Message, StringComparison.Ordinal);
        Assert.Equal(result.Message, result.Response!.Message);
    }

    private static FlowRun CreateRunningRun()
    {
        var run = FlowRun.Start(Guid.NewGuid(), Guid.NewGuid(), 1, DateTimeOffset.UtcNow);
        run.MarkRunning(DateTimeOffset.UtcNow);
        return run;
    }

    private sealed class RecordingWorkerClient : IWorkerMessageRunClient
    {
        public List<WorkerMessageDeliveryDto> Deliveries { get; } = [];

        public Func<WorkerMessageDeliveryDto, WorkerMessageDeliveryResponseDto>? ResponseFactory { get; init; }

        public Task<WorkerMessageDeliveryResponseDto> DeliverMessageAsync(
            WorkerMessageDeliveryDto delivery,
            CancellationToken cancellationToken = default)
        {
            Deliveries.Add(delivery);
            return Task.FromResult(ResponseFactory?.Invoke(delivery) ?? new WorkerMessageDeliveryResponseDto(
                WorkerMessageDeliveryStatusDto.Accepted,
                WorkerProtocol.Version,
                delivery.RunId,
                delivery.MessageId,
                delivery.Topic,
                null,
                null));
        }
    }

    private sealed class TestRunStore(FlowRun? run) : IFlowRunStore
    {
        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, FlowRunExecutionOptions options, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FlowRunAdmissionResult> TryCreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, FlowRunExecutionOptions options, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<PendingFlowRun>> ListPendingAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PendingFlowRun>>([]);

        public Task<IReadOnlyList<FlowRun>> ListAsync(FlowRunQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FlowRun>>(run is null ? [] : [run]);

        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FlowRun?> FindAsync(Guid runId, CancellationToken cancellationToken = default)
            => Task.FromResult<FlowRun?>(run?.Id == runId ? run : null);

        public Task<FlowDefinitionDto?> GetSnapshotAsync(Guid runId, CancellationToken cancellationToken = default)
            => Task.FromResult<FlowDefinitionDto?>(null);

        public Task<bool> SaveAsync(FlowRun value, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public FlowRun CreateWithSnapshot(FlowRun value, FlowDefinitionDto flow)
            => throw new NotSupportedException();

        public FlowRun? Find(Guid runId)
            => run?.Id == runId ? run : null;

        public FlowDefinitionDto? GetSnapshot(Guid runId)
            => null;

        public bool Save(FlowRun value)
            => false;
    }
}

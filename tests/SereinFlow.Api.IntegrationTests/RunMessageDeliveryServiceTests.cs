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
        var testRunStore = new TestRunStore(run);
        var service = new RunMessageDeliveryService(testRunStore, worker);
        var payload = JsonSerializer.Deserialize<JsonElement>("[null,\"订单\",42,true]");
        var command = new RunMessageDeliveryCommand(run.Id, "  order.created  ", payload, IdempotencyKey: "agent-call-1", ContractId: "order.created.v1", ChannelKind: WorkerMessageChannelKindDto.EventBus);
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
        var testRunStore1 = new TestRunStore(run);
        var service = new RunMessageDeliveryService(testRunStore1, worker);
        var newGuidResult = Guid.NewGuid();
        var serializeToElementResult = JsonSerializer.SerializeToElement(new { value = 1 });
        var runMessageDeliveryCommand = new RunMessageDeliveryCommand(newGuidResult, "topic", serializeToElementResult);
        var missingRun = await service.DeliverAsync(runMessageDeliveryCommand);
        var inactiveRun = CreateRunningRun();
        inactiveRun.Cancel("test", DateTimeOffset.UtcNow);
        var testRunStore2 = new TestRunStore(inactiveRun);
        var runMessageDeliveryService = new RunMessageDeliveryService(testRunStore2, worker);
        var serializeToElementResult3 = JsonSerializer.SerializeToElement(new { value = 1 });
        var runMessageDeliveryCommand4 = new RunMessageDeliveryCommand(inactiveRun.Id, "topic", serializeToElementResult3);
        var inactive = await runMessageDeliveryService.DeliverAsync(runMessageDeliveryCommand4);
        var serializeToElementResult5 = JsonSerializer.SerializeToElement(new { value = 1 });
        var runMessageDeliveryCommand6 = new RunMessageDeliveryCommand(run.Id, "  ", serializeToElementResult5);
        var invalidTopic = await service.DeliverAsync(runMessageDeliveryCommand6);
        var runMessageDeliveryCommand7 = new RunMessageDeliveryCommand(run.Id, "topic", default);
        var missingPayload = await service.DeliverAsync(runMessageDeliveryCommand7);
        var serializeToElementResult8 = JsonSerializer.SerializeToElement(new { value = 1 });
        var runMessageDeliveryCommand9 = new RunMessageDeliveryCommand(run.Id, "topic", serializeToElementResult8, ChannelKind: (WorkerMessageChannelKindDto)99);
        var invalidChannel = await service.DeliverAsync(runMessageDeliveryCommand9);
        var serializeToElementResult10 = JsonSerializer.SerializeToElement(new { value = 1 });
        var runMessageDeliveryCommand11 = new RunMessageDeliveryCommand(run.Id, "topic", serializeToElementResult10, MessageId: "not-a-guid");
        var invalidMessageId = await service.DeliverAsync(runMessageDeliveryCommand11);
        Assert.Equal((RunMessageDeliveryDisposition.RunNotFound, RunErrorCodes.NotFound, 404), (missingRun.Disposition, missingRun.ErrorCode, missingRun.StatusCode));
        Assert.Equal((RunMessageDeliveryDisposition.WorkerNotActive, WorkerErrorCodes.NotActive, 409), (inactive.Disposition, inactive.ErrorCode, inactive.StatusCode));
        Assert.Equal(MessageErrorCodes.TopicInvalid, invalidTopic.ErrorCode);
        Assert.Equal(MessageErrorCodes.PayloadRequired, missingPayload.ErrorCode);
        Assert.Equal(MessageErrorCodes.ChannelInvalid, invalidChannel.ErrorCode);
        Assert.Equal(MessageErrorCodes.IdInvalid, invalidMessageId.ErrorCode);
        Assert.Empty(worker.Deliveries);
    }

    [Fact]
    public async Task WorkerRejectionIsMappedToStableDispositionAndSafeMessage()
    {
        var run = CreateRunningRun();
        var worker = new RecordingWorkerClient
        {
            ResponseFactory = delivery =>
            {
                var workerMessageDeliveryResponseDto = new WorkerMessageDeliveryResponseDto(WorkerMessageDeliveryStatusDto.Rejected, WorkerProtocol.Version, delivery.RunId, delivery.MessageId, delivery.Topic, MessageErrorCodes.EndpointForbidden, "a transport detail that must not cross the application boundary");
                return workerMessageDeliveryResponseDto;
            }
        };
        var testRunStore12 = new TestRunStore(run);
        var service = new RunMessageDeliveryService(testRunStore12, worker);
        var serializeToElementResult13 = JsonSerializer.SerializeToElement("value");
        var runMessageDeliveryCommand14 = new RunMessageDeliveryCommand(run.Id, "topic", serializeToElementResult13);
        var result = await service.DeliverAsync(runMessageDeliveryCommand14);
        Assert.Equal(RunMessageDeliveryDisposition.EndpointForbidden, result.Disposition);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(MessageErrorCodes.EndpointForbidden, result.ErrorCode);
        Assert.DoesNotContain("transport detail", result.Message, StringComparison.Ordinal);
        Assert.Equal(result.Message, result.Response!.Message);
    }

    private static FlowRun CreateRunningRun()
    {
        var newGuidResult15 = Guid.NewGuid();
        var newGuidResult16 = Guid.NewGuid();
        var run = FlowRun.Start(newGuidResult15, newGuidResult16, 1, DateTimeOffset.UtcNow);
        run.MarkRunning(DateTimeOffset.UtcNow);
        return run;
    }

    private sealed class RecordingWorkerClient : IWorkerMessageRunClient
    {
        public List<WorkerMessageDeliveryDto> Deliveries { get; } = [];
        public Func<WorkerMessageDeliveryDto, WorkerMessageDeliveryResponseDto>? ResponseFactory { get; init; }

        public Task<WorkerMessageDeliveryResponseDto> DeliverMessageAsync(WorkerMessageDeliveryDto delivery, CancellationToken cancellationToken = default)
        {
            Deliveries.Add(delivery);
            var evaluatedValue = ResponseFactory?.Invoke(delivery) ?? new WorkerMessageDeliveryResponseDto(WorkerMessageDeliveryStatusDto.Accepted, WorkerProtocol.Version, delivery.RunId, delivery.MessageId, delivery.Topic, null, null);
            var fromResultResult = Task.FromResult(evaluatedValue);
            return fromResultResult;
        }
    }

    private sealed class TestRunStore(FlowRun? run) : IFlowRunStore
    {
        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, FlowRunExecutionOptions options, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FlowRunAdmissionResult> TryCreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, FlowRunExecutionOptions options, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<PendingFlowRun>> ListPendingAsync(CancellationToken cancellationToken = default)
        {
            var fromResultResult17 = Task.FromResult<IReadOnlyList<PendingFlowRun>>([]);
            return fromResultResult17;
        }

        public Task<IReadOnlyList<FlowRun>> ListAsync(FlowRunQuery query, CancellationToken cancellationToken = default)
        {
            var fromResultResult18 = Task.FromResult<IReadOnlyList<FlowRun>>(run is null ? [] : [run]);
            return fromResultResult18;
        }

        public Task<FlowRun> CreateWithSnapshotAsync(FlowRun value, FlowDefinitionDto flow, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FlowRun?> FindAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var fromResultResult19 = Task.FromResult<FlowRun?>(run?.Id == runId ? run : null);
            return fromResultResult19;
        }

        public Task<FlowDefinitionDto?> GetSnapshotAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var fromResultResult20 = Task.FromResult<FlowDefinitionDto?>(null);
            return fromResultResult20;
        }

        public Task<bool> SaveAsync(FlowRun value, CancellationToken cancellationToken = default)
        {
            var fromResultResult21 = Task.FromResult(false);
            return fromResultResult21;
        }

        public FlowRun CreateWithSnapshot(FlowRun value, FlowDefinitionDto flow)
        {
            throw new NotSupportedException();
        }

        public FlowRun? Find(Guid runId) => run?.Id == runId ? run : null;
        public FlowDefinitionDto? GetSnapshot(Guid runId) => null;
        public bool Save(FlowRun value) => false;
    }
}

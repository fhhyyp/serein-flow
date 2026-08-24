using System.Text.Json;
using System.Text.Json.Serialization;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Runtime;
using SereinFlow.Runtime.Abstractions;
using SereinFlow.ScriptAdapter;
using SereinFlow.Worker.Protocol;

namespace SereinFlow.Worker.Runner;

internal static class Program
{
    private static Task<int> Main()
        => RunnerHost.RunAsync(Console.OpenStandardInput(), Console.OpenStandardOutput());
}

public static class RunnerHost
{
    public static async Task<int> RunAsync(Stream input, Stream output, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(input, leaveOpen: true);
        await using var writer = new WorkerMessageWriter(output);
        await writer.WriteAsync(WorkerMessage.Create(WorkerProtocolConstants.ReadyKind), cancellationToken);

        var handshake = await WorkerProtocolCodec.ReadAsync(reader, cancellationToken);
        if (handshake is null || handshake.Kind != WorkerProtocolConstants.HandshakeKind)
        {
            await SendErrorAsync(writer, "worker.handshake_required", "The runner requires a handshake before a run.", cancellationToken);
            return 2;
        }

        await writer.WriteAsync(WorkerMessage.Create(WorkerProtocolConstants.HandshakeAcceptedKind), cancellationToken);

        var runMessage = await WorkerProtocolCodec.ReadAsync(reader, cancellationToken);
        if (runMessage is null || runMessage.Kind != WorkerProtocolConstants.RunKind || runMessage.RunId is null)
        {
            await SendErrorAsync(writer, "worker.run_required", "The runner requires one run request.", cancellationToken);
            return 2;
        }

        var request = WorkerProtocolCodec.DeserializePayload<WorkerRunRequestDto>(runMessage);
        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var remaining = request.Deadline - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
            runCancellation.Cancel();
        else
            runCancellation.CancelAfter(remaining);

        var runTask = ExecuteRunAsync(request, writer, runCancellation.Token);
        var readTask = WorkerProtocolCodec.ReadAsync(reader, cancellationToken).AsTask();

        while (!runTask.IsCompleted)
        {
            var completed = await Task.WhenAny(runTask, readTask);
            if (completed == runTask)
                break;

            var message = await readTask;
            if (message is null)
            {
                runCancellation.Cancel();
                break;
            }

            if (message.Kind == WorkerProtocolConstants.CancelKind && message.RunId == request.RunId)
            {
                runCancellation.Cancel();
                await writer.WriteAsync(
                    WorkerMessage.Create(WorkerProtocolConstants.CancelAcknowledgedKind, runId: request.RunId),
                    cancellationToken);
            }
            else if (message.Kind == WorkerProtocolConstants.HeartbeatKind)
            {
                await writer.WriteAsync(
                    WorkerMessage.Create(WorkerProtocolConstants.HeartbeatAcknowledgedKind, runId: request.RunId),
                    cancellationToken);
            }

            readTask = WorkerProtocolCodec.ReadAsync(reader, cancellationToken).AsTask();
        }

        await runTask;
        return 0;
    }

    private static async Task ExecuteRunAsync(WorkerRunRequestDto request, WorkerMessageWriter writer, CancellationToken cancellationToken)
    {
        try
        {
            var definition = FlowDefinitionMapper.Map(request.DefinitionJson);
            await using var session = new FlowExecutionSession(request.RunId, cancellationToken);
            await writer.WriteAsync(
                WorkerMessage.Create(
                    WorkerProtocolConstants.EventKind,
                    WorkerProtocolCodec.SerializePayload(new WorkerEventEnvelopeDto(
                        WorkerProtocolConstants.Version,
                        request.RunId,
                        session.NextSequence(),
                        DateTimeOffset.UtcNow,
                        WorkerEventType.RunStarted,
                        null,
                        "{}")),
                    request.RunId),
                cancellationToken);

            var publisher = new WorkerEventPublisher(writer, request.RunId);
            var executors = new NodeExecutorRegistry([
                new SuccessfulActionExecutor(),
                new SereinScriptNodeExecutor()]);
            var runner = new FlowRunner(new ExecutionPlanBuilder(), executors, publisher);
            var result = await runner.RunAsync(definition, session, cancellationToken);
            var status = result.IsSuccess ? FlowRunStatusDto.Succeeded : FlowRunStatusDto.Failed;
            await writer.WriteAsync(
                WorkerMessage.Create(
                    WorkerProtocolConstants.ResultKind,
                    WorkerProtocolCodec.SerializePayload(new WorkerRunResultDto(
                        WorkerProtocolConstants.Version,
                        request.RunId,
                        status,
                        result.ErrorCode,
                        result.ErrorMessage)),
                    request.RunId),
                CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            await writer.WriteAsync(
                WorkerMessage.Create(
                    WorkerProtocolConstants.ResultKind,
                    WorkerProtocolCodec.SerializePayload(new WorkerRunResultDto(
                        WorkerProtocolConstants.Version,
                        request.RunId,
                        FlowRunStatusDto.Cancelled,
                        "worker.cancelled",
                        "The worker run was cancelled.")),
                    request.RunId),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            await SendErrorAsync(writer, "worker.run_failed", exception.Message, CancellationToken.None, request.RunId);
        }
    }

    private static ValueTask SendErrorAsync(WorkerMessageWriter writer, string code, string message, CancellationToken cancellationToken, Guid? runId = null)
        => writer.WriteAsync(
            WorkerMessage.Create(
                WorkerProtocolConstants.ErrorKind,
                WorkerProtocolCodec.SerializePayload(new WorkerErrorDto(code, message)),
                runId),
            cancellationToken);

    private sealed class WorkerEventPublisher(WorkerMessageWriter writer, Guid runId) : IRunEventPublisher
    {
        public async ValueTask PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken)
        {
            var eventType = runtimeEvent.Type switch
            {
                "run.started" => WorkerEventType.RunStarted,
                "node.started" => WorkerEventType.NodeStarted,
                "node.completed" => WorkerEventType.NodeCompleted,
                "node.failed" => WorkerEventType.NodeFailed,
                _ => WorkerEventType.Log
            };
            var payload = JsonSerializer.Serialize(runtimeEvent.Payload);
            var envelope = new WorkerEventEnvelopeDto(
                WorkerProtocolConstants.Version,
                runId,
                runtimeEvent.Sequence,
                runtimeEvent.Timestamp,
                eventType,
                runtimeEvent.NodeId,
                payload);
            await writer.WriteAsync(
                WorkerMessage.Create(WorkerProtocolConstants.EventKind, WorkerProtocolCodec.SerializePayload(envelope), runId, sequence: runtimeEvent.Sequence),
                cancellationToken);
        }
    }

    private sealed class SuccessfulActionExecutor : INodeExecutor
    {
        public NodeType NodeType => NodeType.Action;

        public ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
            => ValueTask.FromResult(NodeExecutionResult.Success());
    }
}

public static class FlowDefinitionMapper
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static FlowDefinition Map(string definitionJson)
    {
        var dto = JsonSerializer.Deserialize<FlowDefinitionDto>(definitionJson, Options)
            ?? throw new InvalidOperationException("Flow definition payload is empty.");
        return FlowDefinition.Create(
            dto.Id,
            dto.Version,
            dto.Canvases.Select(MapCanvas),
            dto.EntryNodeId,
            dto.SchemaVersion,
            dto.Checksum);
    }

    private static CanvasDefinition MapCanvas(CanvasDto dto)
        => CanvasDefinition.Create(dto.Id, (CanvasLifecycle)dto.Lifecycle, dto.Nodes.Select(MapNode), dto.Connections.Select(MapConnection));

    private static NodeDefinition MapNode(NodeDto dto)
        => NodeDefinition.Create(
            dto.Id,
            (NodeType)dto.Type,
            dto.DisplayName,
            new NodePosition(dto.X, dto.Y),
            dto.Ports.Select(port => new PortDefinition(port.Id, port.Name, Enum.Parse<PortDirection>(port.Direction, true), port.Required)),
            dto.Parameters.Select(parameter => new NodeParameterDefinition(parameter.Name, parameter.ValueJson, (DataSource)parameter.Source, parameter.Required)),
            dto.Script is null ? null : ScriptNodeDefinition.Create(
                dto.Script.NodeId,
                dto.Script.Source,
                dto.Script.LanguageVersion,
                dto.Script.SourceHash,
                dto.Script.Inputs.Select(input => new ScriptValueContract(input.Name, input.ValueKind, input.Required)),
                dto.Script.Outputs.Select(output => new ScriptValueContract(output.Name, output.ValueKind, output.Required))));

    private static ConnectionDefinition MapConnection(ConnectionDto dto)
        => dto.Kind == ConnectionKindDto.Execution
            ? ConnectionDefinition.Execution(dto.FromNodeId, dto.FromPortId, dto.ToNodeId, dto.ToPortId, (ExecutionBranch)(dto.Branch ?? ExecutionBranchDto.Success), dto.Priority, dto.Id)
            : ConnectionDefinition.Data(dto.FromNodeId, dto.FromPortId, dto.ToNodeId, dto.ToPortId, (DataSource)(dto.DataSource ?? DataSourceDto.Literal), dto.Priority, dto.Id);
}

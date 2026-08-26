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
        // stdout is the framed Worker protocol stream. Third-party DLLs and
        // script runtimes must never be able to write diagnostic text into it,
        // otherwise the supervisor will try to parse that text as JSON.
        // stdout 是 Worker 协议专用流；外部 DLL 或脚本不得向其中写入诊断文本，否则 Supervisor 会把文本误解析为 JSON。
        Console.SetOut(TextWriter.Null);
        using var reader = new StreamReader(input, leaveOpen: true);
        await using var writer = new WorkerMessageWriter(output);
        await writer.WriteAsync(WorkerMessage.Create(WorkerProtocolConstants.ReadyKind), cancellationToken);

        var handshake = await WorkerProtocolCodec.ReadAsync(reader, cancellationToken);
        if (handshake is null || handshake.Kind != WorkerProtocolConstants.HandshakeKind)
        {
            await SendErrorAsync(writer, "worker.handshake_required", "The runner requires a handshake before a run. Worker Runner 必须先完成握手才能运行。", cancellationToken);
            return 2;
        }

        await writer.WriteAsync(WorkerMessage.Create(WorkerProtocolConstants.HandshakeAcceptedKind), cancellationToken);

        var runMessage = await WorkerProtocolCodec.ReadAsync(reader, cancellationToken);
        if (runMessage is null || runMessage.Kind != WorkerProtocolConstants.RunKind || runMessage.RunId is null)
        {
            await SendErrorAsync(writer, "worker.run_required", "The runner requires one run request. Worker Runner 需要一个运行请求。", cancellationToken);
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
            await using var session = new FlowExecutionSession(
                request.RunId,
                cancellationToken,
                request.MaxSteps > 0 ? request.MaxSteps : 10_000,
                request.MaxNodeVisits > 0 ? request.MaxNodeVisits : 1_000);
            if (request.ProjectInputs is not null)
            {
                foreach (var input in request.ProjectInputs)
                    session.Write($"project.{input.Key}", JsonElementToClr(input.Value));
            }
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

            await using var publisher = new WorkerEventPublisher(writer, request.RunId);
            ScriptArtifactStore? artifactStore = null;
            if (!string.IsNullOrWhiteSpace(request.ScriptArtifactRootPath))
            {
                artifactStore = new ScriptArtifactStore(request.ScriptArtifactRootPath);
                var scriptDefinitions = definition.Canvases
                    .SelectMany(canvas => canvas.Nodes)
                    .Where(static node => node.Type == NodeType.Script && node.Script is not null)
                    .Select(static node => node.Script!)
                    .ToArray();
                // Rebuild failures are surfaced by the Script node itself so
                // the flow can route them through its Error branch.
                // 缓存重建失败由脚本节点在执行时报告，从而进入 Error 分支。
                _ = artifactStore.RebuildProject(request.ProjectId ?? "default", scriptDefinitions);
            }

            session.Write("projectId", request.ProjectId ?? "default");
            await using var libraryRuntimeCache = new WorkerLibraryRuntimeCache(
                request.LibraryPackageRootPath,
                request.RunId,
                request.AllowedLibraryIds);
            var executors = new NodeExecutorRegistry([
                new LibraryNodeExecutor(NodeType.Action, libraryRuntimeCache),
                new LibraryNodeExecutor(NodeType.Flipflop, libraryRuntimeCache),
                new SereinScriptNodeExecutor(artifactStore, request.ProjectId ?? "default"),
                new ConditionNodeExecutor(),
                new FlowCallNodeExecutor()]);
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
                        "The worker run was cancelled. Worker 运行已取消。")),
                    request.RunId),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            await SendErrorAsync(writer, "worker.run_failed", $"The worker run failed. Worker 运行失败。 {exception.Message}", CancellationToken.None, request.RunId);
        }
    }

    private static ValueTask SendErrorAsync(WorkerMessageWriter writer, string code, string message, CancellationToken cancellationToken, Guid? runId = null)
        => writer.WriteAsync(
            WorkerMessage.Create(
                WorkerProtocolConstants.ErrorKind,
                WorkerProtocolCodec.SerializePayload(new WorkerErrorDto(code, message)),
                runId),
            cancellationToken);

    private sealed class WorkerEventPublisher(WorkerMessageWriter writer, Guid runId) : IRunEventPublisher, IAsyncDisposable
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly SortedDictionary<long, WorkerEventEnvelopeDto> _pending = [];
        // RunStarted is written by ExecuteRunAsync before this publisher is
        // created, so the first runtime event has sequence 2. Keep the
        // already-emitted sequence as the initial cursor.
        // RunStarted 在创建发布器前写出，因此第一个运行时事件通常是 2；
        // 以已写出的序列作为初始游标，避免等待不存在的序列 1。
        private long _lastWrittenSequence = 1;

        public async ValueTask PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken)
        {
            var eventType = runtimeEvent.Type switch
            {
                "run.started" => WorkerEventType.RunStarted,
                "node.started" => WorkerEventType.NodeStarted,
                "node.completed" => WorkerEventType.NodeCompleted,
                "node.failed" => WorkerEventType.NodeFailed,
                "node.error" => WorkerEventType.NodeErrored,
                _ => WorkerEventType.Log
            };
            var payload = JsonSerializer.Serialize(runtimeEvent.Payload, SereinJsonSerialization.CreateWebOptions());
            var envelope = new WorkerEventEnvelopeDto(
                WorkerProtocolConstants.Version,
                runId,
                runtimeEvent.Sequence,
                runtimeEvent.Timestamp,
                eventType,
                runtimeEvent.NodeId,
                payload);
            await _gate.WaitAsync(cancellationToken);
            try
            {
                _pending[runtimeEvent.Sequence] = envelope;
                while (_pending.Remove(_lastWrittenSequence + 1, out var next))
                {
                    await writer.WriteAsync(
                        WorkerMessage.Create(WorkerProtocolConstants.EventKind, WorkerProtocolCodec.SerializePayload(next), runId, sequence: next.Sequence),
                        cancellationToken);
                    _lastWrittenSequence = next.Sequence;
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public ValueTask DisposeAsync()
        {
            _gate.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private static object? JsonElementToClr(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToClr).ToArray(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(item => item.Name, item => JsonElementToClr(item.Value), StringComparer.Ordinal),
            _ => null
        };
}

public static class FlowDefinitionMapper
{
    private static readonly JsonSerializerOptions Options = SereinJsonSerialization.CreateWebOptions(options =>
    {
        options.PropertyNameCaseInsensitive = true;
        options.Converters.Add(new JsonStringEnumConverter());
    });

    public static FlowDefinition Map(string definitionJson)
    {
        var dto = JsonSerializer.Deserialize<FlowDefinitionDto>(definitionJson, Options)
            ?? throw new InvalidOperationException("Flow definition payload is empty. 流程定义载荷为空。");
        return FlowDefinition.Create(
            dto.Id,
            dto.Version,
            dto.Canvases.Select(MapCanvas),
            dto.EntryNodeId,
            dto.SchemaVersion,
            dto.Checksum,
            dto.RunPolicy is null ? null : new FlowRunPolicy((FlowConcurrencyMode)dto.RunPolicy.ConcurrencyMode));
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
            dto.Parameters.Select(parameter => new NodeParameterDefinition(
                parameter.Name,
                parameter.ValueJson,
                (DataSource)parameter.Source,
                parameter.Required,
                parameter.Ui?.Id,
                parameter.Ui?.ProjectInputKey,
                parameter.Ui?.Expression,
                parameter.Ui?.SourceNodeId,
                parameter.Ui?.SourcePortId,
                parameter.Ui?.ValueKind)),
            dto.Script is null ? null : ScriptNodeDefinition.Create(
                dto.Script.NodeId,
                dto.Script.Source,
                dto.Script.LanguageVersion,
                dto.Script.SourceHash,
                dto.Script.Inputs.Select(input => new ScriptValueContract(input.Name, input.ValueKind, input.Required)),
                dto.Script.Outputs.Select(output => new ScriptValueContract(output.Name, output.ValueKind, output.Required))),
            dto.Ui is null
                ? null
                : new NodeRuntimeDefinition(
                    dto.Ui.LibraryId,
                    dto.Ui.ClassName,
                    dto.Ui.MethodName,
                    dto.Ui.DllName,
                    dto.Ui.DllVersion,
                    dto.Ui.ReturnType,
                    dto.Ui.TargetNodeId,
                    Guid.TryParse(dto.Ui.TargetFlowId, out var targetFlowId) ? targetFlowId : null,
                    dto.Ui.IsAwaitable ?? false,
                    dto.Ui.StaticReturnType,
                    dto.Ui.IsDynamicReturnType ?? false));

    private static ConnectionDefinition MapConnection(ConnectionDto dto)
    {
        if (dto.Kind == ConnectionKindDto.Execution)
        {
            if (dto.Branch is null)
                throw new ArgumentException("Execution connections must declare Success, Failure, or Error. 流程连接必须声明 Success、Failure 或 Error 分支。", nameof(dto));

            if (!Enum.IsDefined(dto.Branch.Value))
                throw new ArgumentException("Execution connection branch is invalid. 流程连接分支无效。", nameof(dto));

            return ConnectionDefinition.Execution(
                dto.FromNodeId,
                dto.FromPortId,
                dto.ToNodeId,
                dto.ToPortId,
                (ExecutionBranch)dto.Branch.Value,
                dto.Priority,
                dto.Id);
        }

        return ConnectionDefinition.Data(
            dto.FromNodeId,
            dto.FromPortId,
            dto.ToNodeId,
            dto.ToPortId,
            (DataSource)(dto.DataSource ?? DataSourceDto.Literal),
            dto.Priority,
            dto.Id);
    }
}

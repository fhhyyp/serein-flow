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
        using var deadlineCancellation = new CancellationTokenSource();
        var remaining = request.Deadline - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
            deadlineCancellation.Cancel();
        else
            deadlineCancellation.CancelAfter(remaining);
        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineCancellation.Token);

        var debugController = request.Debug is null
            ? null
            : new DebugRunController(request.RunId, request.Debug);
        var runTask = ExecuteRunAsync(request, writer, runCancellation.Token, deadlineCancellation.Token, debugController);
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
            else if (debugController is not null
                && message.RunId == request.RunId
                && message.Kind is WorkerProtocolConstants.DebugContinueKind
                    or WorkerProtocolConstants.DebugStepKind
                    or WorkerProtocolConstants.DebugStopKind)
            {
                var command = WorkerProtocolCodec.DeserializePayload<WorkerDebugCommandDto>(message);
                var accepted = message.Kind switch
                {
                    WorkerProtocolConstants.DebugContinueKind => debugController.TryContinue(command),
                    WorkerProtocolConstants.DebugStepKind => debugController.TryStep(command),
                    WorkerProtocolConstants.DebugStopKind => debugController.TryStop(command),
                    _ => false
                };
                if (accepted && message.Kind == WorkerProtocolConstants.DebugStopKind)
                    runCancellation.Cancel();
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

    private static async Task ExecuteRunAsync(
        WorkerRunRequestDto request,
        WorkerMessageWriter writer,
        CancellationToken cancellationToken,
        CancellationToken deadlineCancellationToken,
        DebugRunController? debugController)
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
                new FlowCallNodeExecutor()]);
            var executionGate = debugController?.CreateGate(session, publisher);
            var invocationScheduler = request.Debug is null
                ? null
                : new DebugInvocationScheduler(request.Debug.MaxQueuedFlipflopTriggers);
            var runner = new FlowRunner(
                new ExecutionPlanBuilder(),
                executors,
                publisher,
                executionGate: executionGate,
                debugInvocationScheduler: invocationScheduler);
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
            var timedOut = deadlineCancellationToken.IsCancellationRequested;
            await writer.WriteAsync(
                WorkerMessage.Create(
                    WorkerProtocolConstants.ResultKind,
                    WorkerProtocolCodec.SerializePayload(new WorkerRunResultDto(
                        WorkerProtocolConstants.Version,
                        request.RunId,
                        timedOut ? FlowRunStatusDto.TimedOut : FlowRunStatusDto.Cancelled,
                        timedOut ? "worker.timed_out" : "worker.cancelled",
                        timedOut
                            ? "The worker run timed out. Worker 运行已超时。"
                            : "The worker run was cancelled. Worker 运行已取消。")),
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
                "debug.paused" => WorkerEventType.DebugPaused,
                "debug.trigger.received" => WorkerEventType.DebugTriggerReceived,
                "debug.trigger.queued" => WorkerEventType.DebugTriggerQueued,
                "debug.trigger.admitted" => WorkerEventType.DebugTriggerAdmitted,
                "debug.trigger.rejected" => WorkerEventType.DebugTriggerRejected,
                "debug.trigger.completed" => WorkerEventType.DebugTriggerCompleted,
                "debug.trigger.failed" => WorkerEventType.DebugTriggerFailed,
                _ => WorkerEventType.Log
            };
            // Runtime sessions can retain ScriptLang.Value instances so a
            // following DLL node can convert them against its declared type.
            // The protocol boundary must receive only a safe audit projection.
            // 运行会话可保留 ScriptLang.Value 供后续 DLL 节点按声明类型转换；
            // 协议边界只能接收安全的审计投影。
            var payload = JsonSerializer.Serialize(
                ScriptValueConverter.ToAuditValue(runtimeEvent.Payload),
                SereinJsonSerialization.CreateWebOptions());
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

        public ValueTask PublishDebugPausedAsync(
            WorkerDebugPauseDto pause,
            long sequence,
            CancellationToken cancellationToken)
        {
            var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["debugSessionId"] = pause.DebugSessionId,
                ["runId"] = pause.RunId,
                ["nodeId"] = pause.NodeId,
                ["nodeType"] = pause.NodeType,
                ["step"] = pause.Step,
                ["inputs"] = ScriptValueConverter.ToAuditValue(pause.Inputs),
                ["frameDepth"] = pause.FrameDepth,
                ["triggerInvocationId"] = pause.TriggerInvocationId
            };
            return PublishEnvelopeAsync(
                new WorkerEventEnvelopeDto(
                    WorkerProtocolConstants.Version,
                    runId,
                    sequence,
                    DateTimeOffset.UtcNow,
                    WorkerEventType.DebugPaused,
                    pause.NodeId,
                    JsonSerializer.Serialize(
                        payload,
                        SereinJsonSerialization.CreateWebOptions())),
                cancellationToken);
        }

        private async ValueTask PublishEnvelopeAsync(
            WorkerEventEnvelopeDto envelope,
            CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                _pending[envelope.Sequence] = envelope;
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

    private sealed class DebugRunController
    {
        private readonly Guid _runId;
        private readonly WorkerDebugOptionsDto _options;
        private readonly object _sync = new();
        private DebugExecutionGate? _gate;
        private long _lastCommandSequence;

        public DebugRunController(Guid runId, WorkerDebugOptionsDto options)
        {
            if (options.DebugSessionId == Guid.Empty)
            {
                throw new ArgumentException(
                    "The debug session ID cannot be empty. 调试会话 ID 不能为空。",
                    nameof(options));
            }

            _runId = runId;
            _options = options;
        }

        public DebugExecutionGate CreateGate(FlowExecutionSession session, WorkerEventPublisher publisher)
        {
            var gate = new DebugExecutionGate(_options.BreakpointNodeIds, (boundary, token) =>
            {
                var pause = new WorkerDebugPauseDto(
                    _options.DebugSessionId,
                    boundary.RunId,
                    boundary.NodeId,
                    boundary.NodeType.ToString(),
                    boundary.Step,
                    boundary.Inputs,
                    boundary.FrameDepth,
                    boundary.InvocationId);
                return publisher.PublishDebugPausedAsync(pause, session.NextSequence(), token);
            });
            lock (_sync)
                _gate = gate;
            return gate;
        }

        public bool TryContinue(WorkerDebugCommandDto command)
            => TryApply(command, static gate => gate.TryContinue());

        public bool TryStep(WorkerDebugCommandDto command)
            => TryApply(command, static gate => gate.TryStep());

        public bool TryStop(WorkerDebugCommandDto command)
        {
            if (command.ProtocolVersion != WorkerProtocolConstants.Version
                || command.RunId != _runId
                || command.DebugSessionId != _options.DebugSessionId
                || command.CommandSequence < 1)
            {
                return false;
            }

            lock (_sync)
            {
                if (command.CommandSequence <= _lastCommandSequence)
                    return false;

                // A listener may be blocked in WaitForTriggerAsync and have
                // no current boundary. Accepting Stop still lets the Runner
                // cancel that wait; if a boundary exists, release it too.
                // 监听器可能阻塞在 WaitForTriggerAsync 且不存在当前断点边界。Stop
                // 仍必须被接受以取消该等待；若边界存在，也一并释放。
                _gate?.TryCancel();
                _lastCommandSequence = command.CommandSequence;
                return true;
            }
        }

        private bool TryApply(WorkerDebugCommandDto command, Func<DebugExecutionGate, bool> apply)
        {
            if (command.ProtocolVersion != WorkerProtocolConstants.Version
                || command.RunId != _runId
                || command.DebugSessionId != _options.DebugSessionId
                || command.CommandSequence < 1)
            {
                return false;
            }

            lock (_sync)
            {
                if (command.CommandSequence <= _lastCommandSequence || _gate is null)
                    return false;

                if (!apply(_gate))
                    return false;

                _lastCommandSequence = command.CommandSequence;
                return true;
            }
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
                parameter.Ui?.ValueKind,
                parameter.Ui?.Description,
                parameter.Ui?.IsVariadic ?? false,
                parameter.Ui?.VariadicGroupId,
                parameter.Ui?.ElementType,
                ParseVariadicMode(parameter.Ui?.VariadicMode))),
            dto.Script is null ? null : ScriptNodeDefinition.Create(
                dto.Script.NodeId,
                dto.Script.Source,
                dto.Script.LanguageVersion,
                // SourceHash is presentation/cache metadata. The Worker must
                // derive it from the immutable source rather than reject a run
                // whose persisted DTO predates the server-side normalization.
                // SourceHash 是展示/缓存元数据；Worker 必须从不可变源代码重新计算它，
                // 不能因运行快照早于服务端规范化逻辑而拒绝执行。
                null,
                dto.Script.Inputs.Select(input => new ScriptValueContract(input.Name, input.ValueKind, input.Required, input.Id, input.Description)),
                dto.Script.Outputs.Select(output => new ScriptValueContract(output.Name, output.ValueKind, output.Required, output.Id, output.Description))),
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
                    dto.Ui.IsDynamicReturnType ?? false,
                    dto.Ui.TargetCanvasId,
                    dto.Ui.IsPublic ?? false,
                    dto.Ui.FlowCallParameterBindings?.Select(item => new FlowCallParameterBinding(item.CallParameterId, item.TargetParameterId)).ToArray(),
                    dto.Ui.LibraryNodeContractId));

    private static VariadicParameterMode? ParseVariadicMode(string? value)
        => Enum.TryParse<VariadicParameterMode>(value, ignoreCase: true, out var mode) ? mode : null;

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

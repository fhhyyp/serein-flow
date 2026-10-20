using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using SereinFlow.Contracts;
using SereinFlow.Library;
using SereinFlow.Domain;
using SereinFlow.Runtime;
using SereinFlow.Runtime.Abstractions;
using SereinFlow.ScriptAdapter;
using SereinFlow.Worker.Protocol;
using NodeType = SereinFlow.Domain.NodeType;

namespace SereinFlow.Worker.Runner;

internal static class Program
{
    private static async Task<int> Main()
    {
        var openStandardInputResult = Console.OpenStandardInput();
        var openStandardOutputResult = Console.OpenStandardOutput();
        await using var transport = new StdioWorkerTransport(openStandardInputResult, openStandardOutputResult);
        var runAsyncResult = await RunnerHost.RunAsync(transport);
        return runAsyncResult;
    }
}

public static class RunnerHost
{
    public static async Task<int> RunAsync(IWorkerTransport transport, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transport);
        // stdout is the framed Worker protocol stream. Third-party DLLs and
        // script runtimes must never be able to write diagnostic text into it,
        // otherwise the supervisor will try to parse that text as JSON.
        // stdout 是 Worker 协议专用流；外部 DLL 或脚本不得向其中写入诊断文本，否则 Supervisor 会把文本误解析为 JSON。
        Console.SetOut(TextWriter.Null);
        var createResult = WorkerMessage.Create(WorkerProtocolConstants.ReadyKind);
        await transport.SendAsync(createResult, cancellationToken);
        var handshake = await transport.ReceiveAsync(cancellationToken);
        if (handshake is null || handshake.Kind != WorkerProtocolConstants.HandshakeKind)
        {
            await SendErrorAsync(transport, WorkerErrorCodes.HandshakeRequired, "The runner requires a handshake before a run. Worker Runner 必须先完成握手才能运行。", cancellationToken);
            return 2;
        }

        var createResult1 = WorkerMessage.Create(WorkerProtocolConstants.HandshakeAcceptedKind);
        await transport.SendAsync(createResult1, cancellationToken);
        var runMessage = await transport.ReceiveAsync(cancellationToken);
        if (runMessage is null || runMessage.Kind != WorkerProtocolConstants.RunKind || runMessage.RunId is null)
        {
            await SendErrorAsync(transport, WorkerErrorCodes.RunRequired, "The runner requires one run request. Worker Runner 需要一个运行请求。", cancellationToken);
            return 2;
        }

        var request = WorkerProtocolCodec.DeserializePayload<WorkerRunRequestDto>(runMessage);
        using var deadlineCancellation = new CancellationTokenSource();
        var remaining = request.Deadline - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            deadlineCancellation.Cancel();
        }
        else
        {
            deadlineCancellation.CancelAfter(remaining);
        }

        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineCancellation.Token);
        await using var messageService = new WorkerMessageService(request.RunId, endpoint =>
        {
            SendEndpointRegistrationAsync(transport, endpoint);
        }, endpoint =>
        {
            SendEndpointUnregistrationAsync(transport, endpoint);
        });
        var debugController = request.Debug is null ? null : new DebugRunController(request.RunId, request.Debug);
        await using var session = new FlowExecutionSession(request.RunId, runCancellation.Token, request.MaxSteps > 0 ? request.MaxSteps : 10_000, request.MaxNodeVisits > 0 ? request.MaxNodeVisits : 1_000);
        var runTask = ExecuteRunAsync(request, transport, messageService, session, debugController, deadlineCancellation.Token, runCancellation.Token);
        await WorkerRunMessageLoop.RunAsync(request, transport, messageService, session.Variables, runTask, debugController, runCancellation, cancellationToken);
        return 0;
    }

    private static async Task ExecuteRunAsync(WorkerRunRequestDto request, IWorkerTransport transport, WorkerMessageService messageService, FlowExecutionSession session, DebugRunController? debugController, CancellationToken deadlineCancellationToken, CancellationToken cancellationToken)
    {
        try
        {
            var definition = FlowDefinitionMapper.Map(request.DefinitionJson);
            if (request.ProjectInputs is not null)
            {
                foreach (var input in request.ProjectInputs)
                {
                    var jsonElementToClrResult = JsonElementToClr(input.Value);
                    session.Write($"project.{input.Key}", jsonElementToClrResult);
                }
            }

            var nextSequenceResult = session.NextSequence();
            var workerEventEnvelopeDto = new WorkerEventEnvelopeDto(WorkerProtocolConstants.Version, request.RunId, nextSequenceResult, DateTimeOffset.UtcNow, WorkerEventType.RunStarted, null, "{}");
            var serializePayloadResult = WorkerProtocolCodec.SerializePayload(workerEventEnvelopeDto);
            var createResult2 = WorkerMessage.Create(WorkerProtocolConstants.EventKind, serializePayloadResult, request.RunId);
            await transport.SendAsync(createResult2, cancellationToken);
            await using var publisher = new WorkerEventPublisher(transport, request.RunId);
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
            await using var libraryRuntimeCache = new WorkerLibraryRuntimeCache(request.LibraryPackageRootPath, request.RunId, request.AllowedLibraryIds);
            var flowWorkpiece = new FileFlowWorkpiece(request.WorkpieceRootPath, request.RunId);
            await using var libraryServiceRuntime = new WorkerLibraryServiceRuntime(messageService, flowWorkpiece, session.Variables, libraryRuntimeCache.GetNativeLibraryLoader);
            IEnumerable<INodeExecutor> evaluatedValue = [new LibraryNodeExecutor(NodeType.Action, libraryRuntimeCache, libraryServiceRuntime), new LibraryNodeExecutor(NodeType.Flipflop, libraryRuntimeCache, libraryServiceRuntime), new SereinScriptNodeExecutor(artifactStore, request.ProjectId ?? "default"), new FlowCallNodeExecutor()];
            var executors = new NodeExecutorRegistry(evaluatedValue);
            var executionGate = debugController?.CreateGate(session, publisher);
            var invocationScheduler = request.Debug is null ? null : new DebugInvocationScheduler(request.Debug.MaxQueuedFlipflopTriggers);
            var executionPlanBuilder = new ExecutionPlanBuilder();
            var runner = new FlowRunner(executionPlanBuilder, executors, publisher, executionGate: executionGate, debugInvocationScheduler: invocationScheduler);
            var result = await runner.RunAsync(definition, session, cancellationToken);
            var status = result.IsSuccess ? FlowRunStatusDto.Succeeded : FlowRunStatusDto.Failed;
            var captureFinalResult = WorkerVariableService.CaptureFinal(request.RunId, session.Variables);
            var workerRunResultDto = new WorkerRunResultDto(WorkerProtocolConstants.Version, request.RunId, status, result.ErrorCode, result.ErrorMessage, captureFinalResult);
            var serializePayloadResult3 = WorkerProtocolCodec.SerializePayload(workerRunResultDto);
            var createResult4 = WorkerMessage.Create(WorkerProtocolConstants.ResultKind, serializePayloadResult3, request.RunId);
            await transport.SendAsync(createResult4, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            var timedOut = deadlineCancellationToken.IsCancellationRequested;
            var captureFinalResult5 = WorkerVariableService.CaptureFinal(request.RunId, session.Variables);
            var workerRunResultDto6 = new WorkerRunResultDto(WorkerProtocolConstants.Version, request.RunId, timedOut ? FlowRunStatusDto.TimedOut : FlowRunStatusDto.Cancelled, timedOut ? WorkerErrorCodes.TimedOut : WorkerErrorCodes.Cancelled, timedOut ? "The worker run timed out. Worker 运行已超时。" : "The worker run was cancelled. Worker 运行已取消。", captureFinalResult5);
            var serializePayloadResult7 = WorkerProtocolCodec.SerializePayload(workerRunResultDto6);
            var createResult8 = WorkerMessage.Create(WorkerProtocolConstants.ResultKind, serializePayloadResult7, request.RunId);
            await transport.SendAsync(createResult8, CancellationToken.None);
        }
        catch (Exception exception)
        {
            var captureFinalResult9 = WorkerVariableService.CaptureFinal(request.RunId, session.Variables);
            await SendErrorAsync(transport, WorkerErrorCodes.RunFailed, $"The worker run failed. Worker 运行失败。 {exception.Message}", CancellationToken.None, request.RunId, captureFinalResult9);
        }
    }

    private static void SendEndpointRegistrationAsync(IWorkerTransport transport, WorkerMessageEndpointDto endpoint)
    {
        _ = SendEndpointRegistrationCoreAsync(transport, endpoint);
    }

    private static async Task SendEndpointRegistrationCoreAsync(IWorkerTransport transport, WorkerMessageEndpointDto endpoint)
    {
        try
        {
            var serializePayloadResult10 = WorkerProtocolCodec.SerializePayload(endpoint);
            var createResult11 = WorkerMessage.Create(WorkerProtocolConstants.MessageRegisterKind, serializePayloadResult10, endpoint.RunId);
            await transport.SendAsync(createResult11, CancellationToken.None);
        }
        catch
        {
            // Registration is best effort while the run is shutting down.
            // 运行关闭期间端点注册属于尽力而为。
        }
    }

    private static void SendEndpointUnregistrationAsync(IWorkerTransport transport, WorkerMessageEndpointDto endpoint)
    {
        _ = SendEndpointUnregistrationCoreAsync(transport, endpoint);
    }

    private static async Task SendEndpointUnregistrationCoreAsync(IWorkerTransport transport, WorkerMessageEndpointDto endpoint)
    {
        try
        {
            var serializePayloadResult12 = WorkerProtocolCodec.SerializePayload(endpoint);
            var createResult13 = WorkerMessage.Create(WorkerProtocolConstants.MessageUnregisterKind, serializePayloadResult12, endpoint.RunId);
            await transport.SendAsync(createResult13, CancellationToken.None);
        }
        catch
        {
        }
    }

    private static ValueTask SendErrorAsync(IWorkerTransport transport, string code, string message, CancellationToken cancellationToken, Guid? runId = null, RunVariableSnapshotDto? finalVariables = null)
    {
        var workerErrorDto = new WorkerErrorDto(code, message, finalVariables);
        var serializePayloadResult14 = WorkerProtocolCodec.SerializePayload(workerErrorDto);
        var createResult15 = WorkerMessage.Create(WorkerProtocolConstants.ErrorKind, serializePayloadResult14, runId);
        var sendAsyncResult = transport.SendAsync(createResult15, cancellationToken);
        return sendAsyncResult;
    }

    private static object? JsonElementToClr(JsonElement element)
    {
        object? evaluatedValue18 = element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.Array => resolveToArrayResult(),
            JsonValueKind.Object => resolveToDictionaryResult(),
            _ => null
        };
        return evaluatedValue18;
        object resolveToArrayResult()
        {
            var toArrayResult = element.EnumerateArray()
                .Select(JsonElementToClr)
                .ToArray();
            return toArrayResult;
        }

        object resolveToDictionaryResult()
        {
            var toDictionaryResult = element.EnumerateObject()
                .ToDictionary(item => item.Name, item =>
                {
                    var jsonElementToClrResult16 = JsonElementToClr(item.Value);
                    return jsonElementToClrResult16;
                }, StringComparer.Ordinal);
            return toDictionaryResult;
        }
    }
}

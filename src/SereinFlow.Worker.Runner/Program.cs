using System.Text.Json;
using System.Text.Json.Serialization;
using SereinFlow.Contracts;
using SereinFlow.Library;
using SereinFlow.Domain;
using SereinFlow.Runtime;
using SereinFlow.Runtime.Abstractions;
using SereinFlow.ScriptAdapter;
using SereinFlow.Worker.Protocol;

namespace SereinFlow.Worker.Runner;

internal static class Program
{
    private static async Task<int> Main()
    {
        await using var transport = new StdioWorkerTransport(
            Console.OpenStandardInput(),
            Console.OpenStandardOutput());
        return await RunnerHost.RunAsync(transport);
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
        await transport.SendAsync(WorkerMessage.Create(WorkerProtocolConstants.ReadyKind), cancellationToken);

        var handshake = await transport.ReceiveAsync(cancellationToken);
        if (handshake is null || handshake.Kind != WorkerProtocolConstants.HandshakeKind)
        {
            await SendErrorAsync(transport, WorkerErrorCodes.HandshakeRequired, "The runner requires a handshake before a run. Worker Runner 必须先完成握手才能运行。", cancellationToken);
            return 2;
        }

        await transport.SendAsync(WorkerMessage.Create(WorkerProtocolConstants.HandshakeAcceptedKind), cancellationToken);

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
            deadlineCancellation.Cancel();
        else
            deadlineCancellation.CancelAfter(remaining);
        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineCancellation.Token);

        await using var messageService = new WorkerMessageService(
            request.RunId,
            endpoint => SendEndpointRegistrationAsync(transport, endpoint),
            endpoint => SendEndpointUnregistrationAsync(transport, endpoint));

        var debugController = request.Debug is null
            ? null
            : new DebugRunController(request.RunId, request.Debug);
        var runTask = ExecuteRunAsync(request, transport, messageService,  debugController, deadlineCancellation.Token, runCancellation.Token);
        await WorkerRunMessageLoop.RunAsync(
            request,
            transport,
            messageService,
            runTask,
            debugController,
            runCancellation,
            cancellationToken);
        return 0;
    }

    private static async Task ExecuteRunAsync(
        WorkerRunRequestDto request,
        IWorkerTransport transport,
        WorkerMessageService messageService,
        DebugRunController? debugController,
        CancellationToken deadlineCancellationToken,
        CancellationToken cancellationToken)
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
            await transport.SendAsync(
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
            await using var libraryRuntimeCache = new WorkerLibraryRuntimeCache(
                request.LibraryPackageRootPath,
                request.RunId,
                request.AllowedLibraryIds);
            var flowWorkpiece = new FileFlowWorkpiece(request.WorkpieceRootPath, request.RunId);
            await using var libraryServiceRuntime = new WorkerLibraryServiceRuntime(
                messageService,
                flowWorkpiece,
                libraryRuntimeCache.GetNativeLibraryLoader);
            var executors = new NodeExecutorRegistry([
                new LibraryNodeExecutor(NodeType.Action, libraryRuntimeCache, libraryServiceRuntime),
                new LibraryNodeExecutor(NodeType.Flipflop, libraryRuntimeCache, libraryServiceRuntime),
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
            await transport.SendAsync(
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
            await transport.SendAsync(
                WorkerMessage.Create(
                    WorkerProtocolConstants.ResultKind,
                    WorkerProtocolCodec.SerializePayload(new WorkerRunResultDto(
                        WorkerProtocolConstants.Version,
                        request.RunId,
                        timedOut ? FlowRunStatusDto.TimedOut : FlowRunStatusDto.Cancelled,
                        timedOut ? WorkerErrorCodes.TimedOut : WorkerErrorCodes.Cancelled,
                        timedOut
                            ? "The worker run timed out. Worker 运行已超时。"
                            : "The worker run was cancelled. Worker 运行已取消。")),
                    request.RunId),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            await SendErrorAsync(transport, WorkerErrorCodes.RunFailed, $"The worker run failed. Worker 运行失败。 {exception.Message}", CancellationToken.None, request.RunId);
        }
    }

    private static void SendEndpointRegistrationAsync(
        IWorkerTransport transport,
        WorkerMessageEndpointDto endpoint)
        => _ = SendEndpointRegistrationCoreAsync(transport, endpoint);

    private static async Task SendEndpointRegistrationCoreAsync(
        IWorkerTransport transport,
        WorkerMessageEndpointDto endpoint)
    {
        try
        {
            await transport.SendAsync(
                WorkerMessage.Create(
                    WorkerProtocolConstants.MessageRegisterKind,
                    WorkerProtocolCodec.SerializePayload(endpoint),
                    endpoint.RunId),
                CancellationToken.None);
        }
        catch
        {
            // Registration is best effort while the run is shutting down.
            // 运行关闭期间端点注册属于尽力而为。
        }
    }

    private static void SendEndpointUnregistrationAsync(
        IWorkerTransport transport,
        WorkerMessageEndpointDto endpoint)
        => _ = SendEndpointUnregistrationCoreAsync(transport, endpoint);

    private static async Task SendEndpointUnregistrationCoreAsync(
        IWorkerTransport transport,
        WorkerMessageEndpointDto endpoint)
    {
        try
        {
            await transport.SendAsync(
                WorkerMessage.Create(
                    WorkerProtocolConstants.MessageUnregisterKind,
                    WorkerProtocolCodec.SerializePayload(endpoint),
                    endpoint.RunId),
                CancellationToken.None);
        }
        catch
        {
        }
    }

    private static ValueTask SendErrorAsync(IWorkerTransport transport, string code, string message, CancellationToken cancellationToken, Guid? runId = null)
        => transport.SendAsync(
            WorkerMessage.Create(
                WorkerProtocolConstants.ErrorKind,
                WorkerProtocolCodec.SerializePayload(new WorkerErrorDto(code, message)),
                runId),
            cancellationToken);

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

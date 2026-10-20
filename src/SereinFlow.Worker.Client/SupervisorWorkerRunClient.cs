using SereinFlow.Contracts;
using SereinFlow.Worker.Supervisor;

namespace SereinFlow.Worker.Client;
/// <summary>
/// Composition-root options for the isolated Worker client.  The API only
/// supplies paths and lifecycle policy; it does not construct WorkerSupervisor
/// or depend on its process-launching details.
/// Worker 客户端的组合根选项。API 只提供路径和生命周期策略，不创建
/// WorkerSupervisor，也不依赖其进程启动细节。
/// </summary>
public sealed record SupervisorWorkerRunClientOptions(string RunnerPath, string? WorkingDirectory = null, TimeSpan? HandshakeTimeout = null, TimeSpan? HeartbeatInterval = null, TimeSpan? CancellationGracePeriod = null, string? AllowedScriptArtifactRoot = null, string? AllowedLibraryPackageRoot = null, string RunnerFileName = "dotnet", Action<string>? DiagnosticLogger = null, TimeSpan? MessageDeliveryTimeout = null, string? AllowedWorkpieceRoot = null);
public sealed class SupervisorWorkerRunClient : IWorkerDebugRunClient, IWorkerMessageRunClient, IWorkerVariableRunClient
{
    private readonly WorkerSupervisor _supervisor;
    public SupervisorWorkerRunClient(WorkerSupervisor supervisor)
    {
        _supervisor = supervisor;
    }

    public SupervisorWorkerRunClient(SupervisorWorkerRunClientOptions options)
    {
        var supervisor = CreateSupervisor(options);
        _supervisor = supervisor;
    }

    private static WorkerSupervisor CreateSupervisor(SupervisorWorkerRunClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var getFileNameWithoutExtensionResult = Path.GetFileNameWithoutExtension(options.RunnerFileName);
        var isDotnetHost = string.Equals(getFileNameWithoutExtensionResult, "dotnet", StringComparison.OrdinalIgnoreCase);
        var arguments = isDotnetHost ? new[]
        {
            "exec",
            options.RunnerPath
        }

        : Array.Empty<string>();
        var runnerLaunchOptions = new RunnerLaunchOptions(options.RunnerFileName, arguments, options.WorkingDirectory, options.HandshakeTimeout, options.HeartbeatInterval, options.CancellationGracePeriod, options.AllowedScriptArtifactRoot, options.AllowedLibraryPackageRoot, options.DiagnosticLogger, options.MessageDeliveryTimeout, options.AllowedWorkpieceRoot);
        var workerSupervisor = new WorkerSupervisor(runnerLaunchOptions);
        return workerSupervisor;
    }

    public Task<WorkerRunResultDto> RunAsync(WorkerRunRequestDto request, IWorkerRunEventSink eventSink, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventSink);
        var runAsyncResult = _supervisor.RunAsync(request, (workerEvent, token) =>
        {
            var publishAsyncResult = eventSink.PublishAsync(workerEvent, token);
            return publishAsyncResult;
        }, cancellationToken);
        return runAsyncResult;
    }

    public async Task<IWorkerDebugRunHandle> StartDebugAsync(WorkerRunRequestDto request, IWorkerRunEventSink eventSink, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(eventSink);
        if (request.Debug is null)
        {
            throw new ArgumentException("A debug worker request must include debug options. 调试 Worker 请求必须包含调试选项。", nameof(request));
        }

        var session = await _supervisor.StartDebugAsync(request, (workerEvent, token) =>
        {
            var publishAsyncResult1 = eventSink.PublishAsync(workerEvent, token);
            return publishAsyncResult1;
        }, cancellationToken);
        var supervisorDebugRunHandle = new SupervisorDebugRunHandle(session);
        return supervisorDebugRunHandle;
    }

    public Task<WorkerMessageDeliveryResponseDto> DeliverMessageAsync(WorkerMessageDeliveryDto delivery, CancellationToken cancellationToken = default)
    {
        var deliverMessageAsyncResult = _supervisor.DeliverMessageAsync(delivery, cancellationToken);
        return deliverMessageAsyncResult;
    }

    public Task<WorkerVariableResponseDto> AccessVariablesAsync(WorkerVariableRequestDto request, CancellationToken cancellationToken = default)
    {
        var accessVariablesAsyncResult = _supervisor.AccessVariablesAsync(request, cancellationToken);
        return accessVariablesAsyncResult;
    }

    private sealed class SupervisorDebugRunHandle(WorkerSupervisor.DebugRunSession session) : IWorkerDebugRunHandle
    {
        public Guid RunId => session.RunId;
        public Guid DebugSessionId => session.DebugSessionId;
        public Task<WorkerRunResultDto> Completion => session.Completion;

        public Task ContinueAsync(long commandSequence, CancellationToken cancellationToken = default)
        {
            var continueAsyncResult = session.ContinueAsync(commandSequence, cancellationToken);
            return continueAsyncResult;
        }

        public Task StepAsync(long commandSequence, CancellationToken cancellationToken = default)
        {
            var stepAsyncResult = session.StepAsync(commandSequence, cancellationToken);
            return stepAsyncResult;
        }

        public Task StopAsync(long commandSequence, CancellationToken cancellationToken = default)
        {
            var stopAsyncResult = session.StopAsync(commandSequence, cancellationToken);
            return stopAsyncResult;
        }

        public ValueTask DisposeAsync()
        {
            var disposeAsyncResult = session.DisposeAsync();
            return disposeAsyncResult;
        }
    }
}

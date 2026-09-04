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
public sealed record SupervisorWorkerRunClientOptions(
    string RunnerPath,
    string? WorkingDirectory = null,
    TimeSpan? HandshakeTimeout = null,
    TimeSpan? HeartbeatInterval = null,
    TimeSpan? CancellationGracePeriod = null,
    string? AllowedScriptArtifactRoot = null,
    string? AllowedLibraryPackageRoot = null,
    string RunnerFileName = "dotnet",
    Action<string>? DiagnosticLogger = null,
    TimeSpan? MessageDeliveryTimeout = null,
    string? AllowedWorkpieceRoot = null);

public sealed class SupervisorWorkerRunClient : IWorkerDebugRunClient, IWorkerMessageRunClient
{
    private readonly WorkerSupervisor _supervisor;

    public SupervisorWorkerRunClient(WorkerSupervisor supervisor) => _supervisor = supervisor;

    public SupervisorWorkerRunClient(SupervisorWorkerRunClientOptions options)
        : this(CreateSupervisor(options))
    {
    }

    private static WorkerSupervisor CreateSupervisor(SupervisorWorkerRunClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var isDotnetHost = string.Equals(
            Path.GetFileNameWithoutExtension(options.RunnerFileName),
            "dotnet",
            StringComparison.OrdinalIgnoreCase);
        var arguments = isDotnetHost
            ? new[] { "exec", options.RunnerPath }
            : Array.Empty<string>();
        return new WorkerSupervisor(new RunnerLaunchOptions(
            options.RunnerFileName,
            arguments,
            options.WorkingDirectory,
            options.HandshakeTimeout,
            options.HeartbeatInterval,
            options.CancellationGracePeriod,
            options.AllowedScriptArtifactRoot,
            options.AllowedLibraryPackageRoot,
            options.DiagnosticLogger,
            options.MessageDeliveryTimeout,
            options.AllowedWorkpieceRoot));
    }

    public Task<WorkerRunResultDto> RunAsync(
        WorkerRunRequestDto request,
        IWorkerRunEventSink eventSink,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventSink);
        return _supervisor.RunAsync(
            request,
            (workerEvent, token) => eventSink.PublishAsync(workerEvent, token),
            cancellationToken);
    }

    public async Task<IWorkerDebugRunHandle> StartDebugAsync(
        WorkerRunRequestDto request,
        IWorkerRunEventSink eventSink,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(eventSink);
        if (request.Debug is null)
        {
            throw new ArgumentException(
                "A debug worker request must include debug options. 调试 Worker 请求必须包含调试选项。",
                nameof(request));
        }

        var session = await _supervisor.StartDebugAsync(
            request,
            (workerEvent, token) => eventSink.PublishAsync(workerEvent, token),
            cancellationToken);
        return new SupervisorDebugRunHandle(session);
    }

    public Task<WorkerMessageDeliveryResponseDto> DeliverMessageAsync(
        WorkerMessageDeliveryDto delivery,
        CancellationToken cancellationToken = default)
        => _supervisor.DeliverMessageAsync(delivery, cancellationToken);

    private sealed class SupervisorDebugRunHandle(WorkerSupervisor.DebugRunSession session) : IWorkerDebugRunHandle
    {
        public Guid RunId => session.RunId;

        public Guid DebugSessionId => session.DebugSessionId;

        public Task<WorkerRunResultDto> Completion => session.Completion;

        public Task ContinueAsync(long commandSequence, CancellationToken cancellationToken = default)
            => session.ContinueAsync(commandSequence, cancellationToken);

        public Task StepAsync(long commandSequence, CancellationToken cancellationToken = default)
            => session.StepAsync(commandSequence, cancellationToken);

        public Task StopAsync(long commandSequence, CancellationToken cancellationToken = default)
            => session.StopAsync(commandSequence, cancellationToken);

        public ValueTask DisposeAsync() => session.DisposeAsync();
    }
}

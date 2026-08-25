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
    Action<string>? DiagnosticLogger = null);

public sealed class SupervisorWorkerRunClient : IWorkerRunClient
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
            options.DiagnosticLogger));
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
}

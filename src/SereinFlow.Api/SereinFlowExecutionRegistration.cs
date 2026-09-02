using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Worker.Client;

namespace SereinFlow.Api;

public static class SereinFlowExecutionRegistration
{
    public static IServiceCollection AddSereinFlowExecution(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        services.Configure<RunExecutionOptions>(configuration.GetSection("SereinFlow:RunExecution"));
        services.AddScoped<RunSubmissionService>();
        services.AddScoped<RunInterruptionService>();

        var workerRunnerPath = ResolveWorkerRunnerPath(
            configuration["SereinFlow:WorkerRunnerPath"],
            contentRootPath);
        var workerRunnerFileName = IsManagedWorkerAssembly(workerRunnerPath) ? "dotnet" : workerRunnerPath;
        services.AddSingleton<SupervisorWorkerRunClient>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<SupervisorWorkerRunClient>>();
            var storage = serviceProvider.GetRequiredService<SereinFlow.Infrastructure.Configuration.SereinFlowStorageOptions>();
            return new SupervisorWorkerRunClient(
                new SupervisorWorkerRunClientOptions(
                    workerRunnerPath,
                    RunnerFileName: workerRunnerFileName,
                    WorkingDirectory: Path.GetDirectoryName(workerRunnerPath),
                    AllowedScriptArtifactRoot: storage.ScriptArtifactRoot,
                    AllowedLibraryPackageRoot: storage.LibraryDirectory,
                    DiagnosticLogger: message => WorkerLog.WorkerDiagnostic(logger, message, null)));
        });
        services.AddSingleton<IWorkerRunClient>(serviceProvider =>
            serviceProvider.GetRequiredService<SupervisorWorkerRunClient>());
        services.AddSingleton<IWorkerDebugRunClient>(serviceProvider =>
            serviceProvider.GetRequiredService<SupervisorWorkerRunClient>());
        services.AddSingleton<IWorkerMessageRunClient>(serviceProvider =>
            serviceProvider.GetRequiredService<SupervisorWorkerRunClient>());
        services.AddSingleton<RunExecutionQueue>();
        services.AddSingleton<RunEventBroadcaster>();
        services.AddSingleton<FlowDebugSessionService>();
        services.AddSingleton<IFlowDebugSessionService>(serviceProvider =>
            serviceProvider.GetRequiredService<FlowDebugSessionService>());
        services.AddHostedService<RunExecutionHostedService>();
        services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<FlowDebugSessionService>());
        services.AddHostedService<LibraryCatalogReindexHostedService>();
        return services;
    }

    private static string ResolveWorkerRunnerPath(string? configuredPath, string contentRootPath)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            candidates.Add(Path.IsPathRooted(configuredPath)
                ? Path.GetFullPath(configuredPath)
                : Path.GetFullPath(Path.Combine(contentRootPath, configuredPath)));
        }

        var contentRoot = new DirectoryInfo(contentRootPath);
        for (var ancestor = contentRoot; ancestor is not null; ancestor = ancestor.Parent)
        {
            foreach (var projectRoot in new[] { ancestor.FullName, Path.Combine(ancestor.FullName, "src") })
            {
                foreach (var configuration in new[] { "Debug", "Release" })
                {
                    var outputRoot = Path.Combine(projectRoot, "SereinFlow.Worker.Runner", "bin", configuration, "net10.0");
                    candidates.Add(Path.Combine(outputRoot, "SereinFlow.Worker.Runner.exe"));
                    candidates.Add(Path.Combine(outputRoot, "SereinFlow.Worker.Runner"));
                    candidates.Add(Path.Combine(outputRoot, "SereinFlow.Worker.Runner.dll"));
                }
            }
        }

        candidates.Add(Path.Combine(AppContext.BaseDirectory, "SereinFlow.Worker.Runner.exe"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "SereinFlow.Worker.Runner"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "SereinFlow.Worker.Runner.dll"));

        var existing = candidates.FirstOrDefault(File.Exists);
        if (existing is not null)
            return existing;

        var requested = candidates.FirstOrDefault() ?? Path.Combine(AppContext.BaseDirectory, "SereinFlow.Worker.Runner.dll");
        throw new InvalidOperationException(
            $"Worker Runner executable was not found. Worker Runner 可执行文件不存在。 Configure SereinFlow:WorkerRunnerPath. 请配置 SereinFlow:WorkerRunnerPath. Requested path: '{requested}'.");
    }

    private static bool IsManagedWorkerAssembly(string path)
        => string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase);
}

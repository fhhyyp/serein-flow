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
        IConfiguration? configuration = null,
        string? contentRootPath = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<RunExecutionOptions>().Configure<IConfiguration>((options, resolvedConfiguration) =>
            resolvedConfiguration.GetSection("SereinFlow:RunExecution").Bind(options));
        services.AddScoped<RunSubmissionService>();
        services.AddScoped<RunInterruptionService>();
        services.AddScoped<IRunMessageDeliveryService, RunMessageDeliveryService>();
        services.AddScoped<IRunVariableService, RunVariableService>();

        services.AddSingleton<SupervisorWorkerRunClient>(serviceProvider =>
        {
            var resolvedConfiguration = configuration ?? serviceProvider.GetRequiredService<IConfiguration>();
            var resolvedContentRoot = contentRootPath
                ?? serviceProvider.GetRequiredService<IHostEnvironment>().ContentRootPath;
            var workerRunnerPath = ResolveWorkerRunnerPath(
                resolvedConfiguration["SereinFlow:WorkerRunnerPath"],
                resolvedContentRoot);
            var workerRunnerFileName = IsManagedWorkerAssembly(workerRunnerPath) ? "dotnet" : workerRunnerPath;
            var logger = serviceProvider.GetRequiredService<ILogger<SupervisorWorkerRunClient>>();
            var storage = serviceProvider.GetRequiredService<SereinFlow.Infrastructure.Configuration.SereinFlowStorageOptions>();
            return new SupervisorWorkerRunClient(
                new SupervisorWorkerRunClientOptions(
                    workerRunnerPath,
                    RunnerFileName: workerRunnerFileName,
                    WorkingDirectory: Path.GetDirectoryName(workerRunnerPath),
                    AllowedScriptArtifactRoot: storage.ScriptArtifactRoot,
                    AllowedLibraryPackageRoot: storage.LibraryDirectory,
                    AllowedWorkpieceRoot: storage.WorkpieceDirectory,
                    DiagnosticLogger: message => WorkerLog.WorkerDiagnostic(logger, message, null)));
        });
        services.AddSingleton<IWorkerRunClient>(serviceProvider =>
            serviceProvider.GetRequiredService<SupervisorWorkerRunClient>());
        services.AddSingleton<IWorkerDebugRunClient>(serviceProvider =>
            serviceProvider.GetRequiredService<SupervisorWorkerRunClient>());
        services.AddSingleton<IWorkerMessageRunClient>(serviceProvider =>
            serviceProvider.GetRequiredService<SupervisorWorkerRunClient>());
        services.AddSingleton<IWorkerVariableRunClient>(serviceProvider =>
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
            var configured = Path.IsPathRooted(configuredPath)
                ? Path.GetFullPath(configuredPath)
                : Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
            if (!File.Exists(configured))
                throw new InvalidOperationException($"Configured SereinFlow:WorkerRunnerPath does not exist: '{configured}'.");
            ValidateManagedRunnerFiles(configured);
            return configured;
        }

        foreach (var fileName in new[] { "SereinFlow.Worker.Runner.exe", "SereinFlow.Worker.Runner", "SereinFlow.Worker.Runner.dll" })
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "sereinflow", "worker", fileName));

        // Only the repository's official host may use source-tree build output.
        // A package consumer must resolve the bundled Runner or configure an explicit path.
        for (var ancestor = new DirectoryInfo(contentRootPath); ancestor is not null; ancestor = ancestor.Parent)
        {
            if (!string.Equals(ancestor.Name, "SereinFlow.Api", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(ancestor.Parent?.Name, "src", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var configuration in new[] { "Debug", "Release" })
                candidates.Add(Path.Combine(ancestor.Parent!.FullName, "SereinFlow.Worker.Runner", "bin",
                    configuration, "net10.0", "SereinFlow.Worker.Runner.dll"));
            break;
        }

        var existing = candidates.FirstOrDefault(File.Exists);
        if (existing is not null)
        {
            ValidateManagedRunnerFiles(existing);
            return existing;
        }

        var requested = candidates.FirstOrDefault() ?? Path.Combine(AppContext.BaseDirectory, "SereinFlow.Worker.Runner.dll");
        throw new InvalidOperationException(
            $"Worker Runner executable was not found. Worker Runner 可执行文件不存在。 Configure SereinFlow:WorkerRunnerPath. 请配置 SereinFlow:WorkerRunnerPath. Requested path: '{requested}'.");
    }

    private static bool IsManagedWorkerAssembly(string path)
        => string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase);

    private static void ValidateManagedRunnerFiles(string path)
    {
        if (!IsManagedWorkerAssembly(path))
            return;
        var directory = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        foreach (var suffix in new[] { ".deps.json", ".runtimeconfig.json" })
        {
            var required = Path.Combine(directory, name + suffix);
            if (!File.Exists(required))
                throw new InvalidOperationException($"Worker Runner deployment is incomplete. Missing '{required}'.");
        }
    }
}

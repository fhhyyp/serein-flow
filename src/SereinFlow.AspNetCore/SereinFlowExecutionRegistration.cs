using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Worker.Client;

namespace SereinFlow.Api;
public static class SereinFlowExecutionRegistration
{
    public static IServiceCollection AddSereinFlowExecution(this IServiceCollection services, IConfiguration? configuration = null, string? contentRootPath = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var addOptionsResult = services.AddOptions<RunExecutionOptions>();
        addOptionsResult.Configure<IConfiguration>((options, resolvedConfiguration) =>
        {
            var getSectionResult = resolvedConfiguration.GetSection("SereinFlow:RunExecution");
            getSectionResult.Bind(options);
        });
        services.AddScoped<RunSubmissionService>();
        services.AddScoped<RunInterruptionService>();
        services.AddScoped<IRunMessageDeliveryService, RunMessageDeliveryService>();
        services.AddScoped<IRunVariableService, RunVariableService>();
        services.AddSingleton<SupervisorWorkerRunClient>(serviceProvider =>
        {
            var resolvedConfiguration = configuration ?? serviceProvider.GetRequiredService<IConfiguration>();
            var resolvedContentRoot = contentRootPath ?? serviceProvider.GetRequiredService<IHostEnvironment>().ContentRootPath;
            var workerRunnerPath = ResolveWorkerRunnerPath(resolvedConfiguration["SereinFlow:WorkerRunnerPath"], resolvedContentRoot);
            var workerRunnerFileName = IsManagedWorkerAssembly(workerRunnerPath) ? "dotnet" : workerRunnerPath;
            var logger = serviceProvider.GetRequiredService<ILogger<SupervisorWorkerRunClient>>();
            var storage = serviceProvider.GetRequiredService<SereinFlow.Infrastructure.Configuration.SereinFlowStorageOptions>();
            var getDirectoryNameResult = Path.GetDirectoryName(workerRunnerPath);
            var supervisorWorkerRunClientOptions = new SupervisorWorkerRunClientOptions(workerRunnerPath, RunnerFileName: workerRunnerFileName, WorkingDirectory: getDirectoryNameResult, AllowedScriptArtifactRoot: storage.ScriptArtifactRoot, AllowedLibraryPackageRoot: storage.LibraryDirectory, AllowedWorkpieceRoot: storage.WorkpieceDirectory, DiagnosticLogger: message =>
            {
                WorkerLog.WorkerDiagnostic(logger, message, null);
            });
            var supervisorWorkerRunClient = new SupervisorWorkerRunClient(supervisorWorkerRunClientOptions);
            return supervisorWorkerRunClient;
        });
        services.AddSingleton<IWorkerRunClient>(serviceProvider =>
        {
            var getRequiredServiceResult = serviceProvider.GetRequiredService<SupervisorWorkerRunClient>();
            return getRequiredServiceResult;
        });
        services.AddSingleton<IWorkerDebugRunClient>(serviceProvider =>
        {
            var getRequiredServiceResult1 = serviceProvider.GetRequiredService<SupervisorWorkerRunClient>();
            return getRequiredServiceResult1;
        });
        services.AddSingleton<IWorkerMessageRunClient>(serviceProvider =>
        {
            var getRequiredServiceResult2 = serviceProvider.GetRequiredService<SupervisorWorkerRunClient>();
            return getRequiredServiceResult2;
        });
        services.AddSingleton<IWorkerVariableRunClient>(serviceProvider =>
        {
            var getRequiredServiceResult3 = serviceProvider.GetRequiredService<SupervisorWorkerRunClient>();
            return getRequiredServiceResult3;
        });
        services.AddSingleton<RunExecutionQueue>();
        services.AddSingleton<RunEventBroadcaster>();
        services.AddSingleton<FlowDebugSessionService>();
        services.AddSingleton<IFlowDebugSessionService>(serviceProvider =>
        {
            var getRequiredServiceResult4 = serviceProvider.GetRequiredService<FlowDebugSessionService>();
            return getRequiredServiceResult4;
        });
        services.AddHostedService<RunExecutionHostedService>();
        services.AddHostedService(serviceProvider =>
        {
            var getRequiredServiceResult5 = serviceProvider.GetRequiredService<FlowDebugSessionService>();
            return getRequiredServiceResult5;
        });
        services.AddHostedService<LibraryCatalogReindexHostedService>();
        return services;
    }

    private static string ResolveWorkerRunnerPath(string? configuredPath, string contentRootPath)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var configured = Path.IsPathRooted(configuredPath) ? Path.GetFullPath(configuredPath) : resolveGetFullPathResult();
            if (!File.Exists(configured))
            {
                throw new InvalidOperationException($"Configured SereinFlow:WorkerRunnerPath does not exist: '{configured}'.");
            }

            ValidateManagedRunnerFiles(configured);
            return configured;
            string resolveGetFullPathResult()
            {
                var combineResult = Path.Combine(contentRootPath, configuredPath);
                var getFullPathResult = Path.GetFullPath(combineResult);
                return getFullPathResult;
            }
        }

        foreach (var fileName in new[]
        {
            "SereinFlow.Worker.Runner.exe",
            "SereinFlow.Worker.Runner",
            "SereinFlow.Worker.Runner.dll"
        }

        )
        {
            var combineResult6 = Path.Combine(AppContext.BaseDirectory, "sereinflow", "worker", fileName);
            candidates.Add(combineResult6);
        }

        // Only the repository's official host may use source-tree build output.
        // A package consumer must resolve the bundled Runner or configure an explicit path.
        for (var ancestor = new DirectoryInfo(contentRootPath); ancestor is not null; ancestor = ancestor.Parent)
        {
            if (!string.Equals(ancestor.Name, "SereinFlow.Api", StringComparison.OrdinalIgnoreCase) || !string.Equals(ancestor.Parent?.Name, "src", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var configuration in new[]
            {
                "Debug",
                "Release"
            }

            )
            {
                var combineResult7 = Path.Combine(ancestor.Parent!.FullName, "SereinFlow.Worker.Runner", "bin", configuration, "net10.0", "SereinFlow.Worker.Runner.dll");
                candidates.Add(combineResult7);
            }

            break;
        }

        var existing = candidates.FirstOrDefault(File.Exists);
        if (existing is not null)
        {
            ValidateManagedRunnerFiles(existing);
            return existing;
        }

        var requested = candidates.FirstOrDefault() ?? Path.Combine(AppContext.BaseDirectory, "SereinFlow.Worker.Runner.dll");
        throw new InvalidOperationException($"Worker Runner executable was not found. Worker Runner 可执行文件不存在。 Configure SereinFlow:WorkerRunnerPath. 请配置 SereinFlow:WorkerRunnerPath. Requested path: '{requested}'.");
    }

    private static bool IsManagedWorkerAssembly(string path)
    {
        var getExtensionResult = Path.GetExtension(path);
        var equalsResult = string.Equals(getExtensionResult, ".dll", StringComparison.OrdinalIgnoreCase);
        return equalsResult;
    }

    private static void ValidateManagedRunnerFiles(string path)
    {
        if (!IsManagedWorkerAssembly(path))
        {
            return;
        }

        var directory = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        foreach (var suffix in new[]
        {
            ".deps.json",
            ".runtimeconfig.json"
        }

        )
        {
            var required = Path.Combine(directory, name + suffix);
            if (!File.Exists(required))
            {
                throw new InvalidOperationException($"Worker Runner deployment is incomplete. Missing '{required}'.");
            }
        }
    }
}

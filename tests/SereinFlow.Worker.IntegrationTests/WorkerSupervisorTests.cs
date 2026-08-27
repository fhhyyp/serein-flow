using System.Text.Json;
using System.IO.Compression;
using SereinFlow.Contracts;
using SereinFlow.TestLibrary;
using SereinFlow.Worker.Client;
using SereinFlow.Worker.Runner;
using SereinFlow.Worker.Supervisor;

namespace SereinFlow.Worker.IntegrationTests;

public sealed class WorkerSupervisorTests
{
    private const string TestLibraryArtifactId = "test-library-artifact";

    [Fact]
    public async Task SupervisorRunsDisposableRunnerAndForwardsOrderedEvents()
    {
        var events = new List<WorkerEventEnvelopeDto>();
        var supervisor = CreateSupervisor();
        var request = CreateActionRequest(DateTimeOffset.UtcNow.AddSeconds(15));

        var result = await supervisor.RunAsync(request, (workerEvent, _) =>
        {
            events.Add(workerEvent);
            return ValueTask.CompletedTask;
        });

        Assert.Equal(FlowRunStatusDto.Succeeded, result.Status);
        Assert.Null(result.ErrorCode);
        Assert.NotEmpty(events);
        Assert.All(events, workerEvent => Assert.Equal(request.RunId, workerEvent.RunId));
        Assert.Equal(events.OrderBy(workerEvent => workerEvent.Sequence).Select(workerEvent => workerEvent.Sequence), events.Select(workerEvent => workerEvent.Sequence));
        Assert.Contains(events, workerEvent => workerEvent.EventType == WorkerEventType.NodeStarted);
        Assert.Contains(events, workerEvent => workerEvent.EventType == WorkerEventType.NodeCompleted);
    }

    [Fact]
    public async Task SupervisorWorkerRunClientUsesDotnetExecForManagedRunnerAssembly()
    {
        var events = new List<WorkerEventEnvelopeDto>();
        var client = new SupervisorWorkerRunClient(new SupervisorWorkerRunClientOptions(
            typeof(RunnerHost).Assembly.Location,
            RunnerFileName: "dotnet",
            HandshakeTimeout: TimeSpan.FromSeconds(10),
            HeartbeatInterval: TimeSpan.FromMilliseconds(100),
            CancellationGracePeriod: TimeSpan.FromSeconds(2)));

        var result = await client.RunAsync(
            CreateActionRequest(DateTimeOffset.UtcNow.AddSeconds(15)),
            new DelegateWorkerRunEventSink((workerEvent, _) =>
            {
                events.Add(workerEvent);
                return ValueTask.CompletedTask;
            }));

        Assert.Equal(FlowRunStatusDto.Succeeded, result.Status);
        Assert.NotEmpty(events);
    }

    [Fact]
    public async Task SupervisorPropagatesCancellationToScriptRunner()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var diagnostics = new List<string>();
        var supervisor = CreateSupervisor(diagnostics.Add);
        var request = CreateScriptRequest(DateTimeOffset.UtcNow.AddSeconds(15));
        using var cancellation = new CancellationTokenSource();

        var runTask = supervisor.RunAsync(request, (workerEvent, _) =>
        {
            if (workerEvent.EventType == WorkerEventType.NodeStarted)
                started.TrySetResult();
            return ValueTask.CompletedTask;
        }, cancellation.Token);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(
            result.Status == FlowRunStatusDto.Cancelled,
            $"Expected a cancelled worker result but received {result.Status}; code={result.ErrorCode}; message={result.ErrorMessage}; diagnostics={string.Join(" | ", diagnostics)}");
        Assert.Equal("worker.cancelled", result.ErrorCode);
    }

    [Fact]
    public async Task SupervisorRejectsExpiredDeadlinesWithoutStartingRunner()
    {
        var supervisor = CreateSupervisor();
        var request = CreateActionRequest(DateTimeOffset.UtcNow.AddMilliseconds(-1));

        var result = await supervisor.RunAsync(request, static (_, _) => ValueTask.CompletedTask);

        Assert.Equal(FlowRunStatusDto.TimedOut, result.Status);
        Assert.Equal("worker.timed_out", result.ErrorCode);
    }

    [Fact]
    public async Task SupervisorClassifiesUnexpectedRunnerExitAsCrash()
    {
        var missingRunner = Path.Combine(Path.GetTempPath(), $"missing-runner-{Guid.NewGuid():N}.dll");
        var supervisor = new WorkerSupervisor(new RunnerLaunchOptions("dotnet", ["exec", missingRunner]));
        var request = CreateActionRequest(DateTimeOffset.UtcNow.AddSeconds(10));

        var result = await supervisor.RunAsync(request, static (_, _) => ValueTask.CompletedTask);

        Assert.Equal(FlowRunStatusDto.Failed, result.Status);
        Assert.Equal("worker.runner_not_found", result.ErrorCode);
    }

    [Fact]
    public async Task SupervisorRejectsExternalLibraryOutsideRunAllowList()
    {
        var packageRoot = CreateTestLibraryPackageRoot();
        try
        {
            var events = new List<WorkerEventEnvelopeDto>();
            var supervisor = CreateSupervisor(allowedLibraryPackageRoot: packageRoot);
            var request = CreateLibraryActionRequest(
                DateTimeOffset.UtcNow.AddSeconds(15),
                packageRoot,
                []);

            var result = await supervisor.RunAsync(request, (workerEvent, _) =>
            {
                events.Add(workerEvent);
                return ValueTask.CompletedTask;
            });

            Assert.Equal(FlowRunStatusDto.Failed, result.Status);
            Assert.Equal("library.not_allowed", result.ErrorCode);
            var nodeError = Assert.Single(events, workerEvent => workerEvent.EventType == WorkerEventType.NodeErrored);
            Assert.Contains("\"errorCode\":\"library.not_allowed\"", nodeError.PayloadJson, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public async Task SupervisorExecutesExternalLibraryInsideRunAllowList()
    {
        var packageRoot = CreateTestLibraryPackageRoot();
        try
        {
            var events = new List<WorkerEventEnvelopeDto>();
            var supervisor = CreateSupervisor(allowedLibraryPackageRoot: packageRoot);
            var request = CreateLibraryActionRequest(
                DateTimeOffset.UtcNow.AddSeconds(15),
                packageRoot,
                [TestLibraryArtifactId]);

            var result = await supervisor.RunAsync(request, (workerEvent, _) =>
            {
                events.Add(workerEvent);
                return ValueTask.CompletedTask;
            });

            Assert.Equal(FlowRunStatusDto.Succeeded, result.Status);
            var completed = Assert.Single(events, workerEvent => workerEvent.EventType == WorkerEventType.NodeCompleted);
            using var payload = JsonDocument.Parse(completed.PayloadJson);
            Assert.Equal(50m, payload.RootElement.GetProperty("outputs").GetProperty("result").GetDecimal());
        }
        finally
        {
            TryDeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public async Task SupervisorInjectsFlowContextAndForwardsTheFailureBranch()
    {
        var packageRoot = CreateTestLibraryPackageRoot();
        try
        {
            var events = new List<WorkerEventEnvelopeDto>();
            var supervisor = CreateSupervisor(allowedLibraryPackageRoot: packageRoot);
            var request = CreateLibraryActionRequest(
                DateTimeOffset.UtcNow.AddSeconds(15),
                packageRoot,
                [TestLibraryArtifactId],
                methodName: "按设备状态选择分支",
                parameters: [new NodeParameterDto("设备状态", "\"告警\"", DataSourceDto.Literal, true)],
                returnType: "System.String");

            var result = await supervisor.RunAsync(request, (workerEvent, _) =>
            {
                events.Add(workerEvent);
                return ValueTask.CompletedTask;
            });

            Assert.Equal(FlowRunStatusDto.Failed, result.Status);
            Assert.Equal("device.not_ready", result.ErrorCode);
            var failed = Assert.Single(events, workerEvent => workerEvent.EventType == WorkerEventType.NodeFailed);
            using var payload = JsonDocument.Parse(failed.PayloadJson);
            Assert.Equal("Failure", payload.RootElement.GetProperty("branch").GetString());
            Assert.Equal("device.not_ready", payload.RootElement.GetProperty("errorCode").GetString());
            Assert.DoesNotContain("流程上下文", payload.RootElement.GetProperty("inputs").EnumerateObject().Select(item => item.Name));
        }
        finally
        {
            TryDeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public async Task SupervisorInjectsFlowContextAndForwardsTheErrorBranch()
    {
        var packageRoot = CreateTestLibraryPackageRoot();
        try
        {
            var events = new List<WorkerEventEnvelopeDto>();
            var supervisor = CreateSupervisor(allowedLibraryPackageRoot: packageRoot);
            var request = CreateLibraryActionRequest(
                DateTimeOffset.UtcNow.AddSeconds(15),
                packageRoot,
                [TestLibraryArtifactId],
                methodName: "按设备状态选择分支",
                parameters: [new NodeParameterDto("设备状态", "\"故障\"", DataSourceDto.Literal, true)],
                returnType: "System.String");

            var result = await supervisor.RunAsync(request, (workerEvent, _) =>
            {
                events.Add(workerEvent);
                return ValueTask.CompletedTask;
            });

            Assert.Equal(FlowRunStatusDto.Failed, result.Status);
            Assert.Equal("device.faulted", result.ErrorCode);
            var errored = Assert.Single(events, workerEvent => workerEvent.EventType == WorkerEventType.NodeErrored);
            using var payload = JsonDocument.Parse(errored.PayloadJson);
            Assert.Equal("Error", payload.RootElement.GetProperty("branch").GetString());
            Assert.Equal("device.faulted", payload.RootElement.GetProperty("errorCode").GetString());
        }
        finally
        {
            TryDeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public async Task SupervisorExpandsVariadicLibraryInputsAndSkipsEmptyPlaceholders()
    {
        var packageRoot = CreateTestLibraryPackageRoot();
        try
        {
            var events = new List<WorkerEventEnvelopeDto>();
            var supervisor = CreateSupervisor(allowedLibraryPackageRoot: packageRoot);
            var request = CreateLibraryActionRequest(
                DateTimeOffset.UtcNow.AddSeconds(15),
                packageRoot,
                [TestLibraryArtifactId],
                methodName: "汇总多个检测值",
                parameters:
                [
                    CreateVariadicParameter("param-1", "检测值", "2", "expanded"),
                    CreateVariadicParameter("param-1-2", "检测值 2", "3", "expanded"),
                    CreateVariadicParameter("param-1-3", "检测值 3", null, "expanded")
                ],
                returnType: "System.Int32");

            var result = await supervisor.RunAsync(request, (workerEvent, _) =>
            {
                events.Add(workerEvent);
                return ValueTask.CompletedTask;
            });

            Assert.Equal(FlowRunStatusDto.Succeeded, result.Status);
            var completed = Assert.Single(events, workerEvent => workerEvent.EventType == WorkerEventType.NodeCompleted);
            using var payload = JsonDocument.Parse(completed.PayloadJson);
            Assert.Equal(5, payload.RootElement.GetProperty("outputs").GetProperty("result").GetInt32());
            Assert.Equal(2, payload.RootElement.GetProperty("inputs").EnumerateObject().Count());
        }
        finally
        {
            TryDeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public async Task SupervisorConvertsCollectionVariadicLibraryInput()
    {
        var packageRoot = CreateTestLibraryPackageRoot();
        try
        {
            var events = new List<WorkerEventEnvelopeDto>();
            var supervisor = CreateSupervisor(allowedLibraryPackageRoot: packageRoot);
            var request = CreateLibraryActionRequest(
                DateTimeOffset.UtcNow.AddSeconds(15),
                packageRoot,
                [TestLibraryArtifactId],
                methodName: "汇总多个检测值",
                parameters: [CreateVariadicParameter("param-1", "检测值", "[2,3,5]", "collection")],
                returnType: "System.Int32");

            var result = await supervisor.RunAsync(request, (workerEvent, _) =>
            {
                events.Add(workerEvent);
                return ValueTask.CompletedTask;
            });

            Assert.Equal(FlowRunStatusDto.Succeeded, result.Status);
            var completed = Assert.Single(events, workerEvent => workerEvent.EventType == WorkerEventType.NodeCompleted);
            using var payload = JsonDocument.Parse(completed.PayloadJson);
            Assert.Equal(10, payload.RootElement.GetProperty("outputs").GetProperty("result").GetInt32());
        }
        finally
        {
            TryDeleteDirectory(packageRoot);
        }
    }

    private static WorkerSupervisor CreateSupervisor(
        Action<string>? diagnosticLogger = null,
        string? allowedLibraryPackageRoot = null)
        => new(new RunnerLaunchOptions(
            "dotnet",
            [typeof(RunnerHost).Assembly.Location],
            HandshakeTimeout: TimeSpan.FromSeconds(10),
            HeartbeatInterval: TimeSpan.FromMilliseconds(100),
            CancellationGracePeriod: TimeSpan.FromSeconds(2),
            AllowedLibraryPackageRoot: allowedLibraryPackageRoot,
            DiagnosticLogger: diagnosticLogger));

    private static WorkerRunRequestDto CreateActionRequest(DateTimeOffset deadline)
    {
        var flowId = Guid.NewGuid();
        var action = new NodeDto("action", NodeTypeDto.Action, "Action", 0, 0, [], [], null);
        return CreateRequest(flowId, action, deadline);
    }

    private static WorkerRunRequestDto CreateScriptRequest(DateTimeOffset deadline)
    {
        var flowId = Guid.NewGuid();
        var script = new ScriptNodeDataDto(
            "script",
            "for i in range(0, 1000000000) { var keep = i }",
            "1",
            SereinFlow.Domain.ScriptNodeDefinition.ComputeSourceHash("for i in range(0, 1000000000) { var keep = i }"),
            [],
            []);
        var node = new NodeDto("script", NodeTypeDto.Script, "Script", 0, 0, [], [], script);
        return CreateRequest(flowId, node, deadline);
    }

    private static WorkerRunRequestDto CreateLibraryActionRequest(
        DateTimeOffset deadline,
        string packageRoot,
        IReadOnlyList<string> allowedLibraryIds,
        string methodName = "计算合格率",
        IReadOnlyList<NodeParameterDto>? parameters = null,
        string returnType = "System.Decimal")
    {
        var flowId = Guid.NewGuid();
        var action = new NodeDto(
            "library-action",
            NodeTypeDto.Action,
            "Calculate quality rate",
            0,
            0,
            [],
            parameters ??
            [
                new NodeParameterDto("合格数量", "8", DataSourceDto.Literal, true),
                new NodeParameterDto("检测总数", "16", DataSourceDto.Literal, true)
            ],
            null,
            new NodeUiMetadataDto(
                "library-action",
                "node.libraryAction.title",
                "node.libraryAction.subtitle",
                null,
                "ready",
                true,
                null,
                Category: "library",
                LibraryId: TestLibraryArtifactId,
                ClassName: typeof(生产线节点).FullName,
                MethodName: methodName,
                DllName: Path.GetFileName(typeof(生产线节点).Assembly.Location),
                DllVersion: "1.0.0",
                ReturnType: returnType));
        var definition = new FlowDefinitionDto(
            flowId,
            5,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [action], [])],
            action.Id,
            "test",
            RunPolicy: new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel));
        return new WorkerRunRequestDto(
            WorkerProtocol.Version,
            Guid.NewGuid(),
            flowId,
            1,
            JsonSerializer.Serialize(definition),
            deadline,
            LibraryPackageRootPath: packageRoot,
            AllowedLibraryIds: allowedLibraryIds);
    }

    private static NodeParameterDto CreateVariadicParameter(
        string id,
        string name,
        string? valueJson,
        string mode)
        => new(
            name,
            valueJson,
            DataSourceDto.Literal,
            false,
            new NodeParameterUiMetadataDto(
                id,
                name,
                "System.Int32",
                valueJson,
                null,
                null,
                null,
                null,
                Type: "System.Int32",
                IsVariadic: true,
                VariadicGroupId: "param-1",
                ElementType: "System.Int32",
                VariadicMode: mode));

    private static WorkerRunRequestDto CreateRequest(Guid flowId, NodeDto node, DateTimeOffset deadline)
    {
        var definition = new FlowDefinitionDto(
            flowId,
            5,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [])],
            node.Id,
            "test",
            RunPolicy: new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel));
        return new WorkerRunRequestDto(
            WorkerProtocol.Version,
            Guid.NewGuid(),
            flowId,
            1,
            JsonSerializer.Serialize(definition),
            deadline);
    }

    private static string CreateTestLibraryPackageRoot()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), $"sereinflow-worker-library-{Guid.NewGuid():N}");
        var packageDirectory = Path.Combine(packageRoot, "packages");
        Directory.CreateDirectory(packageDirectory);
        var packagePath = Path.Combine(packageDirectory, $"{TestLibraryArtifactId}.zip");
        var assemblyPath = typeof(生产线节点).Assembly.Location;
        var assemblyDirectory = Path.GetDirectoryName(assemblyPath)!;

        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
        foreach (var dependencyPath in Directory.EnumerateFiles(assemblyDirectory, "SereinFlow*.dll"))
        {
            archive.CreateEntryFromFile(
                dependencyPath,
                $"library/{Path.GetFileName(dependencyPath)}",
                CompressionLevel.NoCompression);
        }

        var dependenciesFile = Path.ChangeExtension(assemblyPath, ".deps.json");
        if (File.Exists(dependenciesFile))
        {
            archive.CreateEntryFromFile(
                dependenciesFile,
                $"library/{Path.GetFileName(dependenciesFile)}",
                CompressionLevel.NoCompression);
        }

        return packageRoot;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

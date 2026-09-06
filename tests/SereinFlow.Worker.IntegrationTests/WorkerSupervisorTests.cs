using System.Text.Json;
using System.IO.Compression;
using SereinFlow.Contracts;
using SereinFlow.TestLibrary;
using SereinFlow.Library;
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
    public async Task SupervisorDebugSessionStepsAtNodeBoundariesAndCompletesAfterContinue()
    {
        var events = new List<WorkerEventEnvelopeDto>();
        var firstPause = new TaskCompletionSource<WorkerEventEnvelopeDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondPause = new TaskCompletionSource<WorkerEventEnvelopeDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var supervisor = CreateSupervisor();
        var request = CreateTwoNodeDebugRequest(DateTimeOffset.UtcNow.AddSeconds(15));

        await using var debug = await supervisor.StartDebugAsync(request, (workerEvent, _) =>
        {
            events.Add(workerEvent);
            if (workerEvent.EventType == WorkerEventType.DebugPaused)
            {
                using var payload = JsonDocument.Parse(workerEvent.PayloadJson);
                var nodeId = payload.RootElement.GetProperty("nodeId").GetString();
                if (nodeId == "first")
                    firstPause.TrySetResult(workerEvent);
                else if (nodeId == "second")
                    secondPause.TrySetResult(workerEvent);
            }
            return ValueTask.CompletedTask;
        });

        var first = await firstPause.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using (var payload = JsonDocument.Parse(first.PayloadJson))
        {
            Assert.Equal("first", payload.RootElement.GetProperty("nodeId").GetString());
            Assert.Equal(1, payload.RootElement.GetProperty("step").GetInt32());
            Assert.Equal(0, payload.RootElement.GetProperty("frameDepth").GetInt32());
        }
        Assert.DoesNotContain(events, item => item.EventType == WorkerEventType.NodeCompleted && item.NodeId == "first");

        await debug.StepAsync(1);
        var second = await secondPause.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using (var payload = JsonDocument.Parse(second.PayloadJson))
            Assert.Equal("second", payload.RootElement.GetProperty("nodeId").GetString());
        Assert.Single(events, item => item.EventType == WorkerEventType.NodeCompleted && item.NodeId == "first");
        Assert.DoesNotContain(events, item => item.EventType == WorkerEventType.NodeCompleted && item.NodeId == "second");

        await debug.ContinueAsync(2);
        var result = await debug.Completion.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(FlowRunStatusDto.Succeeded, result.Status);
        Assert.Single(events, item => item.EventType == WorkerEventType.NodeCompleted && item.NodeId == "second");
    }

    [Fact]
    public async Task SupervisorStopsGlobalFlipflopDebugSessionWhileListenerWaitsForATrigger()
    {
        var packageRoot = CreateTestLibraryPackageRoot();
        try
        {
            var listenerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var supervisor = CreateSupervisor(allowedLibraryPackageRoot: packageRoot);
            var request = CreateLibraryGlobalFlipflopDebugRequest(
                DateTimeOffset.UtcNow.AddSeconds(15),
                packageRoot,
                breakpointNodeIds: [],
                intervalMilliseconds: 3_600_000);

            await using var debug = await supervisor.StartDebugAsync(request, (workerEvent, _) =>
            {
                if (workerEvent.EventType == WorkerEventType.NodeStarted && workerEvent.NodeId == "library-flipflop")
                    listenerStarted.TrySetResult();
                return ValueTask.CompletedTask;
            });

            await listenerStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(100);
            await debug.StopAsync(1);
            var result = await debug.Completion.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(FlowRunStatusDto.Cancelled, result.Status);
            Assert.Equal(WorkerErrorCodes.Cancelled, result.ErrorCode);
        }
        finally
        {
            TryDeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public async Task SupervisorDebugSessionPausesAtGlobalFlipflopAfterItsTriggerArrives()
    {
        var packageRoot = CreateTestLibraryPackageRoot();
        try
        {
            var paused = new TaskCompletionSource<WorkerEventEnvelopeDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            var supervisor = CreateSupervisor(allowedLibraryPackageRoot: packageRoot);
            var request = CreateLibraryGlobalFlipflopDebugRequest(
                DateTimeOffset.UtcNow.AddSeconds(15),
                packageRoot,
                breakpointNodeIds: ["library-flipflop"],
                intervalMilliseconds: 50);

            await using var debug = await supervisor.StartDebugAsync(request, (workerEvent, _) =>
            {
                if (workerEvent.EventType == WorkerEventType.DebugPaused)
                    paused.TrySetResult(workerEvent);
                return ValueTask.CompletedTask;
            });

            var pause = await paused.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using (var payload = JsonDocument.Parse(pause.PayloadJson))
            {
                Assert.Equal("library-flipflop", payload.RootElement.GetProperty("nodeId").GetString());
                Assert.NotEqual(Guid.Empty, payload.RootElement.GetProperty("triggerInvocationId").GetGuid());
            }

            await debug.StopAsync(1);
            var result = await debug.Completion.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(FlowRunStatusDto.Cancelled, result.Status);
        }
        finally
        {
            TryDeleteDirectory(packageRoot);
        }
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
        Assert.Equal(WorkerErrorCodes.Cancelled, result.ErrorCode);
    }

    [Fact]
    public async Task SupervisorForwardsSereinFlowScriptLogsAsProtocolEvents()
    {
        var events = new List<WorkerEventEnvelopeDto>();
        var supervisor = CreateSupervisor();
        var request = CreateScriptLogRequest(DateTimeOffset.UtcNow.AddSeconds(15));

        var result = await supervisor.RunAsync(request, (workerEvent, _) =>
        {
            events.Add(workerEvent);
            return ValueTask.CompletedTask;
        });

        Assert.Equal(FlowRunStatusDto.Succeeded, result.Status);
        var log = Assert.Single(events, item => item.EventType == WorkerEventType.Log);
        using var payload = JsonDocument.Parse(log.PayloadJson);
        Assert.Equal("info", payload.RootElement.GetProperty("level").GetString());
        Assert.Equal("Started by script", payload.RootElement.GetProperty("message").GetString());
        Assert.Equal(0, payload.RootElement.GetProperty("frameDepth").GetInt32());
    }

    [Fact]
    public async Task SupervisorDerivesScriptHashFromSourceWhenSerializedSnapshotHashIsStale()
    {
        var supervisor = CreateSupervisor();
        var request = CreateScriptLogRequest(DateTimeOffset.UtcNow.AddSeconds(15));
        var definition = JsonSerializer.Deserialize<FlowDefinitionDto>(request.DefinitionJson)!;
        var canvas = definition.Canvases.Single();
        var node = canvas.Nodes.Single();
        var staleNode = node with
        {
            Script = node.Script! with { SourceHash = "stale-source-hash" }
        };
        var staleDefinition = definition with
        {
            Canvases = [canvas with { Nodes = [staleNode] }]
        };

        var result = await supervisor.RunAsync(
            request with { DefinitionJson = JsonSerializer.Serialize(staleDefinition) },
            static (_, _) => ValueTask.CompletedTask);

        Assert.Equal(FlowRunStatusDto.Succeeded, result.Status);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task SupervisorRejectsExpiredDeadlinesWithoutStartingRunner()
    {
        var supervisor = CreateSupervisor();
        var request = CreateActionRequest(DateTimeOffset.UtcNow.AddMilliseconds(-1));

        var result = await supervisor.RunAsync(request, static (_, _) => ValueTask.CompletedTask);

        Assert.Equal(FlowRunStatusDto.TimedOut, result.Status);
        Assert.Equal(WorkerErrorCodes.TimedOut, result.ErrorCode);
    }

    [Fact]
    public async Task SupervisorReportsTimedOutWhenAnActiveRunnerReachesItsDeadline()
    {
        var diagnostics = new List<string>();
        var supervisor = CreateSupervisor(diagnostics.Add);
        var request = CreateScriptRequest(DateTimeOffset.UtcNow.AddMilliseconds(150));

        var result = await supervisor.RunAsync(request, static (_, _) => ValueTask.CompletedTask);

        Assert.True(
            result.Status == FlowRunStatusDto.TimedOut,
            $"Expected TimedOut but got {result.Status}; code={result.ErrorCode}; message={result.ErrorMessage}; diagnostics={string.Join(" | ", diagnostics)}");
        Assert.Equal(WorkerErrorCodes.TimedOut, result.ErrorCode);
    }

    [Fact]
    public async Task SupervisorClassifiesUnexpectedRunnerExitAsCrash()
    {
        var missingRunner = Path.Combine(Path.GetTempPath(), $"missing-runner-{Guid.NewGuid():N}.dll");
        var supervisor = new WorkerSupervisor(new RunnerLaunchOptions("dotnet", ["exec", missingRunner]));
        var request = CreateActionRequest(DateTimeOffset.UtcNow.AddSeconds(10));

        var result = await supervisor.RunAsync(request, static (_, _) => ValueTask.CompletedTask);

        Assert.Equal(FlowRunStatusDto.Failed, result.Status);
        Assert.Equal(WorkerErrorCodes.RunnerNotFound, result.ErrorCode);
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
            Assert.Equal(LibraryErrorCodes.NotAllowed, result.ErrorCode);
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
            Assert.Equal(DeviceErrorCodes.NotReady, result.ErrorCode);
            var failed = Assert.Single(events, workerEvent => workerEvent.EventType == WorkerEventType.NodeFailed);
            using var payload = JsonDocument.Parse(failed.PayloadJson);
            Assert.Equal("Failure", payload.RootElement.GetProperty("branch").GetString());
            Assert.Equal(DeviceErrorCodes.NotReady, payload.RootElement.GetProperty("errorCode").GetString());
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
            Assert.Equal(DeviceErrorCodes.Faulted, result.ErrorCode);
            var errored = Assert.Single(events, workerEvent => workerEvent.EventType == WorkerEventType.NodeErrored);
            using var payload = JsonDocument.Parse(errored.PayloadJson);
            Assert.Equal("Error", payload.RootElement.GetProperty("branch").GetString());
            Assert.Equal(DeviceErrorCodes.Faulted, payload.RootElement.GetProperty("errorCode").GetString());
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

    [Fact]
    public async Task SupervisorInjectsDeclaredLibraryServicesIntoNodeConstructors()
    {
        var packageRoot = CreateTestLibraryPackageRoot();
        try
        {
            var events = new List<WorkerEventEnvelopeDto>();
            var supervisor = CreateSupervisor(allowedLibraryPackageRoot: packageRoot);
            var request = CreateLibraryServiceRequest(
                DateTimeOffset.UtcNow.AddSeconds(15),
                packageRoot,
                typeof(ConstructorInjectionNodes),
                nameof(ConstructorInjectionNodes.Describe));

            var result = await supervisor.RunAsync(request, (workerEvent, _) =>
            {
                events.Add(workerEvent);
                return ValueTask.CompletedTask;
            });

            Assert.Equal(FlowRunStatusDto.Succeeded, result.Status);
            var completed = Assert.Single(events, workerEvent => workerEvent.EventType == WorkerEventType.NodeCompleted);
            using var payload = JsonDocument.Parse(completed.PayloadJson);
            var values = payload.RootElement
                .GetProperty("outputs")
                .GetProperty("result")
                .GetString()!
                .Split(';');

            Assert.Equal(7, values.Length);
            Assert.Equal(values[0], values[1]);
            Assert.Equal("True", values[2]);
            Assert.Equal(values[3], values[4]);
            Assert.Equal("True", values[5]);
            Assert.Equal("False", values[6]);
        }
        finally
        {
            TryDeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public async Task SupervisorRejectsForbiddenWorkerObjectsInNodeConstructors()
    {
        var packageRoot = CreateTestLibraryPackageRoot();
        try
        {
            var supervisor = CreateSupervisor(allowedLibraryPackageRoot: packageRoot);
            var request = CreateLibraryServiceRequest(
                DateTimeOffset.UtcNow.AddSeconds(15),
                packageRoot,
                typeof(ForbiddenProviderNode),
                nameof(ForbiddenProviderNode.Execute));

            var result = await supervisor.RunAsync(request, static (_, _) => ValueTask.CompletedTask);

            Assert.Equal(FlowRunStatusDto.Failed, result.Status);
            Assert.Equal(LibraryErrorCodes.ServiceDependencyForbidden, result.ErrorCode);
        }
        finally
        {
            TryDeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public async Task SupervisorScopesDeclaredLibraryServicesToOneWorkerRun()
    {
        var packageRoot = CreateTestLibraryPackageRoot();
        try
        {
            var events = new List<WorkerEventEnvelopeDto>();
            var supervisor = CreateSupervisor(allowedLibraryPackageRoot: packageRoot);
            var request = CreateTwoNodeLibraryServiceRequest(
                DateTimeOffset.UtcNow.AddSeconds(15),
                packageRoot,
                typeof(ConstructorInjectionNodes),
                nameof(ConstructorInjectionNodes.DescribeRunAndInvocationScopes));

            var result = await supervisor.RunAsync(request, (workerEvent, _) =>
            {
                events.Add(workerEvent);
                return ValueTask.CompletedTask;
            });

            Assert.Equal(FlowRunStatusDto.Succeeded, result.Status);
            var first = GetLibraryNodeResult(events, "library-service-first").Split(';');
            var second = GetLibraryNodeResult(events, "library-service-second").Split(';');

            Assert.Equal(2, first.Length);
            Assert.Equal(2, second.Length);
            Assert.Equal(first[0], second[0]);
            Assert.NotEqual(first[1], second[1]);
        }
        finally
        {
            TryDeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public async Task SupervisorDeliversJsonMessageToRegisteredActiveFlipflopEndpoint()
    {
        var packageRoot = CreateTestLibraryPackageRoot();
        using var cancellation = new CancellationTokenSource();
        try
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var supervisor = CreateSupervisor(allowedLibraryPackageRoot: packageRoot);
            var request = CreateLibraryExternalMessageRequest(DateTimeOffset.UtcNow.AddSeconds(15), packageRoot);
            var runTask = supervisor.RunAsync(
                request,
                (workerEvent, _) =>
                {
                    if (workerEvent.EventType == WorkerEventType.NodeStarted)
                        started.TrySetResult();
                    if (workerEvent.EventType == WorkerEventType.NodeCompleted)
                        completed.TrySetResult();
                    return ValueTask.CompletedTask;
                },
                cancellation.Token);

            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var messageId = Guid.NewGuid();
            var delivery = new WorkerMessageDeliveryDto(
                WorkerProtocol.Version,
                request.RunId,
                messageId,
                "test.message.inbox",
                WorkerMessageChannelKindDto.Queue,
                WorkerMessageSerializationModeDto.Json,
                "test.text",
                JsonSerializer.Serialize("remote hello"),
                DateTimeOffset.UtcNow);

            WorkerMessageDeliveryResponseDto? response = null;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                response = await supervisor.DeliverMessageAsync(delivery);
                if (response.Status != WorkerMessageDeliveryStatusDto.NotReady)
                    break;
                await Task.Delay(50);
            }

            Assert.NotNull(response);
            Assert.Equal(WorkerMessageDeliveryStatusDto.Accepted, response!.Status);
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();

            var result = await runTask.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(FlowRunStatusDto.Cancelled, result.Status);
        }
        finally
        {
            TryDeleteDirectory(packageRoot);
        }
    }

    [Theory]
    [InlineData(WorkerMessageChannelKindDto.Queue, "test.message.inbox", "ReceiveExternal")]
    [InlineData(WorkerMessageChannelKindDto.EventBus, "test.message.events", "ReceiveExternalEvent")]
    public async Task SupervisorDeliveryWakesFlipflopAndExecutesSuccessor(
        WorkerMessageChannelKindDto channelKind,
        string topic,
        string flipflopMethodName)
    {
        var packageRoot = CreateTestLibraryPackageRoot();
        using var cancellation = new CancellationTokenSource();
        try
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var successorCompleted = new TaskCompletionSource<WorkerEventEnvelopeDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            var successorCompletionCount = 0;
            var supervisor = CreateSupervisor(allowedLibraryPackageRoot: packageRoot);
            var request = CreateLibraryExternalMessageChainRequest(
                DateTimeOffset.UtcNow.AddSeconds(15),
                packageRoot,
                channelKind,
                topic,
                flipflopMethodName);
            var runTask = supervisor.RunAsync(
                request,
                (workerEvent, _) =>
                {
                    if (workerEvent.EventType == WorkerEventType.NodeStarted
                        && workerEvent.NodeId == "message-flipflop")
                    {
                        started.TrySetResult();
                    }

                    if (workerEvent.EventType == WorkerEventType.NodeCompleted
                        && workerEvent.NodeId == "message-action")
                    {
                        Interlocked.Increment(ref successorCompletionCount);
                        successorCompleted.TrySetResult(workerEvent);
                    }

                    return ValueTask.CompletedTask;
                },
                cancellation.Token);

            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var delivery = new WorkerMessageDeliveryDto(
                WorkerProtocol.Version,
                request.RunId,
                Guid.NewGuid(),
                topic,
                channelKind,
                WorkerMessageSerializationModeDto.Json,
                "test.text",
                JsonSerializer.Serialize("remote hello"),
                DateTimeOffset.UtcNow);

            WorkerMessageDeliveryResponseDto? response = null;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                response = await supervisor.DeliverMessageAsync(delivery);
                if (response.Status != WorkerMessageDeliveryStatusDto.NotReady)
                    break;
                await Task.Delay(50);
            }

            Assert.NotNull(response);
            Assert.Equal(WorkerMessageDeliveryStatusDto.Accepted, response!.Status);
            var duplicate = await supervisor.DeliverMessageAsync(delivery);
            Assert.Equal(WorkerMessageDeliveryStatusDto.Accepted, duplicate.Status);
            Assert.True(duplicate.Duplicate);
            var completed = await successorCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var payload = JsonDocument.Parse(completed.PayloadJson);
            Assert.Equal("processed:remote hello", payload.RootElement
                .GetProperty("outputs").GetProperty("result").GetString());

            cancellation.Cancel();
            var result = await runTask.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(FlowRunStatusDto.Cancelled, result.Status);
            Assert.Equal(1, Volatile.Read(ref successorCompletionCount));
        }
        finally
        {
            TryDeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public async Task SupervisorDebugSessionAcceptsMessageDeliveryThroughTheSameRunHandle()
    {
        var packageRoot = CreateTestLibraryPackageRoot();
        try
        {
            var successorCompleted = new TaskCompletionSource<WorkerEventEnvelopeDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            var supervisor = CreateSupervisor(allowedLibraryPackageRoot: packageRoot);
            var request = CreateLibraryExternalMessageChainRequest(
                DateTimeOffset.UtcNow.AddSeconds(15),
                packageRoot,
                WorkerMessageChannelKindDto.Queue,
                "test.message.inbox",
                nameof(MessageNodes.ReceiveExternal)) with
            {
                Debug = new WorkerDebugOptionsDto(Guid.NewGuid(), [])
            };
            await using var debug = await supervisor.StartDebugAsync(request, (workerEvent, _) =>
            {
                if (workerEvent.EventType == WorkerEventType.NodeCompleted
                    && workerEvent.NodeId == "message-action")
                {
                    successorCompleted.TrySetResult(workerEvent);
                }

                return ValueTask.CompletedTask;
            });
            var messageId = Guid.NewGuid();
            var delivery = new WorkerMessageDeliveryDto(
                WorkerProtocol.Version,
                request.RunId,
                messageId,
                "test.message.inbox",
                WorkerMessageChannelKindDto.Queue,
                WorkerMessageSerializationModeDto.Json,
                "test.text",
                JsonSerializer.Serialize("debug hello"),
                DateTimeOffset.UtcNow);

            WorkerMessageDeliveryResponseDto? response = null;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                response = await supervisor.DeliverMessageAsync(delivery);
                if (response.Status != WorkerMessageDeliveryStatusDto.NotReady)
                    break;
                await Task.Delay(50);
            }

            Assert.NotNull(response);
            Assert.Equal(WorkerMessageDeliveryStatusDto.Accepted, response!.Status);
            var completed = await successorCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using (var payload = JsonDocument.Parse(completed.PayloadJson))
                Assert.Equal("processed:debug hello", payload.RootElement.GetProperty("outputs").GetProperty("result").GetString());
            await debug.StopAsync(1);
            var result = await debug.Completion.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(FlowRunStatusDto.Cancelled, result.Status);
        }
        finally
        {
            TryDeleteDirectory(packageRoot);
        }
    }

    private static WorkerSupervisor CreateSupervisor(
        Action<string>? diagnosticLogger = null,
        string? allowedLibraryPackageRoot = null,
        string? allowedWorkpieceRoot = null)
        => new(new RunnerLaunchOptions(
            "dotnet",
            [typeof(RunnerHost).Assembly.Location],
            HandshakeTimeout: TimeSpan.FromSeconds(10),
            HeartbeatInterval: TimeSpan.FromMilliseconds(100),
            CancellationGracePeriod: TimeSpan.FromSeconds(2),
            AllowedLibraryPackageRoot: allowedLibraryPackageRoot,
            DiagnosticLogger: diagnosticLogger,
            AllowedWorkpieceRoot: allowedWorkpieceRoot));

    private static WorkerRunRequestDto CreateActionRequest(DateTimeOffset deadline)
    {
        var flowId = Guid.NewGuid();
        var action = new NodeDto("action", NodeTypeDto.Action, "Action", 0, 0, [], [], null);
        return CreateRequest(flowId, action, deadline);
    }

    private static WorkerRunRequestDto CreateTwoNodeDebugRequest(DateTimeOffset deadline)
    {
        const string firstSource = "return 1";
        const string secondSource = "return 2";
        var flowId = Guid.NewGuid();
        var first = new NodeDto(
            "first",
            NodeTypeDto.Script,
            "First",
            0,
            0,
            [],
            [],
            new ScriptNodeDataDto(
                "first",
                firstSource,
                "1",
                SereinFlow.Domain.ScriptNodeDefinition.ComputeSourceHash(firstSource),
                [],
                []));
        var second = new NodeDto(
            "second",
            NodeTypeDto.Script,
            "Second",
            0,
            0,
            [],
            [],
            new ScriptNodeDataDto(
                "second",
                secondSource,
                "1",
                SereinFlow.Domain.ScriptNodeDefinition.ComputeSourceHash(secondSource),
                [],
                []));
        var connection = new ConnectionDto(
            "first:success->second:execute",
            first.Id,
            "success",
            second.Id,
            "execute",
            ConnectionKindDto.Execution,
            ExecutionBranchDto.Success,
            null,
            0);
        var definition = new FlowDefinitionDto(
            flowId,
            5,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [first, second], [connection])],
            first.Id,
            "test",
            RunPolicy: new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel));
        return new WorkerRunRequestDto(
            WorkerProtocol.Version,
            Guid.NewGuid(),
            flowId,
            1,
            JsonSerializer.Serialize(definition),
            deadline,
            Debug: new WorkerDebugOptionsDto(Guid.NewGuid(), [first.Id]));
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

    private static WorkerRunRequestDto CreateScriptLogRequest(DateTimeOffset deadline)
    {
        const string source = """
            import { log, env } from "sereinflow"
            log.info("Started by script")
            return env.nodeId
            """;
        var flowId = Guid.NewGuid();
        var script = new ScriptNodeDataDto(
            "script",
            source,
            "1",
            SereinFlow.Domain.ScriptNodeDefinition.ComputeSourceHash(source),
            [],
            []);
        var node = new NodeDto("script", NodeTypeDto.Script, "Script", 0, 0, [], [], script);
        return CreateRequest(flowId, node, deadline);
    }

    private static WorkerRunRequestDto CreateLibraryServiceRequest(
        DateTimeOffset deadline,
        string packageRoot,
        Type nodeType,
        string methodName)
    {
        var flowId = Guid.NewGuid();
        var action = new NodeDto(
            "library-service-action",
            NodeTypeDto.Action,
            "Library service action",
            0,
            0,
            [],
            [],
            null,
            new NodeUiMetadataDto(
                "library-service-action",
                "node.libraryService.title",
                "node.libraryService.subtitle",
                null,
                "ready",
                true,
                null,
                Category: "library",
                LibraryId: TestLibraryArtifactId,
                ClassName: nodeType.FullName,
                MethodName: methodName,
                DllName: Path.GetFileName(nodeType.Assembly.Location),
                DllVersion: "1.0.0",
                ReturnType: "System.String"));
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
            AllowedLibraryIds: [TestLibraryArtifactId]);
    }

    private static WorkerRunRequestDto CreateTwoNodeLibraryServiceRequest(
        DateTimeOffset deadline,
        string packageRoot,
        Type nodeType,
        string methodName)
    {
        var flowId = Guid.NewGuid();
        var first = CreateLibraryServiceNode(
            "library-service-first",
            "First library service action",
            nodeType,
            methodName);
        var second = CreateLibraryServiceNode(
            "library-service-second",
            "Second library service action",
            nodeType,
            methodName);
        var connection = new ConnectionDto(
            "library-service-first:success->library-service-second:execute",
            first.Id,
            "success",
            second.Id,
            "execute",
            ConnectionKindDto.Execution,
            ExecutionBranchDto.Success,
            null,
            0);
        var definition = new FlowDefinitionDto(
            flowId,
            5,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [first, second], [connection])],
            first.Id,
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
            AllowedLibraryIds: [TestLibraryArtifactId]);
    }

    private static NodeDto CreateLibraryServiceNode(
        string id,
        string name,
        Type nodeType,
        string methodName)
        => new(
            id,
            NodeTypeDto.Action,
            name,
            0,
            0,
            [],
            [],
            null,
            new NodeUiMetadataDto(
                id,
                "node.libraryService.title",
                "node.libraryService.subtitle",
                null,
                "ready",
                true,
                null,
                Category: "library",
                LibraryId: TestLibraryArtifactId,
                ClassName: nodeType.FullName,
                MethodName: methodName,
                DllName: Path.GetFileName(nodeType.Assembly.Location),
                DllVersion: "1.0.0",
                ReturnType: "System.String"));

    private static string GetLibraryNodeResult(
        IReadOnlyCollection<WorkerEventEnvelopeDto> events,
        string nodeId)
    {
        var completed = Assert.Single(events, workerEvent =>
            workerEvent.EventType == WorkerEventType.NodeCompleted
            && workerEvent.NodeId == nodeId);
        using var payload = JsonDocument.Parse(completed.PayloadJson);
        return payload.RootElement
            .GetProperty("outputs")
            .GetProperty("result")
            .GetString()!;
    }

    private static WorkerRunRequestDto CreateLibraryActionRequest(
        DateTimeOffset deadline,
        string packageRoot,
        IReadOnlyList<string> allowedLibraryIds,
        string methodName = "计算合格率",
        IReadOnlyList<NodeParameterDto>? parameters = null,
        string returnType = "System.Decimal",
        string? workpieceRoot = null)
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
            AllowedLibraryIds: allowedLibraryIds,
            WorkpieceRootPath: workpieceRoot);
    }

    private static WorkerRunRequestDto CreateLibraryGlobalFlipflopDebugRequest(
        DateTimeOffset deadline,
        string packageRoot,
        IReadOnlyList<string> breakpointNodeIds,
        int intervalMilliseconds)
    {
        var flowId = Guid.NewGuid();
        var flipflop = new NodeDto(
            "library-flipflop",
            NodeTypeDto.Flipflop,
            "Wait for device trigger",
            0,
            0,
            [],
            [
                new NodeParameterDto("设备编号", "\"debug-device\"", DataSourceDto.Literal, true),
                new NodeParameterDto("轮询间隔毫秒", intervalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture), DataSourceDto.Literal, false)
            ],
            null,
            new NodeUiMetadataDto(
                "library-flipflop",
                "node.libraryFlipflop.title",
                "node.libraryFlipflop.subtitle",
                null,
                "ready",
                true,
                null,
                Category: "library",
                LibraryId: TestLibraryArtifactId,
                ClassName: typeof(生产线节点).FullName,
                MethodName: "等待设备触发",
                DllName: Path.GetFileName(typeof(生产线节点).Assembly.Location),
                DllVersion: "1.0.0",
                ReturnType: "System.Boolean",
                IsAwaitable: true));
        var definition = new FlowDefinitionDto(
            flowId,
            5,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [flipflop], [])],
            string.Empty,
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
            AllowedLibraryIds: [TestLibraryArtifactId],
            Debug: new WorkerDebugOptionsDto(Guid.NewGuid(), breakpointNodeIds));
    }

    private static WorkerRunRequestDto CreateLibraryExternalMessageRequest(
        DateTimeOffset deadline,
        string packageRoot)
    {
        var flowId = Guid.NewGuid();
        var flipflop = new NodeDto(
            "message-flipflop",
            NodeTypeDto.Flipflop,
            "External message listener",
            0,
            0,
            [],
            [],
            null,
            new NodeUiMetadataDto(
                "message-flipflop",
                NodeErrorCodes.MessageTitle,
                NodeErrorCodes.MessageSubtitle,
                null,
                "ready",
                true,
                null,
                Category: "library",
                LibraryId: TestLibraryArtifactId,
                ClassName: typeof(MessageNodes).FullName,
                MethodName: nameof(MessageNodes.ReceiveExternal),
                DllName: Path.GetFileName(typeof(MessageNodes).Assembly.Location),
                DllVersion: "1.0.0",
                ReturnType: "System.String",
                IsAwaitable: true));
        var definition = new FlowDefinitionDto(
            flowId,
            5,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [flipflop], [])],
            string.Empty,
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
            AllowedLibraryIds: [TestLibraryArtifactId]);
    }

    private static WorkerRunRequestDto CreateLibraryExternalMessageChainRequest(
        DateTimeOffset deadline,
        string packageRoot,
        WorkerMessageChannelKindDto channelKind,
        string topic,
        string flipflopMethodName)
    {
        var flowId = Guid.NewGuid();
        var flipflop = new NodeDto(
            "message-flipflop",
            NodeTypeDto.Flipflop,
            "External message listener",
            0,
            0,
            [],
            [],
            null,
            new NodeUiMetadataDto(
                "message-flipflop",
                NodeErrorCodes.MessageTitle,
                NodeErrorCodes.MessageSubtitle,
                null,
                "ready",
                true,
                null,
                Category: "library",
                LibraryId: TestLibraryArtifactId,
                ClassName: typeof(MessageNodes).FullName,
                MethodName: flipflopMethodName,
                DllName: Path.GetFileName(typeof(MessageNodes).Assembly.Location),
                DllVersion: "1.0.0",
                ReturnType: "System.String",
                IsAwaitable: true));
        var action = new NodeDto(
            "message-action",
            NodeTypeDto.Action,
            "Process external message",
            0,
            0,
            [],
            [new NodeParameterDto("message", null, DataSourceDto.PreviousNode, true)],
            null,
            new NodeUiMetadataDto(
                "message-action",
                NodeErrorCodes.MessageActionTitle,
                NodeErrorCodes.MessageActionSubtitle,
                null,
                "ready",
                true,
                null,
                Category: "library",
                LibraryId: TestLibraryArtifactId,
                ClassName: typeof(MessageNodes).FullName,
                MethodName: nameof(MessageNodes.ProcessExternal),
                DllName: Path.GetFileName(typeof(MessageNodes).Assembly.Location),
                DllVersion: "1.0.0",
                ReturnType: "System.String"));
        var execution = new ConnectionDto(
            "message-flipflop:success->message-action:execute",
            flipflop.Id,
            "success",
            action.Id,
            "execute",
            ConnectionKindDto.Execution,
            ExecutionBranchDto.Success,
            null,
            0);
        var data = new ConnectionDto(
            "message-flipflop:data-out->message-action:message",
            flipflop.Id,
            "data-out",
            action.Id,
            "message",
            ConnectionKindDto.Data,
            null,
            DataSourceDto.PreviousNode,
            0);
        var definition = new FlowDefinitionDto(
            flowId,
            5,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [flipflop, action], [execution, data])],
            string.Empty,
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
            AllowedLibraryIds: [TestLibraryArtifactId]);
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

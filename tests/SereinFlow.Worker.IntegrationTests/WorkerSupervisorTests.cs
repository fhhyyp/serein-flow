using System.Collections.Generic;
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
    public async Task LongFlowUsesOrderedBatchesAndDrainsEveryEventBeforeReturning()
    {
        var flowId = Guid.NewGuid();
        var nodes = Enumerable.Range(0, 300).Select(index =>
        {
            var operationResult1 = new NodeDto($"node-{index}", NodeTypeDto.Action, "Action", 0, 0, [], [], null);
            return operationResult1;
        }).ToArray();
        var edges = Enumerable.Range(1, nodes.Length - 1).Select(index =>
        {
            var operationResult2 = new ConnectionDto($"edge-{index}", nodes[index - 1].Id, "success", nodes[index].Id, "execute", ConnectionKindDto.Execution, ExecutionBranchDto.Success, null, 0);
            return operationResult2;
        }).ToArray();
        CanvasDto[] canvases = [new CanvasDto("main", CanvasLifecycleDto.Main, nodes, edges)];
        var definition = new FlowDefinitionDto(flowId, 5, 1, canvases, nodes[0].Id, "test");
        var operationResult3 = Guid.NewGuid();
        var operationResult4 = JsonSerializer.Serialize(definition);
        var operationResult5 = DateTimeOffset.UtcNow.AddSeconds(20);
        var request = new WorkerRunRequestDto(WorkerProtocol.Version, operationResult3, flowId, 1, operationResult4, operationResult5);
        var received = new List<long>();
        var batchSizes = new List<int>();
        var result = await CreateSupervisor().RunAsync(request, (_, _) =>
        {
            throw new InvalidOperationException("The batch sink must be used.");
        }, publishBatch: async (batch, token) =>
        {
            await Task.Delay(5, token);
            batchSizes.Add(batch.Count);
            var operationResult1 = batch.Select(item => item.Sequence);
            received.AddRange(operationResult1);
        });
        Assert.Equal(FlowRunStatusDto.Succeeded, result.Status);
        var operationResult6 = Enumerable.Range(1, 601).Select(index => (long)index);
        Assert.Equal(operationResult6, received);
        Assert.Contains(batchSizes, count => count > 1);
    }

    [Fact]
    public async Task SlowEventSinkDoesNotBlockDebugVariableResponseAndCompletionDrainsEvents()
    {
        var supervisor = CreateSupervisor();
        var operationResult9 = DateTimeOffset.UtcNow.AddSeconds(15);
        var request = CreateTwoNodeDebugRequest(operationResult9);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var events = new List<WorkerEventEnvelopeDto>();
        await using var debug = await supervisor.StartDebugAsync(request, async (item, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            events.Add(item);
        });
        try
        {
            var operationResult7 = TimeSpan.FromSeconds(10);
            await entered.Task.WaitAsync(operationResult7);
            var operationResult8 = new WorkerVariableRequestDto(WorkerProtocol.Version, request.RunId, "list");
            var response = await supervisor.AccessVariablesAsync(operationResult8);
            Assert.Equal(200, response.StatusCode);
            Assert.Empty(events);
            await debug.StopAsync(1);
            Assert.False(debug.Completion.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
        }

        var operationResult10 = TimeSpan.FromSeconds(10);
        var result = await debug.Completion.WaitAsync(operationResult10);
        Assert.Equal(FlowRunStatusDto.Cancelled, result.Status);
        Assert.NotEmpty(events);
    }

    [Fact]
    public async Task SupervisorRunsDisposableRunnerAndForwardsOrderedEvents()
    {
        var events = new List<WorkerEventEnvelopeDto>();
        var supervisor = CreateSupervisor();
        var addSecondsResult = DateTimeOffset.UtcNow.AddSeconds(15);
        var request = CreateActionRequest(addSecondsResult);
        var result = await supervisor.RunAsync(request, (workerEvent, _) =>
        {
            events.Add(workerEvent);
            return ValueTask.CompletedTask;
        });
        Assert.Equal(FlowRunStatusDto.Succeeded, result.Status);
        Assert.Null(result.ErrorCode);
        Assert.NotEmpty(events);
        Assert.All(events, workerEvent =>
        {
            Assert.Equal(request.RunId, workerEvent.RunId);
        });
        var selectResult = events
            .OrderBy(workerEvent => workerEvent.Sequence)
            .Select(workerEvent => workerEvent.Sequence);
        var selectResult1 = events.Select(workerEvent => workerEvent.Sequence);
        Assert.Equal(selectResult, selectResult1);
        Assert.Contains(events, workerEvent => workerEvent.EventType == WorkerEventType.NodeStarted);
        Assert.Contains(events, workerEvent => workerEvent.EventType == WorkerEventType.NodeCompleted);
    }

    [Fact]
    public async Task SupervisorWorkerRunClientUsesDotnetExecForManagedRunnerAssembly()
    {
        var events = new List<WorkerEventEnvelopeDto>();
        var fromSecondsResult = TimeSpan.FromSeconds(10);
        var fromMillisecondsResult = TimeSpan.FromMilliseconds(100);
        var fromSecondsResult2 = TimeSpan.FromSeconds(2);
        var supervisorWorkerRunClientOptions = new SupervisorWorkerRunClientOptions(typeof(RunnerHost).Assembly.Location, RunnerFileName: "dotnet", HandshakeTimeout: fromSecondsResult, HeartbeatInterval: fromMillisecondsResult, CancellationGracePeriod: fromSecondsResult2);
        var client = new SupervisorWorkerRunClient(supervisorWorkerRunClientOptions);
        var addSecondsResult3 = DateTimeOffset.UtcNow.AddSeconds(15);
        var createActionRequestResult = CreateActionRequest(addSecondsResult3);
        var delegateWorkerRunEventSink = new DelegateWorkerRunEventSink((workerEvent, _) =>
        {
            events.Add(workerEvent);
            return ValueTask.CompletedTask;
        });
        var result = await client.RunAsync(createActionRequestResult, delegateWorkerRunEventSink);
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
        var addSecondsResult8 = DateTimeOffset.UtcNow.AddSeconds(15);
        var request = CreateTwoNodeDebugRequest(addSecondsResult8);
        await using var debug = await supervisor.StartDebugAsync(request, (workerEvent, _) =>
        {
            events.Add(workerEvent);
            if (workerEvent.EventType == WorkerEventType.DebugPaused)
            {
                using var payload = JsonDocument.Parse(workerEvent.PayloadJson);
                var getPropertyResult = payload.RootElement.GetProperty("nodeId");
                var nodeId = getPropertyResult.GetString();
                if (nodeId == "first")
                {
                    firstPause.TrySetResult(workerEvent);
                }
                else if (nodeId == "second")
                {
                    secondPause.TrySetResult(workerEvent);
                }
            }

            return ValueTask.CompletedTask;
        });
        var fromSecondsResult9 = TimeSpan.FromSeconds(10);
        var first = await firstPause.Task.WaitAsync(fromSecondsResult9);
        using (var payload = JsonDocument.Parse(first.PayloadJson))
        {
            var getPropertyResult4 = payload.RootElement.GetProperty("nodeId");
            var getStringResult = getPropertyResult4.GetString();
            Assert.Equal("first", getStringResult);
            var getPropertyResult5 = payload.RootElement.GetProperty("step");
            var getInt32Result = getPropertyResult5.GetInt32();
            Assert.Equal(1, getInt32Result);
            var getPropertyResult6 = payload.RootElement.GetProperty("frameDepth");
            var getInt32Result7 = getPropertyResult6.GetInt32();
            Assert.Equal(0, getInt32Result7);
        }

        Assert.DoesNotContain(events, item => item.EventType == WorkerEventType.NodeCompleted && item.NodeId == "first");
        await debug.StepAsync(1);
        var fromSecondsResult10 = TimeSpan.FromSeconds(10);
        var second = await secondPause.Task.WaitAsync(fromSecondsResult10);
        using (var payload = JsonDocument.Parse(second.PayloadJson))
        {
            var getPropertyResult1 = payload.RootElement.GetProperty("nodeId");
            var getStringResult2 = getPropertyResult1.GetString();
            Assert.Equal("second", getStringResult2);
        }

        Assert.Single(events, item => item.EventType == WorkerEventType.NodeCompleted && item.NodeId == "first");
        Assert.DoesNotContain(events, item => item.EventType == WorkerEventType.NodeCompleted && item.NodeId == "second");
        await debug.ContinueAsync(2);
        var fromSecondsResult11 = TimeSpan.FromSeconds(10);
        var result = await debug.Completion.WaitAsync(fromSecondsResult11);
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
            var addSecondsResult12 = DateTimeOffset.UtcNow.AddSeconds(15);
            var request = CreateLibraryGlobalFlipflopDebugRequest(addSecondsResult12, packageRoot, breakpointNodeIds: [], intervalMilliseconds: 3_600_000);
            await using var debug = await supervisor.StartDebugAsync(request, (workerEvent, _) =>
            {
                if (workerEvent.EventType == WorkerEventType.NodeStarted && workerEvent.NodeId == "library-flipflop")
                {
                    listenerStarted.TrySetResult();
                }

                return ValueTask.CompletedTask;
            });
            var fromSecondsResult13 = TimeSpan.FromSeconds(10);
            await listenerStarted.Task.WaitAsync(fromSecondsResult13);
            await Task.Delay(100);
            await debug.StopAsync(1);
            var fromSecondsResult14 = TimeSpan.FromSeconds(10);
            var result = await debug.Completion.WaitAsync(fromSecondsResult14);
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
            var addSecondsResult18 = DateTimeOffset.UtcNow.AddSeconds(15);
            var request = CreateLibraryGlobalFlipflopDebugRequest(addSecondsResult18, packageRoot, breakpointNodeIds: ["library-flipflop"], intervalMilliseconds: 50);
            await using var debug = await supervisor.StartDebugAsync(request, (workerEvent, _) =>
            {
                if (workerEvent.EventType == WorkerEventType.DebugPaused)
                {
                    paused.TrySetResult(workerEvent);
                }

                return ValueTask.CompletedTask;
            });
            var fromSecondsResult19 = TimeSpan.FromSeconds(10);
            var pause = await paused.Task.WaitAsync(fromSecondsResult19);
            using (var payload = JsonDocument.Parse(pause.PayloadJson))
            {
                var getPropertyResult15 = payload.RootElement.GetProperty("nodeId");
                var getStringResult16 = getPropertyResult15.GetString();
                Assert.Equal("library-flipflop", getStringResult16);
                var getPropertyResult17 = payload.RootElement.GetProperty("triggerInvocationId");
                var getGuidResult = getPropertyResult17.GetGuid();
                Assert.NotEqual(Guid.Empty, getGuidResult);
            }

            await debug.StopAsync(1);
            var fromSecondsResult20 = TimeSpan.FromSeconds(10);
            var result = await debug.Completion.WaitAsync(fromSecondsResult20);
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
        var addSecondsResult21 = DateTimeOffset.UtcNow.AddSeconds(15);
        var request = CreateScriptRequest(addSecondsResult21);
        using var cancellation = new CancellationTokenSource();
        var runTask = supervisor.RunAsync(request, (workerEvent, _) =>
        {
            if (workerEvent.EventType == WorkerEventType.NodeStarted)
            {
                started.TrySetResult();
            }

            return ValueTask.CompletedTask;
        }, cancellation.Token);
        var fromSecondsResult22 = TimeSpan.FromSeconds(10);
        await started.Task.WaitAsync(fromSecondsResult22);
        cancellation.Cancel();
        var fromSecondsResult23 = TimeSpan.FromSeconds(10);
        var result = await runTask.WaitAsync(fromSecondsResult23);
        var evaluatedValue = $"Expected a cancelled worker result but received {result.Status}; code={result.ErrorCode}; message={result.ErrorMessage}; diagnostics={string.Join(" | ", diagnostics)}";
        Assert.True(result.Status == FlowRunStatusDto.Cancelled, evaluatedValue);
        Assert.Equal(WorkerErrorCodes.Cancelled, result.ErrorCode);
    }

    [Fact]
    public async Task SupervisorForwardsSereinFlowScriptLogsAsProtocolEvents()
    {
        var events = new List<WorkerEventEnvelopeDto>();
        var supervisor = CreateSupervisor();
        var addSecondsResult24 = DateTimeOffset.UtcNow.AddSeconds(15);
        var request = CreateScriptLogRequest(addSecondsResult24);
        var result = await supervisor.RunAsync(request, (workerEvent, _) =>
        {
            events.Add(workerEvent);
            return ValueTask.CompletedTask;
        });
        Assert.Equal(FlowRunStatusDto.Succeeded, result.Status);
        var log = Assert.Single(events, item => item.EventType == WorkerEventType.Log);
        using var payload = JsonDocument.Parse(log.PayloadJson);
        var getPropertyResult25 = payload.RootElement.GetProperty("level");
        var getStringResult26 = getPropertyResult25.GetString();
        Assert.Equal("info", getStringResult26);
        var getPropertyResult27 = payload.RootElement.GetProperty("message");
        var getStringResult28 = getPropertyResult27.GetString();
        Assert.Equal("Started by script", getStringResult28);
        var getPropertyResult29 = payload.RootElement.GetProperty("frameDepth");
        var getInt32Result30 = getPropertyResult29.GetInt32();
        Assert.Equal(0, getInt32Result30);
    }

    [Fact]
    public async Task SupervisorDerivesScriptHashFromSourceWhenSerializedSnapshotHashIsStale()
    {
        var supervisor = CreateSupervisor();
        var addSecondsResult31 = DateTimeOffset.UtcNow.AddSeconds(15);
        var request = CreateScriptLogRequest(addSecondsResult31);
        var definition = JsonSerializer.Deserialize<FlowDefinitionDto>(request.DefinitionJson)!;
        var canvas = definition.Canvases.Single();
        var node = canvas.Nodes.Single();
        var staleNode = node with
        {
            Script = node.Script! with
            {
                SourceHash = "stale-source-hash"
            }
        };
        var staleDefinition = definition with
        {
            Canvases = [canvas with
            {
                Nodes = [staleNode]
            }

            ]
        };
        var evaluatedValue32 = request with
        {
            DefinitionJson = JsonSerializer.Serialize(staleDefinition)
        };
        var result = await supervisor.RunAsync(evaluatedValue32, static (_, _) => ValueTask.CompletedTask);
        Assert.Equal(FlowRunStatusDto.Succeeded, result.Status);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task SupervisorRejectsExpiredDeadlinesWithoutStartingRunner()
    {
        var supervisor = CreateSupervisor();
        var addMillisecondsResult = DateTimeOffset.UtcNow.AddMilliseconds(-1);
        var request = CreateActionRequest(addMillisecondsResult);
        var result = await supervisor.RunAsync(request, static (_, _) => ValueTask.CompletedTask);
        Assert.Equal(FlowRunStatusDto.TimedOut, result.Status);
        Assert.Equal(WorkerErrorCodes.TimedOut, result.ErrorCode);
    }

    [Fact]
    public async Task SupervisorReportsTimedOutWhenAnActiveRunnerReachesItsDeadline()
    {
        var diagnostics = new List<string>();
        var supervisor = CreateSupervisor(diagnostics.Add);
        var addMillisecondsResult33 = DateTimeOffset.UtcNow.AddMilliseconds(150);
        var request = CreateScriptRequest(addMillisecondsResult33);
        var result = await supervisor.RunAsync(request, static (_, _) => ValueTask.CompletedTask);
        var evaluatedValue34 = $"Expected TimedOut but got {result.Status}; code={result.ErrorCode}; message={result.ErrorMessage}; diagnostics={string.Join(" | ", diagnostics)}";
        Assert.True(result.Status == FlowRunStatusDto.TimedOut, evaluatedValue34);
        Assert.Equal(WorkerErrorCodes.TimedOut, result.ErrorCode);
    }

    [Fact]
    public async Task SupervisorClassifiesUnexpectedRunnerExitAsCrash()
    {
        var getTempPathResult = Path.GetTempPath();
        var evaluatedValue35 = $"missing-runner-{Guid.NewGuid():N}.dll";
        var missingRunner = Path.Combine(getTempPathResult, evaluatedValue35);
        var runnerLaunchOptions = new RunnerLaunchOptions("dotnet", ["exec", missingRunner]);
        var supervisor = new WorkerSupervisor(runnerLaunchOptions);
        var addSecondsResult36 = DateTimeOffset.UtcNow.AddSeconds(10);
        var request = CreateActionRequest(addSecondsResult36);
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
            var addSecondsResult37 = DateTimeOffset.UtcNow.AddSeconds(15);
            var request = CreateLibraryActionRequest(addSecondsResult37, packageRoot, []);
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
            var addSecondsResult38 = DateTimeOffset.UtcNow.AddSeconds(15);
            var request = CreateLibraryActionRequest(addSecondsResult38, packageRoot, [TestLibraryArtifactId]);
            var result = await supervisor.RunAsync(request, (workerEvent, _) =>
            {
                events.Add(workerEvent);
                return ValueTask.CompletedTask;
            });
            Assert.Equal(FlowRunStatusDto.Succeeded, result.Status);
            var completed = Assert.Single(events, workerEvent => workerEvent.EventType == WorkerEventType.NodeCompleted);
            using var payload = JsonDocument.Parse(completed.PayloadJson);
            var getPropertyResult39 = payload.RootElement.GetProperty("outputs");
            var getPropertyResult40 = getPropertyResult39.GetProperty("result");
            var getDecimalResult = getPropertyResult40.GetDecimal();
            Assert.Equal(50m, getDecimalResult);
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
            var addSecondsResult41 = DateTimeOffset.UtcNow.AddSeconds(15);
            IReadOnlyList<NodeParameterDto> evaluatedValue42 = [new NodeParameterDto("设备状态", "\"告警\"", DataSourceDto.Literal, true)];
            var request = CreateLibraryActionRequest(addSecondsResult41, packageRoot, [TestLibraryArtifactId], methodName: "按设备状态选择分支", parameters: evaluatedValue42, returnType: "System.String");
            var result = await supervisor.RunAsync(request, (workerEvent, _) =>
            {
                events.Add(workerEvent);
                return ValueTask.CompletedTask;
            });
            Assert.Equal(FlowRunStatusDto.Failed, result.Status);
            Assert.Equal(DeviceErrorCodes.NotReady, result.ErrorCode);
            var failed = Assert.Single(events, workerEvent => workerEvent.EventType == WorkerEventType.NodeFailed);
            using var payload = JsonDocument.Parse(failed.PayloadJson);
            var getPropertyResult43 = payload.RootElement.GetProperty("branch");
            var getStringResult44 = getPropertyResult43.GetString();
            Assert.Equal("Failure", getStringResult44);
            var getPropertyResult45 = payload.RootElement.GetProperty("errorCode");
            var getStringResult46 = getPropertyResult45.GetString();
            Assert.Equal(DeviceErrorCodes.NotReady, getStringResult46);
            var getPropertyResult47 = payload.RootElement.GetProperty("inputs");
            var selectResult48 = getPropertyResult47.EnumerateObject()
                .Select(item => item.Name);
            Assert.DoesNotContain("流程上下文", selectResult48);
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
            var addSecondsResult49 = DateTimeOffset.UtcNow.AddSeconds(15);
            IReadOnlyList<NodeParameterDto> evaluatedValue50 = [new NodeParameterDto("设备状态", "\"故障\"", DataSourceDto.Literal, true)];
            var request = CreateLibraryActionRequest(addSecondsResult49, packageRoot, [TestLibraryArtifactId], methodName: "按设备状态选择分支", parameters: evaluatedValue50, returnType: "System.String");
            var result = await supervisor.RunAsync(request, (workerEvent, _) =>
            {
                events.Add(workerEvent);
                return ValueTask.CompletedTask;
            });
            Assert.Equal(FlowRunStatusDto.Failed, result.Status);
            Assert.Equal(DeviceErrorCodes.Faulted, result.ErrorCode);
            var errored = Assert.Single(events, workerEvent => workerEvent.EventType == WorkerEventType.NodeErrored);
            using var payload = JsonDocument.Parse(errored.PayloadJson);
            var getPropertyResult51 = payload.RootElement.GetProperty("branch");
            var getStringResult52 = getPropertyResult51.GetString();
            Assert.Equal("Error", getStringResult52);
            var getPropertyResult53 = payload.RootElement.GetProperty("errorCode");
            var getStringResult54 = getPropertyResult53.GetString();
            Assert.Equal(DeviceErrorCodes.Faulted, getStringResult54);
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
            var addSecondsResult55 = DateTimeOffset.UtcNow.AddSeconds(15);
            IReadOnlyList<NodeParameterDto> evaluatedValue56 = [CreateVariadicParameter("param-1", "检测值", "2", "expanded"), CreateVariadicParameter("param-1-2", "检测值 2", "3", "expanded"), CreateVariadicParameter("param-1-3", "检测值 3", null, "expanded")];
            var request = CreateLibraryActionRequest(addSecondsResult55, packageRoot, [TestLibraryArtifactId], methodName: "汇总多个检测值", parameters: evaluatedValue56, returnType: "System.Int32");
            var result = await supervisor.RunAsync(request, (workerEvent, _) =>
            {
                events.Add(workerEvent);
                return ValueTask.CompletedTask;
            });
            Assert.Equal(FlowRunStatusDto.Succeeded, result.Status);
            var completed = Assert.Single(events, workerEvent => workerEvent.EventType == WorkerEventType.NodeCompleted);
            using var payload = JsonDocument.Parse(completed.PayloadJson);
            var getPropertyResult57 = payload.RootElement.GetProperty("outputs");
            var getPropertyResult58 = getPropertyResult57.GetProperty("result");
            var getInt32Result59 = getPropertyResult58.GetInt32();
            Assert.Equal(5, getInt32Result59);
            var getPropertyResult60 = payload.RootElement.GetProperty("inputs");
            var countResult = getPropertyResult60.EnumerateObject()
                .Count();
            Assert.Equal(2, countResult);
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
            var addSecondsResult62 = DateTimeOffset.UtcNow.AddSeconds(15);
            IReadOnlyList<NodeParameterDto> evaluatedValue63 = [CreateVariadicParameter("param-1", "检测值", "[2,3,5]", "collection")];
            var request = CreateLibraryActionRequest(addSecondsResult62, packageRoot, [TestLibraryArtifactId], methodName: "汇总多个检测值", parameters: evaluatedValue63, returnType: "System.Int32");
            var result = await supervisor.RunAsync(request, (workerEvent, _) =>
            {
                events.Add(workerEvent);
                return ValueTask.CompletedTask;
            });
            Assert.Equal(FlowRunStatusDto.Succeeded, result.Status);
            var completed = Assert.Single(events, workerEvent => workerEvent.EventType == WorkerEventType.NodeCompleted);
            using var payload = JsonDocument.Parse(completed.PayloadJson);
            var getPropertyResult64 = payload.RootElement.GetProperty("outputs");
            var getPropertyResult65 = getPropertyResult64.GetProperty("result");
            var getInt32Result66 = getPropertyResult65.GetInt32();
            Assert.Equal(10, getInt32Result66);
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
            var addSecondsResult67 = DateTimeOffset.UtcNow.AddSeconds(15);
            var request = CreateLibraryServiceRequest(addSecondsResult67, packageRoot, typeof(ConstructorInjectionNodes), nameof(ConstructorInjectionNodes.Describe));
            var result = await supervisor.RunAsync(request, (workerEvent, _) =>
            {
                events.Add(workerEvent);
                return ValueTask.CompletedTask;
            });
            Assert.Equal(FlowRunStatusDto.Succeeded, result.Status);
            var completed = Assert.Single(events, workerEvent => workerEvent.EventType == WorkerEventType.NodeCompleted);
            using var payload = JsonDocument.Parse(completed.PayloadJson);
            var evaluatedValue71 = resolveGetStringResult()!;
            var values = evaluatedValue71.Split(';');
            Assert.Equal(7, values.Length);
            Assert.Equal(values[0], values[1]);
            Assert.Equal("True", values[2]);
            Assert.Equal(values[3], values[4]);
            Assert.Equal("True", values[5]);
            Assert.Equal("False", values[6]);
            string? resolveGetStringResult()
            {
                var getPropertyResult68 = payload.RootElement.GetProperty("outputs");
                var getPropertyResult69 = getPropertyResult68.GetProperty("result");
                var getStringResult70 = getPropertyResult69.GetString();
                return getStringResult70;
            }
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
            var addSecondsResult72 = DateTimeOffset.UtcNow.AddSeconds(15);
            var request = CreateLibraryServiceRequest(addSecondsResult72, packageRoot, typeof(ForbiddenProviderNode), nameof(ForbiddenProviderNode.Execute));
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
            var addSecondsResult73 = DateTimeOffset.UtcNow.AddSeconds(15);
            var request = CreateTwoNodeLibraryServiceRequest(addSecondsResult73, packageRoot, typeof(ConstructorInjectionNodes), nameof(ConstructorInjectionNodes.DescribeRunAndInvocationScopes));
            var result = await supervisor.RunAsync(request, (workerEvent, _) =>
            {
                events.Add(workerEvent);
                return ValueTask.CompletedTask;
            });
            Assert.Equal(FlowRunStatusDto.Succeeded, result.Status);
            var getLibraryNodeResultResult = GetLibraryNodeResult(events, "library-service-first");
            var first = getLibraryNodeResultResult.Split(';');
            var getLibraryNodeResultResult74 = GetLibraryNodeResult(events, "library-service-second");
            var second = getLibraryNodeResultResult74.Split(';');
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
    public async Task SupervisorReadsAndWritesTheNodeVariableTableWhileDebugIsPaused()
    {
        var packageRoot = CreateTestLibraryPackageRoot();
        try
        {
            var events = new List<WorkerEventEnvelopeDto>();
            var firstPause = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondPause = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var supervisor = CreateSupervisor(allowedLibraryPackageRoot: packageRoot);
            var request = resolveCreateTwoNodeLibraryServiceRequestResult() with
            {
                Debug = resolveWorkerDebugOptionsDto()
            };
            await using var debug = await supervisor.StartDebugAsync(request, (workerEvent, _) =>
            {
                events.Add(workerEvent);
                if (workerEvent.EventType == WorkerEventType.DebugPaused)
                {
                    using var payload = JsonDocument.Parse(workerEvent.PayloadJson);
                    if (resolveGetStringResult75() == "library-service-first")
                    {
                        firstPause.TrySetResult();
                    }
                    else
                    {
                        secondPause.TrySetResult();
                    }

                    string? resolveGetStringResult75()
                    {
                        var getPropertyResult76 = payload.RootElement.GetProperty("nodeId");
                        var getStringResult77 = getPropertyResult76.GetString();
                        return getStringResult77;
                    }
                }

                return ValueTask.CompletedTask;
            });
            var fromSecondsResult79 = TimeSpan.FromSeconds(10);
            await firstPause.Task.WaitAsync(fromSecondsResult79);
            WorkerVariableRequestDto evaluatedValue80 = new(WorkerProtocol.Version, request.RunId, "list");
            var empty = await supervisor.AccessVariablesAsync(evaluatedValue80);
            Assert.Empty(empty.Snapshot!.Variables);
            var serializeToElementResult = JsonSerializer.SerializeToElement<object?>(null);
            WorkerVariableRequestDto evaluatedValue81 = new(WorkerProtocol.Version, request.RunId, "set", "note", serializeToElementResult, "null");
            var nullWrite = await supervisor.AccessVariablesAsync(evaluatedValue81);
            Assert.Equal(200, nullWrite.StatusCode);
            Assert.True(nullWrite.Variable!.ValueAvailable);
            Assert.Null(nullWrite.Variable.Value);
            var serializeToElementResult82 = JsonSerializer.SerializeToElement(42);
            WorkerVariableRequestDto evaluatedValue83 = new(WorkerProtocol.Version, request.RunId, "set", "test.variables.count", serializeToElementResult82, "int32");
            var write = await supervisor.AccessVariablesAsync(evaluatedValue83);
            Assert.Equal(200, write.StatusCode);
            await debug.ContinueAsync(1);
            var fromSecondsResult84 = TimeSpan.FromSeconds(10);
            await secondPause.Task.WaitAsync(fromSecondsResult84);
            WorkerVariableRequestDto evaluatedValue85 = new(WorkerProtocol.Version, request.RunId, "get", "test.variables.count");
            var read = await supervisor.AccessVariablesAsync(evaluatedValue85);
            var getInt32Result86 = read.Variable!.Value!.Value.GetInt32();
            Assert.Equal(43, getInt32Result86);
            Assert.Equal("System.Int32", read.Variable.ClrType);
            var getLibraryNodeResultResult87 = GetLibraryNodeResult(events, "library-service-first");
            Assert.Equal("43", getLibraryNodeResultResult87);
            await debug.ContinueAsync(2);
            var fromSecondsResult88 = TimeSpan.FromSeconds(10);
            var completed = await debug.Completion.WaitAsync(fromSecondsResult88);
            Assert.Equal(FlowRunStatusDto.Succeeded, completed.Status);
            Assert.True(completed.FinalVariables!.IsFinal);
            var evaluatedValue89 = Assert.Single(completed.FinalVariables.Variables, variable => variable.Name == "test.variables.count").Value!.Value;
            var getInt32Result90 = evaluatedValue89.GetInt32();
            Assert.Equal(44, getInt32Result90);
            var getLibraryNodeResultResult91 = GetLibraryNodeResult(events, "library-service-second");
            Assert.Equal("44", getLibraryNodeResultResult91);
            await debug.DisposeAsync();
            WorkerVariableRequestDto evaluatedValue92 = new(WorkerProtocol.Version, request.RunId, "list");
            var evaluatedValue93 = (await supervisor.AccessVariablesAsync(evaluatedValue92)).StatusCode;
            Assert.Equal(409, evaluatedValue93);
            WorkerRunRequestDto resolveCreateTwoNodeLibraryServiceRequestResult()
            {
                var addSecondsResult78 = DateTimeOffset.UtcNow.AddSeconds(20);
                var createTwoNodeLibraryServiceRequestResult = CreateTwoNodeLibraryServiceRequest(addSecondsResult78, packageRoot, typeof(VariableNodes), nameof(VariableNodes.ShareVariables));
                return createTwoNodeLibraryServiceRequestResult;
            }

            WorkerDebugOptionsDto resolveWorkerDebugOptionsDto()
            {
                var newGuidResult = Guid.NewGuid();
                var workerDebugOptionsDto = new WorkerDebugOptionsDto(newGuidResult, ["library-service-first", "library-service-second"]);
                return workerDebugOptionsDto;
            }
        }
        finally
        {
            TryDeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public async Task SupervisorSharesVariablesAcrossNodeContextsAndIsolatesRuns()
    {
        var packageRoot = CreateTestLibraryPackageRoot();
        try
        {
            var supervisor = CreateSupervisor(allowedLibraryPackageRoot: packageRoot);
            for (var run = 0; run < 2; run++)
            {
                var events = new List<WorkerEventEnvelopeDto>();
                var addSecondsResult94 = DateTimeOffset.UtcNow.AddSeconds(15);
                var request = CreateTwoNodeLibraryServiceRequest(addSecondsResult94, packageRoot, typeof(VariableNodes), nameof(VariableNodes.ShareVariables));
                var result = await supervisor.RunAsync(request, (workerEvent, _) =>
                {
                    events.Add(workerEvent);
                    return ValueTask.CompletedTask;
                });
                Assert.Equal(FlowRunStatusDto.Succeeded, result.Status);
                var getLibraryNodeResultResult95 = GetLibraryNodeResult(events, "library-service-first");
                Assert.Equal("1", getLibraryNodeResultResult95);
                var getLibraryNodeResultResult96 = GetLibraryNodeResult(events, "library-service-second");
                Assert.Equal("2", getLibraryNodeResultResult96);
                Assert.True(result.FinalVariables!.IsFinal);
                var evaluatedValue97 = Assert.Single(result.FinalVariables.Variables, variable => variable.Name == "test.variables.count").Value!.Value;
                var getInt32Result98 = evaluatedValue97.GetInt32();
                Assert.Equal(2, getInt32Result98);
            }
        }
        finally
        {
            TryDeleteDirectory(packageRoot);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SupervisorSharesVariablesBetweenScriptsLibrariesAndExternalDebugWrites(bool debugRun)
    {
        var packageRoot = CreateTestLibraryPackageRoot();
        try
        {
            var events = new List<WorkerEventEnvelopeDto>();
            var supervisor = CreateSupervisor(allowedLibraryPackageRoot: packageRoot);
            var first = CreateVariableScriptNode("write", """
                import { variables } from "sereinflow"
                variables.set("test.variables.count", 41)
                return variables.get("test.variables.count")
                """);
            var library = CreateLibraryServiceNode("library", "Increment shared variable", typeof(VariableNodes), nameof(VariableNodes.ShareVariables));
            var last = CreateVariableScriptNode("read", """
                import { variables } from "sereinflow"
                variables.set("script.observed", variables.get("test.variables.count"))
                return variables.get("script.observed")
                """);
            var newGuidResult109 = Guid.NewGuid();
            IReadOnlyList<CanvasDto> evaluatedValue111 = [resolveCanvasDto()];
            var definition = new FlowDefinitionDto(newGuidResult109, 5, 1, evaluatedValue111, first.Id, "test");
            var newGuidResult112 = Guid.NewGuid();
            var serializeResult = JsonSerializer.Serialize(definition);
            var addSecondsResult113 = DateTimeOffset.UtcNow.AddSeconds(20);
            var request = new WorkerRunRequestDto(WorkerProtocol.Version, newGuidResult112, definition.Id, 1, serializeResult, addSecondsResult113, LibraryPackageRootPath: packageRoot, AllowedLibraryIds: [TestLibraryArtifactId]);
            WorkerRunResultDto result;
            if (debugRun)
            {
                request = request with
                {
                    Debug = resolveWorkerDebugOptionsDto99()
                };
                var pause = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                await using var debug = await supervisor.StartDebugAsync(request, (workerEvent, _) =>
                {
                    events.Add(workerEvent);
                    if (workerEvent.EventType == WorkerEventType.DebugPaused)
                    {
                        pause.TrySetResult();
                    }

                    return ValueTask.CompletedTask;
                });
                var fromSecondsResult102 = TimeSpan.FromSeconds(10);
                await pause.Task.WaitAsync(fromSecondsResult102);
                WorkerVariableRequestDto evaluatedValue103 = new(WorkerProtocol.Version, request.RunId, "get", "test.variables.count");
                var read = await supervisor.AccessVariablesAsync(evaluatedValue103);
                var getInt32Result104 = read.Variable!.Value!.Value.GetInt32();
                Assert.Equal(42, getInt32Result104);
                var serializeToElementResult105 = JsonSerializer.SerializeToElement(60);
                WorkerVariableRequestDto evaluatedValue106 = new(WorkerProtocol.Version, request.RunId, "set", "test.variables.count", serializeToElementResult105, "int32");
                var write = await supervisor.AccessVariablesAsync(evaluatedValue106);
                Assert.Equal(200, write.StatusCode);
                await debug.ContinueAsync(1);
                var fromSecondsResult107 = TimeSpan.FromSeconds(10);
                result = await debug.Completion.WaitAsync(fromSecondsResult107);
                WorkerDebugOptionsDto resolveWorkerDebugOptionsDto99()
                {
                    var newGuidResult100 = Guid.NewGuid();
                    var workerDebugOptionsDto101 = new WorkerDebugOptionsDto(newGuidResult100, [last.Id]);
                    return workerDebugOptionsDto101;
                }
            }
            else
            {
                result = await supervisor.RunAsync(request, (workerEvent, _) =>
                {
                    events.Add(workerEvent);
                    return ValueTask.CompletedTask;
                });
            }

            Assert.Equal(FlowRunStatusDto.Succeeded, result.Status);
            var getLibraryNodeResultResult114 = GetLibraryNodeResult(events, library.Id);
            Assert.Equal("42", getLibraryNodeResultResult114);
            var expected = debugRun ? 60 : 42;
            var completed = Assert.Single(events, item => item.EventType == WorkerEventType.NodeCompleted && item.NodeId == last.Id);
            using var payload = JsonDocument.Parse(completed.PayloadJson);
            var getPropertyResult115 = payload.RootElement.GetProperty("outputs");
            var getPropertyResult116 = getPropertyResult115.GetProperty("result");
            var getInt32Result117 = getPropertyResult116.GetInt32();
            Assert.Equal(expected, getInt32Result117);
            Assert.True(result.FinalVariables!.IsFinal);
            foreach (var name in new[]
            {
                "test.variables.count",
                "script.observed"
            }

            )
            {
                var variable = Assert.Single(result.FinalVariables.Variables, item => item.Name == name);
                Assert.Equal("System.Int32", variable.ClrType);
                var getInt32Result108 = variable.Value!.Value.GetInt32();
                Assert.Equal(expected, getInt32Result108);
            }

            CanvasDto resolveCanvasDto()
            {
                IReadOnlyList<ConnectionDto> evaluatedValue110 = [new ConnectionDto("write-library", first.Id, "success", library.Id, "execute", ConnectionKindDto.Execution, ExecutionBranchDto.Success, null, 0), new ConnectionDto("library-read", library.Id, "success", last.Id, "execute", ConnectionKindDto.Execution, ExecutionBranchDto.Success, null, 0)];
                var canvasDto = new CanvasDto("main", CanvasLifecycleDto.Main, [first, library, last], evaluatedValue110);
                return canvasDto;
            }
        }
        finally
        {
            TryDeleteDirectory(packageRoot);
        }
    }

    private static NodeDto CreateVariableScriptNode(string id, string source)
    {
        var computeSourceHashResult = SereinFlow.Domain.ScriptNodeDefinition.ComputeSourceHash(source);
        IReadOnlyList<ScriptValueContractDto> evaluatedValue118 = [new ScriptValueContractDto("result", "System.Int32", false)];
        var scriptNodeDataDto = new ScriptNodeDataDto(id, source, "1", computeSourceHashResult, [], evaluatedValue118);
        NodeDto evaluatedValue119 = new(id, NodeTypeDto.Script, id, 0, 0, [], [], scriptNodeDataDto);
        return evaluatedValue119;
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
            var addSecondsResult120 = DateTimeOffset.UtcNow.AddSeconds(15);
            var request = CreateLibraryExternalMessageRequest(addSecondsResult120, packageRoot);
            var runTask = supervisor.RunAsync(request, (workerEvent, _) =>
            {
                if (workerEvent.EventType == WorkerEventType.NodeStarted)
                {
                    started.TrySetResult();
                }

                if (workerEvent.EventType == WorkerEventType.NodeCompleted)
                {
                    completed.TrySetResult();
                }

                return ValueTask.CompletedTask;
            }, cancellation.Token);
            var fromSecondsResult121 = TimeSpan.FromSeconds(10);
            await started.Task.WaitAsync(fromSecondsResult121);
            var messageId = Guid.NewGuid();
            var serializeResult122 = JsonSerializer.Serialize("remote hello");
            var delivery = new WorkerMessageDeliveryDto(WorkerProtocol.Version, request.RunId, messageId, "test.message.inbox", WorkerMessageChannelKindDto.Queue, WorkerMessageSerializationModeDto.Json, "test.text", serializeResult122, DateTimeOffset.UtcNow);
            WorkerMessageDeliveryResponseDto? response = null;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                response = await supervisor.DeliverMessageAsync(delivery);
                if (response.Status != WorkerMessageDeliveryStatusDto.NotReady)
                {
                    break;
                }

                await Task.Delay(50);
            }

            Assert.NotNull(response);
            Assert.Equal(WorkerMessageDeliveryStatusDto.Accepted, response!.Status);
            var fromSecondsResult123 = TimeSpan.FromSeconds(10);
            await completed.Task.WaitAsync(fromSecondsResult123);
            cancellation.Cancel();
            var fromSecondsResult124 = TimeSpan.FromSeconds(10);
            var result = await runTask.WaitAsync(fromSecondsResult124);
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
    public async Task SupervisorDeliveryWakesFlipflopAndExecutesSuccessor(WorkerMessageChannelKindDto channelKind, string topic, string flipflopMethodName)
    {
        var packageRoot = CreateTestLibraryPackageRoot();
        using var cancellation = new CancellationTokenSource();
        try
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var successorCompleted = new TaskCompletionSource<WorkerEventEnvelopeDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            var successorCompletionCount = 0;
            var supervisor = CreateSupervisor(allowedLibraryPackageRoot: packageRoot);
            var addSecondsResult125 = DateTimeOffset.UtcNow.AddSeconds(15);
            var request = CreateLibraryExternalMessageChainRequest(addSecondsResult125, packageRoot, channelKind, topic, flipflopMethodName);
            var runTask = supervisor.RunAsync(request, (workerEvent, _) =>
            {
                if (workerEvent.EventType == WorkerEventType.NodeStarted && workerEvent.NodeId == "message-flipflop")
                {
                    started.TrySetResult();
                }

                if (workerEvent.EventType == WorkerEventType.NodeCompleted && workerEvent.NodeId == "message-action")
                {
                    Interlocked.Increment(ref successorCompletionCount);
                    successorCompleted.TrySetResult(workerEvent);
                }

                return ValueTask.CompletedTask;
            }, cancellation.Token);
            var fromSecondsResult126 = TimeSpan.FromSeconds(10);
            await started.Task.WaitAsync(fromSecondsResult126);
            var newGuidResult127 = Guid.NewGuid();
            var serializeResult128 = JsonSerializer.Serialize("remote hello");
            var delivery = new WorkerMessageDeliveryDto(WorkerProtocol.Version, request.RunId, newGuidResult127, topic, channelKind, WorkerMessageSerializationModeDto.Json, "test.text", serializeResult128, DateTimeOffset.UtcNow);
            WorkerMessageDeliveryResponseDto? response = null;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                response = await supervisor.DeliverMessageAsync(delivery);
                if (response.Status != WorkerMessageDeliveryStatusDto.NotReady)
                {
                    break;
                }

                await Task.Delay(50);
            }

            Assert.NotNull(response);
            Assert.Equal(WorkerMessageDeliveryStatusDto.Accepted, response!.Status);
            var duplicate = await supervisor.DeliverMessageAsync(delivery);
            Assert.Equal(WorkerMessageDeliveryStatusDto.Accepted, duplicate.Status);
            Assert.True(duplicate.Duplicate);
            var fromSecondsResult129 = TimeSpan.FromSeconds(10);
            var completed = await successorCompleted.Task.WaitAsync(fromSecondsResult129);
            using var payload = JsonDocument.Parse(completed.PayloadJson);
            var getPropertyResult130 = payload.RootElement.GetProperty("outputs");
            var getPropertyResult131 = getPropertyResult130.GetProperty("result");
            var getStringResult132 = getPropertyResult131.GetString();
            Assert.Equal("processed:remote hello", getStringResult132);
            cancellation.Cancel();
            var fromSecondsResult133 = TimeSpan.FromSeconds(10);
            var result = await runTask.WaitAsync(fromSecondsResult133);
            Assert.Equal(FlowRunStatusDto.Cancelled, result.Status);
            var readResult = Volatile.Read(ref successorCompletionCount);
            Assert.Equal(1, readResult);
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
            var request = resolveCreateLibraryExternalMessageChainRequestResult() with
            {
                Debug = resolveWorkerDebugOptionsDto135()
            };
            await using var debug = await supervisor.StartDebugAsync(request, (workerEvent, _) =>
            {
                if (workerEvent.EventType == WorkerEventType.NodeCompleted && workerEvent.NodeId == "message-action")
                {
                    successorCompleted.TrySetResult(workerEvent);
                }

                return ValueTask.CompletedTask;
            });
            var messageId = Guid.NewGuid();
            var serializeResult138 = JsonSerializer.Serialize("debug hello");
            var delivery = new WorkerMessageDeliveryDto(WorkerProtocol.Version, request.RunId, messageId, "test.message.inbox", WorkerMessageChannelKindDto.Queue, WorkerMessageSerializationModeDto.Json, "test.text", serializeResult138, DateTimeOffset.UtcNow);
            WorkerMessageDeliveryResponseDto? response = null;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                response = await supervisor.DeliverMessageAsync(delivery);
                if (response.Status != WorkerMessageDeliveryStatusDto.NotReady)
                {
                    break;
                }

                await Task.Delay(50);
            }

            Assert.NotNull(response);
            Assert.Equal(WorkerMessageDeliveryStatusDto.Accepted, response!.Status);
            var fromSecondsResult139 = TimeSpan.FromSeconds(10);
            var completed = await successorCompleted.Task.WaitAsync(fromSecondsResult139);
            using (var payload = JsonDocument.Parse(completed.PayloadJson))
            {
                var getPropertyResult3 = payload.RootElement.GetProperty("outputs");
                var getPropertyResult7 = getPropertyResult3.GetProperty("result");
                var getStringResult8 = getPropertyResult7.GetString();
                Assert.Equal("processed:debug hello", getStringResult8);
            }

            await debug.StopAsync(1);
            var fromSecondsResult140 = TimeSpan.FromSeconds(10);
            var result = await debug.Completion.WaitAsync(fromSecondsResult140);
            Assert.Equal(FlowRunStatusDto.Cancelled, result.Status);
            WorkerRunRequestDto resolveCreateLibraryExternalMessageChainRequestResult()
            {
                var addSecondsResult134 = DateTimeOffset.UtcNow.AddSeconds(15);
                var createLibraryExternalMessageChainRequestResult = CreateLibraryExternalMessageChainRequest(addSecondsResult134, packageRoot, WorkerMessageChannelKindDto.Queue, "test.message.inbox", nameof(MessageNodes.ReceiveExternal));
                return createLibraryExternalMessageChainRequestResult;
            }

            WorkerDebugOptionsDto resolveWorkerDebugOptionsDto135()
            {
                var newGuidResult136 = Guid.NewGuid();
                var workerDebugOptionsDto137 = new WorkerDebugOptionsDto(newGuidResult136, []);
                return workerDebugOptionsDto137;
            }
        }
        finally
        {
            TryDeleteDirectory(packageRoot);
        }
    }

    private static WorkerSupervisor CreateSupervisor(Action<string>? diagnosticLogger = null, string? allowedLibraryPackageRoot = null, string? allowedWorkpieceRoot = null)
    {
        var fromSecondsResult141 = TimeSpan.FromSeconds(10);
        var fromMillisecondsResult142 = TimeSpan.FromMilliseconds(100);
        var fromSecondsResult143 = TimeSpan.FromSeconds(2);
        var runnerLaunchOptions144 = new RunnerLaunchOptions("dotnet", [typeof(RunnerHost).Assembly.Location], HandshakeTimeout: fromSecondsResult141, HeartbeatInterval: fromMillisecondsResult142, CancellationGracePeriod: fromSecondsResult143, AllowedLibraryPackageRoot: allowedLibraryPackageRoot, DiagnosticLogger: diagnosticLogger, AllowedWorkpieceRoot: allowedWorkpieceRoot);
        WorkerSupervisor evaluatedValue145 = new(runnerLaunchOptions144);
        return evaluatedValue145;
    }

    private static WorkerRunRequestDto CreateActionRequest(DateTimeOffset deadline)
    {
        var flowId = Guid.NewGuid();
        var action = new NodeDto("action", NodeTypeDto.Action, "Action", 0, 0, [], [], null);
        var createRequestResult = CreateRequest(flowId, action, deadline);
        return createRequestResult;
    }

    private static WorkerRunRequestDto CreateTwoNodeDebugRequest(DateTimeOffset deadline)
    {
        const string firstSource = "return 1";
        const string secondSource = "return 2";
        var flowId = Guid.NewGuid();
        var computeSourceHashResult146 = SereinFlow.Domain.ScriptNodeDefinition.ComputeSourceHash(firstSource);
        var scriptNodeDataDto147 = new ScriptNodeDataDto("first", firstSource, "1", computeSourceHashResult146, [], []);
        var first = new NodeDto("first", NodeTypeDto.Script, "First", 0, 0, [], [], scriptNodeDataDto147);
        var computeSourceHashResult148 = SereinFlow.Domain.ScriptNodeDefinition.ComputeSourceHash(secondSource);
        var scriptNodeDataDto149 = new ScriptNodeDataDto("second", secondSource, "1", computeSourceHashResult148, [], []);
        var second = new NodeDto("second", NodeTypeDto.Script, "Second", 0, 0, [], [], scriptNodeDataDto149);
        var connection = new ConnectionDto("first:success->second:execute", first.Id, "success", second.Id, "execute", ConnectionKindDto.Execution, ExecutionBranchDto.Success, null, 0);
        IReadOnlyList<CanvasDto> evaluatedValue150 = [new CanvasDto("main", CanvasLifecycleDto.Main, [first, second], [connection])];
        var flowRunPolicyDto = new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel);
        var definition = new FlowDefinitionDto(flowId, 5, 1, evaluatedValue150, first.Id, "test", RunPolicy: flowRunPolicyDto);
        var newGuidResult151 = Guid.NewGuid();
        var serializeResult152 = JsonSerializer.Serialize(definition);
        var newGuidResult153 = Guid.NewGuid();
        var workerDebugOptionsDto154 = new WorkerDebugOptionsDto(newGuidResult153, [first.Id]);
        var workerRunRequestDto = new WorkerRunRequestDto(WorkerProtocol.Version, newGuidResult151, flowId, 1, serializeResult152, deadline, Debug: workerDebugOptionsDto154);
        return workerRunRequestDto;
    }

    private static WorkerRunRequestDto CreateScriptRequest(DateTimeOffset deadline)
    {
        var flowId = Guid.NewGuid();
        var computeSourceHashResult155 = SereinFlow.Domain.ScriptNodeDefinition.ComputeSourceHash("for i in range(0, 1000000000) { var keep = i }");
        var script = new ScriptNodeDataDto("script", "for i in range(0, 1000000000) { var keep = i }", "1", computeSourceHashResult155, [], []);
        var node = new NodeDto("script", NodeTypeDto.Script, "Script", 0, 0, [], [], script);
        var createRequestResult156 = CreateRequest(flowId, node, deadline);
        return createRequestResult156;
    }

    private static WorkerRunRequestDto CreateScriptLogRequest(DateTimeOffset deadline)
    {
        const string source = """
            import { log, env } from "sereinflow"
            log.info("Started by script")
            return env.nodeId
            """;
        var flowId = Guid.NewGuid();
        var computeSourceHashResult157 = SereinFlow.Domain.ScriptNodeDefinition.ComputeSourceHash(source);
        var script = new ScriptNodeDataDto("script", source, "1", computeSourceHashResult157, [], []);
        var node = new NodeDto("script", NodeTypeDto.Script, "Script", 0, 0, [], [], script);
        var createRequestResult158 = CreateRequest(flowId, node, deadline);
        return createRequestResult158;
    }

    private static WorkerRunRequestDto CreateLibraryServiceRequest(DateTimeOffset deadline, string packageRoot, Type nodeType, string methodName)
    {
        var flowId = Guid.NewGuid();
        var getFileNameResult = Path.GetFileName(nodeType.Assembly.Location);
        var nodeUiMetadataDto = new NodeUiMetadataDto("library-service-action", "node.libraryService.title", "node.libraryService.subtitle", null, "ready", true, null, Category: "library", LibraryId: TestLibraryArtifactId, ClassName: nodeType.FullName, MethodName: methodName, DllName: getFileNameResult, DllVersion: "1.0.0", ReturnType: "System.String");
        var action = new NodeDto("library-service-action", NodeTypeDto.Action, "Library service action", 0, 0, [], [], null, nodeUiMetadataDto);
        IReadOnlyList<CanvasDto> evaluatedValue159 = [new CanvasDto("main", CanvasLifecycleDto.Main, [action], [])];
        var flowRunPolicyDto160 = new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel);
        var definition = new FlowDefinitionDto(flowId, 5, 1, evaluatedValue159, action.Id, "test", RunPolicy: flowRunPolicyDto160);
        var newGuidResult161 = Guid.NewGuid();
        var serializeResult162 = JsonSerializer.Serialize(definition);
        var workerRunRequestDto163 = new WorkerRunRequestDto(WorkerProtocol.Version, newGuidResult161, flowId, 1, serializeResult162, deadline, LibraryPackageRootPath: packageRoot, AllowedLibraryIds: [TestLibraryArtifactId]);
        return workerRunRequestDto163;
    }

    private static WorkerRunRequestDto CreateTwoNodeLibraryServiceRequest(DateTimeOffset deadline, string packageRoot, Type nodeType, string methodName)
    {
        var flowId = Guid.NewGuid();
        var first = CreateLibraryServiceNode("library-service-first", "First library service action", nodeType, methodName);
        var second = CreateLibraryServiceNode("library-service-second", "Second library service action", nodeType, methodName);
        var connection = new ConnectionDto("library-service-first:success->library-service-second:execute", first.Id, "success", second.Id, "execute", ConnectionKindDto.Execution, ExecutionBranchDto.Success, null, 0);
        IReadOnlyList<CanvasDto> evaluatedValue164 = [new CanvasDto("main", CanvasLifecycleDto.Main, [first, second], [connection])];
        var flowRunPolicyDto165 = new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel);
        var definition = new FlowDefinitionDto(flowId, 5, 1, evaluatedValue164, first.Id, "test", RunPolicy: flowRunPolicyDto165);
        var newGuidResult166 = Guid.NewGuid();
        var serializeResult167 = JsonSerializer.Serialize(definition);
        var workerRunRequestDto168 = new WorkerRunRequestDto(WorkerProtocol.Version, newGuidResult166, flowId, 1, serializeResult167, deadline, LibraryPackageRootPath: packageRoot, AllowedLibraryIds: [TestLibraryArtifactId]);
        return workerRunRequestDto168;
    }

    private static NodeDto CreateLibraryServiceNode(string id, string name, Type nodeType, string methodName)
    {
        var getFileNameResult169 = Path.GetFileName(nodeType.Assembly.Location);
        var nodeUiMetadataDto170 = new NodeUiMetadataDto(id, "node.libraryService.title", "node.libraryService.subtitle", null, "ready", true, null, Category: "library", LibraryId: TestLibraryArtifactId, ClassName: nodeType.FullName, MethodName: methodName, DllName: getFileNameResult169, DllVersion: "1.0.0", ReturnType: "System.String");
        NodeDto evaluatedValue171 = new(id, NodeTypeDto.Action, name, 0, 0, [], [], null, nodeUiMetadataDto170);
        return evaluatedValue171;
    }

    private static string GetLibraryNodeResult(IReadOnlyCollection<WorkerEventEnvelopeDto> events, string nodeId)
    {
        var completed = Assert.Single(events, workerEvent => workerEvent.EventType == WorkerEventType.NodeCompleted && workerEvent.NodeId == nodeId);
        using var payload = JsonDocument.Parse(completed.PayloadJson);
        var evaluatedValue176 = resolveGetStringResult172()!;
        return evaluatedValue176;
        string? resolveGetStringResult172()
        {
            var getPropertyResult173 = payload.RootElement.GetProperty("outputs");
            var getPropertyResult174 = getPropertyResult173.GetProperty("result");
            var getStringResult175 = getPropertyResult174.GetString();
            return getStringResult175;
        }
    }

    private static WorkerRunRequestDto CreateLibraryActionRequest(DateTimeOffset deadline, string packageRoot, IReadOnlyList<string> allowedLibraryIds, string methodName = "计算合格率", IReadOnlyList<NodeParameterDto>? parameters = null, string returnType = "System.Decimal", string? workpieceRoot = null)
    {
        var flowId = Guid.NewGuid();
        var evaluatedValue177 = parameters ?? [new NodeParameterDto("合格数量", "8", DataSourceDto.Literal, true), new NodeParameterDto("检测总数", "16", DataSourceDto.Literal, true)];
        var getFileNameResult178 = Path.GetFileName(typeof(生产线节点).Assembly.Location);
        var nodeUiMetadataDto179 = new NodeUiMetadataDto("library-action", "node.libraryAction.title", "node.libraryAction.subtitle", null, "ready", true, null, Category: "library", LibraryId: TestLibraryArtifactId, ClassName: typeof(生产线节点).FullName, MethodName: methodName, DllName: getFileNameResult178, DllVersion: "1.0.0", ReturnType: returnType);
        var action = new NodeDto("library-action", NodeTypeDto.Action, "Calculate quality rate", 0, 0, [], evaluatedValue177, null, nodeUiMetadataDto179);
        IReadOnlyList<CanvasDto> evaluatedValue180 = [new CanvasDto("main", CanvasLifecycleDto.Main, [action], [])];
        var flowRunPolicyDto181 = new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel);
        var definition = new FlowDefinitionDto(flowId, 5, 1, evaluatedValue180, action.Id, "test", RunPolicy: flowRunPolicyDto181);
        var newGuidResult182 = Guid.NewGuid();
        var serializeResult183 = JsonSerializer.Serialize(definition);
        var workerRunRequestDto184 = new WorkerRunRequestDto(WorkerProtocol.Version, newGuidResult182, flowId, 1, serializeResult183, deadline, LibraryPackageRootPath: packageRoot, AllowedLibraryIds: allowedLibraryIds, WorkpieceRootPath: workpieceRoot);
        return workerRunRequestDto184;
    }

    private static WorkerRunRequestDto CreateLibraryGlobalFlipflopDebugRequest(DateTimeOffset deadline, string packageRoot, IReadOnlyList<string> breakpointNodeIds, int intervalMilliseconds)
    {
        var flowId = Guid.NewGuid();
        IReadOnlyList<NodeParameterDto> evaluatedValue185 = [new NodeParameterDto("设备编号", "\"debug-device\"", DataSourceDto.Literal, true), resolveNodeParameterDto()];
        var getFileNameResult186 = Path.GetFileName(typeof(生产线节点).Assembly.Location);
        var nodeUiMetadataDto187 = new NodeUiMetadataDto("library-flipflop", "node.libraryFlipflop.title", "node.libraryFlipflop.subtitle", null, "ready", true, null, Category: "library", LibraryId: TestLibraryArtifactId, ClassName: typeof(生产线节点).FullName, MethodName: "等待设备触发", DllName: getFileNameResult186, DllVersion: "1.0.0", ReturnType: "System.Boolean", IsAwaitable: true);
        var flipflop = new NodeDto("library-flipflop", NodeTypeDto.Flipflop, "Wait for device trigger", 0, 0, [], evaluatedValue185, null, nodeUiMetadataDto187);
        IReadOnlyList<CanvasDto> evaluatedValue188 = [new CanvasDto("main", CanvasLifecycleDto.Main, [flipflop], [])];
        var flowRunPolicyDto189 = new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel);
        var definition = new FlowDefinitionDto(flowId, 5, 1, evaluatedValue188, string.Empty, "test", RunPolicy: flowRunPolicyDto189);
        var newGuidResult190 = Guid.NewGuid();
        var serializeResult191 = JsonSerializer.Serialize(definition);
        var newGuidResult192 = Guid.NewGuid();
        var workerDebugOptionsDto193 = new WorkerDebugOptionsDto(newGuidResult192, breakpointNodeIds);
        var workerRunRequestDto194 = new WorkerRunRequestDto(WorkerProtocol.Version, newGuidResult190, flowId, 1, serializeResult191, deadline, LibraryPackageRootPath: packageRoot, AllowedLibraryIds: [TestLibraryArtifactId], Debug: workerDebugOptionsDto193);
        return workerRunRequestDto194;
        NodeParameterDto resolveNodeParameterDto()
        {
            var toStringResult = intervalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var nodeParameterDto = new NodeParameterDto("轮询间隔毫秒", toStringResult, DataSourceDto.Literal, false);
            return nodeParameterDto;
        }
    }

    private static WorkerRunRequestDto CreateLibraryExternalMessageRequest(DateTimeOffset deadline, string packageRoot)
    {
        var flowId = Guid.NewGuid();
        var getFileNameResult195 = Path.GetFileName(typeof(MessageNodes).Assembly.Location);
        var nodeUiMetadataDto196 = new NodeUiMetadataDto("message-flipflop", NodeErrorCodes.MessageTitle, NodeErrorCodes.MessageSubtitle, null, "ready", true, null, Category: "library", LibraryId: TestLibraryArtifactId, ClassName: typeof(MessageNodes).FullName, MethodName: nameof(MessageNodes.ReceiveExternal), DllName: getFileNameResult195, DllVersion: "1.0.0", ReturnType: "System.String", IsAwaitable: true);
        var flipflop = new NodeDto("message-flipflop", NodeTypeDto.Flipflop, "External message listener", 0, 0, [], [], null, nodeUiMetadataDto196);
        IReadOnlyList<CanvasDto> evaluatedValue197 = [new CanvasDto("main", CanvasLifecycleDto.Main, [flipflop], [])];
        var flowRunPolicyDto198 = new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel);
        var definition = new FlowDefinitionDto(flowId, 5, 1, evaluatedValue197, string.Empty, "test", RunPolicy: flowRunPolicyDto198);
        var newGuidResult199 = Guid.NewGuid();
        var serializeResult200 = JsonSerializer.Serialize(definition);
        var workerRunRequestDto201 = new WorkerRunRequestDto(WorkerProtocol.Version, newGuidResult199, flowId, 1, serializeResult200, deadline, LibraryPackageRootPath: packageRoot, AllowedLibraryIds: [TestLibraryArtifactId]);
        return workerRunRequestDto201;
    }

    private static WorkerRunRequestDto CreateLibraryExternalMessageChainRequest(DateTimeOffset deadline, string packageRoot, WorkerMessageChannelKindDto channelKind, string topic, string flipflopMethodName)
    {
        var flowId = Guid.NewGuid();
        var getFileNameResult202 = Path.GetFileName(typeof(MessageNodes).Assembly.Location);
        var nodeUiMetadataDto203 = new NodeUiMetadataDto("message-flipflop", NodeErrorCodes.MessageTitle, NodeErrorCodes.MessageSubtitle, null, "ready", true, null, Category: "library", LibraryId: TestLibraryArtifactId, ClassName: typeof(MessageNodes).FullName, MethodName: flipflopMethodName, DllName: getFileNameResult202, DllVersion: "1.0.0", ReturnType: "System.String", IsAwaitable: true);
        var flipflop = new NodeDto("message-flipflop", NodeTypeDto.Flipflop, "External message listener", 0, 0, [], [], null, nodeUiMetadataDto203);
        IReadOnlyList<NodeParameterDto> evaluatedValue204 = [new NodeParameterDto("message", null, DataSourceDto.PreviousNode, true)];
        var getFileNameResult205 = Path.GetFileName(typeof(MessageNodes).Assembly.Location);
        var nodeUiMetadataDto206 = new NodeUiMetadataDto("message-action", NodeErrorCodes.MessageActionTitle, NodeErrorCodes.MessageActionSubtitle, null, "ready", true, null, Category: "library", LibraryId: TestLibraryArtifactId, ClassName: typeof(MessageNodes).FullName, MethodName: nameof(MessageNodes.ProcessExternal), DllName: getFileNameResult205, DllVersion: "1.0.0", ReturnType: "System.String");
        var action = new NodeDto("message-action", NodeTypeDto.Action, "Process external message", 0, 0, [], evaluatedValue204, null, nodeUiMetadataDto206);
        var execution = new ConnectionDto("message-flipflop:success->message-action:execute", flipflop.Id, "success", action.Id, "execute", ConnectionKindDto.Execution, ExecutionBranchDto.Success, null, 0);
        var data = new ConnectionDto("message-flipflop:data-out->message-action:message", flipflop.Id, "data-out", action.Id, "message", ConnectionKindDto.Data, null, DataSourceDto.PreviousNode, 0);
        IReadOnlyList<CanvasDto> evaluatedValue207 = [new CanvasDto("main", CanvasLifecycleDto.Main, [flipflop, action], [execution, data])];
        var flowRunPolicyDto208 = new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel);
        var definition = new FlowDefinitionDto(flowId, 5, 1, evaluatedValue207, string.Empty, "test", RunPolicy: flowRunPolicyDto208);
        var newGuidResult209 = Guid.NewGuid();
        var serializeResult210 = JsonSerializer.Serialize(definition);
        var workerRunRequestDto211 = new WorkerRunRequestDto(WorkerProtocol.Version, newGuidResult209, flowId, 1, serializeResult210, deadline, LibraryPackageRootPath: packageRoot, AllowedLibraryIds: [TestLibraryArtifactId]);
        return workerRunRequestDto211;
    }

    private static NodeParameterDto CreateVariadicParameter(string id, string name, string? valueJson, string mode)
    {
        var nodeParameterUiMetadataDto = new NodeParameterUiMetadataDto(id, name, "System.Int32", valueJson, null, null, null, null, Type: "System.Int32", IsVariadic: true, VariadicGroupId: "param-1", ElementType: "System.Int32", VariadicMode: mode);
        NodeParameterDto evaluatedValue212 = new(name, valueJson, DataSourceDto.Literal, false, nodeParameterUiMetadataDto);
        return evaluatedValue212;
    }

    private static WorkerRunRequestDto CreateRequest(Guid flowId, NodeDto node, DateTimeOffset deadline)
    {
        IReadOnlyList<CanvasDto> evaluatedValue213 = [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [])];
        var flowRunPolicyDto214 = new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel);
        var definition = new FlowDefinitionDto(flowId, 5, 1, evaluatedValue213, node.Id, "test", RunPolicy: flowRunPolicyDto214);
        var newGuidResult215 = Guid.NewGuid();
        var serializeResult216 = JsonSerializer.Serialize(definition);
        var workerRunRequestDto217 = new WorkerRunRequestDto(WorkerProtocol.Version, newGuidResult215, flowId, 1, serializeResult216, deadline);
        return workerRunRequestDto217;
    }

    private static string CreateTestLibraryPackageRoot()
    {
        var getTempPathResult220 = Path.GetTempPath();
        var evaluatedValue221 = $"sereinflow-worker-library-{Guid.NewGuid():N}";
        var packageRoot = Path.Combine(getTempPathResult220, evaluatedValue221);
        var packageDirectory = Path.Combine(packageRoot, "packages");
        Directory.CreateDirectory(packageDirectory);
        var packagePath = Path.Combine(packageDirectory, $"{TestLibraryArtifactId}.zip");
        var assemblyPath = typeof(生产线节点).Assembly.Location;
        var assemblyDirectory = Path.GetDirectoryName(assemblyPath)!;
        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
        foreach (var dependencyPath in Directory.EnumerateFiles(assemblyDirectory, "SereinFlow*.dll"))
        {
            var evaluatedValue218 = $"library/{Path.GetFileName(dependencyPath)}";
            archive.CreateEntryFromFile(dependencyPath, evaluatedValue218, CompressionLevel.NoCompression);
        }

        var dependenciesFile = Path.ChangeExtension(assemblyPath, ".deps.json");
        if (File.Exists(dependenciesFile))
        {
            var evaluatedValue219 = $"library/{Path.GetFileName(dependenciesFile)}";
            archive.CreateEntryFromFile(dependenciesFile, evaluatedValue219, CompressionLevel.NoCompression);
        }

        return packageRoot;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}

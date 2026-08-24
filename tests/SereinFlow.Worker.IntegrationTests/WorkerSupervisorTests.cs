using System.Text.Json;
using SereinFlow.Contracts;
using SereinFlow.Worker.Runner;
using SereinFlow.Worker.Supervisor;

namespace SereinFlow.Worker.IntegrationTests;

public sealed class WorkerSupervisorTests
{
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
    public async Task SupervisorPropagatesCancellationToScriptRunner()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var supervisor = CreateSupervisor();
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

        Assert.Equal(FlowRunStatusDto.Cancelled, result.Status);
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
        Assert.Equal("worker.crashed", result.ErrorCode);
    }

    private static WorkerSupervisor CreateSupervisor()
        => new(new RunnerLaunchOptions(
            "dotnet",
            [typeof(RunnerHost).Assembly.Location],
            HandshakeTimeout: TimeSpan.FromSeconds(10),
            HeartbeatInterval: TimeSpan.FromMilliseconds(100),
            CancellationGracePeriod: TimeSpan.FromSeconds(2)));

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

    private static WorkerRunRequestDto CreateRequest(Guid flowId, NodeDto node, DateTimeOffset deadline)
    {
        var definition = new FlowDefinitionDto(
            flowId,
            1,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [])],
            node.Id,
            "test");
        return new WorkerRunRequestDto(
            WorkerProtocol.Version,
            Guid.NewGuid(),
            flowId,
            1,
            JsonSerializer.Serialize(definition),
            deadline);
    }
}

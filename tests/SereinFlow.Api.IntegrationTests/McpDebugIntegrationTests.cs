using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Mcp;

namespace SereinFlow.Api.IntegrationTests;

public sealed class McpDebugIntegrationTests : IClassFixture<McpHostIntegrationTests.ApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateContractOptions();
    private readonly McpHostIntegrationTests.ApiFactory _factory;

    public McpDebugIntegrationTests(McpHostIntegrationTests.ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task McpDebugToolsRunAFullStepContinueAndStopLifecycle()
    {
        var project = Project.Create("MCP debug integration project");
        var flow = CreateTwoNodeFlow();
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IProjectRepository>().AddAsync(project);
            await scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>().AddAsync(project.Id, flow);
        }

        var principal = new McpPrincipal(
            "mcp-debug-integration-admin",
            null,
            Enum.GetValues<McpPermissionDto>().ToHashSet(),
            IsAdministrator: true);
        _factory.Services.GetRequiredService<IMcpPrincipalAccessor>().Current = principal;
        var backend = _factory.Services.GetRequiredService<ISereinFlowMcpBackend>();

        var listed = await CallAsync<AiPageDto<AiRunSummaryDto>>(
            backend,
            "sereinflow_list_runs",
            new { projectId = project.Id, maxItems = 10 });
        Assert.Empty(listed.Items);

        var started = await CallAsync<McpDebugSessionStartedDto>(
            backend,
            "sereinflow_start_debug_session",
            new
            {
                projectId = project.Id,
                flowId = flow.Id,
                breakpointNodeIds = new[] { "first", "second" },
                idempotencyKey = "mcp-debug-step-continue"
            });

        var firstPause = await CallAsync<AiDebugStateWaitResultDto>(
            backend,
            "sereinflow_wait_debug_state",
            new { sessionId = started.SessionId, afterRevision = started.StateRevision, timeoutSeconds = 10 });
        Assert.False(firstPause.TimedOut);
        Assert.Equal(FlowDebugSessionStatusDto.Paused.ToString(), firstPause.State.Status);
        Assert.Equal("first", firstPause.State.CurrentNodeId);

        var step = await CallAsync<McpDebugCommandAcceptedDto>(
            backend,
            "sereinflow_step_debug",
            new { sessionId = started.SessionId, commandSequence = 1L });
        var secondPause = await CallAsync<AiDebugStateWaitResultDto>(
            backend,
            "sereinflow_wait_debug_state",
            new { sessionId = started.SessionId, afterRevision = step.StateRevision, timeoutSeconds = 10 });
        Assert.False(secondPause.TimedOut);
        Assert.Equal(FlowDebugSessionStatusDto.Paused.ToString(), secondPause.State.Status);
        Assert.Equal("second", secondPause.State.CurrentNodeId);

        var invalid = await Assert.ThrowsAsync<McpProtocolException>(() => backend.CallToolAsync(
            "sereinflow_step_debug",
            JsonSerializer.SerializeToElement(new { sessionId = started.SessionId, commandSequence = 0L }, JsonOptions),
            CancellationToken.None));
        Assert.Equal(-32602, invalid.Code);
        Assert.Contains(DebugErrorCodes.InvalidCommandSequence, JsonSerializer.Serialize(invalid.ErrorData));

        var duplicate = await Assert.ThrowsAsync<McpProtocolException>(() => backend.CallToolAsync(
            "sereinflow_step_debug",
            JsonSerializer.SerializeToElement(new { sessionId = started.SessionId, commandSequence = 1L }, JsonOptions),
            CancellationToken.None));
        Assert.Equal(-32010, duplicate.Code);
        Assert.Contains(DebugErrorCodes.CommandSequenceConflict, JsonSerializer.Serialize(duplicate.ErrorData));

        var continued = await CallAsync<McpDebugCommandAcceptedDto>(
            backend,
            "sereinflow_continue_debug",
            new { sessionId = started.SessionId, commandSequence = 2L });
        var completed = await CallAsync<AiDebugStateWaitResultDto>(
            backend,
            "sereinflow_wait_debug_state",
            new { sessionId = started.SessionId, afterRevision = continued.StateRevision, timeoutSeconds = 10 });
        Assert.False(completed.TimedOut);
        Assert.Equal(FlowDebugSessionStatusDto.Completed.ToString(), completed.State.Status);

        var runs = await CallAsync<AiPageDto<AiRunSummaryDto>>(
            backend,
            "sereinflow_list_runs",
            new { projectId = project.Id, maxItems = 10 });
        Assert.Contains(runs.Items, run => run.DebugSessionId == started.SessionId);

        var restarted = await CallAsync<McpDebugSessionStartedDto>(
            backend,
            "sereinflow_start_debug_session",
            new
            {
                projectId = project.Id,
                flowId = flow.Id,
                breakpointNodeIds = new[] { "first" },
                idempotencyKey = "mcp-debug-stop"
            });
        var restartPause = await CallAsync<AiDebugStateWaitResultDto>(
            backend,
            "sereinflow_wait_debug_state",
            new { sessionId = restarted.SessionId, afterRevision = restarted.StateRevision, timeoutSeconds = 10 });
        Assert.Equal(FlowDebugSessionStatusDto.Paused.ToString(), restartPause.State.Status);

        var stopped = await CallAsync<McpDebugCommandAcceptedDto>(
            backend,
            "sereinflow_stop_debug",
            new { sessionId = restarted.SessionId, commandSequence = 1L });
        var cancelled = await CallAsync<AiDebugStateWaitResultDto>(
            backend,
            "sereinflow_wait_debug_state",
            new { sessionId = restarted.SessionId, afterRevision = stopped.StateRevision, timeoutSeconds = 10 });
        Assert.False(cancelled.TimedOut);
        Assert.Equal(FlowDebugSessionStatusDto.Cancelled.ToString(), cancelled.State.Status);
    }

    private static async Task<T> CallAsync<T>(
        ISereinFlowMcpBackend backend,
        string tool,
        object arguments)
    {
        var result = await backend.CallToolAsync(
            tool,
            JsonSerializer.SerializeToElement(arguments),
            CancellationToken.None);
        return JsonSerializer.Deserialize<T>(
            JsonSerializer.Serialize(result.Value, JsonOptions),
            JsonOptions)!;
    }

    private static FlowDefinitionDto CreateTwoNodeFlow()
    {
        const string firstSource = "return 1";
        const string secondSource = "return 2";
        var flowId = Guid.NewGuid();
        var first = CreateScriptNode("first", "First", firstSource);
        var second = CreateScriptNode("second", "Second", secondSource);
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
        return new FlowDefinitionDto(
            flowId,
            FlowDefinition.CurrentSchemaVersion,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [first, second], [connection])],
            first.Id,
            "MCP debug integration",
            RunPolicy: new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel));
    }

    private static NodeDto CreateScriptNode(string id, string displayName, string source)
        => new(
            id,
            NodeTypeDto.Script,
            displayName,
            0,
            0,
            [],
            [],
            new ScriptNodeDataDto(
                id,
                source,
                "1",
                ScriptNodeDefinition.ComputeSourceHash(source),
                [],
                []));
}

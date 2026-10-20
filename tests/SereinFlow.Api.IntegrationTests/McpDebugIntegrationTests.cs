using System.Collections.Generic;
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
            var getRequiredServiceResult = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            await getRequiredServiceResult.AddAsync(project);
            var getRequiredServiceResult1 = scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
            await getRequiredServiceResult1.AddAsync(project.Id, flow);
        }

        var toHashSetResult = Enum.GetValues<McpPermissionDto>()
            .ToHashSet();
        var principal = new McpPrincipal("mcp-debug-integration-admin", null, toHashSetResult, IsAdministrator: true);
        _factory.Services.GetRequiredService<IMcpPrincipalAccessor>().Current = principal;
        var backend = _factory.Services.GetRequiredService<ISereinFlowMcpBackend>();
        var listed = await CallAsync<AiPageDto<AiRunSummaryDto>>(backend, "sereinflow_list_runs", new { projectId = project.Id, maxItems = 10 });
        Assert.Empty(listed.Items);
        var started = await CallAsync<McpDebugSessionStartedDto>(backend, "sereinflow_start_debug_session", new { projectId = project.Id, flowId = flow.Id, breakpointNodeIds = new[] { "first", "second" }, idempotencyKey = "mcp-debug-step-continue" });
        var firstPause = await CallAsync<AiDebugStateWaitResultDto>(backend, "sereinflow_wait_debug_state", new { sessionId = started.SessionId, afterRevision = started.StateRevision, timeoutSeconds = 10 });
        Assert.False(firstPause.TimedOut);
        var toStringResult = FlowDebugSessionStatusDto.Paused.ToString();
        Assert.Equal(toStringResult, firstPause.State.Status);
        Assert.Equal("first", firstPause.State.CurrentNodeId);
        _ = await CallAsync<RunVariableDto>(backend, "sereinflow_set_run_variable", new { runId = started.RunId, name = "debug.final.count", value = 42, valueType = "int32", idempotencyKey = "mcp-debug-final-variable" });
        var step = await CallAsync<McpDebugCommandAcceptedDto>(backend, "sereinflow_step_debug", new { sessionId = started.SessionId, commandSequence = 1L });
        var secondPause = await CallAsync<AiDebugStateWaitResultDto>(backend, "sereinflow_wait_debug_state", new { sessionId = started.SessionId, afterRevision = step.StateRevision, timeoutSeconds = 10 });
        Assert.False(secondPause.TimedOut);
        var toStringResult4 = FlowDebugSessionStatusDto.Paused.ToString();
        Assert.Equal(toStringResult4, secondPause.State.Status);
        Assert.Equal("second", secondPause.State.CurrentNodeId);
        var invalid = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            var serializeToElementResult = JsonSerializer.SerializeToElement(new { sessionId = started.SessionId, commandSequence = 0L }, JsonOptions);
            var callToolAsyncResult = backend.CallToolAsync("sereinflow_step_debug", serializeToElementResult, CancellationToken.None);
            return callToolAsyncResult;
        });
        Assert.Equal(McpProtocolErrorCodes.InvalidParams, invalid.Code);
        var serializeResult = JsonSerializer.Serialize(invalid.ErrorData);
        Assert.Contains(DebugErrorCodes.InvalidCommandSequence, serializeResult);
        var duplicate = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            var serializeToElementResult2 = JsonSerializer.SerializeToElement(new { sessionId = started.SessionId, commandSequence = 1L }, JsonOptions);
            var callToolAsyncResult3 = backend.CallToolAsync("sereinflow_step_debug", serializeToElementResult2, CancellationToken.None);
            return callToolAsyncResult3;
        });
        Assert.Equal(McpProtocolErrorCodes.Conflict, duplicate.Code);
        var serializeResult5 = JsonSerializer.Serialize(duplicate.ErrorData);
        Assert.Contains(DebugErrorCodes.CommandSequenceConflict, serializeResult5);
        var continued = await CallAsync<McpDebugCommandAcceptedDto>(backend, "sereinflow_continue_debug", new { sessionId = started.SessionId, commandSequence = 2L });
        var completed = await CallAsync<AiDebugStateWaitResultDto>(backend, "sereinflow_wait_debug_state", new { sessionId = started.SessionId, afterRevision = continued.StateRevision, timeoutSeconds = 10 });
        Assert.False(completed.TimedOut);
        var toStringResult6 = FlowDebugSessionStatusDto.Completed.ToString();
        Assert.Equal(toStringResult6, completed.State.Status);
        var finalVariables = await CallAsync<RunVariableSnapshotDto>(backend, "sereinflow_get_run_variables", new { runId = started.RunId });
        Assert.True(finalVariables.IsFinal);
        var evaluatedValue = Assert.Single(finalVariables.Variables, variable => variable.Name == "debug.final.count").Value!.Value;
        var getInt32Result = evaluatedValue.GetInt32();
        Assert.Equal(42, getInt32Result);
        var runs = await CallAsync<AiPageDto<AiRunSummaryDto>>(backend, "sereinflow_list_runs", new { projectId = project.Id, maxItems = 10 });
        Assert.Contains(runs.Items, run => run.DebugSessionId == started.SessionId);
        var restarted = await CallAsync<McpDebugSessionStartedDto>(backend, "sereinflow_start_debug_session", new { projectId = project.Id, flowId = flow.Id, breakpointNodeIds = new[] { "first" }, idempotencyKey = "mcp-debug-stop" });
        var restartPause = await CallAsync<AiDebugStateWaitResultDto>(backend, "sereinflow_wait_debug_state", new { sessionId = restarted.SessionId, afterRevision = restarted.StateRevision, timeoutSeconds = 10 });
        var toStringResult7 = FlowDebugSessionStatusDto.Paused.ToString();
        Assert.Equal(toStringResult7, restartPause.State.Status);
        var stopped = await CallAsync<McpDebugCommandAcceptedDto>(backend, "sereinflow_stop_debug", new { sessionId = restarted.SessionId, commandSequence = 1L });
        var cancelled = await CallAsync<AiDebugStateWaitResultDto>(backend, "sereinflow_wait_debug_state", new { sessionId = restarted.SessionId, afterRevision = stopped.StateRevision, timeoutSeconds = 10 });
        Assert.False(cancelled.TimedOut);
        var toStringResult8 = FlowDebugSessionStatusDto.Cancelled.ToString();
        Assert.Equal(toStringResult8, cancelled.State.Status);
    }

    private static async Task<T> CallAsync<T>(ISereinFlowMcpBackend backend, string tool, object arguments)
    {
        var serializeToElementResult9 = JsonSerializer.SerializeToElement(arguments);
        var result = await backend.CallToolAsync(tool, serializeToElementResult9, CancellationToken.None);
        var evaluatedValue11 = resolveDeserializeResult()!;
        return evaluatedValue11;
        T? resolveDeserializeResult()
        {
            var serializeResult10 = JsonSerializer.Serialize(result.Value, JsonOptions);
            var deserializeResult = JsonSerializer.Deserialize<T>(serializeResult10, JsonOptions);
            return deserializeResult;
        }
    }

    private static FlowDefinitionDto CreateTwoNodeFlow()
    {
        const string firstSource = "return 1";
        const string secondSource = "return 2";
        var flowId = Guid.NewGuid();
        var first = CreateScriptNode("first", "First", firstSource);
        var second = CreateScriptNode("second", "Second", secondSource);
        var connection = new ConnectionDto("first:success->second:execute", first.Id, "success", second.Id, "execute", ConnectionKindDto.Execution, ExecutionBranchDto.Success, null, 0);
        IReadOnlyList<CanvasDto> evaluatedValue12 = [new CanvasDto("main", CanvasLifecycleDto.Main, [first, second], [connection])];
        var flowRunPolicyDto = new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel);
        var flowDefinitionDto = new FlowDefinitionDto(flowId, FlowDefinition.CurrentSchemaVersion, 1, evaluatedValue12, first.Id, "MCP debug integration", RunPolicy: flowRunPolicyDto);
        return flowDefinitionDto;
    }

    private static NodeDto CreateScriptNode(string id, string displayName, string source)
    {
        var computeSourceHashResult = ScriptNodeDefinition.ComputeSourceHash(source);
        var scriptNodeDataDto = new ScriptNodeDataDto(id, source, "1", computeSourceHashResult, [], []);
        NodeDto evaluatedValue13 = new(id, NodeTypeDto.Script, displayName, 0, 0, [], [], scriptNodeDataDto);
        return evaluatedValue13;
    }
}

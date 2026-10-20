using System.Collections.Generic;
using System.Text.Json;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Api;
internal static class ApiEndpointHelpers
{
    private static readonly TimeSpan RunCompletionPollInterval = TimeSpan.FromMilliseconds(80);
    internal static ProjectDto ToProjectDto(Project project)
    {
        var toProjectStatusValueResult = ToProjectStatusValue(project.Status);
        ProjectDto evaluatedValue = new(project.Id, project.Name, project.Version, toProjectStatusValueResult, project.CreatedAt, project.UpdatedAt);
        return evaluatedValue;
    }

    internal static string ToProjectStatusValue(ProjectStatus status)
    {
        string evaluatedValue1 = status switch
        {
            ProjectStatus.Draft => "draft",
            ProjectStatus.Ready => "ready",
            ProjectStatus.ScriptInvalid => "scriptInvalid",
            ProjectStatus.Archived => "archived",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The project status is not supported. 项目状态不受支持。")};
        return evaluatedValue1;
    }

    internal static FlowRunDto ToRunDto(FlowRun run)
    {
        FlowRunDto evaluatedValue2 = new(run.Id, run.FlowId, run.FlowVersion, (FlowRunStatusDto)run.Status, run.StartedAt, run.EndedAt, run.ErrorSummary, run.ProjectId == Guid.Empty ? null : run.ProjectId, run.CreatedAt, run.CancellationReason, (FlowConcurrencyModeDto)run.ConcurrencyMode, run.IsListenerRun, run.QueuedAt, (FlowRunExecutionKindDto)run.ExecutionKind, run.DebugSessionId, run.DefinitionChecksum);
        return evaluatedValue2;
    }

    internal static FlowDebugSessionDto ToFlowDebugSessionDto(FlowDebugSession session)
    {
        FlowDebugPauseStateDto? evaluatedValue3 = session.PauseState is null ? null : resolveFlowDebugPauseStateDto();
        FlowDebugNodeResultDto? evaluatedValue6 = session.LastNodeResult is null ? null : resolveFlowDebugNodeResultDto();
        FlowDebugSessionDto evaluatedValue7 = new(session.Id, session.RunId, session.ProjectId, session.FlowId, (FlowDebugSessionStatusDto)session.Status, session.BreakpointNodeIds, session.CurrentNodeId, session.ActiveInvocationId, session.ActiveFlipflopNodeId, session.QueuedTriggerCount, session.LastCommandSequence, session.FailureMessage, session.CreatedAt, session.UpdatedAt, session.StateRevision, evaluatedValue3, evaluatedValue6);
        return evaluatedValue7;
        FlowDebugPauseStateDto resolveFlowDebugPauseStateDto()
        {
            var parseDebugJsonResult = ParseDebugJson(session.PauseState.InputsJson);
            var flowDebugPauseStateDto = new FlowDebugPauseStateDto(session.PauseState.NodeId, session.PauseState.NodeType, session.PauseState.Step, session.PauseState.FrameDepth, session.PauseState.InvocationId, session.PauseState.BoundarySequence, parseDebugJsonResult, session.PauseState.PausedAt, session.PauseState.ExecutionId);
            return flowDebugPauseStateDto;
        }

        FlowDebugNodeResultDto resolveFlowDebugNodeResultDto()
        {
            var parseDebugJsonResult4 = ParseDebugJson(session.LastNodeResult.InputsJson);
            var parseDebugJsonResult5 = ParseDebugJson(session.LastNodeResult.OutputsJson);
            var flowDebugNodeResultDto = new FlowDebugNodeResultDto(session.LastNodeResult.NodeId, session.LastNodeResult.Sequence, session.LastNodeResult.CompletedAt, session.LastNodeResult.Outcome, session.LastNodeResult.Branch, parseDebugJsonResult4, parseDebugJsonResult5, session.LastNodeResult.ErrorCode, session.LastNodeResult.ErrorMessage);
            return flowDebugNodeResultDto;
        }
    }

    internal static JsonElement ParseDebugJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            var serializeToElementResult = JsonSerializer.SerializeToElement(new { });
            return serializeToElementResult;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var cloneResult = document.RootElement.Clone();
            return cloneResult;
        }
        catch (JsonException)
        {
            var serializeToElementResult8 = JsonSerializer.SerializeToElement(new { malformed = true, raw = json });
            return serializeToElementResult8;
        }
    }

    internal static IResult ToDebugCommandResponse(FlowDebugSessionCommandResult result)
    {
        var evaluatedValue9 = result.IsAccepted ? Results.Accepted() : Results.Problem(statusCode: result.StatusCode, title: result.ErrorTitle);
        return evaluatedValue9;
    }

    internal static FlowDefinitionSummaryDto ToFlowSummaryDto(FlowDefinitionDto definition, long? productionVersion = null)
    {
        var sumResult = definition.Canvases.Sum(static canvas => canvas.Nodes.Count);
        FlowDefinitionSummaryDto evaluatedValue10 = new(definition.Id, definition.Version, definition.EntryNodeId, definition.Canvases.Count, sumResult, productionVersion);
        return evaluatedValue10;
    }

    internal static bool TryParseFlowVersionTrack(string? value, out FlowVersionTrackDto track)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            track = FlowVersionTrackDto.Development;
            return true;
        }

        var evaluatedValue11 = Enum.TryParse(value, ignoreCase: true, out track) && Enum.IsDefined(track);
        return evaluatedValue11;
    }

    internal static IResult ToRunSubmissionResponse(RunSubmissionResult submission)
    {
        if (submission.IsAccepted)
        {
            var toRunDtoResult = ToRunDto(submission.Run);
            var acceptedResult = Results.Accepted($"/api/runs/{submission.Run!.Id:D}", toRunDtoResult);
            return acceptedResult;
        }

        if (submission.ErrorBody is not null)
        {
            var jsonResult = Results.Json(submission.ErrorBody, statusCode: submission.StatusCode);
            return jsonResult;
        }

        var problem = submission.CurrentVersion is null
            ? ApiProblemDetailsFactory.Create(submission.StatusCode, submission.ErrorTitle)
            : ApiProblemDetailsFactory.WithCurrentVersion(submission.StatusCode, submission.ErrorTitle, submission.CurrentVersion);
        var response = Results.Problem(problem);
        return response;
    }

    internal static IResult ToProjectLibraryResponse(ProjectLibraryOperationResult result)
    {
        if (result.IsSuccess)
        {
            var okResult = Results.Ok(result.References ?? []);
            return okResult;
        }

        var problem = string.IsNullOrWhiteSpace(result.Code)
            ? ApiProblemDetailsFactory.Create(result.StatusCode, result.Message)
            : ApiProblemDetailsFactory.WithCode(result.StatusCode, result.Message, result.Code);
        var response = Results.Problem(problem);
        return response;
    }

    internal static IResult ToLibraryUpgradeResponse<T>(LibraryUpgradeOperationResult<T> result)
    {
        if (result.IsSuccess)
        {
            if (result.StatusCode == StatusCodes.Status201Created)
            {
                var upgradeId = GetUpgradeId(result.Value);
                var upgradeLocation = $"/api/projects/library-upgrades/{upgradeId}";
                var createdResponse = Results.Created(upgradeLocation, result.Value);
                return createdResponse;
            }

            var successfulResponse = Results.Ok(result.Value);
            return successfulResponse;
        }

        var title = result.Message ?? "Library upgrade request failed. 类库升级请求失败。";
        var problem = ApiProblemDetailsFactory.WithCode(result.StatusCode, title, result.Code, result.CurrentVersion);
        var response = Results.Problem(problem);
        return response;
    }

    internal static string GetUpgradeId<T>(T? value)
    {
        string evaluatedValue17 = value is LibraryUpgradePlanDto plan ? plan.Id.ToString("D") : string.Empty;
        return evaluatedValue17;
    }

    internal static async Task<FlowRun?> WaitForTerminalRunAsync(IFlowRunStore runStore, Guid runId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var run = await runStore.FindAsync(runId, cancellationToken);
            if (run is null || run.IsTerminal)
            {
                return run;
            }

            await Task.Delay(RunCompletionPollInterval, cancellationToken);
        }

        var findAsyncResult = await runStore.FindAsync(runId, cancellationToken);
        return findAsyncResult;
    }

    internal static async Task<IReadOnlyList<FlowNodeDataDto>> ReadNodeDataAsync(IFlowRunOutputStore outputStore, Guid runId, CancellationToken cancellationToken)
    {
        var data = new List<FlowNodeDataDto>();
        foreach (var item in await outputStore.ListAsync(runId, cancellationToken))
        {
            if (!string.Equals(item.Outcome, "completed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var outputs = JsonDocument.Parse(item.OutputsJson);
            var cloneResult18 = outputs.RootElement.Clone();
            var flowNodeDataDto = new FlowNodeDataDto(item.NodeId, cloneResult18);
            data.Add(flowNodeDataDto);
        }

        return data;
    }

    internal static FlowRunOutputDto ToRunOutputDto(FlowRunOutput output)
    {
        using var inputs = JsonDocument.Parse(output.InputsJson);
        using var outputs = JsonDocument.Parse(output.OutputsJson);
        var cloneResult19 = inputs.RootElement.Clone();
        var cloneResult20 = outputs.RootElement.Clone();
        var flowRunOutputDto = new FlowRunOutputDto(output.RunId, output.Sequence, output.Timestamp, output.NodeId, output.Outcome, output.Branch, cloneResult19, cloneResult20, output.ErrorCode, output.ErrorMessage);
        return flowRunOutputDto;
    }

    internal static async Task WriteSseAsync(HttpContext context, FlowRunEventDto item)
    {
        var evaluatedValue21 = $"id: {item.Sequence}\nevent: {item.Type}\ndata: {resolveSerializeResult()}\n\n";
        await context.Response.WriteAsync(evaluatedValue21, context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
        string resolveSerializeResult()
        {
            var createWebOptionsResult = SereinJsonSerialization.CreateWebOptions();
            var serializeResult = JsonSerializer.Serialize(item, createWebOptionsResult);
            return serializeResult;
        }
    }
}

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
        var status = ToProjectStatusValue(project.Status);
        return new(project.Id, project.Name, project.Version, status, project.CreatedAt, project.UpdatedAt);
    }

    internal static string ToProjectStatusValue(ProjectStatus status)
    {
        return status switch
        {
            ProjectStatus.Draft => "draft",
            ProjectStatus.Ready => "ready",
            ProjectStatus.ScriptInvalid => "scriptInvalid",
            ProjectStatus.Archived => "archived",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The project status is not supported. 项目状态不受支持。")
        };
    }

    internal static FlowRunDto ToRunDto(FlowRun run)
    {
        return new(run.Id, run.FlowId, run.FlowVersion, (FlowRunStatusDto)run.Status, run.StartedAt, run.EndedAt, run.ErrorSummary, run.ProjectId == Guid.Empty ? null : run.ProjectId, run.CreatedAt, run.CancellationReason, (FlowConcurrencyModeDto)run.ConcurrencyMode, run.IsListenerRun, run.QueuedAt, (FlowRunExecutionKindDto)run.ExecutionKind, run.DebugSessionId, run.DefinitionChecksum);
    }

    internal static FlowDebugSessionDto ToFlowDebugSessionDto(FlowDebugSession session)
    {
        FlowDebugPauseStateDto? pauseState = session.PauseState is null ? null : MapPauseState();
        FlowDebugNodeResultDto? flowDebugNodeResultDto = session.LastNodeResult is null ? null : MapLastNodeResult();
        return new(session.Id, session.RunId, session.ProjectId, session.FlowId, (FlowDebugSessionStatusDto)session.Status, session.BreakpointNodeIds, session.CurrentNodeId, session.ActiveInvocationId, session.ActiveFlipflopNodeId, session.QueuedTriggerCount, session.LastCommandSequence, session.FailureMessage, session.CreatedAt, session.UpdatedAt, session.StateRevision, pauseState, flowDebugNodeResultDto);
        FlowDebugPauseStateDto MapPauseState()
        {
            var inputs = ParseDebugJson(session.PauseState.InputsJson);
            return new FlowDebugPauseStateDto(session.PauseState.NodeId, session.PauseState.NodeType, session.PauseState.Step, session.PauseState.FrameDepth, session.PauseState.InvocationId, session.PauseState.BoundarySequence, inputs, session.PauseState.PausedAt, session.PauseState.ExecutionId);
        }

        FlowDebugNodeResultDto MapLastNodeResult()
        {
            var debugJson = ParseDebugJson(session.LastNodeResult.InputsJson);
            var outputs = ParseDebugJson(session.LastNodeResult.OutputsJson);
            return new FlowDebugNodeResultDto(session.LastNodeResult.NodeId, session.LastNodeResult.Sequence, session.LastNodeResult.CompletedAt, session.LastNodeResult.Outcome, session.LastNodeResult.Branch, debugJson, outputs, session.LastNodeResult.ErrorCode, session.LastNodeResult.ErrorMessage);
        }
    }

    internal static JsonElement ParseDebugJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return JsonSerializer.SerializeToElement(new { });
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(new { malformed = true, raw = json });
        }
    }

    internal static IResult ToDebugCommandResponse(FlowDebugSessionCommandResult result)
    {
        return result.IsAccepted ? Results.Accepted() : Results.Problem(statusCode: result.StatusCode, title: result.ErrorTitle);
    }

    internal static FlowDefinitionSummaryDto ToFlowSummaryDto(FlowDefinitionDto definition, long? productionVersion = null)
    {
        var nodeCount = definition.Canvases.Sum(static canvas => canvas.Nodes.Count);
        return new(definition.Id, definition.Version, definition.EntryNodeId, definition.Canvases.Count, nodeCount, productionVersion);
    }

    internal static bool TryParseFlowVersionTrack(string? value, out FlowVersionTrackDto track)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            track = FlowVersionTrackDto.Development;
            return true;
        }
        return Enum.TryParse(value, ignoreCase: true, out track) && Enum.IsDefined(track);
    }

    internal static IResult ToRunSubmissionResponse(RunSubmissionResult submission)
    {
        if (submission.IsAccepted)
        {
            var runDto = ToRunDto(submission.Run);
            return Results.Accepted($"/api/runs/{submission.Run!.Id:D}", runDto);
        }

        if (submission.ErrorBody is not null)
        {
            return Results.Json(submission.ErrorBody, statusCode: submission.StatusCode);
        }

        var problem = submission.CurrentVersion is null
            ? ApiProblemDetailsFactory.Create(submission.StatusCode, submission.ErrorTitle)
            : ApiProblemDetailsFactory.WithCurrentVersion(submission.StatusCode, submission.ErrorTitle, submission.CurrentVersion);
        return Results.Problem(problem);
    }

    internal static IResult ToProjectLibraryResponse(ProjectLibraryOperationResult result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.References ?? []);
        }

        var problem = string.IsNullOrWhiteSpace(result.Code)
            ? ApiProblemDetailsFactory.Create(result.StatusCode, result.Message)
            : ApiProblemDetailsFactory.WithCode(result.StatusCode, result.Message, result.Code);
        return Results.Problem(problem);
    }

    internal static IResult ToLibraryUpgradeResponse<T>(LibraryUpgradeOperationResult<T> result)
    {
        if (result.IsSuccess)
        {
            if (result.StatusCode == StatusCodes.Status201Created)
            {
                var upgradeId = GetUpgradeId(result.Value);
                var upgradeLocation = $"/api/projects/library-upgrades/{upgradeId}";
                return Results.Created(upgradeLocation, result.Value);
            }
            return Results.Ok(result.Value);
        }

        var title = result.Message ?? "Library upgrade request failed. 类库升级请求失败。";
        var problem = ApiProblemDetailsFactory.WithCode(result.StatusCode, title, result.Code, result.CurrentVersion);
        return Results.Problem(problem);
    }

    internal static string GetUpgradeId<T>(T? value)
    {
        return value is LibraryUpgradePlanDto plan ? plan.Id.ToString("D") : string.Empty;
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
        return await runStore.FindAsync(runId, cancellationToken);
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
            var clone = outputs.RootElement.Clone();
            var flowNodeDataDto = new FlowNodeDataDto(item.NodeId, clone);
            data.Add(flowNodeDataDto);
        }

        return data;
    }

    internal static FlowRunOutputDto ToRunOutputDto(FlowRunOutput output)
    {
        using var inputs = JsonDocument.Parse(output.InputsJson);
        using var outputs = JsonDocument.Parse(output.OutputsJson);
        var clone = inputs.RootElement.Clone();
        var jsonElement = outputs.RootElement.Clone();
        return new FlowRunOutputDto(output.RunId, output.Sequence, output.Timestamp, output.NodeId, output.Outcome, output.Branch, clone, jsonElement, output.ErrorCode, output.ErrorMessage);
    }

    internal static async Task WriteSseAsync(HttpContext context, FlowRunEventDto item)
    {
        var eventFrame = $"id: {item.Sequence}\nevent: {item.Type}\ndata: {SerializeEvent()}\n\n";
        await context.Response.WriteAsync(eventFrame, context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
        string SerializeEvent()
        {
            var webOptions = SereinJsonSerialization.CreateWebOptions();
            return JsonSerializer.Serialize(item, webOptions);
        }
    }
}

using System.Text.Json;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Api;

internal static class ApiEndpointHelpers
{
    internal static ProjectDto ToProjectDto(Project project)
        => new(project.Id, project.Name, project.Version, ToProjectStatusValue(project.Status), project.CreatedAt, project.UpdatedAt);

    internal static string ToProjectStatusValue(ProjectStatus status)
        => status switch
        {
            ProjectStatus.Draft => "draft",
            ProjectStatus.Ready => "ready",
            ProjectStatus.ScriptInvalid => "scriptInvalid",
            ProjectStatus.Archived => "archived",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The project status is not supported. 项目状态不受支持。")
        };

    internal static FlowRunDto ToRunDto(FlowRun run)
        => new(
            run.Id,
            run.FlowId,
            run.FlowVersion,
            (FlowRunStatusDto)run.Status,
            run.StartedAt,
            run.EndedAt,
            run.ErrorSummary,
            run.ProjectId == Guid.Empty ? null : run.ProjectId,
            run.CreatedAt,
            run.CancellationReason,
            (FlowConcurrencyModeDto)run.ConcurrencyMode,
            run.IsListenerRun,
            run.QueuedAt,
            (FlowRunExecutionKindDto)run.ExecutionKind,
            run.DebugSessionId);

    internal static FlowDebugSessionDto ToFlowDebugSessionDto(FlowDebugSession session)
        => new(
            session.Id,
            session.RunId,
            session.ProjectId,
            session.FlowId,
            (FlowDebugSessionStatusDto)session.Status,
            session.BreakpointNodeIds,
            session.CurrentNodeId,
            session.ActiveInvocationId,
            session.ActiveFlipflopNodeId,
            session.QueuedTriggerCount,
            session.LastCommandSequence,
            session.FailureMessage,
            session.CreatedAt,
            session.UpdatedAt,
            session.StateRevision,
            session.PauseState is null
                ? null
                : new FlowDebugPauseStateDto(
                    session.PauseState.NodeId,
                    session.PauseState.NodeType,
                    session.PauseState.Step,
                    session.PauseState.FrameDepth,
                    session.PauseState.InvocationId,
                    session.PauseState.BoundarySequence,
                    ParseDebugJson(session.PauseState.InputsJson),
                    session.PauseState.PausedAt,
                    session.PauseState.ExecutionId),
            session.LastNodeResult is null
                ? null
                : new FlowDebugNodeResultDto(
                    session.LastNodeResult.NodeId,
                    session.LastNodeResult.Sequence,
                    session.LastNodeResult.CompletedAt,
                    session.LastNodeResult.Outcome,
                    session.LastNodeResult.Branch,
                    ParseDebugJson(session.LastNodeResult.InputsJson),
                    ParseDebugJson(session.LastNodeResult.OutputsJson),
                    session.LastNodeResult.ErrorCode,
                    session.LastNodeResult.ErrorMessage));

    internal static JsonElement ParseDebugJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return JsonSerializer.SerializeToElement(new { });

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(new
            {
                malformed = true,
                raw = json
            });
        }
    }

    internal static IResult ToDebugCommandResponse(FlowDebugSessionCommandResult result)
        => result.IsAccepted
            ? Results.Accepted()
            : Results.Problem(statusCode: result.StatusCode, title: result.ErrorTitle);

    internal static FlowDefinitionSummaryDto ToFlowSummaryDto(FlowDefinitionDto definition, long? productionVersion = null)
        => new(
            definition.Id,
            definition.Version,
            definition.EntryNodeId,
            definition.Canvases.Count,
            definition.Canvases.Sum(static canvas => canvas.Nodes.Count),
            productionVersion);

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
            return Results.Accepted($"/api/runs/{submission.Run!.Id:D}", ToRunDto(submission.Run));
        if (submission.ErrorBody is not null)
            return Results.Json(submission.ErrorBody, statusCode: submission.StatusCode);
        return Results.Problem(
            statusCode: submission.StatusCode,
            title: submission.ErrorTitle,
            extensions: submission.CurrentVersion is null
                ? null
                : new Dictionary<string, object?> { ["currentVersion"] = submission.CurrentVersion });
    }

    internal static IResult ToProjectLibraryResponse(ProjectLibraryOperationResult result)
    {
        if (result.IsSuccess)
            return Results.Ok(result.References ?? []);

        return Results.Problem(
            statusCode: result.StatusCode,
            title: result.Message,
            extensions: string.IsNullOrWhiteSpace(result.Code)
                ? null
                : new Dictionary<string, object?> { ["code"] = result.Code });
    }

    internal static IResult ToLibraryUpgradeResponse<T>(LibraryUpgradeOperationResult<T> result)
    {
        if (result.IsSuccess)
        {
            return result.StatusCode == StatusCodes.Status201Created
                ? Results.Created($"/api/projects/library-upgrades/{GetUpgradeId(result.Value)}", result.Value)
                : Results.Ok(result.Value);
        }

        var extensions = new Dictionary<string, object?> { ["code"] = result.Code };
        if (result.CurrentVersion is not null)
            extensions["currentVersion"] = result.CurrentVersion;
        return Results.Problem(
            statusCode: result.StatusCode,
            title: result.Message ?? "Library upgrade request failed. 类库升级请求失败。",
            extensions: extensions);
    }

    internal static string GetUpgradeId<T>(T? value)
        => value is LibraryUpgradePlanDto plan ? plan.Id.ToString("D") : string.Empty;

    internal static async Task<FlowRun?> WaitForTerminalRunAsync(
        IFlowRunStore runStore,
        Guid runId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var run = await runStore.FindAsync(runId, cancellationToken);
            if (run is null || run.IsTerminal)
                return run;
            await Task.Delay(TimeSpan.FromMilliseconds(80), cancellationToken);
        }
        return await runStore.FindAsync(runId, cancellationToken);
    }

    internal static async Task<IReadOnlyList<FlowNodeDataDto>> ReadNodeDataAsync(
        IFlowRunOutputStore outputStore,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var data = new List<FlowNodeDataDto>();
        foreach (var item in await outputStore.ListAsync(runId, cancellationToken))
        {
            if (!string.Equals(item.Outcome, "completed", StringComparison.OrdinalIgnoreCase))
                continue;

            using var outputs = JsonDocument.Parse(item.OutputsJson);
            data.Add(new FlowNodeDataDto(item.NodeId, outputs.RootElement.Clone()));
        }

        return data;
    }

    internal static FlowRunOutputDto ToRunOutputDto(FlowRunOutput output)
    {
        using var inputs = JsonDocument.Parse(output.InputsJson);
        using var outputs = JsonDocument.Parse(output.OutputsJson);
        return new FlowRunOutputDto(
            output.RunId,
            output.Sequence,
            output.Timestamp,
            output.NodeId,
            output.Outcome,
            output.Branch,
            inputs.RootElement.Clone(),
            outputs.RootElement.Clone(),
            output.ErrorCode,
            output.ErrorMessage);
    }

    internal static async Task WriteSseAsync(HttpContext context, FlowRunEventDto item)
    {
        await context.Response.WriteAsync($"id: {item.Sequence}\nevent: {item.Type}\ndata: {JsonSerializer.Serialize(item, SereinJsonSerialization.CreateWebOptions())}\n\n", context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }

}

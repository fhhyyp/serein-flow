using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using static SereinFlow.Mcp.Support.McpReadOptions;
using static SereinFlow.Mcp.Support.McpToolAuthorization;

namespace SereinFlow.Mcp.Queries;

internal sealed class McpRunQueries(
    AiReadModelService service,
    IFlowRunStore _flowRunStore,
    IFlowWorkpieceStore _flowWorkpieceStore,
    SereinFlowAccess security)
{
    internal async Task<FlowWorkpieceDto[]?> ListRunWorkpiecesAsync(
        SereinFlowActor? principal,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await _flowRunStore.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        await security.RequireAnyAsync(principal, run.ProjectId, cancellationToken, SereinFlowPermission.DebugRead, SereinFlowPermission.RunRead);
        var items = await _flowWorkpieceStore.ListAsync(runId, cancellationToken);
        return items.Select(ToWorkpieceDto).ToArray();
    }

    internal async Task<FlowWorkpieceDto?> GetRunWorkpieceAsync(
        SereinFlowActor? principal,
        Guid runId,
        string workpieceId,
        CancellationToken cancellationToken)
    {
        var run = await _flowRunStore.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        await security.RequireAnyAsync(principal, run.ProjectId, cancellationToken, SereinFlowPermission.DebugRead, SereinFlowPermission.RunRead);
        var item = await _flowWorkpieceStore.FindAsync(runId, workpieceId, cancellationToken);
        return item is null ? null : ToWorkpieceDto(item);
    }

    private static FlowWorkpieceDto ToWorkpieceDto(FlowWorkpieceRecord item)
    {
        var runWorkpiece = SereinFlowApiUris.RunWorkpiece(item.RunId, item.Id);
        return new FlowWorkpieceDto(item.RunId, item.Id, item.Kind, item.Name, item.ContentType, item.Length, item.CreatedAt, runWorkpiece, item.NodeId, item.ExecutionId);
    }

    internal async Task<AiRunInspectionDto?> ReadRunAsync(
        SereinFlowActor? principal,
        Guid runId,
        CancellationToken cancellationToken,
        FlowVersionTrackDto? definitionTrack = null,
        AiReadModelOptions? options = null,
        long afterEventSequence = 0)
    {
        await security.RequireAsync(principal, SereinFlowPermission.RunRead, cancellationToken: cancellationToken);
        var inspection = await service.GetRunInspectionAsync(runId, definitionTrack, options, afterEventSequence, cancellationToken);
        if (inspection is not null)
        {
            await security.RequireAsync(principal, SereinFlowPermission.RunRead, inspection.Run.ProjectId, cancellationToken: cancellationToken);
            await RequireSensitiveReadAsync(security, principal, inspection.Run.ProjectId, options, cancellationToken: cancellationToken);
        }

        return inspection;
    }

    internal async Task<AiPageDto<AiRunSummaryDto>> ListRunsAsync(
        SereinFlowMcpCaller context,
        McpListRunsRequest arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var requestedProjectId = arguments.ProjectId;
        var projectId = requestedProjectId;
        var scope = await security.GetReadScopeAsync(principal, cancellationToken);
        await security.RequireAnyAsync(principal, projectId, cancellationToken, SereinFlowPermission.DebugRead, SereinFlowPermission.RunRead);
        var status = arguments.Status;
        var options = ReadOptions(arguments);
        return await service.ListRunsAsync(projectId, status, options, cancellationToken, scope);
    }
}

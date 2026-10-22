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
    McpSecurityService security)
{
    internal async Task<FlowWorkpieceDto[]?> ListRunWorkpiecesAsync(
        McpPrincipal? principal,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await _flowRunStore.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        security.RequireAny(principal, run.ProjectId, McpPermissionDto.DebugRead, McpPermissionDto.RunRead);
        var items = await _flowWorkpieceStore.ListAsync(runId, cancellationToken);
        return items.Select(ToWorkpieceDto).ToArray();
    }

    internal async Task<FlowWorkpieceDto?> GetRunWorkpieceAsync(
        McpPrincipal? principal,
        Guid runId,
        string workpieceId,
        CancellationToken cancellationToken)
    {
        var run = await _flowRunStore.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        security.RequireAny(principal, run.ProjectId, McpPermissionDto.DebugRead, McpPermissionDto.RunRead);
        var item = await _flowWorkpieceStore.FindAsync(runId, workpieceId, cancellationToken);
        return item is null ? null : ToWorkpieceDto(item);
    }

    private static FlowWorkpieceDto ToWorkpieceDto(FlowWorkpieceRecord item)
    {
        var runWorkpiece = SereinFlowApiUris.RunWorkpiece(item.RunId, item.Id);
        return new FlowWorkpieceDto(item.RunId, item.Id, item.Kind, item.Name, item.ContentType, item.Length, item.CreatedAt, runWorkpiece, item.NodeId, item.ExecutionId);
    }

    internal async Task<AiRunInspectionDto?> ReadRunAsync(
        McpPrincipal? principal,
        Guid runId,
        CancellationToken cancellationToken,
        FlowVersionTrackDto? definitionTrack = null,
        AiReadModelOptions? options = null,
        long afterEventSequence = 0)
    {
        security.Require(principal, McpPermissionDto.RunRead);
        var inspection = await service.GetRunInspectionAsync(runId, definitionTrack, options, afterEventSequence, cancellationToken);
        if (inspection is not null)
        {
            security.Require(principal, McpPermissionDto.RunRead, inspection.Run.ProjectId);
            RequireSensitiveRead(security, principal, inspection.Run.ProjectId, options);
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
        var projectId = IsProjectScoped(principal) ? principal.ProjectId : requestedProjectId;
        security.RequireAny(principal, projectId, McpPermissionDto.DebugRead, McpPermissionDto.RunRead);
        var status = arguments.Status;
        var options = ReadOptions(arguments);
        return await service.ListRunsAsync(projectId, status, options, cancellationToken);
    }
}

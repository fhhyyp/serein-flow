using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using SereinFlow.Mcp.Queries;
using static SereinFlow.Mcp.Support.McpReadOptions;

namespace SereinFlow.Mcp.Tools;

internal sealed class McpRunQueryTools(McpRunQueries queries)
{
    [SereinFlowMcpTool("sereinflow_get_run_inspection", McpPermissionDto.RunRead)]
    internal Task<AiRunInspectionDto?> GetInspectionAsync(
        SereinFlowMcpCaller caller,
        McpRunInspectionRequest request,
        CancellationToken cancellationToken) => queries.ReadRunAsync(caller.Principal, request.RunId, cancellationToken, request.Track, ReadOptions(request), request.AfterEventSequence);
    [SereinFlowMcpTool("sereinflow_list_run_workpieces", McpPermissionDto.DebugRead, McpPermissionDto.RunRead)]
    internal Task<FlowWorkpieceDto[]?> ListWorkpiecesAsync(
        SereinFlowMcpCaller caller,
        McpRunQueryRequest request,
        CancellationToken cancellationToken) => queries.ListRunWorkpiecesAsync(caller.Principal, request.RunId, cancellationToken);
    [SereinFlowMcpTool("sereinflow_get_run_workpiece", McpPermissionDto.DebugRead, McpPermissionDto.RunRead)]
    internal Task<FlowWorkpieceDto?> GetWorkpieceAsync(
        SereinFlowMcpCaller caller,
        McpRunWorkpieceQueryRequest request,
        CancellationToken cancellationToken) => queries.GetRunWorkpieceAsync(caller.Principal, request.RunId, request.WorkpieceId, cancellationToken);
    [SereinFlowMcpTool("sereinflow_list_runs", McpPermissionDto.DebugRead, McpPermissionDto.RunRead)]
    internal Task<AiPageDto<AiRunSummaryDto>> ListAsync(
        SereinFlowMcpCaller caller,
        McpListRunsRequest request,
        CancellationToken cancellationToken) => queries.ListRunsAsync(caller, request, cancellationToken);
}

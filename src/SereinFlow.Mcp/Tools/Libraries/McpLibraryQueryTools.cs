using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using SereinFlow.Mcp.Queries;
using static SereinFlow.Mcp.Support.McpReadOptions;

namespace SereinFlow.Mcp.Tools;

internal sealed class McpLibraryQueryTools(McpLibraryQueries queries)
{
    [SereinFlowMcpTool("sereinflow_list_libraries", SereinFlowPermission.LibraryRead)]
    internal Task<AiPageDto<AiLibrarySummaryDto>> ListAsync(
        SereinFlowMcpCaller caller,
        McpListLibrariesRequest request,
        CancellationToken cancellationToken) => queries.ReadLibrariesAsync(caller.Principal, cancellationToken, request.IncludeArchived ? McpLibraryQueries.LibraryListScope.All : McpLibraryQueries.LibraryListScope.Active, ReadOptions(request));
    [SereinFlowMcpTool("sereinflow_list_archived_libraries", SereinFlowPermission.LibraryRead)]
    internal Task<AiPageDto<AiLibrarySummaryDto>> ListArchivedAsync(
        SereinFlowMcpCaller caller,
        McpReadOptionsRequest request,
        CancellationToken cancellationToken) => queries.ReadLibrariesAsync(caller.Principal, cancellationToken, McpLibraryQueries.LibraryListScope.Archived, ReadOptions(request));
    [SereinFlowMcpTool("sereinflow_get_library", SereinFlowPermission.LibraryRead)]
    internal Task<AiLibrarySummaryDto?> GetAsync(
        SereinFlowMcpCaller caller,
        McpLibraryQueryRequest request,
        CancellationToken cancellationToken) => queries.ReadLibraryAsync(caller.Principal, request.LibraryId, cancellationToken);
    [SereinFlowMcpTool("sereinflow_list_library_families", SereinFlowPermission.LibraryRead)]
    internal Task<AiPageDto<LibraryFamilyDto>> ListFamiliesAsync(
        SereinFlowMcpCaller caller,
        McpListLibraryFamiliesRequest request,
        CancellationToken cancellationToken) => queries.ReadLibraryFamiliesAsync(caller.Principal, request.IncludeArchivedArtifacts, ReadOptions(request), cancellationToken);
    [SereinFlowMcpTool("sereinflow_get_library_family", SereinFlowPermission.LibraryRead)]
    internal Task<LibraryFamilyDto?> GetFamilyAsync(
        SereinFlowMcpCaller caller,
        McpLibraryFamilyQueryRequest request,
        CancellationToken cancellationToken) => queries.ReadLibraryFamilyAsync(caller.Principal, request.FamilyId, cancellationToken);
    [SereinFlowMcpTool("sereinflow_get_project_libraries", SereinFlowPermission.LibraryRead)]
    internal Task<AiPageDto<ProjectLibraryReferenceDto>> GetProjectLibrariesAsync(
        SereinFlowMcpCaller caller,
        McpProjectLibrariesRequest request,
        CancellationToken cancellationToken) => queries.ReadProjectLibrariesAsync(caller.Principal, request.ProjectId, ReadOptions(request), cancellationToken);
    [SereinFlowMcpTool("sereinflow_get_library_upgrade", SereinFlowPermission.LibraryRead)]
    internal Task<LibraryUpgradePlanDto> GetUpgradeAsync(
        SereinFlowMcpCaller caller,
        McpLibraryUpgradeQueryRequest request,
        CancellationToken cancellationToken) => queries.ReadLibraryUpgradeAsync(caller.Principal, request.ProjectId, request.UpgradeId, cancellationToken);
}

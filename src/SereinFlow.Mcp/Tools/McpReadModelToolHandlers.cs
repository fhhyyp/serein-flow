using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Mcp.Integration;
using static SereinFlow.Mcp.Tools.McpToolAuthorization;
using static SereinFlow.Mcp.Tools.McpToolSupport;
using static SereinFlow.Mcp.Tools.McpPreviewPayloadReaders;

namespace SereinFlow.Mcp.Tools;
/// <summary>
/// Bounded read-model tools and the read projections shared by MCP resources.
/// </summary>
internal static class McpReadModelToolHandlers
{
    [SereinFlowMcpTool("sereinflow_list_projects", McpPermissionDto.ProjectRead)]
    internal static Task<object> ListProjectsAsync(SereinFlowMcpToolContext context, McpReadOptionsRequest arguments, CancellationToken cancellationToken)
    {
        var readModelsResult = ReadModels(context);
        var readOptionsResult = ReadOptions(arguments);
        var readProjectsAsyncResult = ReadProjectsAsync(context.Scope, readModelsResult, context.Security, context.Principal, archivedOnly: false, cancellationToken, readOptionsResult);
        return readProjectsAsyncResult;
    }

    [SereinFlowMcpTool("sereinflow_list_archived_projects", McpPermissionDto.ProjectRead)]
    internal static Task<object> ListArchivedProjectsAsync(SereinFlowMcpToolContext context, McpReadOptionsRequest arguments, CancellationToken cancellationToken)
    {
        var readModelsResult1 = ReadModels(context);
        var readOptionsResult2 = ReadOptions(arguments);
        var readProjectsAsyncResult3 = ReadProjectsAsync(context.Scope, readModelsResult1, context.Security, context.Principal, archivedOnly: true, cancellationToken, readOptionsResult2);
        return readProjectsAsyncResult3;
    }

    [SereinFlowMcpTool("sereinflow_get_flow_topology", McpPermissionDto.ProjectRead)]
    internal static Task<object?> GetFlowTopologyAsync(SereinFlowMcpToolContext context, McpFlowTopologyRequest arguments, CancellationToken cancellationToken)
    {
        var projectId = arguments.ProjectId;
        var readModelsResult5 = ReadModels(context);
        var getGuidResult6 = arguments.FlowId;
        var readTrackResult = arguments.Track;
        var getOptionalLongResult = arguments.Version;
        var readAuthorizedOptionsResult = ReadAuthorizedOptions(arguments, context.Security, context.Principal, projectId);
        var readFlowTopologyAsyncResult = ReadFlowTopologyAsync(readModelsResult5, context.Security, context.Principal, projectId, getGuidResult6, readTrackResult, getOptionalLongResult, readAuthorizedOptionsResult, cancellationToken);
        return readFlowTopologyAsyncResult;
    }

    [SereinFlowMcpTool("sereinflow_list_libraries", McpPermissionDto.LibraryRead)]
    internal static Task<object> ListLibrariesAsync(SereinFlowMcpToolContext context, McpListLibrariesRequest arguments, CancellationToken cancellationToken)
    {
        var readModelsResult7 = ReadModels(context);
        McpReadModelToolHandlers.LibraryListScope evaluatedValue = arguments.IncludeArchived == true ? LibraryListScope.All : LibraryListScope.Active;
        var readOptionsResult8 = ReadOptions(arguments);
        var readLibrariesAsyncResult = ReadLibrariesAsync(context.Scope, readModelsResult7, context.Security, context.Principal, cancellationToken, evaluatedValue, readOptionsResult8);
        return readLibrariesAsyncResult;
    }

    [SereinFlowMcpTool("sereinflow_list_archived_libraries", McpPermissionDto.LibraryRead)]
    internal static Task<object> ListArchivedLibrariesAsync(SereinFlowMcpToolContext context, McpReadOptionsRequest arguments, CancellationToken cancellationToken)
    {
        var readModelsResult9 = ReadModels(context);
        var readOptionsResult10 = ReadOptions(arguments);
        var readLibrariesAsyncResult11 = ReadLibrariesAsync(context.Scope, readModelsResult9, context.Security, context.Principal, cancellationToken, LibraryListScope.Archived, readOptionsResult10);
        return readLibrariesAsyncResult11;
    }

    [SereinFlowMcpTool("sereinflow_get_library", McpPermissionDto.LibraryRead)]
    internal static Task<object?> GetLibraryAsync(SereinFlowMcpToolContext context, McpLibraryQueryRequest arguments, CancellationToken cancellationToken)
    {
        var readModelsResult12 = ReadModels(context);
        var getRequiredStringResult = arguments.LibraryId;
        var readLibraryAsyncResult = ReadLibraryAsync(context.Scope, readModelsResult12, context.Security, context.Principal, getRequiredStringResult, cancellationToken);
        return readLibraryAsyncResult;
    }

    [SereinFlowMcpTool("sereinflow_list_library_families", McpPermissionDto.LibraryRead)]
    internal static Task<object> ListLibraryFamiliesAsync(SereinFlowMcpToolContext context, McpListLibraryFamiliesRequest arguments, CancellationToken cancellationToken)
    {
        var evaluatedValue13 = arguments.IncludeArchivedArtifacts;
        var readOptionsResult14 = ReadOptions(arguments);
        var readLibraryFamiliesAsyncResult = ReadLibraryFamiliesAsync(context.Scope, context.Security, context.Principal, evaluatedValue13, readOptionsResult14, cancellationToken);
        return readLibraryFamiliesAsyncResult;
    }

    [SereinFlowMcpTool("sereinflow_get_library_family", McpPermissionDto.LibraryRead)]
    internal static Task<object?> GetLibraryFamilyAsync(SereinFlowMcpToolContext context, McpLibraryFamilyQueryRequest arguments, CancellationToken cancellationToken)
    {
        var getRequiredStringResult15 = arguments.FamilyId;
        var readLibraryFamilyAsyncResult = ReadLibraryFamilyAsync(context.Scope, context.Security, context.Principal, getRequiredStringResult15, cancellationToken);
        return readLibraryFamilyAsyncResult;
    }

    [SereinFlowMcpTool("sereinflow_get_project_libraries", McpPermissionDto.LibraryRead)]
    internal static Task<object> GetProjectLibrariesAsync(SereinFlowMcpToolContext context, McpProjectLibrariesRequest arguments, CancellationToken cancellationToken)
    {
        var getGuidResult16 = arguments.ProjectId;
        var readOptionsResult17 = ReadOptions(arguments);
        var readProjectLibrariesAsyncResult = ReadProjectLibrariesAsync(context.Scope, context.Security, context.Principal, getGuidResult16, readOptionsResult17, cancellationToken);
        return readProjectLibrariesAsyncResult;
    }

    [SereinFlowMcpTool("sereinflow_get_library_upgrade", McpPermissionDto.LibraryRead)]
    internal static Task<object> GetLibraryUpgradeAsync(SereinFlowMcpToolContext context, McpLibraryUpgradeQueryRequest arguments, CancellationToken cancellationToken)
    {
        var getGuidResult18 = arguments.ProjectId;
        var getGuidResult19 = arguments.UpgradeId;
        var readLibraryUpgradeAsyncResult = ReadLibraryUpgradeAsync(context.Scope, context.Security, context.Principal, getGuidResult18, getGuidResult19, cancellationToken);
        return readLibraryUpgradeAsyncResult;
    }

    [SereinFlowMcpTool("sereinflow_get_run_inspection", McpPermissionDto.RunRead)]
    internal static Task<object?> GetRunInspectionAsync(SereinFlowMcpToolContext context, McpRunInspectionRequest arguments, CancellationToken cancellationToken)
    {
        var readModelsResult20 = ReadModels(context);
        var getGuidResult21 = arguments.RunId;
        var readOptionalTrackResult = arguments.Track;
        var readOptionsResult22 = ReadOptions(arguments);
        var evaluatedValue23 = arguments.AfterEventSequence;
        var readRunAsyncResult = ReadRunAsync(context.Scope, readModelsResult20, context.Security, context.Principal, getGuidResult21, cancellationToken, readOptionalTrackResult, readOptionsResult22, evaluatedValue23);
        return readRunAsyncResult;
    }

    [SereinFlowMcpTool("sereinflow_list_run_workpieces", McpPermissionDto.DebugRead, McpPermissionDto.RunRead)]
    internal static async Task<object?> ListRunWorkpiecesAsync(SereinFlowMcpToolContext context, McpRunQueryRequest arguments, CancellationToken cancellationToken)
    {
        var runId = arguments.RunId;
        var getRequiredServiceResult = context.Services.GetRequiredService<IFlowRunStore>();
        var run = await getRequiredServiceResult.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        context.Security.RequireAny(context.Principal, run.ProjectId, McpPermissionDto.DebugRead, McpPermissionDto.RunRead);
        var getRequiredServiceResult24 = context.Services.GetRequiredService<IFlowWorkpieceStore>();
        var items = await getRequiredServiceResult24.ListAsync(runId, cancellationToken);
        var selectResult = items.Select(ToWorkpieceDto);
        var toArrayResult = selectResult.ToArray();
        return toArrayResult;
    }

    [SereinFlowMcpTool("sereinflow_get_run_workpiece", McpPermissionDto.DebugRead, McpPermissionDto.RunRead)]
    internal static async Task<object?> GetRunWorkpieceAsync(SereinFlowMcpToolContext context, McpRunWorkpieceQueryRequest arguments, CancellationToken cancellationToken)
    {
        var runId = arguments.RunId;
        var workpieceId = arguments.WorkpieceId;
        var getRequiredServiceResult25 = context.Services.GetRequiredService<IFlowRunStore>();
        var run = await getRequiredServiceResult25.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        context.Security.RequireAny(context.Principal, run.ProjectId, McpPermissionDto.DebugRead, McpPermissionDto.RunRead);
        var getRequiredServiceResult26 = context.Services.GetRequiredService<IFlowWorkpieceStore>();
        var item = await getRequiredServiceResult26.FindAsync(runId, workpieceId, cancellationToken);
        object? evaluatedValue27 = item is null ? null : ToWorkpieceDto(item);
        return evaluatedValue27;
    }

    [SereinFlowMcpTool("sereinflow_get_debug_state", McpPermissionDto.DebugRead, McpPermissionDto.RunRead)]
    internal static Task<object?> GetDebugStateAsync(SereinFlowMcpToolContext context, McpDebugStateRequest arguments, CancellationToken cancellationToken)
    {
        var readModelsResult28 = ReadModels(context);
        var getGuidResult29 = arguments.SessionId;
        var readDebugAsyncResult = ReadDebugAsync(context.Scope, readModelsResult28, context.Security, context.Principal, getGuidResult29, cancellationToken);
        return readDebugAsyncResult;
    }

    [SereinFlowMcpTool("sereinflow_wait_debug_state", McpPermissionDto.DebugRead, McpPermissionDto.RunRead)]
    internal static Task<AiDebugStateWaitResultDto?> WaitForDebugStateAsync(SereinFlowMcpToolContext context, McpDebugStateWaitRequest arguments, CancellationToken cancellationToken)
    {
        var readModelsResult30 = ReadModels(context);
        var waitForDebugStateAsyncResult = WaitForDebugStateAsync(context.Scope, readModelsResult30, context.Security, context.Principal, arguments, cancellationToken);
        return waitForDebugStateAsyncResult;
    }

    [SereinFlowMcpTool("sereinflow_get_flow_edit_model", McpPermissionDto.ProjectRead)]
    internal static Task<object?> GetFlowEditModelAsync(SereinFlowMcpToolContext context, McpFlowEditRequest arguments, CancellationToken cancellationToken)
    {
        var projectId = arguments.ProjectId;
        var readModelsResult31 = ReadModels(context);
        var getGuidResult32 = arguments.FlowId;
        var readAuthorizedOptionsResult33 = ReadAuthorizedOptions(arguments, context.Security, context.Principal, projectId);
        var readFlowEditModelAsyncResult = ReadFlowEditModelAsync(context.Scope, readModelsResult31, context.Security, context.Principal, projectId, getGuidResult32, readAuthorizedOptionsResult33, cancellationToken);
        return readFlowEditModelAsyncResult;
    }

    internal static Task<object> ReadProjectsResourceAsync(SereinFlowMcpToolContext context, CancellationToken cancellationToken)
    {
        var readModelsResult34 = ReadModels(context);
        var readProjectsAsyncResult35 = ReadProjectsAsync(context.Scope, readModelsResult34, context.Security, context.Principal, archivedOnly: false, cancellationToken);
        return readProjectsAsyncResult35;
    }

    internal static Task<object> ReadArchivedProjectsResourceAsync(SereinFlowMcpToolContext context, CancellationToken cancellationToken)
    {
        var readModelsResult36 = ReadModels(context);
        var readProjectsAsyncResult37 = ReadProjectsAsync(context.Scope, readModelsResult36, context.Security, context.Principal, archivedOnly: true, cancellationToken);
        return readProjectsAsyncResult37;
    }

    internal static Task<object?> ReadProjectResourceAsync(SereinFlowMcpToolContext context, Guid projectId, CancellationToken cancellationToken)
    {
        var readModelsResult38 = ReadModels(context);
        var readProjectAsyncResult39 = ReadProjectAsync(readModelsResult38, context.Security, context.Principal, projectId, cancellationToken);
        return readProjectAsyncResult39;
    }

    internal static Task<object?> ReadTopologyResourceAsync(SereinFlowMcpToolContext context, Guid projectId, Guid flowId, CancellationToken cancellationToken)
    {
        var readModelsResult40 = ReadModels(context);
        var readFlowTopologyAsyncResult41 = ReadFlowTopologyAsync(readModelsResult40, context.Security, context.Principal, projectId, flowId, FlowVersionTrackDto.Development, null, null, cancellationToken);
        return readFlowTopologyAsyncResult41;
    }

    internal static Task<object> ReadLibrariesResourceAsync(SereinFlowMcpToolContext context, CancellationToken cancellationToken)
    {
        var readModelsResult42 = ReadModels(context);
        var readLibrariesAsyncResult43 = ReadLibrariesAsync(context.Scope, readModelsResult42, context.Security, context.Principal, cancellationToken, LibraryListScope.Active);
        return readLibrariesAsyncResult43;
    }

    internal static Task<object> ReadArchivedLibrariesResourceAsync(SereinFlowMcpToolContext context, CancellationToken cancellationToken)
    {
        var readModelsResult44 = ReadModels(context);
        var readLibrariesAsyncResult45 = ReadLibrariesAsync(context.Scope, readModelsResult44, context.Security, context.Principal, cancellationToken, LibraryListScope.Archived);
        return readLibrariesAsyncResult45;
    }

    internal static Task<object?> ReadLibraryResourceAsync(SereinFlowMcpToolContext context, string libraryId, CancellationToken cancellationToken)
    {
        var readModelsResult46 = ReadModels(context);
        var readLibraryAsyncResult47 = ReadLibraryAsync(context.Scope, readModelsResult46, context.Security, context.Principal, libraryId, cancellationToken);
        return readLibraryAsyncResult47;
    }

    internal static Task<object> ReadLibraryFamiliesResourceAsync(SereinFlowMcpToolContext context, CancellationToken cancellationToken)
    {
        AiReadModelOptions evaluatedValue48 = new();
        var readLibraryFamiliesAsyncResult49 = ReadLibraryFamiliesAsync(context.Scope, context.Security, context.Principal, includeArchivedArtifacts: true, evaluatedValue48, cancellationToken);
        return readLibraryFamiliesAsyncResult49;
    }

    internal static Task<object?> ReadLibraryFamilyResourceAsync(SereinFlowMcpToolContext context, string familyId, CancellationToken cancellationToken)
    {
        var readLibraryFamilyAsyncResult50 = ReadLibraryFamilyAsync(context.Scope, context.Security, context.Principal, familyId, cancellationToken);
        return readLibraryFamilyAsyncResult50;
    }

    internal static Task<object> ReadProjectLibrariesResourceAsync(SereinFlowMcpToolContext context, Guid projectId, CancellationToken cancellationToken)
    {
        AiReadModelOptions evaluatedValue51 = new();
        var readProjectLibrariesAsyncResult52 = ReadProjectLibrariesAsync(context.Scope, context.Security, context.Principal, projectId, evaluatedValue51, cancellationToken);
        return readProjectLibrariesAsyncResult52;
    }

    internal static Task<object> ReadLibraryUpgradeResourceAsync(SereinFlowMcpToolContext context, Guid projectId, Guid upgradeId, CancellationToken cancellationToken)
    {
        var readLibraryUpgradeAsyncResult53 = ReadLibraryUpgradeAsync(context.Scope, context.Security, context.Principal, projectId, upgradeId, cancellationToken);
        return readLibraryUpgradeAsyncResult53;
    }

    internal static Task<object?> ReadRunResourceAsync(SereinFlowMcpToolContext context, Guid runId, CancellationToken cancellationToken)
    {
        var readModelsResult54 = ReadModels(context);
        var readRunAsyncResult55 = ReadRunAsync(context.Scope, readModelsResult54, context.Security, context.Principal, runId, cancellationToken);
        return readRunAsyncResult55;
    }

    internal static Task<object?> ReadRunWorkpiecesResourceAsync(SereinFlowMcpToolContext context, Guid runId, CancellationToken cancellationToken)
    {
        var listRunWorkpiecesAsyncResult = ListRunWorkpiecesAsync(context, new McpRunQueryRequest { RunId = runId }, cancellationToken);
        return listRunWorkpiecesAsyncResult;
    }

    internal static Task<object?> ReadRunWorkpieceResourceAsync(SereinFlowMcpToolContext context, Guid runId, string workpieceId, CancellationToken cancellationToken)
    {
        var getRunWorkpieceAsyncResult = GetRunWorkpieceAsync(context, new McpRunWorkpieceQueryRequest { RunId = runId, WorkpieceId = workpieceId.Trim() }, cancellationToken);
        return getRunWorkpieceAsyncResult;
    }

    internal static Task<object?> ReadDebugResourceAsync(SereinFlowMcpToolContext context, Guid sessionId, CancellationToken cancellationToken)
    {
        var readModelsResult57 = ReadModels(context);
        var readDebugAsyncResult58 = ReadDebugAsync(context.Scope, readModelsResult57, context.Security, context.Principal, sessionId, cancellationToken);
        return readDebugAsyncResult58;
    }

    internal static Task<object> ReadVersionsResourceAsync(SereinFlowMcpToolContext context, Guid projectId, Guid flowId, string track, CancellationToken cancellationToken)
    {
        var readVersionsAsyncResult = ReadVersionsAsync(context.Security, context.Principal, projectId, flowId, track, context.Scope, cancellationToken);
        return readVersionsAsyncResult;
    }

    internal static Task<object?> ReadVersionResourceAsync(SereinFlowMcpToolContext context, Guid projectId, Guid flowId, string track, long version, CancellationToken cancellationToken)
    {
        var readVersionAsyncResult = ReadVersionAsync(context.Security, context.Principal, projectId, flowId, track, version, context.Scope, cancellationToken);
        return readVersionAsyncResult;
    }

    internal static Task<object> ReadPreviewResourceAsync(SereinFlowMcpToolContext context, Guid previewId, CancellationToken cancellationToken)
    {
        var readPreviewAsyncResult = ReadPreviewAsync(context.Scope, context.Security, context.Principal, previewId, cancellationToken);
        return readPreviewAsyncResult;
    }

    private static AiReadModelService ReadModels(SereinFlowMcpToolContext context)
    {
        var getRequiredServiceResult59 = context.Services.GetRequiredService<AiReadModelService>();
        return getRequiredServiceResult59;
    }

    private static FlowWorkpieceDto ToWorkpieceDto(FlowWorkpieceRecord item)
    {
        var runWorkpieceResult = SereinFlowApiUris.RunWorkpiece(item.RunId, item.Id);
        FlowWorkpieceDto evaluatedValue60 = new(item.RunId, item.Id, item.Kind, item.Name, item.ContentType, item.Length, item.CreatedAt, runWorkpieceResult, item.NodeId, item.ExecutionId);
        return evaluatedValue60;
    }

    private static async Task<AiDebugStateWaitResultDto?> WaitForDebugStateAsync(IServiceScope scope, AiReadModelService service, McpSecurityService security, McpPrincipal? principal, McpDebugStateWaitRequest arguments, CancellationToken cancellationToken)
    {
        security.RequireAny(principal, null, McpPermissionDto.DebugRead, McpPermissionDto.RunRead);
        var afterRevision = arguments.AfterRevision;
        var timeoutSeconds = arguments.TimeoutSeconds;
        if (afterRevision < 0)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "MCP parameter 'afterRevision' cannot be negative.");
        }

        if (timeoutSeconds is < 0 or > 60)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "MCP parameter 'timeoutSeconds' must be between 0 and 60.");
        }

        var getGuidResult61 = arguments.SessionId;
        var fromSecondsResult = TimeSpan.FromSeconds(timeoutSeconds);
        var result = await service.WaitForDebugStateChangeAsync(getGuidResult61, afterRevision, fromSecondsResult, cancellationToken);
        if (result is not null)
        {
            security.RequireAny(principal, result.State.ProjectId, McpPermissionDto.DebugRead, McpPermissionDto.RunRead);
        }

        return result;
    }

    private static async Task<object> ReadProjectsAsync(IServiceScope scope, AiReadModelService service, McpSecurityService security, McpPrincipal? principal, bool archivedOnly, CancellationToken cancellationToken, AiReadModelOptions? options = null)
    {
        security.Require(principal, McpPermissionDto.ProjectRead);
        if (IsProjectScoped(principal))
        {
            var project = await service.GetProjectAsync(principal!.ProjectId!.Value, cancellationToken);
            var matchesRequestedState = project is not null && resolveEqualsResult() == archivedOnly;
            var aiPageDto = new AiPageDto<AiProjectSummaryDto>(AiReadModelContract.SchemaVersion, matchesRequestedState ? [project!] : [], false, null);
            return aiPageDto;
            bool resolveEqualsResult()
            {
                var toStringResult = ProjectStatus.Archived.ToString();
                var equalsResult = string.Equals(project.Status, toStringResult, StringComparison.OrdinalIgnoreCase);
                return equalsResult;
            }
        }

        var page = archivedOnly ? await service.ListArchivedProjectsAsync(options, cancellationToken) : await service.ListProjectsAsync(options, cancellationToken);
        return page;
    }

    private static async Task<object?> ReadProjectAsync(AiReadModelService service, McpSecurityService security, McpPrincipal? principal, Guid projectId, CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        var getProjectAsyncResult = await service.GetProjectAsync(projectId, cancellationToken);
        return getProjectAsyncResult;
    }

    private static async Task<object?> ReadFlowTopologyAsync(AiReadModelService service, McpSecurityService security, McpPrincipal? principal, Guid projectId, Guid flowId, FlowVersionTrackDto track, long? version, AiReadModelOptions? options, CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        RequireSensitiveRead(security, principal, projectId, options);
        var getFlowTopologyAsyncResult = await service.GetFlowTopologyAsync(projectId, flowId, track, version, options, cancellationToken);
        return getFlowTopologyAsyncResult;
    }

    private static async Task<object?> ReadFlowEditModelAsync(IServiceScope scope, AiReadModelService service, McpSecurityService security, McpPrincipal? principal, Guid projectId, Guid flowId, AiReadModelOptions options, CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        RequireSensitiveRead(security, principal, projectId, options);
        var getRequiredServiceResult62 = scope.ServiceProvider.GetRequiredService<IBuiltinNodeCatalog>();
        var getCatalogResult = getRequiredServiceResult62.GetCatalog();
        var getFlowEditModelAsyncResult = await service.GetFlowEditModelAsync(projectId, flowId, getCatalogResult, options, cancellationToken);
        return getFlowEditModelAsyncResult;
    }

    private static async Task<object> ReadLibrariesAsync(IServiceScope scope, AiReadModelService service, McpSecurityService security, McpPrincipal? principal, CancellationToken cancellationToken, LibraryListScope listScope = LibraryListScope.Active, AiReadModelOptions? options = null)
    {
        security.Require(principal, McpPermissionDto.LibraryRead);
        if (!IsProjectScoped(principal))
        {
            object evaluatedValue63 = listScope == LibraryListScope.Archived ? await service.ListArchivedLibrariesAsync(options, cancellationToken) : await service.ListLibrariesAsync(listScope == LibraryListScope.All, options, cancellationToken);
            return evaluatedValue63;
        }

        var getRequiredServiceResult65 = scope.ServiceProvider.GetRequiredService<IProjectLibraryReferenceRepository>();
        var references = await getRequiredServiceResult65.ListByProjectAsync(principal!.ProjectId!.Value, cancellationToken);
        var evaluatedValue66 = (options ?? new());
        var maxItems = evaluatedValue66.Normalize().MaxItems;
        var items = new List<AiLibrarySummaryDto>(maxItems + 1);
        var selectResult67 = references.Select(static item => item.LibraryId);
        var whereResult = selectResult67.Where(static id =>
        {
            var evaluatedValue64 = !string.IsNullOrWhiteSpace(id);
            return evaluatedValue64;
        });
        var distinctResult = whereResult.Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var libraryId in distinctResult.OrderBy(static id => id, StringComparer.Ordinal))
        {
            var library = await service.GetLibraryAsync(libraryId, cancellationToken);
            if (library is null || !MatchesScope(library, listScope))
            {
                continue;
            }

            items.Add(library with { Nodes = null });
            if (items.Count > maxItems)
            {
                break;
            }
        }

        var hasMore = items.Count > maxItems;
        var visible = hasMore ? resolveToArrayResult() : items.ToArray();
        var aiPageDto69 = new AiPageDto<AiLibrarySummaryDto>(AiReadModelContract.SchemaVersion, visible, hasMore, hasMore && visible.Length > 0 ? visible[^1].Id : null);
        return aiPageDto69;
        AiLibrarySummaryDto[] resolveToArrayResult()
        {
            var toArrayResult68 = items
                .Take(maxItems)
                .ToArray();
            return toArrayResult68;
        }
    }

    private static bool MatchesScope(AiLibrarySummaryDto library, LibraryListScope listScope)
    {
        bool evaluatedValue76 = listScope switch
        {
            LibraryListScope.Active => resolveEqualsResult70(),
            LibraryListScope.Archived => resolveEqualsResult73(),
            _ => true,
        };
        return evaluatedValue76;
        bool resolveEqualsResult70()
        {
            var toStringResult71 = LibraryLifecycleDto.Available.ToString();
            var equalsResult72 = string.Equals(library.Lifecycle, toStringResult71, StringComparison.OrdinalIgnoreCase);
            return equalsResult72;
        }

        bool resolveEqualsResult73()
        {
            var toStringResult74 = LibraryLifecycleDto.Archived.ToString();
            var equalsResult75 = string.Equals(library.Lifecycle, toStringResult74, StringComparison.OrdinalIgnoreCase);
            return equalsResult75;
        }
    }

    private enum LibraryListScope
    {
        Active,
        Archived,
        All,
    }

    private static async Task<object?> ReadLibraryAsync(IServiceScope scope, AiReadModelService service, McpSecurityService security, McpPrincipal? principal, string libraryId, CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.LibraryRead);
        var library = await service.GetLibraryAsync(libraryId, cancellationToken);
        if (library is not null && IsProjectScoped(principal))
        {
            var getRequiredServiceResult77 = scope.ServiceProvider.GetRequiredService<IProjectLibraryReferenceRepository>();
            var referenced = await getRequiredServiceResult77.IsReferencedAsync(principal!.ProjectId!.Value, library.Id, cancellationToken);
            if (!referenced)
            {
                throw new McpSecurityException(McpErrorCodes.LibraryAccessDenied, "The MCP caller cannot access this library artifact.", StatusCodes.Status403Forbidden);
            }
        }

        return library;
    }

    private static async Task<object> ReadLibraryFamiliesAsync(IServiceScope scope, McpSecurityService security, McpPrincipal? principal, bool includeArchivedArtifacts, AiReadModelOptions options, CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.LibraryRead);
        var normalized = options.Normalize();
        var getRequiredServiceResult78 = scope.ServiceProvider.GetRequiredService<ILibraryCatalogService>();
        var families = await getRequiredServiceResult78.ListFamiliesAsync(includeArchivedArtifacts, cancellationToken);
        var evaluatedValue79 = (IsProjectScoped(principal) ? await ProjectVisibleFamiliesAsync(scope, principal!.ProjectId!.Value, families, cancellationToken) : families);
        var takeResult80 = evaluatedValue79.Take(normalized.MaxItems + 1);
        var items = takeResult80.ToArray();
        var hasMore = items.Length > normalized.MaxItems;
        var visible = hasMore ? resolveToArrayResult81() : items;
        var aiPageDto84 = new AiPageDto<LibraryFamilyDto>(AiReadModelContract.SchemaVersion, visible, hasMore, hasMore && visible.Length > 0 ? visible[^1].Id : null);
        return aiPageDto84;
        LibraryFamilyDto[] resolveToArrayResult81()
        {
            var takeResult82 = items.Take(normalized.MaxItems);
            var toArrayResult83 = takeResult82.ToArray();
            return toArrayResult83;
        }
    }

    private static async Task<object?> ReadLibraryFamilyAsync(IServiceScope scope, McpSecurityService security, McpPrincipal? principal, string familyId, CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.LibraryRead);
        var getRequiredServiceResult86 = scope.ServiceProvider.GetRequiredService<ILibraryCatalogService>();
        var families = await getRequiredServiceResult86.ListFamiliesAsync(includeArchivedArtifacts: true, cancellationToken: cancellationToken);
        var visible = IsProjectScoped(principal) ? await ProjectVisibleFamiliesAsync(scope, principal!.ProjectId!.Value, families, cancellationToken) : families;
        var singleOrDefaultResult = visible.SingleOrDefault(item =>
        {
            var equalsResult85 = string.Equals(item.Id, familyId, StringComparison.OrdinalIgnoreCase);
            return equalsResult85;
        });
        return singleOrDefaultResult;
    }

    private static async Task<IReadOnlyList<LibraryFamilyDto>> ProjectVisibleFamiliesAsync(IServiceScope scope, Guid projectId, IReadOnlyList<LibraryFamilyDto> families, CancellationToken cancellationToken)
    {
        var getRequiredServiceResult88 = scope.ServiceProvider.GetRequiredService<IProjectLibraryReferenceRepository>();
        var references = await getRequiredServiceResult88.ListByProjectAsync(projectId, cancellationToken);
        var selectResult89 = references.Select(static reference => reference.LibraryId);
        var whereResult90 = selectResult89.Where(static id =>
        {
            var evaluatedValue87 = !string.IsNullOrWhiteSpace(id);
            return evaluatedValue87;
        });
        var referencedArtifactIds = whereResult90.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toArrayResult94 = families
            .Select(family =>
            {
                var projectVisibleFamilyResult = ProjectVisibleFamily(family, referencedArtifactIds);
                return projectVisibleFamilyResult;
            })
            .Where(static family => family is not null)
            .Select(static family => family!)
            .ToArray();
        return toArrayResult94;
    }

    private static LibraryFamilyDto? ProjectVisibleFamily(LibraryFamilyDto family, HashSet<string> referencedArtifactIds)
    {
        var artifacts = (family.Artifacts ?? [])
            .Where(artifact =>
            {
                var containsResult = referencedArtifactIds.Contains(artifact.Id);
                return containsResult;
            })
            .OrderByDescending(artifact =>
            {
                var parseSemanticVersionResult = ParseSemanticVersion(artifact.SemanticVersion ?? artifact.Version);
                return parseSemanticVersionResult;
            })
            .ThenByDescending(static artifact => artifact.UploadedAt)
            .ThenBy(static artifact => artifact.Id, StringComparer.Ordinal)
            .ToArray();
        if (artifacts.Length == 0)
        {
            return null;
        }

        var latestArtifactId = artifacts.FirstOrDefault(static artifact => artifact.Lifecycle == LibraryLifecycleDto.Available)?.Id;
        return family with
        {
            LatestArtifactId = latestArtifactId,
            Artifacts = artifacts,
        };
    }

    private static Version ParseSemanticVersion(string value)
    {
        var optionalReceiver = value;
        string? optionalResult = default(string);
        if (optionalReceiver is not null)
        {
            var trimResult = optionalReceiver.Trim();
            optionalResult = trimResult.TrimStart('v', 'V');
        }

        var evaluatedValue1 = optionalResult;
        Version conditionalValue;
        if (Version.TryParse(evaluatedValue1, out var version))
        {
            conditionalValue = version;
        }
        else
        {
            conditionalValue = new Version(0, 0);
        }

        Version evaluatedValue96 = conditionalValue;
        return evaluatedValue96;
    }

    private static async Task<object> ReadProjectLibrariesAsync(IServiceScope scope, McpSecurityService security, McpPrincipal? principal, Guid projectId, AiReadModelOptions options, CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.LibraryRead, projectId);
        var getRequiredServiceResult97 = scope.ServiceProvider.GetRequiredService<ProjectLibraryService>();
        var result = await getRequiredServiceResult97.ListAsync(projectId, cancellationToken);
        if (!result.IsSuccess)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, result.Message ?? "The project was not found.", new { code = result.Code });
        }

        var normalized = options.Normalize();
        var orderByResult = (result.References ?? []).OrderBy(static reference => reference.LibraryId, StringComparer.Ordinal);
        var takeResult98 = orderByResult.Take(normalized.MaxItems + 1);
        var items = takeResult98.ToArray();
        var hasMore = items.Length > normalized.MaxItems;
        var visible = hasMore ? resolveToArrayResult99() : items;
        var aiPageDto102 = new AiPageDto<ProjectLibraryReferenceDto>(AiReadModelContract.SchemaVersion, visible, hasMore, hasMore && visible.Length > 0 ? visible[^1].LibraryId : null);
        return aiPageDto102;
        ProjectLibraryReferenceDto[] resolveToArrayResult99()
        {
            var takeResult100 = items.Take(normalized.MaxItems);
            var toArrayResult101 = takeResult100.ToArray();
            return toArrayResult101;
        }
    }

    private static async Task<object> ReadLibraryUpgradeAsync(IServiceScope scope, McpSecurityService security, McpPrincipal? principal, Guid projectId, Guid upgradeId, CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.LibraryRead, projectId);
        var getRequiredServiceResult103 = scope.ServiceProvider.GetRequiredService<LibraryUpgradeService>();
        var result = await getRequiredServiceResult103.GetPlanAsync(projectId, upgradeId, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, result.Message ?? "The library upgrade preview was not found.", new { code = result.Code });
        }

        return result.Value;
    }

    private static async Task<object?> ReadRunAsync(IServiceScope scope, AiReadModelService service, McpSecurityService security, McpPrincipal? principal, Guid runId, CancellationToken cancellationToken, FlowVersionTrackDto? definitionTrack = null, AiReadModelOptions? options = null, long afterEventSequence = 0)
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

    private static async Task<object?> ReadDebugAsync(IServiceScope scope, AiReadModelService service, McpSecurityService security, McpPrincipal? principal, Guid sessionId, CancellationToken cancellationToken)
    {
        security.RequireAny(principal, null, McpPermissionDto.DebugRead, McpPermissionDto.RunRead);
        var state = await service.GetDebugStateAsync(sessionId, cancellationToken);
        if (state is not null)
        {
            security.RequireAny(principal, state.ProjectId, McpPermissionDto.DebugRead, McpPermissionDto.RunRead);
        }

        return state;
    }

    private static async Task<object> ReadVersionsAsync(McpSecurityService security, McpPrincipal? principal, Guid projectId, Guid flowId, string trackText, IServiceScope scope, CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        var track = ParseTrack(trackText);
        var service = scope.ServiceProvider.GetRequiredService<AiReadModelService>();
        var getFlowVersionHistoryAsyncResult = await service.GetFlowVersionHistoryAsync(projectId, flowId, track, cancellationToken);
        return getFlowVersionHistoryAsyncResult;
    }

    private static async Task<object?> ReadVersionAsync(McpSecurityService security, McpPrincipal? principal, Guid projectId, Guid flowId, string trackText, long version, IServiceScope scope, CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        var track = ParseTrack(trackText);
        var getRequiredServiceResult104 = scope.ServiceProvider.GetRequiredService<AiReadModelService>();
        var detail = await getRequiredServiceResult104.GetFlowVersionAsync(projectId, flowId, version, cancellationToken);
        if (detail is null || detail.Version.Track != track)
        {
            return null;
        }

        var evaluatedValue105 = detail with
        {
            Definition = FlowDiffService.RedactSensitive(detail.Definition)
        };
        return evaluatedValue105;
    }

    private static async Task<object> ReadPreviewAsync(IServiceScope scope, McpSecurityService security, McpPrincipal? principal, Guid previewId, CancellationToken cancellationToken)
    {
        var entry = await resolveFindAsyncResult() ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The MCP preview was not found.");
        if (principal is not null && !principal.IsLocal && !principal.IsAdministrator && !string.Equals(entry.PrincipalId, principal.Id, StringComparison.Ordinal))
        {
            throw new McpSecurityException(McpErrorCodes.PreviewOwnerMismatch, "The MCP preview belongs to another caller.", StatusCodes.Status403Forbidden);
        }

        if (entry.ProjectId is null && principal is not null && !principal.IsLocal && !principal.IsAdministrator)
        {
            throw new McpSecurityException(McpErrorCodes.PreviewAccessDenied, "The MCP caller cannot access this global preview.", StatusCodes.Status403Forbidden);
        }

        if (entry.ProjectId is not null && principal is not null && !principal.CanAccess(entry.ProjectId.Value))
        {
            throw new McpSecurityException(McpErrorCodes.ProjectAccessDenied, "The MCP caller cannot access this preview.", StatusCodes.Status403Forbidden);
        }

        RequirePreviewPermission(security, principal, entry);
        var descriptor = new McpPreviewDescriptorDto(entry.Id, entry.Operation, entry.ProjectId, entry.FlowId, entry.Status, entry.ExpiresAt, entry.PreviewFingerprint);
        object evaluatedValue107 = entry.Operation switch
        {
            ProjectErrorCodes.Create => McpProjectToolHandlers.ReadProjectCreatePreview(entry, descriptor),
            FlowErrorCodes.Patch => ReadFlowPatchPreview(entry, descriptor),
            "flow.publish" => ReadPublishPreview(entry, descriptor),
            "flow.rollback" => ReadRollbackPreview(entry, descriptor),
            LibraryErrorCodes.Package => ReadLibraryPackagePreview(entry, descriptor),
            ProjectErrorCodes.LibraryAttach => ReadProjectLibraryAttachPreview(entry, descriptor),
            LibraryErrorCodes.FamilyAssign => ReadLibraryFamilyAssignmentPreview(entry, descriptor),
            "library.upgrade" => ReadLibraryUpgradePreview(entry, descriptor),
            _ => descriptor,
        };
        return evaluatedValue107;
        Task<McpPreviewEntry?> resolveFindAsyncResult()
        {
            var getRequiredServiceResult106 = scope.ServiceProvider.GetRequiredService<IMcpPreviewStore>();
            var findAsyncResult = getRequiredServiceResult106.FindAsync(previewId, cancellationToken);
            return findAsyncResult;
        }
    }

    private static FlowPatchPreviewDto ReadFlowPatchPreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredFlowPatchPreview>(entry);
        var toFlowPatchPreviewResult = ToFlowPatchPreview(entry, descriptor, stored);
        return toFlowPatchPreviewResult;
    }

    internal static FlowPatchPreviewDto ToFlowPatchPreview(McpPreviewEntry preview, StoredFlowPatchPreview stored)
    {
        var isPreviewPendingResult = IsPreviewPending(preview);
        var toFlowPatchPreviewResult108 = ToFlowPatchPreview(preview.Id, preview.ExpiresAt, preview.PreviewFingerprint, isPreviewPendingResult, stored);
        return toFlowPatchPreviewResult108;
    }

    private static FlowPatchPreviewDto ToFlowPatchPreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor, StoredFlowPatchPreview stored)
    {
        var isPreviewPendingResult109 = IsPreviewPending(entry);
        var toFlowPatchPreviewResult110 = ToFlowPatchPreview(descriptor.PreviewId, descriptor.ExpiresAt, descriptor.PreviewFingerprint, isPreviewPendingResult109, stored);
        return toFlowPatchPreviewResult110;
    }

    private static FlowPatchPreviewDto ToFlowPatchPreview(Guid previewId, DateTimeOffset expiresAt, string previewFingerprint, bool isPending, StoredFlowPatchPreview stored)
    {
        var normalized = stored.CanonicalRequest is null ? resolveNormalizeLegacyRequestResult() : new NormalizedFlowPatchRequest(stored.CanonicalRequest, stored.Request, stored.NormalizationWarnings ?? []);
        var redactSensitiveResult = FlowDiffService.RedactSensitive(stored.Diff);
        var redactSensitiveResult111 = FlowDiffService.RedactSensitive(stored.CandidateDefinition);
        var flowPatchPreviewDto = new FlowPatchPreviewDto(previewId, stored.Request.ProjectId, stored.Request.FlowId, stored.Request.ExpectedDevelopmentVersion, expiresAt, previewFingerprint, isPending && stored.Validation.IsValid && stored.Diff.Changes.Count > 0, stored.Validation, redactSensitiveResult, redactSensitiveResult111, SchemaVersion: FlowPatchContract.CurrentSchemaVersion, EnumEncoding: FlowPatchContract.EnumEncoding, NormalizedOperations: normalized.Request.Operations, NormalizationWarnings: normalized.Warnings);
        return flowPatchPreviewDto;
        NormalizedFlowPatchRequest resolveNormalizeLegacyRequestResult()
        {
            var flowPatchContractNormalizer = new FlowPatchContractNormalizer();
            var normalizeLegacyRequestResult = flowPatchContractNormalizer.NormalizeLegacyRequest(stored.Request);
            return normalizeLegacyRequestResult;
        }
    }
}

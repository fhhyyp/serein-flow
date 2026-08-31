using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using static SereinFlow.Mcp.McpPreviewPayloadReaders;
using static SereinFlow.Mcp.McpToolAuthorization;
using static SereinFlow.Mcp.McpToolSupport;

namespace SereinFlow.Mcp;

/// <summary>
/// Bounded read-model tools and the read projections shared by MCP resources.
/// </summary>
internal static class McpReadModelToolHandlers
{
    internal static Task<object> ListProjectsAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => ReadProjectsAsync(
            context.Scope, ReadModels(context), context.Security, context.Principal,
            archivedOnly: false, cancellationToken, ReadOptions(arguments));

    internal static Task<object> ListArchivedProjectsAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => ReadProjectsAsync(
            context.Scope, ReadModels(context), context.Security, context.Principal,
            archivedOnly: true, cancellationToken, ReadOptions(arguments));

    internal static Task<object?> GetProjectAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => ReadProjectAsync(ReadModels(context), context.Security, context.Principal, GetGuid(arguments, "projectId"), cancellationToken);

    internal static Task<object?> GetFlowTopologyAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var projectId = GetGuid(arguments, "projectId");
        return ReadFlowTopologyAsync(
            ReadModels(context), context.Security, context.Principal, projectId, GetGuid(arguments, "flowId"),
            ReadTrack(arguments), GetOptionalLong(arguments, "version"),
            ReadAuthorizedOptions(arguments, context.Security, context.Principal, projectId), cancellationToken);
    }

    internal static Task<object> ListLibrariesAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => ReadLibrariesAsync(
            context.Scope, ReadModels(context), context.Security, context.Principal, cancellationToken,
            GetOptionalBool(arguments, "includeArchived") == true ? LibraryListScope.All : LibraryListScope.Active,
            ReadOptions(arguments));

    internal static Task<object> ListArchivedLibrariesAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => ReadLibrariesAsync(
            context.Scope, ReadModels(context), context.Security, context.Principal, cancellationToken,
            LibraryListScope.Archived, ReadOptions(arguments));

    internal static Task<object?> GetLibraryAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => ReadLibraryAsync(context.Scope, ReadModels(context), context.Security, context.Principal, GetRequiredString(arguments, "libraryId"), cancellationToken);

    internal static Task<object> ListLibraryFamiliesAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => ReadLibraryFamiliesAsync(
            context.Scope,
            context.Security,
            context.Principal,
            GetOptionalBool(arguments, "includeArchivedArtifacts") ?? true,
            ReadOptions(arguments),
            cancellationToken);

    internal static Task<object?> GetLibraryFamilyAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => ReadLibraryFamilyAsync(
            context.Scope,
            context.Security,
            context.Principal,
            GetRequiredString(arguments, "familyId"),
            cancellationToken);

    internal static Task<object> GetProjectLibrariesAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => ReadProjectLibrariesAsync(
            context.Scope,
            context.Security,
            context.Principal,
            GetGuid(arguments, "projectId"),
            ReadOptions(arguments),
            cancellationToken);

    internal static Task<object> GetLibraryUpgradeAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => ReadLibraryUpgradeAsync(
            context.Scope,
            context.Security,
            context.Principal,
            GetGuid(arguments, "projectId"),
            GetGuid(arguments, "upgradeId"),
            cancellationToken);

    internal static Task<object?> GetRunInspectionAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => ReadRunAsync(
            context.Scope, ReadModels(context), context.Security, context.Principal, GetGuid(arguments, "runId"),
            cancellationToken, ReadOptionalTrack(arguments), ReadOptions(arguments), GetOptionalLong(arguments, "afterEventSequence") ?? 0);

    internal static Task<object?> GetDebugStateAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => ReadDebugAsync(context.Scope, ReadModels(context), context.Security, context.Principal, GetGuid(arguments, "sessionId"), cancellationToken);

    internal static Task<AiDebugStateWaitResultDto?> WaitForDebugStateAsync(
        McpToolContext context,
        JsonElement arguments,
        CancellationToken cancellationToken)
        => WaitForDebugStateAsync(context.Scope, ReadModels(context), context.Security, context.Principal, arguments, cancellationToken);

    internal static Task<object?> GetFlowEditModelAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var projectId = GetGuid(arguments, "projectId");
        return ReadFlowEditModelAsync(
            context.Scope, ReadModels(context), context.Security, context.Principal,
            projectId, GetGuid(arguments, "flowId"),
            ReadAuthorizedOptions(arguments, context.Security, context.Principal, projectId), cancellationToken);
    }

    internal static Task<object> ReadProjectsResourceAsync(McpToolContext context, CancellationToken cancellationToken)
        => ReadProjectsAsync(
            context.Scope, ReadModels(context), context.Security, context.Principal,
            archivedOnly: false, cancellationToken);

    internal static Task<object> ReadArchivedProjectsResourceAsync(McpToolContext context, CancellationToken cancellationToken)
        => ReadProjectsAsync(
            context.Scope, ReadModels(context), context.Security, context.Principal,
            archivedOnly: true, cancellationToken);

    internal static Task<object?> ReadProjectResourceAsync(McpToolContext context, Guid projectId, CancellationToken cancellationToken)
        => ReadProjectAsync(ReadModels(context), context.Security, context.Principal, projectId, cancellationToken);

    internal static Task<object?> ReadTopologyResourceAsync(McpToolContext context, Guid projectId, Guid flowId, CancellationToken cancellationToken)
        => ReadFlowTopologyAsync(ReadModels(context), context.Security, context.Principal, projectId, flowId, FlowVersionTrackDto.Development, null, null, cancellationToken);

    internal static Task<object> ReadLibrariesResourceAsync(McpToolContext context, CancellationToken cancellationToken)
        => ReadLibrariesAsync(
            context.Scope, ReadModels(context), context.Security, context.Principal,
            cancellationToken, LibraryListScope.Active);

    internal static Task<object> ReadArchivedLibrariesResourceAsync(McpToolContext context, CancellationToken cancellationToken)
        => ReadLibrariesAsync(
            context.Scope, ReadModels(context), context.Security, context.Principal,
            cancellationToken, LibraryListScope.Archived);

    internal static Task<object?> ReadLibraryResourceAsync(McpToolContext context, string libraryId, CancellationToken cancellationToken)
        => ReadLibraryAsync(context.Scope, ReadModels(context), context.Security, context.Principal, libraryId, cancellationToken);

    internal static Task<object> ReadLibraryFamiliesResourceAsync(McpToolContext context, CancellationToken cancellationToken)
        => ReadLibraryFamiliesAsync(context.Scope, context.Security, context.Principal, includeArchivedArtifacts: true, new(), cancellationToken);

    internal static Task<object?> ReadLibraryFamilyResourceAsync(McpToolContext context, string familyId, CancellationToken cancellationToken)
        => ReadLibraryFamilyAsync(context.Scope, context.Security, context.Principal, familyId, cancellationToken);

    internal static Task<object> ReadProjectLibrariesResourceAsync(McpToolContext context, Guid projectId, CancellationToken cancellationToken)
        => ReadProjectLibrariesAsync(context.Scope, context.Security, context.Principal, projectId, new(), cancellationToken);

    internal static Task<object> ReadLibraryUpgradeResourceAsync(
        McpToolContext context,
        Guid projectId,
        Guid upgradeId,
        CancellationToken cancellationToken)
        => ReadLibraryUpgradeAsync(context.Scope, context.Security, context.Principal, projectId, upgradeId, cancellationToken);

    internal static Task<object?> ReadRunResourceAsync(McpToolContext context, Guid runId, CancellationToken cancellationToken)
        => ReadRunAsync(context.Scope, ReadModels(context), context.Security, context.Principal, runId, cancellationToken);

    internal static Task<object?> ReadDebugResourceAsync(McpToolContext context, Guid sessionId, CancellationToken cancellationToken)
        => ReadDebugAsync(context.Scope, ReadModels(context), context.Security, context.Principal, sessionId, cancellationToken);

    internal static Task<object> ReadVersionsResourceAsync(McpToolContext context, Guid projectId, Guid flowId, string track, CancellationToken cancellationToken)
        => ReadVersionsAsync(context.Security, context.Principal, projectId, flowId, track, context.Scope, cancellationToken);

    internal static Task<object?> ReadVersionResourceAsync(McpToolContext context, Guid projectId, Guid flowId, string track, long version, CancellationToken cancellationToken)
        => ReadVersionAsync(context.Security, context.Principal, projectId, flowId, track, version, context.Scope, cancellationToken);

    internal static Task<object> ReadPreviewResourceAsync(McpToolContext context, Guid previewId, CancellationToken cancellationToken)
        => ReadPreviewAsync(context.Scope, context.Security, context.Principal, previewId, cancellationToken);

    private static AiReadModelService ReadModels(McpToolContext context)
        => context.Services.GetRequiredService<AiReadModelService>();

    private static async Task<AiDebugStateWaitResultDto?> WaitForDebugStateAsync(
        IServiceScope scope,
        AiReadModelService service,
        McpSecurityService security,
        McpPrincipal? principal,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var afterRevision = GetOptionalLong(arguments, "afterRevision")
            ?? throw new McpProtocolException(-32602, "MCP parameter 'afterRevision' is required.");
        var timeoutSeconds = GetOptionalInt(arguments, "timeoutSeconds") ?? 15;
        if (afterRevision < 0)
            throw new McpProtocolException(-32602, "MCP parameter 'afterRevision' cannot be negative.");
        if (timeoutSeconds is < 0 or > 60)
            throw new McpProtocolException(-32602, "MCP parameter 'timeoutSeconds' must be between 0 and 60.");

        var result = await service.WaitForDebugStateChangeAsync(
            GetGuid(arguments, "sessionId"),
            afterRevision,
            TimeSpan.FromSeconds(timeoutSeconds),
            cancellationToken);
        if (result is not null)
            security.Require(principal, McpPermissionDto.DebugRead, result.State.ProjectId);
        return result;
    }

    private static async Task<object> ReadProjectsAsync(
        IServiceScope scope,
        AiReadModelService service,
        McpSecurityService security,
        McpPrincipal? principal,
        bool archivedOnly,
        CancellationToken cancellationToken,
        AiReadModelOptions? options = null)
    {
        security.Require(principal, McpPermissionDto.ProjectRead);
        if (IsProjectScoped(principal))
        {
            var project = await service.GetProjectAsync(principal!.ProjectId!.Value, cancellationToken);
            var matchesRequestedState = project is not null
                && string.Equals(
                    project.Status,
                    ProjectStatus.Archived.ToString(),
                    StringComparison.OrdinalIgnoreCase) == archivedOnly;
            return new AiPageDto<AiProjectSummaryDto>(
                AiReadModelContract.SchemaVersion,
                matchesRequestedState ? [project!] : [],
                false,
                null);
        }

        var page = archivedOnly
            ? await service.ListArchivedProjectsAsync(options, cancellationToken)
            : await service.ListProjectsAsync(options, cancellationToken);
        return page;
    }

    private static async Task<object?> ReadProjectAsync(
        AiReadModelService service,
        McpSecurityService security,
        McpPrincipal? principal,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        return await service.GetProjectAsync(projectId, cancellationToken);
    }

    private static async Task<object?> ReadFlowTopologyAsync(
        AiReadModelService service,
        McpSecurityService security,
        McpPrincipal? principal,
        Guid projectId,
        Guid flowId,
        FlowVersionTrackDto track,
        long? version,
        AiReadModelOptions? options,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        RequireSensitiveRead(security, principal, projectId, options);
        return await service.GetFlowTopologyAsync(projectId, flowId, track, version, options, cancellationToken);
    }

    private static async Task<object?> ReadFlowEditModelAsync(
        IServiceScope scope,
        AiReadModelService service,
        McpSecurityService security,
        McpPrincipal? principal,
        Guid projectId,
        Guid flowId,
        AiReadModelOptions options,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        RequireSensitiveRead(security, principal, projectId, options);
        return await service.GetFlowEditModelAsync(
            projectId,
            flowId,
            scope.ServiceProvider.GetRequiredService<IBuiltinNodeCatalog>().GetCatalog(),
            options,
            cancellationToken);
    }

    private static async Task<object> ReadLibrariesAsync(
        IServiceScope scope,
        AiReadModelService service,
        McpSecurityService security,
        McpPrincipal? principal,
        CancellationToken cancellationToken,
        LibraryListScope listScope = LibraryListScope.Active,
        AiReadModelOptions? options = null)
    {
        security.Require(principal, McpPermissionDto.LibraryRead);
        if (!IsProjectScoped(principal))
        {
            return listScope == LibraryListScope.Archived
                ? await service.ListArchivedLibrariesAsync(options, cancellationToken)
                : await service.ListLibrariesAsync(listScope == LibraryListScope.All, options, cancellationToken);
        }

        var references = await scope.ServiceProvider.GetRequiredService<IProjectLibraryReferenceRepository>()
            .ListByProjectAsync(principal!.ProjectId!.Value, cancellationToken);
        var maxItems = (options ?? new()).Normalize().MaxItems;
        var items = new List<AiLibrarySummaryDto>(maxItems + 1);
        foreach (var libraryId in references
            .Select(static item => item.LibraryId)
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static id => id, StringComparer.Ordinal))
        {
            var library = await service.GetLibraryAsync(libraryId, cancellationToken);
            if (library is null || !MatchesScope(library, listScope))
                continue;

            items.Add(library with { Nodes = null });
            if (items.Count > maxItems)
                break;
        }

        var hasMore = items.Count > maxItems;
        var visible = hasMore ? items.Take(maxItems).ToArray() : items.ToArray();
        return new AiPageDto<AiLibrarySummaryDto>(
            AiReadModelContract.SchemaVersion,
            visible,
            hasMore,
            hasMore && visible.Length > 0 ? visible[^1].Id : null);
    }

    private static bool MatchesScope(AiLibrarySummaryDto library, LibraryListScope listScope)
        => listScope switch
        {
            LibraryListScope.Active => string.Equals(
                library.Lifecycle,
                LibraryLifecycleDto.Available.ToString(),
                StringComparison.OrdinalIgnoreCase),
            LibraryListScope.Archived => string.Equals(
                library.Lifecycle,
                LibraryLifecycleDto.Archived.ToString(),
                StringComparison.OrdinalIgnoreCase),
            _ => true,
        };

    private enum LibraryListScope
    {
        Active,
        Archived,
        All,
    }

    private static async Task<object?> ReadLibraryAsync(
        IServiceScope scope,
        AiReadModelService service,
        McpSecurityService security,
        McpPrincipal? principal,
        string libraryId,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.LibraryRead);
        var library = await service.GetLibraryAsync(libraryId, cancellationToken);
        if (library is not null && IsProjectScoped(principal))
        {
            var referenced = await scope.ServiceProvider.GetRequiredService<IProjectLibraryReferenceRepository>()
                .IsReferencedAsync(principal!.ProjectId!.Value, library.Id, cancellationToken);
            if (!referenced)
                throw new McpSecurityException("mcp.library_access_denied", "The MCP caller cannot access this library artifact.", 403);
        }
        return library;
    }

    private static async Task<object> ReadLibraryFamiliesAsync(
        IServiceScope scope,
        McpSecurityService security,
        McpPrincipal? principal,
        bool includeArchivedArtifacts,
        AiReadModelOptions options,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.LibraryRead);
        var normalized = options.Normalize();
        var families = await scope.ServiceProvider.GetRequiredService<ILibraryCatalogService>()
            .ListFamiliesAsync(includeArchivedArtifacts, cancellationToken);
        var items = (IsProjectScoped(principal)
                ? await ProjectVisibleFamiliesAsync(
                    scope,
                    principal!.ProjectId!.Value,
                    families,
                    cancellationToken)
                : families)
            .Take(normalized.MaxItems + 1)
            .ToArray();
        var hasMore = items.Length > normalized.MaxItems;
        var visible = hasMore ? items.Take(normalized.MaxItems).ToArray() : items;
        return new AiPageDto<LibraryFamilyDto>(
            AiReadModelContract.SchemaVersion,
            visible,
            hasMore,
            hasMore && visible.Length > 0 ? visible[^1].Id : null);
    }

    private static async Task<object?> ReadLibraryFamilyAsync(
        IServiceScope scope,
        McpSecurityService security,
        McpPrincipal? principal,
        string familyId,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.LibraryRead);
        var families = await scope.ServiceProvider.GetRequiredService<ILibraryCatalogService>()
            .ListFamiliesAsync(includeArchivedArtifacts: true, cancellationToken: cancellationToken);
        var visible = IsProjectScoped(principal)
            ? await ProjectVisibleFamiliesAsync(scope, principal!.ProjectId!.Value, families, cancellationToken)
            : families;
        return visible.SingleOrDefault(item => string.Equals(item.Id, familyId, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<IReadOnlyList<LibraryFamilyDto>> ProjectVisibleFamiliesAsync(
        IServiceScope scope,
        Guid projectId,
        IReadOnlyList<LibraryFamilyDto> families,
        CancellationToken cancellationToken)
    {
        var references = await scope.ServiceProvider.GetRequiredService<IProjectLibraryReferenceRepository>()
            .ListByProjectAsync(projectId, cancellationToken);
        var referencedArtifactIds = references
            .Select(static reference => reference.LibraryId)
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return families
            .Select(family => ProjectVisibleFamily(family, referencedArtifactIds))
            .Where(static family => family is not null)
            .Select(static family => family!)
            .ToArray();
    }

    private static LibraryFamilyDto? ProjectVisibleFamily(
        LibraryFamilyDto family,
        HashSet<string> referencedArtifactIds)
    {
        var artifacts = (family.Artifacts ?? [])
            .Where(artifact => referencedArtifactIds.Contains(artifact.Id))
            .OrderByDescending(artifact => ParseSemanticVersion(artifact.SemanticVersion ?? artifact.Version))
            .ThenByDescending(static artifact => artifact.UploadedAt)
            .ThenBy(static artifact => artifact.Id, StringComparer.Ordinal)
            .ToArray();
        if (artifacts.Length == 0)
            return null;

        var latestArtifactId = artifacts
            .FirstOrDefault(static artifact => artifact.Lifecycle == LibraryLifecycleDto.Available)
            ?.Id;
        return family with
        {
            LatestArtifactId = latestArtifactId,
            Artifacts = artifacts,
        };
    }

    private static Version ParseSemanticVersion(string value)
        => Version.TryParse(value?.Trim().TrimStart('v', 'V'), out var version)
            ? version
            : new Version(0, 0);

    private static async Task<object> ReadProjectLibrariesAsync(
        IServiceScope scope,
        McpSecurityService security,
        McpPrincipal? principal,
        Guid projectId,
        AiReadModelOptions options,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.LibraryRead, projectId);
        var result = await scope.ServiceProvider.GetRequiredService<ProjectLibraryService>()
            .ListAsync(projectId, cancellationToken);
        if (!result.IsSuccess)
            throw new McpProtocolException(-32004, result.Message ?? "The project was not found.", new { code = result.Code });
        var normalized = options.Normalize();
        var items = (result.References ?? [])
            .OrderBy(static reference => reference.LibraryId, StringComparer.Ordinal)
            .Take(normalized.MaxItems + 1)
            .ToArray();
        var hasMore = items.Length > normalized.MaxItems;
        var visible = hasMore ? items.Take(normalized.MaxItems).ToArray() : items;
        return new AiPageDto<ProjectLibraryReferenceDto>(
            AiReadModelContract.SchemaVersion,
            visible,
            hasMore,
            hasMore && visible.Length > 0 ? visible[^1].LibraryId : null);
    }

    private static async Task<object> ReadLibraryUpgradeAsync(
        IServiceScope scope,
        McpSecurityService security,
        McpPrincipal? principal,
        Guid projectId,
        Guid upgradeId,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.LibraryRead, projectId);
        var result = await scope.ServiceProvider.GetRequiredService<LibraryUpgradeService>()
            .GetPlanAsync(projectId, upgradeId, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
            throw new McpProtocolException(-32004, result.Message ?? "The library upgrade preview was not found.", new { code = result.Code });
        return result.Value;
    }

    private static async Task<object?> ReadRunAsync(
        IServiceScope scope,
        AiReadModelService service,
        McpSecurityService security,
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

    private static async Task<object?> ReadDebugAsync(
        IServiceScope scope,
        AiReadModelService service,
        McpSecurityService security,
        McpPrincipal? principal,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.DebugRead);
        var state = await service.GetDebugStateAsync(sessionId, cancellationToken);
        if (state is not null)
            security.Require(principal, McpPermissionDto.DebugRead, state.ProjectId);
        return state;
    }

    private static async Task<object> ReadVersionsAsync(
        McpSecurityService security,
        McpPrincipal? principal,
        Guid projectId,
        Guid flowId,
        string trackText,
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        var track = ParseTrack(trackText);
        var service = scope.ServiceProvider.GetRequiredService<AiReadModelService>();
        return await service.GetFlowVersionHistoryAsync(projectId, flowId, track, cancellationToken);
    }

    private static async Task<object?> ReadVersionAsync(
        McpSecurityService security,
        McpPrincipal? principal,
        Guid projectId,
        Guid flowId,
        string trackText,
        long version,
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        var track = ParseTrack(trackText);
        var detail = await scope.ServiceProvider.GetRequiredService<AiReadModelService>()
            .GetFlowVersionAsync(projectId, flowId, version, cancellationToken);
        if (detail is null || detail.Version.Track != track)
            return null;
        return detail with { Definition = FlowDiffService.RedactSensitive(detail.Definition) };
    }

    private static async Task<object> ReadPreviewAsync(
        IServiceScope scope,
        McpSecurityService security,
        McpPrincipal? principal,
        Guid previewId,
        CancellationToken cancellationToken)
    {
        var entry = await scope.ServiceProvider.GetRequiredService<IMcpPreviewStore>().FindAsync(previewId, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The MCP preview was not found.");
        if (principal is not null
            && !principal.IsLocal
            && !principal.IsAdministrator
            && !string.Equals(entry.PrincipalId, principal.Id, StringComparison.Ordinal))
        {
            throw new McpSecurityException(
                "mcp.preview_owner_mismatch",
                "The MCP preview belongs to another caller.",
                403);
        }
        if (entry.ProjectId is null && principal is not null && !principal.IsLocal && !principal.IsAdministrator)
            throw new McpSecurityException("mcp.preview_access_denied", "The MCP caller cannot access this global preview.", 403);
        if (entry.ProjectId is not null && principal is not null && !principal.CanAccess(entry.ProjectId.Value))
            throw new McpSecurityException("mcp.project_access_denied", "The MCP caller cannot access this preview.", 403);
        RequirePreviewPermission(security, principal, entry);
        var descriptor = new McpPreviewDescriptorDto(entry.Id, entry.Operation, entry.ProjectId, entry.FlowId, entry.Status, entry.ExpiresAt, entry.PreviewFingerprint);
        return entry.Operation switch
        {
            "project.create" => McpProjectToolHandlers.ReadProjectCreatePreview(entry, descriptor),
            "flow.patch" => ReadFlowPatchPreview(entry, descriptor),
            "flow.publish" => ReadPublishPreview(entry, descriptor),
            "flow.rollback" => ReadRollbackPreview(entry, descriptor),
            "library.package" => ReadLibraryPackagePreview(entry, descriptor),
            "project.library.attach" => ReadProjectLibraryAttachPreview(entry, descriptor),
            "library.family.assign" => ReadLibraryFamilyAssignmentPreview(entry, descriptor),
            "library.upgrade" => ReadLibraryUpgradePreview(entry, descriptor),
            _ => descriptor,
        };
    }

    private static FlowPatchPreviewDto ReadFlowPatchPreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredFlowPatchPreview>(entry);
        return ToFlowPatchPreview(entry, descriptor, stored);
    }

    internal static FlowPatchPreviewDto ToFlowPatchPreview(McpPreviewEntry preview, StoredFlowPatchPreview stored)
        => ToFlowPatchPreview(
            preview.Id,
            preview.ExpiresAt,
            preview.PreviewFingerprint,
            IsPreviewPending(preview),
            stored);

    private static FlowPatchPreviewDto ToFlowPatchPreview(
        McpPreviewEntry entry,
        McpPreviewDescriptorDto descriptor,
        StoredFlowPatchPreview stored)
        => ToFlowPatchPreview(
            descriptor.PreviewId,
            descriptor.ExpiresAt,
            descriptor.PreviewFingerprint,
            IsPreviewPending(entry),
            stored);

    private static FlowPatchPreviewDto ToFlowPatchPreview(
        Guid previewId,
        DateTimeOffset expiresAt,
        string previewFingerprint,
        bool isPending,
        StoredFlowPatchPreview stored)
    {
        var normalized = stored.CanonicalRequest is null
            ? new FlowPatchContractNormalizer().NormalizeLegacyRequest(stored.Request)
            : new NormalizedFlowPatchRequest(
                stored.CanonicalRequest,
                stored.Request,
                stored.NormalizationWarnings ?? []);
        return new FlowPatchPreviewDto(
            previewId,
            stored.Request.ProjectId,
            stored.Request.FlowId,
            stored.Request.ExpectedDevelopmentVersion,
            expiresAt,
            previewFingerprint,
            isPending
                && stored.Validation.IsValid
                && stored.Diff.Changes.Count > 0,
            stored.Validation,
            FlowDiffService.RedactSensitive(stored.Diff),
            FlowDiffService.RedactSensitive(stored.CandidateDefinition),
            SchemaVersion: FlowPatchContract.CurrentSchemaVersion,
            EnumEncoding: FlowPatchContract.EnumEncoding,
            NormalizedOperations: normalized.Request.Operations,
            NormalizationWarnings: normalized.Warnings);
    }

}

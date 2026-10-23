using Microsoft.AspNetCore.Http;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using static SereinFlow.Mcp.Support.McpToolAuthorization;

namespace SereinFlow.Mcp.Queries;

internal sealed class McpLibraryQueries(
    AiReadModelService service,
    ILibraryCatalogService _libraryCatalogService,
    IProjectLibraryReferenceRepository _projectLibraryReferenceRepository,
    LibraryUpgradeService _libraryUpgradeService,
    SereinFlowAccess security,
    ProjectLibraryService _projectLibraryService)
{
    internal async Task<AiPageDto<AiLibrarySummaryDto>> ReadLibrariesAsync(
        SereinFlowActor? principal,
        CancellationToken cancellationToken,
        LibraryListScope listScope = LibraryListScope.Active,
        AiReadModelOptions? options = null)
    {
        await security.RequireAsync(principal, SereinFlowPermission.LibraryRead, cancellationToken: cancellationToken);
        var scope = await security.GetScopeAsync(principal!, SereinFlowPermission.LibraryRead, cancellationToken);
        if (scope.AllProjects)
        {
            return listScope == LibraryListScope.Archived
                ? await service.ListArchivedLibrariesAsync(options, cancellationToken)
                : await service.ListLibrariesAsync(listScope == LibraryListScope.All, options, cancellationToken);
        }

        var references = await VisibleReferencesAsync(scope, cancellationToken);
        var maxItems = (options ?? new()).Normalize().MaxItems;
        var items = new List<AiLibrarySummaryDto>(maxItems + 1);
        var libraryIds = references
            .Select(static item => item.LibraryId)
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static id => id, StringComparer.Ordinal);
        foreach (var libraryId in libraryIds)
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
        var visible = hasMore ? items.Take(maxItems).ToArray() : items.ToArray();
        return new AiPageDto<AiLibrarySummaryDto>(AiReadModelContract.SchemaVersion, visible, hasMore, hasMore && visible.Length > 0 ? visible[^1].Id : null);
    }

    private static bool MatchesScope(AiLibrarySummaryDto library, LibraryListScope listScope) => listScope switch
    {
        LibraryListScope.Active => string.Equals(library.Lifecycle, LibraryLifecycleDto.Available.ToString(), StringComparison.OrdinalIgnoreCase),
        LibraryListScope.Archived => string.Equals(library.Lifecycle, LibraryLifecycleDto.Archived.ToString(), StringComparison.OrdinalIgnoreCase),
        _ => true,
    };

    internal enum LibraryListScope
    {
        Active,
        Archived,
        All,
    }

    internal async Task<AiLibrarySummaryDto?> ReadLibraryAsync(
        SereinFlowActor? principal,
        string libraryId,
        CancellationToken cancellationToken)
    {
        await security.RequireAsync(principal, SereinFlowPermission.LibraryRead, cancellationToken: cancellationToken);
        var scope = await security.GetScopeAsync(principal!, SereinFlowPermission.LibraryRead, cancellationToken);
        var library = await service.GetLibraryAsync(libraryId, cancellationToken);
        if (library is not null && !scope.AllProjects)
        {
            var referenced = (await VisibleReferencesAsync(scope, cancellationToken)).Any(reference => string.Equals(reference.LibraryId, library.Id, StringComparison.OrdinalIgnoreCase));
            if (!referenced)
            {
                throw new SereinFlowAccessException(McpErrorCodes.LibraryAccessDenied, "The MCP caller cannot access this library artifact. MCP 调用方无权访问此类库制品。", StatusCodes.Status403Forbidden);
            }
        }

        return library;
    }

    internal async Task<AiPageDto<LibraryFamilyDto>> ReadLibraryFamiliesAsync(
        SereinFlowActor? principal,
        bool includeArchivedArtifacts,
        AiReadModelOptions options,
        CancellationToken cancellationToken)
    {
        await security.RequireAsync(principal, SereinFlowPermission.LibraryRead, cancellationToken: cancellationToken);
        var scope = await security.GetScopeAsync(principal!, SereinFlowPermission.LibraryRead, cancellationToken);
        var normalized = options.Normalize();
        var families = await _libraryCatalogService.ListFamiliesAsync(includeArchivedArtifacts, cancellationToken);
        var visibleFamilies = !scope.AllProjects
            ? await ProjectVisibleFamiliesAsync(scope, families, cancellationToken)
            : families;
        var items = visibleFamilies.Take(normalized.MaxItems + 1).ToArray();
        var hasMore = items.Length > normalized.MaxItems;
        var visible = hasMore ? items.Take(normalized.MaxItems).ToArray() : items;
        return new AiPageDto<LibraryFamilyDto>(AiReadModelContract.SchemaVersion, visible, hasMore, hasMore && visible.Length > 0 ? visible[^1].Id : null);
    }

    internal async Task<LibraryFamilyDto?> ReadLibraryFamilyAsync(
        SereinFlowActor? principal,
        string familyId,
        CancellationToken cancellationToken)
    {
        await security.RequireAsync(principal, SereinFlowPermission.LibraryRead, cancellationToken: cancellationToken);
        var scope = await security.GetScopeAsync(principal!, SereinFlowPermission.LibraryRead, cancellationToken);
        var families = await _libraryCatalogService.ListFamiliesAsync(includeArchivedArtifacts: true, cancellationToken: cancellationToken);
        var visible = !scope.AllProjects ? await ProjectVisibleFamiliesAsync(scope, families, cancellationToken) : families;
        return visible.SingleOrDefault(item => string.Equals(item.Id, familyId, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyList<LibraryFamilyDto>> ProjectVisibleFamiliesAsync(
        ProjectAccessScope scope,
        IReadOnlyList<LibraryFamilyDto> families,
        CancellationToken cancellationToken)
    {
        var referencedArtifactIds = (await VisibleReferencesAsync(scope, cancellationToken))
            .Select(static reference => reference.LibraryId)
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return families
            .Select(family => ProjectVisibleFamily(family, referencedArtifactIds))
            .Where(static family => family is not null)
            .Select(static family => family!)
            .ToArray();
    }

    private static LibraryFamilyDto? ProjectVisibleFamily(LibraryFamilyDto family, HashSet<string> referencedArtifactIds)
    {
        var artifacts = (family.Artifacts ?? [])
            .Where(artifact => referencedArtifactIds.Contains(artifact.Id))
            .OrderByDescending(artifact => ParseSemanticVersion(artifact.SemanticVersion ?? artifact.Version))
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

    private async Task<IReadOnlyList<ProjectLibraryReference>> VisibleReferencesAsync(ProjectAccessScope scope, CancellationToken cancellationToken)
    {
        var references = new List<ProjectLibraryReference>();
        foreach (var project in scope.ProjectIds)
            references.AddRange(await _projectLibraryReferenceRepository.ListByProjectAsync(project, cancellationToken));
        return references;
    }

    private static Version ParseSemanticVersion(string value)
        => Version.TryParse(value?.Trim().TrimStart('v', 'V'), out var version) ? version : new Version(0, 0);

    internal async Task<AiPageDto<ProjectLibraryReferenceDto>> ReadProjectLibrariesAsync(
        SereinFlowActor? principal,
        Guid projectId,
        AiReadModelOptions options,
        CancellationToken cancellationToken)
    {
        await security.RequireAsync(principal, SereinFlowPermission.LibraryRead, projectId, cancellationToken: cancellationToken);
        var result = await _projectLibraryService.ListAsync(projectId, cancellationToken);
        if (!result.IsSuccess)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, result.Message ?? "The project was not found. 未找到项目。", new { code = result.Code });
        }

        var normalized = options.Normalize();
        var items = (result.References ?? [])
            .OrderBy(static reference => reference.LibraryId, StringComparer.Ordinal)
            .Take(normalized.MaxItems + 1)
            .ToArray();
        var hasMore = items.Length > normalized.MaxItems;
        var visible = hasMore ? items.Take(normalized.MaxItems).ToArray() : items;
        return new AiPageDto<ProjectLibraryReferenceDto>(AiReadModelContract.SchemaVersion, visible, hasMore, hasMore && visible.Length > 0 ? visible[^1].LibraryId : null);
    }

    internal async Task<LibraryUpgradePlanDto> ReadLibraryUpgradeAsync(
        SereinFlowActor? principal,
        Guid projectId,
        Guid upgradeId,
        CancellationToken cancellationToken)
    {
        await security.RequireAsync(principal, SereinFlowPermission.LibraryRead, projectId, cancellationToken: cancellationToken);
        var result = await _libraryUpgradeService.GetPlanAsync(projectId, upgradeId, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, result.Message ?? "The library upgrade preview was not found. 未找到类库升级预览。", new { code = result.Code });
        }

        return result.Value;
    }
}

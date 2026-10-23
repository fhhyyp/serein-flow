using Microsoft.AspNetCore.Http;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using SereinFlow.Mcp.Support;
using SereinFlow.Mcp.Previews;
using static SereinFlow.Mcp.Support.McpJson;
using static SereinFlow.Mcp.Support.McpMutationSupport;

namespace SereinFlow.Mcp.Tools;

internal sealed class McpLibraryPackageTools(
    IFileUploadSettings _fileUploadSettings,
    IFlowDefinitionRepository _flowDefinitionRepository,
    ILibraryCatalogService _libraryCatalogService,
    IProjectLibraryReferenceRepository _projectLibraryReferenceRepository,
    McpIdempotencyService _mcpIdempotencyService,
    McpPackageStagingService _mcpPackageStagingService,
    McpPreviewService _mcpPreviewService,
    McpResourceAuthorization _mcpResourceAuthorization,
    SereinFlowAccess _mcpSecurityService)
{
    [SereinFlowMcpTool("sereinflow_preview_library_package", SereinFlowPermission.LibraryImport, ExecutionKind = McpToolExecutionKind.Mutation)]
    internal async Task<LibraryPackagePreviewDto> PreviewPackageAsync(
        SereinFlowMcpCaller context,
        McpLibraryPackagePreviewRequest arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var fileName = arguments.FileName;
        var packageBase64 = arguments.PackageBase64;
        if (packageBase64.Length > FileUploadLimits.GetBase64EncodedLimit(_fileUploadSettings.MaxLibraryUploadBytes))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.RequestTooLarge, "The library package request is too large. 类库包请求过大。");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(packageBase64);
        }
        catch (FormatException exception)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The library package must be valid base64. 类库包必须使用有效的 base64 编码。", exception.Message);
        }

        var projectId = arguments.ProjectId;
        var familyId = arguments.FamilyId;
        var baselineArtifactId = arguments.BaselineArtifactId;
        if (!string.IsNullOrWhiteSpace(familyId) && !string.IsNullOrWhiteSpace(baselineArtifactId))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "Specify either familyId or baselineArtifactId, not both. familyId 和 baselineArtifactId 只能指定其中一个。");
        }

        if (projectId is not null)
        {
            await _mcpResourceAuthorization.RequireActiveProjectAsync(principal, projectId.Value, SereinFlowPermission.LibraryImport, cancellationToken);
        }
        else
        {
            await _mcpSecurityService.RequireWorkspaceAsync(principal, cancellationToken: cancellationToken);
        }

        if (projectId is not null)
        {
            await EnsurePackageBaselineAccessAsync(principal, projectId.Value, familyId, baselineArtifactId, cancellationToken);
        }

        McpStagedPackage? staged = null;
        try
        {
            var memoryStream = new MemoryStream(bytes, writable: false);
            staged = await _mcpPackageStagingService.StageAsync(memoryStream, cancellationToken);
            await using var package = _mcpPackageStagingService.OpenRead(staged.Path);
            var inspection = await _libraryCatalogService.InspectAsync(package, fileName, staged.SizeBytes, familyId, baselineArtifactId, cancellationToken) ?? throw new McpProtocolException(McpProtocolErrorCodes.LibraryInspectionUnavailable, "Library package inspection is not configured in this host. 当前宿主未配置类库包检查功能。");
            var compatibility = inspection.Compatibility;
            var projectImpact = projectId is null || compatibility is null ? null : await BuildLibraryPackageProjectImpactAsync(_flowDefinitionRepository, projectId.Value, compatibility.BaselineArtifactId, cancellationToken);
            var stored = new StoredLibraryPackagePreview(fileName, projectId, staged.Path, staged.SizeBytes, inspection.Library.Sha256, inspection, compatibility, projectImpact);
            var entry = await _mcpPreviewService.CreateAsync(LibraryErrorCodes.Package, principal, projectId, null, null, stored, cancellationToken);
            staged = null;
            return new LibraryPackagePreviewDto(entry.Id, fileName, stored.SizeBytes, inspection.Library.Sha256, entry.ExpiresAt, entry.PreviewFingerprint, true, inspection.Library, [], inspection.AlreadyExists, inspection.Library.DllSha256, compatibility, projectImpact);
        }
        catch (LibraryUploadException exception)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, exception.Message, new { code = McpErrorCodes.LibraryPackageInvalid, statusCode = exception.StatusCode });
        }
        catch (InvalidOperationException exception)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.RequestTooLarge, exception.Message, new { code = McpErrorCodes.LibraryPackageInvalid, statusCode = StatusCodes.Status413PayloadTooLarge });
        }
        finally
        {
            if (staged is not null)
            {
                _mcpPackageStagingService.Delete(staged.Path);
            }
        }
    }

    [SereinFlowMcpTool("sereinflow_apply_library_package", SereinFlowPermission.LibraryImport, ExecutionKind = McpToolExecutionKind.Mutation, RequiresIdempotencyKey = true)]
    internal async Task<object> ApplyPackageAsync(
        SereinFlowMcpCaller context,
        McpMutationApplyRequestDto arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var request = arguments;
        RequireConfirmation(request);
        var requestJson = Serialize(request);
        var replay = await _mcpIdempotencyService.FindAsync(principal.Id, LibraryErrorCodes.Package, request.IdempotencyKey, requestJson, cancellationToken);
        if (replay is not null)
        {
            return DeserializeStoredResponse(replay.ResponseJson);
        }

        var entry = await _mcpPreviewService.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, LibraryErrorCodes.Package, StringComparison.Ordinal))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The preview does not describe a library package. 此预览不是类库包预览。");
        }

        var stored = McpPreviewService.Deserialize<StoredLibraryPackagePreview>(entry);
        if (stored.ProjectId is null)
        {
            await _mcpSecurityService.RequireWorkspaceAsync(principal, cancellationToken: cancellationToken);
        }
        else
        {
            await _mcpResourceAuthorization.RequireActiveProjectAsync(principal, stored.ProjectId.Value, SereinFlowPermission.LibraryImport, cancellationToken);
        }

        try
        {
            await using var package = _mcpPackageStagingService.OpenRead(stored.StagingPath);
            if (package.Length != stored.SizeBytes)
            {
                throw new McpProtocolException(McpProtocolErrorCodes.Conflict, "The staged library package changed after preview. 暂存的类库包在预览后发生了变化。");
            }

            var result = await _libraryCatalogService.UploadVerifiedAsync(package, stored.FileName, stored.SizeBytes, stored.PackageSha256, origin: "mcp", cancellationToken: cancellationToken);
            await MarkPreviewAppliedAsync(_mcpPreviewService, entry, cancellationToken);
            await _mcpIdempotencyService.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, result, requestJson, cancellationToken);
            return result;
        }
        catch (LibraryUploadException exception) when (exception.StatusCode == StatusCodes.Status409Conflict)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.Conflict, exception.Message);
        }
        catch (FileNotFoundException)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The staged library package is no longer available. Create a new preview and apply it again. 暂存的类库包已不可用，请创建新预览并重新应用。", new { code = McpErrorCodes.PreviewPackageUnavailable });
        }
        finally
        {
            _mcpPackageStagingService.Delete(stored.StagingPath);
        }
    }

    private static async Task<LibraryPackageProjectImpactDto?> BuildLibraryPackageProjectImpactAsync(
        IFlowDefinitionRepository flows,
        Guid projectId,
        string baselineArtifactId,
        CancellationToken cancellationToken)
    {
        var affectedFlowIds = new List<Guid>();
        var affectedNodeCount = 0;
        foreach (var flow in await flows.ListByProjectAsync(projectId, cancellationToken))
        {
            var nodeCount = flow.Canvases.SelectMany(static canvas => canvas.Nodes).Count(node => node.Type is NodeTypeDto.Action or NodeTypeDto.Flipflop && string.Equals(node.Ui?.LibraryId, baselineArtifactId, StringComparison.OrdinalIgnoreCase));
            if (nodeCount == 0)
            {
                continue;
            }

            affectedFlowIds.Add(flow.Id);
            affectedNodeCount += nodeCount;
        }

        var toArray = affectedFlowIds.OrderBy(static id => id).ToArray();
        return new LibraryPackageProjectImpactDto(projectId, baselineArtifactId, affectedFlowIds.Count, affectedNodeCount, toArray);
    }

    private async Task EnsurePackageBaselineAccessAsync(
        SereinFlowActor principal,
        Guid projectId,
        string? familyId,
        string? baselineArtifactId,
        CancellationToken cancellationToken)
    {
        if (await _mcpSecurityService.CanManageWorkspaceAsync(principal, cancellationToken) || (string.IsNullOrWhiteSpace(familyId) && string.IsNullOrWhiteSpace(baselineArtifactId)))
        {
            return;
        }

        var artifactId = baselineArtifactId?.Trim();
        if (string.IsNullOrWhiteSpace(artifactId) && !string.IsNullOrWhiteSpace(familyId))
        {
            var family = ((await _libraryCatalogService.ListFamiliesAsync(includeArchivedArtifacts: true, cancellationToken: cancellationToken))).SingleOrDefault(item =>
            {
                var trim = familyId.Trim();
                return string.Equals(item.Id, trim, StringComparison.OrdinalIgnoreCase);
            });
            artifactId = family?.LatestArtifactId;
        }

        if (string.IsNullOrWhiteSpace(artifactId) || !await _projectLibraryReferenceRepository.IsReferencedAsync(projectId, artifactId, cancellationToken))
        {
            throw new SereinFlowAccessException(McpErrorCodes.LibraryAccessDenied, "The MCP caller cannot use the requested library baseline for this project. MCP 调用方无权为此项目使用指定的类库基线。", StatusCodes.Status403Forbidden);
        }
    }
}

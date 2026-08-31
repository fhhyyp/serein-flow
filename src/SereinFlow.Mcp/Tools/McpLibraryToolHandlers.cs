using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using static SereinFlow.Mcp.McpToolSupport;

namespace SereinFlow.Mcp;

/// <summary>
/// Library contracts, package staging and project attachment MCP tools.
/// Attachment remains an explicit preview/apply workflow.
/// </summary>
internal static class McpLibraryToolHandlers
{
    internal static Task<object> CreateNodeTemplateAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => CreateLibraryNodeTemplateAsync(context.Scope, context.RequirePrincipal(), arguments, cancellationToken);

    internal static Task<object> CompileScriptAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => CompileScriptAsync(context.Scope, arguments, cancellationToken);

    internal static Task<object> PreviewPackageAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => PreviewLibraryPackageAsync(context.Scope, context.RequirePrincipal(), arguments, cancellationToken);

    internal static Task<object> ApplyPackageAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => ApplyLibraryPackageAsync(context.Scope, context.RequirePrincipal(), arguments, cancellationToken);

    internal static Task<object> PreviewProjectAttachAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => PreviewProjectLibraryAttachAsync(context.Scope, context.RequirePrincipal(), arguments, cancellationToken);

    internal static Task<object> ApplyProjectAttachAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => ApplyProjectLibraryAttachAsync(context.Scope, context.RequirePrincipal(), arguments, cancellationToken);

    internal static Task<object> PreviewLibraryFamilyAssignmentAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => PreviewLibraryFamilyAssignmentAsync(context.Scope, context.RequirePrincipal(), arguments, cancellationToken);

    internal static Task<object> ApplyLibraryFamilyAssignmentAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => ApplyLibraryFamilyAssignmentAsync(context.Scope, context.RequirePrincipal(), arguments, cancellationToken);

    internal static Task<object> PreviewLibraryUpgradeAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => PreviewLibraryUpgradeAsync(context.Scope, context.RequirePrincipal(), arguments, cancellationToken);

    internal static Task<object> ApplyLibraryUpgradeAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => ApplyLibraryUpgradeAsync(context.Scope, context.RequirePrincipal(), arguments, cancellationToken);

    private static async Task<object> PreviewLibraryFamilyAssignmentAsync(
        IServiceScope scope,
        McpPrincipal principal,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        security.RequireAdministrator(principal);
        security.Require(principal, McpPermissionDto.LibraryManage);
        var validated = await ValidateLibraryFamilyAssignmentAsync(
            scope,
            Deserialize<LibraryFamilyAssignmentMcpRequestDto>(arguments),
            cancellationToken);
        var stored = new StoredLibraryFamilyAssignmentPreview(
            validated.Request,
            validated.Diagnostics,
            validated.CurrentFamily,
            validated.TargetFamily);
        var entry = await scope.ServiceProvider.GetRequiredService<McpPreviewService>().CreateAsync(
            "library.family.assign",
            principal,
            null,
            null,
            null,
            stored,
            cancellationToken);
        return new LibraryFamilyAssignmentMcpPreviewDto(
            entry.Id,
            validated.Request,
            entry.ExpiresAt,
            entry.PreviewFingerprint,
            validated.Diagnostics.Count == 0,
            validated.Diagnostics,
            validated.CurrentFamily,
            validated.TargetFamily);
    }

    private static async Task<object> ApplyLibraryFamilyAssignmentAsync(
        IServiceScope scope,
        McpPrincipal principal,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var request = Deserialize<McpMutationApplyRequestDto>(arguments);
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(
            principal.Id,
            "library.family.assign",
            request.IdempotencyKey,
            requestPayload,
            cancellationToken);
        if (replay is not null)
            return DeserializeStoredResponse(replay.ResponseJson);

        var preview = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await preview.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, "library.family.assign", StringComparison.Ordinal))
            throw new McpProtocolException(-32602, "The preview does not describe a library family assignment.");

        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        security.RequireAdministrator(principal);
        security.Require(principal, McpPermissionDto.LibraryManage);
        var stored = McpPreviewService.Deserialize<StoredLibraryFamilyAssignmentPreview>(entry);
        var validated = await ValidateLibraryFamilyAssignmentAsync(scope, stored.Request, cancellationToken);
        if (validated.Diagnostics.Count > 0)
        {
            throw new McpProtocolException(
                -32011,
                "The library family assignment is no longer valid. Create a new preview and review it again.",
                new { code = "mcp.validation_failed", diagnostics = validated.Diagnostics });
        }

        LibraryFamilyDto? result;
        try
        {
            result = await scope.ServiceProvider.GetRequiredService<ILibraryCatalogService>().AssignFamilyAsync(
                validated.Request.LibraryId,
                new AssignLibraryFamilyRequestDto(
                    validated.Request.FamilyId,
                    validated.Request.Name,
                    validated.Request.Description),
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            throw new McpProtocolException(
                -32011,
                "The library family assignment is no longer valid. Create a new preview and review it again.",
                new { code = "mcp.validation_failed", message = exception.Message });
        }

        if (result is null)
            throw new McpProtocolException(-32004, "The library artifact was not found.", new { code = "library.not_found" });

        await MarkPreviewAppliedAsync(preview, entry, cancellationToken);
        await idempotency.SaveAsync(
            principal.Id,
            entry.Operation,
            request.IdempotencyKey,
            result,
            requestPayload,
            cancellationToken);
        return result;
    }

    private static async Task<object> PreviewLibraryUpgradeAsync(
        IServiceScope scope,
        McpPrincipal principal,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var request = Deserialize<McpLibraryUpgradePreviewRequestDto>(arguments);
        request = request with
        {
            SourceArtifactId = request.SourceArtifactId?.Trim() ?? string.Empty,
            TargetArtifactId = request.TargetArtifactId?.Trim() ?? string.Empty,
            FlowIds = request.FlowIds ?? []
        };
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        await RequireActiveProjectAsync(
            scope,
            security,
            principal,
            request.ProjectId,
            McpPermissionDto.FlowWrite,
            cancellationToken);
        security.Require(principal, McpPermissionDto.LibraryManage, request.ProjectId);

        var upgradeRequest = new LibraryUpgradePreviewRequestDto(
            request.SourceArtifactId,
            request.TargetArtifactId,
            request.FlowIds);
        var result = await scope.ServiceProvider.GetRequiredService<LibraryUpgradeService>()
            .PreviewAsync(request.ProjectId, upgradeRequest, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
            throw LibraryUpgradeFailure(result);

        var stored = new StoredLibraryUpgradePreview(upgradeRequest, result.Value);
        var entry = await scope.ServiceProvider.GetRequiredService<McpPreviewService>().CreateAsync(
            "library.upgrade",
            principal,
            result.Value.ProjectId,
            null,
            null,
            stored,
            cancellationToken);
        return new McpLibraryUpgradePreviewDto(
            entry.Id,
            entry.ExpiresAt,
            entry.PreviewFingerprint,
            result.Value.Flows.Any(static flow => flow.CanApply),
            result.Value);
    }

    private static async Task<object> ApplyLibraryUpgradeAsync(
        IServiceScope scope,
        McpPrincipal principal,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var request = Deserialize<McpLibraryUpgradeApplyRequestDto>(arguments);
        var applyEnvelope = new McpMutationApplyRequestDto(
            request.PreviewId,
            request.PreviewFingerprint,
            request.Confirmation,
            request.IdempotencyKey);
        RequireConfirmation(applyEnvelope);
        if (request.Flows is null || request.Flows.Count == 0)
            throw new McpProtocolException(-32602, "At least one flow must be selected for a library upgrade.");

        var requestPayload = Serialize(request);
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(
            principal.Id,
            "library.upgrade",
            request.IdempotencyKey,
            requestPayload,
            cancellationToken);
        if (replay is not null)
            return DeserializeStoredResponse(replay.ResponseJson);

        var preview = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await preview.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, "library.upgrade", StringComparison.Ordinal))
            throw new McpProtocolException(-32602, "The preview does not describe a library upgrade.");

        var stored = McpPreviewService.Deserialize<StoredLibraryUpgradePreview>(entry);
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        await RequireActiveProjectAsync(
            scope,
            security,
            principal,
            stored.Plan.ProjectId,
            McpPermissionDto.FlowWrite,
            cancellationToken);
        security.Require(principal, McpPermissionDto.LibraryManage, stored.Plan.ProjectId);

        object response;
        var hasDurableSuccess = false;
        var upgrades = scope.ServiceProvider.GetRequiredService<LibraryUpgradeService>();
        if (request.Flows.Count == 1)
        {
            var result = await upgrades.ApplyAsync(
                stored.Plan.ProjectId,
                stored.Plan.Id,
                request.Flows[0],
                cancellationToken);
            if (!result.IsSuccess || result.Value is null)
                throw LibraryUpgradeFailure(result);
            response = result.Value;
            hasDurableSuccess = true;
        }
        else
        {
            var result = await upgrades.ApplyBatchAsync(
                stored.Plan.ProjectId,
                stored.Plan.Id,
                new ApplyLibraryUpgradeBatchRequestDto(request.Flows),
                cancellationToken);
            if (!result.IsSuccess || result.Value is null)
                throw LibraryUpgradeFailure(result);
            response = result.Value;
            hasDurableSuccess = result.Value.Succeeded.Count > 0;
        }

        if (hasDurableSuccess)
            await MarkPreviewAppliedAsync(preview, entry, cancellationToken);
        await idempotency.SaveAsync(
            principal.Id,
            entry.Operation,
            request.IdempotencyKey,
            response,
            requestPayload,
            cancellationToken);
        return response;
    }

    private static async Task<LibraryFamilyAssignmentValidation> ValidateLibraryFamilyAssignmentAsync(
        IServiceScope scope,
        LibraryFamilyAssignmentMcpRequestDto request,
        CancellationToken cancellationToken)
    {
        var libraryId = request.LibraryId?.Trim() ?? string.Empty;
        var familyId = NormalizeOptional(request.FamilyId);
        var name = NormalizeOptional(request.Name);
        var description = NormalizeOptional(request.Description);
        if (familyId is not null)
            name = null;
        var normalized = new LibraryFamilyAssignmentMcpRequestDto(libraryId, familyId, name, description);
        var diagnostics = new List<ValidationDiagnosticDto>();
        var catalog = scope.ServiceProvider.GetRequiredService<ILibraryCatalogService>();
        var library = string.IsNullOrWhiteSpace(libraryId)
            ? null
            : await catalog.FindAsync(libraryId, cancellationToken);
        if (string.IsNullOrWhiteSpace(libraryId))
            diagnostics.Add(new("library.id_required", "The library ID is required.", "libraryId"));
        else if (library is null)
            diagnostics.Add(new("library.not_found", "The library artifact was not found.", "libraryId"));

        var families = await catalog.ListFamiliesAsync(includeArchivedArtifacts: true, cancellationToken: cancellationToken);
        var currentFamily = library is null || string.IsNullOrWhiteSpace(library.FamilyId)
            ? null
            : families.SingleOrDefault(item => string.Equals(item.Id, library.FamilyId, StringComparison.OrdinalIgnoreCase));
        var targetFamily = familyId is null
            ? null
            : families.SingleOrDefault(item => string.Equals(item.Id, familyId, StringComparison.OrdinalIgnoreCase));
        if (familyId is not null && targetFamily is null)
            diagnostics.Add(new("library.family_not_found", "The requested library family was not found.", "familyId"));
        if (familyId is null && string.IsNullOrWhiteSpace(name))
            diagnostics.Add(new("library.family_name_required", "A family name is required when creating a library family.", "name"));

        return new LibraryFamilyAssignmentValidation(normalized, diagnostics, currentFamily, targetFamily);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static McpProtocolException LibraryUpgradeFailure<T>(LibraryUpgradeOperationResult<T> result)
        => new(
            result.StatusCode switch
            {
                400 => -32602,
                404 => -32004,
                _ => -32011,
            },
            result.Message ?? "The library upgrade could not be completed.",
            new { code = result.Code, statusCode = result.StatusCode, currentVersion = result.CurrentVersion });

    private sealed record LibraryFamilyAssignmentValidation(
        LibraryFamilyAssignmentMcpRequestDto Request,
        IReadOnlyList<ValidationDiagnosticDto> Diagnostics,
        LibraryFamilyDto? CurrentFamily,
        LibraryFamilyDto? TargetFamily);

    private static async Task<object> CreateLibraryNodeTemplateAsync(
        IServiceScope scope,
        McpPrincipal principal,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var request = Deserialize<LibraryNodeTemplateRequestDto>(arguments);
        if (request.ProjectId == Guid.Empty)
            throw new McpProtocolException(-32602, "The library node template request is invalid.");
        await RequireActiveProjectAsync(
            scope,
            scope.ServiceProvider.GetRequiredService<McpSecurityService>(),
            principal,
            request.ProjectId,
            McpPermissionDto.ProjectRead,
            cancellationToken);
        try
        {
            return await scope.ServiceProvider.GetRequiredService<LibraryNodeTemplateService>()
                .CreateAsync(request, cancellationToken);
        }
        catch (LibraryNodeTemplateException exception)
        {
            throw InvalidLibraryNodeTemplate(exception);
        }
    }

    private static async Task<object> CompileScriptAsync(
        IServiceScope scope,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var request = Deserialize<ScriptCompileRequestDto>(arguments);
        if (string.IsNullOrWhiteSpace(request.Source))
            throw new McpProtocolException(-32602, "The SereinLang source is required.");
        return await scope.ServiceProvider
            .GetRequiredService<SereinFlow.ScriptAdapter.ISereinLangCompiler>()
            .CompileAsync(request, cancellationToken);
    }

    private static async Task<object> PreviewLibraryPackageAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var fileName = GetRequiredString(arguments, "fileName");
        var packageBase64 = GetRequiredString(arguments, "packageBase64");
        if (packageBase64.Length > 140 * 1024 * 1024)
            throw new McpProtocolException(-32012, "The library package request is too large.");
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(packageBase64);
        }
        catch (FormatException exception)
        {
            throw new McpProtocolException(-32602, "The library package must be valid base64.", exception.Message);
        }
        var projectId = TryGetGuid(arguments, "projectId");
        var familyId = GetOptionalString(arguments, "familyId");
        var baselineArtifactId = GetOptionalString(arguments, "baselineArtifactId");
        if (!string.IsNullOrWhiteSpace(familyId) && !string.IsNullOrWhiteSpace(baselineArtifactId))
            throw new McpProtocolException(-32602, "Specify either familyId or baselineArtifactId, not both.");
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        if (projectId is not null)
            await RequireActiveProjectAsync(
                scope,
                security,
                principal,
                projectId.Value,
                McpPermissionDto.LibraryImport,
                cancellationToken);
        else if (!principal.IsLocal && !principal.IsAdministrator)
            security.RequireAdministrator(principal);
        if (projectId is not null)
        {
            await EnsurePackageBaselineAccessAsync(
                scope,
                principal,
                projectId.Value,
                familyId,
                baselineArtifactId,
                cancellationToken);
        }

        var staging = scope.ServiceProvider.GetRequiredService<McpPackageStagingService>();
        McpStagedPackage? staged = null;
        try
        {
            staged = await staging.StageAsync(new MemoryStream(bytes, writable: false), cancellationToken);
            await using var package = staging.OpenRead(staged.Path);
            var inspection = await scope.ServiceProvider.GetRequiredService<ILibraryCatalogService>().InspectAsync(
                package,
                fileName,
                staged.SizeBytes,
                familyId,
                baselineArtifactId,
                cancellationToken)
                ?? throw new McpProtocolException(-32020, "Library package inspection is not configured in this host.");
            var compatibility = inspection.Compatibility;
            var projectImpact = projectId is null || compatibility is null
                ? null
                : await BuildLibraryPackageProjectImpactAsync(
                    scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>(),
                    projectId.Value,
                    compatibility.BaselineArtifactId,
                    cancellationToken);
            var stored = new StoredLibraryPackagePreview(
                fileName,
                projectId,
                staged.Path,
                staged.SizeBytes,
                staged.Sha256,
                inspection,
                compatibility,
                projectImpact);
            var entry = await scope.ServiceProvider.GetRequiredService<McpPreviewService>().CreateAsync(
                "library.package", principal, projectId, null, null, stored, cancellationToken);
            staged = null;
            return new LibraryPackagePreviewDto(
                entry.Id,
                fileName,
                stored.SizeBytes,
                inspection.Library.Sha256,
                entry.ExpiresAt,
                entry.PreviewFingerprint,
                true,
                inspection.Library,
                [],
                inspection.AlreadyExists,
                inspection.Library.DllSha256,
                compatibility,
                projectImpact);
        }
        catch (LibraryUploadException exception)
        {
            throw new McpProtocolException(
                -32011,
                exception.Message,
                new { code = "mcp.library_package_invalid", statusCode = exception.StatusCode });
        }
        catch (InvalidOperationException exception)
        {
            throw new McpProtocolException(
                -32012,
                exception.Message,
                new { code = "mcp.library_package_invalid", statusCode = 413 });
        }
        finally
        {
            if (staged is not null)
                staging.Delete(staged.Path);
        }
    }

    private static async Task<object> ApplyLibraryPackageAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<McpMutationApplyRequestDto>(arguments);
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, "library.package", request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
            return DeserializeStoredResponse(replay.ResponseJson);
        var preview = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await preview.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, "library.package", StringComparison.Ordinal))
            throw new McpProtocolException(-32602, "The preview does not describe a library package.");
        var stored = McpPreviewService.Deserialize<StoredLibraryPackagePreview>(entry);
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        if (stored.ProjectId is null)
            security.RequireAdministrator(principal);
        else
            await RequireActiveProjectAsync(
                scope,
                security,
                principal,
                stored.ProjectId.Value,
                McpPermissionDto.LibraryImport,
                cancellationToken);
        var staging = scope.ServiceProvider.GetRequiredService<McpPackageStagingService>();
        try
        {
            await using var package = staging.OpenRead(stored.StagingPath);
            if (package.Length != stored.SizeBytes || !string.Equals(await ComputeSha256Async(package, cancellationToken), stored.PackageSha256, StringComparison.OrdinalIgnoreCase))
                throw new McpProtocolException(-32010, "The staged library package changed after preview.");
            package.Position = 0;
            var result = await scope.ServiceProvider.GetRequiredService<ILibraryCatalogService>().UploadAsync(
                package, stored.FileName, stored.SizeBytes, cancellationToken);
            await MarkPreviewAppliedAsync(preview, entry, cancellationToken);
            await idempotency.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, result, requestPayload, cancellationToken);
            return result;
        }
        catch (FileNotFoundException)
        {
            throw new McpProtocolException(
                -32004,
                "The staged library package is no longer available. Create a new preview and apply it again.",
                new { code = "mcp.preview_package_unavailable" });
        }
        finally
        {
            staging.Delete(stored.StagingPath);
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
            var nodeCount = flow.Canvases
                .SelectMany(static canvas => canvas.Nodes)
                .Count(node => node.Type is NodeTypeDto.Action or NodeTypeDto.Flipflop
                    && string.Equals(node.Ui?.LibraryId, baselineArtifactId, StringComparison.OrdinalIgnoreCase));
            if (nodeCount == 0)
                continue;
            affectedFlowIds.Add(flow.Id);
            affectedNodeCount += nodeCount;
        }

        return new LibraryPackageProjectImpactDto(
            projectId,
            baselineArtifactId,
            affectedFlowIds.Count,
            affectedNodeCount,
            affectedFlowIds.OrderBy(static id => id).ToArray());
    }

    private static async Task EnsurePackageBaselineAccessAsync(
        IServiceScope scope,
        McpPrincipal principal,
        Guid projectId,
        string? familyId,
        string? baselineArtifactId,
        CancellationToken cancellationToken)
    {
        if (principal.IsLocal || principal.IsAdministrator
            || (string.IsNullOrWhiteSpace(familyId) && string.IsNullOrWhiteSpace(baselineArtifactId)))
        {
            return;
        }

        var artifactId = baselineArtifactId?.Trim();
        if (string.IsNullOrWhiteSpace(artifactId) && !string.IsNullOrWhiteSpace(familyId))
        {
            var family = (await scope.ServiceProvider.GetRequiredService<ILibraryCatalogService>()
                    .ListFamiliesAsync(includeArchivedArtifacts: true, cancellationToken: cancellationToken))
                .SingleOrDefault(item => string.Equals(item.Id, familyId.Trim(), StringComparison.OrdinalIgnoreCase));
            artifactId = family?.LatestArtifactId;
        }

        if (string.IsNullOrWhiteSpace(artifactId)
            || !await scope.ServiceProvider.GetRequiredService<IProjectLibraryReferenceRepository>()
                .IsReferencedAsync(projectId, artifactId, cancellationToken))
        {
            throw new McpSecurityException(
                "mcp.library_access_denied",
                "The MCP caller cannot use the requested library baseline for this project.",
                403);
        }
    }

    private static async Task<object> PreviewProjectLibraryAttachAsync(
        IServiceScope scope,
        McpPrincipal principal,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var request = Deserialize<ProjectLibraryAttachRequestDto>(arguments);
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        security.Require(principal, McpPermissionDto.LibraryManage, request.ProjectId);
        var libraryId = request.LibraryId?.Trim() ?? string.Empty;
        request = request with { LibraryId = libraryId };
        var validation = await ValidateProjectLibraryAttachAsync(scope, request, cancellationToken);
        var stored = new StoredProjectLibraryAttachPreview(new(request.ProjectId, libraryId), validation);
        var entry = await scope.ServiceProvider.GetRequiredService<McpPreviewService>().CreateAsync(
            "project.library.attach", principal, request.ProjectId, null, null, stored, cancellationToken);
        return new ProjectLibraryAttachPreviewDto(
            entry.Id,
            request.ProjectId,
            libraryId,
            entry.ExpiresAt,
            entry.PreviewFingerprint,
            validation.Count == 0,
            validation);
    }

    private static async Task<object> ApplyProjectLibraryAttachAsync(
        IServiceScope scope,
        McpPrincipal principal,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var request = Deserialize<McpMutationApplyRequestDto>(arguments);
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, "project.library.attach", request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
            return DeserializeStoredResponse(replay.ResponseJson);
        var preview = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await preview.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, "project.library.attach", StringComparison.Ordinal))
            throw new McpProtocolException(-32602, "The preview does not describe a project library attachment.");
        var stored = McpPreviewService.Deserialize<StoredProjectLibraryAttachPreview>(entry);
        scope.ServiceProvider.GetRequiredService<McpSecurityService>()
            .Require(principal, McpPermissionDto.LibraryManage, stored.Request.ProjectId);
        var currentDiagnostics = await ValidateProjectLibraryAttachAsync(scope, stored.Request, cancellationToken);
        if (currentDiagnostics.Count > 0)
        {
            throw new McpProtocolException(
                -32011,
                "The project library attachment is no longer valid. Create a new preview and review it again.",
                new { code = "mcp.validation_failed", diagnostics = currentDiagnostics });
        }
        var result = await scope.ServiceProvider.GetRequiredService<ProjectLibraryService>().AddAsync(
            stored.Request.ProjectId, stored.Request.LibraryId, cancellationToken);
        if (!result.IsSuccess)
            throw new McpProtocolException(-32011, result.Message ?? "The project library attachment failed.", result.Code);
        await MarkPreviewAppliedAsync(preview, entry, cancellationToken);
        await idempotency.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, result, requestPayload, cancellationToken);
        return result;
    }

    private static async Task<IReadOnlyList<ValidationDiagnosticDto>> ValidateProjectLibraryAttachAsync(
        IServiceScope scope,
        ProjectLibraryAttachRequestDto request,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<ValidationDiagnosticDto>();
        var project = await scope.ServiceProvider.GetRequiredService<IProjectRepository>()
            .FindAsync(request.ProjectId, cancellationToken);
        if (project is null)
            diagnostics.Add(new("project.not_found", "The project was not found. 未找到项目。", "projectId"));
        else if (project.Status == SereinFlow.Domain.ProjectStatus.Archived)
            diagnostics.Add(new("project.archived", "Archived projects cannot change library references. 已归档项目不能修改类库引用。", "projectId"));

        var libraryId = request.LibraryId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(libraryId))
            diagnostics.Add(new("library.id_required", "The library ID is required. 类库 ID 不能为空。", "libraryId"));
        var library = string.IsNullOrWhiteSpace(libraryId)
            ? null
            : await scope.ServiceProvider.GetRequiredService<ILibraryCatalogService>()
                .FindAsync(libraryId, cancellationToken);
        if (library is null)
            diagnostics.Add(new("library.not_found", "The library artifact was not found. 未找到类库制品。", "libraryId"));
        else if (library.Lifecycle != LibraryLifecycleDto.Available)
            diagnostics.Add(new("library.archived", "Archived library artifacts cannot be attached. 已归档类库制品不能被接入。", "libraryId"));

        if (library is not null
            && await scope.ServiceProvider.GetRequiredService<IProjectLibraryReferenceRepository>()
                .IsReferencedAsync(request.ProjectId, library.Id, cancellationToken))
        {
            diagnostics.Add(new("project_library.already_referenced", "The project already references this library artifact. 项目已引用该类库制品。", "libraryId"));
        }

        return diagnostics;
    }

}

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
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
    {
        var requirePrincipalResult = context.RequirePrincipal();
        var createLibraryNodeTemplateAsyncResult = CreateLibraryNodeTemplateAsync(context.Scope, requirePrincipalResult, arguments, cancellationToken);
        return createLibraryNodeTemplateAsyncResult;
    }

    internal static Task<object> CompileScriptAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var compileScriptAsyncResult = CompileScriptAsync(context.Scope, arguments, cancellationToken);
        return compileScriptAsyncResult;
    }

    internal static Task<object> PreviewPackageAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult1 = context.RequirePrincipal();
        var previewLibraryPackageAsyncResult = PreviewLibraryPackageAsync(context.Scope, requirePrincipalResult1, arguments, cancellationToken);
        return previewLibraryPackageAsyncResult;
    }

    internal static Task<object> ApplyPackageAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult2 = context.RequirePrincipal();
        var applyLibraryPackageAsyncResult = ApplyLibraryPackageAsync(context.Scope, requirePrincipalResult2, arguments, cancellationToken);
        return applyLibraryPackageAsyncResult;
    }

    internal static Task<object> PreviewProjectAttachAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult3 = context.RequirePrincipal();
        var previewProjectLibraryAttachAsyncResult = PreviewProjectLibraryAttachAsync(context.Scope, requirePrincipalResult3, arguments, cancellationToken);
        return previewProjectLibraryAttachAsyncResult;
    }

    internal static Task<object> ApplyProjectAttachAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult4 = context.RequirePrincipal();
        var applyProjectLibraryAttachAsyncResult = ApplyProjectLibraryAttachAsync(context.Scope, requirePrincipalResult4, arguments, cancellationToken);
        return applyProjectLibraryAttachAsyncResult;
    }

    internal static Task<object> PreviewLibraryFamilyAssignmentAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult5 = context.RequirePrincipal();
        var previewLibraryFamilyAssignmentAsyncResult = PreviewLibraryFamilyAssignmentAsync(context.Scope, requirePrincipalResult5, arguments, cancellationToken);
        return previewLibraryFamilyAssignmentAsyncResult;
    }

    internal static Task<object> ApplyLibraryFamilyAssignmentAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult6 = context.RequirePrincipal();
        var applyLibraryFamilyAssignmentAsyncResult = ApplyLibraryFamilyAssignmentAsync(context.Scope, requirePrincipalResult6, arguments, cancellationToken);
        return applyLibraryFamilyAssignmentAsyncResult;
    }

    internal static Task<object> PreviewLibraryUpgradeAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult7 = context.RequirePrincipal();
        var previewLibraryUpgradeAsyncResult = PreviewLibraryUpgradeAsync(context.Scope, requirePrincipalResult7, arguments, cancellationToken);
        return previewLibraryUpgradeAsyncResult;
    }

    internal static Task<object> ApplyLibraryUpgradeAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult8 = context.RequirePrincipal();
        var applyLibraryUpgradeAsyncResult = ApplyLibraryUpgradeAsync(context.Scope, requirePrincipalResult8, arguments, cancellationToken);
        return applyLibraryUpgradeAsyncResult;
    }

    private static async Task<object> PreviewLibraryFamilyAssignmentAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        security.RequireAdministrator(principal);
        security.Require(principal, McpPermissionDto.LibraryManage);
        var deserializeResult = Deserialize<LibraryFamilyAssignmentMcpRequestDto>(arguments);
        var validated = await ValidateLibraryFamilyAssignmentAsync(scope, deserializeResult, cancellationToken);
        var stored = new StoredLibraryFamilyAssignmentPreview(validated.Request, validated.Diagnostics, validated.CurrentFamily, validated.TargetFamily);
        var getRequiredServiceResult = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await getRequiredServiceResult.CreateAsync(LibraryErrorCodes.FamilyAssign, principal, null, null, null, stored, cancellationToken);
        var libraryFamilyAssignmentMcpPreviewDto = new LibraryFamilyAssignmentMcpPreviewDto(entry.Id, validated.Request, entry.ExpiresAt, entry.PreviewFingerprint, validated.Diagnostics.Count == 0, validated.Diagnostics, validated.CurrentFamily, validated.TargetFamily);
        return libraryFamilyAssignmentMcpPreviewDto;
    }

    private static async Task<object> ApplyLibraryFamilyAssignmentAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<McpMutationApplyRequestDto>(arguments);
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, LibraryErrorCodes.FamilyAssign, request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            var deserializeStoredResponseResult = DeserializeStoredResponse(replay.ResponseJson);
            return deserializeStoredResponseResult;
        }

        var preview = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await preview.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, LibraryErrorCodes.FamilyAssign, StringComparison.Ordinal))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The preview does not describe a library family assignment.");
        }

        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        security.RequireAdministrator(principal);
        security.Require(principal, McpPermissionDto.LibraryManage);
        var stored = McpPreviewService.Deserialize<StoredLibraryFamilyAssignmentPreview>(entry);
        var validated = await ValidateLibraryFamilyAssignmentAsync(scope, stored.Request, cancellationToken);
        if (validated.Diagnostics.Count > 0)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "The library family assignment is no longer valid. Create a new preview and review it again.", new { code = McpErrorCodes.ValidationFailed, diagnostics = validated.Diagnostics });
        }

        LibraryFamilyDto? result;
        try
        {
            var getRequiredServiceResult9 = scope.ServiceProvider.GetRequiredService<ILibraryCatalogService>();
            var assignLibraryFamilyRequestDto = new AssignLibraryFamilyRequestDto(validated.Request.FamilyId, validated.Request.Name, validated.Request.Description);
            result = await getRequiredServiceResult9.AssignFamilyAsync(validated.Request.LibraryId, assignLibraryFamilyRequestDto, cancellationToken, origin: "mcp");
        }
        catch (ArgumentException exception)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "The library family assignment is no longer valid. Create a new preview and review it again.", new { code = McpErrorCodes.ValidationFailed, message = exception.Message });
        }

        if (result is null)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The library artifact was not found.", new { code = LibraryErrorCodes.NotFound });
        }

        await MarkPreviewAppliedAsync(preview, entry, cancellationToken);
        await idempotency.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, result, requestPayload, cancellationToken);
        return result;
    }

    private static async Task<object> PreviewLibraryUpgradeAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<McpLibraryUpgradePreviewRequestDto>(arguments);
        request = request with
        {
            SourceArtifactId = request.SourceArtifactId?.Trim() ?? string.Empty,
            TargetArtifactId = request.TargetArtifactId?.Trim() ?? string.Empty,
            FlowIds = request.FlowIds ?? []
        };
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        await RequireActiveProjectAsync(scope, security, principal, request.ProjectId, McpPermissionDto.FlowWrite, cancellationToken);
        security.Require(principal, McpPermissionDto.LibraryManage, request.ProjectId);
        var upgradeRequest = new LibraryUpgradePreviewRequestDto(request.SourceArtifactId, request.TargetArtifactId, request.FlowIds);
        var getRequiredServiceResult10 = scope.ServiceProvider.GetRequiredService<LibraryUpgradeService>();
        var result = await getRequiredServiceResult10.PreviewAsync(request.ProjectId, upgradeRequest, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            throw LibraryUpgradeFailure(result);
        }

        var stored = new StoredLibraryUpgradePreview(upgradeRequest, result.Value);
        var getRequiredServiceResult11 = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await getRequiredServiceResult11.CreateAsync("library.upgrade", principal, result.Value.ProjectId, null, null, stored, cancellationToken);
        var anyResult = result.Value.Flows.Any(static flow => flow.CanApply);
        var mcpLibraryUpgradePreviewDto = new McpLibraryUpgradePreviewDto(entry.Id, entry.ExpiresAt, entry.PreviewFingerprint, anyResult, result.Value);
        return mcpLibraryUpgradePreviewDto;
    }

    private static async Task<object> ApplyLibraryUpgradeAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<McpLibraryUpgradeApplyRequestDto>(arguments);
        var applyEnvelope = new McpMutationApplyRequestDto(request.PreviewId, request.PreviewFingerprint, request.Confirmation, request.IdempotencyKey);
        RequireConfirmation(applyEnvelope);
        if (request.Flows is null || request.Flows.Count == 0)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "At least one flow must be selected for a library upgrade.");
        }

        var requestPayload = Serialize(request);
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, "library.upgrade", request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            var deserializeStoredResponseResult12 = DeserializeStoredResponse(replay.ResponseJson);
            return deserializeStoredResponseResult12;
        }

        var preview = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await preview.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, "library.upgrade", StringComparison.Ordinal))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The preview does not describe a library upgrade.");
        }

        var stored = McpPreviewService.Deserialize<StoredLibraryUpgradePreview>(entry);
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        await RequireActiveProjectAsync(scope, security, principal, stored.Plan.ProjectId, McpPermissionDto.FlowWrite, cancellationToken);
        security.Require(principal, McpPermissionDto.LibraryManage, stored.Plan.ProjectId);
        object response;
        var hasDurableSuccess = false;
        var upgrades = scope.ServiceProvider.GetRequiredService<LibraryUpgradeService>();
        if (request.Flows.Count == 1)
        {
            var result = await upgrades.ApplyAsync(stored.Plan.ProjectId, stored.Plan.Id, request.Flows[0], cancellationToken, origin: "mcp");
            if (!result.IsSuccess || result.Value is null)
            {
                throw LibraryUpgradeFailure(result);
            }

            response = result.Value;
            hasDurableSuccess = true;
        }
        else
        {
            var applyLibraryUpgradeBatchRequestDto = new ApplyLibraryUpgradeBatchRequestDto(request.Flows);
            var result = await upgrades.ApplyBatchAsync(stored.Plan.ProjectId, stored.Plan.Id, applyLibraryUpgradeBatchRequestDto, cancellationToken, origin: "mcp");
            if (!result.IsSuccess || result.Value is null)
            {
                throw LibraryUpgradeFailure(result);
            }

            response = result.Value;
            hasDurableSuccess = result.Value.Succeeded.Count > 0;
        }

        if (hasDurableSuccess)
        {
            await MarkPreviewAppliedAsync(preview, entry, cancellationToken);
        }

        await idempotency.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, response, requestPayload, cancellationToken);
        return response;
    }

    private static async Task<LibraryFamilyAssignmentValidation> ValidateLibraryFamilyAssignmentAsync(IServiceScope scope, LibraryFamilyAssignmentMcpRequestDto request, CancellationToken cancellationToken)
    {
        var libraryId = request.LibraryId?.Trim() ?? string.Empty;
        var familyId = NormalizeOptional(request.FamilyId);
        var name = NormalizeOptional(request.Name);
        var description = NormalizeOptional(request.Description);
        if (familyId is not null)
        {
            name = null;
        }

        var normalized = new LibraryFamilyAssignmentMcpRequestDto(libraryId, familyId, name, description);
        var diagnostics = new List<ValidationDiagnosticDto>();
        var catalog = scope.ServiceProvider.GetRequiredService<ILibraryCatalogService>();
        var library = string.IsNullOrWhiteSpace(libraryId) ? null : await catalog.FindAsync(libraryId, cancellationToken);
        if (string.IsNullOrWhiteSpace(libraryId))
        {
            global::SereinFlow.Contracts.ValidationDiagnosticDto evaluatedValue13 = new(LibraryErrorCodes.IdRequired, "The library ID is required.", "libraryId");
            diagnostics.Add(evaluatedValue13);
        }
        else if (library is null)
        {
            global::SereinFlow.Contracts.ValidationDiagnosticDto evaluatedValue = new(LibraryErrorCodes.NotFound, "The library artifact was not found.", "libraryId");
            diagnostics.Add(evaluatedValue);
        }

        var families = await catalog.ListFamiliesAsync(includeArchivedArtifacts: true, cancellationToken: cancellationToken);
        var currentFamily = library is null || string.IsNullOrWhiteSpace(library.FamilyId) ? null : families.SingleOrDefault(item =>
        {
            var equalsResult = string.Equals(item.Id, library.FamilyId, StringComparison.OrdinalIgnoreCase);
            return equalsResult;
        });
        var targetFamily = familyId is null ? null : families.SingleOrDefault(item =>
        {
            var equalsResult14 = string.Equals(item.Id, familyId, StringComparison.OrdinalIgnoreCase);
            return equalsResult14;
        });
        if (familyId is not null && targetFamily is null)
        {
            global::SereinFlow.Contracts.ValidationDiagnosticDto evaluatedValue15 = new(LibraryErrorCodes.FamilyNotFound, "The requested library family was not found.", "familyId");
            diagnostics.Add(evaluatedValue15);
        }

        if (familyId is null && string.IsNullOrWhiteSpace(name))
        {
            global::SereinFlow.Contracts.ValidationDiagnosticDto evaluatedValue16 = new(LibraryErrorCodes.FamilyNameRequired, "A family name is required when creating a library family.", "name");
            diagnostics.Add(evaluatedValue16);
        }

        var libraryFamilyAssignmentValidation = new LibraryFamilyAssignmentValidation(normalized, diagnostics, currentFamily, targetFamily);
        return libraryFamilyAssignmentValidation;
    }

    private static string? NormalizeOptional(string? value)
    {
        string? evaluatedValue17 = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return evaluatedValue17;
    }

    private static McpProtocolException LibraryUpgradeFailure<T>(LibraryUpgradeOperationResult<T> result)
    {
        global::SereinFlow.Mcp.McpProtocolException evaluatedValue18 = new(result.StatusCode switch
        {
            StatusCodes.Status400BadRequest => McpProtocolErrorCodes.InvalidParams,
            StatusCodes.Status404NotFound => McpProtocolErrorCodes.ResourceNotFound,
            _ => McpProtocolErrorCodes.OperationRejected,
        }, result.Message ?? "The library upgrade could not be completed.", new { code = result.Code, statusCode = result.StatusCode, currentVersion = result.CurrentVersion });
        return evaluatedValue18;
    }

    private sealed record LibraryFamilyAssignmentValidation(LibraryFamilyAssignmentMcpRequestDto Request, IReadOnlyList<ValidationDiagnosticDto> Diagnostics, LibraryFamilyDto? CurrentFamily, LibraryFamilyDto? TargetFamily);
    private static async Task<object> CreateLibraryNodeTemplateAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<LibraryNodeTemplateRequestDto>(arguments);
        if (request.ProjectId == Guid.Empty)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The library node template request is invalid.");
        }

        var getRequiredServiceResult20 = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        await RequireActiveProjectAsync(scope, getRequiredServiceResult20, principal, request.ProjectId, McpPermissionDto.ProjectRead, cancellationToken);
        try
        {
            var getRequiredServiceResult19 = scope.ServiceProvider.GetRequiredService<LibraryNodeTemplateService>();
            var createAsyncResult = await getRequiredServiceResult19.CreateAsync(request, cancellationToken);
            return createAsyncResult;
        }
        catch (LibraryNodeTemplateException exception)
        {
            throw InvalidLibraryNodeTemplate(exception);
        }
    }

    private static async Task<object> CompileScriptAsync(IServiceScope scope, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<ScriptCompileRequestDto>(arguments);
        if (string.IsNullOrWhiteSpace(request.Source))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The SereinLang source is required.");
        }

        var getRequiredServiceResult21 = scope.ServiceProvider.GetRequiredService<SereinFlow.ScriptAdapter.ISereinLangCompiler>();
        var compileAsyncResult = await getRequiredServiceResult21.CompileAsync(request, cancellationToken);
        return compileAsyncResult;
    }

    private static async Task<object> PreviewLibraryPackageAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var fileName = GetRequiredString(arguments, "fileName");
        var packageBase64 = GetRequiredString(arguments, "packageBase64");
        var fileUploadSettings = scope.ServiceProvider.GetRequiredService<IFileUploadSettings>();
        if (packageBase64.Length > FileUploadLimits.GetBase64EncodedLimit(fileUploadSettings.MaxLibraryUploadBytes))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.RequestTooLarge, "The library package request is too large.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(packageBase64);
        }
        catch (FormatException exception)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The library package must be valid base64.", exception.Message);
        }

        var projectId = TryGetGuid(arguments, "projectId");
        var familyId = GetOptionalString(arguments, "familyId");
        var baselineArtifactId = GetOptionalString(arguments, "baselineArtifactId");
        if (!string.IsNullOrWhiteSpace(familyId) && !string.IsNullOrWhiteSpace(baselineArtifactId))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "Specify either familyId or baselineArtifactId, not both.");
        }

        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        if (projectId is not null)
        {
            await RequireActiveProjectAsync(scope, security, principal, projectId.Value, McpPermissionDto.LibraryImport, cancellationToken);
        }
        else if (!principal.IsLocal && !principal.IsAdministrator)
        {
            security.RequireAdministrator(principal);
        }

        if (projectId is not null)
        {
            await EnsurePackageBaselineAccessAsync(scope, principal, projectId.Value, familyId, baselineArtifactId, cancellationToken);
        }

        var staging = scope.ServiceProvider.GetRequiredService<McpPackageStagingService>();
        McpStagedPackage? staged = null;
        try
        {
            var memoryStream = new MemoryStream(bytes, writable: false);
            staged = await staging.StageAsync(memoryStream, cancellationToken);
            await using var package = staging.OpenRead(staged.Path);
            var inspection = await resolveInspectAsyncResult() ?? throw new McpProtocolException(McpProtocolErrorCodes.LibraryInspectionUnavailable, "Library package inspection is not configured in this host.");
            var compatibility = inspection.Compatibility;
            var projectImpact = projectId is null || compatibility is null ? null : await resolveBuildLibraryPackageProjectImpactAsyncResult();
            var stored = new StoredLibraryPackagePreview(fileName, projectId, staged.Path, staged.SizeBytes, inspection.Library.Sha256, inspection, compatibility, projectImpact);
            var getRequiredServiceResult24 = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
            var entry = await getRequiredServiceResult24.CreateAsync(LibraryErrorCodes.Package, principal, projectId, null, null, stored, cancellationToken);
            staged = null;
            var libraryPackagePreviewDto = new LibraryPackagePreviewDto(entry.Id, fileName, stored.SizeBytes, inspection.Library.Sha256, entry.ExpiresAt, entry.PreviewFingerprint, true, inspection.Library, [], inspection.AlreadyExists, inspection.Library.DllSha256, compatibility, projectImpact);
            return libraryPackagePreviewDto;
            global::System.Threading.Tasks.Task<global::SereinFlow.Application.LibraryPackageInspectionDto?> resolveInspectAsyncResult()
            {
                var getRequiredServiceResult22 = scope.ServiceProvider.GetRequiredService<ILibraryCatalogService>();
                var inspectAsyncResult = getRequiredServiceResult22.InspectAsync(package, fileName, staged.SizeBytes, familyId, baselineArtifactId, cancellationToken);
                return inspectAsyncResult;
            }

            global::System.Threading.Tasks.Task<global::SereinFlow.Contracts.LibraryPackageProjectImpactDto?> resolveBuildLibraryPackageProjectImpactAsyncResult()
            {
                var getRequiredServiceResult23 = scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
                var buildLibraryPackageProjectImpactAsyncResult = BuildLibraryPackageProjectImpactAsync(getRequiredServiceResult23, projectId.Value, compatibility.BaselineArtifactId, cancellationToken);
                return buildLibraryPackageProjectImpactAsyncResult;
            }
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
                staging.Delete(staged.Path);
            }
        }
    }

    private static async Task<object> ApplyLibraryPackageAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<McpMutationApplyRequestDto>(arguments);
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, LibraryErrorCodes.Package, request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            var deserializeStoredResponseResult25 = DeserializeStoredResponse(replay.ResponseJson);
            return deserializeStoredResponseResult25;
        }

        var preview = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await preview.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, LibraryErrorCodes.Package, StringComparison.Ordinal))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The preview does not describe a library package.");
        }

        var stored = McpPreviewService.Deserialize<StoredLibraryPackagePreview>(entry);
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        if (stored.ProjectId is null)
        {
            security.RequireAdministrator(principal);
        }
        else
        {
            await RequireActiveProjectAsync(scope, security, principal, stored.ProjectId.Value, McpPermissionDto.LibraryImport, cancellationToken);
        }

        var staging = scope.ServiceProvider.GetRequiredService<McpPackageStagingService>();
        try
        {
            await using var package = staging.OpenRead(stored.StagingPath);
            if (package.Length != stored.SizeBytes)
            {
                throw new McpProtocolException(McpProtocolErrorCodes.Conflict, "The staged library package changed after preview.");
            }

            var getRequiredServiceResult26 = scope.ServiceProvider.GetRequiredService<ILibraryCatalogService>();
            var result = await getRequiredServiceResult26.UploadVerifiedAsync(package, stored.FileName, stored.SizeBytes, stored.PackageSha256, origin: "mcp", cancellationToken: cancellationToken);
            await MarkPreviewAppliedAsync(preview, entry, cancellationToken);
            await idempotency.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, result, requestPayload, cancellationToken);
            return result;
        }
        catch (LibraryUploadException exception)when (exception.StatusCode == StatusCodes.Status409Conflict)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.Conflict, exception.Message);
        }
        catch (FileNotFoundException)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The staged library package is no longer available. Create a new preview and apply it again.", new { code = McpErrorCodes.PreviewPackageUnavailable });
        }
        finally
        {
            staging.Delete(stored.StagingPath);
        }
    }

    private static async Task<LibraryPackageProjectImpactDto?> BuildLibraryPackageProjectImpactAsync(IFlowDefinitionRepository flows, Guid projectId, string baselineArtifactId, CancellationToken cancellationToken)
    {
        var affectedFlowIds = new List<Guid>();
        var affectedNodeCount = 0;
        foreach (var flow in await flows.ListByProjectAsync(projectId, cancellationToken))
        {
            var selectManyResult = flow.Canvases.SelectMany(static canvas => canvas.Nodes);
            var nodeCount = selectManyResult.Count(node =>
            {
                var evaluatedValue27 = node.Type is NodeTypeDto.Action or NodeTypeDto.Flipflop && string.Equals(node.Ui?.LibraryId, baselineArtifactId, StringComparison.OrdinalIgnoreCase);
                return evaluatedValue27;
            });
            if (nodeCount == 0)
            {
                continue;
            }

            affectedFlowIds.Add(flow.Id);
            affectedNodeCount += nodeCount;
        }

        var orderByResult = affectedFlowIds.OrderBy(static id => id);
        var toArrayResult = orderByResult.ToArray();
        var libraryPackageProjectImpactDto = new LibraryPackageProjectImpactDto(projectId, baselineArtifactId, affectedFlowIds.Count, affectedNodeCount, toArrayResult);
        return libraryPackageProjectImpactDto;
    }

    private static async Task EnsurePackageBaselineAccessAsync(IServiceScope scope, McpPrincipal principal, Guid projectId, string? familyId, string? baselineArtifactId, CancellationToken cancellationToken)
    {
        if (principal.IsLocal || principal.IsAdministrator || (string.IsNullOrWhiteSpace(familyId) && string.IsNullOrWhiteSpace(baselineArtifactId)))
        {
            return;
        }

        var artifactId = baselineArtifactId?.Trim();
        if (string.IsNullOrWhiteSpace(artifactId) && !string.IsNullOrWhiteSpace(familyId))
        {
            var getRequiredServiceResult29 = scope.ServiceProvider.GetRequiredService<ILibraryCatalogService>();
            var evaluatedValue30 = (await getRequiredServiceResult29.ListFamiliesAsync(includeArchivedArtifacts: true, cancellationToken: cancellationToken));
            var family = evaluatedValue30.SingleOrDefault(item =>
            {
                var trimResult = familyId.Trim();
                var equalsResult28 = string.Equals(item.Id, trimResult, StringComparison.OrdinalIgnoreCase);
                return equalsResult28;
            });
            artifactId = family?.LatestArtifactId;
        }

        if (string.IsNullOrWhiteSpace(artifactId) || !await resolveIsReferencedAsyncResult())
        {
            throw new McpSecurityException(McpErrorCodes.LibraryAccessDenied, "The MCP caller cannot use the requested library baseline for this project.", StatusCodes.Status403Forbidden);
        }

        global::System.Threading.Tasks.Task<bool> resolveIsReferencedAsyncResult()
        {
            var getRequiredServiceResult31 = scope.ServiceProvider.GetRequiredService<IProjectLibraryReferenceRepository>();
            var isReferencedAsyncResult = getRequiredServiceResult31.IsReferencedAsync(projectId, artifactId, cancellationToken);
            return isReferencedAsyncResult;
        }
    }

    private static async Task<object> PreviewProjectLibraryAttachAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<ProjectLibraryAttachRequestDto>(arguments);
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        security.Require(principal, McpPermissionDto.LibraryManage, request.ProjectId);
        var libraryId = request.LibraryId?.Trim() ?? string.Empty;
        request = request with
        {
            LibraryId = libraryId
        };
        var validation = await ValidateProjectLibraryAttachAsync(scope, request, cancellationToken);
        global::SereinFlow.Contracts.ProjectLibraryAttachRequestDto evaluatedValue32 = new(request.ProjectId, libraryId);
        var stored = new StoredProjectLibraryAttachPreview(evaluatedValue32, validation);
        var getRequiredServiceResult33 = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await getRequiredServiceResult33.CreateAsync(ProjectErrorCodes.LibraryAttach, principal, request.ProjectId, null, null, stored, cancellationToken);
        var projectLibraryAttachPreviewDto = new ProjectLibraryAttachPreviewDto(entry.Id, request.ProjectId, libraryId, entry.ExpiresAt, entry.PreviewFingerprint, validation.Count == 0, validation);
        return projectLibraryAttachPreviewDto;
    }

    private static async Task<object> ApplyProjectLibraryAttachAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<McpMutationApplyRequestDto>(arguments);
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, ProjectErrorCodes.LibraryAttach, request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            var deserializeStoredResponseResult34 = DeserializeStoredResponse(replay.ResponseJson);
            return deserializeStoredResponseResult34;
        }

        var preview = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await preview.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, ProjectErrorCodes.LibraryAttach, StringComparison.Ordinal))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The preview does not describe a project library attachment.");
        }

        var stored = McpPreviewService.Deserialize<StoredProjectLibraryAttachPreview>(entry);
        var getRequiredServiceResult35 = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        getRequiredServiceResult35.Require(principal, McpPermissionDto.LibraryManage, stored.Request.ProjectId);
        var currentDiagnostics = await ValidateProjectLibraryAttachAsync(scope, stored.Request, cancellationToken);
        if (currentDiagnostics.Count > 0)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "The project library attachment is no longer valid. Create a new preview and review it again.", new { code = McpErrorCodes.ValidationFailed, diagnostics = currentDiagnostics });
        }

        var getRequiredServiceResult36 = scope.ServiceProvider.GetRequiredService<ProjectLibraryService>();
        var result = await getRequiredServiceResult36.AddAsync(stored.Request.ProjectId, stored.Request.LibraryId, cancellationToken, origin: "mcp");
        if (!result.IsSuccess)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, result.Message ?? "The project library attachment failed.", result.Code);
        }

        await MarkPreviewAppliedAsync(preview, entry, cancellationToken);
        await idempotency.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, result, requestPayload, cancellationToken);
        return result;
    }

    private static async Task<IReadOnlyList<ValidationDiagnosticDto>> ValidateProjectLibraryAttachAsync(IServiceScope scope, ProjectLibraryAttachRequestDto request, CancellationToken cancellationToken)
    {
        var diagnostics = new List<ValidationDiagnosticDto>();
        var getRequiredServiceResult43 = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var project = await getRequiredServiceResult43.FindAsync(request.ProjectId, cancellationToken);
        if (project is null)
        {
            global::SereinFlow.Contracts.ValidationDiagnosticDto evaluatedValue38 = new(ProjectErrorCodes.NotFound, "The project was not found. 未找到项目。", "projectId");
            diagnostics.Add(evaluatedValue38);
        }
        else if (project.Status == SereinFlow.Domain.ProjectStatus.Archived)
        {
            global::SereinFlow.Contracts.ValidationDiagnosticDto evaluatedValue37 = new(ProjectErrorCodes.Archived, "Archived projects cannot change library references. 已归档项目不能修改类库引用。", "projectId");
            diagnostics.Add(evaluatedValue37);
        }

        var libraryId = request.LibraryId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(libraryId))
        {
            global::SereinFlow.Contracts.ValidationDiagnosticDto evaluatedValue39 = new(LibraryErrorCodes.IdRequired, "The library ID is required. 类库 ID 不能为空。", "libraryId");
            diagnostics.Add(evaluatedValue39);
        }

        var library = string.IsNullOrWhiteSpace(libraryId) ? null : await resolveFindAsyncResult();
        if (library is null)
        {
            global::SereinFlow.Contracts.ValidationDiagnosticDto evaluatedValue41 = new(LibraryErrorCodes.NotFound, "The library artifact was not found. 未找到类库制品。", "libraryId");
            diagnostics.Add(evaluatedValue41);
        }
        else if (library.Lifecycle != LibraryLifecycleDto.Available)
        {
            global::SereinFlow.Contracts.ValidationDiagnosticDto evaluatedValue40 = new(LibraryErrorCodes.Archived, "Archived library artifacts cannot be attached. 已归档类库制品不能被接入。", "libraryId");
            diagnostics.Add(evaluatedValue40);
        }

        if (library is not null && await resolveIsReferencedAsyncResult45())
        {
            global::SereinFlow.Contracts.ValidationDiagnosticDto evaluatedValue42 = new(ProjectLibraryErrorCodes.AlreadyReferenced, "The project already references this library artifact. 项目已引用该类库制品。", "libraryId");
            diagnostics.Add(evaluatedValue42);
        }

        return diagnostics;
        global::System.Threading.Tasks.Task<global::SereinFlow.Contracts.LibraryDto?> resolveFindAsyncResult()
        {
            var getRequiredServiceResult44 = scope.ServiceProvider.GetRequiredService<ILibraryCatalogService>();
            var findAsyncResult = getRequiredServiceResult44.FindAsync(libraryId, cancellationToken);
            return findAsyncResult;
        }

        global::System.Threading.Tasks.Task<bool> resolveIsReferencedAsyncResult45()
        {
            var getRequiredServiceResult46 = scope.ServiceProvider.GetRequiredService<IProjectLibraryReferenceRepository>();
            var isReferencedAsyncResult47 = getRequiredServiceResult46.IsReferencedAsync(request.ProjectId, library.Id, cancellationToken);
            return isReferencedAsyncResult47;
        }
    }
}

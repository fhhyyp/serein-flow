using System.Collections.Generic;
using System.Threading.Tasks;
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
/// Flow patch, publish, rollback and version comparison MCP tools.
/// </summary>
internal static class McpFlowToolHandlers
{
    internal static Task<object> PreviewPatchAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult = context.RequirePrincipal();
        var previewFlowPatchAsyncResult = PreviewFlowPatchAsync(context.Scope, requirePrincipalResult, arguments, cancellationToken);
        return previewFlowPatchAsyncResult;
    }

    internal static Task<object> ApplyPatchAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult1 = context.RequirePrincipal();
        var applyFlowPatchAsyncResult = ApplyFlowPatchAsync(context.Scope, requirePrincipalResult1, arguments, cancellationToken);
        return applyFlowPatchAsyncResult;
    }

    internal static Task<object> CompareVersionsAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult2 = context.RequirePrincipal();
        var compareFlowVersionsAsyncResult = CompareFlowVersionsAsync(context.Scope, requirePrincipalResult2, arguments, cancellationToken);
        return compareFlowVersionsAsyncResult;
    }

    internal static Task<object> PreviewPublishAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult3 = context.RequirePrincipal();
        var previewPublishAsyncResult = PreviewPublishAsync(context.Scope, requirePrincipalResult3, arguments, cancellationToken);
        return previewPublishAsyncResult;
    }

    internal static Task<object> ApplyPublishAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult4 = context.RequirePrincipal();
        var applyPublishAsyncResult = ApplyPublishAsync(context.Scope, requirePrincipalResult4, arguments, cancellationToken);
        return applyPublishAsyncResult;
    }

    internal static Task<object> PreviewRollbackAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult5 = context.RequirePrincipal();
        var previewRollbackAsyncResult = PreviewRollbackAsync(context.Scope, requirePrincipalResult5, arguments, cancellationToken);
        return previewRollbackAsyncResult;
    }

    internal static Task<object> ApplyRollbackAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult6 = context.RequirePrincipal();
        var applyRollbackAsyncResult = ApplyRollbackAsync(context.Scope, requirePrincipalResult6, arguments, cancellationToken);
        return applyRollbackAsyncResult;
    }

    private static async Task<object> PreviewFlowPatchAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        NormalizedFlowPatchRequest normalized;
        try
        {
            var getRequiredServiceResult = scope.ServiceProvider.GetRequiredService<FlowPatchContractNormalizer>();
            normalized = getRequiredServiceResult.Normalize(arguments);
        }
        catch (FlowPatchContractException exception)
        {
            throw InvalidFlowPatchContract(exception);
        }

        var request = normalized.Request;
        if (request.ProjectId == Guid.Empty || request.FlowId == Guid.Empty || request.ExpectedDevelopmentVersion < 1)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The flow patch request is invalid.");
        }

        var getRequiredServiceResult9 = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        await RequireActiveProjectAsync(scope, getRequiredServiceResult9, principal, request.ProjectId, McpPermissionDto.FlowWrite, cancellationToken);
        var flows = scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        var current = await flows.FindAsync(request.ProjectId, request.FlowId, cancellationToken) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The flow definition was not found.");
        if (current.Version != request.ExpectedDevelopmentVersion)
        {
            throw VersionConflict(current.Version);
        }

        FlowDefinitionDto candidate;
        try
        {
            var getRequiredServiceResult7 = scope.ServiceProvider.GetRequiredService<FlowPatchContractNormalizer>();
            getRequiredServiceResult7.ValidateReferences(current, request.Operations);
            var getRequiredServiceResult8 = scope.ServiceProvider.GetRequiredService<FlowPatchService>();
            candidate = getRequiredServiceResult8.Apply(current, request.Operations);
        }
        catch (FlowPatchContractException exception)
        {
            throw InvalidFlowPatchContract(exception);
        }
        catch (JsonException exception)
        {
            throw InvalidPatchValue(exception);
        }
        catch (Exception exception)when (exception is ArgumentException or InvalidOperationException)
        {
            var newGuidResult = Guid.NewGuid();
            var toStringResult = newGuidResult.ToString("N");
            var evaluatedValue = new
            {
                code = McpErrorCodes.FlowPatchReferenceInvalid,
                diagnosticId = toStringResult,
                fieldPath = "$.operations",
                expected = "a valid operation sequence for the current flow",
                schemaVersion = FlowPatchContract.CurrentSchemaVersion,
                remediation = "Read the current flow edit model and create a new typed patch."
            };
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The flow patch references are invalid.", evaluatedValue);
        }

        var preparation = await resolvePrepareAsyncResult() ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The flow definition was not found.");
        var getRequiredServiceResult11 = scope.ServiceProvider.GetRequiredService<FlowDiffService>();
        var compareResult = getRequiredServiceResult11.Compare(preparation.Current, preparation.Candidate);
        var stored = new StoredFlowPatchPreview(normalized.LegacyRequest, preparation.Candidate, preparation.Validation, compareResult, normalized.Request, normalized.Warnings);
        var getRequiredServiceResult12 = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var preview = await getRequiredServiceResult12.CreateAsync(FlowErrorCodes.Patch, principal, request.ProjectId, request.FlowId, request.ExpectedDevelopmentVersion, stored, cancellationToken);
        var toFlowPatchPreviewResult = McpReadModelToolHandlers.ToFlowPatchPreview(preview, stored);
        return toFlowPatchPreviewResult;
        Task<FlowDefinitionPreparationResult?> resolvePrepareAsyncResult()
        {
            var getRequiredServiceResult10 = scope.ServiceProvider.GetRequiredService<FlowDefinitionWriteService>();
            var prepareAsyncResult = getRequiredServiceResult10.PrepareAsync(request.ProjectId, request.FlowId, candidate, cancellationToken);
            return prepareAsyncResult;
        }
    }

    private static async Task<object> ApplyFlowPatchAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<McpMutationApplyRequestDto>(arguments);
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, FlowErrorCodes.Patch, request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            var deserializeStoredResponseResult = DeserializeStoredResponse(replay.ResponseJson);
            return deserializeStoredResponseResult;
        }

        var previews = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await previews.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, FlowErrorCodes.Patch, StringComparison.Ordinal))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The preview does not describe a flow patch.");
        }

        var stored = McpPreviewService.Deserialize<StoredFlowPatchPreview>(entry);
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        await RequireActiveProjectAsync(scope, security, principal, stored.Request.ProjectId, McpPermissionDto.FlowWrite, cancellationToken);
        if (!stored.Validation.IsValid || stored.Diff.Changes.Count == 0)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "The flow patch preview cannot be applied.");
        }

        var flows = scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        var current = await flows.FindAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The flow definition was not found.");
        if (current.Version != stored.Request.ExpectedDevelopmentVersion)
        {
            throw VersionConflict(current.Version);
        }

        var getRequiredServiceResult13 = scope.ServiceProvider.GetRequiredService<FlowDiffService>();
        var currentDiff = getRequiredServiceResult13.Compare(current, stored.CandidateDefinition);
        if (!string.Equals(currentDiff.BaseChecksum, stored.Diff.BaseChecksum, StringComparison.Ordinal))
        {
            throw VersionConflict(current.Version);
        }

        var getRequiredServiceResult14 = scope.ServiceProvider.GetRequiredService<FlowDefinitionWriteService>();
        var write = await getRequiredServiceResult14.WriteAsync(stored.Request.ProjectId, stored.Request.FlowId, stored.CandidateDefinition, stored.Request.ExpectedDevelopmentVersion, cancellationToken, origin: "mcp");
        if (write.Status == FlowDefinitionWriteStatus.Conflict)
        {
            throw VersionConflict(write.CurrentVersion);
        }

        if (write.Status == FlowDefinitionWriteStatus.Archived)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "Archived projects cannot save flow definitions.");
        }

        if (write.Status != FlowDefinitionWriteStatus.Saved || write.Saved is null)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "The flow patch preview cannot be applied.");
        }

        var saved = write.Saved;
        await MarkPreviewAppliedAsync(previews, entry, cancellationToken);
        var persisted = await flows.FindAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken);
        if (persisted is null || persisted.Version != saved.Version || !string.Equals(persisted.Checksum, saved.Checksum, StringComparison.Ordinal) || !resolveEqualsResult())
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InternalError, "The flow patch was committed but authoritative verification failed.", new { code = McpErrorCodes.PostApplyVerificationFailed });
        }

        var response = FlowDiffService.RedactSensitive(persisted);
        await idempotency.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, response, requestPayload, cancellationToken);
        return response;
        bool resolveEqualsResult()
        {
            var getChecksumResult = FlowDiffService.GetChecksum(persisted);
            var equalsResult = string.Equals(persisted.Checksum, getChecksumResult, StringComparison.Ordinal);
            return equalsResult;
        }
    }

    private static async Task<object> CompareFlowVersionsAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var projectId = GetGuid(arguments, "projectId");
        var flowId = GetGuid(arguments, "flowId");
        var getRequiredStringResult = GetRequiredString(arguments, "track");
        var track = ParseTrack(getRequiredStringResult);
        var fromVersion = GetLong(arguments, "fromVersion");
        var toVersion = GetLong(arguments, "toVersion");
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        var service = scope.ServiceProvider.GetRequiredService<AiReadModelService>();
        var fromVersionDetail = await service.GetFlowVersionAsync(projectId, flowId, fromVersion, cancellationToken) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The source flow version was not found.");
        var toVersionDetail = await service.GetFlowVersionAsync(projectId, flowId, toVersion, cancellationToken) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The target flow version was not found.");
        if (fromVersionDetail.Version.Track != track || toVersionDetail.Version.Track != track)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The requested versions are not on the selected track.");
        }

        var diffService = scope.ServiceProvider.GetRequiredService<FlowDiffService>();
        var evaluatedValue15 = fromVersionDetail with
        {
            Definition = FlowDiffService.RedactSensitive(fromVersionDetail.Definition)
        };
        var evaluatedValue16 = toVersionDetail with
        {
            Definition = FlowDiffService.RedactSensitive(toVersionDetail.Definition)
        };
        var compareResult17 = diffService.Compare(fromVersionDetail.Definition, toVersionDetail.Definition);
        var redactSensitiveResult = FlowDiffService.RedactSensitive(compareResult17);
        var flowVersionComparisonDto = new FlowVersionComparisonDto(evaluatedValue15, evaluatedValue16, redactSensitiveResult);
        return flowVersionComparisonDto;
    }

    private static async Task<object> PreviewPublishAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<PublishFlowPreviewRequestDto>(arguments);
        var getRequiredServiceResult18 = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        await RequireActiveProjectAsync(scope, getRequiredServiceResult18, principal, request.ProjectId, McpPermissionDto.FlowPublish, cancellationToken);
        var flow = await resolveFindAsyncResult() ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The flow definition was not found.");
        if (flow.Version != request.ExpectedDevelopmentVersion)
        {
            throw VersionConflict(flow.Version);
        }

        var executableValidation = await ValidateExecutableFlowAsync(scope, request.ProjectId, flow, cancellationToken);
        var productionVersions = scope.ServiceProvider.GetRequiredService<IFlowVersionRepository>();
        var expectedProductionVersion = await productionVersions.FindProductionVersionAsync(request.ProjectId, request.FlowId, cancellationToken);
        var diagnostics = executableValidation.Diagnostics.ToArray();
        var production = await productionVersions.FindProductionDefinitionAsync(request.ProjectId, request.FlowId, cancellationToken);
        var getRequiredServiceResult20 = scope.ServiceProvider.GetRequiredService<FlowDiffService>();
        var diff = getRequiredServiceResult20.Compare(production ?? flow, flow);
        if (production is not null && diff.Changes.Count == 0)
        {
            var validationDiagnosticDto = new ValidationDiagnosticDto(FlowErrorCodes.PublishNoChange, "The current development version is already the production definition. 当前开发版本已经是生产定义。", null);
            var appendResult = diagnostics.Append(validationDiagnosticDto);
            diagnostics = appendResult.ToArray();
        }

        var flowValidationResultDto = new FlowValidationResultDto(diagnostics.Length == 0, diagnostics);
        var result = new StoredPublishPreview(request, flowValidationResultDto, diff, HasProductionVersion: production is not null, ExpectedProductionVersion: expectedProductionVersion);
        var getRequiredServiceResult21 = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await getRequiredServiceResult21.CreateAsync("flow.publish", principal, request.ProjectId, request.FlowId, request.ExpectedDevelopmentVersion, result, cancellationToken);
        var redactSensitiveResult22 = FlowDiffService.RedactSensitive(diff);
        return new
        {
            previewId = entry.Id,
            entry.ExpiresAt,
            entry.PreviewFingerprint,
            isPreviewOnly = true,
            expectedProductionVersion,
            canApply = result.Validation.IsValid && (production is null || diff.Changes.Count > 0),
            validation = result.Validation,
            diff = redactSensitiveResult22
        };
        Task<FlowDefinitionDto?> resolveFindAsyncResult()
        {
            var getRequiredServiceResult19 = scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
            var findAsyncResult = getRequiredServiceResult19.FindAsync(request.ProjectId, request.FlowId, cancellationToken);
            return findAsyncResult;
        }
    }

    private static async Task<object> ApplyPublishAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<McpMutationApplyRequestDto>(arguments);
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, "flow.publish", request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            var deserializeStoredResponseResult23 = DeserializeStoredResponse(replay.ResponseJson);
            return deserializeStoredResponseResult23;
        }

        var preview = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await preview.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        var stored = McpPreviewService.Deserialize<StoredPublishPreview>(entry);
        var getRequiredServiceResult24 = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        await RequireActiveProjectAsync(scope, getRequiredServiceResult24, principal, stored.Request.ProjectId, McpPermissionDto.FlowPublish, cancellationToken);
        var versions = scope.ServiceProvider.GetRequiredService<IFlowVersionRepository>();
        var current = await resolveFindAsyncResult25() ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The flow definition was not found.");
        if (current.Version != stored.Request.ExpectedDevelopmentVersion)
        {
            throw VersionConflict(current.Version);
        }

        var currentValidation = await ValidateExecutableFlowAsync(scope, stored.Request.ProjectId, current, cancellationToken);
        RequireValidMutation(currentValidation, "The current development flow cannot be published.");
        var currentProductionVersion = await versions.FindProductionVersionAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken);
        if (currentProductionVersion != stored.ExpectedProductionVersion)
        {
            throw VersionConflict(currentProductionVersion);
        }

        var currentProduction = await versions.FindProductionDefinitionAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken);
        if (currentProduction is not null && resolveEqualsResult28())
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "The development definition is already published.");
        }

        var result = await versions.PublishAsync(stored.Request.ProjectId, stored.Request.FlowId, stored.Request.ExpectedDevelopmentVersion, stored.Request.Remark, expectedProductionVersion: stored.ExpectedProductionVersion, cancellationToken: cancellationToken);
        if (!result.IsCommitted)
        {
            throw VersionConflict(result.CurrentHeadVersion);
        }

        await MarkPreviewAppliedAsync(preview, entry, cancellationToken);
        var redactSensitiveResult32 = FlowDiffService.RedactSensitive(stored.Diff);
        FlowDefinitionDto? evaluatedValue33 = result.DevelopmentDefinition is null ? null : FlowDiffService.RedactSensitive(result.DevelopmentDefinition);
        var response = new FlowVersionMutationDto(result.Version!, redactSensitiveResult32, evaluatedValue33);
        await idempotency.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, response, requestPayload, cancellationToken);
        return response;
        Task<FlowDefinitionDto?> resolveFindAsyncResult25()
        {
            var getRequiredServiceResult26 = scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
            var findAsyncResult27 = getRequiredServiceResult26.FindAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken);
            return findAsyncResult27;
        }

        bool resolveEqualsResult28()
        {
            var getChecksumResult29 = FlowDiffService.GetChecksum(currentProduction);
            var getChecksumResult30 = FlowDiffService.GetChecksum(current);
            var equalsResult31 = string.Equals(getChecksumResult29, getChecksumResult30, StringComparison.Ordinal);
            return equalsResult31;
        }
    }

    private static async Task<object> PreviewRollbackAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<RollbackFlowPreviewRequestDto>(arguments);
        if (request.SourceVersion < 1 || request.ExpectedHeadVersion < 1 || !Enum.IsDefined(request.Track))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The rollback request is invalid.");
        }

        var getRequiredServiceResult35 = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        await RequireActiveProjectAsync(scope, getRequiredServiceResult35, principal, request.ProjectId, McpPermissionDto.FlowRollback, cancellationToken);
        var versions = scope.ServiceProvider.GetRequiredService<IFlowVersionRepository>();
        var source = await versions.FindVersionAsync(request.ProjectId, request.FlowId, request.SourceVersion, cancellationToken) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The rollback source version was not found.");
        if (source.Version.Track != request.Track)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The rollback source version is not on the selected track.");
        }

        var evaluatedValue36 = (await versions.ListVersionsAsync(request.ProjectId, request.FlowId, request.Track, cancellationToken));
        var head = evaluatedValue36.FirstOrDefault(item => item.IsCurrent);
        if (head is null || head.Version != request.ExpectedHeadVersion)
        {
            throw VersionConflict(head?.Version);
        }

        var current = await versions.FindVersionAsync(request.ProjectId, request.FlowId, head.Version, cancellationToken) ?? source;
        var validation = FlowDefinitionContractValidator.ValidateForExecution(source.Definition);
        var getRequiredServiceResult37 = scope.ServiceProvider.GetRequiredService<ProjectLibraryService>();
        var libraryValidation = await getRequiredServiceResult37.ValidateFlowLibrariesAsync(request.ProjectId, source.Definition, cancellationToken);
        var concatResult38 = validation.Diagnostics.Concat(libraryValidation.Diagnostics);
        var diagnostics = concatResult38.ToArray();
        var getRequiredServiceResult39 = scope.ServiceProvider.GetRequiredService<FlowDiffService>();
        var diff = getRequiredServiceResult39.Compare(current.Definition, source.Definition);
        if (diff.Changes.Count == 0)
        {
            IEnumerable<ValidationDiagnosticDto> evaluatedValue34 = [new ValidationDiagnosticDto(FlowErrorCodes.RollbackNoChange, "The selected version is already the current track head. 选定版本已经是当前轨道头版本。", null)];
            var concatResult = diagnostics.Concat(evaluatedValue34);
            diagnostics = concatResult.ToArray();
        }

        var flowValidationResultDto40 = new FlowValidationResultDto(diagnostics.Length == 0, diagnostics);
        var stored = new StoredRollbackPreview(request, flowValidationResultDto40, diff);
        var getRequiredServiceResult41 = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await getRequiredServiceResult41.CreateAsync("flow.rollback", principal, request.ProjectId, request.FlowId, request.ExpectedHeadVersion, stored, cancellationToken);
        var redactSensitiveResult42 = FlowDiffService.RedactSensitive(diff);
        return new
        {
            previewId = entry.Id,
            entry.ExpiresAt,
            entry.PreviewFingerprint,
            isPreviewOnly = true,
            canApply = stored.Validation.IsValid,
            validation = stored.Validation,
            diff = redactSensitiveResult42
        };
    }

    private static async Task<object> ApplyRollbackAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<McpMutationApplyRequestDto>(arguments);
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, "flow.rollback", request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            var deserializeStoredResponseResult43 = DeserializeStoredResponse(replay.ResponseJson);
            return deserializeStoredResponseResult43;
        }

        var preview = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await preview.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        var stored = McpPreviewService.Deserialize<StoredRollbackPreview>(entry);
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        await RequireActiveProjectAsync(scope, security, principal, stored.Request.ProjectId, McpPermissionDto.FlowRollback, cancellationToken);
        long? currentHead;
        if (stored.Request.Track == FlowVersionTrackDto.Development)
        {
            var currentDefinitions = scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
            var currentDefinition = await currentDefinitions.FindAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken);
            currentHead = currentDefinition?.Version;
        }
        else
        {
            currentHead = await resolveFindProductionVersionAsyncResult();
        }
        if (currentHead != stored.Request.ExpectedHeadVersion)
        {
            throw VersionConflict(currentHead);
        }

        var source = await resolveFindVersionAsyncResult() ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The rollback source version was not found.");
        if (source.Version.Track != stored.Request.Track)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The rollback source version is not on the selected track.");
        }

        var currentValidation = await ValidateExecutableFlowAsync(scope, stored.Request.ProjectId, source.Definition, cancellationToken);
        RequireValidMutation(currentValidation, "The rollback source flow cannot be executed.");
        var getRequiredServiceResult48 = scope.ServiceProvider.GetRequiredService<IFlowVersionRepository>();
        var result = await getRequiredServiceResult48.RollbackAsync(stored.Request.ProjectId, stored.Request.FlowId, stored.Request.SourceVersion, stored.Request.Track, stored.Request.ExpectedHeadVersion, cancellationToken);
        if (!result.IsCommitted)
        {
            throw VersionConflict(result.CurrentHeadVersion);
        }

        await MarkPreviewAppliedAsync(preview, entry, cancellationToken);
        var redactSensitiveResult49 = FlowDiffService.RedactSensitive(stored.Diff);
        FlowDefinitionDto? evaluatedValue50 = result.DevelopmentDefinition is null ? null : FlowDiffService.RedactSensitive(result.DevelopmentDefinition);
        var response = new FlowVersionMutationDto(result.Version!, redactSensitiveResult49, evaluatedValue50);
        await idempotency.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, response, requestPayload, cancellationToken);
        return response;
        Task<long?> resolveFindProductionVersionAsyncResult()
        {
            var getRequiredServiceResult46 = scope.ServiceProvider.GetRequiredService<IFlowVersionRepository>();
            var findProductionVersionAsyncResult = getRequiredServiceResult46.FindProductionVersionAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken);
            return findProductionVersionAsyncResult;
        }

        Task<FlowVersionDetailDto?> resolveFindVersionAsyncResult()
        {
            var getRequiredServiceResult47 = scope.ServiceProvider.GetRequiredService<IFlowVersionRepository>();
            var findVersionAsyncResult = getRequiredServiceResult47.FindVersionAsync(stored.Request.ProjectId, stored.Request.FlowId, stored.Request.SourceVersion, cancellationToken);
            return findVersionAsyncResult;
        }
    }
}

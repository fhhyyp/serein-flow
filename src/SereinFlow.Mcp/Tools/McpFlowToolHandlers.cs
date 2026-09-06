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
        => PreviewFlowPatchAsync(context.Scope, context.RequirePrincipal(), arguments, cancellationToken);

    internal static Task<object> ApplyPatchAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => ApplyFlowPatchAsync(context.Scope, context.RequirePrincipal(), arguments, cancellationToken);

    internal static Task<object> CompareVersionsAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => CompareFlowVersionsAsync(context.Scope, context.RequirePrincipal(), arguments, cancellationToken);

    internal static Task<object> PreviewPublishAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => PreviewPublishAsync(context.Scope, context.RequirePrincipal(), arguments, cancellationToken);

    internal static Task<object> ApplyPublishAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => ApplyPublishAsync(context.Scope, context.RequirePrincipal(), arguments, cancellationToken);

    internal static Task<object> PreviewRollbackAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => PreviewRollbackAsync(context.Scope, context.RequirePrincipal(), arguments, cancellationToken);

    internal static Task<object> ApplyRollbackAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => ApplyRollbackAsync(context.Scope, context.RequirePrincipal(), arguments, cancellationToken);

    private static async Task<object> PreviewFlowPatchAsync(
        IServiceScope scope,
        McpPrincipal principal,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        NormalizedFlowPatchRequest normalized;
        try
        {
            normalized = scope.ServiceProvider.GetRequiredService<FlowPatchContractNormalizer>().Normalize(arguments);
        }
        catch (FlowPatchContractException exception)
        {
            throw InvalidFlowPatchContract(exception);
        }

        var request = normalized.Request;
        if (request.ProjectId == Guid.Empty || request.FlowId == Guid.Empty || request.ExpectedDevelopmentVersion < 1)
            throw new McpProtocolException(-32602, "The flow patch request is invalid.");
        await RequireActiveProjectAsync(
            scope,
            scope.ServiceProvider.GetRequiredService<McpSecurityService>(),
            principal,
            request.ProjectId,
            McpPermissionDto.FlowWrite,
            cancellationToken);
        var flows = scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        var current = await flows.FindAsync(request.ProjectId, request.FlowId, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The flow definition was not found.");
        if (current.Version != request.ExpectedDevelopmentVersion)
            throw VersionConflict(current.Version);

        FlowDefinitionDto candidate;
        try
        {
            scope.ServiceProvider.GetRequiredService<FlowPatchContractNormalizer>()
                .ValidateReferences(current, request.Operations);
            candidate = scope.ServiceProvider.GetRequiredService<FlowPatchService>().Apply(current, request.Operations);
        }
        catch (FlowPatchContractException exception)
        {
            throw InvalidFlowPatchContract(exception);
        }
        catch (JsonException exception)
        {
            throw InvalidPatchValue(exception);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw new McpProtocolException(
                -32602,
                "The flow patch references are invalid.",
                new
                {
                    code = "mcp.flow_patch.reference_invalid",
                    diagnosticId = Guid.NewGuid().ToString("N"),
                    fieldPath = "$.operations",
                    expected = "a valid operation sequence for the current flow",
                    schemaVersion = FlowPatchContract.CurrentSchemaVersion,
                    remediation = "Read the current flow edit model and create a new typed patch."
                });
        }

        var preparation = await scope.ServiceProvider.GetRequiredService<FlowDefinitionWriteService>()
            .PrepareAsync(request.ProjectId, request.FlowId, candidate, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The flow definition was not found.");
        var stored = new StoredFlowPatchPreview(
            normalized.LegacyRequest,
            preparation.Candidate,
            preparation.Validation,
            scope.ServiceProvider.GetRequiredService<FlowDiffService>().Compare(preparation.Current, preparation.Candidate),
            normalized.Request,
            normalized.Warnings);
        var preview = await scope.ServiceProvider.GetRequiredService<McpPreviewService>().CreateAsync(
            "flow.patch",
            principal,
            request.ProjectId,
            request.FlowId,
            request.ExpectedDevelopmentVersion,
            stored,
            cancellationToken);
        return McpReadModelToolHandlers.ToFlowPatchPreview(preview, stored);
    }

    private static async Task<object> ApplyFlowPatchAsync(
        IServiceScope scope,
        McpPrincipal principal,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var request = Deserialize<McpMutationApplyRequestDto>(arguments);
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, "flow.patch", request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
            return DeserializeStoredResponse(replay.ResponseJson);
        var previews = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await previews.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, "flow.patch", StringComparison.Ordinal))
            throw new McpProtocolException(-32602, "The preview does not describe a flow patch.");
        var stored = McpPreviewService.Deserialize<StoredFlowPatchPreview>(entry);
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        await RequireActiveProjectAsync(
            scope,
            security,
            principal,
            stored.Request.ProjectId,
            McpPermissionDto.FlowWrite,
            cancellationToken);
        if (!stored.Validation.IsValid || stored.Diff.Changes.Count == 0)
            throw new McpProtocolException(-32011, "The flow patch preview cannot be applied.");
        var flows = scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        var current = await flows.FindAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The flow definition was not found.");
        if (current.Version != stored.Request.ExpectedDevelopmentVersion)
            throw VersionConflict(current.Version);
        var currentDiff = scope.ServiceProvider.GetRequiredService<FlowDiffService>().Compare(current, stored.CandidateDefinition);
        if (!string.Equals(currentDiff.BaseChecksum, stored.Diff.BaseChecksum, StringComparison.Ordinal))
            throw VersionConflict(current.Version);
        var write = await scope.ServiceProvider.GetRequiredService<FlowDefinitionWriteService>().WriteAsync(
            stored.Request.ProjectId,
            stored.Request.FlowId,
            stored.CandidateDefinition,
            stored.Request.ExpectedDevelopmentVersion,
            cancellationToken,
            origin: "mcp");
        if (write.Status == FlowDefinitionWriteStatus.Conflict)
            throw VersionConflict(write.CurrentVersion);
        if (write.Status == FlowDefinitionWriteStatus.Archived)
            throw new McpProtocolException(-32011, "Archived projects cannot save flow definitions.");
        if (write.Status != FlowDefinitionWriteStatus.Saved || write.Saved is null)
            throw new McpProtocolException(-32011, "The flow patch preview cannot be applied.");
        var saved = write.Saved;
        await MarkPreviewAppliedAsync(previews, entry, cancellationToken);
        var persisted = await flows.FindAsync(
            stored.Request.ProjectId,
            stored.Request.FlowId,
            cancellationToken);
        if (persisted is null
            || persisted.Version != saved.Version
            || !string.Equals(persisted.Checksum, saved.Checksum, StringComparison.Ordinal)
            || !string.Equals(persisted.Checksum, FlowDiffService.GetChecksum(persisted), StringComparison.Ordinal))
        {
            throw new McpProtocolException(
                -32603,
                "The flow patch was committed but authoritative verification failed.",
                new { code = "mcp.post_apply_verification_failed" });
        }
        var response = FlowDiffService.RedactSensitive(persisted);
        await idempotency.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, response, requestPayload, cancellationToken);
        return response;
    }

    private static async Task<object> CompareFlowVersionsAsync(
        IServiceScope scope,
        McpPrincipal principal,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var projectId = GetGuid(arguments, "projectId");
        var flowId = GetGuid(arguments, "flowId");
        var track = ParseTrack(GetRequiredString(arguments, "track"));
        var fromVersion = GetLong(arguments, "fromVersion");
        var toVersion = GetLong(arguments, "toVersion");
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        var service = scope.ServiceProvider.GetRequiredService<AiReadModelService>();
        var fromVersionDetail = await service.GetFlowVersionAsync(projectId, flowId, fromVersion, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The source flow version was not found.");
        var toVersionDetail = await service.GetFlowVersionAsync(projectId, flowId, toVersion, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The target flow version was not found.");
        if (fromVersionDetail.Version.Track != track || toVersionDetail.Version.Track != track)
            throw new McpProtocolException(-32004, "The requested versions are not on the selected track.");
        var diffService = scope.ServiceProvider.GetRequiredService<FlowDiffService>();
        return new FlowVersionComparisonDto(
            fromVersionDetail with { Definition = FlowDiffService.RedactSensitive(fromVersionDetail.Definition) },
            toVersionDetail with { Definition = FlowDiffService.RedactSensitive(toVersionDetail.Definition) },
            FlowDiffService.RedactSensitive(diffService.Compare(fromVersionDetail.Definition, toVersionDetail.Definition)));
    }

    private static async Task<object> PreviewPublishAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<PublishFlowPreviewRequestDto>(arguments);
        await RequireActiveProjectAsync(
            scope,
            scope.ServiceProvider.GetRequiredService<McpSecurityService>(),
            principal,
            request.ProjectId,
            McpPermissionDto.FlowPublish,
            cancellationToken);
        var flow = await scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>().FindAsync(request.ProjectId, request.FlowId, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The flow definition was not found.");
        if (flow.Version != request.ExpectedDevelopmentVersion)
            throw VersionConflict(flow.Version);
        var executableValidation = await ValidateExecutableFlowAsync(scope, request.ProjectId, flow, cancellationToken);
        var productionVersions = scope.ServiceProvider.GetRequiredService<IFlowVersionRepository>();
        var expectedProductionVersion = await productionVersions.FindProductionVersionAsync(request.ProjectId, request.FlowId, cancellationToken);
        var diagnostics = executableValidation.Diagnostics.ToArray();
        var production = await productionVersions.FindProductionDefinitionAsync(request.ProjectId, request.FlowId, cancellationToken);
        var diff = scope.ServiceProvider.GetRequiredService<FlowDiffService>().Compare(production ?? flow, flow);
        if (production is not null && diff.Changes.Count == 0)
        {
            diagnostics = diagnostics.Append(new ValidationDiagnosticDto(
                "flow.publish_no_change",
                "The current development version is already the production definition. 当前开发版本已经是生产定义。",
                null)).ToArray();
        }
        var result = new StoredPublishPreview(
            request,
            new FlowValidationResultDto(diagnostics.Length == 0, diagnostics),
            diff,
            HasProductionVersion: production is not null,
            ExpectedProductionVersion: expectedProductionVersion);
        var entry = await scope.ServiceProvider.GetRequiredService<McpPreviewService>().CreateAsync("flow.publish", principal, request.ProjectId, request.FlowId, request.ExpectedDevelopmentVersion, result, cancellationToken);
        return new
        {
            previewId = entry.Id,
            entry.ExpiresAt,
            entry.PreviewFingerprint,
            isPreviewOnly = true,
            expectedProductionVersion,
            canApply = result.Validation.IsValid && (production is null || diff.Changes.Count > 0),
            validation = result.Validation,
            diff = FlowDiffService.RedactSensitive(diff),
        };
    }

    private static async Task<object> ApplyPublishAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<McpMutationApplyRequestDto>(arguments);
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, "flow.publish", request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
            return DeserializeStoredResponse(replay.ResponseJson);
        var preview = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await preview.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        var stored = McpPreviewService.Deserialize<StoredPublishPreview>(entry);
        await RequireActiveProjectAsync(
            scope,
            scope.ServiceProvider.GetRequiredService<McpSecurityService>(),
            principal,
            stored.Request.ProjectId,
            McpPermissionDto.FlowPublish,
            cancellationToken);
        var versions = scope.ServiceProvider.GetRequiredService<IFlowVersionRepository>();
        var current = await scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>().FindAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The flow definition was not found.");
        if (current.Version != stored.Request.ExpectedDevelopmentVersion)
            throw VersionConflict(current.Version);
        var currentValidation = await ValidateExecutableFlowAsync(scope, stored.Request.ProjectId, current, cancellationToken);
        RequireValidMutation(currentValidation, "The current development flow cannot be published.");
        var currentProductionVersion = await versions.FindProductionVersionAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken);
        if (currentProductionVersion != stored.ExpectedProductionVersion)
            throw VersionConflict(currentProductionVersion);
        var currentProduction = await versions.FindProductionDefinitionAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken);
        if (currentProduction is not null
            && string.Equals(FlowDiffService.GetChecksum(currentProduction), FlowDiffService.GetChecksum(current), StringComparison.Ordinal))
            throw new McpProtocolException(-32011, "The development definition is already published.");
        var result = await versions.PublishAsync(
            stored.Request.ProjectId,
            stored.Request.FlowId,
            stored.Request.ExpectedDevelopmentVersion,
            stored.Request.Remark,
            expectedProductionVersion: stored.ExpectedProductionVersion,
            cancellationToken: cancellationToken);
        if (!result.IsCommitted)
            throw VersionConflict(result.CurrentHeadVersion);
        await MarkPreviewAppliedAsync(preview, entry, cancellationToken);
        var response = new FlowVersionMutationDto(
            result.Version!,
            FlowDiffService.RedactSensitive(stored.Diff),
            result.DevelopmentDefinition is null ? null : FlowDiffService.RedactSensitive(result.DevelopmentDefinition));
        await idempotency.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, response, requestPayload, cancellationToken);
        return response;
    }

    private static async Task<object> PreviewRollbackAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<RollbackFlowPreviewRequestDto>(arguments);
        if (request.SourceVersion < 1 || request.ExpectedHeadVersion < 1 || !Enum.IsDefined(request.Track))
            throw new McpProtocolException(-32602, "The rollback request is invalid.");
        await RequireActiveProjectAsync(
            scope,
            scope.ServiceProvider.GetRequiredService<McpSecurityService>(),
            principal,
            request.ProjectId,
            McpPermissionDto.FlowRollback,
            cancellationToken);
        var versions = scope.ServiceProvider.GetRequiredService<IFlowVersionRepository>();
        var source = await versions.FindVersionAsync(request.ProjectId, request.FlowId, request.SourceVersion, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The rollback source version was not found.");
        if (source.Version.Track != request.Track)
            throw new McpProtocolException(-32004, "The rollback source version is not on the selected track.");
        var head = (await versions.ListVersionsAsync(request.ProjectId, request.FlowId, request.Track, cancellationToken)).FirstOrDefault(item => item.IsCurrent);
        if (head is null || head.Version != request.ExpectedHeadVersion)
            throw VersionConflict(head?.Version);
        var current = await versions.FindVersionAsync(request.ProjectId, request.FlowId, head.Version, cancellationToken) ?? source;
        var validation = FlowDefinitionContractValidator.ValidateForExecution(source.Definition);
        var libraryValidation = await scope.ServiceProvider.GetRequiredService<ProjectLibraryService>().ValidateFlowLibrariesAsync(request.ProjectId, source.Definition, cancellationToken);
        var diagnostics = validation.Diagnostics.Concat(libraryValidation.Diagnostics).ToArray();
        var diff = scope.ServiceProvider.GetRequiredService<FlowDiffService>().Compare(current.Definition, source.Definition);
        if (diff.Changes.Count == 0)
            diagnostics = diagnostics.Concat([new ValidationDiagnosticDto(
                "flow.rollback_no_change",
                "The selected version is already the current track head. 选定版本已经是当前轨道头版本。",
                null)]).ToArray();
        var stored = new StoredRollbackPreview(request, new FlowValidationResultDto(diagnostics.Length == 0, diagnostics), diff);
        var entry = await scope.ServiceProvider.GetRequiredService<McpPreviewService>().CreateAsync("flow.rollback", principal, request.ProjectId, request.FlowId, request.ExpectedHeadVersion, stored, cancellationToken);
        return new
        {
            previewId = entry.Id,
            entry.ExpiresAt,
            entry.PreviewFingerprint,
            isPreviewOnly = true,
            canApply = stored.Validation.IsValid,
            validation = stored.Validation,
            diff = FlowDiffService.RedactSensitive(diff),
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
            return DeserializeStoredResponse(replay.ResponseJson);
        var preview = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await preview.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        var stored = McpPreviewService.Deserialize<StoredRollbackPreview>(entry);
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        await RequireActiveProjectAsync(
            scope,
            security,
            principal,
            stored.Request.ProjectId,
            McpPermissionDto.FlowRollback,
            cancellationToken);
        var currentHead = stored.Request.Track == FlowVersionTrackDto.Development
            ? (await scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>().FindAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken))?.Version
            : await scope.ServiceProvider.GetRequiredService<IFlowVersionRepository>().FindProductionVersionAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken);
        if (currentHead != stored.Request.ExpectedHeadVersion)
            throw VersionConflict(currentHead);
        var source = await scope.ServiceProvider.GetRequiredService<IFlowVersionRepository>().FindVersionAsync(
            stored.Request.ProjectId,
            stored.Request.FlowId,
            stored.Request.SourceVersion,
            cancellationToken)
            ?? throw new McpProtocolException(-32004, "The rollback source version was not found.");
        if (source.Version.Track != stored.Request.Track)
            throw new McpProtocolException(-32004, "The rollback source version is not on the selected track.");
        var currentValidation = await ValidateExecutableFlowAsync(
            scope,
            stored.Request.ProjectId,
            source.Definition,
            cancellationToken);
        RequireValidMutation(currentValidation, "The rollback source flow cannot be executed.");
        var result = await scope.ServiceProvider.GetRequiredService<IFlowVersionRepository>().RollbackAsync(
            stored.Request.ProjectId, stored.Request.FlowId, stored.Request.SourceVersion, stored.Request.Track, stored.Request.ExpectedHeadVersion, cancellationToken);
        if (!result.IsCommitted)
            throw VersionConflict(result.CurrentHeadVersion);
        await MarkPreviewAppliedAsync(preview, entry, cancellationToken);
        var response = new FlowVersionMutationDto(
            result.Version!,
            FlowDiffService.RedactSensitive(stored.Diff),
            result.DevelopmentDefinition is null ? null : FlowDiffService.RedactSensitive(result.DevelopmentDefinition));
        await idempotency.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, response, requestPayload, cancellationToken);
        return response;
    }

}

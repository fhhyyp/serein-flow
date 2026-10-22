using System.Text.Json;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using SereinFlow.Mcp.Support;
using SereinFlow.Mcp.Previews;
using static SereinFlow.Mcp.Support.McpJson;
using static SereinFlow.Mcp.Support.McpErrors;
using static SereinFlow.Mcp.Support.McpMutationSupport;

namespace SereinFlow.Mcp.Tools;

internal sealed class McpFlowPatchTools(
    FlowDefinitionWriteService _flowDefinitionWriteService,
    FlowDiffService _flowDiffService,
    FlowPatchContractNormalizer _flowPatchContractNormalizer,
    FlowPatchService _flowPatchService,
    IFlowDefinitionRepository _flowDefinitionRepository,
    McpIdempotencyService _mcpIdempotencyService,
    McpPreviewService _mcpPreviewService,
    McpResourceAuthorization _mcpResourceAuthorization)
{
    [SereinFlowMcpTool("sereinflow_preview_flow_patch", McpPermissionDto.FlowWrite, ExecutionKind = McpToolExecutionKind.Mutation, HasAuditTrack = true, AuditTrack = FlowVersionTrackDto.Development)]
    internal async Task<FlowPatchPreviewDto> PreviewPatchAsync(
        SereinFlowMcpCaller context,
        NormalizedFlowPatchRequest arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var normalized = arguments;
        var request = normalized.Request;
        if (request.ProjectId == Guid.Empty || request.FlowId == Guid.Empty || request.ExpectedDevelopmentVersion < 1)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The flow patch request is invalid.");
        }

        await _mcpResourceAuthorization.RequireActiveProjectAsync(principal, request.ProjectId, McpPermissionDto.FlowWrite, cancellationToken);
        var current = await _flowDefinitionRepository.FindAsync(request.ProjectId, request.FlowId, cancellationToken) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The flow definition was not found.");
        if (current.Version != request.ExpectedDevelopmentVersion)
        {
            throw VersionConflict(current.Version);
        }

        FlowDefinitionDto candidate;
        try
        {
            _flowPatchContractNormalizer.ValidateReferences(current, request.Operations);
            candidate = _flowPatchService.Apply(current, request.Operations);
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
            var toString = Guid.NewGuid().ToString("N");
            var value = new
            {
                code = McpErrorCodes.FlowPatchReferenceInvalid,
                diagnosticId = toString,
                fieldPath = "$.operations",
                expected = "a valid operation sequence for the current flow",
                schemaVersion = FlowPatchContract.CurrentSchemaVersion,
                remediation = "Read the current flow edit model and create a new typed patch."
            };
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The flow patch references are invalid.", value);
        }

        var preparation = await _flowDefinitionWriteService.PrepareAsync(request.ProjectId, request.FlowId, candidate, cancellationToken) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The flow definition was not found.");
        var compare = _flowDiffService.Compare(preparation.Current, preparation.Candidate);
        var stored = new StoredFlowPatchPreview(normalized.LegacyRequest, preparation.Candidate, preparation.Validation, compare, normalized.Request, normalized.Warnings);
        var preview = await _mcpPreviewService.CreateAsync(FlowErrorCodes.Patch, principal, request.ProjectId, request.FlowId, request.ExpectedDevelopmentVersion, stored, cancellationToken);
        return McpPreviewProjection.ToFlowPatchPreview(preview, stored);
    }

    [SereinFlowMcpTool("sereinflow_apply_flow_patch", McpPermissionDto.FlowWrite, ExecutionKind = McpToolExecutionKind.Mutation, RequiresIdempotencyKey = true, HasAuditTrack = true, AuditTrack = FlowVersionTrackDto.Development)]
    internal async Task<object> ApplyPatchAsync(
        SereinFlowMcpCaller context,
        McpMutationApplyRequestDto arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var request = arguments;
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var replay = await _mcpIdempotencyService.FindAsync(principal.Id, FlowErrorCodes.Patch, request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            return DeserializeStoredResponse(replay.ResponseJson);
        }

        var entry = await _mcpPreviewService.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, FlowErrorCodes.Patch, StringComparison.Ordinal))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The preview does not describe a flow patch.");
        }

        var stored = McpPreviewService.Deserialize<StoredFlowPatchPreview>(entry);
        await _mcpResourceAuthorization.RequireActiveProjectAsync(principal, stored.Request.ProjectId, McpPermissionDto.FlowWrite, cancellationToken);
        if (!stored.Validation.IsValid || stored.Diff.Changes.Count == 0)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "The flow patch preview cannot be applied.");
        }

        var current = await _flowDefinitionRepository.FindAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The flow definition was not found.");
        if (current.Version != stored.Request.ExpectedDevelopmentVersion)
        {
            throw VersionConflict(current.Version);
        }

        var currentDiff = _flowDiffService.Compare(current, stored.CandidateDefinition);
        if (!string.Equals(currentDiff.BaseChecksum, stored.Diff.BaseChecksum, StringComparison.Ordinal))
        {
            throw VersionConflict(current.Version);
        }

        var write = await _flowDefinitionWriteService.WriteAsync(stored.Request.ProjectId, stored.Request.FlowId, stored.CandidateDefinition, stored.Request.ExpectedDevelopmentVersion, cancellationToken, origin: "mcp");
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
        await MarkPreviewAppliedAsync(_mcpPreviewService, entry, cancellationToken);
        var persisted = await _flowDefinitionRepository.FindAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken);
        if (persisted is null || persisted.Version != saved.Version
            || !string.Equals(persisted.Checksum, saved.Checksum, StringComparison.Ordinal)
            || !string.Equals(persisted.Checksum, FlowDiffService.GetChecksum(persisted), StringComparison.Ordinal))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InternalError, "The flow patch was committed but authoritative verification failed.", new { code = McpErrorCodes.PostApplyVerificationFailed });
        }

        var response = FlowDiffService.RedactSensitive(persisted);
        await _mcpIdempotencyService.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, response, requestPayload, cancellationToken);
        return response;
    }
}

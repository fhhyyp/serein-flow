using Microsoft.AspNetCore.Http;
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

internal sealed class McpFlowReleaseTools(
    FlowDiffService _flowDiffService,
    IFlowDefinitionRepository _flowDefinitionRepository,
    IFlowVersionRepository _flowVersionRepository,
    McpFlowValidation _mcpFlowValidation,
    McpIdempotencyService _mcpIdempotencyService,
    McpPreviewService _mcpPreviewService,
    McpResourceAuthorization _mcpResourceAuthorization,
    ProjectLibraryService _projectLibraryService)
{
    [SereinFlowMcpTool("sereinflow_preview_publish_flow", SereinFlowPermission.FlowPublish, ExecutionKind = McpToolExecutionKind.Mutation, HasAuditTrack = true, AuditTrack = FlowVersionTrackDto.Development)]
    internal async Task<object> PreviewPublishAsync(
        SereinFlowMcpCaller context,
        PublishFlowPreviewRequestDto arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var request = arguments;
        await _mcpResourceAuthorization.RequireActiveProjectAsync(principal, request.ProjectId, SereinFlowPermission.FlowPublish, cancellationToken);
        var flow = await _flowDefinitionRepository.FindAsync(request.ProjectId, request.FlowId, cancellationToken) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The flow definition was not found. 未找到流程定义。");
        if (flow.Version != request.ExpectedDevelopmentVersion)
        {
            throw VersionConflict(flow.Version);
        }

        var executableValidation = await _mcpFlowValidation.ValidateExecutableFlowAsync(request.ProjectId, flow, cancellationToken);
        var expectedProductionVersion = await _flowVersionRepository.FindProductionVersionAsync(request.ProjectId, request.FlowId, cancellationToken);
        var diagnostics = executableValidation.Diagnostics.ToArray();
        var production = await _flowVersionRepository.FindProductionDefinitionAsync(request.ProjectId, request.FlowId, cancellationToken);
        var diff = _flowDiffService.Compare(production ?? flow, flow);
        if (production is not null && diff.Changes.Count == 0)
        {
            var validationDiagnosticDto = new ValidationDiagnosticDto(FlowErrorCodes.PublishNoChange, "The current development version is already the production definition. 当前开发版本已经是生产定义。", null);
            diagnostics = diagnostics.Append(validationDiagnosticDto).ToArray();
        }

        var flowValidationResultDto = new FlowValidationResultDto(diagnostics.Length == 0, diagnostics);
        var result = new StoredPublishPreview(request, flowValidationResultDto, diff, HasProductionVersion: production is not null, ExpectedProductionVersion: expectedProductionVersion);
        var entry = await _mcpPreviewService.CreateAsync("flow.publish", principal, request.ProjectId, request.FlowId, request.ExpectedDevelopmentVersion, result, cancellationToken);
        var redactedDiff = FlowDiffService.RedactSensitive(diff);
        return new
        {
            previewId = entry.Id,
            entry.ExpiresAt,
            entry.PreviewFingerprint,
            isPreviewOnly = true,
            expectedProductionVersion,
            canApply = result.Validation.IsValid && (production is null || diff.Changes.Count > 0),
            validation = result.Validation,
            diff = redactedDiff
        };
    }

    [SereinFlowMcpTool("sereinflow_apply_publish_flow", SereinFlowPermission.FlowPublish, ExecutionKind = McpToolExecutionKind.Mutation, RequiresIdempotencyKey = true, HasAuditTrack = true, AuditTrack = FlowVersionTrackDto.Development)]
    internal async Task<object> ApplyPublishAsync(
        SereinFlowMcpCaller context,
        McpMutationApplyRequestDto arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var request = arguments;
        RequireConfirmation(request);
        var requestJson = Serialize(request);
        var replay = await _mcpIdempotencyService.FindAsync(principal.Id, "flow.publish", request.IdempotencyKey, requestJson, cancellationToken);
        if (replay is not null)
        {
            return DeserializeStoredResponse(replay.ResponseJson);
        }

        var entry = await _mcpPreviewService.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, "flow.publish", StringComparison.Ordinal))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The preview does not describe a flow publication. 此预览不是流程发布预览。");
        }

        var stored = McpPreviewService.Deserialize<StoredPublishPreview>(entry);
        await _mcpResourceAuthorization.RequireActiveProjectAsync(principal, stored.Request.ProjectId, SereinFlowPermission.FlowPublish, cancellationToken);
        var current = await _flowDefinitionRepository.FindAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The flow definition was not found. 未找到流程定义。");
        if (current.Version != stored.Request.ExpectedDevelopmentVersion)
        {
            throw VersionConflict(current.Version);
        }

        var currentValidation = await _mcpFlowValidation.ValidateExecutableFlowAsync(stored.Request.ProjectId, current, cancellationToken);
        RequireValidMutation(currentValidation, "The current development flow cannot be published. 无法发布当前开发流程。");
        var currentProductionVersion = await _flowVersionRepository.FindProductionVersionAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken);
        if (currentProductionVersion != stored.ExpectedProductionVersion)
        {
            throw VersionConflict(currentProductionVersion);
        }

        var currentProduction = await _flowVersionRepository.FindProductionDefinitionAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken);
        if (currentProduction is not null
            && string.Equals(FlowDiffService.GetChecksum(currentProduction), FlowDiffService.GetChecksum(current), StringComparison.Ordinal))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "The development definition is already published. 当前开发定义已发布。");
        }

        var result = await _flowVersionRepository.PublishAsync(stored.Request.ProjectId, stored.Request.FlowId, stored.Request.ExpectedDevelopmentVersion, stored.Request.Remark, expectedProductionVersion: stored.ExpectedProductionVersion, cancellationToken: cancellationToken);
        if (!result.IsCommitted)
        {
            throw VersionConflict(result.CurrentHeadVersion);
        }

        await MarkPreviewAppliedAsync(_mcpPreviewService, entry, cancellationToken);
        var redactedDiff = FlowDiffService.RedactSensitive(stored.Diff);
        FlowDefinitionDto? selectedValue = result.DevelopmentDefinition is null ? null : FlowDiffService.RedactSensitive(result.DevelopmentDefinition);
        var response = new FlowVersionMutationDto(result.Version!, redactedDiff, selectedValue);
        await _mcpIdempotencyService.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, response, requestJson, cancellationToken);
        return response;
    }

    [SereinFlowMcpTool("sereinflow_preview_rollback_flow", SereinFlowPermission.FlowRollback, ExecutionKind = McpToolExecutionKind.Mutation)]
    internal async Task<object> PreviewRollbackAsync(
        SereinFlowMcpCaller context,
        RollbackFlowPreviewRequestDto arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var request = arguments;
        if (request.SourceVersion < 1 || request.ExpectedHeadVersion < 1 || !Enum.IsDefined(request.Track))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The rollback request is invalid. 回滚请求无效。");
        }

        await _mcpResourceAuthorization.RequireActiveProjectAsync(principal, request.ProjectId, SereinFlowPermission.FlowRollback, cancellationToken);
        var source = await _flowVersionRepository.FindVersionAsync(request.ProjectId, request.FlowId, request.SourceVersion, cancellationToken) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The rollback source version was not found. 未找到回滚源版本。");
        if (source.Version.Track != request.Track)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The rollback source version is not on the selected track. 回滚源版本不属于所选版本轨道。");
        }

        var head = ((await _flowVersionRepository.ListVersionsAsync(request.ProjectId, request.FlowId, request.Track, cancellationToken))).FirstOrDefault(item => item.IsCurrent);
        if (head is null || head.Version != request.ExpectedHeadVersion)
        {
            throw VersionConflict(head?.Version);
        }

        var current = await _flowVersionRepository.FindVersionAsync(request.ProjectId, request.FlowId, head.Version, cancellationToken) ?? source;
        var validation = FlowDefinitionContractValidator.ValidateForExecution(source.Definition);
        var libraryValidation = await _projectLibraryService.ValidateFlowLibrariesAsync(request.ProjectId, source.Definition, cancellationToken);
        var diagnostics = validation.Diagnostics.Concat(libraryValidation.Diagnostics).ToArray();
        var diff = _flowDiffService.Compare(current.Definition, source.Definition);
        if (diff.Changes.Count == 0)
        {
            IEnumerable<ValidationDiagnosticDto> value = [new ValidationDiagnosticDto(FlowErrorCodes.RollbackNoChange, "The selected version is already the current track head. 选定版本已经是当前轨道头版本。", null)];
            diagnostics = diagnostics.Concat(value).ToArray();
        }
        var stored = new StoredRollbackPreview(request, new FlowValidationResultDto(diagnostics.Length == 0, diagnostics), diff);
        var entry = await _mcpPreviewService.CreateAsync("flow.rollback", principal, request.ProjectId, request.FlowId, request.ExpectedHeadVersion, stored, cancellationToken);
        var redactedDiff = FlowDiffService.RedactSensitive(diff);
        return new
        {
            previewId = entry.Id,
            entry.ExpiresAt,
            entry.PreviewFingerprint,
            isPreviewOnly = true,
            canApply = stored.Validation.IsValid,
            validation = stored.Validation,
            diff = redactedDiff
        };
    }

    [SereinFlowMcpTool("sereinflow_apply_rollback_flow", SereinFlowPermission.FlowRollback, ExecutionKind = McpToolExecutionKind.Mutation, RequiresIdempotencyKey = true)]
    internal async Task<object> ApplyRollbackAsync(
        SereinFlowMcpCaller context,
        McpMutationApplyRequestDto arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var request = arguments;
        RequireConfirmation(request);
        var requestJson = Serialize(request);
        var replay = await _mcpIdempotencyService.FindAsync(principal.Id, "flow.rollback", request.IdempotencyKey, requestJson, cancellationToken);
        if (replay is not null)
        {
            return DeserializeStoredResponse(replay.ResponseJson);
        }

        var entry = await _mcpPreviewService.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, "flow.rollback", StringComparison.Ordinal))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The preview does not describe a flow rollback. 此预览不是流程回滚预览。");
        }

        var stored = McpPreviewService.Deserialize<StoredRollbackPreview>(entry);
        await _mcpResourceAuthorization.RequireActiveProjectAsync(principal, stored.Request.ProjectId, SereinFlowPermission.FlowRollback, cancellationToken);
        long? currentHead;
        if (stored.Request.Track == FlowVersionTrackDto.Development)
        {
            currentHead = (await _flowDefinitionRepository.FindAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken))?.Version;
        }
        else
        {
            currentHead = await _flowVersionRepository.FindProductionVersionAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken);
        }

        if (currentHead != stored.Request.ExpectedHeadVersion)
        {
            throw VersionConflict(currentHead);
        }

        var source = await _flowVersionRepository.FindVersionAsync(stored.Request.ProjectId, stored.Request.FlowId, stored.Request.SourceVersion, cancellationToken) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The rollback source version was not found. 未找到回滚源版本。");
        if (source.Version.Track != stored.Request.Track)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The rollback source version is not on the selected track. 回滚源版本不属于所选版本轨道。");
        }

        var currentValidation = await _mcpFlowValidation.ValidateExecutableFlowAsync(stored.Request.ProjectId, source.Definition, cancellationToken);
        RequireValidMutation(currentValidation, "The rollback source flow cannot be executed. 回滚源流程无法执行。");
        var result = await _flowVersionRepository.RollbackAsync(stored.Request.ProjectId, stored.Request.FlowId, stored.Request.SourceVersion, stored.Request.Track, stored.Request.ExpectedHeadVersion, cancellationToken);
        if (!result.IsCommitted)
        {
            throw VersionConflict(result.CurrentHeadVersion);
        }

        await MarkPreviewAppliedAsync(_mcpPreviewService, entry, cancellationToken);
        var redactedDiff = FlowDiffService.RedactSensitive(stored.Diff);
        FlowDefinitionDto? selectedValue = result.DevelopmentDefinition is null ? null : FlowDiffService.RedactSensitive(result.DevelopmentDefinition);
        var response = new FlowVersionMutationDto(result.Version!, redactedDiff, selectedValue);
        await _mcpIdempotencyService.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, response, requestJson, cancellationToken);
        return response;
    }
}

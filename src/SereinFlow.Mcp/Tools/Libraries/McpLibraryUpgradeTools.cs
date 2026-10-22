using Microsoft.AspNetCore.Http;
using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using SereinFlow.Mcp.Support;
using SereinFlow.Mcp.Previews;
using static SereinFlow.Mcp.Support.McpJson;
using static SereinFlow.Mcp.Support.McpMutationSupport;

namespace SereinFlow.Mcp.Tools;

internal sealed class McpLibraryUpgradeTools(
    LibraryUpgradeService _libraryUpgradeService,
    McpIdempotencyService _mcpIdempotencyService,
    McpPreviewService _mcpPreviewService,
    McpResourceAuthorization _mcpResourceAuthorization,
    McpSecurityService _mcpSecurityService)
{
    [SereinFlowMcpTool("sereinflow_preview_library_upgrade", McpPermissionDto.FlowWrite, ExecutionKind = McpToolExecutionKind.Mutation)]
    internal async Task<McpLibraryUpgradePreviewDto> PreviewLibraryUpgradeAsync(
        SereinFlowMcpCaller context,
        McpLibraryUpgradePreviewRequestDto arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var request = arguments;
        request = request with
        {
            SourceArtifactId = request.SourceArtifactId?.Trim() ?? string.Empty,
            TargetArtifactId = request.TargetArtifactId?.Trim() ?? string.Empty,
            FlowIds = request.FlowIds ?? []
        };
        await _mcpResourceAuthorization.RequireActiveProjectAsync(principal, request.ProjectId, McpPermissionDto.FlowWrite, cancellationToken);
        _mcpSecurityService.Require(principal, McpPermissionDto.LibraryManage, request.ProjectId);
        var upgradeRequest = new LibraryUpgradePreviewRequestDto(request.SourceArtifactId, request.TargetArtifactId, request.FlowIds);
        var result = await _libraryUpgradeService.PreviewAsync(request.ProjectId, upgradeRequest, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            throw LibraryUpgradeFailure(result);
        }

        var stored = new StoredLibraryUpgradePreview(upgradeRequest, result.Value);
        var entry = await _mcpPreviewService.CreateAsync("library.upgrade", principal, result.Value.ProjectId, null, null, stored, cancellationToken);
        var any = result.Value.Flows.Any(static flow => flow.CanApply);
        return new McpLibraryUpgradePreviewDto(entry.Id, entry.ExpiresAt, entry.PreviewFingerprint, any, result.Value);
    }

    [SereinFlowMcpTool("sereinflow_apply_library_upgrade", McpPermissionDto.FlowWrite, ExecutionKind = McpToolExecutionKind.Mutation, RequiresIdempotencyKey = true)]
    internal async Task<object> ApplyLibraryUpgradeAsync(
        SereinFlowMcpCaller context,
        McpLibraryUpgradeApplyRequestDto arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var request = arguments;
        var applyEnvelope = new McpMutationApplyRequestDto(request.PreviewId, request.PreviewFingerprint, request.Confirmation, request.IdempotencyKey);
        RequireConfirmation(applyEnvelope);
        if (request.Flows is null || request.Flows.Count == 0)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "At least one flow must be selected for a library upgrade. 类库升级必须至少选择一个流程。");
        }

        var requestPayload = Serialize(request);
        var replay = await _mcpIdempotencyService.FindAsync(principal.Id, "library.upgrade", request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            return DeserializeStoredResponse(replay.ResponseJson);
        }

        var entry = await _mcpPreviewService.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, "library.upgrade", StringComparison.Ordinal))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The preview does not describe a library upgrade. 此预览不是类库升级预览。");
        }

        var stored = McpPreviewService.Deserialize<StoredLibraryUpgradePreview>(entry);
        await _mcpResourceAuthorization.RequireActiveProjectAsync(principal, stored.Plan.ProjectId, McpPermissionDto.FlowWrite, cancellationToken);
        _mcpSecurityService.Require(principal, McpPermissionDto.LibraryManage, stored.Plan.ProjectId);
        object response;
        var hasDurableSuccess = false;
        if (request.Flows.Count == 1)
        {
            var result = await _libraryUpgradeService.ApplyAsync(stored.Plan.ProjectId, stored.Plan.Id, request.Flows[0], cancellationToken, origin: "mcp");
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
            var result = await _libraryUpgradeService.ApplyBatchAsync(stored.Plan.ProjectId, stored.Plan.Id, applyLibraryUpgradeBatchRequestDto, cancellationToken, origin: "mcp");
            if (!result.IsSuccess || result.Value is null)
            {
                throw LibraryUpgradeFailure(result);
            }

            response = result.Value;
            hasDurableSuccess = result.Value.Succeeded.Count > 0;
        }

        if (hasDurableSuccess)
        {
            await MarkPreviewAppliedAsync(_mcpPreviewService, entry, cancellationToken);
        }

        await _mcpIdempotencyService.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, response, requestPayload, cancellationToken);
        return response;
    }

    private static McpProtocolException LibraryUpgradeFailure<T>(LibraryUpgradeOperationResult<T> result)
    {
        return new McpProtocolException(result.StatusCode switch
        {
            StatusCodes.Status400BadRequest => McpProtocolErrorCodes.InvalidParams,
            StatusCodes.Status404NotFound => McpProtocolErrorCodes.ResourceNotFound,
            _ => McpProtocolErrorCodes.OperationRejected,
        }, result.Message ?? "The library upgrade could not be completed. 无法完成类库升级。", new { code = result.Code, statusCode = result.StatusCode, currentVersion = result.CurrentVersion });
    }
}

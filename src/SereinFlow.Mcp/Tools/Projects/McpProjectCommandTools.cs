using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using SereinFlow.Mcp.Previews;
using static SereinFlow.Mcp.Support.McpJson;
using static SereinFlow.Mcp.Support.McpMutationSupport;
using static SereinFlow.Mcp.Previews.McpPreviewProjection;

namespace SereinFlow.Mcp.Tools;

internal sealed class McpProjectCommandTools(
    McpIdempotencyService _mcpIdempotencyService,
    McpPreviewService _mcpPreviewService,
    McpSecurityService _mcpSecurityService,
    ProjectCreationService _projectCreationService)
{
    [SereinFlowMcpTool("sereinflow_preview_create_project", McpPermissionDto.ProjectWrite, ExecutionKind = McpToolExecutionKind.Mutation)]
    internal async Task<ProjectCreatePreviewDto> PreviewCreateAsync(
        SereinFlowMcpCaller context,
        CreateProjectMcpRequestDto arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        _mcpSecurityService.RequireAdministrator(principal);
        var request = arguments;
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "MCP parameter 'name' is required. 必须提供 MCP 参数 name。");
        }

        ProjectCreationCandidate candidate;
        try
        {
            candidate = _projectCreationService.PrepareEmpty(request.Name, request.FlowName);
        }
        catch (ArgumentException exception)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, exception.Message);
        }

        var getFlowName = GetFlowName(candidate.Definition);
        var createProjectMcpRequestDto = new CreateProjectMcpRequestDto(candidate.Project.Name, getFlowName);
        var stored = new StoredProjectCreatePreview(createProjectMcpRequestDto, candidate.Project.Id, candidate.Definition.Id, candidate.Project.CreatedAt, candidate.Definition, candidate.Validation);
        var preview = await _mcpPreviewService.CreateAsync("project.create", principal, candidate.Project.Id, candidate.Definition.Id, expectedVersion: null, stored, cancellationToken);
        var mcpPreviewDescriptorDto = new McpPreviewDescriptorDto(preview.Id, preview.Operation, preview.ProjectId, preview.FlowId, preview.Status, preview.ExpiresAt, preview.PreviewFingerprint);
        return ToProjectCreatePreview(mcpPreviewDescriptorDto, candidate);
    }

    [SereinFlowMcpTool("sereinflow_apply_create_project", McpPermissionDto.ProjectWrite, ExecutionKind = McpToolExecutionKind.Mutation, RequiresIdempotencyKey = true)]
    internal async Task<object> ApplyCreateAsync(
        SereinFlowMcpCaller context,
        McpMutationApplyRequestDto arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var request = arguments;
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var replay = await _mcpIdempotencyService.FindAsync(principal.Id, "project.create", request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            return DeserializeStoredResponse(replay.ResponseJson);
        }

        var entry = await _mcpPreviewService.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, "project.create", StringComparison.Ordinal))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The preview does not describe project creation. 此预览不是项目创建预览。");
        }

        _mcpSecurityService.RequireAdministrator(principal);
        _mcpSecurityService.Require(principal, McpPermissionDto.ProjectWrite);
        var stored = McpPreviewService.Deserialize<StoredProjectCreatePreview>(entry);
        var project = RehydrateProject(stored);
        var candidate = _projectCreationService.Prepare(project, stored.Definition);
        if (!candidate.Validation.IsValid)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "The project creation preview is no longer valid. 项目创建预览已失效。", new { code = McpErrorCodes.ValidationFailed, diagnostics = candidate.Validation.Diagnostics });
        }

        var result = await _projectCreationService.CreateAsync(candidate, cancellationToken);
        if (result.Status == ProjectCreationStatus.Conflict)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.Conflict, result.ErrorMessage ?? "The project already exists. 项目已存在。", new { code = result.ErrorCode });
        }

        if (result.Status != ProjectCreationStatus.Created)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "The project creation preview cannot be applied. 无法应用此项目创建预览。");
        }

        var response = CreateWorkspace(candidate);
        await MarkPreviewAppliedAsync(_mcpPreviewService, entry, cancellationToken);
        await _mcpIdempotencyService.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, response, requestPayload, cancellationToken);
        return response;
    }
}

using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using SereinFlow.Mcp.Previews;
using static SereinFlow.Mcp.Support.McpJson;
using static SereinFlow.Mcp.Support.McpMutationSupport;

namespace SereinFlow.Mcp.Tools;

internal sealed class McpProjectLibraryTools(
    ILibraryCatalogService _libraryCatalogService,
    IProjectLibraryReferenceRepository _projectLibraryReferenceRepository,
    IProjectRepository _projectRepository,
    McpIdempotencyService _mcpIdempotencyService,
    McpPreviewService _mcpPreviewService,
    McpSecurityService _mcpSecurityService,
    ProjectLibraryService _projectLibraryService)
{
    [SereinFlowMcpTool("sereinflow_preview_project_library_attach", McpPermissionDto.LibraryManage, ExecutionKind = McpToolExecutionKind.Mutation)]
    internal async Task<ProjectLibraryAttachPreviewDto> PreviewProjectAttachAsync(
        SereinFlowMcpCaller context,
        ProjectLibraryAttachRequestDto arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var request = arguments;
        _mcpSecurityService.Require(principal, McpPermissionDto.LibraryManage, request.ProjectId);
        var libraryId = request.LibraryId?.Trim() ?? string.Empty;
        request = request with
        {
            LibraryId = libraryId
        };
        var validation = await ValidateProjectLibraryAttachAsync(request, cancellationToken);
        ProjectLibraryAttachRequestDto value = new(request.ProjectId, libraryId);
        var stored = new StoredProjectLibraryAttachPreview(value, validation);
        var entry = await _mcpPreviewService.CreateAsync(ProjectErrorCodes.LibraryAttach, principal, request.ProjectId, null, null, stored, cancellationToken);
        return new ProjectLibraryAttachPreviewDto(entry.Id, request.ProjectId, libraryId, entry.ExpiresAt, entry.PreviewFingerprint, validation.Count == 0, validation);
    }

    [SereinFlowMcpTool("sereinflow_apply_project_library_attach", McpPermissionDto.LibraryManage, ExecutionKind = McpToolExecutionKind.Mutation, RequiresIdempotencyKey = true)]
    internal async Task<object> ApplyProjectAttachAsync(
        SereinFlowMcpCaller context,
        McpMutationApplyRequestDto arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var request = arguments;
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var replay = await _mcpIdempotencyService.FindAsync(principal.Id, ProjectErrorCodes.LibraryAttach, request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            return DeserializeStoredResponse(replay.ResponseJson);
        }

        var entry = await _mcpPreviewService.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, ProjectErrorCodes.LibraryAttach, StringComparison.Ordinal))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The preview does not describe a project library attachment. 此预览不是项目类库关联预览。");
        }

        var stored = McpPreviewService.Deserialize<StoredProjectLibraryAttachPreview>(entry);
        _mcpSecurityService.Require(principal, McpPermissionDto.LibraryManage, stored.Request.ProjectId);
        var currentDiagnostics = await ValidateProjectLibraryAttachAsync(stored.Request, cancellationToken);
        if (currentDiagnostics.Count > 0)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "The project library attachment is no longer valid. Create a new preview and review it again. 项目类库关联已失效，请创建新预览并重新审阅。", new { code = McpErrorCodes.ValidationFailed, diagnostics = currentDiagnostics });
        }

        var result = await _projectLibraryService.AddAsync(stored.Request.ProjectId, stored.Request.LibraryId, cancellationToken, origin: "mcp");
        if (!result.IsSuccess)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, result.Message ?? "The project library attachment failed. 项目类库关联失败。", result.Code);
        }

        await MarkPreviewAppliedAsync(_mcpPreviewService, entry, cancellationToken);
        await _mcpIdempotencyService.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, result, requestPayload, cancellationToken);
        return result;
    }

    private async Task<IReadOnlyList<ValidationDiagnosticDto>> ValidateProjectLibraryAttachAsync(ProjectLibraryAttachRequestDto request, CancellationToken cancellationToken)
    {
        var diagnostics = new List<ValidationDiagnosticDto>();
        var project = await _projectRepository.FindAsync(request.ProjectId, cancellationToken);
        if (project is null)
        {
            diagnostics.Add(new ValidationDiagnosticDto(ProjectErrorCodes.NotFound, "The project was not found. 未找到项目。", "projectId"));
        }
        else if (project.Status == SereinFlow.Domain.ProjectStatus.Archived)
        {
            diagnostics.Add(new ValidationDiagnosticDto(ProjectErrorCodes.Archived, "Archived projects cannot change library references. 已归档项目不能修改类库引用。", "projectId"));
        }

        var libraryId = request.LibraryId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(libraryId))
        {
            diagnostics.Add(new ValidationDiagnosticDto(LibraryErrorCodes.IdRequired, "The library ID is required. 类库 ID 不能为空。", "libraryId"));
        }

        var library = string.IsNullOrWhiteSpace(libraryId) ? null : await _libraryCatalogService.FindAsync(libraryId, cancellationToken);
        if (library is null)
        {
            diagnostics.Add(new ValidationDiagnosticDto(LibraryErrorCodes.NotFound, "The library artifact was not found. 未找到类库制品。", "libraryId"));
        }
        else if (library.Lifecycle != LibraryLifecycleDto.Available)
        {
            diagnostics.Add(new ValidationDiagnosticDto(LibraryErrorCodes.Archived, "Archived library artifacts cannot be attached. 已归档类库制品不能被接入。", "libraryId"));
        }

        if (library is not null && await _projectLibraryReferenceRepository.IsReferencedAsync(request.ProjectId, library.Id, cancellationToken))
        {
            diagnostics.Add(new ValidationDiagnosticDto(ProjectLibraryErrorCodes.AlreadyReferenced, "The project already references this library artifact. 项目已引用该类库制品。", "libraryId"));
        }

        return diagnostics;
    }
}

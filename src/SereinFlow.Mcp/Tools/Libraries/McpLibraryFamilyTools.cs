using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using SereinFlow.Mcp.Previews;
using static SereinFlow.Mcp.Support.McpJson;
using static SereinFlow.Mcp.Support.McpMutationSupport;

namespace SereinFlow.Mcp.Tools;

internal sealed class McpLibraryFamilyTools(
    ILibraryCatalogService _libraryCatalogService,
    McpIdempotencyService _mcpIdempotencyService,
    McpPreviewService _mcpPreviewService,
    McpSecurityService _mcpSecurityService)
{
    [SereinFlowMcpTool("sereinflow_preview_library_family_assignment", McpPermissionDto.LibraryManage, ExecutionKind = McpToolExecutionKind.Mutation)]
    internal async Task<LibraryFamilyAssignmentMcpPreviewDto> PreviewLibraryFamilyAssignmentAsync(
        SereinFlowMcpCaller context,
        LibraryFamilyAssignmentMcpRequestDto arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        _mcpSecurityService.RequireAdministrator(principal);
        _mcpSecurityService.Require(principal, McpPermissionDto.LibraryManage);
        var validated = await ValidateLibraryFamilyAssignmentAsync(arguments, cancellationToken);
        var stored = new StoredLibraryFamilyAssignmentPreview(validated.Request, validated.Diagnostics, validated.CurrentFamily, validated.TargetFamily);
        var entry = await _mcpPreviewService.CreateAsync(LibraryErrorCodes.FamilyAssign, principal, null, null, null, stored, cancellationToken);
        return new LibraryFamilyAssignmentMcpPreviewDto(entry.Id, validated.Request, entry.ExpiresAt, entry.PreviewFingerprint, validated.Diagnostics.Count == 0, validated.Diagnostics, validated.CurrentFamily, validated.TargetFamily);
    }

    [SereinFlowMcpTool("sereinflow_apply_library_family_assignment", McpPermissionDto.LibraryManage, ExecutionKind = McpToolExecutionKind.Mutation, RequiresIdempotencyKey = true)]
    internal async Task<object> ApplyLibraryFamilyAssignmentAsync(
        SereinFlowMcpCaller context,
        McpMutationApplyRequestDto arguments,
        CancellationToken cancellationToken)
    {
        var principal = context.RequirePrincipal();
        var request = arguments;
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var replay = await _mcpIdempotencyService.FindAsync(principal.Id, LibraryErrorCodes.FamilyAssign, request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            return DeserializeStoredResponse(replay.ResponseJson);
        }

        var entry = await _mcpPreviewService.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, LibraryErrorCodes.FamilyAssign, StringComparison.Ordinal))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The preview does not describe a library family assignment. 此预览不是类库家族分配预览。");
        }

        _mcpSecurityService.RequireAdministrator(principal);
        _mcpSecurityService.Require(principal, McpPermissionDto.LibraryManage);
        var stored = McpPreviewService.Deserialize<StoredLibraryFamilyAssignmentPreview>(entry);
        var validated = await ValidateLibraryFamilyAssignmentAsync(stored.Request, cancellationToken);
        if (validated.Diagnostics.Count > 0)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "The library family assignment is no longer valid. Create a new preview and review it again. 类库家族分配已失效，请创建新预览并重新审阅。", new { code = McpErrorCodes.ValidationFailed, diagnostics = validated.Diagnostics });
        }

        LibraryFamilyDto? result;
        try
        {
            var assignLibraryFamilyRequestDto = new AssignLibraryFamilyRequestDto(validated.Request.FamilyId, validated.Request.Name, validated.Request.Description);
            result = await _libraryCatalogService.AssignFamilyAsync(validated.Request.LibraryId, assignLibraryFamilyRequestDto, cancellationToken, origin: "mcp");
        }
        catch (ArgumentException exception)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "The library family assignment is no longer valid. Create a new preview and review it again. 类库家族分配已失效，请创建新预览并重新审阅。", new { code = McpErrorCodes.ValidationFailed, message = exception.Message });
        }

        if (result is null)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The library artifact was not found. 未找到类库制品。", new { code = LibraryErrorCodes.NotFound });
        }

        await MarkPreviewAppliedAsync(_mcpPreviewService, entry, cancellationToken);
        await _mcpIdempotencyService.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, result, requestPayload, cancellationToken);
        return result;
    }

    private async Task<LibraryFamilyAssignmentValidation> ValidateLibraryFamilyAssignmentAsync(LibraryFamilyAssignmentMcpRequestDto request, CancellationToken cancellationToken)
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
        var library = string.IsNullOrWhiteSpace(libraryId) ? null : await _libraryCatalogService.FindAsync(libraryId, cancellationToken);
        if (string.IsNullOrWhiteSpace(libraryId))
        {
            diagnostics.Add(new ValidationDiagnosticDto(LibraryErrorCodes.IdRequired, "The library ID is required. 必须提供类库 ID。", "libraryId"));
        }
        else if (library is null)
        {
            diagnostics.Add(new ValidationDiagnosticDto(LibraryErrorCodes.NotFound, "The library artifact was not found. 未找到类库制品。", "libraryId"));
        }

        var families = await _libraryCatalogService.ListFamiliesAsync(includeArchivedArtifacts: true, cancellationToken: cancellationToken);
        var currentFamily = library is null || string.IsNullOrWhiteSpace(library.FamilyId) ? null : families.SingleOrDefault(item => string.Equals(item.Id, library.FamilyId, StringComparison.OrdinalIgnoreCase));
        var targetFamily = familyId is null ? null : families.SingleOrDefault(item => string.Equals(item.Id, familyId, StringComparison.OrdinalIgnoreCase));
        if (familyId is not null && targetFamily is null)
        {
            diagnostics.Add(new ValidationDiagnosticDto(LibraryErrorCodes.FamilyNotFound, "The requested library family was not found. 未找到请求的类库家族。", "familyId"));
        }

        if (familyId is null && string.IsNullOrWhiteSpace(name))
        {
            diagnostics.Add(new ValidationDiagnosticDto(LibraryErrorCodes.FamilyNameRequired, "A family name is required when creating a library family. 创建类库家族时必须提供家族名称。", "name"));
        }

        return new LibraryFamilyAssignmentValidation(normalized, diagnostics, currentFamily, targetFamily);
    }

    private static string? NormalizeOptional(string? value)
    {
        return (string?)(string.IsNullOrWhiteSpace(value) ? null : value.Trim());
    }

    private sealed record LibraryFamilyAssignmentValidation(LibraryFamilyAssignmentMcpRequestDto Request, IReadOnlyList<ValidationDiagnosticDto> Diagnostics, LibraryFamilyDto? CurrentFamily, LibraryFamilyDto? TargetFamily);
}

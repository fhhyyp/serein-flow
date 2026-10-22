using Microsoft.AspNetCore.Http;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using static SereinFlow.Mcp.Support.McpToolAuthorization;
using static SereinFlow.Mcp.Previews.McpPreviewProjection;

namespace SereinFlow.Mcp.Previews;

internal sealed class McpPreviewReader(IMcpPreviewStore _mcpPreviewStore, McpSecurityService security)
{
    internal async Task<object> ReadPreviewAsync(McpPrincipal? principal, Guid previewId, CancellationToken cancellationToken)
    {
        var entry = await _mcpPreviewStore.FindAsync(previewId, cancellationToken) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The MCP preview was not found. 未找到 MCP 预览。");
        if (principal is not null && !principal.IsLocal && !principal.IsAdministrator && !string.Equals(entry.PrincipalId, principal.Id, StringComparison.Ordinal))
        {
            throw new McpSecurityException(McpErrorCodes.PreviewOwnerMismatch, "The MCP preview belongs to another caller. 该 MCP 预览属于其他调用方。", StatusCodes.Status403Forbidden);
        }

        if (entry.ProjectId is null && principal is not null && !principal.IsLocal && !principal.IsAdministrator)
        {
            throw new McpSecurityException(McpErrorCodes.PreviewAccessDenied, "The MCP caller cannot access this global preview. MCP 调用方无权访问此全局预览。", StatusCodes.Status403Forbidden);
        }

        if (entry.ProjectId is not null && principal is not null && !principal.CanAccess(entry.ProjectId.Value))
        {
            throw new McpSecurityException(McpErrorCodes.ProjectAccessDenied, "The MCP caller cannot access this preview. MCP 调用方无权访问此预览。", StatusCodes.Status403Forbidden);
        }

        RequirePreviewPermission(security, principal, entry);
        var descriptor = new McpPreviewDescriptorDto(entry.Id, entry.Operation, entry.ProjectId, entry.FlowId, entry.Status, entry.ExpiresAt, entry.PreviewFingerprint);
        return (object)(entry.Operation switch
        {
            ProjectErrorCodes.Create => ReadProjectCreatePreview(entry, descriptor),
            FlowErrorCodes.Patch => ReadFlowPatchPreview(entry, descriptor),
            "flow.publish" => ReadPublishPreview(entry, descriptor),
            "flow.rollback" => ReadRollbackPreview(entry, descriptor),
            LibraryErrorCodes.Package => ReadLibraryPackagePreview(entry, descriptor),
            ProjectErrorCodes.LibraryAttach => ReadProjectLibraryAttachPreview(entry, descriptor),
            LibraryErrorCodes.FamilyAssign => ReadLibraryFamilyAssignmentPreview(entry, descriptor),
            "library.upgrade" => ReadLibraryUpgradePreview(entry, descriptor),
            _ => descriptor,
        });
    }
}

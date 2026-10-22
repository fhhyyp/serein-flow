using Microsoft.AspNetCore.Http;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using static SereinFlow.Mcp.Support.McpToolAuthorization;
using static SereinFlow.Mcp.Previews.McpPreviewProjection;

namespace SereinFlow.Mcp.Previews;

internal sealed class McpPreviewReader(IMcpPreviewStore _mcpPreviewStore, SereinFlowAccess security)
{
    internal async Task<object> ReadPreviewAsync(SereinFlowActor? principal, Guid previewId, CancellationToken cancellationToken)
    {
        var entry = await _mcpPreviewStore.FindAsync(previewId, cancellationToken) ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The MCP preview was not found. 未找到 MCP 预览。");
        if (principal is null || !string.Equals(entry.PrincipalId, principal.Id, StringComparison.Ordinal))
            throw new SereinFlowAccessException(McpErrorCodes.PreviewOwnerMismatch, "The preview belongs to another caller.", 403);
        await RequirePreviewPermissionAsync(security, principal, entry);
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

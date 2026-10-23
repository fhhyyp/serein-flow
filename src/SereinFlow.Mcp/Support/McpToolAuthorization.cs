using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Mcp.Support;

internal static class McpToolAuthorization
{
    internal static async Task RequireSensitiveReadAsync(
        SereinFlowAccess security,
        SereinFlowActor? principal,
        Guid projectId,
        AiReadModelOptions? options,
        CancellationToken cancellationToken)
    {
        if (options is null || (!options.IncludeFlowLiteralValues && !options.IncludeScriptSource))
        {
            return;
        }

        await security.RequireAsync(principal, SereinFlowPermission.SensitiveRead, projectId, cancellationToken: cancellationToken);
    }

    internal static async Task RequirePreviewPermissionAsync(SereinFlowAccess security, SereinFlowActor? principal, McpPreviewEntry entry, CancellationToken cancellationToken)
    {
        var permission = entry.Operation switch
        {
            ProjectErrorCodes.Create => SereinFlowPermission.ProjectWrite,
            FlowErrorCodes.Patch => SereinFlowPermission.FlowWrite,
            "flow.publish" => SereinFlowPermission.FlowPublish,
            "flow.rollback" => SereinFlowPermission.FlowRollback,
            LibraryErrorCodes.Package => SereinFlowPermission.LibraryImport,
            ProjectErrorCodes.LibraryAttach => SereinFlowPermission.LibraryManage,
            "sereinflow_create_library_node_template" => SereinFlowPermission.ProjectRead,
            "sereinflow_create_builtin_node_template" => SereinFlowPermission.ProjectRead,
            _ => SereinFlowPermission.ProjectRead,
        };
        if (string.Equals(entry.Operation, LibraryErrorCodes.FamilyAssign, StringComparison.Ordinal))
        {
            await security.RequireWorkspaceAsync(principal, cancellationToken: cancellationToken);
            await security.RequireAsync(principal, SereinFlowPermission.LibraryManage, cancellationToken: cancellationToken);
            return;
        }

        if (string.Equals(entry.Operation, "library.upgrade", StringComparison.Ordinal))
        {
            await security.RequireAsync(principal, SereinFlowPermission.FlowWrite, entry.ProjectId, cancellationToken: cancellationToken);
            await security.RequireAsync(principal, SereinFlowPermission.LibraryManage, entry.ProjectId, cancellationToken: cancellationToken);
            return;
        }

        await security.RequireAsync(principal, permission, entry.ProjectId, cancellationToken: cancellationToken);
    }

}

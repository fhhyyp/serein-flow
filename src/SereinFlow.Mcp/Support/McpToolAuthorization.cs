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
        AiReadModelOptions? options)
    {
        if (options is null || (!options.IncludeFlowLiteralValues && !options.IncludeScriptSource))
        {
            return;
        }

        await security.RequireAsync(principal, SereinFlowPermission.SensitiveRead, projectId);
    }

    internal static async Task RequirePreviewPermissionAsync(SereinFlowAccess security, SereinFlowActor? principal, McpPreviewEntry entry)
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
            await security.RequireWorkspaceAsync(principal);
            await security.RequireAsync(principal, SereinFlowPermission.LibraryManage);
            return;
        }

        if (string.Equals(entry.Operation, "library.upgrade", StringComparison.Ordinal))
        {
            await security.RequireAsync(principal, SereinFlowPermission.FlowWrite, entry.ProjectId);
            await security.RequireAsync(principal, SereinFlowPermission.LibraryManage, entry.ProjectId);
            return;
        }

        await security.RequireAsync(principal, permission, entry.ProjectId);
    }

}

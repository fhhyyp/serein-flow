using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using static SereinFlow.Mcp.McpToolSupport;

namespace SereinFlow.Mcp;

internal static class McpToolAuthorization
{
    internal static void RequireSensitiveRead(
        McpSecurityService security,
        McpPrincipal? principal,
        Guid projectId,
        AiReadModelOptions? options)
    {
        if (options is null || (!options.IncludeFlowLiteralValues && !options.IncludeScriptSource))
            return;

        security.Require(principal, McpPermissionDto.SensitiveRead, projectId);
    }

    internal static void RequirePreviewPermission(
        McpSecurityService security,
        McpPrincipal? principal,
        McpPreviewEntry entry)
    {
        var permission = entry.Operation switch
        {
            "project.create" => McpPermissionDto.ProjectWrite,
            "flow.patch" => McpPermissionDto.FlowWrite,
            "flow.publish" => McpPermissionDto.FlowPublish,
            "flow.rollback" => McpPermissionDto.FlowRollback,
            "library.package" => McpPermissionDto.LibraryImport,
            "project.library.attach" => McpPermissionDto.LibraryManage,
            "sereinflow_create_library_node_template" => McpPermissionDto.ProjectRead,
            _ => McpPermissionDto.ProjectRead,
        };
        if (string.Equals(entry.Operation, "library.family.assign", StringComparison.Ordinal))
        {
            security.RequireAdministrator(principal);
            security.Require(principal, McpPermissionDto.LibraryManage);
            return;
        }
        if (string.Equals(entry.Operation, "library.upgrade", StringComparison.Ordinal))
        {
            security.Require(principal, McpPermissionDto.FlowWrite, entry.ProjectId);
            security.Require(principal, McpPermissionDto.LibraryManage, entry.ProjectId);
            return;
        }
        security.Require(principal, permission, entry.ProjectId);
    }

    internal static AiReadModelOptions ReadAuthorizedOptions(
        JsonElement arguments,
        McpSecurityService security,
        McpPrincipal? principal,
        Guid projectId)
    {
        var options = ReadOptions(arguments);
        RequireSensitiveRead(security, principal, projectId, options);
        return options;
    }

}

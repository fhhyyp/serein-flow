using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Mcp;

public sealed partial class SereinFlowMcpBackend
{
    private static void RequireSensitiveRead(
        McpSecurityService security,
        McpPrincipal? principal,
        Guid projectId,
        AiReadModelOptions? options)
    {
        if (options is null || (!options.IncludeFlowLiteralValues && !options.IncludeScriptSource))
            return;

        security.Require(principal, McpPermissionDto.SensitiveRead, projectId);
    }

    private static void RequirePreviewPermission(
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
        security.Require(principal, permission, entry.ProjectId);
    }

    private static AiReadModelOptions ReadAuthorizedOptions(
        JsonElement arguments,
        McpSecurityService security,
        McpPrincipal? principal,
        Guid projectId)
    {
        var options = ReadOptions(arguments);
        RequireSensitiveRead(security, principal, projectId, options);
        return options;
    }

    private static void AuthorizeTool(string name, JsonElement arguments, McpSecurityService security, McpPrincipal? principal)
    {
        var projectId = TryGetGuid(arguments, "projectId");
        var permission = name switch
        {
            "sereinflow_list_projects" or "sereinflow_get_project" or "sereinflow_get_flow_topology" or "sereinflow_get_flow_edit_model" or "sereinflow_create_library_node_template" or "sereinflow_compare_flow_versions" => McpPermissionDto.ProjectRead,
            "sereinflow_preview_create_project" or "sereinflow_apply_create_project" => McpPermissionDto.ProjectWrite,
            "sereinflow_list_libraries" or "sereinflow_get_library" => McpPermissionDto.LibraryRead,
            "sereinflow_get_run_inspection" => McpPermissionDto.RunRead,
            "sereinflow_get_debug_state" or "sereinflow_wait_debug_state" => McpPermissionDto.DebugRead,
            "sereinflow_preview_flow_patch" or "sereinflow_apply_flow_patch" => McpPermissionDto.FlowWrite,
            "sereinflow_preview_publish_flow" or "sereinflow_apply_publish_flow" => McpPermissionDto.FlowPublish,
            "sereinflow_preview_rollback_flow" or "sereinflow_apply_rollback_flow" => McpPermissionDto.FlowRollback,
            "sereinflow_compile_sereinlang" => McpPermissionDto.ScriptCompile,
            "sereinflow_preview_library_package" or "sereinflow_apply_library_package" => McpPermissionDto.LibraryImport,
            "sereinflow_preview_project_library_attach" or "sereinflow_apply_project_library_attach" => McpPermissionDto.LibraryManage,
            "sereinflow_list_mcp_api_keys" or "sereinflow_create_mcp_api_key" or "sereinflow_revoke_mcp_api_key" or "sereinflow_rotate_mcp_api_key" => McpPermissionDto.McpKeysManage,
            _ => (McpPermissionDto?)null
        };
        if (permission is not null)
            security.Require(principal, permission.Value, projectId);
    }

    private static async Task<IDisposable?> AcquireMutationGateAsync(
        string name,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!IsIdempotentMutation(name))
            return null;

        // A process-wide gate closes the check-then-write window for concurrent
        // requests handled by this host. The database uniqueness constraint
        // remains the final guard when several hosts share one database.
        // 进程级闸门关闭同一宿主内的“检查后写入”窗口；多个宿主共享数据库时，
        // 数据库唯一约束仍是最后一道保护。
        _ = GetRequiredString(arguments, "idempotencyKey");
        var gate = MutationGates.GetOrAdd(name, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new SemaphoreLease(gate);
    }

    private static bool IsIdempotentMutation(string name)
        => name is "sereinflow_apply_flow_patch"
            or "sereinflow_apply_create_project"
            or "sereinflow_apply_publish_flow"
            or "sereinflow_apply_rollback_flow"
            or "sereinflow_apply_library_package"
            or "sereinflow_apply_project_library_attach"
            or "sereinflow_create_mcp_api_key"
            or "sereinflow_revoke_mcp_api_key"
            or "sereinflow_rotate_mcp_api_key";

}

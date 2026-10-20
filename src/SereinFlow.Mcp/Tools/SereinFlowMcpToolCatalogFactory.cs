using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Contracts;
using static SereinFlow.Mcp.McpToolSchemas;
using static SereinFlow.Mcp.McpToolSupport;

namespace SereinFlow.Mcp;
/// <summary>
/// Declares the SereinFlow tools at their protocol boundary. Each entry owns
/// its schema descriptor, required permission, execution mode and handler.
/// This keeps routing out of the backend facade and makes new tools local.
/// 每个工具在同一处声明合同、权限、读写属性与处理器，避免 Backend 成为总路由器。
/// </summary>
internal static class SereinFlowMcpToolCatalogFactory
{
    private static readonly IReadOnlyList<McpToolDescriptor> ToolDescriptors = CreateToolDescriptorsInitialValue();
    internal static McpToolCatalog Create()
    {
        IEnumerable<IMcpTool> evaluatedValue206 = [Read("sereinflow_list_projects", McpPermissionDto.ProjectRead, static (context, arguments, cancellationToken) =>
        {
            var listProjectsAsyncResult = McpReadModelToolHandlers.ListProjectsAsync(context, arguments, cancellationToken);
            return listProjectsAsyncResult;
        }), Read("sereinflow_list_archived_projects", McpPermissionDto.ProjectRead, static (context, arguments, cancellationToken) =>
        {
            var listArchivedProjectsAsyncResult = McpReadModelToolHandlers.ListArchivedProjectsAsync(context, arguments, cancellationToken);
            return listArchivedProjectsAsyncResult;
        }), Mutation("sereinflow_preview_create_project", McpPermissionDto.ProjectWrite, static (context, arguments, cancellationToken) =>
        {
            var previewCreateAsyncResult = McpProjectToolHandlers.PreviewCreateAsync(context, arguments, cancellationToken);
            return previewCreateAsyncResult;
        }), Mutation("sereinflow_apply_create_project", McpPermissionDto.ProjectWrite, static (context, arguments, cancellationToken) =>
        {
            var applyCreateAsyncResult = McpProjectToolHandlers.ApplyCreateAsync(context, arguments, cancellationToken);
            return applyCreateAsyncResult;
        }, requiresIdempotencyKey: true), Read("sereinflow_get_project", McpPermissionDto.ProjectRead, static (context, arguments, cancellationToken) =>
        {
            var getProjectAsyncResult = McpReadModelToolHandlers.GetProjectAsync(context, arguments, cancellationToken);
            return getProjectAsyncResult;
        }), Read("sereinflow_get_flow_topology", McpPermissionDto.ProjectRead, static (context, arguments, cancellationToken) =>
        {
            var getFlowTopologyAsyncResult = McpReadModelToolHandlers.GetFlowTopologyAsync(context, arguments, cancellationToken);
            return getFlowTopologyAsyncResult;
        }), Read("sereinflow_list_libraries", McpPermissionDto.LibraryRead, static (context, arguments, cancellationToken) =>
        {
            var listLibrariesAsyncResult = McpReadModelToolHandlers.ListLibrariesAsync(context, arguments, cancellationToken);
            return listLibrariesAsyncResult;
        }), Read("sereinflow_list_archived_libraries", McpPermissionDto.LibraryRead, static (context, arguments, cancellationToken) =>
        {
            var listArchivedLibrariesAsyncResult = McpReadModelToolHandlers.ListArchivedLibrariesAsync(context, arguments, cancellationToken);
            return listArchivedLibrariesAsyncResult;
        }), Read("sereinflow_get_library", McpPermissionDto.LibraryRead, static (context, arguments, cancellationToken) =>
        {
            var getLibraryAsyncResult = McpReadModelToolHandlers.GetLibraryAsync(context, arguments, cancellationToken);
            return getLibraryAsyncResult;
        }), Read("sereinflow_list_library_families", McpPermissionDto.LibraryRead, static (context, arguments, cancellationToken) =>
        {
            var listLibraryFamiliesAsyncResult = McpReadModelToolHandlers.ListLibraryFamiliesAsync(context, arguments, cancellationToken);
            return listLibraryFamiliesAsyncResult;
        }), Read("sereinflow_get_library_family", McpPermissionDto.LibraryRead, static (context, arguments, cancellationToken) =>
        {
            var getLibraryFamilyAsyncResult = McpReadModelToolHandlers.GetLibraryFamilyAsync(context, arguments, cancellationToken);
            return getLibraryFamilyAsyncResult;
        }), Read("sereinflow_get_project_libraries", McpPermissionDto.LibraryRead, static (context, arguments, cancellationToken) =>
        {
            var getProjectLibrariesAsyncResult = McpReadModelToolHandlers.GetProjectLibrariesAsync(context, arguments, cancellationToken);
            return getProjectLibrariesAsyncResult;
        }), Read("sereinflow_get_library_upgrade", McpPermissionDto.LibraryRead, static (context, arguments, cancellationToken) =>
        {
            var getLibraryUpgradeAsyncResult = McpReadModelToolHandlers.GetLibraryUpgradeAsync(context, arguments, cancellationToken);
            return getLibraryUpgradeAsyncResult;
        }), Read("sereinflow_get_run_inspection", McpPermissionDto.RunRead, static (context, arguments, cancellationToken) =>
        {
            var getRunInspectionAsyncResult = McpReadModelToolHandlers.GetRunInspectionAsync(context, arguments, cancellationToken);
            return getRunInspectionAsyncResult;
        }), Read("sereinflow_get_run_variables", McpPermissionDto.RunRead, static (context, arguments, cancellationToken) =>
        {
            var getAsyncResult = McpRunVariableToolHandlers.GetAsync(context, arguments, cancellationToken);
            return getAsyncResult;
        }), Mutation("sereinflow_set_run_variable", McpPermissionDto.RunVariableWrite, static (context, arguments, cancellationToken) =>
        {
            var setAsyncResult = McpRunVariableToolHandlers.SetAsync(context, arguments, cancellationToken);
            return setAsyncResult;
        }, requiresIdempotencyKey: true), ReadAny("sereinflow_list_run_workpieces", static (context, arguments, cancellationToken) =>
        {
            var listRunWorkpiecesAsyncResult = McpReadModelToolHandlers.ListRunWorkpiecesAsync(context, arguments, cancellationToken);
            return listRunWorkpiecesAsyncResult;
        }), ReadAny("sereinflow_get_run_workpiece", static (context, arguments, cancellationToken) =>
        {
            var getRunWorkpieceAsyncResult = McpReadModelToolHandlers.GetRunWorkpieceAsync(context, arguments, cancellationToken);
            return getRunWorkpieceAsyncResult;
        }), Mutation("sereinflow_publish_run_message", McpPermissionDto.RunMessagePublish, static (context, arguments, cancellationToken) =>
        {
            var publishAsyncResult = McpRunMessageToolHandlers.PublishAsync(context, arguments, cancellationToken);
            return publishAsyncResult;
        }, requiresIdempotencyKey: true), ReadAny("sereinflow_list_runs", static (context, arguments, cancellationToken) =>
        {
            var listRunsAsyncResult = McpDebugToolHandlers.ListRunsAsync(context, arguments, cancellationToken);
            return listRunsAsyncResult;
        }), ReadAny("sereinflow_list_debug_sessions", static (context, arguments, cancellationToken) =>
        {
            var listDebugSessionsAsyncResult = McpDebugToolHandlers.ListDebugSessionsAsync(context, arguments, cancellationToken);
            return listDebugSessionsAsyncResult;
        }), ReadAny("sereinflow_get_debug_state", static (context, arguments, cancellationToken) =>
        {
            var getDebugStateAsyncResult = McpReadModelToolHandlers.GetDebugStateAsync(context, arguments, cancellationToken);
            return getDebugStateAsyncResult;
        }), ReadAny("sereinflow_wait_debug_state", static (context, arguments, cancellationToken) =>
        {
            var waitForDebugStateAsyncResult = McpReadModelToolHandlers.WaitForDebugStateAsync(context, arguments, cancellationToken);
            return waitForDebugStateAsyncResult;
        }), Mutation("sereinflow_start_debug_session", McpPermissionDto.DebugControl, static (context, arguments, cancellationToken) =>
        {
            var startAsyncResult = McpDebugToolHandlers.StartAsync(context, arguments, cancellationToken);
            return startAsyncResult;
        }, requiresIdempotencyKey: true), Mutation("sereinflow_continue_debug", McpPermissionDto.DebugControl, static (context, arguments, cancellationToken) =>
        {
            var continueAsyncResult = McpDebugToolHandlers.ContinueAsync(context, arguments, cancellationToken);
            return continueAsyncResult;
        }), Mutation("sereinflow_step_debug", McpPermissionDto.DebugControl, static (context, arguments, cancellationToken) =>
        {
            var stepAsyncResult = McpDebugToolHandlers.StepAsync(context, arguments, cancellationToken);
            return stepAsyncResult;
        }), Mutation("sereinflow_stop_debug", McpPermissionDto.DebugControl, static (context, arguments, cancellationToken) =>
        {
            var stopAsyncResult = McpDebugToolHandlers.StopAsync(context, arguments, cancellationToken);
            return stopAsyncResult;
        }), Read("sereinflow_get_flow_edit_model", McpPermissionDto.ProjectRead, static (context, arguments, cancellationToken) =>
        {
            var getFlowEditModelAsyncResult = McpReadModelToolHandlers.GetFlowEditModelAsync(context, arguments, cancellationToken);
            return getFlowEditModelAsyncResult;
        }), Read("sereinflow_create_library_node_template", McpPermissionDto.ProjectRead, static (context, arguments, cancellationToken) =>
        {
            var createNodeTemplateAsyncResult = McpLibraryToolHandlers.CreateNodeTemplateAsync(context, arguments, cancellationToken);
            return createNodeTemplateAsyncResult;
        }), Read("sereinflow_create_builtin_node_template", McpPermissionDto.ProjectRead, static (context, arguments, cancellationToken) =>
        {
            var createBuiltinNodeTemplateAsyncResult = McpNodeTemplateToolHandlers.CreateBuiltinNodeTemplateAsync(context, arguments, cancellationToken);
            return createBuiltinNodeTemplateAsyncResult;
        }), Mutation("sereinflow_preview_flow_patch", McpPermissionDto.FlowWrite, static (context, arguments, cancellationToken) =>
        {
            var previewPatchAsyncResult = McpFlowToolHandlers.PreviewPatchAsync(context, arguments, cancellationToken);
            return previewPatchAsyncResult;
        }, auditTrack: FlowVersionTrackDto.Development), Mutation("sereinflow_apply_flow_patch", McpPermissionDto.FlowWrite, static (context, arguments, cancellationToken) =>
        {
            var applyPatchAsyncResult = McpFlowToolHandlers.ApplyPatchAsync(context, arguments, cancellationToken);
            return applyPatchAsyncResult;
        }, requiresIdempotencyKey: true, auditTrack: FlowVersionTrackDto.Development), Read("sereinflow_compare_flow_versions", McpPermissionDto.ProjectRead, static (context, arguments, cancellationToken) =>
        {
            var compareVersionsAsyncResult = McpFlowToolHandlers.CompareVersionsAsync(context, arguments, cancellationToken);
            return compareVersionsAsyncResult;
        }), Mutation("sereinflow_preview_publish_flow", McpPermissionDto.FlowPublish, static (context, arguments, cancellationToken) =>
        {
            var previewPublishAsyncResult = McpFlowToolHandlers.PreviewPublishAsync(context, arguments, cancellationToken);
            return previewPublishAsyncResult;
        }, auditTrack: FlowVersionTrackDto.Development), Mutation("sereinflow_apply_publish_flow", McpPermissionDto.FlowPublish, static (context, arguments, cancellationToken) =>
        {
            var applyPublishAsyncResult = McpFlowToolHandlers.ApplyPublishAsync(context, arguments, cancellationToken);
            return applyPublishAsyncResult;
        }, requiresIdempotencyKey: true, auditTrack: FlowVersionTrackDto.Development), Mutation("sereinflow_preview_rollback_flow", McpPermissionDto.FlowRollback, static (context, arguments, cancellationToken) =>
        {
            var previewRollbackAsyncResult = McpFlowToolHandlers.PreviewRollbackAsync(context, arguments, cancellationToken);
            return previewRollbackAsyncResult;
        }), Mutation("sereinflow_apply_rollback_flow", McpPermissionDto.FlowRollback, static (context, arguments, cancellationToken) =>
        {
            var applyRollbackAsyncResult = McpFlowToolHandlers.ApplyRollbackAsync(context, arguments, cancellationToken);
            return applyRollbackAsyncResult;
        }, requiresIdempotencyKey: true), Read("sereinflow_compile_sereinlang", McpPermissionDto.ScriptCompile, static (context, arguments, cancellationToken) =>
        {
            var compileScriptAsyncResult = McpLibraryToolHandlers.CompileScriptAsync(context, arguments, cancellationToken);
            return compileScriptAsyncResult;
        }), Mutation("sereinflow_preview_library_package", McpPermissionDto.LibraryImport, static (context, arguments, cancellationToken) =>
        {
            var previewPackageAsyncResult = McpLibraryToolHandlers.PreviewPackageAsync(context, arguments, cancellationToken);
            return previewPackageAsyncResult;
        }), Mutation("sereinflow_apply_library_package", McpPermissionDto.LibraryImport, static (context, arguments, cancellationToken) =>
        {
            var applyPackageAsyncResult = McpLibraryToolHandlers.ApplyPackageAsync(context, arguments, cancellationToken);
            return applyPackageAsyncResult;
        }, requiresIdempotencyKey: true), Mutation("sereinflow_preview_project_library_attach", McpPermissionDto.LibraryManage, static (context, arguments, cancellationToken) =>
        {
            var previewProjectAttachAsyncResult = McpLibraryToolHandlers.PreviewProjectAttachAsync(context, arguments, cancellationToken);
            return previewProjectAttachAsyncResult;
        }), Mutation("sereinflow_apply_project_library_attach", McpPermissionDto.LibraryManage, static (context, arguments, cancellationToken) =>
        {
            var applyProjectAttachAsyncResult = McpLibraryToolHandlers.ApplyProjectAttachAsync(context, arguments, cancellationToken);
            return applyProjectAttachAsyncResult;
        }, requiresIdempotencyKey: true), Mutation("sereinflow_preview_library_family_assignment", McpPermissionDto.LibraryManage, static (context, arguments, cancellationToken) =>
        {
            var previewLibraryFamilyAssignmentAsyncResult = McpLibraryToolHandlers.PreviewLibraryFamilyAssignmentAsync(context, arguments, cancellationToken);
            return previewLibraryFamilyAssignmentAsyncResult;
        }), Mutation("sereinflow_apply_library_family_assignment", McpPermissionDto.LibraryManage, static (context, arguments, cancellationToken) =>
        {
            var applyLibraryFamilyAssignmentAsyncResult = McpLibraryToolHandlers.ApplyLibraryFamilyAssignmentAsync(context, arguments, cancellationToken);
            return applyLibraryFamilyAssignmentAsyncResult;
        }, requiresIdempotencyKey: true), Mutation("sereinflow_preview_library_upgrade", McpPermissionDto.FlowWrite, static (context, arguments, cancellationToken) =>
        {
            var previewLibraryUpgradeAsyncResult = McpLibraryToolHandlers.PreviewLibraryUpgradeAsync(context, arguments, cancellationToken);
            return previewLibraryUpgradeAsyncResult;
        }), Mutation("sereinflow_apply_library_upgrade", McpPermissionDto.FlowWrite, static (context, arguments, cancellationToken) =>
        {
            var applyLibraryUpgradeAsyncResult = McpLibraryToolHandlers.ApplyLibraryUpgradeAsync(context, arguments, cancellationToken);
            return applyLibraryUpgradeAsyncResult;
        }, requiresIdempotencyKey: true), Read("sereinflow_list_mcp_api_keys", McpPermissionDto.McpKeysManage, static (context, _, cancellationToken) =>
        {
            var listAsyncResult = McpApiKeyToolHandlers.ListAsync(context, cancellationToken);
            return listAsyncResult;
        }), Mutation("sereinflow_create_mcp_api_key", McpPermissionDto.McpKeysManage, static (context, arguments, cancellationToken) =>
        {
            var createAsyncResult = McpApiKeyToolHandlers.CreateAsync(context, arguments, cancellationToken);
            return createAsyncResult;
        }, requiresIdempotencyKey: true), Mutation("sereinflow_revoke_mcp_api_key", McpPermissionDto.McpKeysManage, static (context, arguments, cancellationToken) =>
        {
            var revokeAsyncResult = McpApiKeyToolHandlers.RevokeAsync(context, arguments, cancellationToken);
            return revokeAsyncResult;
        }, requiresIdempotencyKey: true), Mutation("sereinflow_rotate_mcp_api_key", McpPermissionDto.McpKeysManage, static (context, arguments, cancellationToken) =>
        {
            var rotateAsyncResult = McpApiKeyToolHandlers.RotateAsync(context, arguments, cancellationToken);
            return rotateAsyncResult;
        }, requiresIdempotencyKey: true)];
        McpToolCatalog evaluatedValue207 = new(evaluatedValue206);
        return evaluatedValue207;
    }

    private static McpToolDefinition Read<T>(string name, McpPermissionDto permission, Func<McpToolContext, JsonElement, CancellationToken, Task<T>> execute)
    {
        var defineResult = Define(name, McpToolExecutionKind.Read, permission, async (context, arguments, cancellationToken) =>
        {
            var evaluatedValue208 = (object?)await execute(context, arguments, cancellationToken);
            return evaluatedValue208;
        }, false, null);
        return defineResult;
    }

    private static McpToolDefinition ReadAny<T>(string name, Func<McpToolContext, JsonElement, CancellationToken, Task<T>> execute)
    {
        var getToolDescriptorResult = GetToolDescriptor(name);
        McpToolDefinition evaluatedValue210 = new(getToolDescriptorResult, McpToolExecutionKind.Read, async (context, arguments, cancellationToken) =>
        {
            var evaluatedValue209 = (object?)await execute(context, arguments, cancellationToken);
            return evaluatedValue209;
        }, (context, arguments) =>
        {
            var tryGetGuidResult = TryGetGuid(arguments, "projectId");
            context.Security.RequireAny(context.Principal, tryGetGuidResult, McpPermissionDto.DebugRead, McpPermissionDto.RunRead);
        });
        return evaluatedValue210;
    }

    private static McpToolDefinition Mutation<T>(string name, McpPermissionDto permission, Func<McpToolContext, JsonElement, CancellationToken, Task<T>> execute, bool requiresIdempotencyKey = false, FlowVersionTrackDto? auditTrack = null)
    {
        var defineResult212 = Define(name, McpToolExecutionKind.Mutation, permission, async (context, arguments, cancellationToken) =>
        {
            var evaluatedValue211 = (object?)await execute(context, arguments, cancellationToken);
            return evaluatedValue211;
        }, requiresIdempotencyKey, auditTrack);
        return defineResult212;
    }

    private static McpToolDefinition Define(string name, McpToolExecutionKind executionKind, McpPermissionDto permission, Func<McpToolContext, JsonElement, CancellationToken, Task<object?>> execute, bool requiresIdempotencyKey, FlowVersionTrackDto? auditTrack)
    {
        var getToolDescriptorResult214 = GetToolDescriptor(name);
        McpToolDefinition evaluatedValue215 = new(getToolDescriptorResult214, executionKind, execute, (context, arguments) =>
        {
            var tryGetGuidResult213 = TryGetGuid(arguments, "projectId");
            context.Security.Require(context.Principal, permission, tryGetGuidResult213);
        }, requiresIdempotencyKey, auditTrack);
        return evaluatedValue215;
    }

    private static McpToolDescriptor GetToolDescriptor(string name)
    {
        var singleResult = ToolDescriptors.Single(item =>
        {
            var equalsResult = string.Equals(item.Name, name, StringComparison.Ordinal);
            return equalsResult;
        });
        return singleResult;
    }

    private static IReadOnlyList<McpToolDescriptor> CreateToolDescriptorsInitialValue()
    {
        IReadOnlyList<McpToolDescriptor> toolDescriptorsInitialValue = [resolveToolResult(), resolveToolResult1(), resolveToolResult5(), resolveToolResult9(), resolveToolResult12(), resolveToolResult16(), resolveToolResult20(), resolveToolResult24(), resolveToolResult28(), resolveToolResult32(), resolveToolResult36(), resolveToolResult40(), resolveToolResult44(), resolveToolResult48(), resolveToolResult52(), resolveToolResult56(), resolveToolResult60(), resolveToolResult64(), resolveToolResult68(), resolveToolResult72(), resolveToolResult76(), resolveToolResult80(), resolveToolResult84(), resolveToolResult88(), resolveToolResult92(), resolveToolResult96(), resolveToolResult100(), resolveToolResult104(), resolveToolResult108(), resolveToolResult112(), resolveToolResult116(), resolveToolResult120(), resolveToolResult124(), resolveToolResult128(), resolveToolResult132(), resolveToolResult136(), resolveToolResult140(), resolveToolResult144(), resolveToolResult148(), resolveToolResult152(), resolveToolResult156(), resolveToolResult160(), resolveToolResult164(), resolveToolResult168(), resolveToolResult172(), resolveToolResult179(), resolveToolResult189(), resolveToolResult192(), resolveToolResult198(), resolveToolResult202()];
        return toolDescriptorsInitialValue;
        McpToolDescriptor resolveToolResult()
        {
            var dictionary = new Dictionary<string, object?>
            {
                ["maxItems"] = NumberSchema()
            };
            var schemaResult = Schema(properties: dictionary);
            var toolResult = Tool("sereinflow_list_projects", "List bounded non-archived project and flow summaries.", schemaResult);
            return toolResult;
        }

        McpToolDescriptor resolveToolResult1()
        {
            var dictionary2 = new Dictionary<string, object?>
            {
                ["maxItems"] = NumberSchema()
            };
            var schemaResult3 = Schema(properties: dictionary2);
            var toolResult4 = Tool("sereinflow_list_archived_projects", "List bounded archived project and flow summaries.", schemaResult3);
            return toolResult4;
        }

        McpToolDescriptor resolveToolResult5()
        {
            var dictionary6 = new Dictionary<string, object?>
            {
                ["name"] = StringSchema(),
                ["flowName"] = StringSchema()
            };
            var schemaResult7 = Schema(properties: dictionary6, required: ["name"]);
            var toolResult8 = Tool("sereinflow_preview_create_project", "Preview creating a new draft project with an empty main flow.", schemaResult7);
            return toolResult8;
        }

        McpToolDescriptor resolveToolResult9()
        {
            var applySchemaPropertiesResult = ApplySchemaProperties();
            var schemaResult10 = Schema(properties: applySchemaPropertiesResult, required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"]);
            var toolResult11 = Tool("sereinflow_apply_create_project", "Create a previously previewed draft project after task-level authorization.", schemaResult10);
            return toolResult11;
        }

        McpToolDescriptor resolveToolResult12()
        {
            var dictionary13 = new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema()
            };
            var schemaResult14 = Schema(properties: dictionary13, required: ["projectId"]);
            var toolResult15 = Tool("sereinflow_get_project", "Get one project and its flow summaries.", schemaResult14);
            return toolResult15;
        }

        McpToolDescriptor resolveToolResult16()
        {
            var dictionary17 = new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(),
                ["flowId"] = StringSchema(),
                ["track"] = StringEnumSchema("development or production", "development", "production"),
                ["version"] = NumberSchema(),
                ["maxItems"] = NumberSchema(),
                ["maxJsonBytes"] = NumberSchema(),
                ["includeFlowLiteralValues"] = BooleanSchema(),
                ["includeScriptSource"] = BooleanSchema()
            };
            var schemaResult18 = Schema(properties: dictionary17, required: ["projectId", "flowId"]);
            var toolResult19 = Tool("sereinflow_get_flow_topology", "Read a bounded flow topology. track defaults to development and accepts development or production.", schemaResult18);
            return toolResult19;
        }

        McpToolDescriptor resolveToolResult20()
        {
            var dictionary21 = new Dictionary<string, object?>
            {
                ["includeArchived"] = BooleanSchema(),
                ["maxItems"] = NumberSchema()
            };
            var schemaResult22 = Schema(properties: dictionary21);
            var toolResult23 = Tool("sereinflow_list_libraries", "List bounded available library artifact summaries. Set includeArchived only for the legacy all-lifecycles view; use sereinflow_list_archived_libraries for archived artifacts.", schemaResult22);
            return toolResult23;
        }

        McpToolDescriptor resolveToolResult24()
        {
            var dictionary25 = new Dictionary<string, object?>
            {
                ["maxItems"] = NumberSchema()
            };
            var schemaResult26 = Schema(properties: dictionary25);
            var toolResult27 = Tool("sereinflow_list_archived_libraries", "List bounded archived library artifact summaries.", schemaResult26);
            return toolResult27;
        }

        McpToolDescriptor resolveToolResult28()
        {
            var dictionary29 = new Dictionary<string, object?>
            {
                ["libraryId"] = StringSchema()
            };
            var schemaResult30 = Schema(properties: dictionary29, required: ["libraryId"]);
            var toolResult31 = Tool("sereinflow_get_library", "Read one library's scanned node and parameter contracts.", schemaResult30);
            return toolResult31;
        }

        McpToolDescriptor resolveToolResult32()
        {
            var dictionary33 = new Dictionary<string, object?>
            {
                ["includeArchivedArtifacts"] = BooleanSchema(),
                ["maxItems"] = NumberSchema()
            };
            var schemaResult34 = Schema(properties: dictionary33);
            var toolResult35 = Tool("sereinflow_list_library_families", "List bounded library families and their immutable artifact versions.", schemaResult34);
            return toolResult35;
        }

        McpToolDescriptor resolveToolResult36()
        {
            var dictionary37 = new Dictionary<string, object?>
            {
                ["familyId"] = StringSchema()
            };
            var schemaResult38 = Schema(properties: dictionary37, required: ["familyId"]);
            var toolResult39 = Tool("sereinflow_get_library_family", "Read one library family and its immutable artifact versions.", schemaResult38);
            return toolResult39;
        }

        McpToolDescriptor resolveToolResult40()
        {
            var dictionary41 = new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(),
                ["maxItems"] = NumberSchema()
            };
            var schemaResult42 = Schema(properties: dictionary41, required: ["projectId"]);
            var toolResult43 = Tool("sereinflow_get_project_libraries", "Read bounded library artifacts referenced by one project.", schemaResult42);
            return toolResult43;
        }

        McpToolDescriptor resolveToolResult44()
        {
            var dictionary45 = new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(),
                ["upgradeId"] = StringSchema()
            };
            var schemaResult46 = Schema(properties: dictionary45, required: ["projectId", "upgradeId"]);
            var toolResult47 = Tool("sereinflow_get_library_upgrade", "Read one persisted project library upgrade analysis and per-flow results.", schemaResult46);
            return toolResult47;
        }

        McpToolDescriptor resolveToolResult48()
        {
            var dictionary49 = new Dictionary<string, object?>
            {
                ["runId"] = StringSchema(),
                ["afterEventSequence"] = NumberSchema(),
                ["track"] = StringEnumSchema("development or production", "development", "production"),
                ["maxItems"] = NumberSchema(),
                ["maxJsonBytes"] = NumberSchema(),
                ["includeFlowLiteralValues"] = BooleanSchema(),
                ["includeScriptSource"] = BooleanSchema()
            };
            var schemaResult50 = Schema(properties: dictionary49, required: ["runId"]);
            var toolResult51 = Tool("sereinflow_get_run_inspection", "Read a bounded run snapshot, timeline, node outputs and debug state. track accepts development or production when supplied.", schemaResult50);
            return toolResult51;
        }

        McpToolDescriptor resolveToolResult52()
        {
            var dictionary53 = new Dictionary<string, object?>
            {
                ["runId"] = StringSchema(),
                ["name"] = StringSchema(),
                ["offset"] = NumberSchema(),
                ["take"] = NumberSchema()
            };
            var schemaResult54 = Schema(properties: dictionary53, required: ["runId"]);
            var toolResult55 = Tool("sereinflow_get_run_variables", "Read live run variables or the saved final snapshot with CLR types and bounded JSON values. Supply name for one variable; otherwise use offset/take (1-200). Complex CLR objects expose metadata only. Requires run.read; older runs or forcibly terminated Workers may have no saved snapshot.", schemaResult54);
            return toolResult55;
        }

        McpToolDescriptor resolveToolResult56()
        {
            var dictionary57 = new Dictionary<string, object?>
            {
                ["runId"] = StringSchema(),
                ["name"] = StringSchema(),
                ["value"] = new
                {
                },
                ["valueType"] = StringEnumSchema("Stored value type.", "null", "boolean", "string", "int32", "int64", "double", "decimal", "json"),
                ["idempotencyKey"] = StringSchema("A fresh key for this exact variable write request.")
            };
            var schemaResult58 = Schema(properties: dictionary57, required: ["runId", "name", "value", "valueType", "idempotencyKey"]);
            var toolResult59 = Tool("sereinflow_set_run_variable", "Set one live run variable using an explicit built-in CLR value type. Requires run.variable.write and an idempotencyKey; does not persist across runs.", schemaResult58);
            return toolResult59;
        }

        McpToolDescriptor resolveToolResult60()
        {
            var dictionary61 = new Dictionary<string, object?>
            {
                ["runId"] = StringSchema()
            };
            var schemaResult62 = Schema(properties: dictionary61, required: ["runId"]);
            var toolResult63 = Tool("sereinflow_list_run_workpieces", "List image and file workpieces uploaded by nodes in one run.", schemaResult62);
            return toolResult63;
        }

        McpToolDescriptor resolveToolResult64()
        {
            var dictionary65 = new Dictionary<string, object?>
            {
                ["runId"] = StringSchema(),
                ["workpieceId"] = StringSchema()
            };
            var schemaResult66 = Schema(properties: dictionary65, required: ["runId", "workpieceId"]);
            var toolResult67 = Tool("sereinflow_get_run_workpiece", "Read metadata and the API download URL for one run workpiece.", schemaResult66);
            return toolResult67;
        }

        McpToolDescriptor resolveToolResult68()
        {
            var dictionary69 = new Dictionary<string, object?>
            {
                ["runId"] = StringSchema(),
                ["topic"] = StringSchema(),
                ["payload"] = new
                {
                },
                ["channelKind"] = new
                {
                    type = "string",
                    @enum = new[]
                    {
                        "queue",
                        "eventBus"
                    }
                },
                ["contractId"] = StringSchema(),
                ["messageId"] = StringSchema(),
                ["idempotencyKey"] = StringSchema("A fresh key for this exact message publish request.")
            };
            var schemaResult70 = Schema(properties: dictionary69, required: ["runId", "topic", "payload", "idempotencyKey"]);
            var toolResult71 = Tool("sereinflow_publish_run_message", "Publish an arbitrary JSON value to an explicitly exposed endpoint in an active run. channelKind is optional and defaults to queue; canonical values are queue or eventBus. The result acknowledges Worker broker acceptance, not downstream business completion.", schemaResult70);
            return toolResult71;
        }

        McpToolDescriptor resolveToolResult72()
        {
            var dictionary73 = new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(),
                ["status"] = StringEnumSchema("Run status filter.", "pending", "running", "succeeded", "failed", "cancelled", "timedOut", "interrupted"),
                ["maxItems"] = NumberSchema()
            };
            var schemaResult74 = Schema(properties: dictionary73);
            var toolResult75 = Tool("sereinflow_list_runs", "List bounded run summaries, optionally filtered by project and status. status accepts pending, running, succeeded, failed, cancelled, timedOut or interrupted.", schemaResult74);
            return toolResult75;
        }

        McpToolDescriptor resolveToolResult76()
        {
            var dictionary77 = new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(),
                ["maxItems"] = NumberSchema()
            };
            var schemaResult78 = Schema(properties: dictionary77);
            var toolResult79 = Tool("sereinflow_list_debug_sessions", "List bounded active debug session summaries.", schemaResult78);
            return toolResult79;
        }

        McpToolDescriptor resolveToolResult80()
        {
            var dictionary81 = new Dictionary<string, object?>
            {
                ["sessionId"] = StringSchema()
            };
            var schemaResult82 = Schema(properties: dictionary81, required: ["sessionId"]);
            var toolResult83 = Tool("sereinflow_get_debug_state", "Read the structured state of one debug session.", schemaResult82);
            return toolResult83;
        }

        McpToolDescriptor resolveToolResult84()
        {
            var dictionary85 = new Dictionary<string, object?>
            {
                ["sessionId"] = StringSchema(),
                ["afterRevision"] = NumberSchema(),
                ["timeoutSeconds"] = NumberSchema()
            };
            var schemaResult86 = Schema(properties: dictionary85, required: ["sessionId", "afterRevision"]);
            var toolResult87 = Tool("sereinflow_wait_debug_state", "Wait for a debug session state revision to change or reach a terminal state.", schemaResult86);
            return toolResult87;
        }

        McpToolDescriptor resolveToolResult88()
        {
            var dictionary89 = new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(),
                ["flowId"] = StringSchema(),
                ["breakpointNodeIds"] = resolveArraySchemaResult(),
                ["projectInputs"] = new
                {
                    type = "object"
                },
                ["timeoutSeconds"] = NumberSchema(),
                ["maxSteps"] = NumberSchema(),
                ["maxNodeVisits"] = NumberSchema(),
                ["expectedFlowVersion"] = NumberSchema(),
                ["maxQueuedFlipflopTriggers"] = NumberSchema(),
                ["idempotencyKey"] = StringSchema("A fresh key for this exact debug start request.")
            };
            var schemaResult90 = Schema(properties: dictionary89, required: ["projectId", "flowId", "idempotencyKey"]);
            var toolResult91 = Tool("sereinflow_start_debug_session", "Start a bounded debug Worker session for a saved flow.", schemaResult90);
            return toolResult91;
            object resolveArraySchemaResult()
            {
                var stringSchemaResult = StringSchema();
                var arraySchemaResult = ArraySchema(stringSchemaResult);
                return arraySchemaResult;
            }
        }

        McpToolDescriptor resolveToolResult92()
        {
            var dictionary93 = new Dictionary<string, object?>
            {
                ["sessionId"] = StringSchema(),
                ["commandSequence"] = NumberSchema()
            };
            var schemaResult94 = Schema(properties: dictionary93, required: ["sessionId", "commandSequence"]);
            var toolResult95 = Tool("sereinflow_continue_debug", "Continue a paused debug session.", schemaResult94);
            return toolResult95;
        }

        McpToolDescriptor resolveToolResult96()
        {
            var dictionary97 = new Dictionary<string, object?>
            {
                ["sessionId"] = StringSchema(),
                ["commandSequence"] = NumberSchema()
            };
            var schemaResult98 = Schema(properties: dictionary97, required: ["sessionId", "commandSequence"]);
            var toolResult99 = Tool("sereinflow_step_debug", "Execute one node boundary in a paused debug session.", schemaResult98);
            return toolResult99;
        }

        McpToolDescriptor resolveToolResult100()
        {
            var dictionary101 = new Dictionary<string, object?>
            {
                ["sessionId"] = StringSchema(),
                ["commandSequence"] = NumberSchema()
            };
            var schemaResult102 = Schema(properties: dictionary101, required: ["sessionId", "commandSequence"]);
            var toolResult103 = Tool("sereinflow_stop_debug", "Stop an active debug session.", schemaResult102);
            return toolResult103;
        }

        McpToolDescriptor resolveToolResult104()
        {
            var dictionary105 = new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(),
                ["flowId"] = StringSchema(),
                ["includeScriptSource"] = BooleanSchema(),
                ["includeFlowLiteralValues"] = BooleanSchema()
            };
            var schemaResult106 = Schema(properties: dictionary105, required: ["projectId", "flowId"]);
            var toolResult107 = Tool("sereinflow_get_flow_edit_model", "Read the development flow model used to create safe structured patches.", schemaResult106);
            return toolResult107;
        }

        McpToolDescriptor resolveToolResult108()
        {
            var dictionary109 = new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(),
                ["libraryId"] = StringSchema(),
                ["libraryNodeContractId"] = StringSchema(),
                ["position"] = PositionSchema()
            };
            var schemaResult110 = Schema(properties: dictionary109, required: ["projectId", "libraryId", "libraryNodeContractId", "position"]);
            var toolResult111 = Tool("sereinflow_create_library_node_template", "Create a read-only Action or Flipflop node template from a library contract already attached to the project.", schemaResult110);
            return toolResult111;
        }

        McpToolDescriptor resolveToolResult112()
        {
            var dictionary113 = new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(),
                ["builtinNodeId"] = StringSchema(),
                ["position"] = PositionSchema()
            };
            var schemaResult114 = Schema(properties: dictionary113, required: ["projectId", "builtinNodeId", "position"]);
            var toolResult115 = Tool("sereinflow_create_builtin_node_template", "Create a complete schema 2.0 patch node from a built-in node descriptor in the flow edit model.", schemaResult114);
            return toolResult115;
        }

        McpToolDescriptor resolveToolResult116()
        {
            var dictionary117 = new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(),
                ["flowId"] = StringSchema(),
                ["expectedDevelopmentVersion"] = NumberSchema(),
                ["schemaVersion"] = StringSchema(),
                ["operations"] = FlowPatchOperationsSchema(),
                ["remark"] = StringSchema()
            };
            var schemaResult118 = Schema(properties: dictionary117, required: ["projectId", "flowId", "expectedDevelopmentVersion", "operations"]);
            var toolResult119 = Tool("sereinflow_preview_flow_patch", "Validate a structured development-flow patch and return its diff. New requests use schemaVersion 2.0, canonical camelCase enums, an op discriminator, and named payload fields. For connection payloads, kind=execution uses branch=success/failure/error and dataSource=null; kind=data uses branch=null and dataSource=previousNode. The dataSource field is required but nullable in schema 2.0. Removing the current entry node automatically clears entryNodeId when that node is absent from the final canvas, so removing the last node produces a savable empty draft. Legacy operation/value input remains read-compatible.", schemaResult118);
            return toolResult119;
        }

        McpToolDescriptor resolveToolResult120()
        {
            var applySchemaPropertiesResult121 = ApplySchemaProperties();
            var schemaResult122 = Schema(properties: applySchemaPropertiesResult121, required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"]);
            var toolResult123 = Tool("sereinflow_apply_flow_patch", "Apply a previously previewed flow patch after task-level authorization.", schemaResult122);
            return toolResult123;
        }

        McpToolDescriptor resolveToolResult124()
        {
            var dictionary125 = new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(),
                ["flowId"] = StringSchema(),
                ["track"] = StringEnumSchema("development or production", "development", "production"),
                ["fromVersion"] = NumberSchema(),
                ["toVersion"] = NumberSchema()
            };
            var schemaResult126 = Schema(properties: dictionary125, required: ["projectId", "flowId", "track", "fromVersion", "toVersion"]);
            var toolResult127 = Tool("sereinflow_compare_flow_versions", "Compare two immutable versions on the selected track. track accepts development or production.", schemaResult126);
            return toolResult127;
        }

        McpToolDescriptor resolveToolResult128()
        {
            var dictionary129 = new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(),
                ["flowId"] = StringSchema(),
                ["expectedDevelopmentVersion"] = NumberSchema(),
                ["remark"] = StringSchema()
            };
            var schemaResult130 = Schema(properties: dictionary129, required: ["projectId", "flowId", "expectedDevelopmentVersion"]);
            var toolResult131 = Tool("sereinflow_preview_publish_flow", "Validate and preview publishing the current development flow.", schemaResult130);
            return toolResult131;
        }

        McpToolDescriptor resolveToolResult132()
        {
            var applySchemaPropertiesResult133 = ApplySchemaProperties();
            var schemaResult134 = Schema(properties: applySchemaPropertiesResult133, required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"]);
            var toolResult135 = Tool("sereinflow_apply_publish_flow", "Publish a previously previewed development version.", schemaResult134);
            return toolResult135;
        }

        McpToolDescriptor resolveToolResult136()
        {
            var dictionary137 = new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(),
                ["flowId"] = StringSchema(),
                ["track"] = StringEnumSchema("development or production", "development", "production"),
                ["sourceVersion"] = NumberSchema(),
                ["expectedHeadVersion"] = NumberSchema()
            };
            var schemaResult138 = Schema(properties: dictionary137, required: ["projectId", "flowId", "track", "sourceVersion", "expectedHeadVersion"]);
            var toolResult139 = Tool("sereinflow_preview_rollback_flow", "Preview rollback of a selected development or production version. track accepts development or production.", schemaResult138);
            return toolResult139;
        }

        McpToolDescriptor resolveToolResult140()
        {
            var applySchemaPropertiesResult141 = ApplySchemaProperties();
            var schemaResult142 = Schema(properties: applySchemaPropertiesResult141, required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"]);
            var toolResult143 = Tool("sereinflow_apply_rollback_flow", "Rollback a previously previewed flow version.", schemaResult142);
            return toolResult143;
        }

        McpToolDescriptor resolveToolResult144()
        {
            var dictionary145 = new Dictionary<string, object?>
            {
                ["source"] = StringSchema(),
                ["sourceName"] = StringSchema(),
                ["languageVersion"] = StringSchema(),
                ["inputs"] = ArraySchema()
            };
            var schemaResult146 = Schema(properties: dictionary145, required: ["source", "languageVersion", "inputs"]);
            var toolResult147 = Tool("sereinflow_compile_sereinlang", "Compile SereinLang source and return bounded diagnostics without changing a flow.", schemaResult146);
            return toolResult147;
        }

        McpToolDescriptor resolveToolResult148()
        {
            var dictionary149 = new Dictionary<string, object?>
            {
                ["fileName"] = StringSchema(),
                ["packageBase64"] = StringSchema(),
                ["projectId"] = StringSchema(),
                ["familyId"] = StringSchema(),
                ["baselineArtifactId"] = StringSchema()
            };
            var schemaResult150 = Schema(properties: dictionary149, required: ["fileName", "packageBase64"]);
            var toolResult151 = Tool("sereinflow_preview_library_package", "Inspect a completed library publish ZIP, including its managed and native dependencies, without persisting it.", schemaResult150);
            return toolResult151;
        }

        McpToolDescriptor resolveToolResult152()
        {
            var dictionary153 = new Dictionary<string, object?>
            {
                ["previewId"] = StringSchema(),
                ["previewFingerprint"] = StringSchema(),
                ["confirmation"] = StringSchema(),
                ["idempotencyKey"] = StringSchema(),
                ["fileName"] = StringSchema(),
                ["packageBase64"] = StringSchema()
            };
            var schemaResult154 = Schema(properties: dictionary153, required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"]);
            var toolResult155 = Tool("sereinflow_apply_library_package", "Import a previously inspected library ZIP after task-level authorization.", schemaResult154);
            return toolResult155;
        }

        McpToolDescriptor resolveToolResult156()
        {
            var dictionary157 = new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(),
                ["libraryId"] = StringSchema()
            };
            var schemaResult158 = Schema(properties: dictionary157, required: ["projectId", "libraryId"]);
            var toolResult159 = Tool("sereinflow_preview_project_library_attach", "Preview adding an existing immutable library artifact to a project.", schemaResult158);
            return toolResult159;
        }

        McpToolDescriptor resolveToolResult160()
        {
            var applySchemaPropertiesResult161 = ApplySchemaProperties();
            var schemaResult162 = Schema(properties: applySchemaPropertiesResult161, required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"]);
            var toolResult163 = Tool("sereinflow_apply_project_library_attach", "Apply a previously previewed project library attachment within the requesting library task.", schemaResult162);
            return toolResult163;
        }

        McpToolDescriptor resolveToolResult164()
        {
            var dictionary165 = new Dictionary<string, object?>
            {
                ["libraryId"] = StringSchema(),
                ["familyId"] = StringSchema(),
                ["name"] = StringSchema(),
                ["description"] = StringSchema()
            };
            var schemaResult166 = Schema(properties: dictionary165, required: ["libraryId"]);
            var toolResult167 = Tool("sereinflow_preview_library_family_assignment", "Preview assigning an immutable library artifact to an existing family or a new family.", schemaResult166);
            return toolResult167;
        }

        McpToolDescriptor resolveToolResult168()
        {
            var applySchemaPropertiesResult169 = ApplySchemaProperties();
            var schemaResult170 = Schema(properties: applySchemaPropertiesResult169, required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"]);
            var toolResult171 = Tool("sereinflow_apply_library_family_assignment", "Apply a previously previewed global library family assignment.", schemaResult170);
            return toolResult171;
        }

        McpToolDescriptor resolveToolResult172()
        {
            var dictionary176 = new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(),
                ["sourceArtifactId"] = StringSchema(),
                ["targetArtifactId"] = StringSchema(),
                ["flowIds"] = resolveArraySchemaResult173()
            };
            var schemaResult177 = Schema(properties: dictionary176, required: ["projectId", "sourceArtifactId", "targetArtifactId", "flowIds"]);
            var toolResult178 = Tool("sereinflow_preview_library_upgrade", "Analyze a same-family library upgrade for selected development flows without changing them.", schemaResult177);
            return toolResult178;
            object resolveArraySchemaResult173()
            {
                var stringSchemaResult174 = StringSchema();
                var arraySchemaResult175 = ArraySchema(stringSchemaResult174, minItems: 1);
                return arraySchemaResult175;
            }
        }

        McpToolDescriptor resolveToolResult179()
        {
            var dictionary186 = new Dictionary<string, object?>
            {
                ["previewId"] = StringSchema(),
                ["previewFingerprint"] = StringSchema(),
                ["confirmation"] = new
                {
                    type = "string",
                    @enum = new[]
                    {
                        "APPLY"
                    }
                },
                ["idempotencyKey"] = StringSchema(),
                ["flows"] = resolveArraySchemaResult180()
            };
            var schemaResult187 = Schema(properties: dictionary186, required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey", "flows"]);
            var toolResult188 = Tool("sereinflow_apply_library_upgrade", "Apply selected flows from a previously previewed library upgrade after explicit confirmation.", schemaResult187);
            return toolResult188;
            object resolveArraySchemaResult180()
            {
                var dictionary184 = new Dictionary<string, object?>
                {
                    ["flowId"] = StringSchema(),
                    ["expectedFlowVersion"] = NumberSchema(),
                    ["acknowledgedItemIds"] = resolveArraySchemaResult181()
                };
                var evaluatedValue = new
                {
                    type = "object",
                    properties = dictionary184,
                    required = new[]
                    {
                        "flowId",
                        "expectedFlowVersion"
                    },
                    additionalProperties = false
                };
                var arraySchemaResult185 = ArraySchema(evaluatedValue, minItems: 1);
                return arraySchemaResult185;
                object resolveArraySchemaResult181()
                {
                    var stringSchemaResult182 = StringSchema();
                    var arraySchemaResult183 = ArraySchema(stringSchemaResult182);
                    return arraySchemaResult183;
                }
            }
        }

        McpToolDescriptor resolveToolResult189()
        {
            var schemaResult190 = Schema();
            var toolResult191 = Tool("sereinflow_list_mcp_api_keys", "List API keys visible to the administrator.", schemaResult190);
            return toolResult191;
        }

        McpToolDescriptor resolveToolResult192()
        {
            var dictionary195 = new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(),
                ["name"] = StringSchema(),
                ["permissions"] = resolveArraySchemaResult193(),
                ["expiresAt"] = StringSchema(),
                ["idempotencyKey"] = StringSchema(),
                ["isAdministrator"] = BooleanSchema()
            };
            var schemaResult196 = Schema(properties: dictionary195, required: ["name", "permissions", "idempotencyKey"]);
            var toolResult197 = Tool("sereinflow_create_mcp_api_key", "Create a project-scoped or administrator MCP API key; permissions use stable dotted names and the secret is returned once.", schemaResult196);
            return toolResult197;
            object resolveArraySchemaResult193()
            {
                var toArrayResult = Enum.GetValues<McpPermissionDto>()
                    .Select(McpPermissionNames.ToName)
                    .ToArray();
                var stringEnumSchemaResult = StringEnumSchema("Stable dotted MCP permission name.", toArrayResult);
                var arraySchemaResult194 = ArraySchema(stringEnumSchemaResult);
                return arraySchemaResult194;
            }
        }

        McpToolDescriptor resolveToolResult198()
        {
            var dictionary199 = new Dictionary<string, object?>
            {
                ["keyId"] = StringSchema(),
                ["idempotencyKey"] = StringSchema()
            };
            var schemaResult200 = Schema(properties: dictionary199, required: ["keyId", "idempotencyKey"]);
            var toolResult201 = Tool("sereinflow_revoke_mcp_api_key", "Revoke an MCP API key.", schemaResult200);
            return toolResult201;
        }

        McpToolDescriptor resolveToolResult202()
        {
            var dictionary203 = new Dictionary<string, object?>
            {
                ["keyId"] = StringSchema(),
                ["idempotencyKey"] = StringSchema()
            };
            var schemaResult204 = Schema(properties: dictionary203, required: ["keyId", "idempotencyKey"]);
            var toolResult205 = Tool("sereinflow_rotate_mcp_api_key", "Rotate an MCP API key and return the replacement secret once.", schemaResult204);
            return toolResult205;
        }
    }
}

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
    private static readonly IReadOnlyList<McpToolDescriptor> ToolDescriptors =
    [
        Tool("sereinflow_list_projects", "List bounded non-archived project and flow summaries.", Schema(
            properties: new Dictionary<string, object?> { ["maxItems"] = NumberSchema() })),
        Tool("sereinflow_list_archived_projects", "List bounded archived project and flow summaries.", Schema(
            properties: new Dictionary<string, object?> { ["maxItems"] = NumberSchema() })),
        Tool("sereinflow_preview_create_project", "Preview creating a new draft project with an empty main flow.", Schema(
            properties: new Dictionary<string, object?> { ["name"] = StringSchema(), ["flowName"] = StringSchema() }, required: ["name"])),
        Tool("sereinflow_apply_create_project", "Create a previously previewed draft project after task-level authorization.", Schema(
            properties: ApplySchemaProperties(), required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"])),
        Tool("sereinflow_get_project", "Get one project and its flow summaries.", Schema(
            properties: new Dictionary<string, object?> { ["projectId"] = StringSchema() }, required: ["projectId"])),
        Tool("sereinflow_get_flow_topology", "Read a bounded flow topology for development or production.", Schema(
            properties: new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(), ["flowId"] = StringSchema(), ["track"] = StringSchema("development or production"),
                ["version"] = NumberSchema(), ["maxItems"] = NumberSchema(), ["maxJsonBytes"] = NumberSchema(),
                ["includeFlowLiteralValues"] = BooleanSchema(), ["includeScriptSource"] = BooleanSchema()
            }, required: ["projectId", "flowId"])),
        Tool("sereinflow_list_libraries", "List bounded available library artifact summaries. Set includeArchived only for the legacy all-lifecycles view; use sereinflow_list_archived_libraries for archived artifacts.", Schema(
            properties: new Dictionary<string, object?> { ["includeArchived"] = BooleanSchema(), ["maxItems"] = NumberSchema() })),
        Tool("sereinflow_list_archived_libraries", "List bounded archived library artifact summaries.", Schema(
            properties: new Dictionary<string, object?> { ["maxItems"] = NumberSchema() })),
        Tool("sereinflow_get_library", "Read one library's scanned node and parameter contracts.", Schema(
            properties: new Dictionary<string, object?> { ["libraryId"] = StringSchema() }, required: ["libraryId"])),
        Tool("sereinflow_list_library_families", "List bounded library families and their immutable artifact versions.", Schema(
            properties: new Dictionary<string, object?> { ["includeArchivedArtifacts"] = BooleanSchema(), ["maxItems"] = NumberSchema() })),
        Tool("sereinflow_get_library_family", "Read one library family and its immutable artifact versions.", Schema(
            properties: new Dictionary<string, object?> { ["familyId"] = StringSchema() }, required: ["familyId"])),
        Tool("sereinflow_get_project_libraries", "Read bounded library artifacts referenced by one project.", Schema(
            properties: new Dictionary<string, object?> { ["projectId"] = StringSchema(), ["maxItems"] = NumberSchema() }, required: ["projectId"])),
        Tool("sereinflow_get_library_upgrade", "Read one persisted project library upgrade analysis and per-flow results.", Schema(
            properties: new Dictionary<string, object?> { ["projectId"] = StringSchema(), ["upgradeId"] = StringSchema() }, required: ["projectId", "upgradeId"])),
        Tool("sereinflow_get_run_inspection", "Read a bounded run snapshot, timeline, node outputs and debug state.", Schema(
            properties: new Dictionary<string, object?>
            {
                ["runId"] = StringSchema(), ["afterEventSequence"] = NumberSchema(), ["track"] = StringSchema("development or production"),
                ["maxItems"] = NumberSchema(), ["maxJsonBytes"] = NumberSchema(),
                ["includeFlowLiteralValues"] = BooleanSchema(), ["includeScriptSource"] = BooleanSchema()
            }, required: ["runId"])),
        Tool("sereinflow_list_run_workpieces", "List image and file workpieces uploaded by nodes in one run.", Schema(
            properties: new Dictionary<string, object?> { ["runId"] = StringSchema() }, required: ["runId"])),
        Tool("sereinflow_get_run_workpiece", "Read metadata and the API download URL for one run workpiece.", Schema(
            properties: new Dictionary<string, object?> { ["runId"] = StringSchema(), ["workpieceId"] = StringSchema() }, required: ["runId", "workpieceId"])),
        Tool("sereinflow_publish_run_message", "Publish an arbitrary JSON value to an explicitly exposed endpoint in an active run. The result acknowledges Worker broker acceptance, not downstream business completion.", Schema(
            properties: new Dictionary<string, object?>
            {
                ["runId"] = StringSchema(),
                ["topic"] = StringSchema(),
                ["payload"] = new { },
                ["channelKind"] = new { type = "string", @enum = new[] { "queue", "eventBus" } },
                ["contractId"] = StringSchema(),
                ["messageId"] = StringSchema(),
                ["idempotencyKey"] = StringSchema("A fresh key for this exact message publish request.")
            }, required: ["runId", "topic", "payload", "idempotencyKey"])),
        Tool("sereinflow_list_runs", "List bounded run summaries, optionally filtered by project and status.", Schema(
            properties: new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(), ["status"] = StringSchema(), ["maxItems"] = NumberSchema()
            })),
        Tool("sereinflow_list_debug_sessions", "List bounded active debug session summaries.", Schema(
            properties: new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(), ["maxItems"] = NumberSchema()
            })),
        Tool("sereinflow_get_debug_state", "Read the structured state of one debug session.", Schema(
            properties: new Dictionary<string, object?> { ["sessionId"] = StringSchema() }, required: ["sessionId"])),
        Tool("sereinflow_wait_debug_state", "Wait for a debug session state revision to change or reach a terminal state.", Schema(
            properties: new Dictionary<string, object?> { ["sessionId"] = StringSchema(), ["afterRevision"] = NumberSchema(), ["timeoutSeconds"] = NumberSchema() }, required: ["sessionId", "afterRevision"])),
        Tool("sereinflow_start_debug_session", "Start a bounded debug Worker session for a saved flow.", Schema(
            properties: new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(), ["flowId"] = StringSchema(),
                ["breakpointNodeIds"] = ArraySchema(StringSchema()), ["projectInputs"] = new { type = "object" },
                ["timeoutSeconds"] = NumberSchema(), ["maxSteps"] = NumberSchema(), ["maxNodeVisits"] = NumberSchema(),
                ["expectedFlowVersion"] = NumberSchema(), ["maxQueuedFlipflopTriggers"] = NumberSchema(),
                ["idempotencyKey"] = StringSchema("A fresh key for this exact debug start request.")
            }, required: ["projectId", "flowId", "idempotencyKey"])),
        Tool("sereinflow_continue_debug", "Continue a paused debug session.", Schema(
            properties: new Dictionary<string, object?> { ["sessionId"] = StringSchema(), ["commandSequence"] = NumberSchema() },
            required: ["sessionId", "commandSequence"])),
        Tool("sereinflow_step_debug", "Execute one node boundary in a paused debug session.", Schema(
            properties: new Dictionary<string, object?> { ["sessionId"] = StringSchema(), ["commandSequence"] = NumberSchema() },
            required: ["sessionId", "commandSequence"])),
        Tool("sereinflow_stop_debug", "Stop an active debug session.", Schema(
            properties: new Dictionary<string, object?> { ["sessionId"] = StringSchema(), ["commandSequence"] = NumberSchema() },
            required: ["sessionId", "commandSequence"])),
        Tool("sereinflow_get_flow_edit_model", "Read the development flow model used to create safe structured patches.", Schema(
            properties: new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(), ["flowId"] = StringSchema(),
                ["includeScriptSource"] = BooleanSchema(), ["includeFlowLiteralValues"] = BooleanSchema()
            }, required: ["projectId", "flowId"])),
        Tool("sereinflow_create_library_node_template", "Create a read-only Action or Flipflop node template from a library contract already attached to the project.", Schema(
            properties: new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(), ["libraryId"] = StringSchema(),
                ["libraryNodeContractId"] = StringSchema(), ["position"] = PositionSchema()
            }, required: ["projectId", "libraryId", "libraryNodeContractId", "position"])),
        Tool("sereinflow_preview_flow_patch", "Validate a structured development-flow patch and return its diff. New requests use schemaVersion 2.0, canonical camelCase enums, an op discriminator, and named payload fields. Legacy operation/value input remains read-compatible.", Schema(
            properties: new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(), ["flowId"] = StringSchema(), ["expectedDevelopmentVersion"] = NumberSchema(),
                ["schemaVersion"] = StringSchema(), ["operations"] = FlowPatchOperationsSchema(), ["remark"] = StringSchema()
            }, required: ["projectId", "flowId", "expectedDevelopmentVersion", "operations"])),
        Tool("sereinflow_apply_flow_patch", "Apply a previously previewed flow patch after task-level authorization.", Schema(
            properties: ApplySchemaProperties(), required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"])),
        Tool("sereinflow_compare_flow_versions", "Compare two immutable versions on the selected track.", Schema(
            properties: new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(), ["flowId"] = StringSchema(), ["track"] = StringSchema(),
                ["fromVersion"] = NumberSchema(), ["toVersion"] = NumberSchema()
            }, required: ["projectId", "flowId", "track", "fromVersion", "toVersion"])),
        Tool("sereinflow_preview_publish_flow", "Validate and preview publishing the current development flow.", Schema(
            properties: new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(), ["flowId"] = StringSchema(), ["expectedDevelopmentVersion"] = NumberSchema(), ["remark"] = StringSchema()
            }, required: ["projectId", "flowId", "expectedDevelopmentVersion"])),
        Tool("sereinflow_apply_publish_flow", "Publish a previously previewed development version.", Schema(
            properties: ApplySchemaProperties(), required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"])),
        Tool("sereinflow_preview_rollback_flow", "Preview rollback of a selected development or production version.", Schema(
            properties: new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(), ["flowId"] = StringSchema(), ["track"] = StringSchema(),
                ["sourceVersion"] = NumberSchema(), ["expectedHeadVersion"] = NumberSchema()
            }, required: ["projectId", "flowId", "track", "sourceVersion", "expectedHeadVersion"])),
        Tool("sereinflow_apply_rollback_flow", "Rollback a previously previewed flow version.", Schema(
            properties: ApplySchemaProperties(), required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"])),
        Tool("sereinflow_compile_sereinlang", "Compile SereinLang source and return bounded diagnostics without changing a flow.", Schema(
            properties: new Dictionary<string, object?>
            {
                ["source"] = StringSchema(), ["sourceName"] = StringSchema(), ["languageVersion"] = StringSchema(), ["inputs"] = ArraySchema()
            }, required: ["source", "languageVersion", "inputs"])),
        Tool("sereinflow_preview_library_package", "Inspect a completed library publish ZIP, including its managed and native dependencies, without persisting it.", Schema(
            properties: new Dictionary<string, object?>
            {
                ["fileName"] = StringSchema(), ["packageBase64"] = StringSchema(), ["projectId"] = StringSchema(),
                ["familyId"] = StringSchema(), ["baselineArtifactId"] = StringSchema()
            }, required: ["fileName", "packageBase64"])),
        Tool("sereinflow_apply_library_package", "Import a previously inspected library ZIP after task-level authorization.", Schema(
            properties: new Dictionary<string, object?>
            {
                ["previewId"] = StringSchema(), ["previewFingerprint"] = StringSchema(), ["confirmation"] = StringSchema(),
                ["idempotencyKey"] = StringSchema(), ["fileName"] = StringSchema(), ["packageBase64"] = StringSchema()
            }, required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"])),
        Tool("sereinflow_preview_project_library_attach", "Preview adding an existing immutable library artifact to a project.", Schema(
            properties: new Dictionary<string, object?> { ["projectId"] = StringSchema(), ["libraryId"] = StringSchema() }, required: ["projectId", "libraryId"])),
        Tool("sereinflow_apply_project_library_attach", "Apply a previously previewed project library attachment within the requesting library task.", Schema(
            properties: ApplySchemaProperties(), required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"])),
        Tool("sereinflow_preview_library_family_assignment", "Preview assigning an immutable library artifact to an existing family or a new family.", Schema(
            properties: new Dictionary<string, object?>
            {
                ["libraryId"] = StringSchema(), ["familyId"] = StringSchema(), ["name"] = StringSchema(), ["description"] = StringSchema()
            }, required: ["libraryId"])),
        Tool("sereinflow_apply_library_family_assignment", "Apply a previously previewed global library family assignment.", Schema(
            properties: ApplySchemaProperties(), required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"])),
        Tool("sereinflow_preview_library_upgrade", "Analyze a same-family library upgrade for selected development flows without changing them.", Schema(
            properties: new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(), ["sourceArtifactId"] = StringSchema(), ["targetArtifactId"] = StringSchema(),
                ["flowIds"] = ArraySchema(StringSchema(), minItems: 1)
            }, required: ["projectId", "sourceArtifactId", "targetArtifactId", "flowIds"])),
        Tool("sereinflow_apply_library_upgrade", "Apply selected flows from a previously previewed library upgrade after explicit confirmation.", Schema(
            properties: new Dictionary<string, object?>
            {
                ["previewId"] = StringSchema(), ["previewFingerprint"] = StringSchema(),
                ["confirmation"] = new { type = "string", @enum = new[] { "APPLY" } }, ["idempotencyKey"] = StringSchema(),
                ["flows"] = ArraySchema(new
                {
                    type = "object",
                    properties = new Dictionary<string, object?>
                    {
                        ["flowId"] = StringSchema(), ["expectedFlowVersion"] = NumberSchema(),
                        ["acknowledgedItemIds"] = ArraySchema(StringSchema())
                    },
                    required = new[] { "flowId", "expectedFlowVersion" },
                    additionalProperties = false
                }, minItems: 1)
            }, required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey", "flows"])),
        Tool("sereinflow_list_mcp_api_keys", "List API keys visible to the administrator.", Schema()),
        Tool("sereinflow_create_mcp_api_key", "Create a project-scoped MCP API key; the secret is returned once.", Schema(
            properties: new Dictionary<string, object?>
            {
                ["projectId"] = StringSchema(), ["name"] = StringSchema(), ["permissions"] = ArraySchema(),
                ["expiresAt"] = StringSchema(), ["idempotencyKey"] = StringSchema(), ["isAdministrator"] = BooleanSchema()
            }, required: ["name", "permissions", "idempotencyKey"])),
        Tool("sereinflow_revoke_mcp_api_key", "Revoke an MCP API key.", Schema(
            properties: new Dictionary<string, object?> { ["keyId"] = StringSchema(), ["idempotencyKey"] = StringSchema() }, required: ["keyId", "idempotencyKey"])),
        Tool("sereinflow_rotate_mcp_api_key", "Rotate an MCP API key and return the replacement secret once.", Schema(
            properties: new Dictionary<string, object?> { ["keyId"] = StringSchema(), ["idempotencyKey"] = StringSchema() }, required: ["keyId", "idempotencyKey"]))
    ];

    internal static McpToolCatalog Create()
        => new(
        [
            Read("sereinflow_list_projects", McpPermissionDto.ProjectRead,
                static (context, arguments, cancellationToken) => McpReadModelToolHandlers.ListProjectsAsync(context, arguments, cancellationToken)),
            Read("sereinflow_list_archived_projects", McpPermissionDto.ProjectRead,
                static (context, arguments, cancellationToken) => McpReadModelToolHandlers.ListArchivedProjectsAsync(context, arguments, cancellationToken)),
            Mutation("sereinflow_preview_create_project", McpPermissionDto.ProjectWrite,
                static (context, arguments, cancellationToken) => McpProjectToolHandlers.PreviewCreateAsync(context, arguments, cancellationToken)),
            Mutation("sereinflow_apply_create_project", McpPermissionDto.ProjectWrite,
                static (context, arguments, cancellationToken) => McpProjectToolHandlers.ApplyCreateAsync(context, arguments, cancellationToken),
                requiresIdempotencyKey: true),
            Read("sereinflow_get_project", McpPermissionDto.ProjectRead,
                static (context, arguments, cancellationToken) => McpReadModelToolHandlers.GetProjectAsync(context, arguments, cancellationToken)),
            Read("sereinflow_get_flow_topology", McpPermissionDto.ProjectRead,
                static (context, arguments, cancellationToken) => McpReadModelToolHandlers.GetFlowTopologyAsync(context, arguments, cancellationToken)),
            Read("sereinflow_list_libraries", McpPermissionDto.LibraryRead,
                static (context, arguments, cancellationToken) => McpReadModelToolHandlers.ListLibrariesAsync(context, arguments, cancellationToken)),
            Read("sereinflow_list_archived_libraries", McpPermissionDto.LibraryRead,
                static (context, arguments, cancellationToken) => McpReadModelToolHandlers.ListArchivedLibrariesAsync(context, arguments, cancellationToken)),
            Read("sereinflow_get_library", McpPermissionDto.LibraryRead,
                static (context, arguments, cancellationToken) => McpReadModelToolHandlers.GetLibraryAsync(context, arguments, cancellationToken)),
            Read("sereinflow_list_library_families", McpPermissionDto.LibraryRead,
                static (context, arguments, cancellationToken) => McpReadModelToolHandlers.ListLibraryFamiliesAsync(context, arguments, cancellationToken)),
            Read("sereinflow_get_library_family", McpPermissionDto.LibraryRead,
                static (context, arguments, cancellationToken) => McpReadModelToolHandlers.GetLibraryFamilyAsync(context, arguments, cancellationToken)),
            Read("sereinflow_get_project_libraries", McpPermissionDto.LibraryRead,
                static (context, arguments, cancellationToken) => McpReadModelToolHandlers.GetProjectLibrariesAsync(context, arguments, cancellationToken)),
            Read("sereinflow_get_library_upgrade", McpPermissionDto.LibraryRead,
                static (context, arguments, cancellationToken) => McpReadModelToolHandlers.GetLibraryUpgradeAsync(context, arguments, cancellationToken)),
            Read("sereinflow_get_run_inspection", McpPermissionDto.RunRead,
                static (context, arguments, cancellationToken) => McpReadModelToolHandlers.GetRunInspectionAsync(context, arguments, cancellationToken)),
            ReadAny("sereinflow_list_run_workpieces",
                static (context, arguments, cancellationToken) => McpReadModelToolHandlers.ListRunWorkpiecesAsync(context, arguments, cancellationToken)),
            ReadAny("sereinflow_get_run_workpiece",
                static (context, arguments, cancellationToken) => McpReadModelToolHandlers.GetRunWorkpieceAsync(context, arguments, cancellationToken)),
            Mutation("sereinflow_publish_run_message", McpPermissionDto.RunMessagePublish,
                static (context, arguments, cancellationToken) => McpRunMessageToolHandlers.PublishAsync(context, arguments, cancellationToken),
                requiresIdempotencyKey: true),
            ReadAny("sereinflow_list_runs",
                static (context, arguments, cancellationToken) => McpDebugToolHandlers.ListRunsAsync(context, arguments, cancellationToken)),
            ReadAny("sereinflow_list_debug_sessions",
                static (context, arguments, cancellationToken) => McpDebugToolHandlers.ListDebugSessionsAsync(context, arguments, cancellationToken)),
            ReadAny("sereinflow_get_debug_state",
                static (context, arguments, cancellationToken) => McpReadModelToolHandlers.GetDebugStateAsync(context, arguments, cancellationToken)),
            ReadAny("sereinflow_wait_debug_state",
                static (context, arguments, cancellationToken) => McpReadModelToolHandlers.WaitForDebugStateAsync(context, arguments, cancellationToken)),
            Mutation("sereinflow_start_debug_session", McpPermissionDto.DebugControl,
                static (context, arguments, cancellationToken) => McpDebugToolHandlers.StartAsync(context, arguments, cancellationToken),
                requiresIdempotencyKey: true),
            Mutation("sereinflow_continue_debug", McpPermissionDto.DebugControl,
                static (context, arguments, cancellationToken) => McpDebugToolHandlers.ContinueAsync(context, arguments, cancellationToken)),
            Mutation("sereinflow_step_debug", McpPermissionDto.DebugControl,
                static (context, arguments, cancellationToken) => McpDebugToolHandlers.StepAsync(context, arguments, cancellationToken)),
            Mutation("sereinflow_stop_debug", McpPermissionDto.DebugControl,
                static (context, arguments, cancellationToken) => McpDebugToolHandlers.StopAsync(context, arguments, cancellationToken)),
            Read("sereinflow_get_flow_edit_model", McpPermissionDto.ProjectRead,
                static (context, arguments, cancellationToken) => McpReadModelToolHandlers.GetFlowEditModelAsync(context, arguments, cancellationToken)),
            Read("sereinflow_create_library_node_template", McpPermissionDto.ProjectRead,
                static (context, arguments, cancellationToken) => McpLibraryToolHandlers.CreateNodeTemplateAsync(context, arguments, cancellationToken)),
            Mutation("sereinflow_preview_flow_patch", McpPermissionDto.FlowWrite,
                static (context, arguments, cancellationToken) => McpFlowToolHandlers.PreviewPatchAsync(context, arguments, cancellationToken),
                auditTrack: FlowVersionTrackDto.Development),
            Mutation("sereinflow_apply_flow_patch", McpPermissionDto.FlowWrite,
                static (context, arguments, cancellationToken) => McpFlowToolHandlers.ApplyPatchAsync(context, arguments, cancellationToken),
                requiresIdempotencyKey: true,
                auditTrack: FlowVersionTrackDto.Development),
            Read("sereinflow_compare_flow_versions", McpPermissionDto.ProjectRead,
                static (context, arguments, cancellationToken) => McpFlowToolHandlers.CompareVersionsAsync(context, arguments, cancellationToken)),
            Mutation("sereinflow_preview_publish_flow", McpPermissionDto.FlowPublish,
                static (context, arguments, cancellationToken) => McpFlowToolHandlers.PreviewPublishAsync(context, arguments, cancellationToken),
                auditTrack: FlowVersionTrackDto.Development),
            Mutation("sereinflow_apply_publish_flow", McpPermissionDto.FlowPublish,
                static (context, arguments, cancellationToken) => McpFlowToolHandlers.ApplyPublishAsync(context, arguments, cancellationToken),
                requiresIdempotencyKey: true,
                auditTrack: FlowVersionTrackDto.Development),
            Mutation("sereinflow_preview_rollback_flow", McpPermissionDto.FlowRollback,
                static (context, arguments, cancellationToken) => McpFlowToolHandlers.PreviewRollbackAsync(context, arguments, cancellationToken)),
            Mutation("sereinflow_apply_rollback_flow", McpPermissionDto.FlowRollback,
                static (context, arguments, cancellationToken) => McpFlowToolHandlers.ApplyRollbackAsync(context, arguments, cancellationToken),
                requiresIdempotencyKey: true),
            Read("sereinflow_compile_sereinlang", McpPermissionDto.ScriptCompile,
                static (context, arguments, cancellationToken) => McpLibraryToolHandlers.CompileScriptAsync(context, arguments, cancellationToken)),
            Mutation("sereinflow_preview_library_package", McpPermissionDto.LibraryImport,
                static (context, arguments, cancellationToken) => McpLibraryToolHandlers.PreviewPackageAsync(context, arguments, cancellationToken)),
            Mutation("sereinflow_apply_library_package", McpPermissionDto.LibraryImport,
                static (context, arguments, cancellationToken) => McpLibraryToolHandlers.ApplyPackageAsync(context, arguments, cancellationToken),
                requiresIdempotencyKey: true),
            Mutation("sereinflow_preview_project_library_attach", McpPermissionDto.LibraryManage,
                static (context, arguments, cancellationToken) => McpLibraryToolHandlers.PreviewProjectAttachAsync(context, arguments, cancellationToken)),
            Mutation("sereinflow_apply_project_library_attach", McpPermissionDto.LibraryManage,
                static (context, arguments, cancellationToken) => McpLibraryToolHandlers.ApplyProjectAttachAsync(context, arguments, cancellationToken),
                requiresIdempotencyKey: true),
            Mutation("sereinflow_preview_library_family_assignment", McpPermissionDto.LibraryManage,
                static (context, arguments, cancellationToken) => McpLibraryToolHandlers.PreviewLibraryFamilyAssignmentAsync(context, arguments, cancellationToken)),
            Mutation("sereinflow_apply_library_family_assignment", McpPermissionDto.LibraryManage,
                static (context, arguments, cancellationToken) => McpLibraryToolHandlers.ApplyLibraryFamilyAssignmentAsync(context, arguments, cancellationToken),
                requiresIdempotencyKey: true),
            Mutation("sereinflow_preview_library_upgrade", McpPermissionDto.FlowWrite,
                static (context, arguments, cancellationToken) => McpLibraryToolHandlers.PreviewLibraryUpgradeAsync(context, arguments, cancellationToken)),
            Mutation("sereinflow_apply_library_upgrade", McpPermissionDto.FlowWrite,
                static (context, arguments, cancellationToken) => McpLibraryToolHandlers.ApplyLibraryUpgradeAsync(context, arguments, cancellationToken),
                requiresIdempotencyKey: true),
            Read("sereinflow_list_mcp_api_keys", McpPermissionDto.McpKeysManage,
                static (context, _, cancellationToken) => McpApiKeyToolHandlers.ListAsync(context, cancellationToken)),
            Mutation("sereinflow_create_mcp_api_key", McpPermissionDto.McpKeysManage,
                static (context, arguments, cancellationToken) => McpApiKeyToolHandlers.CreateAsync(context, arguments, cancellationToken),
                requiresIdempotencyKey: true),
            Mutation("sereinflow_revoke_mcp_api_key", McpPermissionDto.McpKeysManage,
                static (context, arguments, cancellationToken) => McpApiKeyToolHandlers.RevokeAsync(context, arguments, cancellationToken),
                requiresIdempotencyKey: true),
            Mutation("sereinflow_rotate_mcp_api_key", McpPermissionDto.McpKeysManage,
                static (context, arguments, cancellationToken) => McpApiKeyToolHandlers.RotateAsync(context, arguments, cancellationToken),
                requiresIdempotencyKey: true)
        ]);

    private static McpToolDefinition Read<T>(
        string name,
        McpPermissionDto permission,
        Func<McpToolContext, JsonElement, CancellationToken, Task<T>> execute)
        => Define(
            name,
            McpToolExecutionKind.Read,
            permission,
            async (context, arguments, cancellationToken) => (object?)await execute(context, arguments, cancellationToken),
            false,
            null);

    private static McpToolDefinition ReadAny<T>(
        string name,
        Func<McpToolContext, JsonElement, CancellationToken, Task<T>> execute)
        => new(
            GetToolDescriptor(name),
            McpToolExecutionKind.Read,
            async (context, arguments, cancellationToken) => (object?)await execute(context, arguments, cancellationToken),
            (context, arguments) => context.Security.RequireAny(
                context.Principal,
                TryGetGuid(arguments, "projectId"),
                McpPermissionDto.DebugRead,
                McpPermissionDto.RunRead));

    private static McpToolDefinition Mutation<T>(
        string name,
        McpPermissionDto permission,
        Func<McpToolContext, JsonElement, CancellationToken, Task<T>> execute,
        bool requiresIdempotencyKey = false,
        FlowVersionTrackDto? auditTrack = null)
        => Define(
            name,
            McpToolExecutionKind.Mutation,
            permission,
            async (context, arguments, cancellationToken) => (object?)await execute(context, arguments, cancellationToken),
            requiresIdempotencyKey,
            auditTrack);

    private static McpToolDefinition Define(
        string name,
        McpToolExecutionKind executionKind,
        McpPermissionDto permission,
        Func<McpToolContext, JsonElement, CancellationToken, Task<object?>> execute,
        bool requiresIdempotencyKey,
        FlowVersionTrackDto? auditTrack)
        => new(
            GetToolDescriptor(name),
            executionKind,
            execute,
            (context, arguments) => context.Security.Require(context.Principal, permission, TryGetGuid(arguments, "projectId")),
            requiresIdempotencyKey,
            auditTrack);

    private static McpToolDescriptor GetToolDescriptor(string name)
        => ToolDescriptors.Single(item => string.Equals(item.Name, name, StringComparison.Ordinal));

}

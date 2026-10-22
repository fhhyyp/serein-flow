using System.Text.Json;
using SereinFlow.Contracts;
using static SereinFlow.Mcp.Support.McpSchemaBuilder;
using static SereinFlow.Mcp.Tools.McpToolSchemas;

namespace SereinFlow.Mcp.Tools;

/// <summary>
/// Published tool contracts, including their descriptions, schemas and listing order.
/// 已发布的工具契约，包含描述、架构和列表顺序。
/// </summary>
internal static class SereinFlowMcpToolCatalogFactory
{
    private static readonly IReadOnlyList<McpToolDescriptor> ToolDescriptors =
    [
        Tool(
            "sereinflow_list_projects",
            "List bounded non-archived project and flow summaries.",
            Schema(properties: new Dictionary<string, McpJsonSchema> { { "maxItems", NumberSchema() } })),

        Tool(
            "sereinflow_list_archived_projects",
            "List bounded archived project and flow summaries.",
            Schema(properties: new Dictionary<string, McpJsonSchema> { { "maxItems", NumberSchema() } })),

        Tool(
            "sereinflow_preview_create_project",
            "Preview creating a new draft project with an empty main flow.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema> { { "name", StringSchema() }, { "flowName", StringSchema() } },
                required: ["name"])),

        Tool(
            "sereinflow_apply_create_project",
            "Create a previously previewed draft project after task-level authorization.",
            Schema(
                properties: ApplySchemaProperties(),
                required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"])),

        Tool(
            "sereinflow_get_project",
            "Get one project and its flow summaries.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema> { { "projectId", StringSchema() } },
                required: ["projectId"])),

        Tool(
            "sereinflow_get_flow_topology",
            "Read a bounded flow topology. track defaults to development and accepts development or production.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "projectId", StringSchema() },
                    { "flowId", StringSchema() },
                    { "track", StringEnumSchema("development or production", "development", "production") },
                    { "version", NumberSchema() },
                    { "maxItems", NumberSchema() },
                    { "maxJsonBytes", NumberSchema() },
                    { "includeFlowLiteralValues", BooleanSchema() },
                    { "includeScriptSource", BooleanSchema() }
                },
                required: ["projectId", "flowId"])),

        Tool(
            "sereinflow_list_libraries",
            "List bounded available library artifact summaries. Set includeArchived only for the legacy all-lifecycles view; use sereinflow_list_archived_libraries for archived artifacts.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "includeArchived", BooleanSchema() },
                    { "maxItems", NumberSchema() }
                })),

        Tool(
            "sereinflow_list_archived_libraries",
            "List bounded archived library artifact summaries.",
            Schema(properties: new Dictionary<string, McpJsonSchema> { { "maxItems", NumberSchema() } })),

        Tool(
            "sereinflow_get_library",
            "Read one library's scanned node and parameter contracts.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema> { { "libraryId", StringSchema() } },
                required: ["libraryId"])),

        Tool(
            "sereinflow_list_library_families",
            "List bounded library families and their immutable artifact versions.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "includeArchivedArtifacts", BooleanSchema() },
                    { "maxItems", NumberSchema() }
                })),

        Tool(
            "sereinflow_get_library_family",
            "Read one library family and its immutable artifact versions.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema> { { "familyId", StringSchema() } },
                required: ["familyId"])),

        Tool(
            "sereinflow_get_project_libraries",
            "Read bounded library artifacts referenced by one project.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema> { { "projectId", StringSchema() }, { "maxItems", NumberSchema() } },
                required: ["projectId"])),

        Tool(
            "sereinflow_get_library_upgrade",
            "Read one persisted project library upgrade analysis and per-flow results.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema> { { "projectId", StringSchema() }, { "upgradeId", StringSchema() } },
                required: ["projectId", "upgradeId"])),

        Tool(
            "sereinflow_get_run_inspection",
            "Read a bounded run snapshot, timeline, node outputs and debug state. track accepts development or production when supplied.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "runId", StringSchema() },
                    { "afterEventSequence", NumberSchema() },
                    { "track", StringEnumSchema("development or production", "development", "production") },
                    { "maxItems", NumberSchema() },
                    { "maxJsonBytes", NumberSchema() },
                    { "includeFlowLiteralValues", BooleanSchema() },
                    { "includeScriptSource", BooleanSchema() }
                },
                required: ["runId"])),

        Tool(
            "sereinflow_get_run_variables",
            "Read live run variables or the saved final snapshot with CLR types and bounded JSON values. Supply name for one variable; otherwise use offset/take (1-200). Complex CLR objects expose metadata only. Requires run.read; older runs or forcibly terminated Workers may have no saved snapshot.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "runId", StringSchema() },
                    { "name", StringSchema() },
                    { "offset", NumberSchema() },
                    { "take", NumberSchema() }
                },
                required: ["runId"])),

        Tool(
            "sereinflow_set_run_variable",
            "Set one live run variable using an explicit built-in CLR value type. Requires run.variable.write and an idempotencyKey; does not persist across runs.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "runId", StringSchema() },
                    { "name", StringSchema() },
                    { "value", new McpJsonSchema() },
                    { "valueType", StringEnumSchema(
                            "Stored value type.",
                            "null",
                            "boolean",
                            "string",
                            "int32",
                            "int64",
                            "double",
                            "decimal",
                            "json") },
                    { "idempotencyKey", StringSchema("A fresh key for this exact variable write request.") }
                },
                required: ["runId", "name", "value", "valueType", "idempotencyKey"])),

        Tool(
            "sereinflow_list_run_workpieces",
            "List image and file workpieces uploaded by nodes in one run.",
            Schema(properties: new Dictionary<string, McpJsonSchema> { { "runId", StringSchema() } }, required: ["runId"])),

        Tool(
            "sereinflow_get_run_workpiece",
            "Read metadata and the API download URL for one run workpiece.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema> { { "runId", StringSchema() }, { "workpieceId", StringSchema() } },
                required: ["runId", "workpieceId"])),

        Tool(
            "sereinflow_publish_run_message",
            "Publish an arbitrary JSON value to an explicitly exposed endpoint in an active run. channelKind is optional and defaults to queue; canonical values are queue or eventBus. The result acknowledges Worker broker acceptance, not downstream business completion.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "runId", StringSchema() },
                    { "topic", StringSchema() },
                    { "payload", new McpJsonSchema() },
                    { "channelKind", new McpJsonSchema
                        {
                            Type = "string",
                            Enum = JsonSerializer.SerializeToElement(new[] { "queue", "eventBus" })
                        } },
                    { "contractId", StringSchema() },
                    { "messageId", StringSchema() },
                    { "idempotencyKey", StringSchema("A fresh key for this exact message publish request.") }
                },
                required: ["runId", "topic", "payload", "idempotencyKey"])),

        Tool(
            "sereinflow_list_runs",
            "List bounded run summaries, optionally filtered by project and status. status accepts pending, running, succeeded, failed, cancelled, timedOut or interrupted.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "projectId", StringSchema() },
                    { "status", StringEnumSchema(
                            "Run status filter.",
                            "pending",
                            "running",
                            "succeeded",
                            "failed",
                            "cancelled",
                            "timedOut",
                            "interrupted") },
                    { "maxItems", NumberSchema() }
                })),

        Tool(
            "sereinflow_list_debug_sessions",
            "List bounded active debug session summaries.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema> { { "projectId", StringSchema() }, { "maxItems", NumberSchema() } })),

        Tool(
            "sereinflow_get_debug_state",
            "Read the structured state of one debug session.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema> { { "sessionId", StringSchema() } },
                required: ["sessionId"])),

        Tool(
            "sereinflow_wait_debug_state",
            "Wait for a newer paused debug state or a terminal state. Running progress remains available through sereinflow_get_debug_state.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "sessionId", StringSchema() },
                    { "afterRevision", NumberSchema() },
                    { "timeoutSeconds", NumberSchema() }
                },
                required: ["sessionId", "afterRevision"])),

        Tool(
            "sereinflow_start_debug_session",
            "Start a bounded debug Worker session for a saved flow.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "projectId", StringSchema() },
                    { "flowId", StringSchema() },
                    { "breakpointNodeIds", ArraySchema(StringSchema()) },
                    { "projectInputs", new McpJsonSchema { Type = "object" } },
                    { "timeoutSeconds", NumberSchema() },
                    { "maxSteps", NumberSchema() },
                    { "maxNodeVisits", NumberSchema() },
                    { "expectedFlowVersion", NumberSchema() },
                    { "maxQueuedFlipflopTriggers", NumberSchema() },
                    { "idempotencyKey", StringSchema("A fresh key for this exact debug start request.") }
                },
                required: ["projectId", "flowId", "idempotencyKey"])),

        Tool(
            "sereinflow_continue_debug",
            "Continue a paused debug session.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "sessionId", StringSchema() },
                    { "commandSequence", NumberSchema() }
                },
                required: ["sessionId", "commandSequence"])),

        Tool(
            "sereinflow_step_debug",
            "Execute one node boundary in a paused debug session.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "sessionId", StringSchema() },
                    { "commandSequence", NumberSchema() }
                },
                required: ["sessionId", "commandSequence"])),

        Tool(
            "sereinflow_stop_debug",
            "Stop an active debug session.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "sessionId", StringSchema() },
                    { "commandSequence", NumberSchema() }
                },
                required: ["sessionId", "commandSequence"])),

        Tool(
            "sereinflow_get_flow_edit_model",
            "Read the development flow model used to create safe structured patches.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "projectId", StringSchema() },
                    { "flowId", StringSchema() },
                    { "includeScriptSource", BooleanSchema() },
                    { "includeFlowLiteralValues", BooleanSchema() }
                },
                required: ["projectId", "flowId"])),

        Tool(
            "sereinflow_create_library_node_template",
            "Create a read-only Action or Flipflop node template from a library contract already attached to the project.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "projectId", StringSchema() },
                    { "libraryId", StringSchema() },
                    { "libraryNodeContractId", StringSchema() },
                    { "position", PositionSchema() }
                },
                required: ["projectId", "libraryId", "libraryNodeContractId", "position"])),

        Tool(
            "sereinflow_create_builtin_node_template",
            "Create a complete schema 2.0 patch node from a built-in node descriptor in the flow edit model.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "projectId", StringSchema() },
                    { "builtinNodeId", StringSchema() },
                    { "position", PositionSchema() }
                },
                required: ["projectId", "builtinNodeId", "position"])),

        Tool(
            "sereinflow_preview_flow_patch",
            "Validate a structured development-flow patch and return its diff. New requests use schemaVersion 2.0, canonical camelCase enums, an op discriminator, and named payload fields. For connection payloads, kind=execution uses branch=success/failure/error and dataSource=null; kind=data uses branch=null and dataSource=previousNode. The dataSource field is required but nullable in schema 2.0. Removing the current entry node automatically clears entryNodeId when that node is absent from the final canvas, so removing the last node produces a savable empty draft. Legacy operation/value input remains read-compatible.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "projectId", StringSchema() },
                    { "flowId", StringSchema() },
                    { "expectedDevelopmentVersion", NumberSchema() },
                    { "schemaVersion", StringSchema() },
                    { "operations", FlowPatchOperationsSchema() },
                    { "remark", StringSchema() }
                },
                required: ["projectId", "flowId", "expectedDevelopmentVersion", "operations"])),

        Tool(
            "sereinflow_apply_flow_patch",
            "Apply a previously previewed flow patch after task-level authorization.",
            Schema(
                properties: ApplySchemaProperties(),
                required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"])),

        Tool(
            "sereinflow_compare_flow_versions",
            "Compare two immutable versions on the selected track. track accepts development or production.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "projectId", StringSchema() },
                    { "flowId", StringSchema() },
                    { "track", StringEnumSchema("development or production", "development", "production") },
                    { "fromVersion", NumberSchema() },
                    { "toVersion", NumberSchema() }
                },
                required: ["projectId", "flowId", "track", "fromVersion", "toVersion"])),

        Tool(
            "sereinflow_preview_publish_flow",
            "Validate and preview publishing the current development flow.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "projectId", StringSchema() },
                    { "flowId", StringSchema() },
                    { "expectedDevelopmentVersion", NumberSchema() },
                    { "remark", StringSchema() }
                },
                required: ["projectId", "flowId", "expectedDevelopmentVersion"])),

        Tool(
            "sereinflow_apply_publish_flow",
            "Publish a previously previewed development version.",
            Schema(
                properties: ApplySchemaProperties(),
                required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"])),

        Tool(
            "sereinflow_preview_rollback_flow",
            "Preview rollback of a selected development or production version. track accepts development or production.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "projectId", StringSchema() },
                    { "flowId", StringSchema() },
                    { "track", StringEnumSchema("development or production", "development", "production") },
                    { "sourceVersion", NumberSchema() },
                    { "expectedHeadVersion", NumberSchema() }
                },
                required: ["projectId", "flowId", "track", "sourceVersion", "expectedHeadVersion"])),

        Tool(
            "sereinflow_apply_rollback_flow",
            "Rollback a previously previewed flow version.",
            Schema(
                properties: ApplySchemaProperties(),
                required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"])),

        Tool(
            "sereinflow_compile_sereinlang",
            "Compile SereinLang source and return bounded diagnostics without changing a flow.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "source", StringSchema() },
                    { "sourceName", StringSchema() },
                    { "languageVersion", StringSchema() },
                    { "inputs", ArraySchema() }
                },
                required: ["source", "languageVersion", "inputs"])),

        Tool(
            "sereinflow_preview_library_package",
            "Inspect a completed library publish ZIP, including its managed and native dependencies, without persisting it.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "fileName", StringSchema() },
                    { "packageBase64", StringSchema() },
                    { "projectId", StringSchema() },
                    { "familyId", StringSchema() },
                    { "baselineArtifactId", StringSchema() }
                },
                required: ["fileName", "packageBase64"])),

        Tool(
            "sereinflow_apply_library_package",
            "Import a previously inspected library ZIP after task-level authorization.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "previewId", StringSchema() },
                    { "previewFingerprint", StringSchema() },
                    { "confirmation", StringSchema() },
                    { "idempotencyKey", StringSchema() },
                    { "fileName", StringSchema() },
                    { "packageBase64", StringSchema() }
                },
                required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"])),

        Tool(
            "sereinflow_preview_project_library_attach",
            "Preview adding an existing immutable library artifact to a project.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema> { { "projectId", StringSchema() }, { "libraryId", StringSchema() } },
                required: ["projectId", "libraryId"])),

        Tool(
            "sereinflow_apply_project_library_attach",
            "Apply a previously previewed project library attachment within the requesting library task.",
            Schema(
                properties: ApplySchemaProperties(),
                required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"])),

        Tool(
            "sereinflow_preview_library_family_assignment",
            "Preview assigning an immutable library artifact to an existing family or a new family.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "libraryId", StringSchema() },
                    { "familyId", StringSchema() },
                    { "name", StringSchema() },
                    { "description", StringSchema() }
                },
                required: ["libraryId"])),

        Tool(
            "sereinflow_apply_library_family_assignment",
            "Apply a previously previewed global library family assignment.",
            Schema(
                properties: ApplySchemaProperties(),
                required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"])),

        Tool(
            "sereinflow_preview_library_upgrade",
            "Analyze a same-family library upgrade for selected development flows without changing them.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "projectId", StringSchema() },
                    { "sourceArtifactId", StringSchema() },
                    { "targetArtifactId", StringSchema() },
                    { "flowIds", ArraySchema(StringSchema(), minItems: 1) }
                },
                required: ["projectId", "sourceArtifactId", "targetArtifactId", "flowIds"])),

        Tool(
            "sereinflow_apply_library_upgrade",
            "Apply selected flows from a previously previewed library upgrade after explicit confirmation.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "previewId", StringSchema() },
                    { "previewFingerprint", StringSchema() },
                    { "confirmation", new McpJsonSchema { Type = "string", Enum = JsonSerializer.SerializeToElement(new[] { "APPLY" }) } },
                    { "idempotencyKey", StringSchema() },
                    { "flows", ArraySchema(
                            McpJsonSchema.Object(
                                new Dictionary<string, McpJsonSchema>
                                {
                                    { "flowId", StringSchema() },
                                    { "expectedFlowVersion", NumberSchema() },
                                    { "acknowledgedItemIds", ArraySchema(StringSchema()) }
                                },
                                ["flowId", "expectedFlowVersion"]),
                            minItems: 1) }
                },
                required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey", "flows"])),

        Tool("sereinflow_list_mcp_api_keys", "List API keys visible to the administrator.", Schema()),

        Tool(
            "sereinflow_create_mcp_api_key",
            "Create a project-scoped or administrator MCP API key; permissions use stable dotted names and the secret is returned once.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema>
                {
                    { "projectId", StringSchema() },
                    { "name", StringSchema() },
                    { "permissions", ArraySchema(
                            StringEnumSchema(
                                "Stable dotted MCP permission name.",
                                Enum.GetValues<McpPermissionDto>().Select(McpPermissionNames.ToName).ToArray())) },
                    { "expiresAt", StringSchema() },
                    { "idempotencyKey", StringSchema() },
                    { "isAdministrator", BooleanSchema() }
                },
                required: ["name", "permissions", "idempotencyKey"])),

        Tool(
            "sereinflow_revoke_mcp_api_key",
            "Revoke an MCP API key.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema> { { "keyId", StringSchema() }, { "idempotencyKey", StringSchema() } },
                required: ["keyId", "idempotencyKey"])),

        Tool(
            "sereinflow_rotate_mcp_api_key",
            "Rotate an MCP API key and return the replacement secret once.",
            Schema(
                properties: new Dictionary<string, McpJsonSchema> { { "keyId", StringSchema() }, { "idempotencyKey", StringSchema() } },
                required: ["keyId", "idempotencyKey"]))
    ];

    internal static McpToolCatalog Create() => SereinFlowMcpHosting.CreateCatalog(ToolDescriptors);
}

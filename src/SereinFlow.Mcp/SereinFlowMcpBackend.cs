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

public sealed partial class SereinFlowMcpBackend : ISereinFlowMcpBackend
{
    private static readonly JsonSerializerOptions ContractJsonOptions = SereinJsonSerialization.CreateContractOptions(options =>
    {
        // The dotted permission names are a public MCP contract. Keep the
        // dedicated converter ahead of the generic enum converter so replayed
        // idempotency responses accept those names as well.
        // 点号权限名称是 MCP 公开契约；必须优先于通用枚举转换器，确保幂等重放也能读取。
        options.Converters.Insert(0, new McpPermissionJsonConverter());
    });
    private static readonly IReadOnlyList<McpResourceDescriptor> Resources =
    [
        new("sereinflow://projects", "projects", "SereinFlow project summaries"),
        new("sereinflow://libraries", "libraries", "Available SereinFlow library artifacts")
    ];

    private static readonly IReadOnlyList<McpResourceTemplateDescriptor> ResourceTemplates =
    [
        new("sereinflow://projects/{projectId}", "project", "One SereinFlow project"),
        new("sereinflow://projects/{projectId}/flows/{flowId}/topology", "flow topology", "A development flow topology"),
        new("sereinflow://projects/{projectId}/flows/{flowId}/versions/{track}", "flow versions", "Flow version history for one track"),
        new("sereinflow://projects/{projectId}/flows/{flowId}/versions/{track}/{version}", "flow version", "One immutable flow version"),
        new("sereinflow://mcp-previews/{previewId}", "MCP preview", "One pending or completed MCP mutation preview"),
        new("sereinflow://libraries/{libraryId}", "library", "One SereinFlow library contract"),
        new("sereinflow://runs/{runId}", "run inspection", "A bounded run timeline, node output and debug inspection"),
        new("sereinflow://debug-sessions/{sessionId}", "debug session", "A structured debug session state")
    ];
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> MutationGates = new(StringComparer.Ordinal);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMcpPrincipalAccessor _principalAccessor;

    public SereinFlowMcpBackend(IServiceScopeFactory scopeFactory, IMcpPrincipalAccessor principalAccessor)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _principalAccessor = principalAccessor ?? throw new ArgumentNullException(nameof(principalAccessor));
    }

    public Task<IReadOnlyList<McpResourceDescriptor>> ListResourcesAsync(CancellationToken cancellationToken)
        => Task.FromResult(Resources);

    public Task<IReadOnlyList<McpResourceTemplateDescriptor>> ListResourceTemplatesAsync(CancellationToken cancellationToken)
        => Task.FromResult(ResourceTemplates);

    public Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<McpToolDescriptor>>(
        [
            Tool("sereinflow_list_projects", "List bounded project and flow summaries.", Schema(
                properties: new Dictionary<string, object?> { ["maxItems"] = NumberSchema() })),
            Tool("sereinflow_preview_create_project", "Preview creating a new draft project with an empty main flow.", Schema(
                properties: new Dictionary<string, object?>
                {
                    ["name"] = StringSchema(),
                    ["flowName"] = StringSchema()
                }, required: ["name"])),
            Tool("sereinflow_apply_create_project", "Create a previously previewed draft project after explicit confirmation.", Schema(
                properties: ApplySchemaProperties(), required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"])),
            Tool("sereinflow_get_project", "Get one project and its flow summaries.", Schema(
                properties: new Dictionary<string, object?> { ["projectId"] = StringSchema() },
                required: ["projectId"])),
            Tool("sereinflow_get_flow_topology", "Read a bounded flow topology for development or production.", Schema(
                properties: new Dictionary<string, object?>
                {
                    ["projectId"] = StringSchema(),
                    ["flowId"] = StringSchema(),
                    ["track"] = StringSchema("development or production"),
                    ["version"] = NumberSchema(),
                    ["maxItems"] = NumberSchema(),
                    ["maxJsonBytes"] = NumberSchema(),
                    ["includeFlowLiteralValues"] = BooleanSchema(),
                    ["includeScriptSource"] = BooleanSchema()
                },
                required: ["projectId", "flowId"])),
            Tool("sereinflow_list_libraries", "List bounded library artifact summaries.", Schema(
                properties: new Dictionary<string, object?>
                {
                    ["includeArchived"] = BooleanSchema(),
                    ["maxItems"] = NumberSchema()
                })),
            Tool("sereinflow_get_library", "Read one library's scanned node and parameter contracts.", Schema(
                properties: new Dictionary<string, object?> { ["libraryId"] = StringSchema() },
                required: ["libraryId"])),
            Tool("sereinflow_get_run_inspection", "Read a bounded run snapshot, timeline, node outputs and debug state.", Schema(
                properties: new Dictionary<string, object?>
                {
                    ["runId"] = StringSchema(),
                    ["afterEventSequence"] = NumberSchema(),
                    ["track"] = StringSchema("development or production"),
                    ["maxItems"] = NumberSchema(),
                    ["maxJsonBytes"] = NumberSchema(),
                    ["includeFlowLiteralValues"] = BooleanSchema(),
                    ["includeScriptSource"] = BooleanSchema()
                },
                required: ["runId"])),
            Tool("sereinflow_get_debug_state", "Read the structured state of one debug session.", Schema(
                properties: new Dictionary<string, object?> { ["sessionId"] = StringSchema() },
                required: ["sessionId"])),
            Tool("sereinflow_wait_debug_state", "Wait for a debug session state revision to change or reach a terminal state.", Schema(
                properties: new Dictionary<string, object?>
                {
                    ["sessionId"] = StringSchema(),
                    ["afterRevision"] = NumberSchema(),
                    ["timeoutSeconds"] = NumberSchema()
                },
                required: ["sessionId", "afterRevision"])),
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
                    ["projectId"] = StringSchema(), ["flowId"] = StringSchema(),
                    ["expectedDevelopmentVersion"] = NumberSchema(), ["schemaVersion"] = StringSchema(),
                    ["operations"] = FlowPatchOperationsSchema(), ["remark"] = StringSchema()
                }, required: ["projectId", "flowId", "expectedDevelopmentVersion", "operations"])),
            Tool("sereinflow_apply_flow_patch", "Apply a previously previewed flow patch after explicit confirmation.", Schema(
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
            Tool("sereinflow_apply_library_package", "Import a previously inspected library ZIP after explicit confirmation.", Schema(
                properties: new Dictionary<string, object?>
                {
                    ["previewId"] = StringSchema(), ["previewFingerprint"] = StringSchema(), ["confirmation"] = StringSchema(),
                    ["idempotencyKey"] = StringSchema(), ["fileName"] = StringSchema(), ["packageBase64"] = StringSchema()
                }, required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"])),
            Tool("sereinflow_preview_project_library_attach", "Preview adding an existing immutable library artifact to a project.", Schema(
                properties: new Dictionary<string, object?>
                {
                    ["projectId"] = StringSchema(), ["libraryId"] = StringSchema()
                }, required: ["projectId", "libraryId"])),
            Tool("sereinflow_apply_project_library_attach", "Apply a previously previewed project library attachment.", Schema(
                properties: ApplySchemaProperties(), required: ["previewId", "previewFingerprint", "confirmation", "idempotencyKey"])),
            Tool("sereinflow_list_mcp_api_keys", "List API keys visible to the administrator.", Schema()),
            Tool("sereinflow_create_mcp_api_key", "Create a project-scoped MCP API key; the secret is returned once.", Schema(
                properties: new Dictionary<string, object?>
                {
                    ["projectId"] = StringSchema(), ["name"] = StringSchema(), ["permissions"] = ArraySchema(), ["expiresAt"] = StringSchema(), ["idempotencyKey"] = StringSchema()
                    , ["isAdministrator"] = BooleanSchema()
                }, required: ["name", "permissions", "idempotencyKey"])),
            Tool("sereinflow_revoke_mcp_api_key", "Revoke an MCP API key.", Schema(
                properties: new Dictionary<string, object?> { ["keyId"] = StringSchema(), ["idempotencyKey"] = StringSchema() }, required: ["keyId", "idempotencyKey"]))
            , Tool("sereinflow_rotate_mcp_api_key", "Rotate an MCP API key and return the replacement secret once.", Schema(
                properties: new Dictionary<string, object?> { ["keyId"] = StringSchema(), ["idempotencyKey"] = StringSchema() }, required: ["keyId", "idempotencyKey"]))
        ]);

    public async Task<McpResourceReadResult> ReadResourceAsync(
        string uri,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
            || !string.Equals(parsed.Scheme, "sereinflow", StringComparison.OrdinalIgnoreCase))
        {
            throw new McpProtocolException(-32602, "The SereinFlow resource URI is invalid.");
        }

        var collection = parsed.Host.ToLowerInvariant();
        var segments = parsed.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AiReadModelService>();
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        var principal = _principalAccessor.Current;

        object? value = collection switch
        {
            "projects" when segments.Length == 0
                => await ReadProjectsAsync(scope, service, security, principal, cancellationToken),
            "projects" when segments.Length == 1 && Guid.TryParse(segments[0], out var projectId)
                => await ReadProjectAsync(service, security, principal, projectId, cancellationToken),
            "projects" when segments.Length == 4
                && Guid.TryParse(segments[0], out var topologyProjectId)
                && string.Equals(segments[1], "flows", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(segments[2], out var flowId)
                && string.Equals(segments[3], "topology", StringComparison.OrdinalIgnoreCase)
                => await ReadFlowTopologyAsync(
                    service, security, principal,
                    topologyProjectId,
                    flowId,
                    FlowVersionTrackDto.Development,
                    null,
                    null,
                    cancellationToken),
            "libraries" when segments.Length == 0
                => await ReadLibrariesAsync(scope, service, security, principal, cancellationToken),
            "libraries" when segments.Length == 1
                => await ReadLibraryAsync(scope, service, security, principal, segments[0], cancellationToken),
            "runs" when segments.Length == 1 && Guid.TryParse(segments[0], out var runId)
                => await ReadRunAsync(scope, service, security, principal, runId, cancellationToken),
            "debug-sessions" when segments.Length == 1 && Guid.TryParse(segments[0], out var sessionId)
                => await ReadDebugAsync(scope, service, security, principal, sessionId, cancellationToken),
            "projects" when segments.Length == 5
                && Guid.TryParse(segments[0], out var versionsProjectId)
                && string.Equals(segments[1], "flows", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(segments[2], out var versionsFlowId)
                && string.Equals(segments[3], "versions", StringComparison.OrdinalIgnoreCase)
                => await ReadVersionsAsync(security, principal, versionsProjectId, versionsFlowId, segments[4], scope, cancellationToken),
            "projects" when segments.Length == 6
                && Guid.TryParse(segments[0], out var versionProjectId)
                && string.Equals(segments[1], "flows", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(segments[2], out var versionFlowId)
                && string.Equals(segments[3], "versions", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(segments[5], out var version)
                => await ReadVersionAsync(
                    security,
                    principal,
                    versionProjectId,
                    versionFlowId,
                    segments[4],
                    version,
                    scope,
                    cancellationToken),
            "mcp-previews" when segments.Length == 1 && Guid.TryParse(segments[0], out var previewId)
                => await ReadPreviewAsync(scope, security, principal, previewId, cancellationToken),
            _ => throw new McpProtocolException(-32602, "The SereinFlow resource URI is not supported.")
        };

        if (value is null)
        {
            throw new McpProtocolException(
                -32004,
                "The requested SereinFlow resource was not found.");
        }

        return new(uri, value);
    }

    public async Task<McpToolCallResult> CallToolAsync(
        string name,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await CallToolCoreAsync(name, arguments, cancellationToken);
            stopwatch.Stop();
            await RecordAuditAsync(name, arguments, _principalAccessor.Current, "succeeded", null, result.Value, stopwatch.Elapsed);
            return result;
        }
        catch (McpSecurityException exception)
        {
            stopwatch.Stop();
            await RecordAuditAsync(name, arguments, _principalAccessor.Current, "denied", exception.Code, null, stopwatch.Elapsed);
            throw new McpProtocolException(exception.StatusCode == 401 ? -32001 : -32003, exception.Message, new { code = exception.Code });
        }
        catch (McpProtocolException exception)
        {
            stopwatch.Stop();
            await RecordAuditAsync(name, arguments, _principalAccessor.Current, "rejected", exception.Code.ToString(System.Globalization.CultureInfo.InvariantCulture), null, stopwatch.Elapsed);
            throw;
        }
        catch (Exception)
        {
            stopwatch.Stop();
            await RecordAuditAsync(name, arguments, _principalAccessor.Current, "failed", "internal_error", null, stopwatch.Elapsed);
            throw;
        }
    }

    private async Task<McpToolCallResult> CallToolCoreAsync(
        string name,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (arguments.ValueKind is not (JsonValueKind.Object or JsonValueKind.Undefined))
            throw new McpProtocolException(-32602, "MCP tool arguments must be a JSON object.");

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AiReadModelService>();
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        var principal = _principalAccessor.Current;
        AuthorizeTool(name, arguments, security, principal);
        using var mutationGate = await AcquireMutationGateAsync(name, arguments, cancellationToken);
        var options = ReadOptions(arguments);

        object? value = name switch
        {
            "sereinflow_list_projects"
                => await ReadProjectsAsync(scope, service, security, principal, cancellationToken, options),
            "sereinflow_preview_create_project"
                => await PreviewCreateProjectAsync(scope, principal!, arguments, cancellationToken),
            "sereinflow_apply_create_project"
                => await ApplyCreateProjectAsync(scope, principal!, arguments, cancellationToken),
            "sereinflow_get_project"
                => await ReadProjectAsync(
                    service,
                    security,
                    principal,
                    GetGuid(arguments, "projectId"),
                    cancellationToken),
            "sereinflow_get_flow_topology"
                => await ReadFlowTopologyAsync(
                    service,
                    security,
                    principal,
                    GetGuid(arguments, "projectId"),
                    GetGuid(arguments, "flowId"),
                    ReadTrack(arguments),
                    GetOptionalLong(arguments, "version"),
                    ReadAuthorizedOptions(
                        arguments,
                        security,
                        principal,
                        GetGuid(arguments, "projectId")),
                    cancellationToken),
            "sereinflow_list_libraries"
                => await ReadLibrariesAsync(scope, service, security, principal, cancellationToken, GetOptionalBool(arguments, "includeArchived") ?? false, options),
            "sereinflow_get_library"
                => await ReadLibraryAsync(scope, service, security, principal, GetRequiredString(arguments, "libraryId"), cancellationToken),
            "sereinflow_get_run_inspection"
                => await ReadRunAsync(scope, service, security, principal, GetGuid(arguments, "runId"), cancellationToken, ReadOptionalTrack(arguments), options, GetOptionalLong(arguments, "afterEventSequence") ?? 0),
            "sereinflow_get_debug_state"
                => await ReadDebugAsync(scope, service, security, principal, GetGuid(arguments, "sessionId"), cancellationToken),
            "sereinflow_wait_debug_state"
                => await WaitForDebugStateAsync(scope, service, security, principal, arguments, cancellationToken),
            "sereinflow_get_flow_edit_model"
                => await ReadFlowEditModelAsync(
                    scope,
                    service,
                    security,
                    principal,
                    GetGuid(arguments, "projectId"),
                    GetGuid(arguments, "flowId"),
                    ReadAuthorizedOptions(arguments, security, principal, GetGuid(arguments, "projectId")),
                    cancellationToken),
            "sereinflow_create_library_node_template"
                => await CreateLibraryNodeTemplateAsync(scope, principal!, arguments, cancellationToken),
            "sereinflow_preview_flow_patch"
                => await PreviewFlowPatchAsync(scope, principal!, arguments, cancellationToken),
            "sereinflow_apply_flow_patch"
                => await ApplyFlowPatchAsync(scope, principal!, arguments, cancellationToken),
            "sereinflow_compare_flow_versions"
                => await CompareFlowVersionsAsync(scope, principal!, arguments, cancellationToken),
            "sereinflow_preview_publish_flow"
                => await PreviewPublishAsync(scope, principal!, arguments, cancellationToken),
            "sereinflow_apply_publish_flow"
                => await ApplyPublishAsync(scope, principal!, arguments, cancellationToken),
            "sereinflow_preview_rollback_flow"
                => await PreviewRollbackAsync(scope, principal!, arguments, cancellationToken),
            "sereinflow_apply_rollback_flow"
                => await ApplyRollbackAsync(scope, principal!, arguments, cancellationToken),
            "sereinflow_compile_sereinlang"
                => await CompileScriptAsync(scope, arguments, cancellationToken),
            "sereinflow_preview_library_package"
                => await PreviewLibraryPackageAsync(scope, principal!, arguments, cancellationToken),
            "sereinflow_apply_library_package"
                => await ApplyLibraryPackageAsync(scope, principal!, arguments, cancellationToken),
            "sereinflow_preview_project_library_attach"
                => await PreviewProjectLibraryAttachAsync(scope, principal!, arguments, cancellationToken),
            "sereinflow_apply_project_library_attach"
                => await ApplyProjectLibraryAttachAsync(scope, principal!, arguments, cancellationToken),
            "sereinflow_list_mcp_api_keys"
                => await ListApiKeysAsync(scope, principal!, cancellationToken),
            "sereinflow_create_mcp_api_key"
                => await CreateApiKeyAsync(scope, principal!, arguments, cancellationToken),
            "sereinflow_revoke_mcp_api_key"
                => await RevokeApiKeyAsync(scope, principal!, arguments, cancellationToken),
            "sereinflow_rotate_mcp_api_key"
                => await RotateApiKeyAsync(scope, principal!, arguments, cancellationToken),
            _ => throw new McpProtocolException(-32601, $"MCP tool '{name}' is not supported.")
        };

        if (value is null)
            throw new McpProtocolException(-32004, "The requested SereinFlow resource was not found.");
        return new(value);
    }

    private async Task RecordAuditAsync(
        string operation,
        JsonElement arguments,
        McpPrincipal? principal,
        string outcome,
        string? summary,
        object? value,
        TimeSpan duration)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var rawArguments = arguments.ValueKind == JsonValueKind.Undefined ? string.Empty : arguments.GetRawText();
            var idempotencyKey = TryGetStringSilently(arguments, "idempotencyKey");
            var inputHash = string.IsNullOrWhiteSpace(idempotencyKey)
                ? McpIdempotencyService.HashKey(rawArguments)
                : McpIdempotencyService.HashKey(idempotencyKey);
            var inputBytes = Encoding.UTF8.GetByteCount(rawArguments);
            var outputBytes = value is null
                ? 0
                : Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(value, ContractJsonOptions));
            var previewId = TryGetGuidSilently(arguments, "previewId");
            var preview = previewId is null
                ? null
                : await scope.ServiceProvider.GetRequiredService<IMcpPreviewStore>()
                    .FindAsync(previewId.Value, CancellationToken.None);
            var projectId = TryGetGuidSilently(arguments, "projectId") ?? preview?.ProjectId;
            var flowId = TryGetGuidSilently(arguments, "flowId") ?? preview?.FlowId;
            var track = GetAuditTrack(operation, arguments);
            if (track is null && preview is not null)
                track = GetPreviewAuditTrack(preview);
            var flowVersion = GetAuditFlowVersion(arguments) ?? preview?.ExpectedVersion;
            await scope.ServiceProvider.GetRequiredService<IMcpAuditStore>().AddAsync(
                new McpAuditEntry(
                    Guid.NewGuid(),
                    principal?.Id ?? "anonymous",
                    operation,
                    projectId,
                    flowId,
                    track,
                    flowVersion,
                    previewId,
                    outcome,
                    inputHash,
                    summary,
                    DateTimeOffset.UtcNow,
                    Math.Max(0, (long)duration.TotalMilliseconds),
                    inputBytes,
                    outputBytes),
                CancellationToken.None);
        }
        catch
        {
            // Audit persistence must never expose a database failure as a
            // second, misleading MCP business error.
        }
    }

}

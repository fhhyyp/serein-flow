using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.McpServer;

public sealed class SereinFlowMcpBackend : ISereinFlowMcpBackend
{
    private static readonly JsonSerializerOptions ContractJsonOptions = SereinJsonSerialization.CreateWebOptions(options =>
    {
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
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
            Tool("sereinflow_preview_flow_patch", "Validate a structured development-flow patch and return its diff.", Schema(
                properties: new Dictionary<string, object?>
                {
                    ["projectId"] = StringSchema(), ["flowId"] = StringSchema(),
                    ["expectedDevelopmentVersion"] = NumberSchema(), ["operations"] = ArraySchema(), ["remark"] = StringSchema()
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
            Tool("sereinflow_preview_library_package", "Inspect a completed library ZIP without persisting it.", Schema(
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

    private static async Task<AiDebugStateWaitResultDto?> WaitForDebugStateAsync(
        IServiceScope scope,
        AiReadModelService service,
        McpSecurityService security,
        McpPrincipal? principal,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var afterRevision = GetOptionalLong(arguments, "afterRevision")
            ?? throw new McpProtocolException(-32602, "MCP parameter 'afterRevision' is required.");
        var timeoutSeconds = GetOptionalInt(arguments, "timeoutSeconds") ?? 15;
        if (afterRevision < 0)
            throw new McpProtocolException(-32602, "MCP parameter 'afterRevision' cannot be negative.");
        if (timeoutSeconds is < 0 or > 60)
            throw new McpProtocolException(-32602, "MCP parameter 'timeoutSeconds' must be between 0 and 60.");

        var result = await service.WaitForDebugStateChangeAsync(
            GetGuid(arguments, "sessionId"),
            afterRevision,
            TimeSpan.FromSeconds(timeoutSeconds),
            cancellationToken);
        if (result is not null)
            security.Require(principal, McpPermissionDto.DebugRead, result.State.ProjectId);
        return result;
    }

    private static async Task<object> ReadProjectsAsync(
        IServiceScope scope,
        AiReadModelService service,
        McpSecurityService security,
        McpPrincipal? principal,
        CancellationToken cancellationToken,
        AiReadModelOptions? options = null)
    {
        security.Require(principal, McpPermissionDto.ProjectRead);
        if (IsProjectScoped(principal))
        {
            var project = await service.GetProjectAsync(principal!.ProjectId!.Value, cancellationToken);
            return new AiPageDto<AiProjectSummaryDto>(
                AiReadModelContract.SchemaVersion,
                project is null ? [] : [project],
                false,
                null);
        }

        var page = await service.ListProjectsAsync(options, cancellationToken);
        return page;
    }

    private static async Task<object?> ReadProjectAsync(
        AiReadModelService service,
        McpSecurityService security,
        McpPrincipal? principal,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        return await service.GetProjectAsync(projectId, cancellationToken);
    }

    private static async Task<object?> ReadFlowTopologyAsync(
        AiReadModelService service,
        McpSecurityService security,
        McpPrincipal? principal,
        Guid projectId,
        Guid flowId,
        FlowVersionTrackDto track,
        long? version,
        AiReadModelOptions? options,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        RequireSensitiveRead(security, principal, projectId, options);
        return await service.GetFlowTopologyAsync(projectId, flowId, track, version, options, cancellationToken);
    }

    private static async Task<object?> ReadFlowEditModelAsync(
        IServiceScope scope,
        AiReadModelService service,
        McpSecurityService security,
        McpPrincipal? principal,
        Guid projectId,
        Guid flowId,
        AiReadModelOptions options,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        RequireSensitiveRead(security, principal, projectId, options);
        return await service.GetFlowEditModelAsync(
            projectId,
            flowId,
            scope.ServiceProvider.GetRequiredService<IBuiltinNodeCatalog>().GetCatalog(),
            options,
            cancellationToken);
    }

    private static async Task<object> ReadLibrariesAsync(
        IServiceScope scope,
        AiReadModelService service,
        McpSecurityService security,
        McpPrincipal? principal,
        CancellationToken cancellationToken,
        bool includeArchived = false,
        AiReadModelOptions? options = null)
    {
        security.Require(principal, McpPermissionDto.LibraryRead);
        if (!IsProjectScoped(principal))
            return await service.ListLibrariesAsync(includeArchived, options, cancellationToken);

        var references = await scope.ServiceProvider.GetRequiredService<IProjectLibraryReferenceRepository>()
            .ListByProjectAsync(principal!.ProjectId!.Value, cancellationToken);
        var maxItems = (options ?? new()).Normalize().MaxItems;
        var items = new List<AiLibrarySummaryDto>(maxItems + 1);
        foreach (var libraryId in references
            .Select(static item => item.LibraryId)
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static id => id, StringComparer.Ordinal))
        {
            var library = await service.GetLibraryAsync(libraryId, cancellationToken);
            if (library is null || (!includeArchived && !string.Equals(library.Lifecycle, LibraryLifecycleDto.Available.ToString(), StringComparison.OrdinalIgnoreCase)))
                continue;

            items.Add(library with { Nodes = null });
            if (items.Count > maxItems)
                break;
        }

        var hasMore = items.Count > maxItems;
        var visible = hasMore ? items.Take(maxItems).ToArray() : items.ToArray();
        return new AiPageDto<AiLibrarySummaryDto>(
            AiReadModelContract.SchemaVersion,
            visible,
            hasMore,
            hasMore && visible.Length > 0 ? visible[^1].Id : null);
    }

    private static async Task<object?> ReadLibraryAsync(
        IServiceScope scope,
        AiReadModelService service,
        McpSecurityService security,
        McpPrincipal? principal,
        string libraryId,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.LibraryRead);
        var library = await service.GetLibraryAsync(libraryId, cancellationToken);
        if (library is not null && IsProjectScoped(principal))
        {
            var referenced = await scope.ServiceProvider.GetRequiredService<IProjectLibraryReferenceRepository>()
                .IsReferencedAsync(principal!.ProjectId!.Value, library.Id, cancellationToken);
            if (!referenced)
                throw new McpSecurityException("mcp.library_access_denied", "The MCP caller cannot access this library artifact.", 403);
        }
        return library;
    }

    private static async Task<object?> ReadRunAsync(
        IServiceScope scope,
        AiReadModelService service,
        McpSecurityService security,
        McpPrincipal? principal,
        Guid runId,
        CancellationToken cancellationToken,
        FlowVersionTrackDto? definitionTrack = null,
        AiReadModelOptions? options = null,
        long afterEventSequence = 0)
    {
        security.Require(principal, McpPermissionDto.RunRead);
        var inspection = await service.GetRunInspectionAsync(runId, definitionTrack, options, afterEventSequence, cancellationToken);
        if (inspection is not null)
        {
            security.Require(principal, McpPermissionDto.RunRead, inspection.Run.ProjectId);
            RequireSensitiveRead(security, principal, inspection.Run.ProjectId, options);
        }
        return inspection;
    }

    private static async Task<object?> ReadDebugAsync(
        IServiceScope scope,
        AiReadModelService service,
        McpSecurityService security,
        McpPrincipal? principal,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.DebugRead);
        var state = await service.GetDebugStateAsync(sessionId, cancellationToken);
        if (state is not null)
            security.Require(principal, McpPermissionDto.DebugRead, state.ProjectId);
        return state;
    }

    private static async Task<object> ReadVersionsAsync(
        McpSecurityService security,
        McpPrincipal? principal,
        Guid projectId,
        Guid flowId,
        string trackText,
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        var track = ParseTrack(trackText);
        var service = scope.ServiceProvider.GetRequiredService<AiReadModelService>();
        return await service.GetFlowVersionHistoryAsync(projectId, flowId, track, cancellationToken);
    }

    private static async Task<object?> ReadVersionAsync(
        McpSecurityService security,
        McpPrincipal? principal,
        Guid projectId,
        Guid flowId,
        string trackText,
        long version,
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        var track = ParseTrack(trackText);
        var detail = await scope.ServiceProvider.GetRequiredService<AiReadModelService>()
            .GetFlowVersionAsync(projectId, flowId, version, cancellationToken);
        if (detail is null || detail.Version.Track != track)
            return null;
        return detail with { Definition = FlowDiffService.RedactSensitive(detail.Definition) };
    }

    private static async Task<object> ReadPreviewAsync(
        IServiceScope scope,
        McpSecurityService security,
        McpPrincipal? principal,
        Guid previewId,
        CancellationToken cancellationToken)
    {
        security.Require(principal, McpPermissionDto.ProjectRead);
        var entry = await scope.ServiceProvider.GetRequiredService<IMcpPreviewStore>().FindAsync(previewId, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The MCP preview was not found.");
        if (principal is not null
            && !principal.IsLocal
            && !principal.IsAdministrator
            && !string.Equals(entry.PrincipalId, principal.Id, StringComparison.Ordinal))
        {
            throw new McpSecurityException(
                "mcp.preview_owner_mismatch",
                "The MCP preview belongs to another caller.",
                403);
        }
        if (entry.ProjectId is null && principal is not null && !principal.IsLocal && !principal.IsAdministrator)
            throw new McpSecurityException("mcp.preview_access_denied", "The MCP caller cannot access this global preview.", 403);
        if (entry.ProjectId is not null && principal is not null && !principal.CanAccess(entry.ProjectId.Value))
            throw new McpSecurityException("mcp.project_access_denied", "The MCP caller cannot access this preview.", 403);
        RequirePreviewPermission(security, principal, entry);
        var descriptor = new McpPreviewDescriptorDto(entry.Id, entry.Operation, entry.ProjectId, entry.FlowId, entry.Status, entry.ExpiresAt, entry.PreviewFingerprint);
        return entry.Operation switch
        {
            "project.create" => ReadProjectCreatePreview(entry, descriptor),
            "flow.patch" => ReadFlowPatchPreview(entry, descriptor),
            "flow.publish" => ReadPublishPreview(entry, descriptor),
            "flow.rollback" => ReadRollbackPreview(entry, descriptor),
            "library.package" => ReadLibraryPackagePreview(entry, descriptor),
            "project.library.attach" => ReadProjectLibraryAttachPreview(entry, descriptor),
            _ => descriptor,
        };
    }

    private static FlowPatchPreviewDto ReadFlowPatchPreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredFlowPatchPreview>(entry);
        return new FlowPatchPreviewDto(
            descriptor.PreviewId,
            stored.Request.ProjectId,
            stored.Request.FlowId,
            stored.Request.ExpectedDevelopmentVersion,
            descriptor.ExpiresAt,
            descriptor.PreviewFingerprint,
            IsPreviewPending(entry)
                && stored.Validation.IsValid
                && stored.Diff.Changes.Count > 0,
            stored.Validation,
            FlowDiffService.RedactSensitive(stored.Diff),
            FlowDiffService.RedactSensitive(stored.CandidateDefinition));
    }

    private static ProjectCreatePreviewDto ReadProjectCreatePreview(
        McpPreviewEntry entry,
        McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredProjectCreatePreview>(entry);
        var project = RehydrateProject(stored);
        var candidate = new ProjectCreationCandidate(project, stored.Definition, stored.Validation);
        return ToProjectCreatePreview(descriptor, candidate);
    }

    private static async Task<object> PreviewCreateProjectAsync(
        IServiceScope scope,
        McpPrincipal principal,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        security.RequireAdministrator(principal);
        var request = Deserialize<CreateProjectMcpRequestDto>(arguments);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new McpProtocolException(-32602, "MCP parameter 'name' is required.");

        ProjectCreationCandidate candidate;
        try
        {
            candidate = scope.ServiceProvider.GetRequiredService<ProjectCreationService>()
                .PrepareEmpty(request.Name, request.FlowName);
        }
        catch (ArgumentException exception)
        {
            throw new McpProtocolException(-32602, exception.Message);
        }

        var stored = new StoredProjectCreatePreview(
            new CreateProjectMcpRequestDto(candidate.Project.Name, GetFlowName(candidate.Definition)),
            candidate.Project.Id,
            candidate.Definition.Id,
            candidate.Project.CreatedAt,
            candidate.Definition,
            candidate.Validation);
        var preview = await scope.ServiceProvider.GetRequiredService<McpPreviewService>().CreateAsync(
            "project.create",
            principal,
            candidate.Project.Id,
            candidate.Definition.Id,
            expectedVersion: null,
            stored,
            cancellationToken);
        return ToProjectCreatePreview(
            new McpPreviewDescriptorDto(
                preview.Id,
                preview.Operation,
                preview.ProjectId,
                preview.FlowId,
                preview.Status,
                preview.ExpiresAt,
                preview.PreviewFingerprint),
            candidate);
    }

    private static async Task<object> ApplyCreateProjectAsync(
        IServiceScope scope,
        McpPrincipal principal,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var request = Deserialize<McpMutationApplyRequestDto>(arguments);
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(
            principal.Id,
            "project.create",
            request.IdempotencyKey,
            requestPayload,
            cancellationToken);
        if (replay is not null)
            return DeserializeStoredResponse(replay.ResponseJson);

        var previews = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await previews.RequireAsync(
            request.PreviewId,
            request.PreviewFingerprint,
            principal,
            cancellationToken);
        if (!string.Equals(entry.Operation, "project.create", StringComparison.Ordinal))
            throw new McpProtocolException(-32602, "The preview does not describe project creation.");

        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        security.RequireAdministrator(principal);
        security.Require(principal, McpPermissionDto.ProjectWrite);
        var stored = McpPreviewService.Deserialize<StoredProjectCreatePreview>(entry);
        var project = RehydrateProject(stored);
        var candidate = scope.ServiceProvider.GetRequiredService<ProjectCreationService>()
            .Prepare(project, stored.Definition);
        if (!candidate.Validation.IsValid)
        {
            throw new McpProtocolException(
                -32011,
                "The project creation preview is no longer valid.",
                new { code = "mcp.validation_failed", diagnostics = candidate.Validation.Diagnostics });
        }

        var result = await scope.ServiceProvider.GetRequiredService<ProjectCreationService>()
            .CreateAsync(candidate, cancellationToken);
        if (result.Status == ProjectCreationStatus.Conflict)
        {
            throw new McpProtocolException(
                -32010,
                result.ErrorMessage ?? "The project already exists.",
                new { code = result.ErrorCode });
        }
        if (result.Status != ProjectCreationStatus.Created)
            throw new McpProtocolException(-32011, "The project creation preview cannot be applied.");

        var response = CreateWorkspace(candidate);
        await previews.MarkAppliedAsync(entry, cancellationToken);
        await idempotency.SaveAsync(
            principal.Id,
            entry.Operation,
            request.IdempotencyKey,
            response,
            requestPayload,
            cancellationToken);
        return response;
    }

    private static Project RehydrateProject(StoredProjectCreatePreview stored)
        => Project.Rehydrate(
            stored.ProjectId,
            stored.Request.Name,
            version: 1,
            SereinFlow.Domain.ProjectStatus.Draft,
            stored.CreatedAt,
            stored.CreatedAt);

    private static ProjectCreatePreviewDto ToProjectCreatePreview(
        McpPreviewDescriptorDto descriptor,
        ProjectCreationCandidate candidate)
        => new(
            descriptor.PreviewId,
            candidate.Project.Id,
            candidate.Definition.Id,
            candidate.Project.Name,
            GetFlowName(candidate.Definition),
            descriptor.ExpiresAt,
            descriptor.PreviewFingerprint,
            descriptor.Status == McpMutationPreviewStatusDto.Pending
                && descriptor.ExpiresAt > DateTimeOffset.UtcNow
                && candidate.Validation.IsValid,
            candidate.Validation,
            CreateWorkspace(candidate));

    private static ProjectWorkspaceDto CreateWorkspace(ProjectCreationCandidate candidate)
        => new(
            new ProjectDto(
                candidate.Project.Id,
                candidate.Project.Name,
                candidate.Project.Version,
                ToProjectStatusValue(candidate.Project.Status),
                candidate.Project.CreatedAt,
                candidate.Project.UpdatedAt),
            [new FlowDefinitionSummaryDto(
                candidate.Definition.Id,
                candidate.Definition.Version,
                candidate.Definition.EntryNodeId,
                candidate.Definition.Canvases.Count,
                candidate.Definition.Canvases.Sum(static canvas => canvas.Nodes.Count))]);

    private static string GetFlowName(FlowDefinitionDto definition)
        => definition.Canvases.FirstOrDefault()?.Name ?? "Main";

    private static string ToProjectStatusValue(SereinFlow.Domain.ProjectStatus status)
        => status switch
        {
            SereinFlow.Domain.ProjectStatus.Draft => "draft",
            SereinFlow.Domain.ProjectStatus.Ready => "ready",
            SereinFlow.Domain.ProjectStatus.ScriptInvalid => "scriptInvalid",
            SereinFlow.Domain.ProjectStatus.Archived => "archived",
            _ => status.ToString()
        };

    private static object ReadPublishPreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredPublishPreview>(entry);
        return new
        {
            previewId = descriptor.PreviewId,
            descriptor.ExpiresAt,
            descriptor.PreviewFingerprint,
            expectedProductionVersion = stored.ExpectedProductionVersion,
            canApply = IsPreviewPending(entry)
                && stored.Validation.IsValid
                && (!stored.HasProductionVersion || stored.Diff.Changes.Count > 0),
            validation = stored.Validation,
            diff = FlowDiffService.RedactSensitive(stored.Diff),
        };
    }

    private static object ReadRollbackPreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredRollbackPreview>(entry);
        return new
        {
            previewId = descriptor.PreviewId,
            descriptor.ExpiresAt,
            descriptor.PreviewFingerprint,
            canApply = IsPreviewPending(entry)
                && stored.Validation.IsValid
                && stored.Diff.Changes.Count > 0,
            validation = stored.Validation,
            diff = FlowDiffService.RedactSensitive(stored.Diff),
        };
    }

    private static LibraryPackagePreviewDto ReadLibraryPackagePreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredLibraryPackagePreview>(entry);
        return new LibraryPackagePreviewDto(
            descriptor.PreviewId,
            stored.FileName,
            stored.SizeBytes,
            stored.PackageSha256,
            descriptor.ExpiresAt,
            descriptor.PreviewFingerprint,
            IsPreviewPending(entry),
            stored.Inspection.Library,
            [],
            stored.Inspection.AlreadyExists,
            stored.Inspection.Library.DllSha256,
            stored.Compatibility,
            stored.ProjectImpact);
    }

    private static ProjectLibraryAttachPreviewDto ReadProjectLibraryAttachPreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredProjectLibraryAttachPreview>(entry);
        return new ProjectLibraryAttachPreviewDto(
            descriptor.PreviewId,
            stored.Request.ProjectId,
            stored.Request.LibraryId,
            descriptor.ExpiresAt,
            descriptor.PreviewFingerprint,
            IsPreviewPending(entry) && stored.Diagnostics.Count == 0,
            stored.Diagnostics);
    }

    private static async Task<object> PreviewFlowPatchAsync(
        IServiceScope scope,
        McpPrincipal principal,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var request = Deserialize<FlowPatchRequestDto>(arguments);
        if (request.ProjectId == Guid.Empty || request.FlowId == Guid.Empty || request.ExpectedDevelopmentVersion < 1)
            throw new McpProtocolException(-32602, "The flow patch request is invalid.");
        await RequireActiveProjectAsync(
            scope,
            scope.ServiceProvider.GetRequiredService<McpSecurityService>(),
            principal,
            request.ProjectId,
            McpPermissionDto.FlowWrite,
            cancellationToken);
        var flows = scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        var current = await flows.FindAsync(request.ProjectId, request.FlowId, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The flow definition was not found.");
        if (current.Version != request.ExpectedDevelopmentVersion)
            throw VersionConflict(current.Version);

        FlowDefinitionDto candidate;
        try
        {
            candidate = scope.ServiceProvider.GetRequiredService<FlowPatchService>().Apply(current, request.Operations);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or JsonException)
        {
            throw new McpProtocolException(-32602, $"The flow patch is invalid: {exception.Message}");
        }

        var preparation = await scope.ServiceProvider.GetRequiredService<FlowDefinitionWriteService>()
            .PrepareAsync(request.ProjectId, request.FlowId, candidate, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The flow definition was not found.");
        var stored = new StoredFlowPatchPreview(
            request,
            preparation.Candidate,
            preparation.Validation,
            scope.ServiceProvider.GetRequiredService<FlowDiffService>().Compare(preparation.Current, preparation.Candidate));
        var preview = await scope.ServiceProvider.GetRequiredService<McpPreviewService>().CreateAsync(
            "flow.patch",
            principal,
            request.ProjectId,
            request.FlowId,
            request.ExpectedDevelopmentVersion,
            stored,
            cancellationToken);
        return new FlowPatchPreviewDto(
            preview.Id,
            request.ProjectId,
            request.FlowId,
            request.ExpectedDevelopmentVersion,
            preview.ExpiresAt,
            preview.PreviewFingerprint,
            IsPreviewPending(preview) && stored.Validation.IsValid && stored.Diff.Changes.Count > 0,
            stored.Validation,
            FlowDiffService.RedactSensitive(stored.Diff),
            FlowDiffService.RedactSensitive(stored.CandidateDefinition));
    }

    private static async Task<object> ApplyFlowPatchAsync(
        IServiceScope scope,
        McpPrincipal principal,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var request = Deserialize<McpMutationApplyRequestDto>(arguments);
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, "flow.patch", request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
            return DeserializeStoredResponse(replay.ResponseJson);
        var previews = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await previews.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, "flow.patch", StringComparison.Ordinal))
            throw new McpProtocolException(-32602, "The preview does not describe a flow patch.");
        var stored = McpPreviewService.Deserialize<StoredFlowPatchPreview>(entry);
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        await RequireActiveProjectAsync(
            scope,
            security,
            principal,
            stored.Request.ProjectId,
            McpPermissionDto.FlowWrite,
            cancellationToken);
        if (!stored.Validation.IsValid || stored.Diff.Changes.Count == 0)
            throw new McpProtocolException(-32011, "The flow patch preview cannot be applied.");
        var flows = scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        var current = await flows.FindAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The flow definition was not found.");
        if (current.Version != stored.Request.ExpectedDevelopmentVersion)
            throw VersionConflict(current.Version);
        var currentDiff = scope.ServiceProvider.GetRequiredService<FlowDiffService>().Compare(current, stored.CandidateDefinition);
        if (!string.Equals(currentDiff.BaseChecksum, stored.Diff.BaseChecksum, StringComparison.Ordinal))
            throw VersionConflict(current.Version);
        var write = await scope.ServiceProvider.GetRequiredService<FlowDefinitionWriteService>().WriteAsync(
            stored.Request.ProjectId,
            stored.Request.FlowId,
            stored.CandidateDefinition,
            stored.Request.ExpectedDevelopmentVersion,
            cancellationToken);
        if (write.Status == FlowDefinitionWriteStatus.Conflict)
            throw VersionConflict(write.CurrentVersion);
        if (write.Status == FlowDefinitionWriteStatus.Archived)
            throw new McpProtocolException(-32011, "Archived projects cannot save flow definitions.");
        if (write.Status != FlowDefinitionWriteStatus.Saved || write.Saved is null)
            throw new McpProtocolException(-32011, "The flow patch preview cannot be applied.");
        var saved = write.Saved;
        await previews.MarkAppliedAsync(entry, cancellationToken);
        var response = FlowDiffService.RedactSensitive(saved);
        await idempotency.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, response, requestPayload, cancellationToken);
        return response;
    }

    private static async Task<object> CompareFlowVersionsAsync(
        IServiceScope scope,
        McpPrincipal principal,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var projectId = GetGuid(arguments, "projectId");
        var flowId = GetGuid(arguments, "flowId");
        var track = ParseTrack(GetRequiredString(arguments, "track"));
        var fromVersion = GetLong(arguments, "fromVersion");
        var toVersion = GetLong(arguments, "toVersion");
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        security.Require(principal, McpPermissionDto.ProjectRead, projectId);
        var service = scope.ServiceProvider.GetRequiredService<AiReadModelService>();
        var fromVersionDetail = await service.GetFlowVersionAsync(projectId, flowId, fromVersion, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The source flow version was not found.");
        var toVersionDetail = await service.GetFlowVersionAsync(projectId, flowId, toVersion, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The target flow version was not found.");
        if (fromVersionDetail.Version.Track != track || toVersionDetail.Version.Track != track)
            throw new McpProtocolException(-32004, "The requested versions are not on the selected track.");
        var diffService = scope.ServiceProvider.GetRequiredService<FlowDiffService>();
        return new FlowVersionComparisonDto(
            fromVersionDetail with { Definition = FlowDiffService.RedactSensitive(fromVersionDetail.Definition) },
            toVersionDetail with { Definition = FlowDiffService.RedactSensitive(toVersionDetail.Definition) },
            FlowDiffService.RedactSensitive(diffService.Compare(fromVersionDetail.Definition, toVersionDetail.Definition)));
    }

    private static async Task<object> PreviewPublishAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<PublishFlowPreviewRequestDto>(arguments);
        await RequireActiveProjectAsync(
            scope,
            scope.ServiceProvider.GetRequiredService<McpSecurityService>(),
            principal,
            request.ProjectId,
            McpPermissionDto.FlowPublish,
            cancellationToken);
        var flow = await scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>().FindAsync(request.ProjectId, request.FlowId, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The flow definition was not found.");
        if (flow.Version != request.ExpectedDevelopmentVersion)
            throw VersionConflict(flow.Version);
        var executableValidation = await ValidateExecutableFlowAsync(scope, request.ProjectId, flow, cancellationToken);
        var productionVersions = scope.ServiceProvider.GetRequiredService<IFlowVersionRepository>();
        var expectedProductionVersion = await productionVersions.FindProductionVersionAsync(request.ProjectId, request.FlowId, cancellationToken);
        var diagnostics = executableValidation.Diagnostics.ToArray();
        var production = await productionVersions.FindProductionDefinitionAsync(request.ProjectId, request.FlowId, cancellationToken);
        var diff = scope.ServiceProvider.GetRequiredService<FlowDiffService>().Compare(production ?? flow, flow);
        if (production is not null && diff.Changes.Count == 0)
        {
            diagnostics = diagnostics.Append(new ValidationDiagnosticDto(
                "flow.publish_no_change",
                "The current development version is already the production definition. 当前开发版本已经是生产定义。",
                null)).ToArray();
        }
        var result = new StoredPublishPreview(
            request,
            new FlowValidationResultDto(diagnostics.Length == 0, diagnostics),
            diff,
            HasProductionVersion: production is not null,
            ExpectedProductionVersion: expectedProductionVersion);
        var entry = await scope.ServiceProvider.GetRequiredService<McpPreviewService>().CreateAsync("flow.publish", principal, request.ProjectId, request.FlowId, request.ExpectedDevelopmentVersion, result, cancellationToken);
        return new
        {
            previewId = entry.Id,
            entry.ExpiresAt,
            entry.PreviewFingerprint,
            expectedProductionVersion,
            canApply = result.Validation.IsValid && (production is null || diff.Changes.Count > 0),
            validation = result.Validation,
            diff = FlowDiffService.RedactSensitive(diff),
        };
    }

    private static async Task<object> ApplyPublishAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<McpMutationApplyRequestDto>(arguments);
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, "flow.publish", request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
            return DeserializeStoredResponse(replay.ResponseJson);
        var preview = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await preview.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        var stored = McpPreviewService.Deserialize<StoredPublishPreview>(entry);
        await RequireActiveProjectAsync(
            scope,
            scope.ServiceProvider.GetRequiredService<McpSecurityService>(),
            principal,
            stored.Request.ProjectId,
            McpPermissionDto.FlowPublish,
            cancellationToken);
        var versions = scope.ServiceProvider.GetRequiredService<IFlowVersionRepository>();
        var current = await scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>().FindAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The flow definition was not found.");
        if (current.Version != stored.Request.ExpectedDevelopmentVersion)
            throw VersionConflict(current.Version);
        var currentValidation = await ValidateExecutableFlowAsync(scope, stored.Request.ProjectId, current, cancellationToken);
        RequireValidMutation(currentValidation, "The current development flow cannot be published.");
        var currentProductionVersion = await versions.FindProductionVersionAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken);
        if (currentProductionVersion != stored.ExpectedProductionVersion)
            throw VersionConflict(currentProductionVersion);
        var currentProduction = await versions.FindProductionDefinitionAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken);
        if (currentProduction is not null
            && string.Equals(FlowDiffService.GetChecksum(currentProduction), FlowDiffService.GetChecksum(current), StringComparison.Ordinal))
            throw new McpProtocolException(-32011, "The development definition is already published.");
        var result = await versions.PublishAsync(
            stored.Request.ProjectId,
            stored.Request.FlowId,
            stored.Request.ExpectedDevelopmentVersion,
            stored.Request.Remark,
            expectedProductionVersion: stored.ExpectedProductionVersion,
            cancellationToken: cancellationToken);
        if (!result.IsCommitted)
            throw VersionConflict(result.CurrentHeadVersion);
        await preview.MarkAppliedAsync(entry, cancellationToken);
        var response = new FlowVersionMutationDto(
            result.Version!,
            FlowDiffService.RedactSensitive(stored.Diff),
            result.DevelopmentDefinition is null ? null : FlowDiffService.RedactSensitive(result.DevelopmentDefinition));
        await idempotency.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, response, requestPayload, cancellationToken);
        return response;
    }

    private static async Task<object> PreviewRollbackAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<RollbackFlowPreviewRequestDto>(arguments);
        if (request.SourceVersion < 1 || request.ExpectedHeadVersion < 1 || !Enum.IsDefined(request.Track))
            throw new McpProtocolException(-32602, "The rollback request is invalid.");
        await RequireActiveProjectAsync(
            scope,
            scope.ServiceProvider.GetRequiredService<McpSecurityService>(),
            principal,
            request.ProjectId,
            McpPermissionDto.FlowRollback,
            cancellationToken);
        var versions = scope.ServiceProvider.GetRequiredService<IFlowVersionRepository>();
        var source = await versions.FindVersionAsync(request.ProjectId, request.FlowId, request.SourceVersion, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The rollback source version was not found.");
        if (source.Version.Track != request.Track)
            throw new McpProtocolException(-32004, "The rollback source version is not on the selected track.");
        var head = (await versions.ListVersionsAsync(request.ProjectId, request.FlowId, request.Track, cancellationToken)).FirstOrDefault(item => item.IsCurrent);
        if (head is null || head.Version != request.ExpectedHeadVersion)
            throw VersionConflict(head?.Version);
        var current = await versions.FindVersionAsync(request.ProjectId, request.FlowId, head.Version, cancellationToken) ?? source;
        var validation = FlowDefinitionContractValidator.ValidateForExecution(source.Definition);
        var libraryValidation = await scope.ServiceProvider.GetRequiredService<ProjectLibraryService>().ValidateFlowLibrariesAsync(request.ProjectId, source.Definition, cancellationToken);
        var diagnostics = validation.Diagnostics.Concat(libraryValidation.Diagnostics).ToArray();
        var diff = scope.ServiceProvider.GetRequiredService<FlowDiffService>().Compare(current.Definition, source.Definition);
        if (diff.Changes.Count == 0)
            diagnostics = diagnostics.Concat([new ValidationDiagnosticDto(
                "flow.rollback_no_change",
                "The selected version is already the current track head. 选定版本已经是当前轨道头版本。",
                null)]).ToArray();
        var stored = new StoredRollbackPreview(request, new FlowValidationResultDto(diagnostics.Length == 0, diagnostics), diff);
        var entry = await scope.ServiceProvider.GetRequiredService<McpPreviewService>().CreateAsync("flow.rollback", principal, request.ProjectId, request.FlowId, request.ExpectedHeadVersion, stored, cancellationToken);
        return new
        {
            previewId = entry.Id,
            entry.ExpiresAt,
            entry.PreviewFingerprint,
            canApply = stored.Validation.IsValid,
            validation = stored.Validation,
            diff = FlowDiffService.RedactSensitive(diff),
        };
    }

    private static async Task<object> ApplyRollbackAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<McpMutationApplyRequestDto>(arguments);
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, "flow.rollback", request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
            return DeserializeStoredResponse(replay.ResponseJson);
        var preview = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await preview.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        var stored = McpPreviewService.Deserialize<StoredRollbackPreview>(entry);
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        await RequireActiveProjectAsync(
            scope,
            security,
            principal,
            stored.Request.ProjectId,
            McpPermissionDto.FlowRollback,
            cancellationToken);
        var currentHead = stored.Request.Track == FlowVersionTrackDto.Development
            ? (await scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>().FindAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken))?.Version
            : await scope.ServiceProvider.GetRequiredService<IFlowVersionRepository>().FindProductionVersionAsync(stored.Request.ProjectId, stored.Request.FlowId, cancellationToken);
        if (currentHead != stored.Request.ExpectedHeadVersion)
            throw VersionConflict(currentHead);
        var source = await scope.ServiceProvider.GetRequiredService<IFlowVersionRepository>().FindVersionAsync(
            stored.Request.ProjectId,
            stored.Request.FlowId,
            stored.Request.SourceVersion,
            cancellationToken)
            ?? throw new McpProtocolException(-32004, "The rollback source version was not found.");
        if (source.Version.Track != stored.Request.Track)
            throw new McpProtocolException(-32004, "The rollback source version is not on the selected track.");
        var currentValidation = await ValidateExecutableFlowAsync(
            scope,
            stored.Request.ProjectId,
            source.Definition,
            cancellationToken);
        RequireValidMutation(currentValidation, "The rollback source flow cannot be executed.");
        var result = await scope.ServiceProvider.GetRequiredService<IFlowVersionRepository>().RollbackAsync(
            stored.Request.ProjectId, stored.Request.FlowId, stored.Request.SourceVersion, stored.Request.Track, stored.Request.ExpectedHeadVersion, cancellationToken);
        if (!result.IsCommitted)
            throw VersionConflict(result.CurrentHeadVersion);
        await preview.MarkAppliedAsync(entry, cancellationToken);
        var response = new FlowVersionMutationDto(
            result.Version!,
            FlowDiffService.RedactSensitive(stored.Diff),
            result.DevelopmentDefinition is null ? null : FlowDiffService.RedactSensitive(result.DevelopmentDefinition));
        await idempotency.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, response, requestPayload, cancellationToken);
        return response;
    }

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
            "sereinflow_list_projects" or "sereinflow_get_project" or "sereinflow_get_flow_topology" or "sereinflow_get_flow_edit_model" or "sereinflow_compare_flow_versions" => McpPermissionDto.ProjectRead,
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

    private static async Task<object> CompileScriptAsync(
        IServiceScope scope,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var request = Deserialize<ScriptCompileRequestDto>(arguments);
        if (string.IsNullOrWhiteSpace(request.Source))
            throw new McpProtocolException(-32602, "The SereinLang source is required.");
        return await scope.ServiceProvider
            .GetRequiredService<SereinFlow.ScriptAdapter.ISereinLangCompiler>()
            .CompileAsync(request, cancellationToken);
    }

    private static async Task<object> PreviewLibraryPackageAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var fileName = GetRequiredString(arguments, "fileName");
        var packageBase64 = GetRequiredString(arguments, "packageBase64");
        if (packageBase64.Length > 140 * 1024 * 1024)
            throw new McpProtocolException(-32012, "The library package request is too large.");
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(packageBase64);
        }
        catch (FormatException exception)
        {
            throw new McpProtocolException(-32602, "The library package must be valid base64.", exception.Message);
        }
        var projectId = TryGetGuid(arguments, "projectId");
        var familyId = GetOptionalString(arguments, "familyId");
        var baselineArtifactId = GetOptionalString(arguments, "baselineArtifactId");
        if (!string.IsNullOrWhiteSpace(familyId) && !string.IsNullOrWhiteSpace(baselineArtifactId))
            throw new McpProtocolException(-32602, "Specify either familyId or baselineArtifactId, not both.");
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        if (projectId is not null)
            await RequireActiveProjectAsync(
                scope,
                security,
                principal,
                projectId.Value,
                McpPermissionDto.LibraryImport,
                cancellationToken);
        else if (!principal.IsLocal && !principal.IsAdministrator)
            security.RequireAdministrator(principal);
        if (projectId is not null)
        {
            await EnsurePackageBaselineAccessAsync(
                scope,
                principal,
                projectId.Value,
                familyId,
                baselineArtifactId,
                cancellationToken);
        }

        var staging = scope.ServiceProvider.GetRequiredService<McpPackageStagingService>();
        McpStagedPackage? staged = null;
        try
        {
            staged = await staging.StageAsync(new MemoryStream(bytes, writable: false), cancellationToken);
            await using var package = staging.OpenRead(staged.Path);
            var inspection = await scope.ServiceProvider.GetRequiredService<ILibraryCatalogService>().InspectAsync(
                package,
                fileName,
                staged.SizeBytes,
                familyId,
                baselineArtifactId,
                cancellationToken)
                ?? throw new McpProtocolException(-32020, "Library package inspection is not configured in this host.");
            var compatibility = inspection.Compatibility;
            var projectImpact = projectId is null || compatibility is null
                ? null
                : await BuildLibraryPackageProjectImpactAsync(
                    scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>(),
                    projectId.Value,
                    compatibility.BaselineArtifactId,
                    cancellationToken);
            var stored = new StoredLibraryPackagePreview(
                fileName,
                projectId,
                staged.Path,
                staged.SizeBytes,
                staged.Sha256,
                inspection,
                compatibility,
                projectImpact);
            var entry = await scope.ServiceProvider.GetRequiredService<McpPreviewService>().CreateAsync(
                "library.package", principal, projectId, null, null, stored, cancellationToken);
            staged = null;
            return new LibraryPackagePreviewDto(
                entry.Id,
                fileName,
                stored.SizeBytes,
                inspection.Library.Sha256,
                entry.ExpiresAt,
                entry.PreviewFingerprint,
                true,
                inspection.Library,
                [],
                inspection.AlreadyExists,
                inspection.Library.DllSha256,
                compatibility,
                projectImpact);
        }
        catch (LibraryUploadException exception)
        {
            throw new McpProtocolException(
                -32011,
                exception.Message,
                new { code = "mcp.library_package_invalid", statusCode = exception.StatusCode });
        }
        catch (InvalidOperationException exception)
        {
            throw new McpProtocolException(
                -32012,
                exception.Message,
                new { code = "mcp.library_package_invalid", statusCode = 413 });
        }
        finally
        {
            if (staged is not null)
                staging.Delete(staged.Path);
        }
    }

    private static async Task<object> ApplyLibraryPackageAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = Deserialize<McpMutationApplyRequestDto>(arguments);
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, "library.package", request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
            return DeserializeStoredResponse(replay.ResponseJson);
        var preview = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await preview.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, "library.package", StringComparison.Ordinal))
            throw new McpProtocolException(-32602, "The preview does not describe a library package.");
        var stored = McpPreviewService.Deserialize<StoredLibraryPackagePreview>(entry);
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        if (stored.ProjectId is null)
            security.RequireAdministrator(principal);
        else
            await RequireActiveProjectAsync(
                scope,
                security,
                principal,
                stored.ProjectId.Value,
                McpPermissionDto.LibraryImport,
                cancellationToken);
        var staging = scope.ServiceProvider.GetRequiredService<McpPackageStagingService>();
        try
        {
            await using var package = staging.OpenRead(stored.StagingPath);
            if (package.Length != stored.SizeBytes || !string.Equals(await ComputeSha256Async(package, cancellationToken), stored.PackageSha256, StringComparison.OrdinalIgnoreCase))
                throw new McpProtocolException(-32010, "The staged library package changed after preview.");
            package.Position = 0;
            var result = await scope.ServiceProvider.GetRequiredService<ILibraryCatalogService>().UploadAsync(
                package, stored.FileName, stored.SizeBytes, cancellationToken);
            await preview.MarkAppliedAsync(entry, cancellationToken);
            await idempotency.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, result, requestPayload, cancellationToken);
            return result;
        }
        catch (FileNotFoundException)
        {
            throw new McpProtocolException(
                -32004,
                "The staged library package is no longer available. Create a new preview and apply it again.",
                new { code = "mcp.preview_package_unavailable" });
        }
        finally
        {
            staging.Delete(stored.StagingPath);
        }
    }

    private static async Task<LibraryPackageProjectImpactDto?> BuildLibraryPackageProjectImpactAsync(
        IFlowDefinitionRepository flows,
        Guid projectId,
        string baselineArtifactId,
        CancellationToken cancellationToken)
    {
        var affectedFlowIds = new List<Guid>();
        var affectedNodeCount = 0;
        foreach (var flow in await flows.ListByProjectAsync(projectId, cancellationToken))
        {
            var nodeCount = flow.Canvases
                .SelectMany(static canvas => canvas.Nodes)
                .Count(node => node.Type is NodeTypeDto.Action or NodeTypeDto.Flipflop
                    && string.Equals(node.Ui?.LibraryId, baselineArtifactId, StringComparison.OrdinalIgnoreCase));
            if (nodeCount == 0)
                continue;
            affectedFlowIds.Add(flow.Id);
            affectedNodeCount += nodeCount;
        }

        return new LibraryPackageProjectImpactDto(
            projectId,
            baselineArtifactId,
            affectedFlowIds.Count,
            affectedNodeCount,
            affectedFlowIds.OrderBy(static id => id).ToArray());
    }

    private static async Task EnsurePackageBaselineAccessAsync(
        IServiceScope scope,
        McpPrincipal principal,
        Guid projectId,
        string? familyId,
        string? baselineArtifactId,
        CancellationToken cancellationToken)
    {
        if (principal.IsLocal || principal.IsAdministrator
            || (string.IsNullOrWhiteSpace(familyId) && string.IsNullOrWhiteSpace(baselineArtifactId)))
        {
            return;
        }

        var artifactId = baselineArtifactId?.Trim();
        if (string.IsNullOrWhiteSpace(artifactId) && !string.IsNullOrWhiteSpace(familyId))
        {
            var family = (await scope.ServiceProvider.GetRequiredService<ILibraryCatalogService>()
                    .ListFamiliesAsync(includeArchivedArtifacts: true, cancellationToken: cancellationToken))
                .SingleOrDefault(item => string.Equals(item.Id, familyId.Trim(), StringComparison.OrdinalIgnoreCase));
            artifactId = family?.LatestArtifactId;
        }

        if (string.IsNullOrWhiteSpace(artifactId)
            || !await scope.ServiceProvider.GetRequiredService<IProjectLibraryReferenceRepository>()
                .IsReferencedAsync(projectId, artifactId, cancellationToken))
        {
            throw new McpSecurityException(
                "mcp.library_access_denied",
                "The MCP caller cannot use the requested library baseline for this project.",
                403);
        }
    }

    private static async Task<object> PreviewProjectLibraryAttachAsync(
        IServiceScope scope,
        McpPrincipal principal,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var request = Deserialize<ProjectLibraryAttachRequestDto>(arguments);
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        security.Require(principal, McpPermissionDto.LibraryManage, request.ProjectId);
        var libraryId = request.LibraryId?.Trim() ?? string.Empty;
        request = request with { LibraryId = libraryId };
        var validation = await ValidateProjectLibraryAttachAsync(scope, request, cancellationToken);
        var stored = new StoredProjectLibraryAttachPreview(new(request.ProjectId, libraryId), validation);
        var entry = await scope.ServiceProvider.GetRequiredService<McpPreviewService>().CreateAsync(
            "project.library.attach", principal, request.ProjectId, null, null, stored, cancellationToken);
        return new ProjectLibraryAttachPreviewDto(
            entry.Id,
            request.ProjectId,
            libraryId,
            entry.ExpiresAt,
            entry.PreviewFingerprint,
            validation.Count == 0,
            validation);
    }

    private static async Task<object> ApplyProjectLibraryAttachAsync(
        IServiceScope scope,
        McpPrincipal principal,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var request = Deserialize<McpMutationApplyRequestDto>(arguments);
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, "project.library.attach", request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
            return DeserializeStoredResponse(replay.ResponseJson);
        var preview = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await preview.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, "project.library.attach", StringComparison.Ordinal))
            throw new McpProtocolException(-32602, "The preview does not describe a project library attachment.");
        var stored = McpPreviewService.Deserialize<StoredProjectLibraryAttachPreview>(entry);
        scope.ServiceProvider.GetRequiredService<McpSecurityService>()
            .Require(principal, McpPermissionDto.LibraryManage, stored.Request.ProjectId);
        var currentDiagnostics = await ValidateProjectLibraryAttachAsync(scope, stored.Request, cancellationToken);
        if (currentDiagnostics.Count > 0)
        {
            throw new McpProtocolException(
                -32011,
                "The project library attachment is no longer valid. Create a new preview and review it again.",
                new { code = "mcp.validation_failed", diagnostics = currentDiagnostics });
        }
        var result = await scope.ServiceProvider.GetRequiredService<ProjectLibraryService>().AddAsync(
            stored.Request.ProjectId, stored.Request.LibraryId, cancellationToken);
        if (!result.IsSuccess)
            throw new McpProtocolException(-32011, result.Message ?? "The project library attachment failed.", result.Code);
        await preview.MarkAppliedAsync(entry, cancellationToken);
        await idempotency.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, result, requestPayload, cancellationToken);
        return result;
    }

    private static async Task<IReadOnlyList<ValidationDiagnosticDto>> ValidateProjectLibraryAttachAsync(
        IServiceScope scope,
        ProjectLibraryAttachRequestDto request,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<ValidationDiagnosticDto>();
        var project = await scope.ServiceProvider.GetRequiredService<IProjectRepository>()
            .FindAsync(request.ProjectId, cancellationToken);
        if (project is null)
            diagnostics.Add(new("project.not_found", "The project was not found. 未找到项目。", "projectId"));
        else if (project.Status == SereinFlow.Domain.ProjectStatus.Archived)
            diagnostics.Add(new("project.archived", "Archived projects cannot change library references. 已归档项目不能修改类库引用。", "projectId"));

        var libraryId = request.LibraryId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(libraryId))
            diagnostics.Add(new("library.id_required", "The library ID is required. 类库 ID 不能为空。", "libraryId"));
        var library = string.IsNullOrWhiteSpace(libraryId)
            ? null
            : await scope.ServiceProvider.GetRequiredService<ILibraryCatalogService>()
                .FindAsync(libraryId, cancellationToken);
        if (library is null)
            diagnostics.Add(new("library.not_found", "The library artifact was not found. 未找到类库制品。", "libraryId"));
        else if (library.Lifecycle != LibraryLifecycleDto.Available)
            diagnostics.Add(new("library.archived", "Archived library artifacts cannot be attached. 已归档类库制品不能被接入。", "libraryId"));

        if (library is not null
            && await scope.ServiceProvider.GetRequiredService<IProjectLibraryReferenceRepository>()
                .IsReferencedAsync(request.ProjectId, library.Id, cancellationToken))
        {
            diagnostics.Add(new("project_library.already_referenced", "The project already references this library artifact. 项目已引用该类库制品。", "libraryId"));
        }

        return diagnostics;
    }

    private static async Task<object> ListApiKeysAsync(IServiceScope scope, McpPrincipal principal, CancellationToken cancellationToken)
    {
        scope.ServiceProvider.GetRequiredService<McpSecurityService>().RequireAdministrator(principal);
        return (await scope.ServiceProvider.GetRequiredService<IMcpApiKeyStore>().ListAsync(cancellationToken))
            .Select(McpSecurityService.ToDto)
            .ToArray();
    }

    private static async Task<object> CreateApiKeyAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        scope.ServiceProvider.GetRequiredService<McpSecurityService>().RequireAdministrator(principal);
        var request = new CreateMcpApiKeyRequestDto(
            TryGetGuid(arguments, "projectId"),
            GetRequiredString(arguments, "name"),
            ReadPermissions(arguments),
            ReadOptionalDate(arguments, "expiresAt"),
            GetOptionalBool(arguments, "isAdministrator") ?? false);
        if (request.ProjectId is not null)
        {
            var project = await scope.ServiceProvider.GetRequiredService<IProjectRepository>()
                .FindAsync(request.ProjectId.Value, cancellationToken);
            if (project is null)
                throw new McpProtocolException(-32004, "The API key project was not found.");
            if (project.Status == SereinFlow.Domain.ProjectStatus.Archived)
                throw new McpProtocolException(-32011, "An API key cannot be bound to an archived project.");
        }
        var idempotencyKey = GetRequiredString(arguments, "idempotencyKey");
        var requestPayload = Serialize(new { request, idempotencyKey });
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, "mcp.key.create", idempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            var key = JsonSerializer.Deserialize<McpApiKeyDto>(replay.ResponseJson, ContractJsonOptions)
                ?? throw new McpProtocolException(-32603, "The stored API key idempotency response is invalid.");
            return new { key, secret = (string?)null, replayed = true };
        }
        var created = McpSecurityService.CreateKeyWithEntry(request);
        await scope.ServiceProvider.GetRequiredService<IMcpApiKeyStore>().AddAsync(created.Entry, cancellationToken);
        await idempotency.SaveAsync(principal.Id, "mcp.key.create", idempotencyKey, created.Dto.Key, requestPayload, cancellationToken);
        return created.Dto;
    }

    private static async Task<object> RevokeApiKeyAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        scope.ServiceProvider.GetRequiredService<McpSecurityService>().RequireAdministrator(principal);
        var keyId = GetRequiredString(arguments, "keyId");
        var idempotencyKey = GetRequiredString(arguments, "idempotencyKey");
        var requestPayload = Serialize(new { keyId, idempotencyKey });
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, "mcp.key.revoke", idempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
            return DeserializeStoredResponse(replay.ResponseJson);
        var store = scope.ServiceProvider.GetRequiredService<IMcpApiKeyStore>();
        var entry = await store.FindAsync(keyId, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The MCP API key was not found.");
        var revoked = entry with { RevokedAt = DateTimeOffset.UtcNow };
        await store.UpdateAsync(revoked, cancellationToken);
        var response = McpSecurityService.ToDto(revoked);
        await idempotency.SaveAsync(principal.Id, "mcp.key.revoke", idempotencyKey, response, requestPayload, cancellationToken);
        return response;
    }

    private static async Task<object> RotateApiKeyAsync(IServiceScope scope, McpPrincipal principal, JsonElement arguments, CancellationToken cancellationToken)
    {
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        security.RequireAdministrator(principal);
        var keyId = GetRequiredString(arguments, "keyId");
        var idempotencyKey = GetRequiredString(arguments, "idempotencyKey");
        var requestPayload = Serialize(new { keyId, idempotencyKey });
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, "mcp.key.rotate", idempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            var replayed = JsonSerializer.Deserialize<RotatedMcpApiKeyDto>(replay.ResponseJson, ContractJsonOptions)
                ?? throw new McpProtocolException(-32603, "The stored API key rotation response is invalid.");
            return replayed with { Secret = null, Replayed = true };
        }

        var store = scope.ServiceProvider.GetRequiredService<IMcpApiKeyStore>();
        var existing = await store.FindAsync(keyId, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The MCP API key was not found.");
        if (existing.RevokedAt is not null)
            throw new McpProtocolException(-32011, "The MCP API key has already been revoked.");

        var replacement = McpSecurityService.RotateKeyWithEntry(existing);
        await store.AddAsync(replacement.Entry, cancellationToken);
        await store.UpdateAsync(existing with { RevokedAt = DateTimeOffset.UtcNow }, cancellationToken);
        var response = new RotatedMcpApiKeyDto(existing.Id, replacement.Dto.Key, replacement.Dto.Secret);
        await idempotency.SaveAsync(
            principal.Id,
            "mcp.key.rotate",
            idempotencyKey,
            response with { Secret = null },
            requestPayload,
            cancellationToken);
        return response;
    }

    private static Dictionary<string, object?> ApplySchemaProperties()
        => new(StringComparer.Ordinal)
        {
            ["previewId"] = StringSchema(),
            ["previewFingerprint"] = StringSchema(),
            ["confirmation"] = StringSchema(),
            ["idempotencyKey"] = StringSchema()
        };

    private static object ArraySchema()
        => new { type = "array", items = new { } };

    private static McpPermissionDto[] ReadPermissions(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("permissions", out var value) || value.ValueKind != JsonValueKind.Array)
            throw new McpProtocolException(-32602, "MCP parameter 'permissions' must be an array.");
        var permissions = new List<McpPermissionDto>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || !McpPermissionNames.TryParse(item.GetString(), out var permission))
                throw new McpProtocolException(-32602, "MCP parameter 'permissions' contains an invalid permission.");
            permissions.Add(permission);
        }
        return permissions.Distinct().ToArray();
    }

    private static DateTimeOffset? ReadOptionalDate(JsonElement arguments, string name)
    {
        var value = GetOptionalString(arguments, name);
        return string.IsNullOrWhiteSpace(value)
            ? null
            : DateTimeOffset.TryParse(value, out var parsed)
                ? parsed
                : throw new McpProtocolException(-32602, $"MCP parameter '{name}' must be an ISO date.");
    }

    private static T Deserialize<T>(JsonElement arguments)
        => arguments.Deserialize<T>(ContractJsonOptions)
            ?? throw new McpProtocolException(-32602, "MCP arguments are invalid.");

    private static Guid? TryGetGuid(JsonElement arguments, string name)
    {
        var value = GetOptionalString(arguments, name);
        return string.IsNullOrWhiteSpace(value)
            ? null
            : Guid.TryParse(value, out var parsed)
                ? parsed
                : throw new McpProtocolException(-32602, $"MCP parameter '{name}' must be a GUID.");
    }

    private static long GetLong(JsonElement arguments, string name)
        => GetOptionalLong(arguments, name)
            ?? throw new McpProtocolException(-32602, $"MCP parameter '{name}' is required.");

    private static FlowVersionTrackDto ParseTrack(string value)
        => Enum.TryParse<FlowVersionTrackDto>(value, true, out var track) && Enum.IsDefined(track)
            ? track
            : throw new McpProtocolException(-32602, "The flow version track must be development or production.");

    private static void RequireConfirmation(McpMutationApplyRequestDto request)
    {
        if (!string.Equals(request.Confirmation, "APPLY", StringComparison.Ordinal))
            throw new McpProtocolException(-32602, "The confirmation value must be APPLY.");
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new McpProtocolException(-32602, "The idempotency key is required.");
    }

    private static McpProtocolException VersionConflict(long? currentVersion)
        => new(-32010, "The flow version changed before the MCP mutation was applied.", new { currentVersion });

    private static async Task<FlowValidationResultDto> ValidateExecutableFlowAsync(
        IServiceScope scope,
        Guid projectId,
        FlowDefinitionDto definition,
        CancellationToken cancellationToken)
    {
        var validation = FlowDefinitionContractValidator.ValidateForExecution(definition);
        var libraryValidation = await scope.ServiceProvider.GetRequiredService<ProjectLibraryService>()
            .ValidateFlowLibrariesAsync(projectId, definition, cancellationToken);
        return new FlowValidationResultDto(
            validation.IsValid && libraryValidation.IsValid,
            validation.Diagnostics.Concat(libraryValidation.Diagnostics).ToArray());
    }

    private static void RequireValidMutation(FlowValidationResultDto validation, string message)
    {
        if (validation.IsValid)
            return;

        throw new McpProtocolException(
            -32011,
            message,
            new { code = "mcp.validation_failed", diagnostics = validation.Diagnostics });
    }

    private static async Task<SereinFlow.Domain.Project> RequireActiveProjectAsync(
        IServiceScope scope,
        McpSecurityService security,
        McpPrincipal principal,
        Guid projectId,
        McpPermissionDto permission,
        CancellationToken cancellationToken)
    {
        security.Require(principal, permission, projectId);
        var project = await scope.ServiceProvider.GetRequiredService<IProjectRepository>()
            .FindAsync(projectId, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The project was not found.");
        if (project.Status == SereinFlow.Domain.ProjectStatus.Archived)
        {
            throw new McpProtocolException(
                -32011,
                "Archived projects cannot be changed by this MCP operation.",
                new { code = "project.archived" });
        }
        return project;
    }

    private static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, ContractJsonOptions);

    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken)
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
                break;
            hash.AppendData(buffer, 0, read);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static JsonElement DeserializeStoredResponse(string responseJson)
        => JsonSerializer.Deserialize<JsonElement>(responseJson, ContractJsonOptions);

    private static bool IsProjectScoped(McpPrincipal? principal)
        => principal?.ProjectId is not null && !principal.IsLocal && !principal.IsAdministrator;

    private static bool IsPreviewPending(McpPreviewEntry entry)
        => entry.Status == McpMutationPreviewStatusDto.Pending
            && entry.ExpiresAt > DateTimeOffset.UtcNow;

    private static Guid? TryGetGuidSilently(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String)
            return null;
        return Guid.TryParse(value.GetString(), out var parsed) ? parsed : null;
    }

    private static string? TryGetStringSilently(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String)
            return null;
        return value.GetString();
    }

    private static FlowVersionTrackDto? GetAuditTrack(string operation, JsonElement arguments)
    {
        var value = GetOptionalString(arguments, "track");
        if (!string.IsNullOrWhiteSpace(value)
            && Enum.TryParse<FlowVersionTrackDto>(value, true, out var track)
            && Enum.IsDefined(track))
        {
            return track;
        }

        return operation is "sereinflow_preview_flow_patch"
            or "sereinflow_apply_flow_patch"
            or "sereinflow_preview_publish_flow"
            or "sereinflow_apply_publish_flow"
            ? FlowVersionTrackDto.Development
            : null;
    }

    private static FlowVersionTrackDto? GetPreviewAuditTrack(McpPreviewEntry entry)
    {
        if (string.Equals(entry.Operation, "flow.patch", StringComparison.Ordinal)
            || string.Equals(entry.Operation, "flow.publish", StringComparison.Ordinal))
        {
            return FlowVersionTrackDto.Development;
        }

        if (!string.Equals(entry.Operation, "flow.rollback", StringComparison.Ordinal))
            return null;

        try
        {
            using var document = JsonDocument.Parse(entry.PayloadJson);
            if (document.RootElement.TryGetProperty("request", out var request)
                && request.TryGetProperty("track", out var track)
                && track.ValueKind == JsonValueKind.String
                && Enum.TryParse<FlowVersionTrackDto>(track.GetString(), true, out var parsed)
                && Enum.IsDefined(parsed))
            {
                return parsed;
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static long? GetAuditFlowVersion(JsonElement arguments)
        => GetOptionalLong(arguments, "expectedDevelopmentVersion")
            ?? GetOptionalLong(arguments, "expectedHeadVersion")
            ?? GetOptionalLong(arguments, "sourceVersion")
            ?? GetOptionalLong(arguments, "version");

    private sealed record StoredProjectCreatePreview(
        CreateProjectMcpRequestDto Request,
        Guid ProjectId,
        Guid FlowId,
        DateTimeOffset CreatedAt,
        FlowDefinitionDto Definition,
        FlowValidationResultDto Validation);
    private sealed record StoredFlowPatchPreview(FlowPatchRequestDto Request, FlowDefinitionDto CandidateDefinition, FlowValidationResultDto Validation, FlowDiffDto Diff);
    private sealed record StoredPublishPreview(
        PublishFlowPreviewRequestDto Request,
        FlowValidationResultDto Validation,
        FlowDiffDto Diff,
        bool HasProductionVersion = true,
        long? ExpectedProductionVersion = null);
    private sealed record StoredRollbackPreview(RollbackFlowPreviewRequestDto Request, FlowValidationResultDto Validation, FlowDiffDto Diff);
    private sealed record StoredLibraryPackagePreview(
        string FileName,
        Guid? ProjectId,
        string StagingPath,
        long SizeBytes,
        string PackageSha256,
        LibraryPackageInspectionDto Inspection,
        LibraryArtifactCompatibilityDto? Compatibility = null,
        LibraryPackageProjectImpactDto? ProjectImpact = null);
    private sealed record StoredProjectLibraryAttachPreview(
        ProjectLibraryAttachRequestDto Request,
        IReadOnlyList<ValidationDiagnosticDto> Diagnostics);

    private sealed class SemaphoreLease(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                semaphore.Release();
        }
    }

    private static McpToolDescriptor Tool(string name, string description, JsonElement inputSchema)
        => new(name, description, inputSchema);

    private static JsonElement Schema(
        Dictionary<string, object?>? properties = null,
        string[]? required = null)
        => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = properties ?? new Dictionary<string, object?>(),
            required = required ?? []
        });

    private static object StringSchema(string? description = null)
        => description is null ? new { type = "string" } : new { type = "string", description };

    private static object NumberSchema()
        => new { type = "integer" };

    private static object BooleanSchema()
        => new { type = "boolean" };

    private static AiReadModelOptions ReadOptions(JsonElement arguments)
        => new(
            GetOptionalInt(arguments, "maxItems") ?? 200,
            GetOptionalInt(arguments, "maxJsonBytes") ?? 64 * 1024,
            GetOptionalBool(arguments, "includeFlowLiteralValues") ?? false,
            GetOptionalBool(arguments, "includeScriptSource") ?? false);

    private static FlowVersionTrackDto ReadTrack(JsonElement arguments)
    {
        var value = GetOptionalString(arguments, "track");
        return string.IsNullOrWhiteSpace(value)
            ? FlowVersionTrackDto.Development
            : Enum.TryParse<FlowVersionTrackDto>(value, true, out var track) && Enum.IsDefined(track)
                ? track
                : throw new McpProtocolException(-32602, "The flow version track must be development or production.");
    }

    private static FlowVersionTrackDto? ReadOptionalTrack(JsonElement arguments)
    {
        var value = GetOptionalString(arguments, "track");
        return string.IsNullOrWhiteSpace(value)
            ? null
            : Enum.TryParse<FlowVersionTrackDto>(value, true, out var track) && Enum.IsDefined(track)
                ? track
                : throw new McpProtocolException(-32602, "The flow version track must be development or production.");
    }

    private static Guid GetGuid(JsonElement arguments, string name)
    {
        var value = GetRequiredString(arguments, name);
        return Guid.TryParse(value, out var parsed)
            ? parsed
            : throw new McpProtocolException(-32602, $"MCP parameter '{name}' must be a GUID.");
    }

    private static string GetRequiredString(JsonElement arguments, string name)
    {
        var value = GetOptionalString(arguments, name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new McpProtocolException(-32602, $"MCP parameter '{name}' is required.")
            : value.Trim();
    }

    private static string? GetOptionalString(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind != JsonValueKind.String)
            throw new McpProtocolException(-32602, $"MCP parameter '{name}' must be a string.");
        return value.GetString();
    }

    private static int? GetOptionalInt(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var parsed))
            throw new McpProtocolException(-32602, $"MCP parameter '{name}' must be an integer.");
        return parsed;
    }

    private static long? GetOptionalLong(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var parsed))
            throw new McpProtocolException(-32602, $"MCP parameter '{name}' must be an integer.");
        return parsed;
    }

    private static bool? GetOptionalBool(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new McpProtocolException(-32602, $"MCP parameter '{name}' must be a boolean.");
        return value.GetBoolean();
    }
}

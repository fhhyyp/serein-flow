using System.Text.Json;
using SereinFlow.Contracts;

namespace SereinFlow.Application;

/// <summary>
/// Converts the public flow-patch wire formats into one strongly typed v2
/// model before a patch reaches the definition service. Version 1 is retained
/// only to read existing callers and persisted preview payloads.
/// </summary>
public sealed class FlowPatchContractNormalizer
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateContractOptions();

    public NormalizedFlowPatchRequest Normalize(JsonElement arguments)
    {
        RequireObject(arguments, "$", "MCP tool arguments must be a JSON object.");
        var isV2 = ReadSchemaVersion(arguments, out var schemaVersion, out var warnings);
        if (isV2)
        {
            EnsureKnownProperties(
                arguments,
                "$",
                ["projectId", "flowId", "expectedDevelopmentVersion", "schemaVersion", "operations", "remark"]);
        }

        var projectId = ReadGuid(arguments, "projectId", "$.projectId");
        var flowId = ReadGuid(arguments, "flowId", "$.flowId");
        var expectedVersion = ReadPositiveLong(arguments, "expectedDevelopmentVersion", "$.expectedDevelopmentVersion");
        var remark = ReadOptionalString(arguments, "remark", "$.remark");
        var operationElement = RequireProperty(arguments, "operations", "$.operations");
        if (operationElement.ValueKind != JsonValueKind.Array)
            throw Invalid("mcp.flow_patch.operations_invalid", "$.operations", "an array", "Provide a typed operations array.");

        if (isV2 && operationElement.GetArrayLength() == 0)
        {
            throw Invalid(
                "mcp.flow_patch.operation_required",
                "$.operations",
                "at least one typed operation",
                "Add an operation or omit the preview request.");
        }

        var operations = new List<FlowPatchCanonicalOperationDto>(operationElement.GetArrayLength());
        var index = 0;
        foreach (var operation in operationElement.EnumerateArray())
        {
            var path = $"$.operations[{index}]";
            operations.Add(isV2 ? ReadV2Operation(operation, path) : ReadV1Operation(operation, path));
            index++;
        }

        var canonical = new FlowPatchCanonicalRequestDto(
            projectId,
            flowId,
            expectedVersion,
            schemaVersion,
            operations,
            remark);
        return new NormalizedFlowPatchRequest(
            canonical,
            ToLegacyRequest(canonical),
            warnings);
    }

    public NormalizedFlowPatchRequest NormalizeLegacyRequest(FlowPatchRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var operations = request.Operations
            .Select((operation, index) => ReadPersistedLegacyOperation(operation, $"$.operations[{index}]"))
            .ToArray();
        var canonical = new FlowPatchCanonicalRequestDto(
            request.ProjectId,
            request.FlowId,
            request.ExpectedDevelopmentVersion,
            FlowPatchContract.CurrentSchemaVersion,
            operations,
            request.Remark);
        return new NormalizedFlowPatchRequest(
            canonical,
            request,
            [new FlowPatchNormalizationWarningDto(
                "mcp.flow_patch.legacy_input",
                "$.operations",
                "The stored preview uses the legacy flow-patch representation and was normalized to schema 2.0.")]);
    }

    public IReadOnlyList<FlowPatchCanonicalOperationDto> NormalizeLegacyOperations(
        IReadOnlyList<FlowPatchOperationDto> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        return operations
            .Select((operation, index) => ReadPersistedLegacyOperation(operation, $"$.operations[{index}]"))
            .ToArray();
    }

    /// <summary>
    /// Validates identifiers and endpoint references against the current flow
    /// without invoking the legacy mutation implementation. The MCP boundary
    /// calls this after wire normalization and before FlowPatchService so
    /// duplicate IDs and unresolved references receive contract diagnostics.
    /// </summary>
    public void ValidateReferences(
        FlowDefinitionDto definition,
        IReadOnlyList<FlowPatchCanonicalOperationDto> operations)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(operations);

        var state = PatchReferenceState.FromDefinition(definition);
        for (var index = 0; index < operations.Count; index++)
            state.Apply(operations[index], $"$.operations[{index}]");
    }

    public static FlowPatchRequestDto ToLegacyRequest(FlowPatchCanonicalRequestDto request)
        => new(
            request.ProjectId,
            request.FlowId,
            request.ExpectedDevelopmentVersion,
            request.Operations.Select(ToLegacyOperation).ToArray(),
            request.Remark);

    public static FlowPatchOperationDto ToLegacyOperation(FlowPatchCanonicalOperationDto operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return operation.Op switch
        {
            "addCanvas" => Legacy(FlowPatchOperationKindDto.AddCanvas, value: operation.Canvas),
            "updateCanvas" => Legacy(FlowPatchOperationKindDto.UpdateCanvas, operation.CanvasId, value: operation.Canvas),
            "removeCanvas" => Legacy(FlowPatchOperationKindDto.RemoveCanvas, operation.CanvasId),
            "addNode" => Legacy(FlowPatchOperationKindDto.AddNode, operation.CanvasId, value: operation.Node),
            "replaceNode" => Legacy(FlowPatchOperationKindDto.ReplaceNode, operation.CanvasId, operation.NodeId, value: operation.Node),
            "removeNode" => Legacy(FlowPatchOperationKindDto.RemoveNode, operation.CanvasId, operation.NodeId),
            "setNodeParameter" => Legacy(FlowPatchOperationKindDto.SetNodeParameter, operation.CanvasId, operation.NodeId, parameterId: operation.ParameterId, value: operation.Parameter),
            "addNodeParameter" => Legacy(FlowPatchOperationKindDto.AddNodeParameter, operation.CanvasId, operation.NodeId, value: operation.Parameter),
            "removeNodeParameter" => Legacy(FlowPatchOperationKindDto.RemoveNodeParameter, operation.CanvasId, operation.NodeId, parameterId: operation.ParameterId),
            "addConnection" => Legacy(FlowPatchOperationKindDto.AddConnection, operation.CanvasId, value: operation.Connection),
            "replaceConnection" => Legacy(FlowPatchOperationKindDto.ReplaceConnection, operation.CanvasId, connectionId: operation.ConnectionId, value: operation.Connection),
            "removeConnection" => Legacy(FlowPatchOperationKindDto.RemoveConnection, operation.CanvasId, connectionId: operation.ConnectionId),
            "setEntryNode" => Legacy(FlowPatchOperationKindDto.SetEntryNode, value: operation.EntryNodeId),
            "setRunPolicy" => Legacy(FlowPatchOperationKindDto.SetRunPolicy, value: operation.RunPolicy),
            "replaceScriptSource" => Legacy(FlowPatchOperationKindDto.ReplaceScriptSource, operation.CanvasId, operation.NodeId, value: operation.Source),
            _ => throw Invalid(
                "mcp.flow_patch.operation_unknown",
                "$.operations",
                "a supported schema 2.0 operation",
                "Use the operation names published by sereinflow_preview_flow_patch.")
        };
    }

    private static FlowPatchOperationDto Legacy(
        FlowPatchOperationKindDto operation,
        string? canvasId = null,
        string? nodeId = null,
        string? connectionId = null,
        string? parameterId = null,
        object? value = null)
        => new(
            operation,
            canvasId,
            nodeId,
            connectionId,
            parameterId,
            value is null ? null : JsonSerializer.SerializeToElement(value, JsonOptions));

    private static bool ReadSchemaVersion(
        JsonElement arguments,
        out string schemaVersion,
        out IReadOnlyList<FlowPatchNormalizationWarningDto> warnings)
    {
        if (!arguments.TryGetProperty("schemaVersion", out var value))
        {
            schemaVersion = FlowPatchContract.CurrentSchemaVersion;
            warnings =
            [
                new FlowPatchNormalizationWarningDto(
                    "mcp.flow_patch.legacy_input",
                    "$.schemaVersion",
                    "Schema version 1.0 was inferred because schemaVersion was omitted.")
            ];
            return false;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw Invalid(
                "mcp.flow_patch.schema_version_invalid",
                "$.schemaVersion",
                $"'{FlowPatchContract.LegacySchemaVersion}' or '{FlowPatchContract.CurrentSchemaVersion}'",
                $"Use schemaVersion '{FlowPatchContract.CurrentSchemaVersion}' for new requests.");
        }

        var supplied = value.GetString();
        if (string.Equals(supplied, FlowPatchContract.CurrentSchemaVersion, StringComparison.Ordinal))
        {
            schemaVersion = FlowPatchContract.CurrentSchemaVersion;
            warnings = [];
            return true;
        }

        if (string.Equals(supplied, FlowPatchContract.LegacySchemaVersion, StringComparison.Ordinal))
        {
            schemaVersion = FlowPatchContract.CurrentSchemaVersion;
            warnings =
            [
                new FlowPatchNormalizationWarningDto(
                    "mcp.flow_patch.legacy_input",
                    "$.schemaVersion",
                    "Schema version 1.0 is deprecated; the preview response uses schema 2.0.")
            ];
            return false;
        }

        throw Invalid(
            "mcp.flow_patch.schema_version_unsupported",
            "$.schemaVersion",
            $"'{FlowPatchContract.LegacySchemaVersion}' or '{FlowPatchContract.CurrentSchemaVersion}'",
            $"Upgrade the request to schemaVersion '{FlowPatchContract.CurrentSchemaVersion}'.");
    }

    private static FlowPatchCanonicalOperationDto ReadV2Operation(JsonElement operation, string path)
    {
        RequireObject(operation, path, "Each operation must be an object.");
        var op = ReadRequiredString(operation, "op", $"{path}.op");
        return op switch
        {
            "addCanvas" => new(op, Canvas: ReadCanvas(operation, "canvas", path, legacyEnums: false, ["op", "canvas"])),
            "updateCanvas" => new(op, ReadRequiredString(operation, "canvasId", $"{path}.canvasId"), Canvas: ReadCanvas(operation, "canvas", path, legacyEnums: false, ["op", "canvasId", "canvas"])),
            "removeCanvas" => ReadV2RemoveCanvas(operation, path, op),
            "addNode" => new(op, ReadRequiredString(operation, "canvasId", $"{path}.canvasId"), Node: ReadNode(operation, "node", path, legacyEnums: false, ["op", "canvasId", "node"])),
            "replaceNode" => new(op, ReadRequiredString(operation, "canvasId", $"{path}.canvasId"), ReadRequiredString(operation, "nodeId", $"{path}.nodeId"), Node: ReadNode(operation, "node", path, legacyEnums: false, ["op", "canvasId", "nodeId", "node"])),
            "removeNode" => ReadV2RemoveNode(operation, path, op),
            "setNodeParameter" => new(op, ReadRequiredString(operation, "canvasId", $"{path}.canvasId"), ReadRequiredString(operation, "nodeId", $"{path}.nodeId"), ParameterId: ReadRequiredString(operation, "parameterId", $"{path}.parameterId"), Parameter: ReadParameter(operation, "parameter", path, legacyEnums: false, ["op", "canvasId", "nodeId", "parameterId", "parameter"])),
            "addNodeParameter" => new(op, ReadRequiredString(operation, "canvasId", $"{path}.canvasId"), ReadRequiredString(operation, "nodeId", $"{path}.nodeId"), Parameter: ReadParameter(operation, "parameter", path, legacyEnums: false, ["op", "canvasId", "nodeId", "parameter"])),
            "removeNodeParameter" => ReadV2RemoveNodeParameter(operation, path, op),
            "addConnection" => new(op, ReadRequiredString(operation, "canvasId", $"{path}.canvasId"), Connection: ReadConnection(operation, "connection", path, legacyEnums: false, ["op", "canvasId", "connection"])),
            "replaceConnection" => new(op, ReadRequiredString(operation, "canvasId", $"{path}.canvasId"), ConnectionId: ReadRequiredString(operation, "connectionId", $"{path}.connectionId"), Connection: ReadConnection(operation, "connection", path, legacyEnums: false, ["op", "canvasId", "connectionId", "connection"])),
            "removeConnection" => ReadV2RemoveConnection(operation, path, op),
            "setEntryNode" => ReadV2SetEntryNode(operation, path, op),
            "setRunPolicy" => new(op, RunPolicy: ReadRunPolicy(operation, "runPolicy", path, legacyEnums: false, ["op", "runPolicy"])),
            "replaceScriptSource" => ReadV2ReplaceScriptSource(operation, path, op),
            _ => throw Invalid(
                "mcp.flow_patch.operation_unknown",
                $"{path}.op",
                "one of the 15 schema 2.0 operation names",
                "Use the operation names exposed by sereinflow_preview_flow_patch.")
        };
    }

    private static FlowPatchCanonicalOperationDto ReadV2RemoveCanvas(JsonElement operation, string path, string op)
    {
        EnsureKnownProperties(operation, path, ["op", "canvasId"]);
        return new(op, ReadRequiredString(operation, "canvasId", $"{path}.canvasId"));
    }

    private static FlowPatchCanonicalOperationDto ReadV2RemoveNode(JsonElement operation, string path, string op)
    {
        EnsureKnownProperties(operation, path, ["op", "canvasId", "nodeId"]);
        return new(op, ReadRequiredString(operation, "canvasId", $"{path}.canvasId"), ReadRequiredString(operation, "nodeId", $"{path}.nodeId"));
    }

    private static FlowPatchCanonicalOperationDto ReadV2RemoveNodeParameter(JsonElement operation, string path, string op)
    {
        EnsureKnownProperties(operation, path, ["op", "canvasId", "nodeId", "parameterId"]);
        return new(
            op,
            ReadRequiredString(operation, "canvasId", $"{path}.canvasId"),
            ReadRequiredString(operation, "nodeId", $"{path}.nodeId"),
            ParameterId: ReadRequiredString(operation, "parameterId", $"{path}.parameterId"));
    }

    private static FlowPatchCanonicalOperationDto ReadV2RemoveConnection(JsonElement operation, string path, string op)
    {
        EnsureKnownProperties(operation, path, ["op", "canvasId", "connectionId"]);
        return new(op, ReadRequiredString(operation, "canvasId", $"{path}.canvasId"), ConnectionId: ReadRequiredString(operation, "connectionId", $"{path}.connectionId"));
    }

    private static FlowPatchCanonicalOperationDto ReadV2SetEntryNode(JsonElement operation, string path, string op)
    {
        EnsureKnownProperties(operation, path, ["op", "entryNodeId"]);
        return new(op, EntryNodeId: ReadRequiredString(operation, "entryNodeId", $"{path}.entryNodeId"));
    }

    private static FlowPatchCanonicalOperationDto ReadV2ReplaceScriptSource(JsonElement operation, string path, string op)
    {
        EnsureKnownProperties(operation, path, ["op", "canvasId", "nodeId", "source"]);
        return new(
            op,
            ReadRequiredString(operation, "canvasId", $"{path}.canvasId"),
            ReadRequiredString(operation, "nodeId", $"{path}.nodeId"),
            Source: ReadRequiredString(operation, "source", $"{path}.source"));
    }

    private static FlowPatchCanonicalOperationDto ReadV1Operation(JsonElement operation, string path)
    {
        RequireObject(operation, path, "Each operation must be an object.");
        var op = ReadLegacyOperationName(RequireProperty(operation, "operation", $"{path}.operation"), $"{path}.operation");
        return op switch
        {
            "addCanvas" => new(op, Canvas: ReadLegacyPayload<CanvasDto>(operation, path, legacyEnums: true)),
            "updateCanvas" => new(op, ReadLegacyIdentifier(operation, "canvasId", path), Canvas: ReadLegacyPayload<CanvasDto>(operation, path, legacyEnums: true)),
            "removeCanvas" => new(op, ReadLegacyIdentifier(operation, "canvasId", path)),
            "addNode" => new(op, ReadLegacyIdentifier(operation, "canvasId", path), Node: ReadLegacyPayload<NodeDto>(operation, path, legacyEnums: true)),
            "replaceNode" => new(op, ReadLegacyIdentifier(operation, "canvasId", path), ReadLegacyIdentifier(operation, "nodeId", path), Node: ReadLegacyPayload<NodeDto>(operation, path, legacyEnums: true)),
            "removeNode" => new(op, ReadLegacyIdentifier(operation, "canvasId", path), ReadLegacyIdentifier(operation, "nodeId", path)),
            "setNodeParameter" => new(op, ReadLegacyIdentifier(operation, "canvasId", path), ReadLegacyIdentifier(operation, "nodeId", path), ParameterId: ReadLegacyIdentifier(operation, "parameterId", path), Parameter: ReadLegacyPayload<NodeParameterDto>(operation, path, legacyEnums: true)),
            "addNodeParameter" => new(op, ReadLegacyIdentifier(operation, "canvasId", path), ReadLegacyIdentifier(operation, "nodeId", path), Parameter: ReadLegacyPayload<NodeParameterDto>(operation, path, legacyEnums: true)),
            "removeNodeParameter" => new(op, ReadLegacyIdentifier(operation, "canvasId", path), ReadLegacyIdentifier(operation, "nodeId", path), ParameterId: ReadLegacyIdentifier(operation, "parameterId", path)),
            "addConnection" => new(op, ReadLegacyIdentifier(operation, "canvasId", path), Connection: NormalizeConnection(ReadLegacyPayload<ConnectionDto>(operation, path, legacyEnums: true))),
            "replaceConnection" => new(op, ReadLegacyIdentifier(operation, "canvasId", path), ConnectionId: ReadLegacyIdentifier(operation, "connectionId", path), Connection: NormalizeConnection(ReadLegacyPayload<ConnectionDto>(operation, path, legacyEnums: true))),
            "removeConnection" => new(op, ReadLegacyIdentifier(operation, "canvasId", path), ConnectionId: ReadLegacyIdentifier(operation, "connectionId", path)),
            "setEntryNode" => new(op, EntryNodeId: ReadLegacyPayload<string>(operation, path, legacyEnums: true)),
            "setRunPolicy" => new(op, RunPolicy: ReadLegacyPayload<FlowRunPolicyDto>(operation, path, legacyEnums: true)),
            "replaceScriptSource" => new(op, ReadLegacyIdentifier(operation, "canvasId", path), ReadLegacyIdentifier(operation, "nodeId", path), Source: ReadLegacyPayload<string>(operation, path, legacyEnums: true)),
            _ => throw Invalid(
                "mcp.flow_patch.operation_unknown",
                $"{path}.operation",
                "a supported legacy operation name",
                "Use schemaVersion 2.0 and the canonical op names.")
        };
    }

    private static FlowPatchCanonicalOperationDto ReadPersistedLegacyOperation(FlowPatchOperationDto operation, string path)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var value = operation.Value;
        return operation.Operation switch
        {
            FlowPatchOperationKindDto.AddCanvas => new("addCanvas", Canvas: ReadPersistedPayload<CanvasDto>(value, path, legacyEnums: true)),
            FlowPatchOperationKindDto.UpdateCanvas => new("updateCanvas", Required(operation.CanvasId, $"{path}.canvasId"), Canvas: ReadPersistedPayload<CanvasDto>(value, path, legacyEnums: true)),
            FlowPatchOperationKindDto.RemoveCanvas => new("removeCanvas", Required(operation.CanvasId, $"{path}.canvasId")),
            FlowPatchOperationKindDto.AddNode => new("addNode", Required(operation.CanvasId, $"{path}.canvasId"), Node: ReadPersistedPayload<NodeDto>(value, path, legacyEnums: true)),
            FlowPatchOperationKindDto.ReplaceNode => new("replaceNode", Required(operation.CanvasId, $"{path}.canvasId"), Required(operation.NodeId, $"{path}.nodeId"), Node: ReadPersistedPayload<NodeDto>(value, path, legacyEnums: true)),
            FlowPatchOperationKindDto.RemoveNode => new("removeNode", Required(operation.CanvasId, $"{path}.canvasId"), Required(operation.NodeId, $"{path}.nodeId")),
            FlowPatchOperationKindDto.SetNodeParameter => new("setNodeParameter", Required(operation.CanvasId, $"{path}.canvasId"), Required(operation.NodeId, $"{path}.nodeId"), ParameterId: Required(operation.ParameterId, $"{path}.parameterId"), Parameter: ReadPersistedPayload<NodeParameterDto>(value, path, legacyEnums: true)),
            FlowPatchOperationKindDto.AddNodeParameter => new("addNodeParameter", Required(operation.CanvasId, $"{path}.canvasId"), Required(operation.NodeId, $"{path}.nodeId"), Parameter: ReadPersistedPayload<NodeParameterDto>(value, path, legacyEnums: true)),
            FlowPatchOperationKindDto.RemoveNodeParameter => new("removeNodeParameter", Required(operation.CanvasId, $"{path}.canvasId"), Required(operation.NodeId, $"{path}.nodeId"), ParameterId: Required(operation.ParameterId, $"{path}.parameterId")),
            FlowPatchOperationKindDto.AddConnection => new("addConnection", Required(operation.CanvasId, $"{path}.canvasId"), Connection: NormalizeConnection(ReadPersistedPayload<ConnectionDto>(value, path, legacyEnums: true))),
            FlowPatchOperationKindDto.ReplaceConnection => new("replaceConnection", Required(operation.CanvasId, $"{path}.canvasId"), ConnectionId: Required(operation.ConnectionId, $"{path}.connectionId"), Connection: NormalizeConnection(ReadPersistedPayload<ConnectionDto>(value, path, legacyEnums: true))),
            FlowPatchOperationKindDto.RemoveConnection => new("removeConnection", Required(operation.CanvasId, $"{path}.canvasId"), ConnectionId: Required(operation.ConnectionId, $"{path}.connectionId")),
            FlowPatchOperationKindDto.SetEntryNode => new("setEntryNode", EntryNodeId: ReadPersistedPayload<string>(value, path, legacyEnums: true)),
            FlowPatchOperationKindDto.SetRunPolicy => new("setRunPolicy", RunPolicy: ReadPersistedPayload<FlowRunPolicyDto>(value, path, legacyEnums: true)),
            FlowPatchOperationKindDto.ReplaceScriptSource => new("replaceScriptSource", Required(operation.CanvasId, $"{path}.canvasId"), Required(operation.NodeId, $"{path}.nodeId"), Source: ReadPersistedPayload<string>(value, path, legacyEnums: true)),
            _ => throw Invalid(
                "mcp.flow_patch.operation_unknown",
                path,
                "a supported legacy operation",
                "Create a new schema 2.0 preview.")
        };
    }

    private static T ReadLegacyPayload<T>(JsonElement operation, string path, bool legacyEnums)
        => ReadPayload<T>(RequireProperty(operation, "value", $"{path}.value"), $"{path}.value", legacyEnums);

    private static T ReadPersistedPayload<T>(JsonElement? value, string path, bool legacyEnums)
    {
        if (value is not { } payload)
            throw Invalid("mcp.flow_patch.field_required", $"{path}.value", "a value", "Provide the operation payload.");
        return ReadPayload<T>(payload, $"{path}.value", legacyEnums);
    }

    private static CanvasDto ReadCanvas(JsonElement operation, string name, string path, bool legacyEnums, string[] allowed)
    {
        EnsureKnownProperties(operation, path, allowed);
        return ReadPayload<CanvasDto>(RequireProperty(operation, name, $"{path}.{name}"), $"{path}.{name}", legacyEnums);
    }

    private static NodeDto ReadNode(JsonElement operation, string name, string path, bool legacyEnums, string[] allowed)
    {
        EnsureKnownProperties(operation, path, allowed);
        return ReadPayload<NodeDto>(RequireProperty(operation, name, $"{path}.{name}"), $"{path}.{name}", legacyEnums);
    }

    private static NodeParameterDto ReadParameter(JsonElement operation, string name, string path, bool legacyEnums, string[] allowed)
    {
        EnsureKnownProperties(operation, path, allowed);
        return ReadPayload<NodeParameterDto>(RequireProperty(operation, name, $"{path}.{name}"), $"{path}.{name}", legacyEnums);
    }

    private static ConnectionDto ReadConnection(JsonElement operation, string name, string path, bool legacyEnums, string[] allowed)
    {
        EnsureKnownProperties(operation, path, allowed);
        return NormalizeConnection(ReadPayload<ConnectionDto>(RequireProperty(operation, name, $"{path}.{name}"), $"{path}.{name}", legacyEnums));
    }

    private static ConnectionDto NormalizeConnection(ConnectionDto connection)
        => connection with
        {
            DataSource = connection.Kind == ConnectionKindDto.Data
                ? DataSourceDto.PreviousNode
                : null,
        };

    private static FlowRunPolicyDto ReadRunPolicy(JsonElement operation, string name, string path, bool legacyEnums, string[] allowed)
    {
        EnsureKnownProperties(operation, path, allowed);
        return ReadPayload<FlowRunPolicyDto>(RequireProperty(operation, name, $"{path}.{name}"), $"{path}.{name}", legacyEnums);
    }

    private static T ReadPayload<T>(JsonElement payload, string path, bool legacyEnums)
    {
        ValidatePayload<T>(payload, path, legacyEnums);
        try
        {
            return payload.Deserialize<T>(JsonOptions)
                ?? throw Invalid("mcp.flow_patch.payload_invalid", path, typeof(T).Name, "Provide a valid typed payload.");
        }
        catch (JsonException)
        {
            throw Invalid("mcp.flow_patch.payload_invalid", path, typeof(T).Name, "Provide a valid typed payload.");
        }
        catch (NotSupportedException)
        {
            throw Invalid("mcp.flow_patch.payload_invalid", path, typeof(T).Name, "Provide a supported typed payload.");
        }
    }

    private static void ValidatePayload<T>(JsonElement payload, string path, bool legacyEnums)
    {
        if (typeof(T) == typeof(CanvasDto))
            ValidateCanvasPayload(payload, path, legacyEnums);
        else if (typeof(T) == typeof(NodeDto))
            ValidateNodePayload(payload, path, legacyEnums);
        else if (typeof(T) == typeof(NodeParameterDto))
            ValidateParameterPayload(payload, path, legacyEnums);
        else if (typeof(T) == typeof(ConnectionDto))
            ValidateConnectionPayload(payload, path, legacyEnums);
        else if (typeof(T) == typeof(FlowRunPolicyDto))
            ValidateRunPolicyPayload(payload, path, legacyEnums);
    }

    private static void ValidateCanvasPayload(JsonElement payload, string path, bool legacyEnums)
    {
        RequireObject(payload, path, "Canvas payload must be an object.");
        if (!legacyEnums)
        {
            EnsureKnownProperties(payload, path, ["id", "lifecycle", "nodes", "connections", "name"]);
            _ = ReadRequiredString(payload, "id", $"{path}.id");
        }
        ValidateEnum(RequireProperty(payload, "lifecycle", $"{path}.lifecycle"), $"{path}.lifecycle", legacyEnums, CanvasLifecycles);
        var nodes = RequireProperty(payload, "nodes", $"{path}.nodes");
        var connections = RequireProperty(payload, "connections", $"{path}.connections");
        RequireArray(nodes, $"{path}.nodes");
        RequireArray(connections, $"{path}.connections");
        var index = 0;
        foreach (var node in nodes.EnumerateArray())
            ValidateNodePayload(node, $"{path}.nodes[{index++}]", legacyEnums);
        index = 0;
        foreach (var connection in connections.EnumerateArray())
            ValidateConnectionPayload(connection, $"{path}.connections[{index++}]", legacyEnums);
    }

    private static void ValidateNodePayload(JsonElement payload, string path, bool legacyEnums)
    {
        RequireObject(payload, path, "Node payload must be an object.");
        if (!legacyEnums)
        {
            EnsureKnownProperties(payload, path, ["id", "type", "displayName", "x", "y", "ports", "parameters", "script", "ui"]);
        }

        var nodeType = ValidateEnum(RequireProperty(payload, "type", $"{path}.type"), $"{path}.type", legacyEnums, NodeTypes).Canonical;
        string? nodeId = null;
        if (!legacyEnums)
        {
            nodeId = ReadRequiredString(payload, "id", $"{path}.id");
            _ = ReadRequiredString(payload, "displayName", $"{path}.displayName");
            RequireFiniteNumber(payload, "x", $"{path}.x");
            RequireFiniteNumber(payload, "y", $"{path}.y");
        }
        var ports = RequireProperty(payload, "ports", $"{path}.ports");
        var parameters = RequireProperty(payload, "parameters", $"{path}.parameters");
        RequireArray(ports, $"{path}.ports");
        RequireArray(parameters, $"{path}.parameters");
        var index = 0;
        var portIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var port in ports.EnumerateArray())
        {
            RequireObject(port, $"{path}.ports[{index}]", "Port payload must be an object.");
            if (!legacyEnums)
            {
                EnsureKnownProperties(port, $"{path}.ports[{index}]", ["id", "name", "direction", "required"]);
                var portId = ReadRequiredString(port, "id", $"{path}.ports[{index}].id");
                if (!portIds.Add(portId))
                    throw Invalid("mcp.flow_patch.duplicate_id", $"{path}.ports[{index}].id", "a unique port ID", "Use a unique port ID on the node.");
                _ = ReadRequiredString(port, "name", $"{path}.ports[{index}].name");
                _ = ReadRequiredString(port, "direction", $"{path}.ports[{index}].direction");
                RequireBoolean(port, "required", $"{path}.ports[{index}].required");
            }
            index++;
        }
        index = 0;
        var parameterIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in parameters.EnumerateArray())
        {
            ValidateParameterPayload(parameter, $"{path}.parameters[{index++}]", legacyEnums);
            if (!legacyEnums)
            {
                var parameterPath = $"{path}.parameters[{index - 1}].ui";
                var parameterUi = RequireProperty(parameter, "ui", parameterPath);
                if (parameterUi.ValueKind == JsonValueKind.Null)
                    throw Invalid("mcp.flow_patch.field_required", parameterPath, "parameter UI metadata", "Provide the parameter contract ID in ui.id.");
                var parameterId = ReadRequiredString(parameterUi, "id", $"{parameterPath}.id");
                if (!parameterIds.Add(parameterId))
                    throw Invalid("mcp.flow_patch.duplicate_id", $"{parameterPath}.id", "a unique parameter ID", "Use a unique parameter ID on the node.");
            }
        }

        var hasScript = payload.TryGetProperty("script", out var script) && script.ValueKind != JsonValueKind.Null;
        if (nodeType == "script")
        {
            if (!hasScript)
                throw Invalid("mcp.flow_patch.field_required", $"{path}.script", "a script payload for a script node", "Provide the script contract for a script node.");
            ValidateScriptPayload(script, $"{path}.script", legacyEnums);
            if (!legacyEnums && !string.Equals(nodeId, ReadRequiredString(script, "nodeId", $"{path}.script.nodeId"), StringComparison.Ordinal))
            {
                throw Invalid("mcp.flow_patch.reference_invalid", $"{path}.script.nodeId", $"the node ID '{nodeId}'", "Make script.nodeId match node.id.");
            }
        }
        else if (hasScript)
        {
            throw Invalid("mcp.flow_patch.unexpected_field", $"{path}.script", "null for action, flipflop, or flowCall nodes", "Remove the script payload or use a script node type.");
        }
        if (payload.TryGetProperty("ui", out var ui) && ui.ValueKind != JsonValueKind.Null)
            ValidateNodeUiPayload(ui, $"{path}.ui", legacyEnums, nodeType);
    }

    private static void ValidateParameterPayload(JsonElement payload, string path, bool legacyEnums)
    {
        RequireObject(payload, path, "Parameter payload must be an object.");
        if (!legacyEnums)
        {
            EnsureKnownProperties(payload, path, ["name", "valueJson", "source", "required", "ui"]);
            _ = ReadRequiredString(payload, "name", $"{path}.name");
            RequireNullableString(payload, "valueJson", $"{path}.valueJson");
            RequireBoolean(payload, "required", $"{path}.required");
        }
        ValidateEnum(RequireProperty(payload, "source", $"{path}.source"), $"{path}.source", legacyEnums, DataSources);
        if (payload.TryGetProperty("ui", out var ui) && ui.ValueKind != JsonValueKind.Null)
            ValidateParameterUiPayload(ui, $"{path}.ui", legacyEnums);
    }

    private static void ValidateConnectionPayload(JsonElement payload, string path, bool legacyEnums)
    {
        RequireObject(payload, path, "Connection payload must be an object.");
        if (!legacyEnums)
        {
            EnsureKnownProperties(payload, path, ["id", "fromNodeId", "fromPortId", "toNodeId", "toPortId", "kind", "branch", "dataSource", "priority"]);
            _ = ReadRequiredString(payload, "id", $"{path}.id");
            _ = ReadRequiredString(payload, "fromNodeId", $"{path}.fromNodeId");
            _ = ReadRequiredString(payload, "fromPortId", $"{path}.fromPortId");
            _ = ReadRequiredString(payload, "toNodeId", $"{path}.toNodeId");
            _ = ReadRequiredString(payload, "toPortId", $"{path}.toPortId");
            RequireInteger(payload, "priority", $"{path}.priority");
            RequireNullableEnum(payload, "branch", $"{path}.branch", legacyEnums, ExecutionBranches);
            RequireNullableEnum(payload, "dataSource", $"{path}.dataSource", legacyEnums, DataSources);
        }
        ValidateEnum(RequireProperty(payload, "kind", $"{path}.kind"), $"{path}.kind", legacyEnums, ConnectionKinds);
        if (payload.TryGetProperty("branch", out var branch) && branch.ValueKind != JsonValueKind.Null)
            ValidateEnum(branch, $"{path}.branch", legacyEnums, ExecutionBranches);
        if (payload.TryGetProperty("dataSource", out var source) && source.ValueKind != JsonValueKind.Null)
            ValidateEnum(source, $"{path}.dataSource", legacyEnums, DataSources);
    }

    private static void ValidateRunPolicyPayload(JsonElement payload, string path, bool legacyEnums)
    {
        RequireObject(payload, path, "Run policy payload must be an object.");
        if (!legacyEnums)
            EnsureKnownProperties(payload, path, ["concurrencyMode"]);
        ValidateEnum(RequireProperty(payload, "concurrencyMode", $"{path}.concurrencyMode"), $"{path}.concurrencyMode", legacyEnums, ConcurrencyModes);
    }

    private static void ValidateScriptPayload(JsonElement payload, string path, bool legacyEnums)
    {
        RequireObject(payload, path, "Script payload must be an object.");
        if (!legacyEnums)
        {
            EnsureKnownProperties(payload, path, ["nodeId", "source", "languageVersion", "sourceHash", "inputs", "outputs"]);
            _ = ReadRequiredString(payload, "nodeId", $"{path}.nodeId");
            _ = ReadRequiredString(payload, "source", $"{path}.source");
            _ = ReadRequiredString(payload, "languageVersion", $"{path}.languageVersion");
            _ = ReadRequiredString(payload, "sourceHash", $"{path}.sourceHash");
        }
        ValidateScriptValues(RequireProperty(payload, "inputs", $"{path}.inputs"), $"{path}.inputs", legacyEnums);
        ValidateScriptValues(RequireProperty(payload, "outputs", $"{path}.outputs"), $"{path}.outputs", legacyEnums);
    }

    private static void ValidateScriptValues(JsonElement values, string path, bool legacyEnums)
    {
        RequireArray(values, path);
        var index = 0;
        foreach (var value in values.EnumerateArray())
        {
            RequireObject(value, $"{path}[{index}]", "Script value must be an object.");
            if (!legacyEnums)
            {
                EnsureKnownProperties(value, $"{path}[{index}]", ["name", "valueKind", "required", "id", "description"]);
                _ = ReadRequiredString(value, "name", $"{path}[{index}].name");
                _ = ReadRequiredString(value, "valueKind", $"{path}[{index}].valueKind");
                RequireBoolean(value, "required", $"{path}[{index}].required");
            }
            index++;
        }
    }

    private static void ValidateNodeUiPayload(JsonElement payload, string path, bool legacyEnums, string nodeType)
    {
        RequireObject(payload, path, "Node UI payload must be an object.");
        if (!legacyEnums)
        {
            EnsureKnownProperties(payload, path,
            [
                "kind", "titleKey", "subtitleKey", "description", "status", "hasDataOutput", "width", "category",
                "libraryId", "className", "methodName", "dllName", "dllVersion", "returnType", "targetNodeId",
                "targetFlowId", "isAwaitable", "staticReturnType", "isDynamicReturnType", "targetCanvasId", "isPublic",
                "flowCallParameterBindings", "libraryNodeContractId", "flowLibraryName"
            ]);
            _ = ReadRequiredString(payload, "kind", $"{path}.kind");
            _ = ReadRequiredString(payload, "titleKey", $"{path}.titleKey");
            _ = ReadRequiredString(payload, "subtitleKey", $"{path}.subtitleKey");
            RequireNullableString(payload, "description", $"{path}.description");
            _ = ReadRequiredString(payload, "status", $"{path}.status");
            RequireBoolean(payload, "hasDataOutput", $"{path}.hasDataOutput");
            RequireNullableFiniteNumber(payload, "width", $"{path}.width");

            var flowCallFields = new[] { "targetNodeId", "targetFlowId", "targetCanvasId", "isPublic", "flowCallParameterBindings" };
            var libraryFields = new[] { "libraryId", "className", "methodName", "dllName", "dllVersion", "returnType", "isAwaitable", "libraryNodeContractId", "flowLibraryName" };
            if (nodeType != "flowCall" && HasAnyProperty(payload, flowCallFields))
                throw Invalid("mcp.flow_patch.unexpected_field", path, "flow-call metadata only on a flowCall node", "Remove flow-call metadata or use a flowCall node type.");
            if (nodeType is not ("action" or "flipflop") && HasAnyProperty(payload, libraryFields))
                throw Invalid("mcp.flow_patch.unexpected_field", path, "library runtime metadata only on an action or flipflop node", "Remove library metadata or use an action or flipflop node type.");
        }
        if (payload.TryGetProperty("flowCallParameterBindings", out var bindings) && bindings.ValueKind != JsonValueKind.Null)
        {
            RequireArray(bindings, $"{path}.flowCallParameterBindings");
            var index = 0;
            foreach (var binding in bindings.EnumerateArray())
            {
                RequireObject(binding, $"{path}.flowCallParameterBindings[{index}]", "Flow call binding must be an object.");
                if (!legacyEnums)
                {
                    EnsureKnownProperties(binding, $"{path}.flowCallParameterBindings[{index}]", ["callParameterId", "targetParameterId"]);
                    _ = ReadRequiredString(binding, "callParameterId", $"{path}.flowCallParameterBindings[{index}].callParameterId");
                    _ = ReadRequiredString(binding, "targetParameterId", $"{path}.flowCallParameterBindings[{index}].targetParameterId");
                }
                index++;
            }
        }
    }

    private static void ValidateParameterUiPayload(JsonElement payload, string path, bool legacyEnums)
    {
        RequireObject(payload, path, "Parameter UI payload must be an object.");
        if (!legacyEnums)
        {
            EnsureKnownProperties(payload, path,
            [
                "id", "nameKey", "valueKind", "literalValue", "projectInputKey", "expression", "sourceNodeId", "sourcePortId",
                "type", "description", "inputMode", "isVariadic", "variadicGroupId", "elementType", "variadicMode", "enumMetadata"
            ]);
            _ = ReadRequiredString(payload, "id", $"{path}.id");
            _ = ReadRequiredString(payload, "nameKey", $"{path}.nameKey");
            _ = ReadRequiredString(payload, "valueKind", $"{path}.valueKind");
        }
        if (payload.TryGetProperty("enumMetadata", out var metadata) && metadata.ValueKind != JsonValueKind.Null)
            ValidateEnumMetadata(metadata, $"{path}.enumMetadata", legacyEnums);
    }

    private static void ValidateEnumMetadata(JsonElement payload, string path, bool legacyEnums)
    {
        RequireObject(payload, path, "Enum metadata must be an object.");
        if (!legacyEnums)
        {
            EnsureKnownProperties(payload, path, ["typeName", "isFlags", "underlyingType", "options"]);
            _ = ReadRequiredString(payload, "typeName", $"{path}.typeName");
            RequireBoolean(payload, "isFlags", $"{path}.isFlags");
            _ = ReadRequiredString(payload, "underlyingType", $"{path}.underlyingType");
        }
        var options = RequireProperty(payload, "options", $"{path}.options");
        RequireArray(options, $"{path}.options");
        var index = 0;
        foreach (var option in options.EnumerateArray())
        {
            RequireObject(option, $"{path}.options[{index}]", "Enum option must be an object.");
            if (!legacyEnums)
            {
                EnsureKnownProperties(option, $"{path}.options[{index}]", ["name", "numericValue"]);
                _ = ReadRequiredString(option, "name", $"{path}.options[{index}].name");
                _ = ReadRequiredString(option, "numericValue", $"{path}.options[{index}].numericValue");
            }
            index++;
        }
    }

    private static EnumValue ValidateEnum(JsonElement value, string path, bool legacy, EnumValue[] allowed)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var supplied = value.GetString();
            var canonical = allowed.FirstOrDefault(item => string.Equals(item.Canonical, supplied, StringComparison.Ordinal));
            if (canonical is not null)
                return canonical;
            if (legacy)
            {
                var pascalCase = allowed.FirstOrDefault(item => string.Equals(item.LegacyPascalCase, supplied, StringComparison.Ordinal));
                if (pascalCase is not null)
                    return pascalCase;
            }
        }
        else if (legacy && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric)
            && allowed.FirstOrDefault(item => item.LegacyNumeric == numeric) is { } numericMatch)
        {
            return numericMatch;
        }

        var expected = string.Join(", ", allowed.Select(static item => $"'{item.Canonical}'"));
        throw Invalid(
            "mcp.flow_patch.enum_encoding_invalid",
            path,
            expected,
            $"Use canonical {FlowPatchContract.EnumEncoding} enum strings for schema {FlowPatchContract.CurrentSchemaVersion}.");
    }

    private static void RequireNullableEnum(
        JsonElement source,
        string name,
        string path,
        bool legacyEnums,
        EnumValue[] allowed)
    {
        var value = RequireProperty(source, name, path);
        if (value.ValueKind != JsonValueKind.Null)
            ValidateEnum(value, path, legacyEnums, allowed);
    }

    private static string ReadLegacyOperationName(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
        {
            return numeric switch
            {
                0 => "addCanvas",
                1 => "updateCanvas",
                2 => "removeCanvas",
                3 => "addNode",
                4 => "replaceNode",
                5 => "removeNode",
                6 => "setNodeParameter",
                7 => "addConnection",
                8 => "replaceConnection",
                9 => "removeConnection",
                10 => "setEntryNode",
                11 => "setRunPolicy",
                12 => "replaceScriptSource",
                13 => "addNodeParameter",
                14 => "removeNodeParameter",
                _ => throw Invalid("mcp.flow_patch.operation_unknown", path, "a known legacy operation number", "Use a schema 2.0 op string.")
            };
        }

        if (value.ValueKind != JsonValueKind.String)
            throw Invalid("mcp.flow_patch.operation_unknown", path, "a legacy operation string", "Use a schema 2.0 op string.");

        return value.GetString() switch
        {
            "addCanvas" or "AddCanvas" or "add_canvas" => "addCanvas",
            "updateCanvas" or "UpdateCanvas" or "update_canvas" => "updateCanvas",
            "removeCanvas" or "RemoveCanvas" or "remove_canvas" => "removeCanvas",
            "addNode" or "AddNode" or "add_node" => "addNode",
            "replaceNode" or "ReplaceNode" or "replace_node" => "replaceNode",
            "removeNode" or "RemoveNode" or "remove_node" => "removeNode",
            "setNodeParameter" or "SetNodeParameter" or "set_node_parameter" => "setNodeParameter",
            "addNodeParameter" or "AddNodeParameter" or "add_node_parameter" => "addNodeParameter",
            "removeNodeParameter" or "RemoveNodeParameter" or "remove_node_parameter" => "removeNodeParameter",
            "addConnection" or "AddConnection" or "add_connection" => "addConnection",
            "replaceConnection" or "ReplaceConnection" or "replace_connection" => "replaceConnection",
            "removeConnection" or "RemoveConnection" or "remove_connection" => "removeConnection",
            "setEntryNode" or "SetEntryNode" or "set_entry_node" => "setEntryNode",
            "setRunPolicy" or "SetRunPolicy" or "set_run_policy" => "setRunPolicy",
            "replaceScriptSource" or "ReplaceScriptSource" or "replace_script_source" => "replaceScriptSource",
            _ => throw Invalid("mcp.flow_patch.operation_unknown", path, "a supported legacy operation name", "Use a schema 2.0 op string.")
        };
    }

    private static Guid ReadGuid(JsonElement source, string name, string path)
    {
        var value = RequireProperty(source, name, path);
        if (value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var parsed) && parsed != Guid.Empty)
            return parsed;
        throw Invalid("mcp.flow_patch.field_invalid", path, "a non-empty GUID", "Provide the project or flow ID from the read model.");
    }

    private static long ReadPositiveLong(JsonElement source, string name, string path)
    {
        var value = RequireProperty(source, name, path);
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var parsed) && parsed >= 1)
            return parsed;
        throw Invalid("mcp.flow_patch.field_invalid", path, "a positive integer", "Use the latest development version from the flow read model.");
    }

    private static string? ReadOptionalString(JsonElement source, string name, string path)
    {
        if (!source.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind == JsonValueKind.String)
            return value.GetString();
        throw Invalid("mcp.flow_patch.field_invalid", path, "a string or null", "Use a string value for this field.");
    }

    private static string ReadLegacyIdentifier(JsonElement source, string name, string path)
        => ReadRequiredString(source, name, $"{path}.{name}");

    private static string ReadRequiredString(JsonElement source, string name, string path)
    {
        var value = RequireProperty(source, name, path);
        if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
            return value.GetString()!.Trim();
        throw Invalid("mcp.flow_patch.field_invalid", path, "a non-empty string", "Provide the required field value.");
    }

    private static void RequireNullableString(JsonElement source, string name, string path)
    {
        var value = RequireProperty(source, name, path);
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.String)
            return;
        throw Invalid("mcp.flow_patch.field_invalid", path, "a string or null", "Provide a string value or null.");
    }

    private static void RequireBoolean(JsonElement source, string name, string path)
    {
        var value = RequireProperty(source, name, path);
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return;
        throw Invalid("mcp.flow_patch.field_invalid", path, "a boolean", "Provide true or false.");
    }

    private static void RequireInteger(JsonElement source, string name, string path)
    {
        var value = RequireProperty(source, name, path);
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _))
            return;
        throw Invalid("mcp.flow_patch.field_invalid", path, "an integer", "Provide an integer value.");
    }

    private static void RequireFiniteNumber(JsonElement source, string name, string path)
    {
        var value = RequireProperty(source, name, path);
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var numeric) && double.IsFinite(numeric))
            return;
        throw Invalid("mcp.flow_patch.field_invalid", path, "a finite number", "Provide a finite numeric value.");
    }

    private static void RequireNullableFiniteNumber(JsonElement source, string name, string path)
    {
        var value = RequireProperty(source, name, path);
        if (value.ValueKind == JsonValueKind.Null)
            return;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var numeric) && double.IsFinite(numeric))
            return;
        throw Invalid("mcp.flow_patch.field_invalid", path, "a finite number or null", "Provide a finite numeric value or null.");
    }

    private static bool HasAnyProperty(JsonElement source, IReadOnlyCollection<string> names)
        => source.EnumerateObject().Any(property => names.Contains(property.Name, StringComparer.Ordinal)
            && property.Value.ValueKind != JsonValueKind.Null);

    private static string Required(string? value, string path)
    {
        if (!string.IsNullOrWhiteSpace(value))
            return value.Trim();
        throw Invalid("mcp.flow_patch.field_required", path, "a non-empty string", "Provide the required field value.");
    }

    private static JsonElement RequireProperty(JsonElement source, string name, string path)
    {
        if (source.TryGetProperty(name, out var value))
            return value;
        throw Invalid("mcp.flow_patch.field_required", path, $"required property '{name}'", "Provide the required field.");
    }

    private static void EnsureKnownProperties(JsonElement source, string path, IReadOnlyCollection<string> allowed)
    {
        foreach (var property in source.EnumerateObject())
        {
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
            {
                throw Invalid(
                    "mcp.flow_patch.unexpected_field",
                    $"{path}.{property.Name}",
                    string.Join(", ", allowed.Select(static item => $"'{item}'")),
                    "Remove fields that are not defined by this operation schema.");
            }
        }
    }

    private static void RequireObject(JsonElement value, string path, string message)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Invalid("mcp.flow_patch.payload_invalid", path, "an object", message);
    }

    private static void RequireArray(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw Invalid("mcp.flow_patch.payload_invalid", path, "an array", "Provide an array value.");
    }

    private static FlowPatchContractException Invalid(string code, string fieldPath, string expected, string remediation)
        => new(code, fieldPath, expected, remediation);

    private sealed class PatchReferenceState
    {
        private readonly Dictionary<string, CanvasReferenceState> _canvases;

        private PatchReferenceState(Dictionary<string, CanvasReferenceState> canvases) => _canvases = canvases;

        public static PatchReferenceState FromDefinition(FlowDefinitionDto definition)
        {
            var canvases = new Dictionary<string, CanvasReferenceState>(StringComparer.Ordinal);
            foreach (var canvas in definition.Canvases)
            {
                if (!canvases.TryAdd(canvas.Id, CanvasReferenceState.FromCanvas(canvas, "$.current.canvases")))
                {
                    throw Invalid(
                        "mcp.flow_patch.duplicate_id",
                        "$.current.canvases",
                        "unique canvas IDs",
                        "Repair the current flow before submitting a patch.");
                }
            }
            return new PatchReferenceState(canvases);
        }

        public void Apply(FlowPatchCanonicalOperationDto operation, string path)
        {
            ArgumentNullException.ThrowIfNull(operation);
            switch (operation.Op)
            {
                case "addCanvas":
                {
                    var canvas = RequireValue(operation.Canvas, $"{path}.canvas");
                    if (!_canvases.TryAdd(canvas.Id, CanvasReferenceState.FromCanvas(canvas, $"{path}.canvas")))
                        throw Duplicate($"{path}.canvas.id", "canvas ID");
                    ValidateConnections(_canvases[canvas.Id], $"{path}.canvas.connections");
                    return;
                }
                case "updateCanvas":
                {
                    var canvasId = RequireId(operation.CanvasId, $"{path}.canvasId");
                    var canvas = RequireValue(operation.Canvas, $"{path}.canvas");
                    RequireMatchingId(canvasId, canvas.Id, $"{path}.canvas.id");
                    RequireCanvas(canvasId, $"{path}.canvasId");
                    var replacement = CanvasReferenceState.FromCanvas(canvas, $"{path}.canvas");
                    ValidateConnections(replacement, $"{path}.canvas.connections");
                    _canvases[canvasId] = replacement;
                    return;
                }
                case "removeCanvas":
                {
                    var canvasId = RequireId(operation.CanvasId, $"{path}.canvasId");
                    var canvas = RequireCanvas(canvasId, $"{path}.canvasId");
                    if (canvas.Nodes.Count != 0 || canvas.Connections.Count != 0)
                    {
                        throw Invalid(
                            "mcp.flow_patch.reference_invalid",
                            $"{path}.canvasId",
                            "an empty canvas",
                            "Remove the canvas connections and nodes before removing the canvas.");
                    }
                    _canvases.Remove(canvasId);
                    return;
                }
                case "addNode":
                {
                    var canvas = RequireCanvas(RequireId(operation.CanvasId, $"{path}.canvasId"), $"{path}.canvasId");
                    var node = RequireValue(operation.Node, $"{path}.node");
                    if (!canvas.Nodes.TryAdd(node.Id, NodeReferenceState.FromNode(node, $"{path}.node")))
                        throw Duplicate($"{path}.node.id", "node ID");
                    return;
                }
                case "replaceNode":
                {
                    var canvas = RequireCanvas(RequireId(operation.CanvasId, $"{path}.canvasId"), $"{path}.canvasId");
                    var nodeId = RequireId(operation.NodeId, $"{path}.nodeId");
                    var node = RequireValue(operation.Node, $"{path}.node");
                    RequireMatchingId(nodeId, node.Id, $"{path}.node.id");
                    if (!canvas.Nodes.ContainsKey(nodeId))
                        throw ReferenceInvalid($"{path}.nodeId", "an existing node ID");
                    canvas.Nodes[nodeId] = NodeReferenceState.FromNode(node, $"{path}.node");
                    ValidateConnections(canvas, $"{path}.connections");
                    return;
                }
                case "removeNode":
                {
                    var canvas = RequireCanvas(RequireId(operation.CanvasId, $"{path}.canvasId"), $"{path}.canvasId");
                    var nodeId = RequireId(operation.NodeId, $"{path}.nodeId");
                    if (!canvas.Nodes.ContainsKey(nodeId))
                        throw ReferenceInvalid($"{path}.nodeId", "an existing node ID");
                    if (canvas.Connections.Values.Any(connection => connection.FromNodeId == nodeId || connection.ToNodeId == nodeId))
                    {
                        throw Invalid(
                            "mcp.flow_patch.reference_invalid",
                            $"{path}.nodeId",
                            "a node without connections",
                            "Remove the node connections before removing the node.");
                    }
                    canvas.Nodes.Remove(nodeId);
                    return;
                }
                case "setNodeParameter":
                {
                    var canvas = RequireCanvas(RequireId(operation.CanvasId, $"{path}.canvasId"), $"{path}.canvasId");
                    var node = RequireNode(canvas, RequireId(operation.NodeId, $"{path}.nodeId"), $"{path}.nodeId");
                    var parameterId = RequireId(operation.ParameterId, $"{path}.parameterId");
                    var parameter = RequireValue(operation.Parameter, $"{path}.parameter");
                    var actualParameterId = RequireId(parameter.Ui?.Id, $"{path}.parameter.ui.id");
                    RequireMatchingId(parameterId, actualParameterId, $"{path}.parameter.ui.id");
                    if (!node.ParameterIds.Contains(parameterId))
                        throw ReferenceInvalid($"{path}.parameterId", "an existing parameter ID on the node");
                    return;
                }
                case "addNodeParameter":
                {
                    var canvas = RequireCanvas(RequireId(operation.CanvasId, $"{path}.canvasId"), $"{path}.canvasId");
                    var node = RequireNode(canvas, RequireId(operation.NodeId, $"{path}.nodeId"), $"{path}.nodeId");
                    var parameter = RequireValue(operation.Parameter, $"{path}.parameter");
                    var parameterId = RequireId(parameter.Ui?.Id, $"{path}.parameter.ui.id");
                    if (!node.ParameterIds.Add(parameterId))
                        throw Duplicate($"{path}.parameter.ui.id", "parameter ID");
                    return;
                }
                case "removeNodeParameter":
                {
                    var canvas = RequireCanvas(RequireId(operation.CanvasId, $"{path}.canvasId"), $"{path}.canvasId");
                    var nodeId = RequireId(operation.NodeId, $"{path}.nodeId");
                    var node = RequireNode(canvas, nodeId, $"{path}.nodeId");
                    var parameterId = RequireId(operation.ParameterId, $"{path}.parameterId");
                    if (!node.ParameterIds.Contains(parameterId))
                        throw ReferenceInvalid($"{path}.parameterId", "an existing parameter ID on the node");
                    if (canvas.Connections.Values.Any(connection =>
                            connection.Kind == ConnectionKindDto.Data
                            && connection.ToNodeId == nodeId
                            && connection.ToPortId == parameterId))
                    {
                        throw Invalid(
                            "mcp.flow_patch.reference_invalid",
                            $"{path}.parameterId",
                            "a parameter without incoming data connections",
                            "Remove the parameter's data connections before removing the parameter.");
                    }
                    node.ParameterIds.Remove(parameterId);
                    return;
                }
                case "addConnection":
                {
                    var canvas = RequireCanvas(RequireId(operation.CanvasId, $"{path}.canvasId"), $"{path}.canvasId");
                    var connection = RequireValue(operation.Connection, $"{path}.connection");
                    if (canvas.Connections.ContainsKey(connection.Id))
                        throw Duplicate($"{path}.connection.id", "connection ID");
                    ValidateConnectionEndpoints(canvas, connection, $"{path}.connection");
                    canvas.Connections.Add(connection.Id, connection);
                    return;
                }
                case "replaceConnection":
                {
                    var canvas = RequireCanvas(RequireId(operation.CanvasId, $"{path}.canvasId"), $"{path}.canvasId");
                    var connectionId = RequireId(operation.ConnectionId, $"{path}.connectionId");
                    var connection = RequireValue(operation.Connection, $"{path}.connection");
                    RequireMatchingId(connectionId, connection.Id, $"{path}.connection.id");
                    if (!canvas.Connections.ContainsKey(connectionId))
                        throw ReferenceInvalid($"{path}.connectionId", "an existing connection ID");
                    ValidateConnectionEndpoints(canvas, connection, $"{path}.connection");
                    canvas.Connections[connectionId] = connection;
                    return;
                }
                case "removeConnection":
                {
                    var canvas = RequireCanvas(RequireId(operation.CanvasId, $"{path}.canvasId"), $"{path}.canvasId");
                    var connectionId = RequireId(operation.ConnectionId, $"{path}.connectionId");
                    if (!canvas.Connections.Remove(connectionId))
                        throw ReferenceInvalid($"{path}.connectionId", "an existing connection ID");
                    return;
                }
                case "setEntryNode":
                {
                    var entryNodeId = RequireId(operation.EntryNodeId, $"{path}.entryNodeId");
                    if (!_canvases.Values.Any(canvas => canvas.Nodes.ContainsKey(entryNodeId)))
                        throw ReferenceInvalid($"{path}.entryNodeId", "an existing node ID");
                    return;
                }
                case "setRunPolicy":
                    _ = RequireValue(operation.RunPolicy, $"{path}.runPolicy");
                    return;
                case "replaceScriptSource":
                {
                    var canvas = RequireCanvas(RequireId(operation.CanvasId, $"{path}.canvasId"), $"{path}.canvasId");
                    var node = RequireNode(canvas, RequireId(operation.NodeId, $"{path}.nodeId"), $"{path}.nodeId");
                    if (!node.HasScript)
                    {
                        throw Invalid(
                            "mcp.flow_patch.reference_invalid",
                            $"{path}.nodeId",
                            "an existing script node",
                            "Use replaceScriptSource only with a script node.");
                    }
                    return;
                }
                default:
                    throw Invalid(
                        "mcp.flow_patch.operation_unknown",
                        $"{path}.op",
                        "one of the 15 schema 2.0 operation names",
                        "Use the operation names exposed by sereinflow_preview_flow_patch.");
            }
        }

        private CanvasReferenceState RequireCanvas(string canvasId, string path)
            => _canvases.TryGetValue(canvasId, out var canvas)
                ? canvas
                : throw ReferenceInvalid(path, "an existing canvas ID");

        private static NodeReferenceState RequireNode(CanvasReferenceState canvas, string nodeId, string path)
            => canvas.Nodes.TryGetValue(nodeId, out var node)
                ? node
                : throw ReferenceInvalid(path, "an existing node ID");

        private static void ValidateConnections(CanvasReferenceState canvas, string path)
        {
            foreach (var connection in canvas.Connections.Values)
                ValidateConnectionEndpoints(canvas, connection, path);
        }

        private static void ValidateConnectionEndpoints(CanvasReferenceState canvas, ConnectionDto connection, string path)
        {
            var fromNode = RequireNode(canvas, connection.FromNodeId, $"{path}.fromNodeId");
            if (!fromNode.PortIds.Contains(connection.FromPortId))
                throw ReferenceInvalid($"{path}.fromPortId", "a port on fromNodeId");
            var toNode = RequireNode(canvas, connection.ToNodeId, $"{path}.toNodeId");
            if (connection.Kind == ConnectionKindDto.Data)
            {
                if (!toNode.ParameterIds.Contains(connection.ToPortId))
                    throw ReferenceInvalid($"{path}.toPortId", "a parameter ID on toNodeId");
                return;
            }
            if (!toNode.PortIds.Contains(connection.ToPortId))
                throw ReferenceInvalid($"{path}.toPortId", "a port on toNodeId");
        }

        private static T RequireValue<T>(T? value, string path) where T : class
            => value ?? throw Invalid(
                "mcp.flow_patch.field_required",
                path,
                "the operation payload",
                "Provide the payload required by the selected operation.");

        private static string RequireId(string? value, string path)
            => string.IsNullOrWhiteSpace(value)
                ? throw Invalid("mcp.flow_patch.field_required", path, "a non-empty ID", "Provide an ID from the flow edit model.")
                : value.Trim();

        private static void RequireMatchingId(string expected, string actual, string path)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw Invalid(
                    "mcp.flow_patch.reference_invalid",
                    path,
                    $"the ID '{expected}'",
                    "Make the payload ID match the operation target ID.");
            }
        }

        private static FlowPatchContractException Duplicate(string path, string kind)
            => Invalid(
                "mcp.flow_patch.duplicate_id",
                path,
                $"a unique {kind}",
                "Use an ID that is not already present in the target canvas.");

        private static FlowPatchContractException ReferenceInvalid(string path, string expected)
            => Invalid(
                "mcp.flow_patch.reference_invalid",
                path,
                expected,
                "Read the current flow edit model and use an existing ID and port.");

        private sealed class CanvasReferenceState
        {
            public Dictionary<string, NodeReferenceState> Nodes { get; }
            public Dictionary<string, ConnectionDto> Connections { get; }

            private CanvasReferenceState(
                Dictionary<string, NodeReferenceState> nodes,
                Dictionary<string, ConnectionDto> connections)
            {
                Nodes = nodes;
                Connections = connections;
            }

            public static CanvasReferenceState FromCanvas(CanvasDto canvas, string path)
            {
                var nodes = new Dictionary<string, NodeReferenceState>(StringComparer.Ordinal);
                foreach (var node in canvas.Nodes)
                {
                    if (!nodes.TryAdd(node.Id, NodeReferenceState.FromNode(node, $"{path}.nodes")))
                        throw Duplicate($"{path}.nodes", "node ID");
                }

                var connections = new Dictionary<string, ConnectionDto>(StringComparer.Ordinal);
                foreach (var connection in canvas.Connections)
                {
                    if (!connections.TryAdd(connection.Id, connection))
                        throw Duplicate($"{path}.connections", "connection ID");
                }
                return new CanvasReferenceState(nodes, connections);
            }
        }

        private sealed class NodeReferenceState
        {
            public HashSet<string> PortIds { get; }
            public HashSet<string> ParameterIds { get; }
            public bool HasScript { get; }

            private NodeReferenceState(HashSet<string> portIds, HashSet<string> parameterIds, bool hasScript)
            {
                PortIds = portIds;
                ParameterIds = parameterIds;
                HasScript = hasScript;
            }

            public static NodeReferenceState FromNode(NodeDto node, string path)
            {
                var ports = new HashSet<string>(StringComparer.Ordinal);
                foreach (var port in node.Ports)
                {
                    if (string.IsNullOrWhiteSpace(port.Id) || !ports.Add(port.Id))
                        throw Duplicate($"{path}.ports", "port ID");
                }

                var parameters = new HashSet<string>(StringComparer.Ordinal);
                foreach (var parameter in node.Parameters)
                {
                    var parameterId = RequireId(parameter.Ui?.Id, $"{path}.parameters.ui.id");
                    if (!parameters.Add(parameterId))
                        throw Duplicate($"{path}.parameters.ui.id", "parameter ID");
                }
                return new NodeReferenceState(ports, parameters, node.Script is not null);
            }
        }
    }

    private sealed record EnumValue(string Canonical, string LegacyPascalCase, int LegacyNumeric);

    private static readonly EnumValue[] NodeTypes =
    [
        new("action", "Action", 0),
        new("flipflop", "Flipflop", 1),
        new("script", "Script", 2),
        new("flowCall", "FlowCall", 4)
    ];

    private static readonly EnumValue[] CanvasLifecycles =
    [
        new("main", "Main", 0), new("init", "Init", 1), new("loading", "Loading", 2),
        new("exit", "Exit", 3), new("custom", "Custom", 4)
    ];

    private static readonly EnumValue[] ConnectionKinds =
    [new("execution", "Execution", 0), new("data", "Data", 1)];

    private static readonly EnumValue[] ExecutionBranches =
    [new("success", "Success", 0), new("failure", "Failure", 1), new("error", "Error", 2)];

    private static readonly EnumValue[] DataSources =
    [
        new("literal", "Literal", 0), new("previousNode", "PreviousNode", 1),
        new("projectInput", "ProjectInput", 2), new("expression", "Expression", 3)
    ];

    private static readonly EnumValue[] ConcurrencyModes =
    [new("parallel", "Parallel", 0), new("exclusiveReject", "ExclusiveReject", 1)];
}

public sealed record NormalizedFlowPatchRequest(
    FlowPatchCanonicalRequestDto Request,
    FlowPatchRequestDto LegacyRequest,
    IReadOnlyList<FlowPatchNormalizationWarningDto> Warnings);

public sealed class FlowPatchContractException(
    string code,
    string fieldPath,
    string expected,
    string remediation) : Exception("The flow patch contract is invalid.")
{
    public string Code { get; } = code;
    public string FieldPath { get; } = fieldPath;
    public string Expected { get; } = expected;
    public string Remediation { get; } = remediation;
    public string DiagnosticId { get; } = Guid.NewGuid().ToString("N");
}

using System.Collections.Generic;
using System.Text.Json;
using SereinFlow.Contracts;

namespace SereinFlow.Application;
/// <summary>
/// Converts the public flow-patch wire formats into one strongly typed v2
/// model before a patch reaches the definition service. Version 1 is retained
/// only to read existing callers and persisted preview payloads.
/// 在补丁进入定义服务前，将公开的流程补丁传输格式转换为统一的强类型 v2 模型；v1 仅用于读取既有调用和持久化预览。
/// </summary>
public sealed class FlowPatchContractNormalizer
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateContractOptions();
    public NormalizedFlowPatchRequest Normalize(JsonElement arguments)
    {
        RequireObject(arguments, "$", "MCP tool arguments must be a JSON object. MCP 工具参数必须是 JSON 对象。");
        var isV2 = ReadSchemaVersion(arguments, out var schemaVersion, out var warnings);
        if (isV2)
        {
            EnsureKnownProperties(arguments, "$", ["projectId", "flowId", "expectedDevelopmentVersion", "schemaVersion", "operations", "remark"]);
        }

        var projectId = ReadGuid(arguments, "projectId", "$.projectId");
        var flowId = ReadGuid(arguments, "flowId", "$.flowId");
        var expectedVersion = ReadPositiveLong(arguments, "expectedDevelopmentVersion", "$.expectedDevelopmentVersion");
        var remark = ReadOptionalString(arguments, "remark", "$.remark");
        var operationElement = RequireProperty(arguments, "operations", "$.operations");
        if (operationElement.ValueKind != JsonValueKind.Array)
        {
            throw Invalid(McpErrorCodes.FlowPatchOperationsInvalid, "$.operations", "an array", "Provide a typed operations array. 请提供强类型操作数组。");
        }

        if (isV2 && operationElement.GetArrayLength() == 0)
        {
            throw Invalid(McpErrorCodes.FlowPatchOperationRequired, "$.operations", "at least one typed operation", "Add an operation or omit the preview request. 请添加操作，或省略预览请求。");
        }
        var operations = new List<FlowPatchCanonicalOperationDto>(operationElement.GetArrayLength());
        var index = 0;
        foreach (var operation in operationElement.EnumerateArray())
        {
            var path = $"$.operations[{index}]";
            operations.Add((isV2 ? ReadV2Operation(operation, path) : ReadV1Operation(operation, path)));
            index++;
        }

        var canonical = new FlowPatchCanonicalRequestDto(projectId, flowId, expectedVersion, schemaVersion, operations, remark);
        return new NormalizedFlowPatchRequest(canonical, ToLegacyRequest(canonical), warnings);
    }

    public NormalizedFlowPatchRequest NormalizeLegacyRequest(FlowPatchRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var operations = request.Operations
            .Select((operation, index) =>
            {
                return ReadPersistedLegacyOperation(operation, $"$.operations[{index}]");
            })
            .ToArray();
        var canonical = new FlowPatchCanonicalRequestDto(request.ProjectId, request.FlowId, request.ExpectedDevelopmentVersion, FlowPatchContract.CurrentSchemaVersion, operations, request.Remark);
        IReadOnlyList<FlowPatchNormalizationWarningDto> warnings = [new FlowPatchNormalizationWarningDto(McpErrorCodes.FlowPatchLegacyInput, "$.operations", "The stored preview uses the legacy flow-patch representation and was normalized to schema 2.0. 已存储的预览使用旧版流程补丁表示，现已规范化为架构 2.0。")];
        return new NormalizedFlowPatchRequest(canonical, request, warnings);
    }

    public IReadOnlyList<FlowPatchCanonicalOperationDto> NormalizeLegacyOperations(IReadOnlyList<FlowPatchOperationDto> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        return operations
            .Select((operation, index) =>
            {
                return ReadPersistedLegacyOperation(operation, $"$.operations[{index}]");
            })
            .ToArray();
    }

    /// <summary>
    /// Validates identifiers and endpoint references against the current flow
    /// without invoking the legacy mutation implementation. The MCP boundary
    /// calls this after wire normalization and before FlowPatchService so
    /// duplicate IDs and unresolved references receive contract diagnostics.
    /// 对照当前流程校验标识符和端点引用，不调用旧版修改实现；MCP 在传输规范化之后、FlowPatchService 之前调用此方法，为重复 ID 和未解析引用返回契约诊断。
    /// </summary>
    public void ValidateReferences(FlowDefinitionDto definition, IReadOnlyList<FlowPatchCanonicalOperationDto> operations)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(operations);
        var state = PatchReferenceState.FromDefinition(definition);
        for (var index = 0; index < operations.Count; index++)
        {
            state.Apply(operations[index], $"$.operations[{index}]");
        }
    }

    public static FlowPatchRequestDto ToLegacyRequest(FlowPatchCanonicalRequestDto request)
    {
        var operations = request.Operations
            .Select(ToLegacyOperation)
            .ToArray();
        return new(request.ProjectId, request.FlowId, request.ExpectedDevelopmentVersion, operations, request.Remark);
    }

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
            _ => throw Invalid(McpErrorCodes.FlowPatchOperationUnknown, "$.operations", "a supported schema 2.0 operation", "Use the operation names published by sereinflow_preview_flow_patch. 请使用 sereinflow_preview_flow_patch 公布的操作名称。")
        };
    }

    private static FlowPatchOperationDto Legacy(FlowPatchOperationKindDto operation, string? canvasId = null, string? nodeId = null, string? connectionId = null, string? parameterId = null, object? value = null)
    {
        JsonElement? serializeToElement = value is null ? null : JsonSerializer.SerializeToElement(value, JsonOptions);
        return new(operation, canvasId, nodeId, connectionId, parameterId, serializeToElement);
    }

    private static bool ReadSchemaVersion(JsonElement arguments, out string schemaVersion, out IReadOnlyList<FlowPatchNormalizationWarningDto> warnings)
    {
        if (!arguments.TryGetProperty("schemaVersion", out var value))
        {
            schemaVersion = FlowPatchContract.CurrentSchemaVersion;
            warnings = [new FlowPatchNormalizationWarningDto(McpErrorCodes.FlowPatchLegacyInput, "$.schemaVersion", "Schema version 1.0 was inferred because schemaVersion was omitted. 未提供 schemaVersion，已推断为架构版本 1.0。")];
            return false;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw Invalid(McpErrorCodes.FlowPatchSchemaVersionInvalid, "$.schemaVersion", $"'{FlowPatchContract.LegacySchemaVersion}' or '{FlowPatchContract.CurrentSchemaVersion}'", $"Use schemaVersion '{FlowPatchContract.CurrentSchemaVersion}' for new requests. 新请求请使用上述 schemaVersion 版本。");
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
            warnings = [new FlowPatchNormalizationWarningDto(McpErrorCodes.FlowPatchLegacyInput, "$.schemaVersion", "Schema version 1.0 is deprecated; the preview response uses schema 2.0. 架构版本 1.0 已弃用；预览响应使用架构 2.0。")];
            return false;
        }

        throw Invalid(McpErrorCodes.FlowPatchSchemaVersionUnsupported, "$.schemaVersion", $"'{FlowPatchContract.LegacySchemaVersion}' or '{FlowPatchContract.CurrentSchemaVersion}'", $"Upgrade the request to schemaVersion '{FlowPatchContract.CurrentSchemaVersion}'. 请将请求升级到上述 schemaVersion 版本。");
    }

    private static FlowPatchCanonicalOperationDto ReadV2Operation(JsonElement operation, string path)
    {
        RequireObject(operation, path, "Each operation must be an object. 每项操作必须是对象。");
        var op = ReadRequiredString(operation, "op", $"{path}.op");
        return op switch
        {
            "addCanvas" => ReadAddCanvas(),
            "updateCanvas" => ReadUpdateCanvas(),
            "removeCanvas" => ReadV2RemoveCanvas(operation, path, op),
            "addNode" => ReadAddNode(),
            "replaceNode" => ReadReplaceNode(),
            "removeNode" => ReadV2RemoveNode(operation, path, op),
            "setNodeParameter" => ReadSetNodeParameter(),
            "addNodeParameter" => ReadAddNodeParameter(),
            "removeNodeParameter" => ReadV2RemoveNodeParameter(operation, path, op),
            "addConnection" => ReadAddConnection(),
            "replaceConnection" => ReadReplaceConnection(),
            "removeConnection" => ReadV2RemoveConnection(operation, path, op),
            "setEntryNode" => ReadV2SetEntryNode(operation, path, op),
            "setRunPolicy" => ReadSetRunPolicy(),
            "replaceScriptSource" => ReadV2ReplaceScriptSource(operation, path, op),
            _ => throw Invalid(McpErrorCodes.FlowPatchOperationUnknown, $"{path}.op", "one of the 15 schema 2.0 operation names", "Use the operation names exposed by sereinflow_preview_flow_patch. 请使用 sereinflow_preview_flow_patch 公开的操作名称。")
        };
        FlowPatchCanonicalOperationDto ReadAddCanvas()
        {
            var canvas = ReadCanvas(operation, "canvas", path, legacyEnums: false, ["op", "canvas"]);
            return new(op, Canvas: canvas);
        }

        FlowPatchCanonicalOperationDto ReadUpdateCanvas()
        {
            var canvasId = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
            var canvas = ReadCanvas(operation, "canvas", path, legacyEnums: false, ["op", "canvasId", "canvas"]);
            return new(op, canvasId, Canvas: canvas);
        }

        FlowPatchCanonicalOperationDto ReadAddNode()
        {
            var canvasId = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
            var node = ReadNode(operation, "node", path, legacyEnums: false, ["op", "canvasId", "node"]);
            return new(op, canvasId, Node: node);
        }

        FlowPatchCanonicalOperationDto ReadReplaceNode()
        {
            var canvasId = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
            var nodeId = ReadRequiredString(operation, "nodeId", $"{path}.nodeId");
            var node = ReadNode(operation, "node", path, legacyEnums: false, ["op", "canvasId", "nodeId", "node"]);
            return new(op, canvasId, nodeId, Node: node);
        }

        FlowPatchCanonicalOperationDto ReadSetNodeParameter()
        {
            var canvasId = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
            var nodeId = ReadRequiredString(operation, "nodeId", $"{path}.nodeId");
            var parameterId = ReadRequiredString(operation, "parameterId", $"{path}.parameterId");
            var parameter = ReadParameter(operation, "parameter", path, legacyEnums: false, ["op", "canvasId", "nodeId", "parameterId", "parameter"]);
            return new(op, canvasId, nodeId, ParameterId: parameterId, Parameter: parameter);
        }

        FlowPatchCanonicalOperationDto ReadAddNodeParameter()
        {
            var canvasId = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
            var nodeId = ReadRequiredString(operation, "nodeId", $"{path}.nodeId");
            var parameter = ReadParameter(operation, "parameter", path, legacyEnums: false, ["op", "canvasId", "nodeId", "parameter"]);
            return new(op, canvasId, nodeId, Parameter: parameter);
        }

        FlowPatchCanonicalOperationDto ReadAddConnection()
        {
            var canvasId = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
            var connection = ReadConnection(operation, "connection", path, legacyEnums: false, ["op", "canvasId", "connection"]);
            return new(op, canvasId, Connection: connection);
        }

        FlowPatchCanonicalOperationDto ReadReplaceConnection()
        {
            var canvasId = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
            var connectionId = ReadRequiredString(operation, "connectionId", $"{path}.connectionId");
            var connection = ReadConnection(operation, "connection", path, legacyEnums: false, ["op", "canvasId", "connectionId", "connection"]);
            return new(op, canvasId, ConnectionId: connectionId, Connection: connection);
        }

        FlowPatchCanonicalOperationDto ReadSetRunPolicy()
        {
            var runPolicy = ReadRunPolicy(operation, "runPolicy", path, legacyEnums: false, ["op", "runPolicy"]);
            return new(op, RunPolicy: runPolicy);
        }
    }

    private static FlowPatchCanonicalOperationDto ReadV2RemoveCanvas(JsonElement operation, string path, string op)
    {
        EnsureKnownProperties(operation, path, ["op", "canvasId"]);
        var canvasId = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
        return new(op, canvasId);
    }

    private static FlowPatchCanonicalOperationDto ReadV2RemoveNode(JsonElement operation, string path, string op)
    {
        EnsureKnownProperties(operation, path, ["op", "canvasId", "nodeId"]);
        var canvasId = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
        var nodeId = ReadRequiredString(operation, "nodeId", $"{path}.nodeId");
        return new(op, canvasId, nodeId);
    }

    private static FlowPatchCanonicalOperationDto ReadV2RemoveNodeParameter(JsonElement operation, string path, string op)
    {
        EnsureKnownProperties(operation, path, ["op", "canvasId", "nodeId", "parameterId"]);
        var canvasId = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
        var nodeId = ReadRequiredString(operation, "nodeId", $"{path}.nodeId");
        var parameterId = ReadRequiredString(operation, "parameterId", $"{path}.parameterId");
        return new(op, canvasId, nodeId, ParameterId: parameterId);
    }

    private static FlowPatchCanonicalOperationDto ReadV2RemoveConnection(JsonElement operation, string path, string op)
    {
        EnsureKnownProperties(operation, path, ["op", "canvasId", "connectionId"]);
        var canvasId = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
        var connectionId = ReadRequiredString(operation, "connectionId", $"{path}.connectionId");
        return new(op, canvasId, ConnectionId: connectionId);
    }

    private static FlowPatchCanonicalOperationDto ReadV2SetEntryNode(JsonElement operation, string path, string op)
    {
        EnsureKnownProperties(operation, path, ["op", "entryNodeId"]);
        var entryNodeId = ReadRequiredString(operation, "entryNodeId", $"{path}.entryNodeId");
        return new(op, EntryNodeId: entryNodeId);
    }

    private static FlowPatchCanonicalOperationDto ReadV2ReplaceScriptSource(JsonElement operation, string path, string op)
    {
        EnsureKnownProperties(operation, path, ["op", "canvasId", "nodeId", "source"]);
        var canvasId = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
        var nodeId = ReadRequiredString(operation, "nodeId", $"{path}.nodeId");
        var source = ReadRequiredString(operation, "source", $"{path}.source");
        return new(op, canvasId, nodeId, Source: source);
    }

    private static FlowPatchCanonicalOperationDto ReadV1Operation(JsonElement operation, string path)
    {
        RequireObject(operation, path, "Each operation must be an object. 每项操作必须是对象。");
        var op = ReadLegacyOperationName(RequireProperty(operation, "operation", $"{path}.operation"), $"{path}.operation");
        return op switch
        {
            "addCanvas" => ReadAddCanvas(),
            "updateCanvas" => ReadUpdateCanvas(),
            "removeCanvas" => ReadRemoveCanvas(),
            "addNode" => ReadAddNode(),
            "replaceNode" => ReadReplaceNode(),
            "removeNode" => ReadRemoveNode(),
            "setNodeParameter" => ReadSetNodeParameter(),
            "addNodeParameter" => ReadAddNodeParameter(),
            "removeNodeParameter" => ReadRemoveNodeParameter(),
            "addConnection" => ReadAddConnection(),
            "replaceConnection" => ReadReplaceConnection(),
            "removeConnection" => ReadRemoveConnection(),
            "setEntryNode" => ReadSetEntryNode(),
            "setRunPolicy" => ReadSetRunPolicy(),
            "replaceScriptSource" => ReadReplaceScriptSource(),
            _ => throw Invalid(McpErrorCodes.FlowPatchOperationUnknown, $"{path}.operation", "a supported legacy operation name", "Use schemaVersion 2.0 and the canonical op names. 请使用 schemaVersion 2.0 和规范的 op 名称。")
        };
        FlowPatchCanonicalOperationDto ReadAddCanvas()
        {
            var canvas = ReadLegacyPayload<CanvasDto>(operation, path, legacyEnums: true);
            return new(op, Canvas: canvas);
        }

        FlowPatchCanonicalOperationDto ReadUpdateCanvas()
        {
            var canvasId = ReadLegacyIdentifier(operation, "canvasId", path);
            var nodeId = ReadLegacyPayload<CanvasDto>(operation, path, legacyEnums: true);
            return new(op, canvasId, Canvas: nodeId);
        }

        FlowPatchCanonicalOperationDto ReadRemoveCanvas()
        {
            var canvasId = ReadLegacyIdentifier(operation, "canvasId", path);
            return new(op, canvasId);
        }

        FlowPatchCanonicalOperationDto ReadAddNode()
        {
            var canvasId = ReadLegacyIdentifier(operation, "canvasId", path);
            var node = ReadLegacyPayload<NodeDto>(operation, path, legacyEnums: true);
            return new(op, canvasId, Node: node);
        }

        FlowPatchCanonicalOperationDto ReadReplaceNode()
        {
            var canvasId = ReadLegacyIdentifier(operation, "canvasId", path);
            var nodeId = ReadLegacyIdentifier(operation, "nodeId", path);
            var connectionId = ReadLegacyPayload<NodeDto>(operation, path, legacyEnums: true);
            return new(op, canvasId, nodeId, Node: connectionId);
        }

        FlowPatchCanonicalOperationDto ReadRemoveNode()
        {
            var canvasId = ReadLegacyIdentifier(operation, "canvasId", path);
            var nodeId = ReadLegacyIdentifier(operation, "nodeId", path);
            return new(op, canvasId, nodeId);
        }

        FlowPatchCanonicalOperationDto ReadSetNodeParameter()
        {
            var canvasId = ReadLegacyIdentifier(operation, "canvasId", path);
            var nodeId = ReadLegacyIdentifier(operation, "nodeId", path);
            var parameterId = ReadLegacyIdentifier(operation, "parameterId", path);
            var parameter = ReadLegacyPayload<NodeParameterDto>(operation, path, legacyEnums: true);
            return new(op, canvasId, nodeId, ParameterId: parameterId, Parameter: parameter);
        }

        FlowPatchCanonicalOperationDto ReadAddNodeParameter()
        {
            var canvasId = ReadLegacyIdentifier(operation, "canvasId", path);
            var nodeId = ReadLegacyIdentifier(operation, "nodeId", path);
            var nodeParameterDto = ReadLegacyPayload<NodeParameterDto>(operation, path, legacyEnums: true);
            return new(op, canvasId, nodeId, Parameter: nodeParameterDto);
        }

        FlowPatchCanonicalOperationDto ReadRemoveNodeParameter()
        {
            var canvasId = ReadLegacyIdentifier(operation, "canvasId", path);
            var nodeId = ReadLegacyIdentifier(operation, "nodeId", path);
            var parameterId = ReadLegacyIdentifier(operation, "parameterId", path);
            return new(op, canvasId, nodeId, ParameterId: parameterId);
        }

        FlowPatchCanonicalOperationDto ReadAddConnection()
        {
            var canvasId = ReadLegacyIdentifier(operation, "canvasId", path);
            var connection = NormalizeConnection(ReadLegacyPayload<ConnectionDto>(operation, path, legacyEnums: true));
            return new(op, canvasId, Connection: connection);
        }

        FlowPatchCanonicalOperationDto ReadReplaceConnection()
        {
            var canvasId = ReadLegacyIdentifier(operation, "canvasId", path);
            var connectionId = ReadLegacyIdentifier(operation, "connectionId", path);
            var normalizeConnection = NormalizeConnection(ReadLegacyPayload<ConnectionDto>(operation, path, legacyEnums: true));
            return new(op, canvasId, ConnectionId: connectionId, Connection: normalizeConnection);
        }

        FlowPatchCanonicalOperationDto ReadRemoveConnection()
        {
            var canvasId = ReadLegacyIdentifier(operation, "canvasId", path);
            var connectionId = ReadLegacyIdentifier(operation, "connectionId", path);
            return new(op, canvasId, ConnectionId: connectionId);
        }

        FlowPatchCanonicalOperationDto ReadSetEntryNode()
        {
            var entryNodeId = ReadLegacyPayload<string>(operation, path, legacyEnums: true);
            return new(op, EntryNodeId: entryNodeId);
        }

        FlowPatchCanonicalOperationDto ReadSetRunPolicy()
        {
            var runPolicy = ReadLegacyPayload<FlowRunPolicyDto>(operation, path, legacyEnums: true);
            return new(op, RunPolicy: runPolicy);
        }

        FlowPatchCanonicalOperationDto ReadReplaceScriptSource()
        {
            var canvasId = ReadLegacyIdentifier(operation, "canvasId", path);
            var nodeId = ReadLegacyIdentifier(operation, "nodeId", path);
            var source = ReadLegacyPayload<string>(operation, path, legacyEnums: true);
            return new(op, canvasId, nodeId, Source: source);
        }
    }

    private static FlowPatchCanonicalOperationDto ReadPersistedLegacyOperation(FlowPatchOperationDto operation, string path)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var value = operation.Value;
        return operation.Operation switch
        {
            FlowPatchOperationKindDto.AddCanvas => ReadAddCanvas(),
            FlowPatchOperationKindDto.UpdateCanvas => ReadUpdateCanvas(),
            FlowPatchOperationKindDto.RemoveCanvas => ReadRemoveCanvas(),
            FlowPatchOperationKindDto.AddNode => ReadAddNode(),
            FlowPatchOperationKindDto.ReplaceNode => ReadReplaceNode(),
            FlowPatchOperationKindDto.RemoveNode => ReadRemoveNode(),
            FlowPatchOperationKindDto.SetNodeParameter => ReadSetNodeParameter(),
            FlowPatchOperationKindDto.AddNodeParameter => ReadAddNodeParameter(),
            FlowPatchOperationKindDto.RemoveNodeParameter => ReadRemoveNodeParameter(),
            FlowPatchOperationKindDto.AddConnection => ReadAddConnection(),
            FlowPatchOperationKindDto.ReplaceConnection => ReadReplaceConnection(),
            FlowPatchOperationKindDto.RemoveConnection => ReadRemoveConnection(),
            FlowPatchOperationKindDto.SetEntryNode => ReadSetEntryNode(),
            FlowPatchOperationKindDto.SetRunPolicy => ReadSetRunPolicy(),
            FlowPatchOperationKindDto.ReplaceScriptSource => ReadReplaceScriptSource(),
            _ => throw Invalid(McpErrorCodes.FlowPatchOperationUnknown, path, "a supported legacy operation", "Create a new schema 2.0 preview. 请创建新的架构 2.0 预览。")
        };
        FlowPatchCanonicalOperationDto ReadAddCanvas()
        {
            var canvas = ReadPersistedPayload<CanvasDto>(value, path, legacyEnums: true);
            return new("addCanvas", Canvas: canvas);
        }

        FlowPatchCanonicalOperationDto ReadUpdateCanvas()
        {
            var canvasId = Required(operation.CanvasId, $"{path}.canvasId");
            var nodeId = ReadPersistedPayload<CanvasDto>(value, path, legacyEnums: true);
            return new("updateCanvas", canvasId, Canvas: nodeId);
        }

        FlowPatchCanonicalOperationDto ReadRemoveCanvas()
        {
            var canvasId = Required(operation.CanvasId, $"{path}.canvasId");
            return new("removeCanvas", canvasId);
        }

        FlowPatchCanonicalOperationDto ReadAddNode()
        {
            var canvasId = Required(operation.CanvasId, $"{path}.canvasId");
            var node = ReadPersistedPayload<NodeDto>(value, path, legacyEnums: true);
            return new("addNode", canvasId, Node: node);
        }

        FlowPatchCanonicalOperationDto ReadReplaceNode()
        {
            var canvasId = Required(operation.CanvasId, $"{path}.canvasId");
            var nodeId = Required(operation.NodeId, $"{path}.nodeId");
            var connectionId = ReadPersistedPayload<NodeDto>(value, path, legacyEnums: true);
            return new("replaceNode", canvasId, nodeId, Node: connectionId);
        }

        FlowPatchCanonicalOperationDto ReadRemoveNode()
        {
            var canvasId = Required(operation.CanvasId, $"{path}.canvasId");
            var nodeId = Required(operation.NodeId, $"{path}.nodeId");
            return new("removeNode", canvasId, nodeId);
        }

        FlowPatchCanonicalOperationDto ReadSetNodeParameter()
        {
            var canvasId = Required(operation.CanvasId, $"{path}.canvasId");
            var nodeId = Required(operation.NodeId, $"{path}.nodeId");
            var parameterId = Required(operation.ParameterId, $"{path}.parameterId");
            var parameter = ReadPersistedPayload<NodeParameterDto>(value, path, legacyEnums: true);
            return new("setNodeParameter", canvasId, nodeId, ParameterId: parameterId, Parameter: parameter);
        }

        FlowPatchCanonicalOperationDto ReadAddNodeParameter()
        {
            var canvasId = Required(operation.CanvasId, $"{path}.canvasId");
            var nodeId = Required(operation.NodeId, $"{path}.nodeId");
            var nodeParameterDto = ReadPersistedPayload<NodeParameterDto>(value, path, legacyEnums: true);
            return new("addNodeParameter", canvasId, nodeId, Parameter: nodeParameterDto);
        }

        FlowPatchCanonicalOperationDto ReadRemoveNodeParameter()
        {
            var canvasId = Required(operation.CanvasId, $"{path}.canvasId");
            var nodeId = Required(operation.NodeId, $"{path}.nodeId");
            var parameterId = Required(operation.ParameterId, $"{path}.parameterId");
            return new("removeNodeParameter", canvasId, nodeId, ParameterId: parameterId);
        }

        FlowPatchCanonicalOperationDto ReadAddConnection()
        {
            var canvasId = Required(operation.CanvasId, $"{path}.canvasId");
            var connection = NormalizeConnection(ReadPersistedPayload<ConnectionDto>(value, path, legacyEnums: true));
            return new("addConnection", canvasId, Connection: connection);
        }

        FlowPatchCanonicalOperationDto ReadReplaceConnection()
        {
            var canvasId = Required(operation.CanvasId, $"{path}.canvasId");
            var connectionId = Required(operation.ConnectionId, $"{path}.connectionId");
            var normalizeConnection = NormalizeConnection(ReadPersistedPayload<ConnectionDto>(value, path, legacyEnums: true));
            return new("replaceConnection", canvasId, ConnectionId: connectionId, Connection: normalizeConnection);
        }

        FlowPatchCanonicalOperationDto ReadRemoveConnection()
        {
            var canvasId = Required(operation.CanvasId, $"{path}.canvasId");
            var connectionId = Required(operation.ConnectionId, $"{path}.connectionId");
            return new("removeConnection", canvasId, ConnectionId: connectionId);
        }

        FlowPatchCanonicalOperationDto ReadSetEntryNode()
        {
            var entryNodeId = ReadPersistedPayload<string>(value, path, legacyEnums: true);
            return new("setEntryNode", EntryNodeId: entryNodeId);
        }

        FlowPatchCanonicalOperationDto ReadSetRunPolicy()
        {
            var runPolicy = ReadPersistedPayload<FlowRunPolicyDto>(value, path, legacyEnums: true);
            return new("setRunPolicy", RunPolicy: runPolicy);
        }

        FlowPatchCanonicalOperationDto ReadReplaceScriptSource()
        {
            var canvasId = Required(operation.CanvasId, $"{path}.canvasId");
            var nodeId = Required(operation.NodeId, $"{path}.nodeId");
            var source = ReadPersistedPayload<string>(value, path, legacyEnums: true);
            return new("replaceScriptSource", canvasId, nodeId, Source: source);
        }
    }

    private static T ReadLegacyPayload<T>(JsonElement operation, string path, bool legacyEnums)
    {
        return ReadPayload<T>(RequireProperty(operation, "value", $"{path}.value"), $"{path}.value", legacyEnums);
    }

    private static T ReadPersistedPayload<T>(JsonElement? value, string path, bool legacyEnums)
    {
        if (value is not { } payload)
        {
            throw Invalid(McpErrorCodes.FlowPatchFieldRequired, $"{path}.value", "a value", "Provide the operation payload. 请提供操作载荷。");
        }
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

    private static ConnectionDto NormalizeConnection(ConnectionDto connection) => connection with
    {
        DataSource = connection.Kind == ConnectionKindDto.Data ? DataSourceDto.PreviousNode : null,
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
            return payload.Deserialize<T>(JsonOptions) ?? throw Invalid(McpErrorCodes.FlowPatchPayloadInvalid, path, typeof(T).Name, "Provide a valid typed payload. 请提供有效的强类型载荷。");
        }
        catch (JsonException)
        {
            throw Invalid(McpErrorCodes.FlowPatchPayloadInvalid, path, typeof(T).Name, "Provide a valid typed payload. 请提供有效的强类型载荷。");
        }
        catch (NotSupportedException)
        {
            throw Invalid(McpErrorCodes.FlowPatchPayloadInvalid, path, typeof(T).Name, "Provide a supported typed payload. 请提供受支持的强类型载荷。");
        }
    }

    private static void ValidatePayload<T>(JsonElement payload, string path, bool legacyEnums)
    {
        if (typeof(T) == typeof(CanvasDto))
        {
            ValidateCanvasPayload(payload, path, legacyEnums);
        }
        else if (typeof(T) == typeof(NodeDto))
        {
            ValidateNodePayload(payload, path, legacyEnums);
        }
        else if (typeof(T) == typeof(NodeParameterDto))
        {
            ValidateParameterPayload(payload, path, legacyEnums);
        }
        else if (typeof(T) == typeof(ConnectionDto))
        {
            ValidateConnectionPayload(payload, path, legacyEnums);
        }
        else if (typeof(T) == typeof(FlowRunPolicyDto))
        {
            ValidateRunPolicyPayload(payload, path, legacyEnums);
        }
    }

    private static void ValidateCanvasPayload(JsonElement payload, string path, bool legacyEnums)
    {
        RequireObject(payload, path, "Canvas payload must be an object. 画布载荷必须是对象。");
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
        {
            ValidateNodePayload(node, $"{path}.nodes[{index++}]", legacyEnums);
        }

        index = 0;
        foreach (var connection in connections.EnumerateArray())
        {
            ValidateConnectionPayload(connection, $"{path}.connections[{index++}]", legacyEnums);
        }
    }

    private static void ValidateNodePayload(JsonElement payload, string path, bool legacyEnums)
    {
        RequireObject(payload, path, "Node payload must be an object. 节点载荷必须是对象。");
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
            RequireObject(port, $"{path}.ports[{index}]", "Port payload must be an object. 端口载荷必须是对象。");
            if (!legacyEnums)
            {
                EnsureKnownProperties(port, $"{path}.ports[{index}]", ["id", "name", "direction", "required"]);
                var id = ReadRequiredString(port, "id", $"{path}.ports[{index}].id");
                if (!portIds.Add(id))
                {
                    throw Invalid(McpErrorCodes.FlowPatchDuplicateId, $"{path}.ports[{index}].id", "a unique port ID", "Use a unique port ID on the node. 请在节点上使用唯一的端口 ID。");
                }

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
                {
                    throw Invalid(McpErrorCodes.FlowPatchFieldRequired, parameterPath, "parameter UI metadata", "Provide the parameter contract ID in ui.id. 请在 ui.id 中提供参数契约 ID。");
                }

                var id = ReadRequiredString(parameterUi, "id", $"{parameterPath}.id");
                if (!parameterIds.Add(id))
                {
                    throw Invalid(McpErrorCodes.FlowPatchDuplicateId, $"{parameterPath}.id", "a unique parameter ID", "Use a unique parameter ID on the node. 请在节点上使用唯一的参数 ID。");
                }
            }
        }

        var hasScript = payload.TryGetProperty("script", out var script) && script.ValueKind != JsonValueKind.Null;
        if (nodeType == "script")
        {
            if (!hasScript)
            {
                throw Invalid(McpErrorCodes.FlowPatchFieldRequired, $"{path}.script", "a script payload for a script node", "Provide the script contract for a script node. 请为脚本节点提供脚本契约。");
            }

            ValidateScriptPayload(script, $"{path}.script", legacyEnums);
            if (!legacyEnums && !ScriptNodeIdMatches())
            {
                throw Invalid(McpErrorCodes.FlowPatchReferenceInvalid, $"{path}.script.nodeId", $"the node ID '{nodeId}'", "Make script.nodeId match node.id. 请确保 script.nodeId 与 node.id 一致。");
            }

            bool ScriptNodeIdMatches()
            {
                var nodeId = ReadRequiredString(script, "nodeId", $"{path}.script.nodeId");
                return string.Equals(nodeId, nodeId, StringComparison.Ordinal);
            }
        }
        else if (hasScript)
        {
            throw Invalid(McpErrorCodes.FlowPatchUnexpectedField, $"{path}.script", "null for action, flipflop, or flowCall nodes", "Remove the script payload or use a script node type. 请移除脚本载荷，或使用 script 节点类型。");
        }

        if (payload.TryGetProperty("ui", out var ui) && ui.ValueKind != JsonValueKind.Null)
        {
            ValidateNodeUiPayload(ui, $"{path}.ui", legacyEnums, nodeType);
        }
    }

    private static void ValidateParameterPayload(JsonElement payload, string path, bool legacyEnums)
    {
        RequireObject(payload, path, "Parameter payload must be an object. 参数载荷必须是对象。");
        if (!legacyEnums)
        {
            EnsureKnownProperties(payload, path, ["name", "valueJson", "source", "required", "ui"]);
            _ = ReadRequiredString(payload, "name", $"{path}.name");
            RequireNullableString(payload, "valueJson", $"{path}.valueJson");
            RequireBoolean(payload, "required", $"{path}.required");
        }
        ValidateEnum(RequireProperty(payload, "source", $"{path}.source"), $"{path}.source", legacyEnums, DataSources);
        if (payload.TryGetProperty("ui", out var ui) && ui.ValueKind != JsonValueKind.Null)
        {
            ValidateParameterUiPayload(ui, $"{path}.ui", legacyEnums);
        }
    }

    private static void ValidateConnectionPayload(JsonElement payload, string path, bool legacyEnums)
    {
        RequireObject(payload, path, "Connection payload must be an object. 连接载荷必须是对象。");
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
        {
            ValidateEnum(branch, $"{path}.branch", legacyEnums, ExecutionBranches);
        }

        if (payload.TryGetProperty("dataSource", out var source) && source.ValueKind != JsonValueKind.Null)
        {
            ValidateEnum(source, $"{path}.dataSource", legacyEnums, DataSources);
        }
    }

    private static void ValidateRunPolicyPayload(JsonElement payload, string path, bool legacyEnums)
    {
        RequireObject(payload, path, "Run policy payload must be an object. 运行策略载荷必须是对象。");
        if (!legacyEnums)
        {
            EnsureKnownProperties(payload, path, ["concurrencyMode"]);
        }
        ValidateEnum(RequireProperty(payload, "concurrencyMode", $"{path}.concurrencyMode"), $"{path}.concurrencyMode", legacyEnums, ConcurrencyModes);
    }

    private static void ValidateScriptPayload(JsonElement payload, string path, bool legacyEnums)
    {
        RequireObject(payload, path, "Script payload must be an object. 脚本载荷必须是对象。");
        if (!legacyEnums)
        {
            EnsureKnownProperties(payload, path, ["nodeId", "source", "languageVersion", "sourceHash", "inputs", "outputs"]);
            _ = ReadRequiredString(payload, "nodeId", $"{path}.nodeId");
            _ = ReadRequiredString(payload, "source", $"{path}.source");
            _ = ReadRequiredString(payload, "languageVersion", $"{path}.languageVersion");
            _ = ReadOptionalString(payload, "sourceHash", $"{path}.sourceHash");
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
            RequireObject(value, $"{path}[{index}]", "Script value must be an object. 脚本值必须是对象。");
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
        RequireObject(payload, path, "Node UI payload must be an object. 节点 UI 载荷必须是对象。");
        if (!legacyEnums)
        {
            EnsureKnownProperties(payload, path, ["kind", "titleKey", "subtitleKey", "description", "status", "hasDataOutput", "width", "category", "libraryId", "className", "methodName", "dllName", "dllVersion", "returnType", "targetNodeId", "targetFlowId", "isAwaitable", "staticReturnType", "isDynamicReturnType", "targetCanvasId", "isPublic", "flowCallParameterBindings", "libraryNodeContractId", "flowLibraryName"]);
            _ = ReadRequiredString(payload, "kind", $"{path}.kind");
            _ = ReadRequiredString(payload, "titleKey", $"{path}.titleKey");
            _ = ReadRequiredString(payload, "subtitleKey", $"{path}.subtitleKey");
            RequireNullableString(payload, "description", $"{path}.description");
            _ = ReadRequiredString(payload, "status", $"{path}.status");
            RequireBoolean(payload, "hasDataOutput", $"{path}.hasDataOutput");
            RequireNullableFiniteNumber(payload, "width", $"{path}.width");
            var flowCallFields = new[]
            {
                "targetNodeId",
                "targetFlowId",
                "targetCanvasId",
                "isPublic",
                "flowCallParameterBindings"
            };
            // Return type metadata is shared by built-in Script/FlowCall nodes
            // and library nodes. Treating it as library-only rejects the
            // server-provided built-in templates (for example
            // `ScriptLang.Runtime.Value` and `System.Object`) before the patch
            // can be previewed. Keep awaitability library-only because it
            // describes the reflected library method contract, not a built-in
            // node's result type.
            // 返回类型元数据同时用于内置 Script/FlowCall 节点和库节点。若将其
            // 误判为库专属字段，服务端提供的内置模板会在预览前被拒绝。
            var libraryFields = new[]
            {
                "libraryId",
                "className",
                "methodName",
                "dllName",
                "dllVersion",
                "isAwaitable",
                "libraryNodeContractId",
                "flowLibraryName"
            };
            if (nodeType != "flowCall" && HasAnyProperty(payload, flowCallFields))
            {
                throw Invalid(McpErrorCodes.FlowPatchUnexpectedField, path, "flow-call metadata only on a flowCall node", "Remove flow-call metadata or use a flowCall node type. 请移除流程调用元数据，或使用 flowCall 节点类型。");
            }

            if (nodeType is not ("action" or "flipflop") && HasAnyProperty(payload, libraryFields))
            {
                throw Invalid(McpErrorCodes.FlowPatchUnexpectedField, path, "library runtime metadata only on an action or flipflop node", "Remove library metadata or use an action or flipflop node type. 请移除类库元数据，或使用 action 或 flipflop 节点类型。");
            }
        }

        if (payload.TryGetProperty("flowCallParameterBindings", out var bindings) && bindings.ValueKind != JsonValueKind.Null)
        {
            RequireArray(bindings, $"{path}.flowCallParameterBindings");
            var index = 0;
            foreach (var binding in bindings.EnumerateArray())
            {
                RequireObject(binding, $"{path}.flowCallParameterBindings[{index}]", "Flow call binding must be an object. 流程调用绑定必须是对象。");
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
        RequireObject(payload, path, "Parameter UI payload must be an object. 参数 UI 载荷必须是对象。");
        if (!legacyEnums)
        {
            EnsureKnownProperties(payload, path, ["id", "nameKey", "valueKind", "literalValue", "projectInputKey", "expression", "sourceNodeId", "sourcePortId", "type", "description", "inputMode", "isVariadic", "variadicGroupId", "elementType", "variadicMode", "enumMetadata"]);
            _ = ReadRequiredString(payload, "id", $"{path}.id");
            _ = ReadRequiredString(payload, "nameKey", $"{path}.nameKey");
            _ = ReadRequiredString(payload, "valueKind", $"{path}.valueKind");
        }

        if (payload.TryGetProperty("enumMetadata", out var metadata) && metadata.ValueKind != JsonValueKind.Null)
        {
            ValidateEnumMetadata(metadata, $"{path}.enumMetadata", legacyEnums);
        }
    }

    private static void ValidateEnumMetadata(JsonElement payload, string path, bool legacyEnums)
    {
        RequireObject(payload, path, "Enum metadata must be an object. 枚举元数据必须是对象。");
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
            RequireObject(option, $"{path}.options[{index}]", "Enum option must be an object. 枚举选项必须是对象。");
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
            var canonical = allowed.FirstOrDefault(item =>
            {
                return string.Equals(item.Canonical, supplied, StringComparison.Ordinal);
            });
            if (canonical is not null)
            {
                return canonical;
            }

            if (legacy)
            {
                var pascalCase = allowed.FirstOrDefault(item =>
                {
                    return string.Equals(item.LegacyPascalCase, supplied, StringComparison.Ordinal);
                });
                if (pascalCase is not null)
                {
                    return pascalCase;
                }
            }
        }
        else if (legacy && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric) && allowed.FirstOrDefault(item => item.LegacyNumeric == numeric) is { } numericMatch)
        {
            return numericMatch;
        }
        var expected = string.Join(", ", allowed.Select(static item => $"'{item.Canonical}'"));
        throw Invalid(McpErrorCodes.FlowPatchEnumEncodingInvalid, path, expected, $"Use canonical {FlowPatchContract.EnumEncoding} enum strings for schema {FlowPatchContract.CurrentSchemaVersion}. 请按上述架构版本使用规范的枚举字符串编码。");
    }

    private static void RequireNullableEnum(JsonElement source, string name, string path, bool legacyEnums, EnumValue[] allowed)
    {
        var value = RequireProperty(source, name, path);
        if (value.ValueKind != JsonValueKind.Null)
        {
            ValidateEnum(value, path, legacyEnums, allowed);
        }
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
                _ => throw Invalid(McpErrorCodes.FlowPatchOperationUnknown, path, "a known legacy operation number", "Use a schema 2.0 op string. 请使用架构 2.0 的 op 字符串。")
            };
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw Invalid(McpErrorCodes.FlowPatchOperationUnknown, path, "a legacy operation string", "Use a schema 2.0 op string. 请使用架构 2.0 的 op 字符串。");
        }
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
            _ => throw Invalid(McpErrorCodes.FlowPatchOperationUnknown, path, "a supported legacy operation name", "Use a schema 2.0 op string. 请使用架构 2.0 的 op 字符串。")
        };
    }

    private static Guid ReadGuid(JsonElement source, string name, string path)
    {
        var value = RequireProperty(source, name, path);
        if (value.ValueKind == JsonValueKind.String)
        {
            var identifierText = value.GetString();
            if (Guid.TryParse(identifierText, out var parsed) && parsed != Guid.Empty)
            {
                return parsed;
            }
        }

        throw Invalid(McpErrorCodes.FlowPatchFieldInvalid, path, "a non-empty GUID", "Provide the project or flow ID from the read model. 请提供读取模型中的项目或流程 ID。");
    }

    private static long ReadPositiveLong(JsonElement source, string name, string path)
    {
        var value = RequireProperty(source, name, path);
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var parsed) && parsed >= 1)
        {
            return parsed;
        }

        throw Invalid(McpErrorCodes.FlowPatchFieldInvalid, path, "a positive integer", "Use the latest development version from the flow read model. 请使用流程读取模型中的最新开发版本。");
    }

    private static string? ReadOptionalString(JsonElement source, string name, string path)
    {
        if (!source.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        throw Invalid(McpErrorCodes.FlowPatchFieldInvalid, path, "a string or null", "Use a string value for this field. 此字段请使用字符串值。");
    }

    private static string ReadLegacyIdentifier(JsonElement source, string name, string path)
    {
        return ReadRequiredString(source, name, $"{path}.{name}");
    }

    private static string ReadRequiredString(JsonElement source, string name, string path)
    {
        var value = RequireProperty(source, name, path);
        if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return (value.GetString()!).Trim();
        }

        throw Invalid(McpErrorCodes.FlowPatchFieldInvalid, path, "a non-empty string", "Provide the required field value. 请提供必填字段的值。");
    }

    private static void RequireNullableString(JsonElement source, string name, string path)
    {
        var value = RequireProperty(source, name, path);
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.String)
        {
            return;
        }

        throw Invalid(McpErrorCodes.FlowPatchFieldInvalid, path, "a string or null", "Provide a string value or null. 请提供字符串值或 null。");
    }

    private static void RequireBoolean(JsonElement source, string name, string path)
    {
        var value = RequireProperty(source, name, path);
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return;
        }

        throw Invalid(McpErrorCodes.FlowPatchFieldInvalid, path, "a boolean", "Provide true or false. 请提供 true 或 false。");
    }

    private static void RequireInteger(JsonElement source, string name, string path)
    {
        var value = RequireProperty(source, name, path);
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _))
        {
            return;
        }

        throw Invalid(McpErrorCodes.FlowPatchFieldInvalid, path, "an integer", "Provide an integer value. 请提供整数值。");
    }

    private static void RequireFiniteNumber(JsonElement source, string name, string path)
    {
        var value = RequireProperty(source, name, path);
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var numeric) && double.IsFinite(numeric))
        {
            return;
        }

        throw Invalid(McpErrorCodes.FlowPatchFieldInvalid, path, "a finite number", "Provide a finite numeric value. 请提供有限数值。");
    }

    private static void RequireNullableFiniteNumber(JsonElement source, string name, string path)
    {
        var value = RequireProperty(source, name, path);
        if (value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var numeric) && double.IsFinite(numeric))
        {
            return;
        }

        throw Invalid(McpErrorCodes.FlowPatchFieldInvalid, path, "a finite number or null", "Provide a finite numeric value or null. 请提供有限数值或 null。");
    }

    private static bool HasAnyProperty(JsonElement source, IReadOnlyCollection<string> names)
    {
        return source.EnumerateObject()
            .Any(property =>
            {
                return names.Contains(property.Name, StringComparer.Ordinal) && property.Value.ValueKind != JsonValueKind.Null;
            });
    }

    private static string Required(string? value, string path)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        throw Invalid(McpErrorCodes.FlowPatchFieldRequired, path, "a non-empty string", "Provide the required field value. 请提供必填字段的值。");
    }

    private static JsonElement RequireProperty(JsonElement source, string name, string path)
    {
        if (source.TryGetProperty(name, out var value))
        {
            return value;
        }

        throw Invalid(McpErrorCodes.FlowPatchFieldRequired, path, $"required property '{name}'", "Provide the required field. 请提供必填字段。");
    }

    private static void EnsureKnownProperties(JsonElement source, string path, IReadOnlyCollection<string> allowed)
    {
        foreach (var property in source.EnumerateObject())
        {
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
            {
                var expected = string.Join(", ", allowed.Select(static item => $"'{item}'"));
                throw Invalid(McpErrorCodes.FlowPatchUnexpectedField, $"{path}.{property.Name}", expected, "Remove fields that are not defined by this operation schema. 请移除此操作架构未定义的字段。");
            }
        }
    }

    private static void RequireObject(JsonElement value, string path, string message)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw Invalid(McpErrorCodes.FlowPatchPayloadInvalid, path, "an object", message);
        }
    }

    private static void RequireArray(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw Invalid(McpErrorCodes.FlowPatchPayloadInvalid, path, "an array", "Provide an array value. 请提供数组值。");
        }
    }

    private static FlowPatchContractException Invalid(string code, string fieldPath, string expected, string remediation)
    {
        return new(code, fieldPath, expected, remediation);
    }

    private sealed class PatchReferenceState
    {
        private readonly Dictionary<string, CanvasReferenceState> _canvases;
        private PatchReferenceState(Dictionary<string, CanvasReferenceState> canvases)
        {
            _canvases = canvases;
        }

        public static PatchReferenceState FromDefinition(FlowDefinitionDto definition)
        {
            var canvases = new Dictionary<string, CanvasReferenceState>(StringComparer.Ordinal);
            foreach (var canvas in definition.Canvases)
            {
                if (!TryAddCurrentCanvas())
                {
                    throw Invalid(McpErrorCodes.FlowPatchDuplicateId, "$.current.canvases", "unique canvas IDs", "Repair the current flow before submitting a patch. 请先修复当前流程，再提交补丁。");
                }

                bool TryAddCurrentCanvas()
                {
                    var fromCanvas = CanvasReferenceState.FromCanvas(canvas, "$.current.canvases");
                    return canvases.TryAdd(canvas.Id, fromCanvas);
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
                        if (!TryAddCanvas())
                        {
                            throw Duplicate($"{path}.canvas.id", "canvas ID");
                        }

                        ValidateConnections(_canvases[canvas.Id], $"{path}.canvas.connections");
                        return;
                        bool TryAddCanvas()
                        {
                            var fromCanvas = CanvasReferenceState.FromCanvas(canvas, $"{path}.canvas");
                            return _canvases.TryAdd(canvas.Id, fromCanvas);
                        }
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
                            throw Invalid(McpErrorCodes.FlowPatchReferenceInvalid, $"{path}.canvasId", "an empty canvas", "Remove the canvas connections and nodes before removing the canvas. 请先移除画布上的连接和节点，再移除画布。");
                        }

                        _canvases.Remove(canvasId);
                        return;
                    }

                case "addNode":
                    {
                        var requireId = RequireId(operation.CanvasId, $"{path}.canvasId");
                        var canvas = RequireCanvas(requireId, $"{path}.canvasId");
                        var node = RequireValue(operation.Node, $"{path}.node");
                        if (!TryAddNode())
                        {
                            throw Duplicate($"{path}.node.id", "node ID");
                        }

                        return;
                        bool TryAddNode()
                        {
                            var fromNode = NodeReferenceState.FromNode(node, $"{path}.node");
                            return canvas.Nodes.TryAdd(node.Id, fromNode);
                        }
                    }

                case "replaceNode":
                    {
                        var mappedCanvasId = RequireId(operation.CanvasId, $"{path}.canvasId");
                        var canvas = RequireCanvas(mappedCanvasId, $"{path}.canvasId");
                        var nodeId = RequireId(operation.NodeId, $"{path}.nodeId");
                        var node = RequireValue(operation.Node, $"{path}.node");
                        RequireMatchingId(nodeId, node.Id, $"{path}.node.id");
                        if (!canvas.Nodes.ContainsKey(nodeId))
                        {
                            throw ReferenceInvalid($"{path}.nodeId", "an existing node ID");
                        }

                        canvas.Nodes[nodeId] = NodeReferenceState.FromNode(node, $"{path}.node");
                        ValidateConnections(canvas, $"{path}.connections");
                        return;
                    }

                case "removeNode":
                    {
                        var resolvedCanvasId = RequireId(operation.CanvasId, $"{path}.canvasId");
                        var canvas = RequireCanvas(resolvedCanvasId, $"{path}.canvasId");
                        var nodeId = RequireId(operation.NodeId, $"{path}.nodeId");
                        if (!canvas.Nodes.ContainsKey(nodeId))
                        {
                            throw ReferenceInvalid($"{path}.nodeId", "an existing node ID");
                        }

                        if (canvas.Connections.Values.Any(connection => connection.FromNodeId == nodeId || connection.ToNodeId == nodeId))
                        {
                            throw Invalid(McpErrorCodes.FlowPatchReferenceInvalid, $"{path}.nodeId", "a node without connections", "Remove the node connections before removing the node. 请先移除节点的连接，再移除节点。");
                        }

                        canvas.Nodes.Remove(nodeId);
                        return;
                    }

                case "setNodeParameter":
                    {
                        var validatedCanvasId = RequireId(operation.CanvasId, $"{path}.canvasId");
                        var canvas = RequireCanvas(validatedCanvasId, $"{path}.canvasId");
                        var node = RequireNode(canvas, RequireId(operation.NodeId, $"{path}.nodeId"), $"{path}.nodeId");
                        var parameterId = RequireId(operation.ParameterId, $"{path}.parameterId");
                        var actualParameterId = RequireId(RequireValue(operation.Parameter, $"{path}.parameter").Ui?.Id, $"{path}.parameter.ui.id");
                        RequireMatchingId(parameterId, actualParameterId, $"{path}.parameter.ui.id");
                        if (!node.ParameterIds.Contains(parameterId))
                        {
                            throw ReferenceInvalid($"{path}.parameterId", "an existing parameter ID on the node");
                        }

                        return;
                    }

                case "addNodeParameter":
                    {
                        var validatedCanvasId = RequireId(operation.CanvasId, $"{path}.canvasId");
                        var canvas = RequireCanvas(validatedCanvasId, $"{path}.canvasId");
                        var node = RequireNode(canvas, RequireId(operation.NodeId, $"{path}.nodeId"), $"{path}.nodeId");
                        var parameterId = RequireId(RequireValue(operation.Parameter, $"{path}.parameter").Ui?.Id, $"{path}.parameter.ui.id");
                        if (!node.ParameterIds.Add(parameterId))
                        {
                            throw Duplicate($"{path}.parameter.ui.id", "parameter ID");
                        }

                        return;
                    }

                case "removeNodeParameter":
                    {
                        var validatedCanvasId = RequireId(operation.CanvasId, $"{path}.canvasId");
                        var canvas = RequireCanvas(validatedCanvasId, $"{path}.canvasId");
                        var nodeId = RequireId(operation.NodeId, $"{path}.nodeId");
                        var node = RequireNode(canvas, nodeId, $"{path}.nodeId");
                        var parameterId = RequireId(operation.ParameterId, $"{path}.parameterId");
                        if (!node.ParameterIds.Contains(parameterId))
                        {
                            throw ReferenceInvalid($"{path}.parameterId", "an existing parameter ID on the node");
                        }

                        if (canvas.Connections.Values.Any(connection => connection.Kind == ConnectionKindDto.Data && connection.ToNodeId == nodeId && connection.ToPortId == parameterId))
                        {
                            throw Invalid(McpErrorCodes.FlowPatchReferenceInvalid, $"{path}.parameterId", "a parameter without incoming data connections", "Remove the parameter's data connections before removing the parameter. 请先移除参数的数据连接，再移除参数。");
                        }

                        node.ParameterIds.Remove(parameterId);
                        return;
                    }

                case "addConnection":
                    {
                        var validatedCanvasId = RequireId(operation.CanvasId, $"{path}.canvasId");
                        var canvas = RequireCanvas(validatedCanvasId, $"{path}.canvasId");
                        var connection = RequireValue(operation.Connection, $"{path}.connection");
                        if (canvas.Connections.ContainsKey(connection.Id))
                        {
                            throw Duplicate($"{path}.connection.id", "connection ID");
                        }

                        ValidateConnectionEndpoints(canvas, connection, $"{path}.connection");
                        canvas.Connections.Add(connection.Id, connection);
                        return;
                    }

                case "replaceConnection":
                    {
                        var validatedCanvasId = RequireId(operation.CanvasId, $"{path}.canvasId");
                        var canvas = RequireCanvas(validatedCanvasId, $"{path}.canvasId");
                        var connectionId = RequireId(operation.ConnectionId, $"{path}.connectionId");
                        var connection = RequireValue(operation.Connection, $"{path}.connection");
                        RequireMatchingId(connectionId, connection.Id, $"{path}.connection.id");
                        if (!canvas.Connections.ContainsKey(connectionId))
                        {
                            throw ReferenceInvalid($"{path}.connectionId", "an existing connection ID");
                        }

                        ValidateConnectionEndpoints(canvas, connection, $"{path}.connection");
                        canvas.Connections[connectionId] = connection;
                        return;
                    }

                case "removeConnection":
                    {
                        var validatedCanvasId = RequireId(operation.CanvasId, $"{path}.canvasId");
                        var canvas = RequireCanvas(validatedCanvasId, $"{path}.canvasId");
                        var connectionId = RequireId(operation.ConnectionId, $"{path}.connectionId");
                        if (!canvas.Connections.Remove(connectionId))
                        {
                            throw ReferenceInvalid($"{path}.connectionId", "an existing connection ID");
                        }

                        return;
                    }

                case "setEntryNode":
                    {
                        var entryNodeId = RequireId(operation.EntryNodeId, $"{path}.entryNodeId");
                        if (!_canvases.Values.Any(canvas =>
                        {
                            return canvas.Nodes.ContainsKey(entryNodeId);
                        }))
                        {
                            throw ReferenceInvalid($"{path}.entryNodeId", "an existing node ID");
                        }

                        return;
                    }

                case "setRunPolicy":
                    _ = RequireValue(operation.RunPolicy, $"{path}.runPolicy");
                    return;
                case "replaceScriptSource":
                    {
                        var validatedCanvasId = RequireId(operation.CanvasId, $"{path}.canvasId");
                        var canvas = RequireCanvas(validatedCanvasId, $"{path}.canvasId");
                        var node = RequireNode(canvas, RequireId(operation.NodeId, $"{path}.nodeId"), $"{path}.nodeId");
                        if (!node.HasScript)
                        {
                            throw Invalid(McpErrorCodes.FlowPatchReferenceInvalid, $"{path}.nodeId", "an existing script node", "Use replaceScriptSource only with a script node. replaceScriptSource 仅适用于 script 节点。");
                        }

                        return;
                    }

                default:
                    throw Invalid(McpErrorCodes.FlowPatchOperationUnknown, $"{path}.op", "one of the 15 schema 2.0 operation names", "Use the operation names exposed by sereinflow_preview_flow_patch. 请使用 sereinflow_preview_flow_patch 公开的操作名称。");
            }
        }

        private CanvasReferenceState RequireCanvas(string canvasId, string path)
        {
            return _canvases.TryGetValue(canvasId, out var canvas) ? canvas : throw ReferenceInvalid(path, "an existing canvas ID");
        }

        private static NodeReferenceState RequireNode(CanvasReferenceState canvas, string nodeId, string path)
        {
            return canvas.Nodes.TryGetValue(nodeId, out var node) ? node : throw ReferenceInvalid(path, "an existing node ID");
        }

        private static void ValidateConnections(CanvasReferenceState canvas, string path)
        {
            foreach (var connection in canvas.Connections.Values)
            {
                ValidateConnectionEndpoints(canvas, connection, path);
            }
        }

        private static void ValidateConnectionEndpoints(CanvasReferenceState canvas, ConnectionDto connection, string path)
        {
            var fromNode = RequireNode(canvas, connection.FromNodeId, $"{path}.fromNodeId");
            if (!fromNode.PortIds.Contains(connection.FromPortId))
            {
                throw ReferenceInvalid($"{path}.fromPortId", "a port on fromNodeId");
            }

            var toNode = RequireNode(canvas, connection.ToNodeId, $"{path}.toNodeId");
            if (connection.Kind == ConnectionKindDto.Data)
            {
                if (!toNode.ParameterIds.Contains(connection.ToPortId))
                {
                    throw ReferenceInvalid($"{path}.toPortId", "a parameter ID on toNodeId");
                }

                return;
            }

            if (!toNode.PortIds.Contains(connection.ToPortId))
            {
                throw ReferenceInvalid($"{path}.toPortId", "a port on toNodeId");
            }
        }

        private static T RequireValue<T>(T? value, string path)
            where T : class
        {
            return value ?? throw Invalid(McpErrorCodes.FlowPatchFieldRequired, path, "the operation payload", "Provide the payload required by the selected operation. 请提供所选操作要求的载荷。");
        }

        private static string RequireId(string? value, string path)
        {
            return string.IsNullOrWhiteSpace(value) ? throw Invalid(McpErrorCodes.FlowPatchFieldRequired, path, "a non-empty ID", "Provide an ID from the flow edit model. 请提供流程编辑模型中的 ID。") : value.Trim();
        }

        private static void RequireMatchingId(string expected, string actual, string path)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw Invalid(McpErrorCodes.FlowPatchReferenceInvalid, path, $"the ID '{expected}'", "Make the payload ID match the operation target ID. 请确保载荷 ID 与操作目标 ID 一致。");
            }
        }

        private static FlowPatchContractException Duplicate(string path, string kind)
        {
            return Invalid(McpErrorCodes.FlowPatchDuplicateId, path, $"a unique {kind}", "Use an ID that is not already present in the target canvas. 请使用目标画布中尚不存在的 ID。");
        }

        private static FlowPatchContractException ReferenceInvalid(string path, string expected)
        {
            return Invalid(McpErrorCodes.FlowPatchReferenceInvalid, path, expected, "Read the current flow edit model and use an existing ID and port. 请读取当前流程编辑模型，并使用已有的 ID 和端口。");
        }

        private sealed class CanvasReferenceState
        {
            public Dictionary<string, NodeReferenceState> Nodes { get; }
            public Dictionary<string, ConnectionDto> Connections { get; }

            private CanvasReferenceState(Dictionary<string, NodeReferenceState> nodes, Dictionary<string, ConnectionDto> connections)
            {
                Nodes = nodes;
                Connections = connections;
            }

            public static CanvasReferenceState FromCanvas(CanvasDto canvas, string path)
            {
                var nodes = new Dictionary<string, NodeReferenceState>(StringComparer.Ordinal);
                foreach (var node in canvas.Nodes)
                {
                    if (!TryAddCanvasNode())
                    {
                        throw Duplicate($"{path}.nodes", "node ID");
                    }

                    bool TryAddCanvasNode()
                    {
                        var fromNode = NodeReferenceState.FromNode(node, $"{path}.nodes");
                        return nodes.TryAdd(node.Id, fromNode);
                    }
                }

                var connections = new Dictionary<string, ConnectionDto>(StringComparer.Ordinal);
                foreach (var connection in canvas.Connections)
                {
                    if (!connections.TryAdd(connection.Id, connection))
                    {
                        throw Duplicate($"{path}.connections", "connection ID");
                    }
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
                    {
                        throw Duplicate($"{path}.ports", "port ID");
                    }
                }

                var parameters = new HashSet<string>(StringComparer.Ordinal);
                foreach (var parameter in node.Parameters)
                {
                    var parameterId = RequireId(parameter.Ui?.Id, $"{path}.parameters.ui.id");
                    if (!parameters.Add(parameterId))
                    {
                        throw Duplicate($"{path}.parameters.ui.id", "parameter ID");
                    }
                }
                return new NodeReferenceState(ports, parameters, node.Script is not null);
            }
        }
    }

    private sealed record EnumValue(string Canonical, string LegacyPascalCase, int LegacyNumeric);
    private static readonly EnumValue[] NodeTypes = [new("action", "Action", 0), new("flipflop", "Flipflop", 1), new("script", "Script", 2), new("flowCall", "FlowCall", 4)];
    private static readonly EnumValue[] CanvasLifecycles = [new("main", "Main", 0), new("init", "Init", 1), new("loading", "Loading", 2), new("exit", "Exit", 3), new("custom", "Custom", 4)];
    private static readonly EnumValue[] ConnectionKinds = [new("execution", "Execution", 0), new("data", "Data", 1)];
    private static readonly EnumValue[] ExecutionBranches = [new("success", "Success", 0), new("failure", "Failure", 1), new("error", "Error", 2)];
    private static readonly EnumValue[] DataSources = [new("literal", "Literal", 0), new("previousNode", "PreviousNode", 1), new("projectInput", "ProjectInput", 2), new("expression", "Expression", 3)];
    private static readonly EnumValue[] ConcurrencyModes = [new("parallel", "Parallel", 0), new("exclusiveReject", "ExclusiveReject", 1)];
}

/// <summary>
/// Carries request data for normalized flow patch operations.
/// 承载规范化流程Patch操作的请求数据。
/// </summary>
public sealed record NormalizedFlowPatchRequest(FlowPatchCanonicalRequestDto Request, FlowPatchRequestDto LegacyRequest, IReadOnlyList<FlowPatchNormalizationWarningDto> Warnings);
public sealed class FlowPatchContractException(string code, string fieldPath, string expected, string remediation) : Exception("The flow patch contract is invalid. 流程补丁契约无效。")
{
    public string Code { get; } = code;
    public string FieldPath { get; } = fieldPath;
    public string Expected { get; } = expected;
    public string Remediation { get; } = remediation;
    public string DiagnosticId { get; } = CreateDiagnosticIdInitialValue();

    private static string CreateDiagnosticIdInitialValue()
    {
        return Guid.NewGuid().ToString("N");
    }
}

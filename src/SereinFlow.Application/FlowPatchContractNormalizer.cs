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

        var getArrayLengthResult = operationElement.GetArrayLength();
        var operations = new List<FlowPatchCanonicalOperationDto>(getArrayLengthResult);
        var index = 0;
        foreach (var operation in operationElement.EnumerateArray())
        {
            var path = $"$.operations[{index}]";
            FlowPatchCanonicalOperationDto evaluatedValue = isV2 ? ReadV2Operation(operation, path) : ReadV1Operation(operation, path);
            operations.Add(evaluatedValue);
            index++;
        }

        var canonical = new FlowPatchCanonicalRequestDto(projectId, flowId, expectedVersion, schemaVersion, operations, remark);
        var toLegacyRequestResult = ToLegacyRequest(canonical);
        var normalizedFlowPatchRequest = new NormalizedFlowPatchRequest(canonical, toLegacyRequestResult, warnings);
        return normalizedFlowPatchRequest;
    }

    public NormalizedFlowPatchRequest NormalizeLegacyRequest(FlowPatchRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var operations = request.Operations
            .Select((operation, index) =>
            {
                var readPersistedLegacyOperationResult = ReadPersistedLegacyOperation(operation, $"$.operations[{index}]");
                return readPersistedLegacyOperationResult;
            })
            .ToArray();
        var canonical = new FlowPatchCanonicalRequestDto(request.ProjectId, request.FlowId, request.ExpectedDevelopmentVersion, FlowPatchContract.CurrentSchemaVersion, operations, request.Remark);
        IReadOnlyList<FlowPatchNormalizationWarningDto> evaluatedValue1 = [new FlowPatchNormalizationWarningDto(McpErrorCodes.FlowPatchLegacyInput, "$.operations", "The stored preview uses the legacy flow-patch representation and was normalized to schema 2.0. 已存储的预览使用旧版流程补丁表示，现已规范化为架构 2.0。")];
        var normalizedFlowPatchRequest2 = new NormalizedFlowPatchRequest(canonical, request, evaluatedValue1);
        return normalizedFlowPatchRequest2;
    }

    public IReadOnlyList<FlowPatchCanonicalOperationDto> NormalizeLegacyOperations(IReadOnlyList<FlowPatchOperationDto> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var toArrayResult = operations
            .Select((operation, index) =>
            {
                var readPersistedLegacyOperationResult3 = ReadPersistedLegacyOperation(operation, $"$.operations[{index}]");
                return readPersistedLegacyOperationResult3;
            })
            .ToArray();
        return toArrayResult;
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
        var toArrayResult6 = request.Operations
            .Select(ToLegacyOperation)
            .ToArray();
        FlowPatchRequestDto evaluatedValue7 = new(request.ProjectId, request.FlowId, request.ExpectedDevelopmentVersion, toArrayResult6, request.Remark);
        return evaluatedValue7;
    }

    public static FlowPatchOperationDto ToLegacyOperation(FlowPatchCanonicalOperationDto operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        FlowPatchOperationDto evaluatedValue8 = operation.Op switch
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
        return evaluatedValue8;
    }

    private static FlowPatchOperationDto Legacy(FlowPatchOperationKindDto operation, string? canvasId = null, string? nodeId = null, string? connectionId = null, string? parameterId = null, object? value = null)
    {
        JsonElement? evaluatedValue9 = value is null ? null : JsonSerializer.SerializeToElement(value, JsonOptions);
        FlowPatchOperationDto evaluatedValue10 = new(operation, canvasId, nodeId, connectionId, parameterId, evaluatedValue9);
        return evaluatedValue10;
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
        FlowPatchCanonicalOperationDto evaluatedValue43 = op switch
        {
            "addCanvas" => resolveevaluatedValue(),
            "updateCanvas" => resolveevaluatedValue12(),
            "removeCanvas" => ReadV2RemoveCanvas(operation, path, op),
            "addNode" => resolveevaluatedValue15(),
            "replaceNode" => resolveevaluatedValue18(),
            "removeNode" => ReadV2RemoveNode(operation, path, op),
            "setNodeParameter" => resolveevaluatedValue23(),
            "addNodeParameter" => resolveevaluatedValue28(),
            "removeNodeParameter" => ReadV2RemoveNodeParameter(operation, path, op),
            "addConnection" => resolveevaluatedValue33(),
            "replaceConnection" => resolveevaluatedValue36(),
            "removeConnection" => ReadV2RemoveConnection(operation, path, op),
            "setEntryNode" => ReadV2SetEntryNode(operation, path, op),
            "setRunPolicy" => resolveevaluatedValue41(),
            "replaceScriptSource" => ReadV2ReplaceScriptSource(operation, path, op),
            _ => throw Invalid(McpErrorCodes.FlowPatchOperationUnknown, $"{path}.op", "one of the 15 schema 2.0 operation names", "Use the operation names exposed by sereinflow_preview_flow_patch. 请使用 sereinflow_preview_flow_patch 公开的操作名称。")
        };
        return evaluatedValue43;
        FlowPatchCanonicalOperationDto resolveevaluatedValue()
        {
            var readCanvasResult = ReadCanvas(operation, "canvas", path, legacyEnums: false, ["op", "canvas"]);
            FlowPatchCanonicalOperationDto evaluatedValue11 = new(op, Canvas: readCanvasResult);
            return evaluatedValue11;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue12()
        {
            var readRequiredStringResult = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
            var readCanvasResult13 = ReadCanvas(operation, "canvas", path, legacyEnums: false, ["op", "canvasId", "canvas"]);
            FlowPatchCanonicalOperationDto evaluatedValue14 = new(op, readRequiredStringResult, Canvas: readCanvasResult13);
            return evaluatedValue14;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue15()
        {
            var readRequiredStringResult16 = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
            var readNodeResult = ReadNode(operation, "node", path, legacyEnums: false, ["op", "canvasId", "node"]);
            FlowPatchCanonicalOperationDto evaluatedValue17 = new(op, readRequiredStringResult16, Node: readNodeResult);
            return evaluatedValue17;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue18()
        {
            var readRequiredStringResult19 = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
            var readRequiredStringResult20 = ReadRequiredString(operation, "nodeId", $"{path}.nodeId");
            var readNodeResult21 = ReadNode(operation, "node", path, legacyEnums: false, ["op", "canvasId", "nodeId", "node"]);
            FlowPatchCanonicalOperationDto evaluatedValue22 = new(op, readRequiredStringResult19, readRequiredStringResult20, Node: readNodeResult21);
            return evaluatedValue22;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue23()
        {
            var readRequiredStringResult24 = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
            var readRequiredStringResult25 = ReadRequiredString(operation, "nodeId", $"{path}.nodeId");
            var readRequiredStringResult26 = ReadRequiredString(operation, "parameterId", $"{path}.parameterId");
            var readParameterResult = ReadParameter(operation, "parameter", path, legacyEnums: false, ["op", "canvasId", "nodeId", "parameterId", "parameter"]);
            FlowPatchCanonicalOperationDto evaluatedValue27 = new(op, readRequiredStringResult24, readRequiredStringResult25, ParameterId: readRequiredStringResult26, Parameter: readParameterResult);
            return evaluatedValue27;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue28()
        {
            var readRequiredStringResult29 = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
            var readRequiredStringResult30 = ReadRequiredString(operation, "nodeId", $"{path}.nodeId");
            var readParameterResult31 = ReadParameter(operation, "parameter", path, legacyEnums: false, ["op", "canvasId", "nodeId", "parameter"]);
            FlowPatchCanonicalOperationDto evaluatedValue32 = new(op, readRequiredStringResult29, readRequiredStringResult30, Parameter: readParameterResult31);
            return evaluatedValue32;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue33()
        {
            var readRequiredStringResult34 = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
            var readConnectionResult = ReadConnection(operation, "connection", path, legacyEnums: false, ["op", "canvasId", "connection"]);
            FlowPatchCanonicalOperationDto evaluatedValue35 = new(op, readRequiredStringResult34, Connection: readConnectionResult);
            return evaluatedValue35;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue36()
        {
            var readRequiredStringResult37 = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
            var readRequiredStringResult38 = ReadRequiredString(operation, "connectionId", $"{path}.connectionId");
            var readConnectionResult39 = ReadConnection(operation, "connection", path, legacyEnums: false, ["op", "canvasId", "connectionId", "connection"]);
            FlowPatchCanonicalOperationDto evaluatedValue40 = new(op, readRequiredStringResult37, ConnectionId: readRequiredStringResult38, Connection: readConnectionResult39);
            return evaluatedValue40;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue41()
        {
            var readRunPolicyResult = ReadRunPolicy(operation, "runPolicy", path, legacyEnums: false, ["op", "runPolicy"]);
            FlowPatchCanonicalOperationDto evaluatedValue42 = new(op, RunPolicy: readRunPolicyResult);
            return evaluatedValue42;
        }
    }

    private static FlowPatchCanonicalOperationDto ReadV2RemoveCanvas(JsonElement operation, string path, string op)
    {
        EnsureKnownProperties(operation, path, ["op", "canvasId"]);
        var readRequiredStringResult44 = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
        FlowPatchCanonicalOperationDto evaluatedValue45 = new(op, readRequiredStringResult44);
        return evaluatedValue45;
    }

    private static FlowPatchCanonicalOperationDto ReadV2RemoveNode(JsonElement operation, string path, string op)
    {
        EnsureKnownProperties(operation, path, ["op", "canvasId", "nodeId"]);
        var readRequiredStringResult46 = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
        var readRequiredStringResult47 = ReadRequiredString(operation, "nodeId", $"{path}.nodeId");
        FlowPatchCanonicalOperationDto evaluatedValue48 = new(op, readRequiredStringResult46, readRequiredStringResult47);
        return evaluatedValue48;
    }

    private static FlowPatchCanonicalOperationDto ReadV2RemoveNodeParameter(JsonElement operation, string path, string op)
    {
        EnsureKnownProperties(operation, path, ["op", "canvasId", "nodeId", "parameterId"]);
        var readRequiredStringResult49 = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
        var readRequiredStringResult50 = ReadRequiredString(operation, "nodeId", $"{path}.nodeId");
        var readRequiredStringResult51 = ReadRequiredString(operation, "parameterId", $"{path}.parameterId");
        FlowPatchCanonicalOperationDto evaluatedValue52 = new(op, readRequiredStringResult49, readRequiredStringResult50, ParameterId: readRequiredStringResult51);
        return evaluatedValue52;
    }

    private static FlowPatchCanonicalOperationDto ReadV2RemoveConnection(JsonElement operation, string path, string op)
    {
        EnsureKnownProperties(operation, path, ["op", "canvasId", "connectionId"]);
        var readRequiredStringResult53 = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
        var readRequiredStringResult54 = ReadRequiredString(operation, "connectionId", $"{path}.connectionId");
        FlowPatchCanonicalOperationDto evaluatedValue55 = new(op, readRequiredStringResult53, ConnectionId: readRequiredStringResult54);
        return evaluatedValue55;
    }

    private static FlowPatchCanonicalOperationDto ReadV2SetEntryNode(JsonElement operation, string path, string op)
    {
        EnsureKnownProperties(operation, path, ["op", "entryNodeId"]);
        var readRequiredStringResult56 = ReadRequiredString(operation, "entryNodeId", $"{path}.entryNodeId");
        FlowPatchCanonicalOperationDto evaluatedValue57 = new(op, EntryNodeId: readRequiredStringResult56);
        return evaluatedValue57;
    }

    private static FlowPatchCanonicalOperationDto ReadV2ReplaceScriptSource(JsonElement operation, string path, string op)
    {
        EnsureKnownProperties(operation, path, ["op", "canvasId", "nodeId", "source"]);
        var readRequiredStringResult58 = ReadRequiredString(operation, "canvasId", $"{path}.canvasId");
        var readRequiredStringResult59 = ReadRequiredString(operation, "nodeId", $"{path}.nodeId");
        var readRequiredStringResult60 = ReadRequiredString(operation, "source", $"{path}.source");
        FlowPatchCanonicalOperationDto evaluatedValue61 = new(op, readRequiredStringResult58, readRequiredStringResult59, Source: readRequiredStringResult60);
        return evaluatedValue61;
    }

    private static FlowPatchCanonicalOperationDto ReadV1Operation(JsonElement operation, string path)
    {
        RequireObject(operation, path, "Each operation must be an object. 每项操作必须是对象。");
        var requirePropertyResult = RequireProperty(operation, "operation", $"{path}.operation");
        var op = ReadLegacyOperationName(requirePropertyResult, $"{path}.operation");
        FlowPatchCanonicalOperationDto evaluatedValue124 = op switch
        {
            "addCanvas" => resolveevaluatedValue62(),
            "updateCanvas" => resolveevaluatedValue64(),
            "removeCanvas" => resolveevaluatedValue67(),
            "addNode" => resolveevaluatedValue70(),
            "replaceNode" => resolveevaluatedValue74(),
            "removeNode" => resolveevaluatedValue79(),
            "setNodeParameter" => resolveevaluatedValue83(),
            "addNodeParameter" => resolveevaluatedValue89(),
            "removeNodeParameter" => resolveevaluatedValue94(),
            "addConnection" => resolveevaluatedValue99(),
            "replaceConnection" => resolveevaluatedValue103(),
            "removeConnection" => resolveevaluatedValue109(),
            "setEntryNode" => resolveevaluatedValue113(),
            "setRunPolicy" => resolveevaluatedValue116(),
            "replaceScriptSource" => resolveevaluatedValue119(),
            _ => throw Invalid(McpErrorCodes.FlowPatchOperationUnknown, $"{path}.operation", "a supported legacy operation name", "Use schemaVersion 2.0 and the canonical op names. 请使用 schemaVersion 2.0 和规范的 op 名称。")
        };
        return evaluatedValue124;
        FlowPatchCanonicalOperationDto resolveevaluatedValue62()
        {
            var readLegacyPayloadResult = ReadLegacyPayload<CanvasDto>(operation, path, legacyEnums: true);
            FlowPatchCanonicalOperationDto evaluatedValue63 = new(op, Canvas: readLegacyPayloadResult);
            return evaluatedValue63;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue64()
        {
            var readLegacyIdentifierResult = ReadLegacyIdentifier(operation, "canvasId", path);
            var readLegacyPayloadResult65 = ReadLegacyPayload<CanvasDto>(operation, path, legacyEnums: true);
            FlowPatchCanonicalOperationDto evaluatedValue66 = new(op, readLegacyIdentifierResult, Canvas: readLegacyPayloadResult65);
            return evaluatedValue66;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue67()
        {
            var readLegacyIdentifierResult68 = ReadLegacyIdentifier(operation, "canvasId", path);
            FlowPatchCanonicalOperationDto evaluatedValue69 = new(op, readLegacyIdentifierResult68);
            return evaluatedValue69;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue70()
        {
            var readLegacyIdentifierResult71 = ReadLegacyIdentifier(operation, "canvasId", path);
            var readLegacyPayloadResult72 = ReadLegacyPayload<NodeDto>(operation, path, legacyEnums: true);
            FlowPatchCanonicalOperationDto evaluatedValue73 = new(op, readLegacyIdentifierResult71, Node: readLegacyPayloadResult72);
            return evaluatedValue73;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue74()
        {
            var readLegacyIdentifierResult75 = ReadLegacyIdentifier(operation, "canvasId", path);
            var readLegacyIdentifierResult76 = ReadLegacyIdentifier(operation, "nodeId", path);
            var readLegacyPayloadResult77 = ReadLegacyPayload<NodeDto>(operation, path, legacyEnums: true);
            FlowPatchCanonicalOperationDto evaluatedValue78 = new(op, readLegacyIdentifierResult75, readLegacyIdentifierResult76, Node: readLegacyPayloadResult77);
            return evaluatedValue78;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue79()
        {
            var readLegacyIdentifierResult80 = ReadLegacyIdentifier(operation, "canvasId", path);
            var readLegacyIdentifierResult81 = ReadLegacyIdentifier(operation, "nodeId", path);
            FlowPatchCanonicalOperationDto evaluatedValue82 = new(op, readLegacyIdentifierResult80, readLegacyIdentifierResult81);
            return evaluatedValue82;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue83()
        {
            var readLegacyIdentifierResult84 = ReadLegacyIdentifier(operation, "canvasId", path);
            var readLegacyIdentifierResult85 = ReadLegacyIdentifier(operation, "nodeId", path);
            var readLegacyIdentifierResult86 = ReadLegacyIdentifier(operation, "parameterId", path);
            var readLegacyPayloadResult87 = ReadLegacyPayload<NodeParameterDto>(operation, path, legacyEnums: true);
            FlowPatchCanonicalOperationDto evaluatedValue88 = new(op, readLegacyIdentifierResult84, readLegacyIdentifierResult85, ParameterId: readLegacyIdentifierResult86, Parameter: readLegacyPayloadResult87);
            return evaluatedValue88;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue89()
        {
            var readLegacyIdentifierResult90 = ReadLegacyIdentifier(operation, "canvasId", path);
            var readLegacyIdentifierResult91 = ReadLegacyIdentifier(operation, "nodeId", path);
            var readLegacyPayloadResult92 = ReadLegacyPayload<NodeParameterDto>(operation, path, legacyEnums: true);
            FlowPatchCanonicalOperationDto evaluatedValue93 = new(op, readLegacyIdentifierResult90, readLegacyIdentifierResult91, Parameter: readLegacyPayloadResult92);
            return evaluatedValue93;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue94()
        {
            var readLegacyIdentifierResult95 = ReadLegacyIdentifier(operation, "canvasId", path);
            var readLegacyIdentifierResult96 = ReadLegacyIdentifier(operation, "nodeId", path);
            var readLegacyIdentifierResult97 = ReadLegacyIdentifier(operation, "parameterId", path);
            FlowPatchCanonicalOperationDto evaluatedValue98 = new(op, readLegacyIdentifierResult95, readLegacyIdentifierResult96, ParameterId: readLegacyIdentifierResult97);
            return evaluatedValue98;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue99()
        {
            var readLegacyIdentifierResult100 = ReadLegacyIdentifier(operation, "canvasId", path);
            var readLegacyPayloadResult101 = ReadLegacyPayload<ConnectionDto>(operation, path, legacyEnums: true);
            var normalizeConnectionResult = NormalizeConnection(readLegacyPayloadResult101);
            FlowPatchCanonicalOperationDto evaluatedValue102 = new(op, readLegacyIdentifierResult100, Connection: normalizeConnectionResult);
            return evaluatedValue102;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue103()
        {
            var readLegacyIdentifierResult104 = ReadLegacyIdentifier(operation, "canvasId", path);
            var readLegacyIdentifierResult105 = ReadLegacyIdentifier(operation, "connectionId", path);
            var readLegacyPayloadResult106 = ReadLegacyPayload<ConnectionDto>(operation, path, legacyEnums: true);
            var normalizeConnectionResult107 = NormalizeConnection(readLegacyPayloadResult106);
            FlowPatchCanonicalOperationDto evaluatedValue108 = new(op, readLegacyIdentifierResult104, ConnectionId: readLegacyIdentifierResult105, Connection: normalizeConnectionResult107);
            return evaluatedValue108;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue109()
        {
            var readLegacyIdentifierResult110 = ReadLegacyIdentifier(operation, "canvasId", path);
            var readLegacyIdentifierResult111 = ReadLegacyIdentifier(operation, "connectionId", path);
            FlowPatchCanonicalOperationDto evaluatedValue112 = new(op, readLegacyIdentifierResult110, ConnectionId: readLegacyIdentifierResult111);
            return evaluatedValue112;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue113()
        {
            var readLegacyPayloadResult114 = ReadLegacyPayload<string>(operation, path, legacyEnums: true);
            FlowPatchCanonicalOperationDto evaluatedValue115 = new(op, EntryNodeId: readLegacyPayloadResult114);
            return evaluatedValue115;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue116()
        {
            var readLegacyPayloadResult117 = ReadLegacyPayload<FlowRunPolicyDto>(operation, path, legacyEnums: true);
            FlowPatchCanonicalOperationDto evaluatedValue118 = new(op, RunPolicy: readLegacyPayloadResult117);
            return evaluatedValue118;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue119()
        {
            var readLegacyIdentifierResult120 = ReadLegacyIdentifier(operation, "canvasId", path);
            var readLegacyIdentifierResult121 = ReadLegacyIdentifier(operation, "nodeId", path);
            var readLegacyPayloadResult122 = ReadLegacyPayload<string>(operation, path, legacyEnums: true);
            FlowPatchCanonicalOperationDto evaluatedValue123 = new(op, readLegacyIdentifierResult120, readLegacyIdentifierResult121, Source: readLegacyPayloadResult122);
            return evaluatedValue123;
        }
    }

    private static FlowPatchCanonicalOperationDto ReadPersistedLegacyOperation(FlowPatchOperationDto operation, string path)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var value = operation.Value;
        FlowPatchCanonicalOperationDto evaluatedValue188 = operation.Operation switch
        {
            FlowPatchOperationKindDto.AddCanvas => resolveevaluatedValue125(),
            FlowPatchOperationKindDto.UpdateCanvas => resolveevaluatedValue127(),
            FlowPatchOperationKindDto.RemoveCanvas => resolveevaluatedValue130(),
            FlowPatchOperationKindDto.AddNode => resolveevaluatedValue133(),
            FlowPatchOperationKindDto.ReplaceNode => resolveevaluatedValue137(),
            FlowPatchOperationKindDto.RemoveNode => resolveevaluatedValue142(),
            FlowPatchOperationKindDto.SetNodeParameter => resolveevaluatedValue146(),
            FlowPatchOperationKindDto.AddNodeParameter => resolveevaluatedValue152(),
            FlowPatchOperationKindDto.RemoveNodeParameter => resolveevaluatedValue157(),
            FlowPatchOperationKindDto.AddConnection => resolveevaluatedValue162(),
            FlowPatchOperationKindDto.ReplaceConnection => resolveevaluatedValue167(),
            FlowPatchOperationKindDto.RemoveConnection => resolveevaluatedValue173(),
            FlowPatchOperationKindDto.SetEntryNode => resolveevaluatedValue177(),
            FlowPatchOperationKindDto.SetRunPolicy => resolveevaluatedValue180(),
            FlowPatchOperationKindDto.ReplaceScriptSource => resolveevaluatedValue183(),
            _ => throw Invalid(McpErrorCodes.FlowPatchOperationUnknown, path, "a supported legacy operation", "Create a new schema 2.0 preview. 请创建新的架构 2.0 预览。")
        };
        return evaluatedValue188;
        FlowPatchCanonicalOperationDto resolveevaluatedValue125()
        {
            var readPersistedPayloadResult = ReadPersistedPayload<CanvasDto>(value, path, legacyEnums: true);
            FlowPatchCanonicalOperationDto evaluatedValue126 = new("addCanvas", Canvas: readPersistedPayloadResult);
            return evaluatedValue126;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue127()
        {
            var requiredResult = Required(operation.CanvasId, $"{path}.canvasId");
            var readPersistedPayloadResult128 = ReadPersistedPayload<CanvasDto>(value, path, legacyEnums: true);
            FlowPatchCanonicalOperationDto evaluatedValue129 = new("updateCanvas", requiredResult, Canvas: readPersistedPayloadResult128);
            return evaluatedValue129;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue130()
        {
            var requiredResult131 = Required(operation.CanvasId, $"{path}.canvasId");
            FlowPatchCanonicalOperationDto evaluatedValue132 = new("removeCanvas", requiredResult131);
            return evaluatedValue132;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue133()
        {
            var requiredResult134 = Required(operation.CanvasId, $"{path}.canvasId");
            var readPersistedPayloadResult135 = ReadPersistedPayload<NodeDto>(value, path, legacyEnums: true);
            FlowPatchCanonicalOperationDto evaluatedValue136 = new("addNode", requiredResult134, Node: readPersistedPayloadResult135);
            return evaluatedValue136;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue137()
        {
            var requiredResult138 = Required(operation.CanvasId, $"{path}.canvasId");
            var requiredResult139 = Required(operation.NodeId, $"{path}.nodeId");
            var readPersistedPayloadResult140 = ReadPersistedPayload<NodeDto>(value, path, legacyEnums: true);
            FlowPatchCanonicalOperationDto evaluatedValue141 = new("replaceNode", requiredResult138, requiredResult139, Node: readPersistedPayloadResult140);
            return evaluatedValue141;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue142()
        {
            var requiredResult143 = Required(operation.CanvasId, $"{path}.canvasId");
            var requiredResult144 = Required(operation.NodeId, $"{path}.nodeId");
            FlowPatchCanonicalOperationDto evaluatedValue145 = new("removeNode", requiredResult143, requiredResult144);
            return evaluatedValue145;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue146()
        {
            var requiredResult147 = Required(operation.CanvasId, $"{path}.canvasId");
            var requiredResult148 = Required(operation.NodeId, $"{path}.nodeId");
            var requiredResult149 = Required(operation.ParameterId, $"{path}.parameterId");
            var readPersistedPayloadResult150 = ReadPersistedPayload<NodeParameterDto>(value, path, legacyEnums: true);
            FlowPatchCanonicalOperationDto evaluatedValue151 = new("setNodeParameter", requiredResult147, requiredResult148, ParameterId: requiredResult149, Parameter: readPersistedPayloadResult150);
            return evaluatedValue151;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue152()
        {
            var requiredResult153 = Required(operation.CanvasId, $"{path}.canvasId");
            var requiredResult154 = Required(operation.NodeId, $"{path}.nodeId");
            var readPersistedPayloadResult155 = ReadPersistedPayload<NodeParameterDto>(value, path, legacyEnums: true);
            FlowPatchCanonicalOperationDto evaluatedValue156 = new("addNodeParameter", requiredResult153, requiredResult154, Parameter: readPersistedPayloadResult155);
            return evaluatedValue156;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue157()
        {
            var requiredResult158 = Required(operation.CanvasId, $"{path}.canvasId");
            var requiredResult159 = Required(operation.NodeId, $"{path}.nodeId");
            var requiredResult160 = Required(operation.ParameterId, $"{path}.parameterId");
            FlowPatchCanonicalOperationDto evaluatedValue161 = new("removeNodeParameter", requiredResult158, requiredResult159, ParameterId: requiredResult160);
            return evaluatedValue161;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue162()
        {
            var requiredResult163 = Required(operation.CanvasId, $"{path}.canvasId");
            var readPersistedPayloadResult164 = ReadPersistedPayload<ConnectionDto>(value, path, legacyEnums: true);
            var normalizeConnectionResult165 = NormalizeConnection(readPersistedPayloadResult164);
            FlowPatchCanonicalOperationDto evaluatedValue166 = new("addConnection", requiredResult163, Connection: normalizeConnectionResult165);
            return evaluatedValue166;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue167()
        {
            var requiredResult168 = Required(operation.CanvasId, $"{path}.canvasId");
            var requiredResult169 = Required(operation.ConnectionId, $"{path}.connectionId");
            var readPersistedPayloadResult170 = ReadPersistedPayload<ConnectionDto>(value, path, legacyEnums: true);
            var normalizeConnectionResult171 = NormalizeConnection(readPersistedPayloadResult170);
            FlowPatchCanonicalOperationDto evaluatedValue172 = new("replaceConnection", requiredResult168, ConnectionId: requiredResult169, Connection: normalizeConnectionResult171);
            return evaluatedValue172;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue173()
        {
            var requiredResult174 = Required(operation.CanvasId, $"{path}.canvasId");
            var requiredResult175 = Required(operation.ConnectionId, $"{path}.connectionId");
            FlowPatchCanonicalOperationDto evaluatedValue176 = new("removeConnection", requiredResult174, ConnectionId: requiredResult175);
            return evaluatedValue176;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue177()
        {
            var readPersistedPayloadResult178 = ReadPersistedPayload<string>(value, path, legacyEnums: true);
            FlowPatchCanonicalOperationDto evaluatedValue179 = new("setEntryNode", EntryNodeId: readPersistedPayloadResult178);
            return evaluatedValue179;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue180()
        {
            var readPersistedPayloadResult181 = ReadPersistedPayload<FlowRunPolicyDto>(value, path, legacyEnums: true);
            FlowPatchCanonicalOperationDto evaluatedValue182 = new("setRunPolicy", RunPolicy: readPersistedPayloadResult181);
            return evaluatedValue182;
        }

        FlowPatchCanonicalOperationDto resolveevaluatedValue183()
        {
            var requiredResult184 = Required(operation.CanvasId, $"{path}.canvasId");
            var requiredResult185 = Required(operation.NodeId, $"{path}.nodeId");
            var readPersistedPayloadResult186 = ReadPersistedPayload<string>(value, path, legacyEnums: true);
            FlowPatchCanonicalOperationDto evaluatedValue187 = new("replaceScriptSource", requiredResult184, requiredResult185, Source: readPersistedPayloadResult186);
            return evaluatedValue187;
        }
    }

    private static T ReadLegacyPayload<T>(JsonElement operation, string path, bool legacyEnums)
    {
        var requirePropertyResult189 = RequireProperty(operation, "value", $"{path}.value");
        var readPayloadResult = ReadPayload<T>(requirePropertyResult189, $"{path}.value", legacyEnums);
        return readPayloadResult;
    }

    private static T ReadPersistedPayload<T>(JsonElement? value, string path, bool legacyEnums)
    {
        if (value is not { } payload)
        {
            throw Invalid(McpErrorCodes.FlowPatchFieldRequired, $"{path}.value", "a value", "Provide the operation payload. 请提供操作载荷。");
        }

        var readPayloadResult190 = ReadPayload<T>(payload, $"{path}.value", legacyEnums);
        return readPayloadResult190;
    }

    private static CanvasDto ReadCanvas(JsonElement operation, string name, string path, bool legacyEnums, string[] allowed)
    {
        EnsureKnownProperties(operation, path, allowed);
        var requirePropertyResult191 = RequireProperty(operation, name, $"{path}.{name}");
        var readPayloadResult192 = ReadPayload<CanvasDto>(requirePropertyResult191, $"{path}.{name}", legacyEnums);
        return readPayloadResult192;
    }

    private static NodeDto ReadNode(JsonElement operation, string name, string path, bool legacyEnums, string[] allowed)
    {
        EnsureKnownProperties(operation, path, allowed);
        var requirePropertyResult193 = RequireProperty(operation, name, $"{path}.{name}");
        var readPayloadResult194 = ReadPayload<NodeDto>(requirePropertyResult193, $"{path}.{name}", legacyEnums);
        return readPayloadResult194;
    }

    private static NodeParameterDto ReadParameter(JsonElement operation, string name, string path, bool legacyEnums, string[] allowed)
    {
        EnsureKnownProperties(operation, path, allowed);
        var requirePropertyResult195 = RequireProperty(operation, name, $"{path}.{name}");
        var readPayloadResult196 = ReadPayload<NodeParameterDto>(requirePropertyResult195, $"{path}.{name}", legacyEnums);
        return readPayloadResult196;
    }

    private static ConnectionDto ReadConnection(JsonElement operation, string name, string path, bool legacyEnums, string[] allowed)
    {
        EnsureKnownProperties(operation, path, allowed);
        var requirePropertyResult197 = RequireProperty(operation, name, $"{path}.{name}");
        var readPayloadResult198 = ReadPayload<ConnectionDto>(requirePropertyResult197, $"{path}.{name}", legacyEnums);
        var normalizeConnectionResult199 = NormalizeConnection(readPayloadResult198);
        return normalizeConnectionResult199;
    }

    private static ConnectionDto NormalizeConnection(ConnectionDto connection) => connection with
    {
        DataSource = connection.Kind == ConnectionKindDto.Data ? DataSourceDto.PreviousNode : null,
    };
    private static FlowRunPolicyDto ReadRunPolicy(JsonElement operation, string name, string path, bool legacyEnums, string[] allowed)
    {
        EnsureKnownProperties(operation, path, allowed);
        var requirePropertyResult200 = RequireProperty(operation, name, $"{path}.{name}");
        var readPayloadResult201 = ReadPayload<FlowRunPolicyDto>(requirePropertyResult200, $"{path}.{name}", legacyEnums);
        return readPayloadResult201;
    }

    private static T ReadPayload<T>(JsonElement payload, string path, bool legacyEnums)
    {
        ValidatePayload<T>(payload, path, legacyEnums);
        try
        {
            var evaluatedValue202 = payload.Deserialize<T>(JsonOptions) ?? throw Invalid(McpErrorCodes.FlowPatchPayloadInvalid, path, typeof(T).Name, "Provide a valid typed payload. 请提供有效的强类型载荷。");
            return evaluatedValue202;
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

        var requirePropertyResult203 = RequireProperty(payload, "lifecycle", $"{path}.lifecycle");
        ValidateEnum(requirePropertyResult203, $"{path}.lifecycle", legacyEnums, CanvasLifecycles);
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

        var requirePropertyResult205 = RequireProperty(payload, "type", $"{path}.type");
        var nodeType = ValidateEnum(requirePropertyResult205, $"{path}.type", legacyEnums, NodeTypes).Canonical;
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
                var portId = ReadRequiredString(port, "id", $"{path}.ports[{index}].id");
                if (!portIds.Add(portId))
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

                var parameterId = ReadRequiredString(parameterUi, "id", $"{parameterPath}.id");
                if (!parameterIds.Add(parameterId))
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
            if (!legacyEnums && !resolveEqualsResult())
            {
                throw Invalid(McpErrorCodes.FlowPatchReferenceInvalid, $"{path}.script.nodeId", $"the node ID '{nodeId}'", "Make script.nodeId match node.id. 请确保 script.nodeId 与 node.id 一致。");
            }

            bool resolveEqualsResult()
            {
                var readRequiredStringResult204 = ReadRequiredString(script, "nodeId", $"{path}.script.nodeId");
                var equalsResult = string.Equals(nodeId, readRequiredStringResult204, StringComparison.Ordinal);
                return equalsResult;
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

        var requirePropertyResult206 = RequireProperty(payload, "source", $"{path}.source");
        ValidateEnum(requirePropertyResult206, $"{path}.source", legacyEnums, DataSources);
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

        var requirePropertyResult207 = RequireProperty(payload, "kind", $"{path}.kind");
        ValidateEnum(requirePropertyResult207, $"{path}.kind", legacyEnums, ConnectionKinds);
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

        var requirePropertyResult208 = RequireProperty(payload, "concurrencyMode", $"{path}.concurrencyMode");
        ValidateEnum(requirePropertyResult208, $"{path}.concurrencyMode", legacyEnums, ConcurrencyModes);
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

        var requirePropertyResult209 = RequireProperty(payload, "inputs", $"{path}.inputs");
        ValidateScriptValues(requirePropertyResult209, $"{path}.inputs", legacyEnums);
        var requirePropertyResult210 = RequireProperty(payload, "outputs", $"{path}.outputs");
        ValidateScriptValues(requirePropertyResult210, $"{path}.outputs", legacyEnums);
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
                var equalsResult211 = string.Equals(item.Canonical, supplied, StringComparison.Ordinal);
                return equalsResult211;
            });
            if (canonical is not null)
            {
                return canonical;
            }

            if (legacy)
            {
                var pascalCase = allowed.FirstOrDefault(item =>
                {
                    var equalsResult212 = string.Equals(item.LegacyPascalCase, supplied, StringComparison.Ordinal);
                    return equalsResult212;
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

        var selectResult213 = allowed.Select(static item => $"'{item.Canonical}'");
        var expected = string.Join(", ", selectResult213);
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
            string evaluatedValue214 = numeric switch
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
            return evaluatedValue214;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw Invalid(McpErrorCodes.FlowPatchOperationUnknown, path, "a legacy operation string", "Use a schema 2.0 op string. 请使用架构 2.0 的 op 字符串。");
        }

        string evaluatedValue215 = value.GetString() switch
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
        return evaluatedValue215;
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
            var getStringResult = value.GetString();
            return getStringResult;
        }

        throw Invalid(McpErrorCodes.FlowPatchFieldInvalid, path, "a string or null", "Use a string value for this field. 此字段请使用字符串值。");
    }

    private static string ReadLegacyIdentifier(JsonElement source, string name, string path)
    {
        var readRequiredStringResult216 = ReadRequiredString(source, name, $"{path}.{name}");
        return readRequiredStringResult216;
    }

    private static string ReadRequiredString(JsonElement source, string name, string path)
    {
        var value = RequireProperty(source, name, path);
        if (value.ValueKind == JsonValueKind.String && !resolveIsNullOrWhiteSpaceResult())
        {
            var evaluatedValue217 = value.GetString()!;
            var trimResult = evaluatedValue217.Trim();
            return trimResult;
        }

        throw Invalid(McpErrorCodes.FlowPatchFieldInvalid, path, "a non-empty string", "Provide the required field value. 请提供必填字段的值。");
        bool resolveIsNullOrWhiteSpaceResult()
        {
            var getStringResult218 = value.GetString();
            var isNullOrWhiteSpaceResult = string.IsNullOrWhiteSpace(getStringResult218);
            return isNullOrWhiteSpaceResult;
        }
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
        var anyResult = source.EnumerateObject()
            .Any(property =>
            {
                var evaluatedValue219 = names.Contains(property.Name, StringComparer.Ordinal) && property.Value.ValueKind != JsonValueKind.Null;
                return evaluatedValue219;
            });
        return anyResult;
    }

    private static string Required(string? value, string path)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            var trimResult220 = value.Trim();
            return trimResult220;
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
                var selectResult221 = allowed.Select(static item => $"'{item}'");
                var joinResult = string.Join(", ", selectResult221);
                throw Invalid(McpErrorCodes.FlowPatchUnexpectedField, $"{path}.{property.Name}", joinResult, "Remove fields that are not defined by this operation schema. 请移除此操作架构未定义的字段。");
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
        FlowPatchContractException evaluatedValue222 = new(code, fieldPath, expected, remediation);
        return evaluatedValue222;
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
                if (!resolveTryAddResult())
                {
                    throw Invalid(McpErrorCodes.FlowPatchDuplicateId, "$.current.canvases", "unique canvas IDs", "Repair the current flow before submitting a patch. 请先修复当前流程，再提交补丁。");
                }

                bool resolveTryAddResult()
                {
                    var fromCanvasResult = CanvasReferenceState.FromCanvas(canvas, "$.current.canvases");
                    var tryAddResult = canvases.TryAdd(canvas.Id, fromCanvasResult);
                    return tryAddResult;
                }
            }

            var patchReferenceState = new PatchReferenceState(canvases);
            return patchReferenceState;
        }

        public void Apply(FlowPatchCanonicalOperationDto operation, string path)
        {
            ArgumentNullException.ThrowIfNull(operation);
            switch (operation.Op)
            {
                case "addCanvas":
                    {
                        var canvas = RequireValue(operation.Canvas, $"{path}.canvas");
                        if (!resolveTryAddResult223())
                        {
                            throw Duplicate($"{path}.canvas.id", "canvas ID");
                        }

                        ValidateConnections(_canvases[canvas.Id], $"{path}.canvas.connections");
                        return;
                        bool resolveTryAddResult223()
                        {
                            var fromCanvasResult224 = CanvasReferenceState.FromCanvas(canvas, $"{path}.canvas");
                            var tryAddResult225 = _canvases.TryAdd(canvas.Id, fromCanvasResult224);
                            return tryAddResult225;
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
                        var requireIdResult = RequireId(operation.CanvasId, $"{path}.canvasId");
                        var canvas = RequireCanvas(requireIdResult, $"{path}.canvasId");
                        var node = RequireValue(operation.Node, $"{path}.node");
                        if (!resolveTryAddResult226())
                        {
                            throw Duplicate($"{path}.node.id", "node ID");
                        }

                        return;
                        bool resolveTryAddResult226()
                        {
                            var fromNodeResult = NodeReferenceState.FromNode(node, $"{path}.node");
                            var tryAddResult227 = canvas.Nodes.TryAdd(node.Id, fromNodeResult);
                            return tryAddResult227;
                        }
                    }

                case "replaceNode":
                    {
                        var requireIdResult228 = RequireId(operation.CanvasId, $"{path}.canvasId");
                        var canvas = RequireCanvas(requireIdResult228, $"{path}.canvasId");
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
                        var requireIdResult229 = RequireId(operation.CanvasId, $"{path}.canvasId");
                        var canvas = RequireCanvas(requireIdResult229, $"{path}.canvasId");
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
                        var requireIdResult230 = RequireId(operation.CanvasId, $"{path}.canvasId");
                        var canvas = RequireCanvas(requireIdResult230, $"{path}.canvasId");
                        var requireIdResult231 = RequireId(operation.NodeId, $"{path}.nodeId");
                        var node = RequireNode(canvas, requireIdResult231, $"{path}.nodeId");
                        var parameterId = RequireId(operation.ParameterId, $"{path}.parameterId");
                        var parameter = RequireValue(operation.Parameter, $"{path}.parameter");
                        var actualParameterId = RequireId(parameter.Ui?.Id, $"{path}.parameter.ui.id");
                        RequireMatchingId(parameterId, actualParameterId, $"{path}.parameter.ui.id");
                        if (!node.ParameterIds.Contains(parameterId))
                        {
                            throw ReferenceInvalid($"{path}.parameterId", "an existing parameter ID on the node");
                        }

                        return;
                    }

                case "addNodeParameter":
                    {
                        var requireIdResult232 = RequireId(operation.CanvasId, $"{path}.canvasId");
                        var canvas = RequireCanvas(requireIdResult232, $"{path}.canvasId");
                        var requireIdResult233 = RequireId(operation.NodeId, $"{path}.nodeId");
                        var node = RequireNode(canvas, requireIdResult233, $"{path}.nodeId");
                        var parameter = RequireValue(operation.Parameter, $"{path}.parameter");
                        var parameterId = RequireId(parameter.Ui?.Id, $"{path}.parameter.ui.id");
                        if (!node.ParameterIds.Add(parameterId))
                        {
                            throw Duplicate($"{path}.parameter.ui.id", "parameter ID");
                        }

                        return;
                    }

                case "removeNodeParameter":
                    {
                        var requireIdResult234 = RequireId(operation.CanvasId, $"{path}.canvasId");
                        var canvas = RequireCanvas(requireIdResult234, $"{path}.canvasId");
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
                        var requireIdResult235 = RequireId(operation.CanvasId, $"{path}.canvasId");
                        var canvas = RequireCanvas(requireIdResult235, $"{path}.canvasId");
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
                        var requireIdResult236 = RequireId(operation.CanvasId, $"{path}.canvasId");
                        var canvas = RequireCanvas(requireIdResult236, $"{path}.canvasId");
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
                        var requireIdResult237 = RequireId(operation.CanvasId, $"{path}.canvasId");
                        var canvas = RequireCanvas(requireIdResult237, $"{path}.canvasId");
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
                            var containsKeyResult = canvas.Nodes.ContainsKey(entryNodeId);
                            return containsKeyResult;
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
                        var requireIdResult238 = RequireId(operation.CanvasId, $"{path}.canvasId");
                        var canvas = RequireCanvas(requireIdResult238, $"{path}.canvasId");
                        var requireIdResult239 = RequireId(operation.NodeId, $"{path}.nodeId");
                        var node = RequireNode(canvas, requireIdResult239, $"{path}.nodeId");
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
            FlowPatchContractNormalizer.PatchReferenceState.CanvasReferenceState evaluatedValue240 = _canvases.TryGetValue(canvasId, out var canvas) ? canvas : throw ReferenceInvalid(path, "an existing canvas ID");
            return evaluatedValue240;
        }

        private static NodeReferenceState RequireNode(CanvasReferenceState canvas, string nodeId, string path)
        {
            FlowPatchContractNormalizer.PatchReferenceState.NodeReferenceState evaluatedValue241 = canvas.Nodes.TryGetValue(nodeId, out var node) ? node : throw ReferenceInvalid(path, "an existing node ID");
            return evaluatedValue241;
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
            var evaluatedValue242 = value ?? throw Invalid(McpErrorCodes.FlowPatchFieldRequired, path, "the operation payload", "Provide the payload required by the selected operation. 请提供所选操作要求的载荷。");
            return evaluatedValue242;
        }

        private static string RequireId(string? value, string path)
        {
            string evaluatedValue243 = string.IsNullOrWhiteSpace(value) ? throw Invalid(McpErrorCodes.FlowPatchFieldRequired, path, "a non-empty ID", "Provide an ID from the flow edit model. 请提供流程编辑模型中的 ID。") : value.Trim();
            return evaluatedValue243;
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
            var invalidResult = Invalid(McpErrorCodes.FlowPatchDuplicateId, path, $"a unique {kind}", "Use an ID that is not already present in the target canvas. 请使用目标画布中尚不存在的 ID。");
            return invalidResult;
        }

        private static FlowPatchContractException ReferenceInvalid(string path, string expected)
        {
            var invalidResult244 = Invalid(McpErrorCodes.FlowPatchReferenceInvalid, path, expected, "Read the current flow edit model and use an existing ID and port. 请读取当前流程编辑模型，并使用已有的 ID 和端口。");
            return invalidResult244;
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
                    if (!resolveTryAddResult245())
                    {
                        throw Duplicate($"{path}.nodes", "node ID");
                    }

                    bool resolveTryAddResult245()
                    {
                        var fromNodeResult246 = NodeReferenceState.FromNode(node, $"{path}.nodes");
                        var tryAddResult247 = nodes.TryAdd(node.Id, fromNodeResult246);
                        return tryAddResult247;
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

                var canvasReferenceState = new CanvasReferenceState(nodes, connections);
                return canvasReferenceState;
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

                var nodeReferenceState = new NodeReferenceState(ports, parameters, node.Script is not null);
                return nodeReferenceState;
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
        var newGuidResult = Guid.NewGuid();
        string diagnosticIdInitialValue = newGuidResult.ToString("N");
        return diagnosticIdInitialValue;
    }
}

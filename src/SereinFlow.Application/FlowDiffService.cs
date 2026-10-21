using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SereinFlow.Contracts;

namespace SereinFlow.Application;

/// <summary>
/// Compares flow definitions and describes their changes.
/// 比较流程定义并描述其中的变更。
/// </summary>
public sealed class FlowDiffService
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateContractOptions();
    public FlowDiffDto Compare(FlowDefinitionDto before, FlowDefinitionDto after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var changes = new List<FlowDiffItemDto>();
        var addedNodes = 0;
        var removedNodes = 0;
        var changedNodes = 0;
        var addedConnections = 0;
        var removedConnections = 0;
        var changedConnections = 0;
        AddScalar(changes, "entryNodeId", before.EntryNodeId, after.EntryNodeId);
        var serializeResult6 = Serialize(before.RunPolicy);
        var serializeResult7 = Serialize(after.RunPolicy);
        AddScalar(changes, "runPolicy", serializeResult6, serializeResult7);
        var beforeCanvases = before.Canvases.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var afterCanvases = after.Canvases.ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (var id in beforeCanvases.Keys
            .Union(afterCanvases.Keys)
            .Order(StringComparer.Ordinal))
        {
            if (!beforeCanvases.TryGetValue(id, out var oldCanvas))
            {
                var serializeResult = Serialize(afterCanvases[id]);
                FlowDiffItemDto evaluatedValue = new("canvas.added", $"canvases.{id}", null, serializeResult);
                changes.Add(evaluatedValue);
                continue;
            }

            if (!afterCanvases.TryGetValue(id, out var newCanvas))
            {
                var serializeResult1 = Serialize(oldCanvas);
                FlowDiffItemDto evaluatedValue2 = new("canvas.removed", $"canvases.{id}", serializeResult1, null);
                changes.Add(evaluatedValue2);
                continue;
            }

            var toStringResult = oldCanvas.Lifecycle.ToString();
            var toStringResult5 = newCanvas.Lifecycle.ToString();
            AddScalar(changes, $"canvases.{id}.lifecycle", toStringResult, toStringResult5);
            AddScalar(changes, $"canvases.{id}.name", oldCanvas.Name, newCanvas.Name);
            CompareEntities(changes, $"canvases.{id}.nodes", oldCanvas.Nodes, newCanvas.Nodes, static node => node.Id, static node =>
            {
                var serializeResult3 = Serialize(node);
                return serializeResult3;
            }, "node", ref addedNodes, ref removedNodes, ref changedNodes);
            CompareEntities(changes, $"canvases.{id}.connections", oldCanvas.Connections, newCanvas.Connections, static connection => connection.Id, static connection =>
            {
                var serializeResult4 = Serialize(connection);
                return serializeResult4;
            }, "connection", ref addedConnections, ref removedConnections, ref changedConnections);
        }

        var getChecksumResult = GetChecksum(before);
        var getChecksumResult8 = GetChecksum(after);
        FlowDiffDto evaluatedValue9 = new(after.Id, before.Version, after.Version == before.Version ? null : after.Version, getChecksumResult, getChecksumResult8, changes, addedNodes, removedNodes, changedNodes, addedConnections, removedConnections, changedConnections);
        return evaluatedValue9;
    }

    public static string GetChecksum(FlowDefinitionDto definition)
    {
        // Version is history metadata, not flow content. Keeping it out of the
        // checksum lets an unchanged definition compare equal across version
        // allocation, publish and rollback operations.
        // 版本是历史元数据而不是流程内容。排除版本后，同一流程在分配版本、发布和
        // 回滚过程中仍可通过校验和判断内容是否真正发生变化。
        var canonical = definition with
        {
            Version = 0,
            Checksum = string.Empty
        };
        var serializeResult10 = Serialize(canonical);
        var bytes = Encoding.UTF8.GetBytes(serializeResult10);
        var hashDataResult = SHA256.HashData(bytes);
        var toHexStringResult = Convert.ToHexString(hashDataResult);
        var toLowerInvariantResult = toHexStringResult.ToLowerInvariant();
        return toLowerInvariantResult;
    }

    public static FlowDiffDto RedactSensitive(FlowDiffDto diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        var evaluatedValue12 = diff with
        {
            Changes = resolveToArrayResult(),
        };
        return evaluatedValue12;
        IReadOnlyList<FlowDiffItemDto> resolveToArrayResult()
        {
            var toArrayResult = diff.Changes
                .Select(static change =>
                {
                    var evaluatedValue11 = change with
                    {
                        Before = RedactJson(change.Before),
                        After = RedactJson(change.After),
                        IsSensitive = IsSensitiveChange(change) || change.IsSensitive,
                    };
                    return evaluatedValue11;
                })
                .ToArray();
            return toArrayResult;
        }
    }

    public static FlowDefinitionDto RedactSensitive(FlowDefinitionDto definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var evaluatedValue24 = definition with
        {
            Canvases = resolveToArrayResult21(),
        };
        return evaluatedValue24;
        IReadOnlyList<CanvasDto> resolveToArrayResult21()
        {
            var toArrayResult23 = definition.Canvases
                .Select(static canvas =>
                {
                    var evaluatedValue20 = canvas with
                    {
                        Nodes = resolveToArrayResult17(),
                    };
                    return evaluatedValue20;
                    IReadOnlyList<NodeDto> resolveToArrayResult17()
                    {
                        var toArrayResult19 = canvas.Nodes
                            .Select(static node =>
                            {
                                var evaluatedValue16 = node with
                                {
                                    Parameters = resolveToArrayResult13(),
                                    Script = node.Script is null ? null : node.Script with
                                    {
                                        Source = "[redacted]"
                                    },
                                };
                                return evaluatedValue16;
                                IReadOnlyList<NodeParameterDto> resolveToArrayResult13()
                                {
                                    var toArrayResult15 = node.Parameters
                                        .Select(static parameter => parameter with { ValueJson = null, Ui = parameter.Ui is null ? null : parameter.Ui with { LiteralValue = null, ProjectInputKey = null, Expression = null, }, })
                                        .ToArray();
                                    return toArrayResult15;
                                }
                            })
                            .ToArray();
                        return toArrayResult19;
                    }
                })
                .ToArray();
            return toArrayResult23;
        }
    }

    private static bool IsSensitiveChange(FlowDiffItemDto change)
    {
        var evaluatedValue25 = change.Path.Contains(".nodes.", StringComparison.Ordinal) || change.Path.Contains(".canvases.", StringComparison.Ordinal) && (change.Before?.Contains("parameters", StringComparison.OrdinalIgnoreCase) == true || change.After?.Contains("parameters", StringComparison.OrdinalIgnoreCase) == true);
        return evaluatedValue25;
    }

    private static string? RedactJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        // Scalar diff values such as canvas names and entry node IDs are not
        // JSON payloads. Preserve them so a redacted diff can still explain
        // the structural change. Sensitive node/parameter payloads are
        // serialized objects or arrays and continue through the redaction
        // path below.
        // 画布名称、入口节点 ID 等标量差异不是 JSON 载荷，应保留它们，
        // 这样脱敏后的差异仍能说明结构变化。节点/参数敏感载荷会被序列化为
        // 对象或数组，继续走下面的脱敏路径。
        var trimmed = value.TrimStart();
        if (trimmed.Length == 0 || (trimmed[0] != '{' && trimmed[0] != '['))
        {
            return value;
        }

        try
        {
            var node = JsonNode.Parse(value);
            if (node is null)
            {
                return null;
            }

            RedactJsonNode(node);
            var toJsonStringResult = node.ToJsonString(JsonOptions);
            return toJsonStringResult;
        }
        catch (JsonException)
        {
            // A value that looked like structured JSON but could not be
            // parsed is safer to omit than to return unredacted.
            return null;
        }
    }

    private static void RedactJsonNode(JsonNode node)
    {
        if (node is JsonObject objectNode)
        {
            foreach (var property in objectNode.ToArray())
            {
                if (property.Key.Equals("source", StringComparison.OrdinalIgnoreCase) || property.Key.Equals("valueJson", StringComparison.OrdinalIgnoreCase) || property.Key.Equals("literalValue", StringComparison.OrdinalIgnoreCase) || property.Key.Equals("projectInputKey", StringComparison.OrdinalIgnoreCase) || property.Key.Equals("expression", StringComparison.OrdinalIgnoreCase))
                {
                    objectNode[property.Key] = null;
                    continue;
                }

                if (property.Value is not null)
                {
                    RedactJsonNode(property.Value);
                }
            }
        }
        else if (node is JsonArray arrayNode)
        {
            foreach (var child in arrayNode)
            {
                if (child is not null)
                {
                    RedactJsonNode(child);
                }
            }
        }
    }

    private static void CompareEntities<T>(List<FlowDiffItemDto> changes, string path, IReadOnlyList<T> before, IReadOnlyList<T> after, Func<T, string> key, Func<T, string> serialize, string kind, ref int added, ref int removed, ref int changed)
    {
        var oldItems = before.ToDictionary(key, StringComparer.Ordinal);
        var newItems = after.ToDictionary(key, StringComparer.Ordinal);
        foreach (var id in oldItems.Keys
            .Union(newItems.Keys)
            .Order(StringComparer.Ordinal))
        {
            if (!oldItems.TryGetValue(id, out var oldItem))
            {
                added++;
                var serializeResult26 = serialize(newItems[id]);
                FlowDiffItemDto evaluatedValue27 = new($"{kind}.added", $"{path}.{id}", null, serializeResult26);
                changes.Add(evaluatedValue27);
            }
            else if (!newItems.TryGetValue(id, out var newItem))
            {
                removed++;
                var serializeResult28 = serialize(oldItem);
                FlowDiffItemDto evaluatedValue29 = new($"{kind}.removed", $"{path}.{id}", serializeResult28, null);
                changes.Add(evaluatedValue29);
            }
            else
            {
                if (!resolveEqualsResult())
                {
                    changed++;
                    var serializeResult30 = serialize(oldItem);
                    var serializeResult31 = serialize(newItem);
                    FlowDiffItemDto evaluatedValue32 = new($"{kind}.changed", $"{path}.{id}", serializeResult30, serializeResult31);
                    changes.Add(evaluatedValue32);
                }

                bool resolveEqualsResult()
                {
                    var serializeResult33 = serialize(oldItem);
                    var serializeResult34 = serialize(newItem);
                    var equalsResult = string.Equals(serializeResult33, serializeResult34, StringComparison.Ordinal);
                    return equalsResult;
                }
            }
        }
    }

    private static void AddScalar(List<FlowDiffItemDto> changes, string path, string? before, string? after)
    {
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            FlowDiffItemDto evaluatedValue36 = new("property.changed", path, before, after);
            changes.Add(evaluatedValue36);
        }
    }

    private static string Serialize<T>(T value)
    {
        var serializeResult37 = JsonSerializer.Serialize(value, JsonOptions);
        return serializeResult37;
    }
}

/// <summary>
/// Previews and applies version-checked changes to flow definitions.
/// 预览并应用带版本检查的流程定义变更。
/// </summary>
public sealed class FlowPatchService
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateContractOptions();
    /// <summary>
    /// The MCP boundary supplies only canonical v2 operations. The established
    /// mutation implementation still uses the legacy internal envelope, which
    /// is created here after the contract normalizer has completed validation.
    /// </summary>
    public FlowDefinitionDto Apply(FlowDefinitionDto definition, IReadOnlyList<FlowPatchCanonicalOperationDto> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var toArrayResult39 = operations
            .Select(FlowPatchContractNormalizer.ToLegacyOperation)
            .ToArray();
        var applyResult = Apply(definition, toArrayResult39);
        return applyResult;
    }

    public FlowDefinitionDto Apply(FlowDefinitionDto definition, IReadOnlyList<FlowPatchOperationDto> operations)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(operations);
        var canvases = definition.Canvases.ToList();
        var entryNodeId = definition.EntryNodeId;
        var runPolicy = definition.RunPolicy;
        foreach (var operation in operations)
        {
            switch (operation.Operation)
            {
                case FlowPatchOperationKindDto.AddCanvas:
                    var readValueResult = ReadValue<CanvasDto>(operation);
                    AddCanvas(canvases, readValueResult, false);
                    break;
                case FlowPatchOperationKindDto.UpdateCanvas:
                    var requiredResult = Required(operation.CanvasId, "canvasId");
                    var readValueResult40 = ReadValue<CanvasDto>(operation);
                    ReplaceCanvas(canvases, requiredResult, readValueResult40);
                    break;
                case FlowPatchOperationKindDto.RemoveCanvas:
                    var requiredResult41 = Required(operation.CanvasId, "canvasId");
                    RemoveCanvas(canvases, requiredResult41);
                    break;
                case FlowPatchOperationKindDto.AddNode:
                    var requiredResult42 = Required(operation.CanvasId, "canvasId");
                    var readValueResult43 = ReadValue<NodeDto>(operation);
                    AddNode(canvases, requiredResult42, null, readValueResult43, false);
                    break;
                case FlowPatchOperationKindDto.ReplaceNode:
                    var requiredResult44 = Required(operation.CanvasId, "canvasId");
                    var requiredResult45 = Required(operation.NodeId, "nodeId");
                    var readValueResult46 = ReadValue<NodeDto>(operation);
                    AddNode(canvases, requiredResult44, requiredResult45, readValueResult46, true);
                    break;
                case FlowPatchOperationKindDto.RemoveNode:
                    var requiredResult47 = Required(operation.CanvasId, "canvasId");
                    var requiredResult48 = Required(operation.NodeId, "nodeId");
                    RemoveNode(canvases, requiredResult47, requiredResult48);
                    break;
                case FlowPatchOperationKindDto.SetNodeParameter:
                    var requiredResult49 = Required(operation.CanvasId, "canvasId");
                    var requiredResult50 = Required(operation.NodeId, "nodeId");
                    var requiredResult51 = Required(operation.ParameterId, "parameterId");
                    var readValueResult52 = ReadValue<NodeParameterDto>(operation);
                    SetNodeParameter(canvases, requiredResult49, requiredResult50, requiredResult51, readValueResult52);
                    break;
                case FlowPatchOperationKindDto.AddNodeParameter:
                    var requiredResult53 = Required(operation.CanvasId, "canvasId");
                    var requiredResult54 = Required(operation.NodeId, "nodeId");
                    var readValueResult55 = ReadValue<NodeParameterDto>(operation);
                    AddNodeParameter(canvases, requiredResult53, requiredResult54, readValueResult55);
                    break;
                case FlowPatchOperationKindDto.RemoveNodeParameter:
                    var requiredResult56 = Required(operation.CanvasId, "canvasId");
                    var requiredResult57 = Required(operation.NodeId, "nodeId");
                    var requiredResult58 = Required(operation.ParameterId, "parameterId");
                    RemoveNodeParameter(canvases, requiredResult56, requiredResult57, requiredResult58);
                    break;
                case FlowPatchOperationKindDto.AddConnection:
                    var requiredResult59 = Required(operation.CanvasId, "canvasId");
                    var readValueResult60 = ReadValue<ConnectionDto>(operation);
                    AddConnection(canvases, requiredResult59, null, readValueResult60, false);
                    break;
                case FlowPatchOperationKindDto.ReplaceConnection:
                    var requiredResult61 = Required(operation.CanvasId, "canvasId");
                    var requiredResult62 = Required(operation.ConnectionId, "connectionId");
                    var readValueResult63 = ReadValue<ConnectionDto>(operation);
                    AddConnection(canvases, requiredResult61, requiredResult62, readValueResult63, true);
                    break;
                case FlowPatchOperationKindDto.RemoveConnection:
                    var requiredResult64 = Required(operation.CanvasId, "canvasId");
                    var requiredResult65 = Required(operation.ConnectionId, "connectionId");
                    RemoveConnection(canvases, requiredResult64, requiredResult65);
                    break;
                case FlowPatchOperationKindDto.SetEntryNode:
                    entryNodeId = ReadValue<string>(operation);
                    break;
                case FlowPatchOperationKindDto.SetRunPolicy:
                    runPolicy = ReadValue<FlowRunPolicyDto>(operation);
                    break;
                case FlowPatchOperationKindDto.ReplaceScriptSource:
                    var requiredResult66 = Required(operation.CanvasId, "canvasId");
                    var requiredResult67 = Required(operation.NodeId, "nodeId");
                    var readValueResult68 = ReadValue<string>(operation);
                    ReplaceScriptSource(canvases, requiredResult66, requiredResult67, readValueResult68);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operations), operation.Operation, "The flow patch operation is not supported.");
            }
        }

        // Deleting the entry node is valid for an editor draft. Keep the
        // flow metadata consistent with the final canvas contents so the
        // persistence validator can accept an intentionally empty draft.
        // 删除入口节点对编辑器草稿是合法的。依据最终画布内容同步流程元数据，
        // 这样持久化校验才能接受刻意保留的空白草稿。
        entryNodeId = NormalizeEntryNodeId(canvases, entryNodeId);
        var evaluatedValue69 = definition with
        {
            Canvases = canvases,
            EntryNodeId = entryNodeId,
            RunPolicy = runPolicy,
            Checksum = FlowDiffService.GetChecksum(definition with { Canvases = canvases, EntryNodeId = entryNodeId, RunPolicy = runPolicy })
        };
        return evaluatedValue69;
    }

    private static void AddCanvas(List<CanvasDto> canvases, CanvasDto canvas, bool replace)
    {
        RequireObjectId(canvas.Id, "canvasId");
        ValidateCanvasObjects(canvas);
        var index = canvases.FindIndex(item => item.Id == canvas.Id);
        if (index >= 0 && !replace)
        {
            throw new InvalidOperationException($"Canvas '{canvas.Id}' already exists.");
        }

        if (index < 0 && replace)
        {
            throw new InvalidOperationException($"Canvas '{canvas.Id}' does not exist.");
        }

        if (index >= 0)
        {
            canvases[index] = canvas;
        }
        else
        {
            canvases.Add(canvas);
        }
    }

    private static void ReplaceCanvas(List<CanvasDto> canvases, string canvasId, CanvasDto canvas)
    {
        RequireMatchingId(canvasId, canvas.Id, "canvasId");
        AddCanvas(canvases, canvas, true);
    }

    private static void RemoveCanvas(List<CanvasDto> canvases, string canvasId)
    {
        var index = canvases.FindIndex(item => item.Id == canvasId);
        if (index < 0)
        {
            throw new InvalidOperationException($"Canvas '{canvasId}' does not exist.");
        }

        if (canvases[index].Nodes.Count > 0 || canvases[index].Connections.Count > 0)
        {
            throw new InvalidOperationException($"Canvas '{canvasId}' must be empty before removal.");
        }

        canvases.RemoveAt(index);
    }

    private static void AddNode(List<CanvasDto> canvases, string canvasId, string? expectedNodeId, NodeDto node, bool replace)
    {
        RequireObjectId(node.Id, "nodeId");
        if (replace)
        {
            RequireMatchingId(expectedNodeId, node.Id, "nodeId");
        }

        ValidateNodeObjects(node);
        var canvasIndex = FindCanvas(canvases, canvasId);
        var nodes = canvases[canvasIndex].Nodes.ToList();
        var index = nodes.FindIndex(item => item.Id == node.Id);
        if (index >= 0 && !replace)
        {
            throw new InvalidOperationException($"Node '{node.Id}' already exists.");
        }

        if (index < 0 && replace)
        {
            throw new InvalidOperationException($"Node '{node.Id}' does not exist.");
        }

        if (index >= 0)
        {
            nodes[index] = node;
        }
        else
        {
            nodes.Add(node);
        }

        canvases[canvasIndex] = canvases[canvasIndex] with
        {
            Nodes = nodes
        };
    }

    private static void RemoveNode(List<CanvasDto> canvases, string canvasId, string nodeId)
    {
        var canvasIndex = FindCanvas(canvases, canvasId);
        var canvas = canvases[canvasIndex];
        if (!canvas.Nodes.Any(node => node.Id == nodeId))
        {
            throw new InvalidOperationException($"Node '{nodeId}' does not exist.");
        }

        if (canvas.Connections.Any(connection => connection.FromNodeId == nodeId || connection.ToNodeId == nodeId))
        {
            throw new InvalidOperationException($"Connections for node '{nodeId}' must be removed first.");
        }

        canvases[canvasIndex] = canvas with
        {
            Nodes = resolveToArrayResult70()
        };
        IReadOnlyList<NodeDto> resolveToArrayResult70()
        {
            var toArrayResult71 = canvas.Nodes
                .Where(node => node.Id != nodeId)
                .ToArray();
            return toArrayResult71;
        }
    }

    private static string NormalizeEntryNodeId(IReadOnlyList<CanvasDto> canvases, string entryNodeId)
    {
        string evaluatedValue72 = string.IsNullOrWhiteSpace(entryNodeId) || canvases.Any(canvas =>
        {
            var anyResult = canvas.Nodes.Any(node => node.Id == entryNodeId);
            return anyResult;
        }) ? entryNodeId ?? string.Empty : string.Empty;
        return evaluatedValue72;
    }

    private static void SetNodeParameter(List<CanvasDto> canvases, string canvasId, string nodeId, string parameterId, NodeParameterDto parameter)
    {
        RequireObjectId(parameterId, "parameterId");
        RequireObjectId(parameter.Ui?.Id, "value.ui.id");
        if (!string.Equals(parameterId, parameter.Ui!.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The parameter ID does not match value.ui.id.");
        }

        var canvasIndex = FindCanvas(canvases, canvasId);
        var canvas = canvases[canvasIndex];
        var toListResult = canvas.Nodes.ToList();
        var nodeIndex = toListResult.FindIndex(node => node.Id == nodeId);
        if (nodeIndex < 0)
        {
            throw new InvalidOperationException($"Node '{nodeId}' does not exist.");
        }

        var nodes = canvas.Nodes.ToList();
        var node = nodes[nodeIndex];
        var parameters = node.Parameters.ToList();
        var index = parameters.FindIndex(item =>
        {
            var equalsResult73 = string.Equals(item.Ui?.Id, parameterId, StringComparison.Ordinal);
            return equalsResult73;
        });
        if (index < 0)
        {
            throw new InvalidOperationException($"Parameter '{parameterId}' does not exist on node '{nodeId}'.");
        }

        parameters[index] = parameter;
        nodes[nodeIndex] = node with
        {
            Parameters = parameters
        };
        canvases[canvasIndex] = canvas with
        {
            Nodes = nodes
        };
    }

    private static void AddNodeParameter(List<CanvasDto> canvases, string canvasId, string nodeId, NodeParameterDto parameter)
    {
        var parameterId = parameter.Ui?.Id;
        RequireObjectId(parameterId, "parameter.ui.id");
        var canvasIndex = FindCanvas(canvases, canvasId);
        var canvas = canvases[canvasIndex];
        var toListResult75 = canvas.Nodes.ToList();
        var nodeIndex = toListResult75.FindIndex(node => node.Id == nodeId);
        if (nodeIndex < 0)
        {
            throw new InvalidOperationException($"Node '{nodeId}' does not exist.");
        }

        var nodes = canvas.Nodes.ToList();
        var node = nodes[nodeIndex];
        if (node.Parameters.Any(item =>
        {
            var equalsResult74 = string.Equals(item.Ui?.Id, parameterId, StringComparison.Ordinal);
            return equalsResult74;
        }))
        {
            throw new InvalidOperationException($"Parameter '{parameterId}' already exists on node '{nodeId}'.");
        }

        var parameters = node.Parameters.ToList();
        parameters.Add(parameter);
        nodes[nodeIndex] = node with
        {
            Parameters = parameters
        };
        canvases[canvasIndex] = canvas with
        {
            Nodes = nodes
        };
    }

    private static void RemoveNodeParameter(List<CanvasDto> canvases, string canvasId, string nodeId, string parameterId)
    {
        var canvasIndex = FindCanvas(canvases, canvasId);
        var canvas = canvases[canvasIndex];
        var toListResult77 = canvas.Nodes.ToList();
        var nodeIndex = toListResult77.FindIndex(node => node.Id == nodeId);
        if (nodeIndex < 0)
        {
            throw new InvalidOperationException($"Node '{nodeId}' does not exist.");
        }

        if (canvas.Connections.Any(connection => connection.Kind == ConnectionKindDto.Data && connection.ToNodeId == nodeId && connection.ToPortId == parameterId))
        {
            throw new InvalidOperationException($"Data connections for parameter '{parameterId}' on node '{nodeId}' must be removed first.");
        }

        var nodes = canvas.Nodes.ToList();
        var node = nodes[nodeIndex];
        var parameters = node.Parameters.ToList();
        var parameterIndex = parameters.FindIndex(item =>
        {
            var equalsResult76 = string.Equals(item.Ui?.Id, parameterId, StringComparison.Ordinal);
            return equalsResult76;
        });
        if (parameterIndex < 0)
        {
            throw new InvalidOperationException($"Parameter '{parameterId}' does not exist on node '{nodeId}'.");
        }

        parameters.RemoveAt(parameterIndex);
        nodes[nodeIndex] = node with
        {
            Parameters = parameters
        };
        canvases[canvasIndex] = canvas with
        {
            Nodes = nodes
        };
    }

    private static void AddConnection(List<CanvasDto> canvases, string canvasId, string? expectedConnectionId, ConnectionDto connection, bool replace)
    {
        RequireObjectId(connection.Id, "connectionId");
        if (replace)
        {
            RequireMatchingId(expectedConnectionId, connection.Id, "connectionId");
        }

        // A data connection is the persisted source binding for its target
        // parameter. Keep the connection metadata and the parameter source in
        // sync so MCP patches have the same observable contract as editor
        // connections.
        // 数据连接就是目标参数的持久化来源绑定。保持连接元数据和参数来源一致，
        // 使 MCP 补丁与编辑器创建的连接具有相同的可观察契约。
        connection = NormalizeDataConnection(connection);
        var canvasIndex = FindCanvas(canvases, canvasId);
        var connections = canvases[canvasIndex].Connections.ToList();
        var index = connections.FindIndex(item => item.Id == connection.Id);
        if (index >= 0 && !replace)
        {
            throw new InvalidOperationException($"Connection '{connection.Id}' already exists.");
        }

        if (index < 0 && replace)
        {
            throw new InvalidOperationException($"Connection '{connection.Id}' does not exist.");
        }

        if (index >= 0)
        {
            ResetDataConnectionTargetParameter(canvases, canvasId, connections[index]);
            connections[index] = connection;
        }
        else
        {
            connections.Add(connection);
        }

        canvases[canvasIndex] = canvases[canvasIndex] with
        {
            Connections = connections
        };
        ApplyDataConnectionTargetParameter(canvases, canvasId, connection);
    }

    private static void RemoveConnection(List<CanvasDto> canvases, string canvasId, string connectionId)
    {
        var canvasIndex = FindCanvas(canvases, canvasId);
        var canvas = canvases[canvasIndex];
        var connection = canvas.Connections.FirstOrDefault(item => item.Id == connectionId);
        if (connection is null)
        {
            throw new InvalidOperationException($"Connection '{connectionId}' does not exist.");
        }

        ResetDataConnectionTargetParameter(canvases, canvasId, connection);
        var updatedCanvas = canvases[canvasIndex];
        canvases[canvasIndex] = updatedCanvas with
        {
            Connections = resolveToArrayResult78(),
        };
        IReadOnlyList<ConnectionDto> resolveToArrayResult78()
        {
            var toArrayResult80 = updatedCanvas.Connections
                .Where(connection => connection.Id != connectionId)
                .ToArray();
            return toArrayResult80;
        }
    }

    private static ConnectionDto NormalizeDataConnection(ConnectionDto connection) => connection with
    {
        DataSource = connection.Kind == ConnectionKindDto.Data ? DataSourceDto.PreviousNode : null,
    };
    private static void ApplyDataConnectionTargetParameter(List<CanvasDto> canvases, string canvasId, ConnectionDto connection)
    {
        if (connection.Kind != ConnectionKindDto.Data)
        {
            return;
        }

        var canvasIndex = FindCanvas(canvases, canvasId);
        var canvas = canvases[canvasIndex];
        var toListResult82 = canvas.Nodes.ToList();
        var nodeIndex = toListResult82.FindIndex(node => node.Id == connection.ToNodeId);
        if (nodeIndex < 0)
        {
            return;
        }

        var nodes = canvas.Nodes.ToList();
        var node = nodes[nodeIndex];
        var toListResult83 = node.Parameters.ToList();
        var parameterIndex = toListResult83.FindIndex(parameter =>
        {
            var equalsResult81 = string.Equals(parameter.Ui?.Id, connection.ToPortId, StringComparison.Ordinal);
            return equalsResult81;
        });
        if (parameterIndex < 0)
        {
            return;
        }

        var parameters = node.Parameters.ToList();
        var parameter = parameters[parameterIndex];
        var ui = parameter.Ui;
        if (ui is null)
        {
            return;
        }

        parameters[parameterIndex] = parameter with
        {
            Source = DataSourceDto.PreviousNode,
            Ui = ui with
            {
                SourceNodeId = connection.FromNodeId,
                SourcePortId = connection.FromPortId,
            },
        };
        nodes[nodeIndex] = node with
        {
            Parameters = parameters
        };
        canvases[canvasIndex] = canvas with
        {
            Nodes = nodes
        };
    }

    private static void ResetDataConnectionTargetParameter(List<CanvasDto> canvases, string canvasId, ConnectionDto connection)
    {
        if (connection.Kind != ConnectionKindDto.Data)
        {
            return;
        }

        var canvasIndex = FindCanvas(canvases, canvasId);
        var canvas = canvases[canvasIndex];
        var toListResult85 = canvas.Nodes.ToList();
        var nodeIndex = toListResult85.FindIndex(node => node.Id == connection.ToNodeId);
        if (nodeIndex < 0)
        {
            return;
        }

        var nodes = canvas.Nodes.ToList();
        var node = nodes[nodeIndex];
        var toListResult86 = node.Parameters.ToList();
        var parameterIndex = toListResult86.FindIndex(parameter =>
        {
            var equalsResult84 = string.Equals(parameter.Ui?.Id, connection.ToPortId, StringComparison.Ordinal);
            return equalsResult84;
        });
        if (parameterIndex < 0)
        {
            return;
        }

        var parameters = node.Parameters.ToList();
        var parameter = parameters[parameterIndex];
        var ui = parameter.Ui;
        if (ui is null || parameter.Source != DataSourceDto.PreviousNode || !string.Equals(ui.SourceNodeId, connection.FromNodeId, StringComparison.Ordinal) || !string.Equals(ui.SourcePortId, connection.FromPortId, StringComparison.Ordinal))
        {
            return;
        }

        parameters[parameterIndex] = parameter with
        {
            Source = DataSourceDto.Literal,
            Ui = ui with
            {
                SourceNodeId = null,
                SourcePortId = null,
            },
        };
        nodes[nodeIndex] = node with
        {
            Parameters = parameters
        };
        canvases[canvasIndex] = canvas with
        {
            Nodes = nodes
        };
    }

    private static void ReplaceScriptSource(List<CanvasDto> canvases, string canvasId, string nodeId, string source)
    {
        var canvasIndex = FindCanvas(canvases, canvasId);
        var canvas = canvases[canvasIndex];
        var nodes = canvas.Nodes.ToList();
        var nodeIndex = nodes.FindIndex(node => node.Id == nodeId);
        if (nodeIndex < 0 || nodes[nodeIndex].Script is null)
        {
            throw new InvalidOperationException($"Script node '{nodeId}' does not exist.");
        }

        var script = nodes[nodeIndex].Script!;
        nodes[nodeIndex] = nodes[nodeIndex] with
        {
            Script = script with
            {
                Source = source
            }
        };
        canvases[canvasIndex] = canvas with
        {
            Nodes = nodes
        };
    }

    private static int FindCanvas(List<CanvasDto> canvases, string canvasId)
    {
        var index = canvases.FindIndex(canvas => canvas.Id == canvasId);
        if (index < 0)
        {
            throw new InvalidOperationException($"Canvas '{canvasId}' does not exist.");
        }

        return index;
    }

    private static T ReadValue<T>(FlowPatchOperationDto operation)
    {
        if (operation.Value is not { } value)
        {
            throw new InvalidOperationException($"Operation '{operation.Operation}' requires a value.");
        }

        var evaluatedValue87 = value.Deserialize<T>(JsonOptions) ?? throw new InvalidOperationException($"Operation '{operation.Operation}' contains an invalid value.");
        return evaluatedValue87;
    }

    private static string Required(string? value, string name)
    {
        string evaluatedValue88 = string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"'{name}' is required.") : value.Trim();
        return evaluatedValue88;
    }

    private static void RequireObjectId(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"'{name}' must be explicitly provided for a structured flow patch.");
        }
    }

    private static void RequireMatchingId(string? expected, string actual, string name)
    {
        RequireObjectId(expected, name);
        if (!resolveEqualsResult89())
        {
            throw new InvalidOperationException($"The operation {name} does not match the value object's {name}.");
        }

        bool resolveEqualsResult89()
        {
            var trimResult = expected!.Trim();
            var equalsResult90 = string.Equals(trimResult, actual, StringComparison.Ordinal);
            return equalsResult90;
        }
    }

    private static void ValidateCanvasObjects(CanvasDto canvas)
    {
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in canvas.Nodes)
        {
            RequireObjectId(node.Id, "nodeId");
            if (!nodeIds.Add(node.Id))
            {
                throw new InvalidOperationException($"Node '{node.Id}' is duplicated in canvas '{canvas.Id}'.");
            }

            ValidateNodeObjects(node);
        }

        var connectionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var connection in canvas.Connections)
        {
            RequireObjectId(connection.Id, "connectionId");
            if (!connectionIds.Add(connection.Id))
            {
                throw new InvalidOperationException($"Connection '{connection.Id}' is duplicated in canvas '{canvas.Id}'.");
            }
        }
    }

    private static void ValidateNodeObjects(NodeDto node)
    {
        var parameterIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in node.Parameters)
        {
            RequireObjectId(parameter.Ui?.Id, "parameter.ui.id");
            if (!parameterIds.Add(parameter.Ui!.Id))
            {
                throw new InvalidOperationException($"Parameter '{parameter.Ui.Id}' is duplicated on node '{node.Id}'.");
            }
        }

        if (node.Script is not null)
        {
            if (!string.Equals(node.Script.NodeId, node.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Script node data must target node '{node.Id}'.");
            }

            var inputIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var input in node.Script.Inputs)
            {
                RequireObjectId(input.Id, "script input id");
                if (!inputIds.Add(input.Id!))
                {
                    throw new InvalidOperationException($"Script input '{input.Id}' is duplicated on node '{node.Id}'.");
                }
            }

            var outputIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var output in node.Script.Outputs)
            {
                RequireObjectId(output.Id, "script output id");
                if (!outputIds.Add(output.Id!))
                {
                    throw new InvalidOperationException($"Script output '{output.Id}' is duplicated on node '{node.Id}'.");
                }
            }
        }
    }
}

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
        AddScalar(changes, "runPolicy", Serialize(before.RunPolicy), Serialize(after.RunPolicy));
        var beforeCanvases = before.Canvases.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var afterCanvases = after.Canvases.ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (var id in beforeCanvases.Keys.Union(afterCanvases.Keys).Order(StringComparer.Ordinal))
        {
            if (!beforeCanvases.TryGetValue(id, out var oldCanvas))
            {
                var serialize = Serialize(afterCanvases[id]);
                FlowDiffItemDto flowDiffItemDto = new("canvas.added", $"canvases.{id}", null, serialize);
                changes.Add(flowDiffItemDto);
                continue;
            }

            if (!afterCanvases.TryGetValue(id, out var newCanvas))
            {
                var oldCanvasJson = Serialize(oldCanvas);
                FlowDiffItemDto mappedFlowDiffItemDto = new("canvas.removed", $"canvases.{id}", oldCanvasJson, null);
                changes.Add(mappedFlowDiffItemDto);
                continue;
            }

            var lifecycleText = oldCanvas.Lifecycle.ToString();
            var mappedAfter = newCanvas.Lifecycle.ToString();
            AddScalar(changes, $"canvases.{id}.lifecycle", lifecycleText, mappedAfter);
            AddScalar(changes, $"canvases.{id}.name", oldCanvas.Name, newCanvas.Name);
            CompareEntities(changes, $"canvases.{id}.nodes", oldCanvas.Nodes, newCanvas.Nodes, static node => node.Id, static node =>
            {
                return Serialize(node);
            }, "node", ref addedNodes, ref removedNodes, ref changedNodes);
            CompareEntities(changes, $"canvases.{id}.connections", oldCanvas.Connections, newCanvas.Connections, static connection => connection.Id, static connection =>
            {
                return Serialize(connection);
            }, "connection", ref addedConnections, ref removedConnections, ref changedConnections);
        }

        var baseChecksum = GetChecksum(before);
        var candidateChecksum = GetChecksum(after);
        return new(after.Id, before.Version, after.Version == before.Version ? null : after.Version, baseChecksum, candidateChecksum, changes, addedNodes, removedNodes, changedNodes, addedConnections, removedConnections, changedConnections);
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
        var canonicalJson = Serialize(canonical);
        var bytes = Encoding.UTF8.GetBytes(canonicalJson);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static FlowDiffDto RedactSensitive(FlowDiffDto diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        return diff with
        {
            Changes = diff.Changes.Select(static change =>
            {
                return change with
                {
                    Before = RedactJson(change.Before),
                    After = RedactJson(change.After),
                    IsSensitive = IsSensitiveChange(change) || change.IsSensitive,
                };
            }).ToArray(),
        };
    }

    public static FlowDefinitionDto RedactSensitive(FlowDefinitionDto definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition with
        {
            Canvases = definition.Canvases.Select(static canvas =>
            {
                return canvas with
                {
                    Nodes = canvas.Nodes.Select(static node =>
                    {
                        return node with
                        {
                            Parameters = node.Parameters.Select(static parameter => parameter with { ValueJson = null, Ui = parameter.Ui is null ? null : parameter.Ui with { LiteralValue = null, ProjectInputKey = null, Expression = null, }, }).ToArray(),
                            Script = node.Script is null ? null : node.Script with
                            {
                                Source = "[redacted]"
                            },
                        };
                    }).ToArray(),
                };
            }).ToArray(),
        };
    }

    private static bool IsSensitiveChange(FlowDiffItemDto change)
    {
        return change.Path.Contains(".nodes.", StringComparison.Ordinal) || change.Path.Contains(".canvases.", StringComparison.Ordinal) && (change.Before?.Contains("parameters", StringComparison.OrdinalIgnoreCase) == true || change.After?.Contains("parameters", StringComparison.OrdinalIgnoreCase) == true);
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
            return node.ToJsonString(JsonOptions);
        }
        catch (JsonException)
        {
            // A value that looked like structured JSON but could not be
            // parsed is safer to omit than to return unredacted.
            // 看似结构化 JSON 却无法解析的值应予以省略，避免返回未经脱敏的内容。
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
        foreach (var id in oldItems.Keys.Union(newItems.Keys).Order(StringComparer.Ordinal))
        {
            if (!oldItems.TryGetValue(id, out var oldItem))
            {
                added++;
                var mappedAfter = serialize(newItems[id]);
                FlowDiffItemDto flowDiffItemDto = new($"{kind}.added", $"{path}.{id}", null, mappedAfter);
                changes.Add(flowDiffItemDto);
            }
            else if (!newItems.TryGetValue(id, out var newItem))
            {
                removed++;
                var oldItemJson = serialize(oldItem);
                FlowDiffItemDto mappedFlowDiffItemDto = new($"{kind}.removed", $"{path}.{id}", oldItemJson, null);
                changes.Add(mappedFlowDiffItemDto);
            }
            else
            {
                if (!ItemsEqual())
                {
                    changed++;
                    var oldItemJson = serialize(oldItem);
                    var newItemJson = serialize(newItem);
                    FlowDiffItemDto resolvedFlowDiffItemDto = new($"{kind}.changed", $"{path}.{id}", oldItemJson, newItemJson);
                    changes.Add(resolvedFlowDiffItemDto);
                }

                bool ItemsEqual()
                {
                    var oldItemJson = serialize(oldItem);
                    var newItemJson = serialize(newItem);
                    return string.Equals(oldItemJson, newItemJson, StringComparison.Ordinal);
                }
            }
        }
    }

    private static void AddScalar(List<FlowDiffItemDto> changes, string path, string? before, string? after)
    {
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            FlowDiffItemDto flowDiffItemDto = new("property.changed", path, before, after);
            changes.Add(flowDiffItemDto);
        }
    }

    private static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
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
    /// MCP 边界只提供规范的 v2 操作；契约规范化器完成校验后，在此转换为现有修改实现使用的旧版内部封装。
    /// </summary>
    public FlowDefinitionDto Apply(FlowDefinitionDto definition, IReadOnlyList<FlowPatchCanonicalOperationDto> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var mappedOperations = operations.Select(FlowPatchContractNormalizer.ToLegacyOperation).ToArray();
        return Apply(definition, mappedOperations);
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
                    AddCanvas(canvases, ReadValue<CanvasDto>(operation), false);
                    break;
                case FlowPatchOperationKindDto.UpdateCanvas:
                    ReplaceCanvas(canvases, Required(operation.CanvasId, "canvasId"), ReadValue<CanvasDto>(operation));
                    break;
                case FlowPatchOperationKindDto.RemoveCanvas:
                    RemoveCanvas(canvases, Required(operation.CanvasId, "canvasId"));
                    break;
                case FlowPatchOperationKindDto.AddNode:
                    AddNode(canvases, Required(operation.CanvasId, "canvasId"), null, ReadValue<NodeDto>(operation), false);
                    break;
                case FlowPatchOperationKindDto.ReplaceNode:
                    AddNode(canvases, Required(operation.CanvasId, "canvasId"), Required(operation.NodeId, "nodeId"), ReadValue<NodeDto>(operation), true);
                    break;
                case FlowPatchOperationKindDto.RemoveNode:
                    RemoveNode(canvases, Required(operation.CanvasId, "canvasId"), Required(operation.NodeId, "nodeId"));
                    break;
                case FlowPatchOperationKindDto.SetNodeParameter:
                    SetNodeParameter(canvases, Required(operation.CanvasId, "canvasId"), Required(operation.NodeId, "nodeId"), Required(operation.ParameterId, "parameterId"), ReadValue<NodeParameterDto>(operation));
                    break;
                case FlowPatchOperationKindDto.AddNodeParameter:
                    AddNodeParameter(canvases, Required(operation.CanvasId, "canvasId"), Required(operation.NodeId, "nodeId"), ReadValue<NodeParameterDto>(operation));
                    break;
                case FlowPatchOperationKindDto.RemoveNodeParameter:
                    RemoveNodeParameter(canvases, Required(operation.CanvasId, "canvasId"), Required(operation.NodeId, "nodeId"), Required(operation.ParameterId, "parameterId"));
                    break;
                case FlowPatchOperationKindDto.AddConnection:
                    AddConnection(canvases, Required(operation.CanvasId, "canvasId"), null, ReadValue<ConnectionDto>(operation), false);
                    break;
                case FlowPatchOperationKindDto.ReplaceConnection:
                    AddConnection(canvases, Required(operation.CanvasId, "canvasId"), Required(operation.ConnectionId, "connectionId"), ReadValue<ConnectionDto>(operation), true);
                    break;
                case FlowPatchOperationKindDto.RemoveConnection:
                    RemoveConnection(canvases, Required(operation.CanvasId, "canvasId"), Required(operation.ConnectionId, "connectionId"));
                    break;
                case FlowPatchOperationKindDto.SetEntryNode:
                    entryNodeId = ReadValue<string>(operation);
                    break;
                case FlowPatchOperationKindDto.SetRunPolicy:
                    runPolicy = ReadValue<FlowRunPolicyDto>(operation);
                    break;
                case FlowPatchOperationKindDto.ReplaceScriptSource:
                    ReplaceScriptSource(canvases, Required(operation.CanvasId, "canvasId"), Required(operation.NodeId, "nodeId"), ReadValue<string>(operation));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operations), operation.Operation, "The flow patch operation is not supported. 不支持此流程补丁操作。");
            }
        }

        // Deleting the entry node is valid for an editor draft. Keep the
        // flow metadata consistent with the final canvas contents so the
        // persistence validator can accept an intentionally empty draft.
        // 删除入口节点对编辑器草稿是合法的。依据最终画布内容同步流程元数据，
        // 这样持久化校验才能接受刻意保留的空白草稿。
        entryNodeId = NormalizeEntryNodeId(canvases, entryNodeId);
        return definition with
        {
            Canvases = canvases,
            EntryNodeId = entryNodeId,
            RunPolicy = runPolicy,
            Checksum = FlowDiffService.GetChecksum(definition with { Canvases = canvases, EntryNodeId = entryNodeId, RunPolicy = runPolicy })
        };
    }

    private static void AddCanvas(List<CanvasDto> canvases, CanvasDto canvas, bool replace)
    {
        RequireObjectId(canvas.Id, "canvasId");
        ValidateCanvasObjects(canvas);
        var index = canvases.FindIndex(item => item.Id == canvas.Id);
        if (index >= 0 && !replace)
        {
            throw new InvalidOperationException($"Canvas '{canvas.Id}' already exists. 指定画布已存在。");
        }

        if (index < 0 && replace)
        {
            throw new InvalidOperationException($"Canvas '{canvas.Id}' does not exist. 指定画布不存在。");
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
            throw new InvalidOperationException($"Canvas '{canvasId}' does not exist. 指定画布不存在。");
        }

        if (canvases[index].Nodes.Count > 0 || canvases[index].Connections.Count > 0)
        {
            throw new InvalidOperationException($"Canvas '{canvasId}' must be empty before removal. 移除画布前必须先清空画布。");
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
            throw new InvalidOperationException($"Node '{node.Id}' already exists. 指定节点已存在。");
        }

        if (index < 0 && replace)
        {
            throw new InvalidOperationException($"Node '{node.Id}' does not exist. 指定节点不存在。");
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
            throw new InvalidOperationException($"Node '{nodeId}' does not exist. 指定节点不存在。");
        }

        if (canvas.Connections.Any(connection => connection.FromNodeId == nodeId || connection.ToNodeId == nodeId))
        {
            throw new InvalidOperationException($"Connections for node '{nodeId}' must be removed first. 必须先移除指定节点的连接。");
        }

        canvases[canvasIndex] = canvas with
        {
            Nodes = canvas.Nodes.Where(node => node.Id != nodeId).ToArray()
        };
    }

    private static string NormalizeEntryNodeId(IReadOnlyList<CanvasDto> canvases, string entryNodeId)
    {
        return string.IsNullOrWhiteSpace(entryNodeId) || canvases.Any(canvas =>
        {
            return canvas.Nodes.Any(node => node.Id == entryNodeId);
        }) ? entryNodeId ?? string.Empty : string.Empty;
    }

    private static void SetNodeParameter(List<CanvasDto> canvases, string canvasId, string nodeId, string parameterId, NodeParameterDto parameter)
    {
        RequireObjectId(parameterId, "parameterId");
        RequireObjectId(parameter.Ui?.Id, "value.ui.id");
        if (!string.Equals(parameterId, parameter.Ui!.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The parameter ID does not match value.ui.id. 参数 ID 与 value.ui.id 不一致。");
        }

        var canvasIndex = FindCanvas(canvases, canvasId);
        var canvas = canvases[canvasIndex];
        var nodeIndex = canvas.Nodes.ToList().FindIndex(node => node.Id == nodeId);
        if (nodeIndex < 0)
        {
            throw new InvalidOperationException($"Node '{nodeId}' does not exist. 指定节点不存在。");
        }

        var nodes = canvas.Nodes.ToList();
        var node = nodes[nodeIndex];
        var parameters = node.Parameters.ToList();
        var index = parameters.FindIndex(item =>
        {
            return string.Equals(item.Ui?.Id, parameterId, StringComparison.Ordinal);
        });
        if (index < 0)
        {
            throw new InvalidOperationException($"Parameter '{parameterId}' does not exist on node '{nodeId}'. 指定节点上不存在该参数。");
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
        var nodeIndex = canvas.Nodes.ToList().FindIndex(node => node.Id == nodeId);
        if (nodeIndex < 0)
        {
            throw new InvalidOperationException($"Node '{nodeId}' does not exist. 指定节点不存在。");
        }

        var nodes = canvas.Nodes.ToList();
        var node = nodes[nodeIndex];
        if (node.Parameters.Any(item =>
        {
            return string.Equals(item.Ui?.Id, parameterId, StringComparison.Ordinal);
        }))
        {
            throw new InvalidOperationException($"Parameter '{parameterId}' already exists on node '{nodeId}'. 指定节点上已存在该参数。");
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
        var nodeIndex = canvas.Nodes.ToList().FindIndex(node => node.Id == nodeId);
        if (nodeIndex < 0)
        {
            throw new InvalidOperationException($"Node '{nodeId}' does not exist. 指定节点不存在。");
        }

        if (canvas.Connections.Any(connection => connection.Kind == ConnectionKindDto.Data && connection.ToNodeId == nodeId && connection.ToPortId == parameterId))
        {
            throw new InvalidOperationException($"Data connections for parameter '{parameterId}' on node '{nodeId}' must be removed first. 必须先移除指定节点上该参数的数据连接。");
        }

        var nodes = canvas.Nodes.ToList();
        var node = nodes[nodeIndex];
        var parameters = node.Parameters.ToList();
        var parameterIndex = parameters.FindIndex(item =>
        {
            return string.Equals(item.Ui?.Id, parameterId, StringComparison.Ordinal);
        });
        if (parameterIndex < 0)
        {
            throw new InvalidOperationException($"Parameter '{parameterId}' does not exist on node '{nodeId}'. 指定节点上不存在该参数。");
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
            throw new InvalidOperationException($"Connection '{connection.Id}' already exists. 指定连接已存在。");
        }

        if (index < 0 && replace)
        {
            throw new InvalidOperationException($"Connection '{connection.Id}' does not exist. 指定连接不存在。");
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
        var connection = canvases[canvasIndex].Connections.FirstOrDefault(item => item.Id == connectionId);
        if (connection is null)
        {
            throw new InvalidOperationException($"Connection '{connectionId}' does not exist. 指定连接不存在。");
        }

        ResetDataConnectionTargetParameter(canvases, canvasId, connection);
        var updatedCanvas = canvases[canvasIndex];
        canvases[canvasIndex] = updatedCanvas with
        {
            Connections = updatedCanvas.Connections.Where(connection => connection.Id != connectionId).ToArray(),
        };
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
        var nodeIndex = canvas.Nodes.ToList().FindIndex(node => node.Id == connection.ToNodeId);
        if (nodeIndex < 0)
        {
            return;
        }

        var nodes = canvas.Nodes.ToList();
        var node = nodes[nodeIndex];
        var parameterIndex = node.Parameters.ToList().FindIndex(parameter =>
        {
            return string.Equals(parameter.Ui?.Id, connection.ToPortId, StringComparison.Ordinal);
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
        var nodeIndex = canvas.Nodes.ToList().FindIndex(node => node.Id == connection.ToNodeId);
        if (nodeIndex < 0)
        {
            return;
        }

        var nodes = canvas.Nodes.ToList();
        var node = nodes[nodeIndex];
        var parameterIndex = node.Parameters.ToList().FindIndex(parameter =>
        {
            return string.Equals(parameter.Ui?.Id, connection.ToPortId, StringComparison.Ordinal);
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
            throw new InvalidOperationException($"Script node '{nodeId}' does not exist. 指定脚本节点不存在。");
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
            throw new InvalidOperationException($"Canvas '{canvasId}' does not exist. 指定画布不存在。");
        }

        return index;
    }

    private static T ReadValue<T>(FlowPatchOperationDto operation)
    {
        if (operation.Value is not { } value)
        {
            throw new InvalidOperationException($"Operation '{operation.Operation}' requires a value. 此操作必须提供值。");
        }
        return value.Deserialize<T>(JsonOptions) ?? throw new InvalidOperationException($"Operation '{operation.Operation}' contains an invalid value. 操作包含无效的值。");
    }

    private static string Required(string? value, string name)
    {
        return string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"'{name}' is required. 必须提供该字段。") : value.Trim();
    }

    private static void RequireObjectId(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"'{name}' must be explicitly provided for a structured flow patch. 结构化流程补丁必须显式提供该字段。");
        }
    }

    private static void RequireMatchingId(string? expected, string actual, string name)
    {
        RequireObjectId(expected, name);
        if (!string.Equals(expected!.Trim(), actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The operation {name} does not match the value object's {name}. 操作字段与值对象中的对应字段不一致。");
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
                throw new InvalidOperationException($"Node '{node.Id}' is duplicated in canvas '{canvas.Id}'. 画布中存在重复的节点 ID。");
            }

            ValidateNodeObjects(node);
        }

        var connectionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var connection in canvas.Connections)
        {
            RequireObjectId(connection.Id, "connectionId");
            if (!connectionIds.Add(connection.Id))
            {
                throw new InvalidOperationException($"Connection '{connection.Id}' is duplicated in canvas '{canvas.Id}'. 画布中存在重复的连接 ID。");
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
                throw new InvalidOperationException($"Parameter '{parameter.Ui.Id}' is duplicated on node '{node.Id}'. 节点上存在重复的参数 ID。");
            }
        }

        if (node.Script is not null)
        {
            if (!string.Equals(node.Script.NodeId, node.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Script node data must target node '{node.Id}'. 脚本节点数据必须指向指定节点。");
            }

            var inputIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var input in node.Script.Inputs)
            {
                RequireObjectId(input.Id, "script input id");
                if (!inputIds.Add(input.Id!))
                {
                    throw new InvalidOperationException($"Script input '{input.Id}' is duplicated on node '{node.Id}'. 节点上存在重复的脚本输入 ID。");
                }
            }

            var outputIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var output in node.Script.Outputs)
            {
                RequireObjectId(output.Id, "script output id");
                if (!outputIds.Add(output.Id!))
                {
                    throw new InvalidOperationException($"Script output '{output.Id}' is duplicated on node '{node.Id}'. 节点上存在重复的脚本输出 ID。");
                }
            }
        }
    }
}

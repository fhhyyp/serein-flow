using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SereinFlow.Contracts;

namespace SereinFlow.Application;

public sealed class FlowDiffService
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateWebOptions();

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
                changes.Add(new("canvas.added", $"canvases.{id}", null, Serialize(afterCanvases[id])));
                continue;
            }
            if (!afterCanvases.TryGetValue(id, out var newCanvas))
            {
                changes.Add(new("canvas.removed", $"canvases.{id}", Serialize(oldCanvas), null));
                continue;
            }

            AddScalar(changes, $"canvases.{id}.lifecycle", oldCanvas.Lifecycle.ToString(), newCanvas.Lifecycle.ToString());
            AddScalar(changes, $"canvases.{id}.name", oldCanvas.Name, newCanvas.Name);
            CompareEntities(
                changes,
                $"canvases.{id}.nodes",
                oldCanvas.Nodes,
                newCanvas.Nodes,
                static node => node.Id,
                static node => Serialize(node),
                "node",
                ref addedNodes,
                ref removedNodes,
                ref changedNodes);
            CompareEntities(
                changes,
                $"canvases.{id}.connections",
                oldCanvas.Connections,
                newCanvas.Connections,
                static connection => connection.Id,
                static connection => Serialize(connection),
                "connection",
                ref addedConnections,
                ref removedConnections,
                ref changedConnections);
        }

        return new(
            after.Id,
            before.Version,
            after.Version == before.Version ? null : after.Version,
            GetChecksum(before),
            GetChecksum(after),
            changes,
            addedNodes,
            removedNodes,
            changedNodes,
            addedConnections,
            removedConnections,
            changedConnections);
    }

    public static string GetChecksum(FlowDefinitionDto definition)
    {
        var canonical = definition with { Checksum = string.Empty };
        var bytes = Encoding.UTF8.GetBytes(Serialize(canonical));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static FlowDiffDto RedactSensitive(FlowDiffDto diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        return diff with
        {
            Changes = diff.Changes
                .Select(static change => change with
                {
                    Before = RedactJson(change.Before),
                    After = RedactJson(change.After),
                    IsSensitive = IsSensitiveChange(change) || change.IsSensitive,
                })
                .ToArray(),
        };
    }

    public static FlowDefinitionDto RedactSensitive(FlowDefinitionDto definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition with
        {
            Canvases = definition.Canvases
                .Select(static canvas => canvas with
                {
                    Nodes = canvas.Nodes
                        .Select(static node => node with
                        {
                            Parameters = node.Parameters
                                .Select(static parameter => parameter with
                                {
                                    ValueJson = null,
                                    Ui = parameter.Ui is null
                                        ? null
                                        : parameter.Ui with
                                        {
                                            LiteralValue = null,
                                            ProjectInputKey = null,
                                            Expression = null,
                                        },
                                })
                                .ToArray(),
                            Script = node.Script is null
                                ? null
                                : node.Script with { Source = "[redacted]" },
                        })
                        .ToArray(),
                })
                .ToArray(),
        };
    }

    private static bool IsSensitiveChange(FlowDiffItemDto change)
        => change.Path.Contains(".nodes.", StringComparison.Ordinal)
            || change.Path.Contains(".canvases.", StringComparison.Ordinal)
                && (change.Before?.Contains("parameters", StringComparison.OrdinalIgnoreCase) == true
                    || change.After?.Contains("parameters", StringComparison.OrdinalIgnoreCase) == true);

    private static string? RedactJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;
        try
        {
            var node = JsonNode.Parse(value);
            if (node is null)
                return null;
            RedactJsonNode(node);
            return node.ToJsonString(JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void RedactJsonNode(JsonNode node)
    {
        if (node is JsonObject objectNode)
        {
            foreach (var property in objectNode.ToArray())
            {
                if (property.Key.Equals("source", StringComparison.OrdinalIgnoreCase)
                    || property.Key.Equals("valueJson", StringComparison.OrdinalIgnoreCase)
                    || property.Key.Equals("literalValue", StringComparison.OrdinalIgnoreCase)
                    || property.Key.Equals("projectInputKey", StringComparison.OrdinalIgnoreCase)
                    || property.Key.Equals("expression", StringComparison.OrdinalIgnoreCase))
                {
                    objectNode[property.Key] = null;
                    continue;
                }
                if (property.Value is not null)
                    RedactJsonNode(property.Value);
            }
        }
        else if (node is JsonArray arrayNode)
        {
            foreach (var child in arrayNode)
            {
                if (child is not null)
                    RedactJsonNode(child);
            }
        }
    }

    private static void CompareEntities<T>(
        List<FlowDiffItemDto> changes,
        string path,
        IReadOnlyList<T> before,
        IReadOnlyList<T> after,
        Func<T, string> key,
        Func<T, string> serialize,
        string kind,
        ref int added,
        ref int removed,
        ref int changed)
    {
        var oldItems = before.ToDictionary(key, StringComparer.Ordinal);
        var newItems = after.ToDictionary(key, StringComparer.Ordinal);
        foreach (var id in oldItems.Keys.Union(newItems.Keys).Order(StringComparer.Ordinal))
        {
            if (!oldItems.TryGetValue(id, out var oldItem))
            {
                added++;
                changes.Add(new($"{kind}.added", $"{path}.{id}", null, serialize(newItems[id])));
            }
            else if (!newItems.TryGetValue(id, out var newItem))
            {
                removed++;
                changes.Add(new($"{kind}.removed", $"{path}.{id}", serialize(oldItem), null));
            }
            else if (!string.Equals(serialize(oldItem), serialize(newItem), StringComparison.Ordinal))
            {
                changed++;
                changes.Add(new($"{kind}.changed", $"{path}.{id}", serialize(oldItem), serialize(newItem)));
            }
        }
    }

    private static void AddScalar(List<FlowDiffItemDto> changes, string path, string? before, string? after)
    {
        if (!string.Equals(before, after, StringComparison.Ordinal))
            changes.Add(new("property.changed", path, before, after));
    }

    private static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, JsonOptions);
}

public sealed class FlowPatchService
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateWebOptions();

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
                    ReplaceCanvas(
                        canvases,
                        Required(operation.CanvasId, "canvasId"),
                        ReadValue<CanvasDto>(operation));
                    break;
                case FlowPatchOperationKindDto.RemoveCanvas:
                    RemoveCanvas(canvases, Required(operation.CanvasId, "canvasId"));
                    break;
                case FlowPatchOperationKindDto.AddNode:
                    AddNode(canvases, Required(operation.CanvasId, "canvasId"), null, ReadValue<NodeDto>(operation), false);
                    break;
                case FlowPatchOperationKindDto.ReplaceNode:
                    AddNode(
                        canvases,
                        Required(operation.CanvasId, "canvasId"),
                        Required(operation.NodeId, "nodeId"),
                        ReadValue<NodeDto>(operation),
                        true);
                    break;
                case FlowPatchOperationKindDto.RemoveNode:
                    RemoveNode(canvases, Required(operation.CanvasId, "canvasId"), Required(operation.NodeId, "nodeId"));
                    break;
                case FlowPatchOperationKindDto.SetNodeParameter:
                    SetNodeParameter(
                        canvases,
                        Required(operation.CanvasId, "canvasId"),
                        Required(operation.NodeId, "nodeId"),
                        Required(operation.ParameterId, "parameterId"),
                        ReadValue<NodeParameterDto>(operation));
                    break;
                case FlowPatchOperationKindDto.AddConnection:
                    AddConnection(canvases, Required(operation.CanvasId, "canvasId"), null, ReadValue<ConnectionDto>(operation), false);
                    break;
                case FlowPatchOperationKindDto.ReplaceConnection:
                    AddConnection(
                        canvases,
                        Required(operation.CanvasId, "canvasId"),
                        Required(operation.ConnectionId, "connectionId"),
                        ReadValue<ConnectionDto>(operation),
                        true);
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
                    throw new ArgumentOutOfRangeException(nameof(operations), operation.Operation, "The flow patch operation is not supported.");
            }
        }

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
            throw new InvalidOperationException($"Canvas '{canvas.Id}' already exists.");
        if (index < 0 && replace)
            throw new InvalidOperationException($"Canvas '{canvas.Id}' does not exist.");
        if (index >= 0)
            canvases[index] = canvas;
        else
            canvases.Add(canvas);
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
            throw new InvalidOperationException($"Canvas '{canvasId}' does not exist.");
        if (canvases[index].Nodes.Count > 0 || canvases[index].Connections.Count > 0)
            throw new InvalidOperationException($"Canvas '{canvasId}' must be empty before removal.");
        canvases.RemoveAt(index);
    }

    private static void AddNode(
        List<CanvasDto> canvases,
        string canvasId,
        string? expectedNodeId,
        NodeDto node,
        bool replace)
    {
        RequireObjectId(node.Id, "nodeId");
        if (replace)
            RequireMatchingId(expectedNodeId, node.Id, "nodeId");
        ValidateNodeObjects(node);
        var canvasIndex = FindCanvas(canvases, canvasId);
        var nodes = canvases[canvasIndex].Nodes.ToList();
        var index = nodes.FindIndex(item => item.Id == node.Id);
        if (index >= 0 && !replace)
            throw new InvalidOperationException($"Node '{node.Id}' already exists.");
        if (index < 0 && replace)
            throw new InvalidOperationException($"Node '{node.Id}' does not exist.");
        if (index >= 0)
            nodes[index] = node;
        else
            nodes.Add(node);
        canvases[canvasIndex] = canvases[canvasIndex] with { Nodes = nodes };
    }

    private static void RemoveNode(List<CanvasDto> canvases, string canvasId, string nodeId)
    {
        var canvasIndex = FindCanvas(canvases, canvasId);
        var canvas = canvases[canvasIndex];
        if (!canvas.Nodes.Any(node => node.Id == nodeId))
            throw new InvalidOperationException($"Node '{nodeId}' does not exist.");
        if (canvas.Connections.Any(connection => connection.FromNodeId == nodeId || connection.ToNodeId == nodeId))
            throw new InvalidOperationException($"Connections for node '{nodeId}' must be removed first.");
        canvases[canvasIndex] = canvas with { Nodes = canvas.Nodes.Where(node => node.Id != nodeId).ToArray() };
    }

    private static void SetNodeParameter(
        List<CanvasDto> canvases,
        string canvasId,
        string nodeId,
        string parameterId,
        NodeParameterDto parameter)
    {
        RequireObjectId(parameterId, "parameterId");
        RequireObjectId(parameter.Ui?.Id, "value.ui.id");
        if (!string.Equals(parameterId, parameter.Ui!.Id, StringComparison.Ordinal))
            throw new InvalidOperationException("The parameter ID does not match value.ui.id.");

        var canvasIndex = FindCanvas(canvases, canvasId);
        var canvas = canvases[canvasIndex];
        var nodeIndex = canvas.Nodes.ToList().FindIndex(node => node.Id == nodeId);
        if (nodeIndex < 0)
            throw new InvalidOperationException($"Node '{nodeId}' does not exist.");
        var nodes = canvas.Nodes.ToList();
        var node = nodes[nodeIndex];
        var parameters = node.Parameters.ToList();
        var index = parameters.FindIndex(item => string.Equals(item.Ui?.Id, parameterId, StringComparison.Ordinal));
        if (index < 0)
            throw new InvalidOperationException($"Parameter '{parameterId}' does not exist on node '{nodeId}'.");
        parameters[index] = parameter;
        nodes[nodeIndex] = node with { Parameters = parameters };
        canvases[canvasIndex] = canvas with { Nodes = nodes };
    }

    private static void AddConnection(
        List<CanvasDto> canvases,
        string canvasId,
        string? expectedConnectionId,
        ConnectionDto connection,
        bool replace)
    {
        RequireObjectId(connection.Id, "connectionId");
        if (replace)
            RequireMatchingId(expectedConnectionId, connection.Id, "connectionId");
        var canvasIndex = FindCanvas(canvases, canvasId);
        var connections = canvases[canvasIndex].Connections.ToList();
        var index = connections.FindIndex(item => item.Id == connection.Id);
        if (index >= 0 && !replace)
            throw new InvalidOperationException($"Connection '{connection.Id}' already exists.");
        if (index < 0 && replace)
            throw new InvalidOperationException($"Connection '{connection.Id}' does not exist.");
        if (index >= 0)
            connections[index] = connection;
        else
            connections.Add(connection);
        canvases[canvasIndex] = canvases[canvasIndex] with { Connections = connections };
    }

    private static void RemoveConnection(List<CanvasDto> canvases, string canvasId, string connectionId)
    {
        var canvasIndex = FindCanvas(canvases, canvasId);
        var canvas = canvases[canvasIndex];
        if (!canvas.Connections.Any(connection => connection.Id == connectionId))
            throw new InvalidOperationException($"Connection '{connectionId}' does not exist.");
        canvases[canvasIndex] = canvas with { Connections = canvas.Connections.Where(connection => connection.Id != connectionId).ToArray() };
    }

    private static void ReplaceScriptSource(List<CanvasDto> canvases, string canvasId, string nodeId, string source)
    {
        var canvasIndex = FindCanvas(canvases, canvasId);
        var canvas = canvases[canvasIndex];
        var nodes = canvas.Nodes.ToList();
        var nodeIndex = nodes.FindIndex(node => node.Id == nodeId);
        if (nodeIndex < 0 || nodes[nodeIndex].Script is null)
            throw new InvalidOperationException($"Script node '{nodeId}' does not exist.");
        var script = nodes[nodeIndex].Script!;
        nodes[nodeIndex] = nodes[nodeIndex] with { Script = script with { Source = source } };
        canvases[canvasIndex] = canvas with { Nodes = nodes };
    }

    private static int FindCanvas(List<CanvasDto> canvases, string canvasId)
    {
        var index = canvases.FindIndex(canvas => canvas.Id == canvasId);
        if (index < 0)
            throw new InvalidOperationException($"Canvas '{canvasId}' does not exist.");
        return index;
    }

    private static T ReadValue<T>(FlowPatchOperationDto operation)
    {
        if (operation.Value is not { } value)
            throw new InvalidOperationException($"Operation '{operation.Operation}' requires a value.");
        return value.Deserialize<T>(JsonOptions)
            ?? throw new InvalidOperationException($"Operation '{operation.Operation}' contains an invalid value.");
    }

    private static string Required(string? value, string name)
        => string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"'{name}' is required.") : value.Trim();

    private static void RequireObjectId(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"'{name}' must be explicitly provided for a structured flow patch.");
    }

    private static void RequireMatchingId(string? expected, string actual, string name)
    {
        RequireObjectId(expected, name);
        if (!string.Equals(expected!.Trim(), actual, StringComparison.Ordinal))
            throw new InvalidOperationException($"The operation {name} does not match the value object's {name}.");
    }

    private static void ValidateCanvasObjects(CanvasDto canvas)
    {
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in canvas.Nodes)
        {
            RequireObjectId(node.Id, "nodeId");
            if (!nodeIds.Add(node.Id))
                throw new InvalidOperationException($"Node '{node.Id}' is duplicated in canvas '{canvas.Id}'.");
            ValidateNodeObjects(node);
        }

        var connectionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var connection in canvas.Connections)
        {
            RequireObjectId(connection.Id, "connectionId");
            if (!connectionIds.Add(connection.Id))
                throw new InvalidOperationException($"Connection '{connection.Id}' is duplicated in canvas '{canvas.Id}'.");
        }
    }

    private static void ValidateNodeObjects(NodeDto node)
    {
        var parameterIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in node.Parameters)
        {
            RequireObjectId(parameter.Ui?.Id, "parameter.ui.id");
            if (!parameterIds.Add(parameter.Ui!.Id))
                throw new InvalidOperationException($"Parameter '{parameter.Ui.Id}' is duplicated on node '{node.Id}'.");
        }

        if (node.Script is not null)
        {
            if (!string.Equals(node.Script.NodeId, node.Id, StringComparison.Ordinal))
                throw new InvalidOperationException($"Script node data must target node '{node.Id}'.");

            var inputIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var input in node.Script.Inputs)
            {
                RequireObjectId(input.Id, "script input id");
                if (!inputIds.Add(input.Id!))
                    throw new InvalidOperationException($"Script input '{input.Id}' is duplicated on node '{node.Id}'.");
            }

            var outputIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var output in node.Script.Outputs)
            {
                RequireObjectId(output.Id, "script output id");
                if (!outputIds.Add(output.Id!))
                    throw new InvalidOperationException($"Script output '{output.Id}' is duplicated on node '{node.Id}'.");
            }
        }
    }
}

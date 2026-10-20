namespace SereinFlow.Domain;
public sealed class FlowDefinition
{
    public const int CurrentSchemaVersion = 5;
    private FlowDefinition(Guid id, int schemaVersion, long version, IReadOnlyList<CanvasDefinition> canvases, string entryNodeId, string checksum, FlowRunPolicy runPolicy)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Version = version;
        Canvases = canvases;
        EntryNodeId = entryNodeId;
        Checksum = checksum;
        RunPolicy = runPolicy;
    }

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public long Version { get; }
    public IReadOnlyList<CanvasDefinition> Canvases { get; }
    public string EntryNodeId { get; }
    public string Checksum { get; }
    public FlowRunPolicy RunPolicy { get; }

    public static FlowDefinition Create(Guid id, long version, IEnumerable<CanvasDefinition> canvases, string entryNodeId, int schemaVersion = CurrentSchemaVersion, string checksum = "", FlowRunPolicy? runPolicy = null)
    {
        if (canvases is null)
        {
            throw new ArgumentNullException(nameof(canvases), "Flow canvases cannot be null. 流程画布集合不能为空。");
        }

        if (id == Guid.Empty)
        {
            throw new ArgumentException("Flow ID cannot be empty. 流程 ID 不能为空。", nameof(id));
        }

        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Flow version must be positive. 流程版本必须为正数。");
        }

        if (schemaVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Schema version must be positive. Schema 版本必须为正数。");
        }

        var toArrayResult = canvases.ToArray();
        var evaluatedValue = entryNodeId?.Trim() ?? string.Empty;
        var flowDefinition = new FlowDefinition(id, schemaVersion, version, toArrayResult, evaluatedValue, checksum ?? string.Empty, runPolicy ?? FlowRunPolicy.Parallel);
        // An empty entry node is a valid editor draft. Execution validates this separately.
        // 空入口节点是有效的编辑器草稿状态，执行时会单独进行校验。
        return flowDefinition;
    }

    public IReadOnlyList<DomainDiagnostic> Validate()
    {
        var diagnostics = new List<DomainDiagnostic>();
        if (SchemaVersion != CurrentSchemaVersion)
        {
            DomainDiagnostic evaluatedValue1 = new(DomainErrorCodes.NodeTypeRemoved, $"Flow schema version {SchemaVersion} is no longer supported; schema {CurrentSchemaVersion} is required. 流程 Schema 版本 {SchemaVersion} 已不再支持，必须使用 Schema {CurrentSchemaVersion}。", "schemaVersion");
            // Schema v5 removes Condition and makes parameter IDs the binding identity.
            // Older payloads are intentionally rejected instead of being silently upgraded.
            // Schema v5 移除了 Condition，并将参数 ID 作为绑定身份；旧流程明确拒绝，不再静默升级。
            diagnostics.Add(evaluatedValue1);
        }

        var canvasIds = new HashSet<string>(StringComparer.Ordinal);
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        var connectionIds = new HashSet<string>(StringComparer.Ordinal);
        var selectManyResult16 = Canvases.SelectMany(static canvas => canvas.Connections);
        var whereResult = selectManyResult16.Where(static connection => connection.Kind == ConnectionKind.Data);
        var selectResult = whereResult.Select(static connection =>
        {
            var createParameterKeyResult = CreateParameterKey(connection.ToNodeId, connection.ToPortId);
            return createParameterKeyResult;
        });
        var dataBoundParameters = selectResult.ToHashSet(StringComparer.Ordinal);
        var selectManyResult17 = Canvases.SelectMany(static canvas => canvas.Nodes);
        var whereResult18 = selectManyResult17.Where(static node =>
        {
            var evaluatedValue2 = node.Type == NodeType.FlowCall && !string.IsNullOrWhiteSpace(node.Runtime?.TargetNodeId);
            return evaluatedValue2;
        });
        var selectResult19 = whereResult18.Select(static node => node.Runtime!.TargetNodeId!);
        var flowCallTargetNodes = selectResult19.ToHashSet(StringComparer.Ordinal);
        foreach (var canvas in Canvases)
        {
            if (!canvasIds.Add(canvas.Id))
            {
                DomainDiagnostic evaluatedValue3 = new(DomainErrorCodes.DuplicateCanvasId, $"Canvas '{canvas.Id}' is duplicated. 画布“{canvas.Id}”重复。", $"canvases.{canvas.Id}");
                diagnostics.Add(evaluatedValue3);
            }

            foreach (var node in canvas.Nodes)
            {
                if (!Enum.IsDefined(node.Type))
                {
                    DomainDiagnostic evaluatedValue4 = new(DomainErrorCodes.NodeTypeRemoved, $"Node type '{(int)node.Type}' has been removed and cannot execute. 节点类型“{(int)node.Type}”已移除，不能执行。", $"nodes.{node.Id}.type");
                    diagnostics.Add(evaluatedValue4);
                }

                if (!nodeIds.Add(node.Id))
                {
                    DomainDiagnostic evaluatedValue5 = new(DomainErrorCodes.DuplicateNodeId, $"Node '{node.Id}' is duplicated. 节点“{node.Id}”重复。", $"nodes.{node.Id}");
                    diagnostics.Add(evaluatedValue5);
                }

                var parameterNames = new HashSet<string>(StringComparer.Ordinal);
                var parameterIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var parameter in node.Parameters)
                {
                    if (!parameterNames.Add(parameter.Name))
                    {
                        DomainDiagnostic evaluatedValue6 = new(DomainErrorCodes.DuplicateParameterName, $"Parameter '{parameter.Name}' is duplicated on node '{node.Id}'. 节点“{node.Id}”中参数“{parameter.Name}”重复。", $"nodes.{node.Id}.parameters.{parameter.Name}");
                        diagnostics.Add(evaluatedValue6);
                    }

                    if (!parameterIds.Add(parameter.Id))
                    {
                        DomainDiagnostic evaluatedValue7 = new(DomainErrorCodes.DuplicateParameterName, $"Parameter ID '{parameter.Id}' is duplicated on node '{node.Id}'. 节点“{node.Id}”中参数 ID“{parameter.Id}”重复。", $"nodes.{node.Id}.parameters.{parameter.Id}");
                        diagnostics.Add(evaluatedValue7);
                    }

                    if (parameter.Source == DataSource.Literal && parameter.EnumMetadata is not null && !string.IsNullOrWhiteSpace(parameter.ValueJson) && !IsValidEnumLiteral(parameter.ValueJson, parameter.EnumMetadata))
                    {
                        DomainDiagnostic evaluatedValue8 = new(DomainErrorCodes.InvalidEnumLiteral, $"Enum literal '{parameter.ValueJson}' is not valid for parameter '{parameter.Name}'. 枚举字面量“{parameter.ValueJson}”不是参数“{parameter.Name}”的有效选项。", $"nodes.{node.Id}.parameters.{parameter.Name}");
                        diagnostics.Add(evaluatedValue8);
                    }

                    if (parameter.Required && parameter.Source == DataSource.Literal && string.IsNullOrWhiteSpace(parameter.ValueJson) && !resolveContainsResult() // A FlowCall provides the public entry's input through
 // a separate mapping. Its completeness is validated by
                    // ExecutionPlanBuilder after target resolution.
                    // FlowCall 通过独立映射提供公开入口参数；映射完整性将在
                    // ExecutionPlanBuilder 解析目标节点后校验。
                    && !flowCallTargetNodes.Contains(node.Id))
                    {
                        DomainDiagnostic evaluatedValue9 = new(DomainErrorCodes.MissingRequiredParameter, $"Required parameter '{parameter.Name}' on node '{node.Id}' has no literal value. 节点“{node.Id}”的必需参数“{parameter.Name}”没有字面量值。", $"nodes.{node.Id}.parameters.{parameter.Name}");
                        diagnostics.Add(evaluatedValue9);
                    }

                    bool resolveContainsResult()
                    {
                        var createParameterKeyResult10 = CreateParameterKey(node.Id, parameter.Id);
                        var containsResult = dataBoundParameters.Contains(createParameterKeyResult10);
                        return containsResult;
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(EntryNodeId) && !nodeIds.Contains(EntryNodeId))
        {
            DomainDiagnostic evaluatedValue11 = new(DomainErrorCodes.UnknownEntryNode, $"Entry node '{EntryNodeId}' does not exist. 入口节点“{EntryNodeId}”不存在。", "entryNodeId");
            diagnostics.Add(evaluatedValue11);
        }

        foreach (var canvas in Canvases)
        {
            foreach (var connection in canvas.Connections)
            {
                if (!connectionIds.Add(connection.Id))
                {
                    DomainDiagnostic evaluatedValue12 = new(DomainErrorCodes.DuplicateConnectionId, $"Connection '{connection.Id}' is duplicated. 连接“{connection.Id}”重复。", $"connections.{connection.Id}");
                    diagnostics.Add(evaluatedValue12);
                }

                if (!nodeIds.Contains(connection.FromNodeId) || !nodeIds.Contains(connection.ToNodeId))
                {
                    DomainDiagnostic evaluatedValue13 = new(DomainErrorCodes.UnknownConnectionEndpoint, $"Connection '{connection.Id}' references an unknown node. 连接“{connection.Id}”引用了未知节点。", $"connections.{connection.Id}");
                    diagnostics.Add(evaluatedValue13);
                }

                if (connection.Kind == ConnectionKind.Data && nodeIds.Contains(connection.ToNodeId))
                {
                    var selectManyResult = Canvases.SelectMany(static item => item.Nodes);
                    var targetNode = selectManyResult.First(item =>
                    {
                        var equalsResult = string.Equals(item.Id, connection.ToNodeId, StringComparison.Ordinal);
                        return equalsResult;
                    });
                    if (!targetNode.Parameters.Any(parameter =>
                    {
                        var equalsResult14 = string.Equals(parameter.Id, connection.ToPortId, StringComparison.Ordinal);
                        return equalsResult14;
                    }))
                    {
                        DomainDiagnostic evaluatedValue15 = new(DomainErrorCodes.UnknownConnectionEndpoint, $"Data connection '{connection.Id}' targets an unknown parameter ID '{connection.ToPortId}'. 数据连接“{connection.Id}”指向未知参数 ID“{connection.ToPortId}”。", $"connections.{connection.Id}.toPortId");
                        diagnostics.Add(evaluatedValue15);
                    }
                }
            }
        }

        return diagnostics;
    }

    private static string CreateParameterKey(string nodeId, string parameterId)
    {
        var concatResult = string.Concat(nodeId, "\u001f", parameterId);
        return concatResult;
    }

    private static bool IsValidEnumLiteral(string value, EnumParameterMetadata metadata)
    {
        var members = value.Split(',', StringSplitOptions.TrimEntries);
        if (members.Length == 0 || members.Any(static member =>
        {
            var isNullOrWhiteSpaceResult = string.IsNullOrWhiteSpace(member);
            return isNullOrWhiteSpaceResult;
        }))
        {
            return false;
        }

        if (!metadata.IsFlags)
        {
            var evaluatedValue21 = members.Length == 1 && metadata.Options.Any(option =>
            {
                var equalsResult20 = string.Equals(option.Name, members[0], StringComparison.OrdinalIgnoreCase);
                return equalsResult20;
            });
            return evaluatedValue21;
        }

        if (resolveCountResult() != members.Length || members.Any(member =>
        {
            var evaluatedValue23 = !metadata.Options.Any(option =>
            {
                var equalsResult22 = string.Equals(option.Name, member, StringComparison.OrdinalIgnoreCase);
                return equalsResult22;
            });
            return evaluatedValue23;
        }))
        {
            return false;
        }

        var whereResult25 = metadata.Options.Where(static option =>
        {
            var equalsResult24 = string.Equals(option.NumericValue, "0", StringComparison.Ordinal);
            return equalsResult24;
        });
        var selectResult26 = whereResult25.Select(static option => option.Name);
        var zeroValueNames = selectResult26.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var evaluatedValue27 = members.Length == 1 || !members.Any(zeroValueNames.Contains);
        return evaluatedValue27;
        int resolveCountResult()
        {
            var distinctResult = members.Distinct(StringComparer.OrdinalIgnoreCase);
            var countResult = distinctResult.Count();
            return countResult;
        }
    }
}

public sealed record FlowRunPolicy(FlowConcurrencyMode ConcurrencyMode)
{
    public static FlowRunPolicy Parallel { get; } = new(FlowConcurrencyMode.Parallel);
}

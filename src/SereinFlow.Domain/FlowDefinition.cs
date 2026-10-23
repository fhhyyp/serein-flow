namespace SereinFlow.Domain;

/// <summary>
/// Represents a versioned flow definition and its validation rules.
/// 表示带版本的流程定义及其校验规则。
/// </summary>
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

    /// <summary>
    /// Identifies the flow definition.
    /// 流程定义标识。
    /// </summary>
    public Guid Id { get; }
    /// <summary>
    /// Stores the flow schema version.
    /// 流程结构版本。
    /// </summary>
    public int SchemaVersion { get; }
    /// <summary>
    /// Tracks the development version.
    /// 开发版本号。
    /// </summary>
    public long Version { get; }
    /// <summary>
    /// Contains the canvases that make up the flow.
    /// 组成流程的画布集合。
    /// </summary>
    public IReadOnlyList<CanvasDefinition> Canvases { get; }
    /// <summary>
    /// Identifies the node where execution starts.
    /// 执行起始节点标识。
    /// </summary>
    public string EntryNodeId { get; }
    /// <summary>
    /// Stores the checksum of the flow definition.
    /// 流程定义的校验和。
    /// </summary>
    public string Checksum { get; }
    /// <summary>
    /// Defines the flow run concurrency policy.
    /// 流程运行并发策略。
    /// </summary>
    public FlowRunPolicy RunPolicy { get; }

    /// <summary>
    /// Creates a versioned flow definition from its canvases and entry node.
    /// 根据画布和入口节点创建带版本的流程定义。
    /// </summary>
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
        // An empty entry node is a valid editor draft. Execution validates this separately.
        // 空入口节点是有效的编辑器草稿状态，执行时会单独进行校验。
        return new FlowDefinition(id, schemaVersion, version, canvases.ToArray(), (entryNodeId?.Trim() ?? string.Empty), checksum ?? string.Empty, runPolicy ?? FlowRunPolicy.Parallel);
    }

    /// <summary>
    /// Validates the flow structure and returns any diagnostics.
    /// 校验流程结构并返回发现的问题。
    /// </summary>
    public IReadOnlyList<DomainDiagnostic> Validate()
    {
        var diagnostics = new List<DomainDiagnostic>();
        if (SchemaVersion != CurrentSchemaVersion)
        {
            DomainDiagnostic nodeTypeRemovedDiagnostic = new(DomainErrorCodes.NodeTypeRemoved, $"Flow schema version {SchemaVersion} is no longer supported; schema {CurrentSchemaVersion} is required. 流程 Schema 版本 {SchemaVersion} 已不再支持，必须使用 Schema {CurrentSchemaVersion}。", "schemaVersion");
            // Schema v5 removes Condition and makes parameter IDs the binding identity.
            // Older payloads are intentionally rejected instead of being silently upgraded.
            // Schema v5 移除了 Condition，并将参数 ID 作为绑定身份；旧流程明确拒绝，不再静默升级。
            diagnostics.Add(nodeTypeRemovedDiagnostic);
        }

        var canvasIds = new HashSet<string>(StringComparer.Ordinal);
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        var connectionIds = new HashSet<string>(StringComparer.Ordinal);
        var dataBoundParameters = Canvases
            .SelectMany(static canvas => canvas.Connections)
            .Where(static connection => connection.Kind == ConnectionKind.Data)
            .Select(static connection =>
            {
                return CreateParameterKey(connection.ToNodeId, connection.ToPortId);
            })
            .ToHashSet(StringComparer.Ordinal);
        var flowCallTargetNodes = Canvases
            .SelectMany(static canvas => canvas.Nodes)
            .Where(static node =>
            {
                return node.Type == NodeType.FlowCall && !string.IsNullOrWhiteSpace(node.Runtime?.TargetNodeId);
            })
            .Select(static node => node.Runtime!.TargetNodeId!)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var canvas in Canvases)
        {
            if (!canvasIds.Add(canvas.Id))
            {
                DomainDiagnostic duplicateCanvasIdDiagnostic = new(DomainErrorCodes.DuplicateCanvasId, $"Canvas '{canvas.Id}' is duplicated. 画布“{canvas.Id}”重复。", $"canvases.{canvas.Id}");
                diagnostics.Add(duplicateCanvasIdDiagnostic);
            }

            foreach (var node in canvas.Nodes)
            {
                if (!Enum.IsDefined(node.Type))
                {
                    DomainDiagnostic nodeTypeRemovedDiagnostic = new(DomainErrorCodes.NodeTypeRemoved, $"Node type '{(int)node.Type}' has been removed and cannot execute. 节点类型“{(int)node.Type}”已移除，不能执行。", $"nodes.{node.Id}.type");
                    diagnostics.Add(nodeTypeRemovedDiagnostic);
                }

                if (!nodeIds.Add(node.Id))
                {
                    DomainDiagnostic duplicateNodeIdDiagnostic = new(DomainErrorCodes.DuplicateNodeId, $"Node '{node.Id}' is duplicated. 节点“{node.Id}”重复。", $"nodes.{node.Id}");
                    diagnostics.Add(duplicateNodeIdDiagnostic);
                }

                var parameterNames = new HashSet<string>(StringComparer.Ordinal);
                var parameterIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var parameter in node.Parameters)
                {
                    if (!parameterNames.Add(parameter.Name))
                    {
                        DomainDiagnostic duplicateParameterNameDiagnostic = new(DomainErrorCodes.DuplicateParameterName, $"Parameter '{parameter.Name}' is duplicated on node '{node.Id}'. 节点“{node.Id}”中参数“{parameter.Name}”重复。", $"nodes.{node.Id}.parameters.{parameter.Name}");
                        diagnostics.Add(duplicateParameterNameDiagnostic);
                    }

                    if (!parameterIds.Add(parameter.Id))
                    {
                        DomainDiagnostic duplicateParameterNameDiagnostic = new(DomainErrorCodes.DuplicateParameterName, $"Parameter ID '{parameter.Id}' is duplicated on node '{node.Id}'. 节点“{node.Id}”中参数 ID“{parameter.Id}”重复。", $"nodes.{node.Id}.parameters.{parameter.Id}");
                        diagnostics.Add(duplicateParameterNameDiagnostic);
                    }

                    if (parameter.Source == DataSource.Literal && parameter.EnumMetadata is not null && !string.IsNullOrWhiteSpace(parameter.ValueJson) && !IsValidEnumLiteral(parameter.ValueJson, parameter.EnumMetadata))
                    {
                        DomainDiagnostic invalidEnumLiteralDiagnostic = new(DomainErrorCodes.InvalidEnumLiteral, $"Enum literal '{parameter.ValueJson}' is not valid for parameter '{parameter.Name}'. 枚举字面量“{parameter.ValueJson}”不是参数“{parameter.Name}”的有效选项。", $"nodes.{node.Id}.parameters.{parameter.Name}");
                        diagnostics.Add(invalidEnumLiteralDiagnostic);
                    }

                    if (parameter.Required && parameter.Source == DataSource.Literal && string.IsNullOrWhiteSpace(parameter.ValueJson) && !HasDataBinding() // A FlowCall provides the public entry's input through
                                                                                                                                                            // a separate mapping. Its completeness is validated by
                                                                                                                                                            // ExecutionPlanBuilder after target resolution.
                                                                                                                                                            // FlowCall 通过独立映射提供公开入口参数；映射完整性将在
                                                                                                                                                            // ExecutionPlanBuilder 解析目标节点后校验。
                    && !flowCallTargetNodes.Contains(node.Id))
                    {
                        DomainDiagnostic missingRequiredParameterDiagnostic = new(DomainErrorCodes.MissingRequiredParameter, $"Required parameter '{parameter.Name}' on node '{node.Id}' has no literal value. 节点“{node.Id}”的必需参数“{parameter.Name}”没有字面量值。", $"nodes.{node.Id}.parameters.{parameter.Name}");
                        diagnostics.Add(missingRequiredParameterDiagnostic);
                    }

                    bool HasDataBinding()
                    {
                        var parameterKey = CreateParameterKey(node.Id, parameter.Id);
                        return dataBoundParameters.Contains(parameterKey);
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(EntryNodeId) && !nodeIds.Contains(EntryNodeId))
        {
            DomainDiagnostic unknownEntryNodeDiagnostic = new(DomainErrorCodes.UnknownEntryNode, $"Entry node '{EntryNodeId}' does not exist. 入口节点“{EntryNodeId}”不存在。", "entryNodeId");
            diagnostics.Add(unknownEntryNodeDiagnostic);
        }

        foreach (var canvas in Canvases)
        {
            foreach (var connection in canvas.Connections)
            {
                if (!connectionIds.Add(connection.Id))
                {
                    DomainDiagnostic duplicateConnectionIdDiagnostic = new(DomainErrorCodes.DuplicateConnectionId, $"Connection '{connection.Id}' is duplicated. 连接“{connection.Id}”重复。", $"connections.{connection.Id}");
                    diagnostics.Add(duplicateConnectionIdDiagnostic);
                }

                if (!nodeIds.Contains(connection.FromNodeId) || !nodeIds.Contains(connection.ToNodeId))
                {
                    DomainDiagnostic unknownConnectionEndpointDiagnostic = new(DomainErrorCodes.UnknownConnectionEndpoint, $"Connection '{connection.Id}' references an unknown node. 连接“{connection.Id}”引用了未知节点。", $"connections.{connection.Id}");
                    diagnostics.Add(unknownConnectionEndpointDiagnostic);
                }

                if (connection.Kind == ConnectionKind.Data && nodeIds.Contains(connection.ToNodeId))
                {
                    var targetNode = Canvases
                        .SelectMany(static item => item.Nodes)
                        .First(item =>
                        {
                            return string.Equals(item.Id, connection.ToNodeId, StringComparison.Ordinal);
                        });
                    if (!targetNode.Parameters.Any(parameter =>
                    {
                        return string.Equals(parameter.Id, connection.ToPortId, StringComparison.Ordinal);
                    }))
                    {
                        DomainDiagnostic unknownConnectionEndpointDiagnostic = new(DomainErrorCodes.UnknownConnectionEndpoint, $"Data connection '{connection.Id}' targets an unknown parameter ID '{connection.ToPortId}'. 数据连接“{connection.Id}”指向未知参数 ID“{connection.ToPortId}”。", $"connections.{connection.Id}.toPortId");
                        diagnostics.Add(unknownConnectionEndpointDiagnostic);
                    }
                }
            }
        }

        return diagnostics;
    }

    private static string CreateParameterKey(string nodeId, string parameterId)
    {
        return string.Concat(nodeId, "\u001f", parameterId);
    }

    private static bool IsValidEnumLiteral(string value, EnumParameterMetadata metadata)
    {
        var members = value.Split(',', StringSplitOptions.TrimEntries);
        if (members.Length == 0 || members.Any(static member =>
        {
            return string.IsNullOrWhiteSpace(member);
        }))
        {
            return false;
        }

        if (!metadata.IsFlags)
        {
            return members.Length == 1 && metadata.Options.Any(option =>
            {
                return string.Equals(option.Name, members[0], StringComparison.OrdinalIgnoreCase);
            });
        }

        if (members
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != members.Length || members.Any(member =>
        {
            return !metadata.Options.Any(option =>
            {
                return string.Equals(option.Name, member, StringComparison.OrdinalIgnoreCase);
            });
        }))
        {
            return false;
        }

        var zeroValueNames = metadata.Options
            .Where(static option =>
            {
                return string.Equals(option.NumericValue, "0", StringComparison.Ordinal);
            })
            .Select(static option => option.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return members.Length == 1 || !members.Any(zeroValueNames.Contains);
    }
}

/// <summary>
/// Represents flow run policy data used by SereinFlow.
/// 表示 SereinFlow 使用的流程运行策略数据。
/// </summary>
public sealed record FlowRunPolicy(FlowConcurrencyMode ConcurrencyMode)
{
    public static FlowRunPolicy Parallel { get; } = new(FlowConcurrencyMode.Parallel);
}

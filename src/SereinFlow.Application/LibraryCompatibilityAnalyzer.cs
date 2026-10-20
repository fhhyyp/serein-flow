using SereinFlow.Contracts;

namespace SereinFlow.Application;
public interface ILibraryCompatibilityAnalyzer
{
    FlowLibraryUpgradePreviewDto Analyze(FlowDefinitionDto flow, LibraryDto source, LibraryDto target);
}

/// <summary>
/// Pure, deterministic manifest comparison. It intentionally refuses fuzzy
/// matching by method name or parameter position, because either can silently
/// retarget persisted data connections after a library update.
/// 纯粹且确定性的 Manifest 比较器。它刻意拒绝按方法名称或参数位置模糊匹配，
/// 因为它们可能在类库更新后静默错误地重定向持久化数据连接。
/// </summary>
public sealed class LibraryCompatibilityAnalyzer : ILibraryCompatibilityAnalyzer
{
    public FlowLibraryUpgradePreviewDto Analyze(FlowDefinitionDto flow, LibraryDto source, LibraryDto target)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        var issues = new List<LibraryUpgradeIssueDto>();
        var sourceManifest = source.CompatibilityManifest;
        var targetManifest = target.CompatibilityManifest;
        if (sourceManifest is null || targetManifest is null)
        {
            var issueResult = Issue("manifest-unavailable", LibraryCompatibilityClassificationDto.Unknown, LibraryErrorCodes.UpgradeManifestMissing, "A compatibility manifest is unavailable for one of the library artifacts. 某个类库工件缺少兼容性 Manifest。", blocks: true);
            issues.Add(issueResult);
            var flowLibraryUpgradePreviewDto = new FlowLibraryUpgradePreviewDto(flow.Id, flow.Version, false, 0, issues);
            return flowLibraryUpgradePreviewDto;
        }

        var affectedCount = 0;
        foreach (var canvas in flow.Canvases)
        {
            foreach (var node in canvas.Nodes.Where(node =>
            {
                var evaluatedValue = node.Type is NodeTypeDto.Action or NodeTypeDto.Flipflop && string.Equals(node.Ui?.LibraryId, source.Id, StringComparison.OrdinalIgnoreCase);
                return evaluatedValue;
            }))
            {
                affectedCount++;
                AnalyzeNode(flow, canvas, node, sourceManifest, targetManifest, issues);
            }
        }

        if (affectedCount == 0)
        {
            var issueResult1 = Issue("no-affected-nodes", LibraryCompatibilityClassificationDto.Unknown, LibraryErrorCodes.UpgradeSourceNotUsed, "The selected flow does not use the source library artifact. 所选流程未使用源类库工件。", blocks: true);
            issues.Add(issueResult1);
        }

        var evaluatedValue2 = !issues.Any(static issue => issue.BlocksApplication);
        var orderByResult = issues.OrderBy(static issue => issue.Id, StringComparer.Ordinal);
        var toArrayResult = orderByResult.ToArray();
        var flowLibraryUpgradePreviewDto3 = new FlowLibraryUpgradePreviewDto(flow.Id, flow.Version, evaluatedValue2, affectedCount, toArrayResult);
        return flowLibraryUpgradePreviewDto3;
    }

    private static void AnalyzeNode(FlowDefinitionDto flow, CanvasDto canvas, NodeDto node, LibraryArtifactManifestDto source, LibraryArtifactManifestDto target, List<LibraryUpgradeIssueDto> issues)
    {
        var runtime = node.Ui;
        var sourceNode = FindSourceNode(source, runtime);
        if (sourceNode is null)
        {
            var issueResult4 = Issue($"{node.Id}:source-node", LibraryCompatibilityClassificationDto.Unknown, LibraryErrorCodes.UpgradeSourceContractUnknown, "The source node cannot be uniquely identified in the source manifest. 源节点无法在源 Manifest 中被唯一识别。", canvas.Id, node.Id, blocks: true);
            issues.Add(issueResult4);
            return;
        }

        var targetNode = FindTargetNode(sourceNode, target);
        if (targetNode is null)
        {
            var classification = sourceNode.IdentityConfidence == LibraryContractIdentityConfidenceDto.Explicit ? LibraryCompatibilityClassificationDto.Breaking : LibraryCompatibilityClassificationDto.Unknown;
            var issueResult5 = Issue($"{node.Id}:target-node", classification, classification == LibraryCompatibilityClassificationDto.Breaking ? LibraryErrorCodes.UpgradeNodeRemoved : LibraryErrorCodes.UpgradeNodeMatchUnknown, classification == LibraryCompatibilityClassificationDto.Breaking ? "The target library no longer provides this node contract. 目标类库不再提供该节点契约。" : "The legacy node identity cannot be uniquely matched in the target library. 旧节点身份无法在目标类库中被唯一匹配。", canvas.Id, node.Id, sourceNode.ContractId, blocks: true);
            issues.Add(issueResult5);
            return;
        }

        if (sourceNode.Type != targetNode.Type || sourceNode.IsAwaitable != targetNode.IsAwaitable)
        {
            var issueResult6 = Issue($"{node.Id}:node-kind", LibraryCompatibilityClassificationDto.Breaking, LibraryErrorCodes.UpgradeNodeExecutionChanged, "The target node changes its execution type or awaitable contract. 目标节点更改了执行类型或可等待契约。", canvas.Id, node.Id, sourceNode.ContractId, targetNode.ContractId, blocks: true);
            issues.Add(issueResult6);
        }

        if (!string.Equals(sourceNode.ReturnType, targetNode.ReturnType, StringComparison.Ordinal))
        {
            var issueResult7 = Issue($"{node.Id}:return-type", LibraryCompatibilityClassificationDto.Breaking, LibraryErrorCodes.UpgradeReturnTypeChanged, "The target node changes its return type. 目标节点更改了返回类型。", canvas.Id, node.Id, sourceNode.ContractId, targetNode.ContractId, blocks: true);
            issues.Add(issueResult7);
        }

        var whereResult = canvas.Connections.Where(connection => connection.Kind == ConnectionKindDto.Data && connection.ToNodeId == node.Id);
        var inbound = whereResult.ToArray();
        var whereResult14 = node.Parameters.Where(IsConfiguredByFlow);
        var selectResult = whereResult14.Select(parameter => parameter.Ui?.Id ?? parameter.Name);
        var configuredParameterIds = selectResult.ToHashSet(StringComparer.Ordinal);
        foreach (var sourceParameter in sourceNode.Parameters)
        {
            var targetParameter = FindTargetParameter(sourceParameter, targetNode);
            var used = configuredParameterIds.Contains(sourceParameter.ContractId) || inbound.Any(connection =>
            {
                var equalsResult = string.Equals(connection.ToPortId, sourceParameter.ContractId, StringComparison.Ordinal);
                return equalsResult;
            });
            if (targetParameter is null)
            {
                if (used)
                {
                    var issueResult8 = Issue($"{node.Id}:parameter:{sourceParameter.ContractId}", LibraryCompatibilityClassificationDto.Breaking, LibraryErrorCodes.UpgradeParameterRemoved, "The target library removes a parameter used by the flow. 目标类库移除了流程正在使用的参数。", canvas.Id, node.Id, sourceNode.ContractId, targetNode.ContractId, sourceParameter.ContractId, blocks: true);
                    issues.Add(issueResult8);
                }
                else
                {
                    var issueResult9 = Issue($"{node.Id}:parameter:{sourceParameter.ContractId}", LibraryCompatibilityClassificationDto.Compatible, LibraryErrorCodes.UpgradeParameterRemovedUnused, "The target library removes an unused parameter; its inactive saved definition will be dropped. 目标类库移除了未使用参数；该参数未启用的已保存定义将被移除。", canvas.Id, node.Id, sourceNode.ContractId, targetNode.ContractId, sourceParameter.ContractId);
                    issues.Add(issueResult9);
                }

                continue;
            }

            var classification = ClassifyParameter(sourceParameter, targetParameter);
            if (classification != LibraryCompatibilityClassificationDto.Exact)
            {
                var requiresMapping = classification == LibraryCompatibilityClassificationDto.RequiresMapping;
                var issueResult10 = Issue($"{node.Id}:parameter:{sourceParameter.ContractId}:{targetParameter.ContractId}", classification, requiresMapping ? LibraryErrorCodes.UpgradeParameterRenamed : LibraryErrorCodes.UpgradeParameterChanged, requiresMapping ? "The target parameter accepts the previous stable ID as an alias; confirm the mapping before upgrade. 目标参数接受旧稳定 ID 作为别名；升级前请确认映射。" : "The target parameter contract has a compatible metadata change. 目标参数契约发生了兼容的元数据变更。", canvas.Id, node.Id, sourceNode.ContractId, targetNode.ContractId, sourceParameter.ContractId, targetParameter.ContractId, requiresAcknowledgement: requiresMapping);
                issues.Add(issueResult10);
            }
        }

        foreach (var targetParameter in targetNode.Parameters)
        {
            var sourceParameter = sourceNode.Parameters.SingleOrDefault(parameter =>
            {
                var evaluatedValue11 = string.Equals(parameter.ContractId, targetParameter.ContractId, StringComparison.Ordinal) || targetParameter.Aliases.Contains(parameter.ContractId, StringComparer.Ordinal);
                return evaluatedValue11;
            });
            if (sourceParameter is null && targetParameter.Required && string.IsNullOrWhiteSpace(targetParameter.DefaultValue))
            {
                var issueResult12 = Issue($"{node.Id}:required:{targetParameter.ContractId}", LibraryCompatibilityClassificationDto.RequiresRewire, LibraryErrorCodes.UpgradeRequiredParameterAdded, "The target library adds a required parameter without a default value. 目标类库新增了没有默认值的必需参数。", canvas.Id, node.Id, sourceNode.ContractId, targetNode.ContractId, targetParameterId: targetParameter.ContractId, blocks: true);
                issues.Add(issueResult12);
            }
        }

        if (!issues.Any(issue => issue.NodeId == node.Id))
        {
            var issueResult13 = Issue($"{node.Id}:exact", LibraryCompatibilityClassificationDto.Exact, LibraryErrorCodes.UpgradeExact, "The node contract is exactly compatible. 节点契约完全兼容。", canvas.Id, node.Id, sourceNode.ContractId, targetNode.ContractId);
            issues.Add(issueResult13);
        }
    }

    internal static LibraryManifestNodeDto? FindSourceNode(LibraryArtifactManifestDto manifest, NodeUiMetadataDto? runtime)
    {
        if (!string.IsNullOrWhiteSpace(runtime?.LibraryNodeContractId))
        {
            var singleOrDefaultResult = manifest.Nodes.SingleOrDefault(node =>
            {
                var evaluatedValue15 = node.IdentityConfidence == LibraryContractIdentityConfidenceDto.Explicit && string.Equals(node.ContractId, runtime.LibraryNodeContractId, StringComparison.Ordinal);
                return evaluatedValue15;
            });
            return singleOrDefaultResult;
        }

        var whereResult17 = manifest.Nodes.Where(node =>
        {
            var evaluatedValue16 = node.IdentityConfidence == LibraryContractIdentityConfidenceDto.Legacy && string.Equals(node.DeclaringType, runtime?.ClassName, StringComparison.Ordinal) && string.Equals(node.MethodName, runtime?.MethodName, StringComparison.Ordinal);
            return evaluatedValue16;
        });
        var legacyMatches = whereResult17.ToArray();
        return legacyMatches.Length == 1 ? legacyMatches[0] : null;
    }

    internal static LibraryManifestNodeDto? FindTargetNode(LibraryManifestNodeDto sourceNode, LibraryArtifactManifestDto target)
    {
        if (sourceNode.IdentityConfidence == LibraryContractIdentityConfidenceDto.Explicit)
        {
            var singleOrDefaultResult19 = target.Nodes.SingleOrDefault(node =>
            {
                var evaluatedValue18 = node.IdentityConfidence == LibraryContractIdentityConfidenceDto.Explicit && string.Equals(node.ContractId, sourceNode.ContractId, StringComparison.Ordinal);
                return evaluatedValue18;
            });
            return singleOrDefaultResult19;
        }

        var whereResult21 = target.Nodes.Where(node =>
        {
            var evaluatedValue20 = node.IdentityConfidence == LibraryContractIdentityConfidenceDto.Legacy && string.Equals(node.OverloadSignature, sourceNode.OverloadSignature, StringComparison.Ordinal);
            return evaluatedValue20;
        });
        var matches = whereResult21.ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    internal static LibraryManifestParameterDto? FindTargetParameter(LibraryManifestParameterDto source, LibraryManifestNodeDto target)
    {
        var singleOrDefaultResult23 = target.Parameters.SingleOrDefault(parameter =>
        {
            var evaluatedValue22 = string.Equals(parameter.ContractId, source.ContractId, StringComparison.Ordinal) || parameter.Aliases.Contains(source.ContractId, StringComparer.Ordinal);
            return evaluatedValue22;
        });
        return singleOrDefaultResult23;
    }

    private static bool IsConfiguredByFlow(NodeParameterDto parameter)
    {
        var evaluatedValue24 = !string.IsNullOrWhiteSpace(parameter.ValueJson) || parameter.Source != DataSourceDto.Literal || !string.IsNullOrWhiteSpace(parameter.Ui?.ProjectInputKey) || !string.IsNullOrWhiteSpace(parameter.Ui?.Expression) || !string.IsNullOrWhiteSpace(parameter.Ui?.SourceNodeId) || !string.IsNullOrWhiteSpace(parameter.Ui?.SourcePortId);
        return evaluatedValue24;
    }

    private static LibraryCompatibilityClassificationDto ClassifyParameter(LibraryManifestParameterDto source, LibraryManifestParameterDto target)
    {
        if (!string.Equals(source.Type, target.Type, StringComparison.Ordinal) || source.IsVariadic != target.IsVariadic || !string.Equals(source.ElementType, target.ElementType, StringComparison.Ordinal))
        {
            return LibraryCompatibilityClassificationDto.Breaking;
        }

        if (!string.Equals(source.ContractId, target.ContractId, StringComparison.Ordinal))
        {
            return LibraryCompatibilityClassificationDto.RequiresMapping;
        }

        if (source.Required != target.Required || !string.Equals(source.DefaultValue, target.DefaultValue, StringComparison.Ordinal))
        {
            return LibraryCompatibilityClassificationDto.Compatible;
        }

        return LibraryCompatibilityClassificationDto.Exact;
    }

    private static LibraryUpgradeIssueDto Issue(string id, LibraryCompatibilityClassificationDto classification, string code, string message, string? canvasId = null, string? nodeId = null, string? sourceNodeContractId = null, string? targetNodeContractId = null, string? sourceParameterId = null, string? targetParameterId = null, string? connectionId = null, bool requiresAcknowledgement = false, bool blocks = false)
    {
        LibraryUpgradeIssueDto evaluatedValue25 = new(id, classification, code, message, canvasId, nodeId, sourceNodeContractId, targetNodeContractId, sourceParameterId, targetParameterId, connectionId, requiresAcknowledgement, blocks);
        return evaluatedValue25;
    }
}

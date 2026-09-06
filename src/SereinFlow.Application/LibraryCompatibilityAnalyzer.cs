using SereinFlow.Contracts;

namespace SereinFlow.Application;

public interface ILibraryCompatibilityAnalyzer
{
    FlowLibraryUpgradePreviewDto Analyze(
        FlowDefinitionDto flow,
        LibraryDto source,
        LibraryDto target);
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
    public FlowLibraryUpgradePreviewDto Analyze(
        FlowDefinitionDto flow,
        LibraryDto source,
        LibraryDto target)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var issues = new List<LibraryUpgradeIssueDto>();
        var sourceManifest = source.CompatibilityManifest;
        var targetManifest = target.CompatibilityManifest;
        if (sourceManifest is null || targetManifest is null)
        {
            issues.Add(Issue(
                "manifest-unavailable",
                LibraryCompatibilityClassificationDto.Unknown,
                LibraryErrorCodes.UpgradeManifestMissing,
                "A compatibility manifest is unavailable for one of the library artifacts. 某个类库工件缺少兼容性 Manifest。",
                blocks: true));
            return new FlowLibraryUpgradePreviewDto(flow.Id, flow.Version, false, 0, issues);
        }

        var affectedCount = 0;
        foreach (var canvas in flow.Canvases)
        {
            foreach (var node in canvas.Nodes.Where(node =>
                         node.Type is NodeTypeDto.Action or NodeTypeDto.Flipflop
                         && string.Equals(node.Ui?.LibraryId, source.Id, StringComparison.OrdinalIgnoreCase)))
            {
                affectedCount++;
                AnalyzeNode(flow, canvas, node, sourceManifest, targetManifest, issues);
            }
        }

        if (affectedCount == 0)
        {
            issues.Add(Issue(
                "no-affected-nodes",
                LibraryCompatibilityClassificationDto.Unknown,
                LibraryErrorCodes.UpgradeSourceNotUsed,
                "The selected flow does not use the source library artifact. 所选流程未使用源类库工件。",
                blocks: true));
        }

        return new FlowLibraryUpgradePreviewDto(
            flow.Id,
            flow.Version,
            !issues.Any(static issue => issue.BlocksApplication),
            affectedCount,
            issues.OrderBy(static issue => issue.Id, StringComparer.Ordinal).ToArray());
    }

    private static void AnalyzeNode(
        FlowDefinitionDto flow,
        CanvasDto canvas,
        NodeDto node,
        LibraryArtifactManifestDto source,
        LibraryArtifactManifestDto target,
        List<LibraryUpgradeIssueDto> issues)
    {
        var runtime = node.Ui;
        var sourceNode = FindSourceNode(source, runtime);
        if (sourceNode is null)
        {
            issues.Add(Issue(
                $"{node.Id}:source-node",
                LibraryCompatibilityClassificationDto.Unknown,
                LibraryErrorCodes.UpgradeSourceContractUnknown,
                "The source node cannot be uniquely identified in the source manifest. 源节点无法在源 Manifest 中被唯一识别。",
                canvas.Id,
                node.Id,
                blocks: true));
            return;
        }

        var targetNode = FindTargetNode(sourceNode, target);
        if (targetNode is null)
        {
            var classification = sourceNode.IdentityConfidence == LibraryContractIdentityConfidenceDto.Explicit
                ? LibraryCompatibilityClassificationDto.Breaking
                : LibraryCompatibilityClassificationDto.Unknown;
            issues.Add(Issue(
                $"{node.Id}:target-node",
                classification,
                classification == LibraryCompatibilityClassificationDto.Breaking
                    ? LibraryErrorCodes.UpgradeNodeRemoved
                    : LibraryErrorCodes.UpgradeNodeMatchUnknown,
                classification == LibraryCompatibilityClassificationDto.Breaking
                    ? "The target library no longer provides this node contract. 目标类库不再提供该节点契约。"
                    : "The legacy node identity cannot be uniquely matched in the target library. 旧节点身份无法在目标类库中被唯一匹配。",
                canvas.Id,
                node.Id,
                sourceNode.ContractId,
                blocks: true));
            return;
        }

        if (sourceNode.Type != targetNode.Type || sourceNode.IsAwaitable != targetNode.IsAwaitable)
        {
            issues.Add(Issue(
                $"{node.Id}:node-kind",
                LibraryCompatibilityClassificationDto.Breaking,
                LibraryErrorCodes.UpgradeNodeExecutionChanged,
                "The target node changes its execution type or awaitable contract. 目标节点更改了执行类型或可等待契约。",
                canvas.Id,
                node.Id,
                sourceNode.ContractId,
                targetNode.ContractId,
                blocks: true));
        }

        if (!string.Equals(sourceNode.ReturnType, targetNode.ReturnType, StringComparison.Ordinal))
        {
            issues.Add(Issue(
                $"{node.Id}:return-type",
                LibraryCompatibilityClassificationDto.Breaking,
                LibraryErrorCodes.UpgradeReturnTypeChanged,
                "The target node changes its return type. 目标节点更改了返回类型。",
                canvas.Id,
                node.Id,
                sourceNode.ContractId,
                targetNode.ContractId,
                blocks: true));
        }

        var inbound = canvas.Connections
            .Where(connection => connection.Kind == ConnectionKindDto.Data && connection.ToNodeId == node.Id)
            .ToArray();
        var configuredParameterIds = node.Parameters
            .Where(IsConfiguredByFlow)
            .Select(parameter => parameter.Ui?.Id ?? parameter.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var sourceParameter in sourceNode.Parameters)
        {
            var targetParameter = FindTargetParameter(sourceParameter, targetNode);
            var used = configuredParameterIds.Contains(sourceParameter.ContractId)
                || inbound.Any(connection => string.Equals(connection.ToPortId, sourceParameter.ContractId, StringComparison.Ordinal));
            if (targetParameter is null)
            {
                if (used)
                {
                    issues.Add(Issue(
                        $"{node.Id}:parameter:{sourceParameter.ContractId}",
                        LibraryCompatibilityClassificationDto.Breaking,
                        LibraryErrorCodes.UpgradeParameterRemoved,
                        "The target library removes a parameter used by the flow. 目标类库移除了流程正在使用的参数。",
                        canvas.Id,
                        node.Id,
                        sourceNode.ContractId,
                        targetNode.ContractId,
                        sourceParameter.ContractId,
                        blocks: true));
                }
                else
                {
                    issues.Add(Issue(
                        $"{node.Id}:parameter:{sourceParameter.ContractId}",
                        LibraryCompatibilityClassificationDto.Compatible,
                        LibraryErrorCodes.UpgradeParameterRemovedUnused,
                        "The target library removes an unused parameter; its inactive saved definition will be dropped. 目标类库移除了未使用参数；该参数未启用的已保存定义将被移除。",
                        canvas.Id,
                        node.Id,
                        sourceNode.ContractId,
                        targetNode.ContractId,
                        sourceParameter.ContractId));
                }
                continue;
            }

            var classification = ClassifyParameter(sourceParameter, targetParameter);
            if (classification != LibraryCompatibilityClassificationDto.Exact)
            {
                var requiresMapping = classification == LibraryCompatibilityClassificationDto.RequiresMapping;
                issues.Add(Issue(
                    $"{node.Id}:parameter:{sourceParameter.ContractId}:{targetParameter.ContractId}",
                    classification,
                    requiresMapping ? LibraryErrorCodes.UpgradeParameterRenamed : LibraryErrorCodes.UpgradeParameterChanged,
                    requiresMapping
                        ? "The target parameter accepts the previous stable ID as an alias; confirm the mapping before upgrade. 目标参数接受旧稳定 ID 作为别名；升级前请确认映射。"
                        : "The target parameter contract has a compatible metadata change. 目标参数契约发生了兼容的元数据变更。",
                    canvas.Id,
                    node.Id,
                    sourceNode.ContractId,
                    targetNode.ContractId,
                    sourceParameter.ContractId,
                    targetParameter.ContractId,
                    requiresAcknowledgement: requiresMapping));
            }
        }

        foreach (var targetParameter in targetNode.Parameters)
        {
            var sourceParameter = sourceNode.Parameters.SingleOrDefault(parameter =>
                string.Equals(parameter.ContractId, targetParameter.ContractId, StringComparison.Ordinal)
                || targetParameter.Aliases.Contains(parameter.ContractId, StringComparer.Ordinal));
            if (sourceParameter is null && targetParameter.Required && string.IsNullOrWhiteSpace(targetParameter.DefaultValue))
            {
                issues.Add(Issue(
                    $"{node.Id}:required:{targetParameter.ContractId}",
                    LibraryCompatibilityClassificationDto.RequiresRewire,
                    LibraryErrorCodes.UpgradeRequiredParameterAdded,
                    "The target library adds a required parameter without a default value. 目标类库新增了没有默认值的必需参数。",
                    canvas.Id,
                    node.Id,
                    sourceNode.ContractId,
                    targetNode.ContractId,
                    targetParameterId: targetParameter.ContractId,
                    blocks: true));
            }
        }

        if (!issues.Any(issue => issue.NodeId == node.Id))
        {
            issues.Add(Issue(
                $"{node.Id}:exact",
                LibraryCompatibilityClassificationDto.Exact,
                LibraryErrorCodes.UpgradeExact,
                "The node contract is exactly compatible. 节点契约完全兼容。",
                canvas.Id,
                node.Id,
                sourceNode.ContractId,
                targetNode.ContractId));
        }
    }

    internal static LibraryManifestNodeDto? FindSourceNode(
        LibraryArtifactManifestDto manifest,
        NodeUiMetadataDto? runtime)
    {
        if (!string.IsNullOrWhiteSpace(runtime?.LibraryNodeContractId))
        {
            return manifest.Nodes.SingleOrDefault(node =>
                node.IdentityConfidence == LibraryContractIdentityConfidenceDto.Explicit
                && string.Equals(node.ContractId, runtime.LibraryNodeContractId, StringComparison.Ordinal));
        }

        var legacyMatches = manifest.Nodes.Where(node =>
            node.IdentityConfidence == LibraryContractIdentityConfidenceDto.Legacy
            && string.Equals(node.DeclaringType, runtime?.ClassName, StringComparison.Ordinal)
            && string.Equals(node.MethodName, runtime?.MethodName, StringComparison.Ordinal))
            .ToArray();
        return legacyMatches.Length == 1 ? legacyMatches[0] : null;
    }

    internal static LibraryManifestNodeDto? FindTargetNode(
        LibraryManifestNodeDto sourceNode,
        LibraryArtifactManifestDto target)
    {
        if (sourceNode.IdentityConfidence == LibraryContractIdentityConfidenceDto.Explicit)
        {
            return target.Nodes.SingleOrDefault(node =>
                node.IdentityConfidence == LibraryContractIdentityConfidenceDto.Explicit
                && string.Equals(node.ContractId, sourceNode.ContractId, StringComparison.Ordinal));
        }

        var matches = target.Nodes.Where(node =>
            node.IdentityConfidence == LibraryContractIdentityConfidenceDto.Legacy
            && string.Equals(node.OverloadSignature, sourceNode.OverloadSignature, StringComparison.Ordinal))
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    internal static LibraryManifestParameterDto? FindTargetParameter(
        LibraryManifestParameterDto source,
        LibraryManifestNodeDto target)
        => target.Parameters.SingleOrDefault(parameter =>
            string.Equals(parameter.ContractId, source.ContractId, StringComparison.Ordinal)
            || parameter.Aliases.Contains(source.ContractId, StringComparer.Ordinal));

    private static bool IsConfiguredByFlow(NodeParameterDto parameter)
        => !string.IsNullOrWhiteSpace(parameter.ValueJson)
            || parameter.Source != DataSourceDto.Literal
            || !string.IsNullOrWhiteSpace(parameter.Ui?.ProjectInputKey)
            || !string.IsNullOrWhiteSpace(parameter.Ui?.Expression)
            || !string.IsNullOrWhiteSpace(parameter.Ui?.SourceNodeId)
            || !string.IsNullOrWhiteSpace(parameter.Ui?.SourcePortId);

    private static LibraryCompatibilityClassificationDto ClassifyParameter(
        LibraryManifestParameterDto source,
        LibraryManifestParameterDto target)
    {
        if (!string.Equals(source.Type, target.Type, StringComparison.Ordinal)
            || source.IsVariadic != target.IsVariadic
            || !string.Equals(source.ElementType, target.ElementType, StringComparison.Ordinal))
        {
            return LibraryCompatibilityClassificationDto.Breaking;
        }
        if (!string.Equals(source.ContractId, target.ContractId, StringComparison.Ordinal))
            return LibraryCompatibilityClassificationDto.RequiresMapping;
        if (source.Required != target.Required || !string.Equals(source.DefaultValue, target.DefaultValue, StringComparison.Ordinal))
            return LibraryCompatibilityClassificationDto.Compatible;
        return LibraryCompatibilityClassificationDto.Exact;
    }

    private static LibraryUpgradeIssueDto Issue(
        string id,
        LibraryCompatibilityClassificationDto classification,
        string code,
        string message,
        string? canvasId = null,
        string? nodeId = null,
        string? sourceNodeContractId = null,
        string? targetNodeContractId = null,
        string? sourceParameterId = null,
        string? targetParameterId = null,
        string? connectionId = null,
        bool requiresAcknowledgement = false,
        bool blocks = false)
        => new(
            id,
            classification,
            code,
            message,
            canvasId,
            nodeId,
            sourceNodeContractId,
            targetNodeContractId,
            sourceParameterId,
            targetParameterId,
            connectionId,
            requiresAcknowledgement,
            blocks);
}

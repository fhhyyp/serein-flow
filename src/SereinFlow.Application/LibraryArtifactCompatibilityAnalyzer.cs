using SereinFlow.Contracts;

namespace SereinFlow.Application;

/// <summary>
/// Compares two immutable library manifests without loading either assembly.
/// The result is deliberately conservative: a missing or ambiguous contract
/// blocks an import preview instead of allowing a silent node rebind.
/// 在不加载程序集的前提下比较两个不可变类库 Manifest。结果采用保守策略：缺失或
/// 不明确的契约会阻止导入预览，避免节点被静默重绑定。
/// </summary>
public static class LibraryArtifactCompatibilityAnalyzer
{
    public static LibraryArtifactCompatibilityDto Analyze(LibraryDto baseline, LibraryDto target)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(target);

        var issues = new List<LibraryArtifactCompatibilityIssueDto>();
        var sourceManifest = baseline.CompatibilityManifest;
        var targetManifest = target.CompatibilityManifest;
        if (sourceManifest is null || targetManifest is null)
        {
            issues.Add(Issue(
                "manifest-missing",
                LibraryCompatibilityClassificationDto.Unknown,
                "library.package_manifest_missing",
                "A compatibility manifest is unavailable for the baseline or target artifact. 基线或目标工件缺少兼容性 Manifest。",
                blocks: true));
        }
        else
        {
            CompareNodes(sourceManifest.Nodes, targetManifest.Nodes, issues);
        }

        return new LibraryArtifactCompatibilityDto(
            baseline.Id,
            baseline.Version,
            target.Id,
            target.Version,
            !issues.Any(static issue => issue.BlocksApplication),
            issues.OrderBy(static issue => issue.Id, StringComparer.Ordinal).ToArray());
    }

    private static void CompareNodes(
        IReadOnlyList<LibraryManifestNodeDto> sourceNodes,
        IReadOnlyList<LibraryManifestNodeDto> targetNodes,
        List<LibraryArtifactCompatibilityIssueDto> issues)
    {
        foreach (var source in sourceNodes.OrderBy(static node => node.ContractId, StringComparer.Ordinal))
        {
            var target = FindTargetNode(source, targetNodes);
            if (target is null)
            {
                var classification = source.IdentityConfidence == LibraryContractIdentityConfidenceDto.Explicit
                    ? LibraryCompatibilityClassificationDto.Breaking
                    : LibraryCompatibilityClassificationDto.Unknown;
                issues.Add(Issue(
                    $"node:{source.ContractId}:removed",
                    classification,
                    classification == LibraryCompatibilityClassificationDto.Breaking
                        ? "library.package_node_removed"
                        : "library.package_node_match_unknown",
                    classification == LibraryCompatibilityClassificationDto.Breaking
                        ? "The baseline node contract is missing from the target artifact. 基线节点契约在目标工件中不存在。"
                        : "The baseline node contract cannot be uniquely matched in the target artifact. 基线节点契约无法在目标工件中唯一匹配。",
                    sourceNodeContractId: source.ContractId,
                    blocks: true));
                continue;
            }

            CompareNode(source, target, issues);
        }

        foreach (var target in targetNodes.OrderBy(static node => node.ContractId, StringComparer.Ordinal))
        {
            if (FindSourceNode(target, sourceNodes) is not null)
                continue;

            foreach (var parameter in target.Parameters.Where(static parameter => parameter.Required && string.IsNullOrWhiteSpace(parameter.DefaultValue)))
            {
                issues.Add(Issue(
                    $"node:{target.ContractId}:required:{parameter.ContractId}",
                    LibraryCompatibilityClassificationDto.RequiresRewire,
                    "library.package_required_parameter_added",
                    "The target artifact adds a required parameter without a default value. 目标工件新增了没有默认值的必需参数。",
                    targetNodeContractId: target.ContractId,
                    targetParameterId: parameter.ContractId,
                    blocks: true));
            }
        }

        if (issues.Count == 0)
        {
            issues.Add(Issue(
                "exact",
                LibraryCompatibilityClassificationDto.Exact,
                "library.package_exact",
                "The target artifact exposes the same node and parameter contracts. 目标工件提供相同的节点和参数契约。"));
        }
    }

    private static void CompareNode(
        LibraryManifestNodeDto source,
        LibraryManifestNodeDto target,
        List<LibraryArtifactCompatibilityIssueDto> issues)
    {
        if (source.Type != target.Type || source.IsAwaitable != target.IsAwaitable)
        {
            issues.Add(Issue(
                $"node:{source.ContractId}:execution",
                LibraryCompatibilityClassificationDto.Breaking,
                "library.package_node_execution_changed",
                "The target node changes its node type or awaitable contract. 目标节点更改了节点类型或可等待契约。",
                source.ContractId,
                target.ContractId,
                blocks: true));
        }

        if (!string.Equals(source.ReturnType, target.ReturnType, StringComparison.Ordinal))
        {
            issues.Add(Issue(
                $"node:{source.ContractId}:return-type",
                LibraryCompatibilityClassificationDto.Breaking,
                "library.package_return_type_changed",
                "The target node changes its return type. 目标节点更改了返回类型。",
                source.ContractId,
                target.ContractId,
                blocks: true));
        }

        foreach (var sourceParameter in source.Parameters.OrderBy(static parameter => parameter.ContractId, StringComparer.Ordinal))
        {
            var targetParameter = FindTargetParameter(sourceParameter, target.Parameters);
            if (targetParameter is null)
            {
                issues.Add(Issue(
                    $"node:{source.ContractId}:parameter:{sourceParameter.ContractId}:removed",
                    LibraryCompatibilityClassificationDto.Breaking,
                    "library.package_parameter_removed",
                    "The target artifact removes a baseline parameter contract. 目标工件移除了基线参数契约。",
                    source.ContractId,
                    target.ContractId,
                    sourceParameter.ContractId,
                    blocks: true));
                continue;
            }

            if (!string.Equals(sourceParameter.Type, targetParameter.Type, StringComparison.Ordinal)
                || sourceParameter.IsVariadic != targetParameter.IsVariadic
                || !string.Equals(sourceParameter.ElementType, targetParameter.ElementType, StringComparison.Ordinal))
            {
                issues.Add(Issue(
                    $"node:{source.ContractId}:parameter:{sourceParameter.ContractId}:type",
                    LibraryCompatibilityClassificationDto.Breaking,
                    "library.package_parameter_type_changed",
                    "The target parameter changes its CLR or variadic type contract. 目标参数更改了 CLR 或可变参数类型契约。",
                    source.ContractId,
                    target.ContractId,
                    sourceParameter.ContractId,
                    targetParameter.ContractId,
                    blocks: true));
            }
            else if (!string.Equals(sourceParameter.ContractId, targetParameter.ContractId, StringComparison.Ordinal))
            {
                issues.Add(Issue(
                    $"node:{source.ContractId}:parameter:{sourceParameter.ContractId}:mapping",
                    LibraryCompatibilityClassificationDto.RequiresMapping,
                    "library.package_parameter_mapping_required",
                    "The target parameter uses a new ID and declares the baseline ID as an alias. 目标参数使用新 ID，并将基线 ID 声明为别名。",
                    source.ContractId,
                    target.ContractId,
                    sourceParameter.ContractId,
                    targetParameter.ContractId,
                    blocks: true));
            }
            else if (sourceParameter.Required != targetParameter.Required
                || !string.Equals(sourceParameter.DefaultValue, targetParameter.DefaultValue, StringComparison.Ordinal))
            {
                issues.Add(Issue(
                    $"node:{source.ContractId}:parameter:{sourceParameter.ContractId}:metadata",
                    LibraryCompatibilityClassificationDto.Compatible,
                    "library.package_parameter_metadata_changed",
                    "The target parameter changes optionality or its default value without changing its type. 目标参数更改了可选性或默认值，但类型未变。"));
            }
        }

        foreach (var targetParameter in target.Parameters)
        {
            if (FindSourceParameter(targetParameter, source.Parameters) is not null)
                continue;
            if (targetParameter.Required && string.IsNullOrWhiteSpace(targetParameter.DefaultValue))
            {
                issues.Add(Issue(
                    $"node:{target.ContractId}:required:{targetParameter.ContractId}",
                    LibraryCompatibilityClassificationDto.RequiresRewire,
                    "library.package_required_parameter_added",
                    "The target artifact adds a required parameter without a default value. 目标工件新增了没有默认值的必需参数。",
                    source.ContractId,
                    target.ContractId,
                    targetParameterId: targetParameter.ContractId,
                    blocks: true));
            }
        }

        if (!issues.Any(issue => string.Equals(issue.SourceNodeContractId, source.ContractId, StringComparison.Ordinal)))
        {
            issues.Add(Issue(
                $"node:{source.ContractId}:exact",
                LibraryCompatibilityClassificationDto.Exact,
                "library.package_node_exact",
                "The node contract is unchanged. 节点契约未发生变化。",
                source.ContractId,
                target.ContractId));
        }
    }

    private static LibraryManifestNodeDto? FindTargetNode(
        LibraryManifestNodeDto source,
        IReadOnlyList<LibraryManifestNodeDto> targetNodes)
    {
        var exact = targetNodes.Where(node => string.Equals(node.ContractId, source.ContractId, StringComparison.Ordinal)).ToArray();
        if (exact.Length == 1)
            return exact[0];
        if (source.IdentityConfidence != LibraryContractIdentityConfidenceDto.Legacy)
            return null;

        var legacy = targetNodes.Where(node =>
            node.IdentityConfidence == LibraryContractIdentityConfidenceDto.Legacy
            && string.Equals(node.OverloadSignature, source.OverloadSignature, StringComparison.Ordinal)).ToArray();
        return legacy.Length == 1 ? legacy[0] : null;
    }

    private static LibraryManifestNodeDto? FindSourceNode(
        LibraryManifestNodeDto target,
        IReadOnlyList<LibraryManifestNodeDto> sourceNodes)
        => sourceNodes.Any(node => string.Equals(node.ContractId, target.ContractId, StringComparison.Ordinal))
            ? target
            : null;

    private static LibraryManifestParameterDto? FindTargetParameter(
        LibraryManifestParameterDto source,
        IReadOnlyList<LibraryManifestParameterDto> targetParameters)
        => targetParameters.SingleOrDefault(parameter =>
            string.Equals(parameter.ContractId, source.ContractId, StringComparison.Ordinal)
            || parameter.Aliases.Contains(source.ContractId, StringComparer.Ordinal));

    private static LibraryManifestParameterDto? FindSourceParameter(
        LibraryManifestParameterDto target,
        IReadOnlyList<LibraryManifestParameterDto> sourceParameters)
        => sourceParameters.SingleOrDefault(parameter =>
            string.Equals(parameter.ContractId, target.ContractId, StringComparison.Ordinal)
            || target.Aliases.Contains(parameter.ContractId, StringComparer.Ordinal));

    private static LibraryArtifactCompatibilityIssueDto Issue(
        string id,
        LibraryCompatibilityClassificationDto classification,
        string code,
        string message,
        string? sourceNodeContractId = null,
        string? targetNodeContractId = null,
        string? sourceParameterId = null,
        string? targetParameterId = null,
        bool blocks = false)
        => new(
            id,
            classification,
            code,
            message,
            sourceNodeContractId,
            targetNodeContractId,
            sourceParameterId,
            targetParameterId,
            blocks);
}

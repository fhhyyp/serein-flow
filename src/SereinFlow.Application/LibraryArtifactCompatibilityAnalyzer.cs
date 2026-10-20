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
            var issueResult = Issue("manifest-missing", LibraryCompatibilityClassificationDto.Unknown, LibraryErrorCodes.PackageManifestMissing, "A compatibility manifest is unavailable for the baseline or target artifact. 基线或目标工件缺少兼容性 Manifest。", blocks: true);
            issues.Add(issueResult);
        }
        else
        {
            CompareNodes(sourceManifest.Nodes, targetManifest.Nodes, issues);
        }

        var evaluatedValue = !issues.Any(static issue => issue.BlocksApplication);
        var orderByResult = issues.OrderBy(static issue => issue.Id, StringComparer.Ordinal);
        var toArrayResult = orderByResult.ToArray();
        var libraryArtifactCompatibilityDto = new LibraryArtifactCompatibilityDto(baseline.Id, baseline.Version, target.Id, target.Version, evaluatedValue, toArrayResult);
        return libraryArtifactCompatibilityDto;
    }

    private static void CompareNodes(IReadOnlyList<LibraryManifestNodeDto> sourceNodes, IReadOnlyList<LibraryManifestNodeDto> targetNodes, List<LibraryArtifactCompatibilityIssueDto> issues)
    {
        foreach (var source in sourceNodes.OrderBy(static node => node.ContractId, StringComparer.Ordinal))
        {
            var target = FindTargetNode(source, targetNodes);
            if (target is null)
            {
                var classification = source.IdentityConfidence == LibraryContractIdentityConfidenceDto.Explicit ? LibraryCompatibilityClassificationDto.Breaking : LibraryCompatibilityClassificationDto.Unknown;
                var issueResult1 = Issue($"node:{source.ContractId}:removed", classification, classification == LibraryCompatibilityClassificationDto.Breaking ? LibraryErrorCodes.PackageNodeRemoved : LibraryErrorCodes.PackageNodeMatchUnknown, classification == LibraryCompatibilityClassificationDto.Breaking ? "The baseline node contract is missing from the target artifact. 基线节点契约在目标工件中不存在。" : "The baseline node contract cannot be uniquely matched in the target artifact. 基线节点契约无法在目标工件中唯一匹配。", sourceNodeContractId: source.ContractId, blocks: true);
                issues.Add(issueResult1);
                continue;
            }

            CompareNode(source, target, issues);
        }

        foreach (var target in targetNodes.OrderBy(static node => node.ContractId, StringComparer.Ordinal))
        {
            if (FindSourceNode(target, sourceNodes)is not null)
            {
                continue;
            }

            foreach (var parameter in target.Parameters.Where(static parameter =>
            {
                var evaluatedValue2 = parameter.Required && string.IsNullOrWhiteSpace(parameter.DefaultValue);
                return evaluatedValue2;
            }))
            {
                var issueResult3 = Issue($"node:{target.ContractId}:required:{parameter.ContractId}", LibraryCompatibilityClassificationDto.RequiresRewire, LibraryErrorCodes.PackageRequiredParameterAdded, "The target artifact adds a required parameter without a default value. 目标工件新增了没有默认值的必需参数。", targetNodeContractId: target.ContractId, targetParameterId: parameter.ContractId, blocks: true);
                issues.Add(issueResult3);
            }
        }

        if (issues.Count == 0)
        {
            var issueResult4 = Issue("exact", LibraryCompatibilityClassificationDto.Exact, LibraryErrorCodes.PackageExact, "The target artifact exposes the same node and parameter contracts. 目标工件提供相同的节点和参数契约。");
            issues.Add(issueResult4);
        }
    }

    private static void CompareNode(LibraryManifestNodeDto source, LibraryManifestNodeDto target, List<LibraryArtifactCompatibilityIssueDto> issues)
    {
        if (source.Type != target.Type || source.IsAwaitable != target.IsAwaitable)
        {
            var issueResult5 = Issue($"node:{source.ContractId}:execution", LibraryCompatibilityClassificationDto.Breaking, LibraryErrorCodes.PackageNodeExecutionChanged, "The target node changes its node type or awaitable contract. 目标节点更改了节点类型或可等待契约。", source.ContractId, target.ContractId, blocks: true);
            issues.Add(issueResult5);
        }

        if (!string.Equals(source.ReturnType, target.ReturnType, StringComparison.Ordinal))
        {
            var issueResult6 = Issue($"node:{source.ContractId}:return-type", LibraryCompatibilityClassificationDto.Breaking, LibraryErrorCodes.PackageReturnTypeChanged, "The target node changes its return type. 目标节点更改了返回类型。", source.ContractId, target.ContractId, blocks: true);
            issues.Add(issueResult6);
        }

        foreach (var sourceParameter in source.Parameters.OrderBy(static parameter => parameter.ContractId, StringComparer.Ordinal))
        {
            var targetParameter = FindTargetParameter(sourceParameter, target.Parameters);
            if (targetParameter is null)
            {
                var issueResult7 = Issue($"node:{source.ContractId}:parameter:{sourceParameter.ContractId}:removed", LibraryCompatibilityClassificationDto.Breaking, LibraryErrorCodes.PackageParameterRemoved, "The target artifact removes a baseline parameter contract. 目标工件移除了基线参数契约。", source.ContractId, target.ContractId, sourceParameter.ContractId, blocks: true);
                issues.Add(issueResult7);
                continue;
            }

            if (!string.Equals(sourceParameter.Type, targetParameter.Type, StringComparison.Ordinal) || sourceParameter.IsVariadic != targetParameter.IsVariadic || !string.Equals(sourceParameter.ElementType, targetParameter.ElementType, StringComparison.Ordinal))
            {
                var issueResult8 = Issue($"node:{source.ContractId}:parameter:{sourceParameter.ContractId}:type", LibraryCompatibilityClassificationDto.Breaking, LibraryErrorCodes.PackageParameterTypeChanged, "The target parameter changes its CLR or variadic type contract. 目标参数更改了 CLR 或可变参数类型契约。", source.ContractId, target.ContractId, sourceParameter.ContractId, targetParameter.ContractId, blocks: true);
                issues.Add(issueResult8);
            }
            else if (!string.Equals(sourceParameter.ContractId, targetParameter.ContractId, StringComparison.Ordinal))
            {
                var issueResult9 = Issue($"node:{source.ContractId}:parameter:{sourceParameter.ContractId}:mapping", LibraryCompatibilityClassificationDto.RequiresMapping, LibraryErrorCodes.PackageParameterMappingRequired, "The target parameter uses a new ID and declares the baseline ID as an alias. 目标参数使用新 ID，并将基线 ID 声明为别名。", source.ContractId, target.ContractId, sourceParameter.ContractId, targetParameter.ContractId, blocks: true);
                issues.Add(issueResult9);
            }
            else if (sourceParameter.Required != targetParameter.Required || !string.Equals(sourceParameter.DefaultValue, targetParameter.DefaultValue, StringComparison.Ordinal))
            {
                var issueResult10 = Issue($"node:{source.ContractId}:parameter:{sourceParameter.ContractId}:metadata", LibraryCompatibilityClassificationDto.Compatible, LibraryErrorCodes.PackageParameterMetadataChanged, "The target parameter changes optionality or its default value without changing its type. 目标参数更改了可选性或默认值，但类型未变。");
                issues.Add(issueResult10);
            }
        }

        foreach (var targetParameter in target.Parameters)
        {
            if (FindSourceParameter(targetParameter, source.Parameters)is not null)
            {
                continue;
            }

            if (targetParameter.Required && string.IsNullOrWhiteSpace(targetParameter.DefaultValue))
            {
                var issueResult11 = Issue($"node:{target.ContractId}:required:{targetParameter.ContractId}", LibraryCompatibilityClassificationDto.RequiresRewire, LibraryErrorCodes.PackageRequiredParameterAdded, "The target artifact adds a required parameter without a default value. 目标工件新增了没有默认值的必需参数。", source.ContractId, target.ContractId, targetParameterId: targetParameter.ContractId, blocks: true);
                issues.Add(issueResult11);
            }
        }

        if (!issues.Any(issue =>
        {
            var equalsResult = string.Equals(issue.SourceNodeContractId, source.ContractId, StringComparison.Ordinal);
            return equalsResult;
        }))
        {
            var issueResult12 = Issue($"node:{source.ContractId}:exact", LibraryCompatibilityClassificationDto.Exact, LibraryErrorCodes.PackageNodeExact, "The node contract is unchanged. 节点契约未发生变化。", source.ContractId, target.ContractId);
            issues.Add(issueResult12);
        }
    }

    private static LibraryManifestNodeDto? FindTargetNode(LibraryManifestNodeDto source, IReadOnlyList<LibraryManifestNodeDto> targetNodes)
    {
        var whereResult = targetNodes.Where(node =>
        {
            var equalsResult13 = string.Equals(node.ContractId, source.ContractId, StringComparison.Ordinal);
            return equalsResult13;
        });
        var exact = whereResult.ToArray();
        if (exact.Length == 1)
        {
            return exact[0];
        }

        if (source.IdentityConfidence != LibraryContractIdentityConfidenceDto.Legacy)
        {
            return null;
        }

        var whereResult15 = targetNodes.Where(node =>
        {
            var evaluatedValue14 = node.IdentityConfidence == LibraryContractIdentityConfidenceDto.Legacy && string.Equals(node.OverloadSignature, source.OverloadSignature, StringComparison.Ordinal);
            return evaluatedValue14;
        });
        var legacy = whereResult15.ToArray();
        return legacy.Length == 1 ? legacy[0] : null;
    }

    private static LibraryManifestNodeDto? FindSourceNode(LibraryManifestNodeDto target, IReadOnlyList<LibraryManifestNodeDto> sourceNodes)
    {
        global::SereinFlow.Contracts.LibraryManifestNodeDto? evaluatedValue17 = sourceNodes.Any(node =>
        {
            var equalsResult16 = string.Equals(node.ContractId, target.ContractId, StringComparison.Ordinal);
            return equalsResult16;
        }) ? target : null;
        return evaluatedValue17;
    }

    private static LibraryManifestParameterDto? FindTargetParameter(LibraryManifestParameterDto source, IReadOnlyList<LibraryManifestParameterDto> targetParameters)
    {
        var singleOrDefaultResult = targetParameters.SingleOrDefault(parameter =>
        {
            var evaluatedValue18 = string.Equals(parameter.ContractId, source.ContractId, StringComparison.Ordinal) || parameter.Aliases.Contains(source.ContractId, StringComparer.Ordinal);
            return evaluatedValue18;
        });
        return singleOrDefaultResult;
    }

    private static LibraryManifestParameterDto? FindSourceParameter(LibraryManifestParameterDto target, IReadOnlyList<LibraryManifestParameterDto> sourceParameters)
    {
        var singleOrDefaultResult20 = sourceParameters.SingleOrDefault(parameter =>
        {
            var evaluatedValue19 = string.Equals(parameter.ContractId, target.ContractId, StringComparison.Ordinal) || target.Aliases.Contains(parameter.ContractId, StringComparer.Ordinal);
            return evaluatedValue19;
        });
        return singleOrDefaultResult20;
    }

    private static LibraryArtifactCompatibilityIssueDto Issue(string id, LibraryCompatibilityClassificationDto classification, string code, string message, string? sourceNodeContractId = null, string? targetNodeContractId = null, string? sourceParameterId = null, string? targetParameterId = null, bool blocks = false)
    {
        global::SereinFlow.Contracts.LibraryArtifactCompatibilityIssueDto evaluatedValue21 = new(id, classification, code, message, sourceNodeContractId, targetNodeContractId, sourceParameterId, targetParameterId, blocks);
        return evaluatedValue21;
    }
}

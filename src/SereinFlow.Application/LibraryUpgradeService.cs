using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Application;

public sealed record LibraryUpgradeOperationResult<T>(
    bool IsSuccess,
    int StatusCode,
    T? Value = default,
    string? Code = null,
    string? Message = null,
    long? CurrentVersion = null);

/// <summary>
/// Coordinates explicit, versioned library upgrades. This service creates a
/// preview first and only commits a new flow version after the same source and
/// target manifests are re-analysed against the expected current version.
/// 协调显式、版本化的类库升级。本服务先创建预览，随后仅在相同源/目标 Manifest
/// 针对期望的当前流程版本重新分析通过后，才提交新的流程版本。
/// </summary>
public sealed class LibraryUpgradeService
{
    private readonly IProjectRepository _projects;
    private readonly IFlowDefinitionRepository _flows;
    private readonly ILibraryCatalogService _catalog;
    private readonly ILibraryCompatibilityAnalyzer _analyzer;
    private readonly IFlowLibraryUpgradeStore _store;

    public LibraryUpgradeService(
        IProjectRepository projects,
        IFlowDefinitionRepository flows,
        ILibraryCatalogService catalog,
        ILibraryCompatibilityAnalyzer analyzer,
        IFlowLibraryUpgradeStore store)
    {
        _projects = projects;
        _flows = flows;
        _catalog = catalog;
        _analyzer = analyzer;
        _store = store;
    }

    public async Task<LibraryUpgradeOperationResult<LibraryUpgradePlanDto>> PreviewAsync(
        Guid projectId,
        LibraryUpgradePreviewRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await _projects.FindAsync(projectId, cancellationToken) is null)
            return NotFound<LibraryUpgradePlanDto>("project.not_found", "Project was not found. 未找到项目。");

        var libraries = await ResolveLibrariesAsync(request.SourceArtifactId, request.TargetArtifactId, cancellationToken);
        if (libraries is not { } pair)
            return Conflict<LibraryUpgradePlanDto>("library.not_found", "One or both library artifacts were not found. 一个或两个类库工件不存在。");
        var (source, target) = pair;

        var family = ValidateFamily(source, target);
        if (family is not null)
            return Conflict<LibraryUpgradePlanDto>(family.Value.Code, family.Value.Message);

        var requestedIds = request.FlowIds?.Distinct().ToArray() ?? [];
        if (requestedIds.Length == 0)
        {
            return new LibraryUpgradeOperationResult<LibraryUpgradePlanDto>(
                false,
                400,
                Code: "library.upgrade_flow_required",
                Message: "At least one flow must be selected for a library upgrade preview. 类库升级预览至少需要选择一个流程。");
        }

        var previews = new List<FlowLibraryUpgradePreviewDto>(requestedIds.Length);
        foreach (var flowId in requestedIds)
        {
            var flow = await _flows.FindAsync(projectId, flowId, cancellationToken);
            if (flow is null)
                return NotFound<LibraryUpgradePlanDto>("flow.not_found", "A selected flow definition was not found. 选定的流程定义不存在。");
            previews.Add(_analyzer.Analyze(flow, source, target));
        }

        var plan = new LibraryUpgradePlanDto(
            Guid.NewGuid(),
            projectId,
            source.Id,
            target.Id,
            LibraryUpgradePlanStatusDto.Analyzed,
            previews,
            DateTimeOffset.UtcNow);
        return new LibraryUpgradeOperationResult<LibraryUpgradePlanDto>(
            true,
            201,
            await _store.SavePlanAsync(plan, cancellationToken));
    }

    public async Task<LibraryUpgradeOperationResult<LibraryUpgradePlanDto>> GetPlanAsync(
        Guid projectId,
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        var plan = await _store.FindPlanAsync(projectId, planId, cancellationToken);
        return plan is null
            ? NotFound<LibraryUpgradePlanDto>("library.upgrade_not_found", "The library upgrade preview was not found. 未找到类库升级预览。")
            : new LibraryUpgradeOperationResult<LibraryUpgradePlanDto>(true, 200, plan);
    }

    public async Task<LibraryUpgradeOperationResult<LibraryUpgradeApplyResultDto>> ApplyAsync(
        Guid projectId,
        Guid planId,
        ApplyLibraryUpgradeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExpectedFlowVersion < 1)
        {
            return new LibraryUpgradeOperationResult<LibraryUpgradeApplyResultDto>(
                false,
                400,
                Code: "flow.version_invalid",
                Message: "The expected flow version must be positive. 期望流程版本必须为正数。");
        }

        var plan = await _store.FindPlanAsync(projectId, planId, cancellationToken);
        if (plan is null)
            return NotFound<LibraryUpgradeApplyResultDto>("library.upgrade_not_found", "The library upgrade preview was not found. 未找到类库升级预览。");
        if (plan.Status is not (LibraryUpgradePlanStatusDto.Analyzed or LibraryUpgradePlanStatusDto.Applied))
        {
            return Conflict<LibraryUpgradeApplyResultDto>(
                "library.upgrade_not_applicable",
                "Only an active library upgrade preview can be applied. 只有活动的类库升级预览可以应用。");
        }
        if (!plan.Flows.Any(flow => flow.FlowId == request.FlowId))
        {
            return new LibraryUpgradeOperationResult<LibraryUpgradeApplyResultDto>(
                false,
                400,
                Code: "library.upgrade_flow_not_in_plan",
                Message: "The selected flow is not included in this upgrade preview. 选定流程不在此升级预览中。");
        }
        if ((plan.AppliedFlows ?? []).Any(result => result.FlowId == request.FlowId))
        {
            return Conflict<LibraryUpgradeApplyResultDto>(
                "library.upgrade_flow_already_applied",
                "The selected flow has already been upgraded by this plan. 选定流程已通过此计划完成升级。");
        }

        var flow = await _flows.FindAsync(projectId, request.FlowId, cancellationToken);
        if (flow is null)
            return NotFound<LibraryUpgradeApplyResultDto>("flow.not_found", "The selected flow definition was not found. 选定的流程定义不存在。");
        if (flow.Version != request.ExpectedFlowVersion)
        {
            return new LibraryUpgradeOperationResult<LibraryUpgradeApplyResultDto>(
                false,
                409,
                Code: "flow.version_conflict",
                Message: "The flow version changed before the library upgrade could be applied. 类库升级应用前流程版本已发生变化。",
                CurrentVersion: flow.Version);
        }

        var libraries = await ResolveLibrariesAsync(plan.SourceArtifactId, plan.TargetArtifactId, cancellationToken);
        if (libraries is not { } pair)
            return Conflict<LibraryUpgradeApplyResultDto>("library.not_found", "One or both library artifacts were not found. 一个或两个类库工件不存在。");
        var (source, target) = pair;
        var family = ValidateFamily(source, target);
        if (family is not null)
            return Conflict<LibraryUpgradeApplyResultDto>(family.Value.Code, family.Value.Message);
        if (target.Lifecycle != LibraryLifecycleDto.Available)
        {
            return Conflict<LibraryUpgradeApplyResultDto>(
                "library.archived",
                "An archived library artifact cannot be used as an upgrade target. 已归档类库工件不能作为升级目标。");
        }

        var analysis = _analyzer.Analyze(flow, source, target);
        var blocked = analysis.Issues.Where(static issue => issue.BlocksApplication).ToArray();
        if (blocked.Length > 0)
        {
            return new LibraryUpgradeOperationResult<LibraryUpgradeApplyResultDto>(
                false,
                409,
                Code: "library.upgrade_blocked",
                Message: "The library upgrade contains blocking compatibility changes. 类库升级包含阻断性兼容性变更。");
        }

        var acknowledgements = (request.AcknowledgedItemIds ?? [])
            .ToHashSet(StringComparer.Ordinal);
        var missingAcknowledgements = analysis.Issues
            .Where(static issue => issue.RequiresAcknowledgement)
            .Where(issue => !acknowledgements.Contains(issue.Id))
            .ToArray();
        if (missingAcknowledgements.Length > 0)
        {
            return new LibraryUpgradeOperationResult<LibraryUpgradeApplyResultDto>(
                false,
                409,
                Code: "library.upgrade_confirmation_required",
                Message: "All parameter mappings that require confirmation must be acknowledged. 必须确认所有需要确认的参数映射。");
        }

        FlowDefinitionDto upgraded;
        try
        {
            upgraded = BuildUpgradedDefinition(flow, source, target);
        }
        catch (LibraryUpgradeTransformationException exception)
        {
            return new LibraryUpgradeOperationResult<LibraryUpgradeApplyResultDto>(false, 409, Code: exception.Code, Message: exception.Message);
        }

        var validation = FlowDefinitionContractValidator.ValidateForPersistence(upgraded);
        if (!validation.IsValid)
        {
            return new LibraryUpgradeOperationResult<LibraryUpgradeApplyResultDto>(
                false,
                409,
                Code: "library.upgrade_flow_invalid",
                Message: validation.Diagnostics.Count > 0
                    ? validation.Diagnostics[0].Message
                    : "The upgraded flow is invalid. 升级后的流程无效。");
        }

        upgraded = FlowDefinitionContractNormalizer.NormalizeForPersistence(upgraded);
        var commit = await _store.CommitAsync(
            projectId,
            planId,
            upgraded,
            request.ExpectedFlowVersion,
            target.Id,
            appliedFlow: null,
            cancellationToken: cancellationToken);
        if (!commit.IsCommitted)
        {
            return new LibraryUpgradeOperationResult<LibraryUpgradeApplyResultDto>(
                false,
                409,
                Code: "flow.version_conflict",
                Message: "The flow version changed before the library upgrade could be applied. 类库升级应用前流程版本已发生变化。",
                CurrentVersion: commit.CurrentVersion);
        }

        var saved = commit.Definition!;
        return new LibraryUpgradeOperationResult<LibraryUpgradeApplyResultDto>(
            true,
            200,
            new LibraryUpgradeApplyResultDto(
                saved.Id,
                request.ExpectedFlowVersion,
                saved.Version,
                source.Id,
                target.Id,
                analysis.AffectedNodeCount,
                analysis.Issues));
    }

    /// <summary>
    /// Applies selected flows one by one. Every call to <see cref="ApplyAsync"/>
    /// owns its own transaction, so a conflict in one flow leaves other flow
    /// upgrades intact and retryable.
    /// 按流程逐一应用升级。每次 <see cref="ApplyAsync"/> 调用都有自己的事务，
    /// 因此一个流程的冲突不会影响其它流程的升级，并可独立重试。
    /// </summary>
    public async Task<LibraryUpgradeOperationResult<LibraryUpgradeBatchApplyResultDto>> ApplyBatchAsync(
        Guid projectId,
        Guid planId,
        ApplyLibraryUpgradeBatchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var requests = request.Flows?.ToArray() ?? [];
        if (requests.Length == 0)
        {
            return new LibraryUpgradeOperationResult<LibraryUpgradeBatchApplyResultDto>(
                false,
                400,
                Code: "library.upgrade_flow_required",
                Message: "At least one flow must be selected for a library upgrade. 类库升级至少需要选择一个流程。");
        }
        if (requests.Select(item => item.FlowId).Distinct().Count() != requests.Length)
        {
            return new LibraryUpgradeOperationResult<LibraryUpgradeBatchApplyResultDto>(
                false,
                400,
                Code: "library.upgrade_duplicate_flow",
                Message: "Each flow can appear only once in a library upgrade batch. 类库升级批次中每个流程只能出现一次。");
        }

        var succeeded = new List<LibraryUpgradeApplyResultDto>(requests.Length);
        var failed = new List<LibraryUpgradeApplyFailureDto>();
        foreach (var item in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await ApplyAsync(projectId, planId, item, cancellationToken);
            if (result.IsSuccess && result.Value is not null)
            {
                succeeded.Add(result.Value);
                continue;
            }

            failed.Add(new LibraryUpgradeApplyFailureDto(
                item.FlowId,
                result.StatusCode,
                result.Code,
                result.Message,
                result.CurrentVersion));
        }

        return new LibraryUpgradeOperationResult<LibraryUpgradeBatchApplyResultDto>(
            true,
            200,
            new LibraryUpgradeBatchApplyResultDto(planId, succeeded, failed));
    }

    private async Task<(LibraryDto Source, LibraryDto Target)?> ResolveLibrariesAsync(
        string sourceArtifactId,
        string targetArtifactId,
        CancellationToken cancellationToken)
    {
        var source = await _catalog.FindAsync(sourceArtifactId, cancellationToken);
        var target = await _catalog.FindAsync(targetArtifactId, cancellationToken);
        return source is null || target is null ? null : (source, target);
    }

    private static (string Code, string Message)? ValidateFamily(LibraryDto source, LibraryDto target)
    {
        if (string.IsNullOrWhiteSpace(source.FamilyId) || string.IsNullOrWhiteSpace(target.FamilyId))
        {
            return (
                "library.upgrade_family_unassigned",
                "Both library artifacts must be explicitly assigned to the same library family before an upgrade. 两个类库工件必须先显式归入同一个类库族，才能升级。");
        }
        if (!string.Equals(source.FamilyId, target.FamilyId, StringComparison.OrdinalIgnoreCase))
        {
            return (
                "library.upgrade_family_mismatch",
                "Library artifacts from different families cannot be upgraded together. 不同类库族的工件不能一起升级。");
        }
        return null;
    }

    private static LibraryUpgradeOperationResult<T> NotFound<T>(string code, string message)
        => new(false, 404, Code: code, Message: message);

    private static LibraryUpgradeOperationResult<T> Conflict<T>(string code, string message)
        => new(false, 409, Code: code, Message: message);

    private static FlowDefinitionDto BuildUpgradedDefinition(
        FlowDefinitionDto sourceDefinition,
        LibraryDto sourceLibrary,
        LibraryDto targetLibrary)
    {
        var sourceManifest = sourceLibrary.CompatibilityManifest
            ?? throw new LibraryUpgradeTransformationException(
                "library.upgrade_manifest_missing",
                "The source compatibility manifest is unavailable. 源兼容性 Manifest 不可用。");
        var targetManifest = targetLibrary.CompatibilityManifest
            ?? throw new LibraryUpgradeTransformationException(
                "library.upgrade_manifest_missing",
                "The target compatibility manifest is unavailable. 目标兼容性 Manifest 不可用。");
        var parameterMaps = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        var canvases = sourceDefinition.Canvases.Select(canvas =>
        {
            var nodes = canvas.Nodes.Select(node =>
            {
                if (node.Type is not (NodeTypeDto.Action or NodeTypeDto.Flipflop)
                    || !string.Equals(node.Ui?.LibraryId, sourceLibrary.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return node;
                }

                var transformed = TransformNode(node, sourceManifest, targetManifest, targetLibrary);
                parameterMaps[node.Id] = transformed.ParameterMap;
                return transformed.Node;
            }).ToArray();
            var connections = canvas.Connections.Select(connection =>
            {
                if (connection.Kind == ConnectionKindDto.Data
                    && parameterMaps.TryGetValue(connection.ToNodeId, out var map)
                    && map.TryGetValue(connection.ToPortId, out var targetPortId))
                {
                    return connection with { ToPortId = targetPortId };
                }
                return connection;
            }).ToArray();
            return canvas with { Nodes = nodes, Connections = connections };
        }).ToArray();
        return sourceDefinition with { Canvases = canvases };
    }

    private static TransformedNode TransformNode(
        NodeDto node,
        LibraryArtifactManifestDto sourceManifest,
        LibraryArtifactManifestDto targetManifest,
        LibraryDto targetLibrary)
    {
        var sourceManifestNode = LibraryCompatibilityAnalyzer.FindSourceNode(sourceManifest, node.Ui)
            ?? throw new LibraryUpgradeTransformationException(
                "library.upgrade_source_contract_unknown",
                "The source node cannot be resolved for upgrade. 无法解析要升级的源节点。");
        var targetManifestNode = LibraryCompatibilityAnalyzer.FindTargetNode(sourceManifestNode, targetManifest)
            ?? throw new LibraryUpgradeTransformationException(
                "library.upgrade_target_contract_unknown",
                "The target node cannot be resolved for upgrade. 无法解析要升级的目标节点。");
        var targetCatalogNode = targetLibrary.Nodes.SingleOrDefault(candidate =>
            string.Equals(candidate.ContractId, targetManifestNode.ContractId, StringComparison.Ordinal)
            && candidate.Type == targetManifestNode.Type);
        if (targetCatalogNode is null)
        {
            throw new LibraryUpgradeTransformationException(
                "library.upgrade_target_catalog_invalid",
                "The target node is missing from the safe library catalog. 目标节点缺少安全类库目录元数据。");
        }

        var sourceParameters = node.Parameters.ToArray();
        var parameterMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var transformedParameters = new List<NodeParameterDto>(targetManifestNode.Parameters.Count);
        foreach (var targetManifestParameter in targetManifestNode.Parameters)
        {
            var sourceManifestParameter = sourceManifestNode.Parameters.SingleOrDefault(candidate =>
                string.Equals(candidate.ContractId, targetManifestParameter.ContractId, StringComparison.Ordinal)
                || targetManifestParameter.Aliases.Contains(candidate.ContractId, StringComparer.Ordinal));
            var targetCatalogParameter = targetCatalogNode.Parameters.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, targetManifestParameter.ContractId, StringComparison.Ordinal));
            if (targetCatalogParameter is null)
            {
                throw new LibraryUpgradeTransformationException(
                    "library.upgrade_target_catalog_invalid",
                    "The target parameter is missing from the safe library catalog. 目标参数缺少安全类库目录元数据。");
            }

            var existing = sourceManifestParameter is null
                ? null
                : sourceParameters.FirstOrDefault(parameter =>
                    string.Equals(parameter.Ui?.Id ?? parameter.Name, sourceManifestParameter.ContractId, StringComparison.Ordinal));
            if (sourceManifestParameter is not null)
                parameterMap[sourceManifestParameter.ContractId] = targetManifestParameter.ContractId;
            transformedParameters.Add(existing is null
                ? CreateAddedParameter(targetManifestParameter, targetCatalogParameter)
                : TransformParameter(existing, targetManifestParameter, targetCatalogParameter));
        }

        var runtime = node.Ui! with
        {
            LibraryId = targetLibrary.Id,
            ClassName = targetCatalogNode.ClassName,
            MethodName = targetCatalogNode.MethodName,
            DllName = targetCatalogNode.DllName,
            DllVersion = targetCatalogNode.DllVersion,
            ReturnType = targetCatalogNode.ReturnType,
            IsAwaitable = targetCatalogNode.IsAwaitable,
            LibraryNodeContractId = targetCatalogNode.ContractId,
            FlowLibraryName = targetCatalogNode.FlowLibraryName,
        };
        return new TransformedNode(node with { Type = targetCatalogNode.Type, Parameters = transformedParameters, Ui = runtime }, parameterMap);
    }

    private static NodeParameterDto TransformParameter(
        NodeParameterDto source,
        LibraryManifestParameterDto targetManifest,
        LibraryParameterDto targetCatalog)
    {
        var existingUi = source.Ui ?? new NodeParameterUiMetadataDto(
            targetManifest.ContractId,
            targetManifest.DisplayName,
            targetManifest.Type,
            null,
            null,
            null,
            null,
            null);
        return source with
        {
            Name = targetManifest.ClrName,
            Required = targetManifest.Required,
            Ui = existingUi with
            {
                Id = targetManifest.ContractId,
                NameKey = targetManifest.DisplayName,
                ValueKind = targetManifest.Type,
                Type = targetManifest.Type,
                Description = targetCatalog.Description,
                IsVariadic = targetManifest.IsVariadic,
                VariadicGroupId = targetManifest.IsVariadic ? targetManifest.ContractId : null,
                ElementType = targetManifest.ElementType,
                EnumMetadata = targetManifest.EnumMetadata,
                InputMode = targetManifest.EnumMetadata is null ? "manual" : "select",
            },
        };
    }

    private static NodeParameterDto CreateAddedParameter(
        LibraryManifestParameterDto targetManifest,
        LibraryParameterDto targetCatalog)
        => new(
            targetManifest.ClrName,
            targetManifest.DefaultValue,
            DataSourceDto.Literal,
            targetManifest.Required,
            new NodeParameterUiMetadataDto(
                targetManifest.ContractId,
                targetManifest.DisplayName,
                targetManifest.Type,
                targetManifest.DefaultValue,
                null,
                null,
                null,
                null,
                Type: targetManifest.Type,
                Description: targetCatalog.Description,
                InputMode: targetManifest.EnumMetadata is null ? "manual" : "select",
                IsVariadic: targetManifest.IsVariadic,
                VariadicGroupId: targetManifest.IsVariadic ? targetManifest.ContractId : null,
                ElementType: targetManifest.ElementType,
                EnumMetadata: targetManifest.EnumMetadata));

    private sealed record TransformedNode(NodeDto Node, IReadOnlyDictionary<string, string> ParameterMap);

    private sealed class LibraryUpgradeTransformationException(string code, string message) : Exception(message)
    {
        public string Code { get; } = code;
    }
}

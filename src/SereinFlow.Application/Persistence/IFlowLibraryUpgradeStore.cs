using SereinFlow.Contracts;

namespace SereinFlow.Application.Persistence;

/// <summary>
/// Persists an upgrade preview and atomically creates the next flow version,
/// target project reference, removes an unused source project reference,
/// updates the binding index and records applied-plan state. The application
/// layer never receives a database client for this workflow.
/// 保存升级预览，并以原子方式创建下一流程版本、目标项目引用、取消未使用的来源项目引用、
/// 更新绑定索引和已应用计划状态。
/// 应用层不会因该工作流获得数据库客户端。
/// </summary>
public interface IFlowLibraryUpgradeStore
{
    Task<LibraryUpgradePlanDto> SavePlanAsync(
        LibraryUpgradePlanDto plan,
        CancellationToken cancellationToken = default);

    Task<LibraryUpgradePlanDto?> FindPlanAsync(
        Guid projectId,
        Guid planId,
        CancellationToken cancellationToken = default);

    Task<FlowLibraryUpgradeCommitResult> CommitAsync(
        Guid projectId,
        Guid planId,
        FlowDefinitionDto upgradedDefinition,
        long expectedVersion,
        string targetArtifactId,
        LibraryUpgradePlanFlowResultDto? appliedFlow = null,
        CancellationToken cancellationToken = default);
}

public sealed record FlowLibraryUpgradeCommitResult(
    FlowDefinitionDto? Definition,
    long? CurrentVersion = null)
{
    public bool IsCommitted => Definition is not null;
}

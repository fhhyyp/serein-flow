using System.Text.Json;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application.Persistence;
public interface IFlowRunStore
{
    Task<FlowRun> CreateWithSnapshotAsync(FlowRun run, FlowDefinitionDto definition, FlowRunExecutionOptions options, CancellationToken cancellationToken = default);
    /// <summary>
    /// Atomically persists a run and its immutable snapshot. Exclusive flows
    /// return an admission conflict instead of leaving a queued duplicate.
    /// 原子写入运行与不可变快照；独占流程发生冲突时不会留下排队的重复实例。
    /// </summary>
    Task<FlowRunAdmissionResult> TryCreateWithSnapshotAsync(FlowRun run, FlowDefinitionDto definition, FlowRunExecutionOptions options, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PendingFlowRun>> ListPendingAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FlowRun>> ListAsync(FlowRunQuery query, CancellationToken cancellationToken = default);
    Task<FlowRun> CreateWithSnapshotAsync(FlowRun run, FlowDefinitionDto definition, CancellationToken cancellationToken = default);
    Task<FlowRun?> FindAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<FlowDefinitionDto?> GetSnapshotAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<bool> SaveAsync(FlowRun run, CancellationToken cancellationToken = default);
    FlowRun CreateWithSnapshot(FlowRun run, FlowDefinitionDto definition);
    FlowRun? Find(Guid runId);
    FlowDefinitionDto? GetSnapshot(Guid runId);
    bool Save(FlowRun run);
}

/// <summary>
/// Configures flow run execution behavior.
/// 配置流程运行执行行为。
/// </summary>
public sealed record FlowRunExecutionOptions(IReadOnlyDictionary<string, JsonElement>? ProjectInputs, int TimeoutSeconds, int MaxSteps, int MaxNodeVisits = 1_000);
/// <summary>
/// Represents pending flow run data used by SereinFlow.
/// 表示 SereinFlow 使用的Pending流程运行数据。
/// </summary>
public sealed record PendingFlowRun(FlowRun Run, FlowDefinitionDto Definition, FlowRunExecutionOptions Options);
/// <summary>
/// Captures the outcome of flow run admission operations.
/// 记录流程运行接纳操作的结果。
/// </summary>
public sealed record FlowRunAdmissionResult(FlowRun? Run, Guid? ActiveRunId = null)
{
    public bool IsAdmitted => Run is not null;
}

/// <summary>
/// Represents flow run query data used by SereinFlow.
/// 表示 SereinFlow 使用的流程运行查询数据。
/// </summary>
public sealed record FlowRunQuery(IReadOnlyCollection<FlowRunStatus>? Statuses = null, Guid? ProjectId = null, int Take = 100);

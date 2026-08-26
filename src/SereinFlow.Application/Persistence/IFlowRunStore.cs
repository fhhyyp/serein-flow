using System.Text.Json;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application.Persistence;

public interface IFlowRunStore
{
    Task<FlowRun> CreateWithSnapshotAsync(
        FlowRun run,
        FlowDefinitionDto definition,
        FlowRunExecutionOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically persists a run and its immutable snapshot. Exclusive flows
    /// return an admission conflict instead of leaving a queued duplicate.
    /// 原子写入运行与不可变快照；独占流程发生冲突时不会留下排队的重复实例。
    /// </summary>
    Task<FlowRunAdmissionResult> TryCreateWithSnapshotAsync(
        FlowRun run,
        FlowDefinitionDto definition,
        FlowRunExecutionOptions options,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingFlowRun>> ListPendingAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FlowRun>> ListAsync(
        FlowRunQuery query,
        CancellationToken cancellationToken = default);

    Task<FlowRun> CreateWithSnapshotAsync(FlowRun run, FlowDefinitionDto definition, CancellationToken cancellationToken = default);

    Task<FlowRun?> FindAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<FlowDefinitionDto?> GetSnapshotAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<bool> SaveAsync(FlowRun run, CancellationToken cancellationToken = default);

    FlowRun CreateWithSnapshot(
        FlowRun run,
        FlowDefinitionDto definition);

    FlowRun? Find(Guid runId);

    FlowDefinitionDto? GetSnapshot(Guid runId);

    bool Save(FlowRun run);
}

public sealed record FlowRunExecutionOptions(
    IReadOnlyDictionary<string, JsonElement>? ProjectInputs,
    int TimeoutSeconds,
    int MaxSteps,
    int MaxNodeVisits = 1_000);

public sealed record PendingFlowRun(
    FlowRun Run,
    FlowDefinitionDto Definition,
    FlowRunExecutionOptions Options);

public sealed record FlowRunAdmissionResult(FlowRun? Run, Guid? ActiveRunId = null)
{
    public bool IsAdmitted => Run is not null;
}

public sealed record FlowRunQuery(
    IReadOnlyCollection<FlowRunStatus>? Statuses = null,
    Guid? ProjectId = null,
    int Take = 100);

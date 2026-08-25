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

    Task<IReadOnlyList<PendingFlowRun>> ListPendingAsync(CancellationToken cancellationToken = default);

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
    DateTimeOffset Deadline,
    int MaxSteps,
    int MaxNodeVisits = 1_000);

public sealed record PendingFlowRun(
    FlowRun Run,
    FlowDefinitionDto Definition,
    FlowRunExecutionOptions Options);

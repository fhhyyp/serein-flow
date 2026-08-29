using SereinFlow.Contracts;

namespace SereinFlow.Application.Persistence;

public interface IFlowVersionRepository
{
    Task<IReadOnlyList<FlowVersionSummaryDto>> ListVersionsAsync(
        Guid projectId,
        Guid flowId,
        FlowVersionTrackDto track,
        CancellationToken cancellationToken = default);

    Task<FlowVersionDetailDto?> FindVersionAsync(
        Guid projectId,
        Guid flowId,
        long version,
        CancellationToken cancellationToken = default);

    Task<FlowDefinitionDto?> FindProductionDefinitionAsync(
        Guid projectId,
        Guid flowId,
        CancellationToken cancellationToken = default);

    Task<long?> FindProductionVersionAsync(
        Guid projectId,
        Guid flowId,
        CancellationToken cancellationToken = default);

    Task<FlowVersionMutationResult> PublishAsync(
        Guid projectId,
        Guid flowId,
        long expectedDevelopmentVersion,
        string? remark,
        long? expectedProductionVersion = null,
        CancellationToken cancellationToken = default);

    Task<FlowVersionMutationResult> RollbackAsync(
        Guid projectId,
        Guid flowId,
        long sourceVersion,
        FlowVersionTrackDto track,
        long expectedHeadVersion,
        CancellationToken cancellationToken = default);

    Task<bool> IsLibraryReferencedByProductionHistoryAsync(
        Guid projectId,
        string libraryId,
        CancellationToken cancellationToken = default);
}

public sealed record FlowVersionMutationResult(
    FlowDefinitionDto? DevelopmentDefinition,
    FlowVersionSummaryDto? Version,
    long? CurrentHeadVersion = null)
{
    public bool IsCommitted => Version is not null;
}

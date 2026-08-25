using SereinFlow.Contracts;

namespace SereinFlow.Application.Persistence;

public interface IFlowDefinitionRepository
{
    Task<IReadOnlyList<FlowDefinitionDto>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<FlowDefinitionDto?> FindAsync(Guid projectId, Guid flowId, CancellationToken cancellationToken = default);

    Task AddAsync(Guid projectId, FlowDefinitionDto definition, CancellationToken cancellationToken = default);

    Task<FlowDefinitionDto?> TryUpdateAsync(Guid projectId, FlowDefinitionDto definition, long expectedVersion, CancellationToken cancellationToken = default);

    IReadOnlyList<FlowDefinitionDto> ListByProject(Guid projectId);

    FlowDefinitionDto? Find(Guid projectId, Guid flowId);

    void Add(Guid projectId, FlowDefinitionDto definition);

    FlowDefinitionDto? TryUpdate(Guid projectId, FlowDefinitionDto definition, long expectedVersion);
}

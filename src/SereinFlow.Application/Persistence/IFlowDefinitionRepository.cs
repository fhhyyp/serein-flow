using SereinFlow.Contracts;

namespace SereinFlow.Application.Persistence;

public interface IFlowDefinitionRepository
{
    IReadOnlyList<FlowDefinitionDto> ListByProject(Guid projectId);

    FlowDefinitionDto? Find(Guid projectId, Guid flowId);

    void Add(Guid projectId, FlowDefinitionDto definition);

    FlowDefinitionDto? TryUpdate(Guid projectId, FlowDefinitionDto definition, long expectedVersion);
}

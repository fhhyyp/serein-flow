using SereinFlow.Domain;

namespace SereinFlow.Application.Persistence;

public interface IFlowRunEventStore
{
    void Append(IReadOnlyList<FlowRunEvent> events);

    IReadOnlyList<FlowRunEvent> GetAfter(Guid runId, long sequenceExclusive);
}

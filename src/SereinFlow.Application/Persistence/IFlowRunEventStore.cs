using SereinFlow.Domain;

namespace SereinFlow.Application.Persistence;

public interface IFlowRunEventStore
{
    Task AppendAsync(IReadOnlyList<FlowRunEvent> events, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FlowRunEvent>> GetAfterAsync(Guid runId, long sequenceExclusive, CancellationToken cancellationToken = default);

    Task<long> GetLastSequenceAsync(Guid runId, CancellationToken cancellationToken = default);

    void Append(IReadOnlyList<FlowRunEvent> events);

    IReadOnlyList<FlowRunEvent> GetAfter(Guid runId, long sequenceExclusive);
}

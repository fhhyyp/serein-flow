using SereinFlow.Domain;

namespace SereinFlow.Application.Persistence;

public interface IFlowDebugSessionStore
{
    Task<FlowDebugSession> CreateAsync(FlowDebugSession session, CancellationToken cancellationToken = default);

    Task<FlowDebugSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<FlowDebugSession?> FindByRunIdAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FlowDebugSession>> ListActiveAsync(CancellationToken cancellationToken = default);

    Task<bool> SaveAsync(FlowDebugSession session, CancellationToken cancellationToken = default);
}

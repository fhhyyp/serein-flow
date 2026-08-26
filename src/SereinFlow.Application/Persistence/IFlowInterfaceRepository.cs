using SereinFlow.Contracts;

namespace SereinFlow.Application.Persistence;

public interface IFlowInterfaceRepository
{
    Task<IReadOnlyList<FlowInterfaceDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<FlowInterfaceDto?> FindAsync(Guid interfaceId, CancellationToken cancellationToken = default);

    Task<FlowInterfaceDto> AddAsync(FlowInterfaceDto flowInterface, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(FlowInterfaceDto flowInterface, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid interfaceId, CancellationToken cancellationToken = default);
}

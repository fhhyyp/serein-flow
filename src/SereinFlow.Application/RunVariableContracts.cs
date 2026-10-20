using SereinFlow.Contracts;

namespace SereinFlow.Application;
/// <summary>Shared REST/MCP access to the variable table of an active run.</summary>
public interface IRunVariableService
{
    Task<WorkerVariableResponseDto> AccessAsync(WorkerVariableRequestDto request, CancellationToken cancellationToken = default);
}

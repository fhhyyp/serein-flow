using SereinFlow.Contracts;

namespace SereinFlow.Application;
/// <summary>
/// Shared REST/MCP access to the variable table of an active run.
/// 通过 REST 和 MCP 共用接口访问活动运行的变量表。
/// </summary>
public interface IRunVariableService
{
    Task<WorkerVariableResponseDto> AccessAsync(WorkerVariableRequestDto request, CancellationToken cancellationToken = default);
}

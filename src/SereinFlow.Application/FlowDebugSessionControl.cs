using System.Diagnostics.CodeAnalysis;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application;
/// <summary>
/// Application boundary for starting and controlling a debug Worker. The API
/// host owns the concrete Worker supervision implementation, while MCP and
/// other application-facing clients depend only on this port.
/// 启动和控制调试 Worker 的应用边界；API 宿主负责具体的 Worker 监管实现，MCP 等应用客户端仅依赖此端口。
/// </summary>
public interface IFlowDebugSessionService
{
    Task<FlowDebugSessionStartResult> CreateAsync(Guid projectId, Guid flowId, StartFlowDebugSessionRequestDto request, CancellationToken cancellationToken = default);
    Task<FlowDebugSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<FlowDebugStateWaitResult> WaitForChangeAsync(Guid sessionId, long afterRevision, TimeSpan timeout, CancellationToken cancellationToken = default);
    Task<FlowDebugSessionCommandResult> ContinueAsync(Guid sessionId, long commandSequence, CancellationToken cancellationToken = default);
    Task<FlowDebugSessionCommandResult> StepAsync(Guid sessionId, long commandSequence, CancellationToken cancellationToken = default);
    Task<FlowDebugSessionCommandResult> StopAsync(Guid sessionId, long commandSequence, CancellationToken cancellationToken = default);
}

/// <summary>
/// Captures the outcome of flow debug session start operations.
/// 记录流程调试会话启动操作的结果。
/// </summary>
public sealed record FlowDebugSessionStartResult(FlowDebugSession? Session, int StatusCode, string? ErrorTitle = null, object? ErrorBody = null, string? ErrorCode = null)
{
    [MemberNotNullWhen(true, nameof(Session))]
    public bool IsAccepted => Session is not null;
}

/// <summary>
/// Captures the outcome of flow debug session command operations.
/// 记录流程调试会话命令操作的结果。
/// </summary>
public sealed record FlowDebugSessionCommandResult(int StatusCode, string? ErrorTitle = null, string? ErrorCode = null, FlowDebugSession? Session = null)
{
    public bool IsAccepted => StatusCode is >= 200 and < 300;
}

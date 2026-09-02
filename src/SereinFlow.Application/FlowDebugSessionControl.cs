using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application;

/// <summary>
/// Application boundary for starting and controlling a debug Worker. The API
/// host owns the concrete Worker supervision implementation, while MCP and
/// other application-facing clients depend only on this port.
/// </summary>
public interface IFlowDebugSessionService
{
    Task<FlowDebugSessionStartResult> CreateAsync(
        Guid projectId,
        Guid flowId,
        StartFlowDebugSessionRequestDto request,
        CancellationToken cancellationToken = default);

    Task<FlowDebugSession?> FindAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<FlowDebugStateWaitResult> WaitForChangeAsync(
        Guid sessionId,
        long afterRevision,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    Task<FlowDebugSessionCommandResult> ContinueAsync(
        Guid sessionId,
        long commandSequence,
        CancellationToken cancellationToken = default);

    Task<FlowDebugSessionCommandResult> StepAsync(
        Guid sessionId,
        long commandSequence,
        CancellationToken cancellationToken = default);

    Task<FlowDebugSessionCommandResult> StopAsync(
        Guid sessionId,
        long commandSequence,
        CancellationToken cancellationToken = default);
}

public sealed record FlowDebugSessionStartResult(
    FlowDebugSession? Session,
    int StatusCode,
    string? ErrorTitle = null,
    object? ErrorBody = null,
    string? ErrorCode = null)
{
    public bool IsAccepted => Session is not null;
}

public sealed record FlowDebugSessionCommandResult(
    int StatusCode,
    string? ErrorTitle = null,
    string? ErrorCode = null)
{
    public bool IsAccepted => StatusCode is >= 200 and < 300;
}

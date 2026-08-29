using SereinFlow.Application.Persistence;
using SereinFlow.Domain;

namespace SereinFlow.Application;

public sealed record FlowDebugStateWaitResult(
    FlowDebugSession? Session,
    bool HasChanged,
    bool TimedOut);

/// <summary>
/// Bounded polling for durable debug state. The waiter does not own a Worker
/// handle and can therefore be used by HTTP, MCP, or another read-only client.
/// 对持久化调试状态进行有界轮询。等待器不持有 Worker 句柄，因此 HTTP、MCP
/// 或其他只读调用方都可以复用。
/// </summary>
public static class FlowDebugSessionWaiter
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    public static async Task<FlowDebugStateWaitResult> WaitAsync(
        IFlowDebugSessionStore store,
        Guid sessionId,
        long afterRevision,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentOutOfRangeException.ThrowIfLessThan(afterRevision, -1);
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);

        var deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            var session = await store.FindAsync(sessionId, cancellationToken);
            if (session is null)
                return new(null, false, false);

            if (session.StateRevision > afterRevision || session.IsTerminal)
                return new(session, session.StateRevision > afterRevision, false);

            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
                return new(session, false, true);

            await Task.Delay(
                remaining < PollInterval ? remaining : PollInterval,
                cancellationToken);
        }
    }
}

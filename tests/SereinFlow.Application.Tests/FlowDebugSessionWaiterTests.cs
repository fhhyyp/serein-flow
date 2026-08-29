using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Domain;

namespace SereinFlow.Application.Tests;

public sealed class FlowDebugSessionWaiterTests
{
    [Fact]
    public async Task ReturnsImmediatelyWhenRevisionHasChanged()
    {
        var session = CreateSession();
        session.MarkRunning(DateTimeOffset.UtcNow);

        var result = await FlowDebugSessionWaiter.WaitAsync(
            new InMemoryStore(session),
            session.Id,
            afterRevision: 0,
            TimeSpan.Zero);

        Assert.NotNull(result.Session);
        Assert.True(result.HasChanged);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task ReturnsTerminalSessionEvenWhenRevisionIsUnchanged()
    {
        var session = CreateSession();
        session.Complete(FlowRunStatus.Succeeded, null, DateTimeOffset.UtcNow);

        var result = await FlowDebugSessionWaiter.WaitAsync(
            new InMemoryStore(session),
            session.Id,
            session.StateRevision,
            TimeSpan.Zero);

        Assert.NotNull(result.Session);
        Assert.False(result.HasChanged);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task ReportsTimeoutWithoutClaimingAChange()
    {
        var session = CreateSession();

        var result = await FlowDebugSessionWaiter.WaitAsync(
            new InMemoryStore(session),
            session.Id,
            session.StateRevision,
            TimeSpan.Zero);

        Assert.NotNull(result.Session);
        Assert.False(result.HasChanged);
        Assert.True(result.TimedOut);
    }

    private static FlowDebugSession CreateSession()
        => FlowDebugSession.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), [], DateTimeOffset.UtcNow);

    private sealed class InMemoryStore(FlowDebugSession session) : IFlowDebugSessionStore
    {
        public Task<FlowDebugSession> CreateAsync(FlowDebugSession value, CancellationToken cancellationToken = default) => Task.FromResult(value);
        public Task<FlowDebugSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult<FlowDebugSession?>(session.Id == sessionId ? session : null);
        public Task<FlowDebugSession?> FindByRunIdAsync(Guid runId, CancellationToken cancellationToken = default) => Task.FromResult<FlowDebugSession?>(session.RunId == runId ? session : null);
        public Task<IReadOnlyList<FlowDebugSession>> ListActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FlowDebugSession>>(session.IsTerminal ? [] : [session]);
        public Task<bool> SaveAsync(FlowDebugSession value, CancellationToken cancellationToken = default) => Task.FromResult(value.Id == session.Id);
    }
}

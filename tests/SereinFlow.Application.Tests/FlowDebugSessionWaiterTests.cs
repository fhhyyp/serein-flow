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
        var inMemoryStore = new InMemoryStore(session);
        var result = await FlowDebugSessionWaiter.WaitAsync(inMemoryStore, session.Id, afterRevision: 0, TimeSpan.Zero);
        Assert.NotNull(result.Session);
        Assert.True(result.HasChanged);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task ReturnsTerminalSessionEvenWhenRevisionIsUnchanged()
    {
        var session = CreateSession();
        session.Complete(FlowRunStatus.Succeeded, null, DateTimeOffset.UtcNow);
        var inMemoryStore1 = new InMemoryStore(session);
        var result = await FlowDebugSessionWaiter.WaitAsync(inMemoryStore1, session.Id, session.StateRevision, TimeSpan.Zero);
        Assert.NotNull(result.Session);
        Assert.False(result.HasChanged);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task ReportsTimeoutWithoutClaimingAChange()
    {
        var session = CreateSession();
        var inMemoryStore2 = new InMemoryStore(session);
        var result = await FlowDebugSessionWaiter.WaitAsync(inMemoryStore2, session.Id, session.StateRevision, TimeSpan.Zero);
        Assert.NotNull(result.Session);
        Assert.False(result.HasChanged);
        Assert.True(result.TimedOut);
    }

    private static FlowDebugSession CreateSession()
    {
        var newGuidResult = Guid.NewGuid();
        var newGuidResult3 = Guid.NewGuid();
        var newGuidResult4 = Guid.NewGuid();
        var createResult = FlowDebugSession.Create(newGuidResult, newGuidResult3, newGuidResult4, [], DateTimeOffset.UtcNow);
        return createResult;
    }

    private sealed class InMemoryStore(FlowDebugSession session) : IFlowDebugSessionStore
    {
        public Task<FlowDebugSession> CreateAsync(FlowDebugSession value, CancellationToken cancellationToken = default)
        {
            var fromResultResult = Task.FromResult(value);
            return fromResultResult;
        }

        public Task<FlowDebugSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            var fromResultResult5 = Task.FromResult<FlowDebugSession?>(session.Id == sessionId ? session : null);
            return fromResultResult5;
        }

        public Task<FlowDebugSession?> FindByRunIdAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var fromResultResult6 = Task.FromResult<FlowDebugSession?>(session.RunId == runId ? session : null);
            return fromResultResult6;
        }

        public Task<IReadOnlyList<FlowDebugSession>> ListActiveAsync(CancellationToken cancellationToken = default)
        {
            var fromResultResult7 = Task.FromResult<IReadOnlyList<FlowDebugSession>>(session.IsTerminal ? [] : [session]);
            return fromResultResult7;
        }

        public Task<bool> SaveAsync(FlowDebugSession value, CancellationToken cancellationToken = default)
        {
            var fromResultResult8 = Task.FromResult(value.Id == session.Id);
            return fromResultResult8;
        }
    }
}

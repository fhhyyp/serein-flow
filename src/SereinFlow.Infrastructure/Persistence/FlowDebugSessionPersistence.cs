using System.Globalization;
using System.Text.Json;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Infrastructure.Persistence;

public sealed class SqlSugarFlowDebugSessionStore : IFlowDebugSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateWebOptions();

    private readonly IRepository<FlowDebugSessionRecord> _sessions;

    public SqlSugarFlowDebugSessionStore(IRepository<FlowDebugSessionRecord> sessions)
    {
        _sessions = sessions;
    }

    public SqlSugarFlowDebugSessionStore(SqliteDatabase database)
        : this(new SqlSugarRepository<FlowDebugSessionRecord>(database.Client))
    {
    }

    public async Task<FlowDebugSession> CreateAsync(
        FlowDebugSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        await _sessions.AddAsync(Map(session), cancellationToken);
        return session;
    }

    public async Task<FlowDebugSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var row = await _sessions.GetByIdAsync(sessionId.ToString("D"), cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<FlowDebugSession?> FindByRunIdAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var rows = await _sessions.ListAsync(
            row => row.RunId == runId.ToString("D"),
            cancellationToken);
        return rows.Count == 0 ? null : Map(rows.Single());
    }

    public async Task<IReadOnlyList<FlowDebugSession>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        var terminal = new[]
        {
            FlowDebugSessionStatus.Completed.ToString(),
            FlowDebugSessionStatus.Cancelled.ToString(),
            FlowDebugSessionStatus.Failed.ToString()
        }.ToHashSet(StringComparer.Ordinal);
        var rows = await _sessions.ListAsync(cancellationToken: cancellationToken);
        return rows
            .Where(row => !terminal.Contains(row.Status))
            .Select(Map)
            .OrderBy(static item => item.CreatedAt)
            .ToArray();
    }

    public Task<bool> SaveAsync(FlowDebugSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        return _sessions.UpdateAsync(Map(session), cancellationToken);
    }

    private static FlowDebugSessionRecord Map(FlowDebugSession session)
        => new()
        {
            Id = session.Id.ToString("D"),
            RunId = session.RunId.ToString("D"),
            ProjectId = session.ProjectId.ToString("D"),
            FlowId = session.FlowId.ToString("D"),
            Status = session.Status.ToString(),
            BreakpointsJson = JsonSerializer.Serialize(session.BreakpointNodeIds, JsonOptions),
            CurrentNodeId = session.CurrentNodeId,
            ActiveInvocationId = session.ActiveInvocationId?.ToString("D"),
            ActiveFlipflopNodeId = session.ActiveFlipflopNodeId,
            QueuedTriggerCount = session.QueuedTriggerCount,
            LastCommandSequence = session.LastCommandSequence,
            FailureMessage = session.FailureMessage,
            CreatedAt = session.CreatedAt.ToString("O"),
            UpdatedAt = session.UpdatedAt.ToString("O")
        };

    private static FlowDebugSession Map(FlowDebugSessionRecord row)
    {
        var breakpoints = string.IsNullOrWhiteSpace(row.BreakpointsJson)
            ? []
            : JsonSerializer.Deserialize<string[]>(row.BreakpointsJson, JsonOptions) ?? [];
        return FlowDebugSession.Rehydrate(
            Guid.Parse(row.Id),
            Guid.Parse(row.RunId),
            Guid.Parse(row.ProjectId),
            Guid.Parse(row.FlowId),
            breakpoints,
            Enum.TryParse<FlowDebugSessionStatus>(row.Status, out var status)
                ? status
                : FlowDebugSessionStatus.Failed,
            row.CurrentNodeId,
            Guid.TryParse(row.ActiveInvocationId, out var activeInvocationId) ? activeInvocationId : null,
            row.ActiveFlipflopNodeId,
            row.QueuedTriggerCount,
            row.LastCommandSequence,
            row.FailureMessage,
            DateTimeOffset.Parse(row.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            DateTimeOffset.Parse(row.UpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }
}

using System;
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
    {
        var sessions = new SqlSugarRepository<FlowDebugSessionRecord>(database.Client);
        _sessions = sessions;
    }

    public async Task<FlowDebugSession> CreateAsync(FlowDebugSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var entity = Map(session);
        await _sessions.AddAsync(entity, cancellationToken);
        return session;
    }

    public async Task<FlowDebugSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var id = sessionId.ToString("D");
        var row = await _sessions.GetByIdAsync(id, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<FlowDebugSession?> FindByRunIdAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var runKey = runId.ToString("D");
        var rows = await _sessions.ListAsync(row => row.RunId == runKey, cancellationToken);
        return rows.Count == 0 ? null : Map(rows.Single());
    }

    public async Task<IReadOnlyList<FlowDebugSession>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        var terminal = (new[]
        {
            FlowDebugSessionStatus.Completed.ToString(),
            FlowDebugSessionStatus.Cancelled.ToString(),
            FlowDebugSessionStatus.Failed.ToString()
        }).ToHashSet(StringComparer.Ordinal);
        return (await _sessions.ListAsync(cancellationToken: cancellationToken))
            .Where(row =>
        {
            return !terminal.Contains(row.Status);
        })
            .Select(Map)
            .OrderBy(static item => item.CreatedAt)
            .ToArray();
    }

    public Task<bool> SaveAsync(FlowDebugSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var entity = Map(session);
        return _sessions.UpdateAsync(entity, cancellationToken);
    }

    private static FlowDebugSessionRecord Map(FlowDebugSession session)
    {
        return new()
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
            StateRevision = session.StateRevision,
            PauseStateJson = session.PauseState is null ? null : JsonSerializer.Serialize(session.PauseState, JsonOptions),
            LastNodeResultJson = session.LastNodeResult is null ? null : JsonSerializer.Serialize(session.LastNodeResult, JsonOptions),
            FailureMessage = session.FailureMessage,
            CreatedAt = session.CreatedAt.ToString("O"),
            UpdatedAt = session.UpdatedAt.ToString("O")
        };
    }

    private static FlowDebugSession Map(FlowDebugSessionRecord row)
    {
        var breakpoints = string.IsNullOrWhiteSpace(row.BreakpointsJson) ? [] : JsonSerializer.Deserialize<string[]>(row.BreakpointsJson, JsonOptions) ?? [];
        var pauseState = DeserializeOrNull<FlowDebugPauseState>(row.PauseStateJson);
        var lastNodeResult = DeserializeOrNull<FlowDebugNodeResult>(row.LastNodeResultJson);
        var id = Guid.Parse(row.Id);
        var runId = Guid.Parse(row.RunId);
        var projectId = Guid.Parse(row.ProjectId);
        var flowId = Guid.Parse(row.FlowId);
        FlowDebugSessionStatus failed = Enum.TryParse<FlowDebugSessionStatus>(row.Status, out var status) ? status : FlowDebugSessionStatus.Failed;
        Guid? mappedActiveInvocationId = Guid.TryParse(row.ActiveInvocationId, out var activeInvocationId) ? activeInvocationId : null;
        var createdAt = DateTimeOffset.Parse(row.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var updatedAt = DateTimeOffset.Parse(row.UpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return FlowDebugSession.Rehydrate(id, runId, projectId, flowId, breakpoints, failed, row.CurrentNodeId, mappedActiveInvocationId, row.ActiveFlipflopNodeId, row.QueuedTriggerCount, row.LastCommandSequence, row.FailureMessage, createdAt, updatedAt, row.StateRevision, pauseState, lastNodeResult);
    }

    private static T? DeserializeOrNull<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}

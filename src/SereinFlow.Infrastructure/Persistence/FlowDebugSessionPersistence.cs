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
        var mapResult = Map(session);
        await _sessions.AddAsync(mapResult, cancellationToken);
        return session;
    }

    public async Task<FlowDebugSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var toStringResult = sessionId.ToString("D");
        var row = await _sessions.GetByIdAsync(toStringResult, cancellationToken);
        FlowDebugSession? evaluatedValue = row is null ? null : Map(row);
        return evaluatedValue;
    }

    public async Task<FlowDebugSession?> FindByRunIdAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var runKey = runId.ToString("D");
        var rows = await _sessions.ListAsync(row => row.RunId == runKey, cancellationToken);
        FlowDebugSession? evaluatedValue2 = rows.Count == 0 ? null : resolveMapResult();
        return evaluatedValue2;
        FlowDebugSession resolveMapResult()
        {
            var singleResult = rows.Single();
            var mapResult1 = Map(singleResult);
            return mapResult1;
        }
    }

    public async Task<IReadOnlyList<FlowDebugSession>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        var evaluatedValue4 = new[]
        {
            FlowDebugSessionStatus.Completed.ToString(),
            FlowDebugSessionStatus.Cancelled.ToString(),
            FlowDebugSessionStatus.Failed.ToString()
        };
        var terminal = evaluatedValue4.ToHashSet(StringComparer.Ordinal);
        var rows = await _sessions.ListAsync(cancellationToken: cancellationToken);
        var whereResult = rows.Where(row =>
        {
            var evaluatedValue3 = !terminal.Contains(row.Status);
            return evaluatedValue3;
        });
        var selectResult = whereResult.Select(Map);
        var orderByResult = selectResult.OrderBy(static item => item.CreatedAt);
        var toArrayResult = orderByResult.ToArray();
        return toArrayResult;
    }

    public Task<bool> SaveAsync(FlowDebugSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var mapResult5 = Map(session);
        var updateAsyncResult = _sessions.UpdateAsync(mapResult5, cancellationToken);
        return updateAsyncResult;
    }

    private static FlowDebugSessionRecord Map(FlowDebugSession session)
    {
        FlowDebugSessionRecord evaluatedValue6 = new()
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
        return evaluatedValue6;
    }

    private static FlowDebugSession Map(FlowDebugSessionRecord row)
    {
        var breakpoints = string.IsNullOrWhiteSpace(row.BreakpointsJson) ? [] : JsonSerializer.Deserialize<string[]>(row.BreakpointsJson, JsonOptions) ?? [];
        var pauseState = DeserializeOrNull<FlowDebugPauseState>(row.PauseStateJson);
        var lastNodeResult = DeserializeOrNull<FlowDebugNodeResult>(row.LastNodeResultJson);
        var parseResult = Guid.Parse(row.Id);
        var parseResult7 = Guid.Parse(row.RunId);
        var parseResult8 = Guid.Parse(row.ProjectId);
        var parseResult9 = Guid.Parse(row.FlowId);
        FlowDebugSessionStatus evaluatedValue10 = Enum.TryParse<FlowDebugSessionStatus>(row.Status, out var status) ? status : FlowDebugSessionStatus.Failed;
        Guid? evaluatedValue11 = Guid.TryParse(row.ActiveInvocationId, out var activeInvocationId) ? activeInvocationId : null;
        var parseResult12 = DateTimeOffset.Parse(row.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var parseResult13 = DateTimeOffset.Parse(row.UpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var rehydrateResult = FlowDebugSession.Rehydrate(parseResult, parseResult7, parseResult8, parseResult9, breakpoints, evaluatedValue10, row.CurrentNodeId, evaluatedValue11, row.ActiveFlipflopNodeId, row.QueuedTriggerCount, row.LastCommandSequence, row.FailureMessage, parseResult12, parseResult13, row.StateRevision, pauseState, lastNodeResult);
        return rehydrateResult;
    }

    private static T? DeserializeOrNull<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            var deserializeResult = JsonSerializer.Deserialize<T>(json, JsonOptions);
            return deserializeResult;
        }
        catch (JsonException)
        {
            return default;
        }
    }
}

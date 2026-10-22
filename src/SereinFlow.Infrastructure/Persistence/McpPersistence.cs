using System;
using System.Text.Json;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SqlSugar;

namespace SereinFlow.Infrastructure.Persistence;
public sealed class SqlSugarMcpPreviewStore : IMcpPreviewStore
{
    private readonly IRepository<McpPreviewRecord> _records;
    public SqlSugarMcpPreviewStore(IRepository<McpPreviewRecord> records)
    {
        _records = records;
    }

    public async Task<McpPreviewEntry?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var toStringResult = id.ToString("D");
        var record = await _records.GetByIdAsync(toStringResult, cancellationToken);
        McpPreviewEntry? evaluatedValue11 = record is null ? null : Map(record);
        return evaluatedValue11;
    }

    public Task AddAsync(McpPreviewEntry entry, CancellationToken cancellationToken = default)
    {
        var mapResult12 = Map(entry);
        var addAsyncResult13 = _records.AddAsync(mapResult12, cancellationToken);
        return addAsyncResult13;
    }

    public Task<bool> UpdateAsync(McpPreviewEntry entry, CancellationToken cancellationToken = default)
    {
        var mapResult14 = Map(entry);
        var updateAsyncResult15 = _records.UpdateAsync(mapResult14, cancellationToken);
        return updateAsyncResult15;
    }

    private static McpPreviewEntry Map(McpPreviewRecord record)
    {
        var parseResult = Guid.Parse(record.Id);
        Guid? evaluatedValue16 = Guid.TryParse(record.ProjectId, out var projectId) ? projectId : null;
        Guid? evaluatedValue17 = Guid.TryParse(record.FlowId, out var flowId) ? flowId : null;
        McpMutationPreviewStatusDto evaluatedValue18 = Enum.TryParse<McpMutationPreviewStatusDto>(record.Status, true, out var status) ? status : McpMutationPreviewStatusDto.Pending;
        DateTimeOffset evaluatedValue19 = DateTimeOffset.TryParse(record.CreatedAt, out var createdAt) ? createdAt : DateTimeOffset.UtcNow;
        DateTimeOffset evaluatedValue20 = DateTimeOffset.TryParse(record.ExpiresAt, out var expiresAt) ? expiresAt : DateTimeOffset.UtcNow;
        DateTimeOffset? evaluatedValue21 = DateTimeOffset.TryParse(record.AppliedAt, out var appliedAt) ? appliedAt : null;
        McpPreviewEntry evaluatedValue22 = new(parseResult, record.Operation, record.PrincipalId, evaluatedValue16, evaluatedValue17, record.ExpectedVersion, record.PayloadJson, record.PreviewFingerprint, evaluatedValue18, evaluatedValue19, evaluatedValue20, evaluatedValue21);
        return evaluatedValue22;
    }

    private static McpPreviewRecord Map(McpPreviewEntry entry)
    {
        McpPreviewRecord evaluatedValue23 = new()
        {
            Id = entry.Id.ToString("D"),
            Operation = entry.Operation,
            PrincipalId = entry.PrincipalId,
            ProjectId = entry.ProjectId?.ToString("D"),
            FlowId = entry.FlowId?.ToString("D"),
            ExpectedVersion = entry.ExpectedVersion,
            PayloadJson = entry.PayloadJson,
            PreviewFingerprint = entry.PreviewFingerprint,
            Status = entry.Status.ToString(),
            CreatedAt = entry.CreatedAt.ToString("O"),
            ExpiresAt = entry.ExpiresAt.ToString("O"),
            AppliedAt = entry.AppliedAt?.ToString("O")
        };
        return evaluatedValue23;
    }
}

public sealed class SqlSugarMcpAuditStore : IMcpAuditStore
{
    private readonly IRepository<McpAuditRecord> _records;
    public SqlSugarMcpAuditStore(IRepository<McpAuditRecord> records)
    {
        _records = records;
    }

    public Task AddAsync(McpAuditEntry entry, CancellationToken cancellationToken = default)
    {
        var mcpAuditRecord = new McpAuditRecord
        {
            Id = entry.Id.ToString("D"),
            PrincipalId = entry.PrincipalId,
            Operation = entry.Operation,
            ProjectId = entry.ProjectId?.ToString("D"),
            FlowId = entry.FlowId?.ToString("D"),
            Track = entry.Track?.ToString(),
            FlowVersion = entry.FlowVersion,
            PreviewId = entry.PreviewId?.ToString("D"),
            Outcome = entry.Outcome,
            RequestHash = entry.RequestHash,
            Summary = entry.Summary,
            CreatedAt = entry.CreatedAt.ToString("O"),
            DurationMilliseconds = entry.DurationMilliseconds,
            InputBytes = entry.InputBytes,
            OutputBytes = entry.OutputBytes,
        };
        var addAsyncResult24 = _records.AddAsync(mcpAuditRecord, cancellationToken);
        return addAsyncResult24;
    }
}

public sealed class SqlSugarMcpIdempotencyStore : IMcpIdempotencyStore
{
    private readonly IRepository<McpIdempotencyRecord> _records;
    public SqlSugarMcpIdempotencyStore(IRepository<McpIdempotencyRecord> records)
    {
        _records = records;
    }

    public async Task<McpIdempotencyEntry?> FindAsync(string principalId, string operation, string keyHash, CancellationToken cancellationToken = default)
    {
        var firstOrDefaultResult = (await _records.ListAsync(item => item.PrincipalId == principalId && item.Operation == operation && item.KeyHash == keyHash, cancellationToken)).OrderByDescending(item => item.CreatedAt, StringComparer.Ordinal).Select(Map).FirstOrDefault();
        return firstOrDefaultResult;
    }

    public Task AddAsync(McpIdempotencyEntry entry, CancellationToken cancellationToken = default)
    {
        var mcpIdempotencyRecord = new McpIdempotencyRecord
        {
            Id = entry.Id.ToString("D"),
            PrincipalId = entry.PrincipalId,
            Operation = entry.Operation,
            KeyHash = entry.KeyHash,
            RequestHash = entry.RequestHash,
            ResponseJson = entry.ResponseJson,
            CreatedAt = entry.CreatedAt.ToString("O"),
        };
        var addAsyncResult27 = _records.AddAsync(mcpIdempotencyRecord, cancellationToken);
        return addAsyncResult27;
    }

    private static McpIdempotencyEntry Map(McpIdempotencyRecord record)
    {
        var parseResult28 = Guid.Parse(record.Id);
        DateTimeOffset evaluatedValue29 = DateTimeOffset.TryParse(record.CreatedAt, out var createdAt) ? createdAt : DateTimeOffset.UtcNow;
        McpIdempotencyEntry evaluatedValue30 = new(parseResult28, record.PrincipalId, record.Operation, record.KeyHash, record.RequestHash, record.ResponseJson, evaluatedValue29);
        return evaluatedValue30;
    }
}

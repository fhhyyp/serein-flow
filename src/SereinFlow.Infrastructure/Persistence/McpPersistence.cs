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
        var idText = id.ToString("D");
        var record = await _records.GetByIdAsync(idText, cancellationToken);
        return record is null ? null : Map(record);
    }

    public Task AddAsync(McpPreviewEntry entry, CancellationToken cancellationToken = default)
    {
        var entity = Map(entry);
        return _records.AddAsync(entity, cancellationToken);
    }

    public Task<bool> UpdateAsync(McpPreviewEntry entry, CancellationToken cancellationToken = default)
    {
        var entity = Map(entry);
        return _records.UpdateAsync(entity, cancellationToken);
    }

    private static McpPreviewEntry Map(McpPreviewRecord record)
    {
        var id = Guid.Parse(record.Id);
        Guid? mappedProjectId = Guid.TryParse(record.ProjectId, out var projectId) ? projectId : null;
        Guid? mappedFlowId = Guid.TryParse(record.FlowId, out var flowId) ? flowId : null;
        McpMutationPreviewStatusDto pending = Enum.TryParse<McpMutationPreviewStatusDto>(record.Status, true, out var status) ? status : McpMutationPreviewStatusDto.Pending;
        DateTimeOffset utcNow = DateTimeOffset.TryParse(record.CreatedAt, out var createdAt) ? createdAt : DateTimeOffset.UtcNow;
        DateTimeOffset dateTimeOffset = DateTimeOffset.TryParse(record.ExpiresAt, out var expiresAt) ? expiresAt : DateTimeOffset.UtcNow;
        DateTimeOffset? mappedAppliedAt = DateTimeOffset.TryParse(record.AppliedAt, out var appliedAt) ? appliedAt : null;
        return new(id, record.Operation, record.PrincipalId, mappedProjectId, mappedFlowId, record.ExpectedVersion, record.PayloadJson, record.PreviewFingerprint, pending, utcNow, dateTimeOffset, mappedAppliedAt);
    }

    private static McpPreviewRecord Map(McpPreviewEntry entry)
    {
        return new()
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
        return _records.AddAsync(mcpAuditRecord, cancellationToken);
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
        return (await _records.ListAsync(item => item.PrincipalId == principalId && item.Operation == operation && item.KeyHash == keyHash, cancellationToken))
            .OrderByDescending(item => item.CreatedAt, StringComparer.Ordinal)
            .Select(Map)
            .FirstOrDefault();
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
        return _records.AddAsync(mcpIdempotencyRecord, cancellationToken);
    }

    private static McpIdempotencyEntry Map(McpIdempotencyRecord record)
    {
        var id = Guid.Parse(record.Id);
        DateTimeOffset utcNow = DateTimeOffset.TryParse(record.CreatedAt, out var createdAt) ? createdAt : DateTimeOffset.UtcNow;
        return new(id, record.PrincipalId, record.Operation, record.KeyHash, record.RequestHash, record.ResponseJson, utcNow);
    }
}

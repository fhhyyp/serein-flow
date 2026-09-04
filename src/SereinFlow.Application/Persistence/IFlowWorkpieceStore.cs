namespace SereinFlow.Application.Persistence;

/// <summary>
/// Read-only API-side access to workpieces created by a Worker run.
/// API 侧读取 Worker 运行所创建工件的只读接口。
/// </summary>
public interface IFlowWorkpieceStore
{
    Task<IReadOnlyList<FlowWorkpieceRecord>> ListAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<FlowWorkpieceRecord?> FindAsync(Guid runId, string workpieceId, CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(Guid runId, string workpieceId, CancellationToken cancellationToken = default);
}

public sealed record FlowWorkpieceRecord(
    Guid RunId,
    string Id,
    string Kind,
    string Name,
    string ContentType,
    long Length,
    DateTimeOffset CreatedAt);

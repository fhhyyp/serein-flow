using SqlSugar;

namespace SereinFlow.Infrastructure.Persistence;

public interface IFlowRunEventQueries
{
    Task<long> GetLastSequenceAsync(Guid runId, CancellationToken cancellationToken = default);
}

internal sealed class SqlSugarFlowRunEventQueries(ISqlSugarClient database) : IFlowRunEventQueries
{
    public async Task<long> GetLastSequenceAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = runId.ToString("D");
        return (await database
            .Queryable<FlowRunEventRecord>()
            .Where(row => row.RunId == key)
            .Select<long?>("MAX(Sequence)")
            .ToListAsync(cancellationToken)).FirstOrDefault() ?? 0;
    }
}

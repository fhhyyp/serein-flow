using System.Globalization;
using SereinFlow.Application.Persistence;
using SereinFlow.Domain;

namespace SereinFlow.Infrastructure.Persistence;
public sealed class SqlSugarFlowRunEventStore : IFlowRunEventStore
{
    private readonly IRepository<FlowRunEventRecord> _events;
    private readonly IUnitOfWork _unitOfWork;
    public SqlSugarFlowRunEventStore(IRepository<FlowRunEventRecord> events, IUnitOfWork unitOfWork)
    {
        _events = events;
        _unitOfWork = unitOfWork;
    }

    public SqlSugarFlowRunEventStore(SqliteDatabase database)
    {
        var events = new SqlSugarRepository<FlowRunEventRecord>(database.Client);
        var unitOfWork = new SqlSugarUnitOfWork(database.Client);
        _events = events;
        _unitOfWork = unitOfWork;
    }

    public async Task AppendAsync(IReadOnlyList<FlowRunEvent> events, CancellationToken cancellationToken = default)
    {
        if (events is null)
        {
            throw new ArgumentNullException(nameof(events), "Flow run events cannot be null. 流程运行事件集合不能为空。");
        }

        if (events.Count == 0)
        {
            return;
        }

        try
        {
            var orderByResult = events.OrderBy(static item => item.Sequence);
            var groupByResult = orderByResult.GroupBy(static item => (item.RunId, item.Sequence));
            var selectResult = groupByResult.Select(static group =>
            {
                var firstResult = group.First();
                return firstResult;
            });
            var grouped = selectResult.ToArray();
            var existingKeys = new HashSet<(string RunId, long Sequence)>();
            foreach (var runGroup in grouped.GroupBy(static item => item.RunId))
            {
                var minimum = runGroup.Min(static item => item.Sequence);
                var maximum = runGroup.Max(static item => item.Sequence);
                var runKey = runGroup.Key.ToString("D");
                var existing = await _events.ListAsync(row => row.RunId == runKey && row.Sequence >= minimum && row.Sequence <= maximum, cancellationToken);
                foreach (var row in existing)
                {
                    existingKeys.Add((row.RunId, row.Sequence));
                }
            }

            await _unitOfWork.ExecuteAsync(async token =>
            {
                foreach (var item in grouped)
                {
                    var evaluatedValue = (item.RunId.ToString("D"), item.Sequence);
                    if (existingKeys.Contains(evaluatedValue))
                    {
                        continue;
                    }

                    var flowRunEventRecord = new FlowRunEventRecord
                    {
                        RunId = item.RunId.ToString("D"),
                        Sequence = item.Sequence,
                        Type = item.Type,
                        NodeId = item.NodeId,
                        PayloadJson = item.PayloadJson,
                        Timestamp = item.Timestamp.ToString("O")
                    };
                    await _events.AddAsync(flowRunEventRecord, token);
                }

                return true;
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Flow run event append failed; the batch was rolled back. 流程运行事件追加失败，批处理已回滚。", exception);
        }
    }

    public async Task<IReadOnlyList<FlowRunEvent>> GetAfterAsync(Guid runId, long sequenceExclusive, CancellationToken cancellationToken = default)
    {
        var key = runId.ToString("D");
        var rows = await _events.ListAsync(row => row.RunId == key && row.Sequence > sequenceExclusive, cancellationToken);
        var orderByResult1 = rows.OrderBy(static row => row.Sequence);
        var selectResult2 = orderByResult1.Select(Map);
        var toArrayResult = selectResult2.ToArray();
        return toArrayResult;
    }

    public async Task<long> GetLastSequenceAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var key = runId.ToString("D");
        var rows = await _events.ListAsync(row => row.RunId == key, cancellationToken);
        long evaluatedValue3 = rows.Count == 0 ? 0 : rows.Max(static row => row.Sequence);
        return evaluatedValue3;
    }

    public void Append(IReadOnlyList<FlowRunEvent> events)
    {
        if (events is null)
        {
            throw new ArgumentNullException(nameof(events), "Flow run events cannot be null. 流程运行事件集合不能为空。");
        }

        if (events.Count == 0)
        {
            return;
        }

        try
        {
            var executeAsyncResult = _unitOfWork.ExecuteAsync(async cancellationToken =>
            {
                foreach (var item in events.OrderBy(static item => item.Sequence))
                {
                    var flowRunEventRecord4 = new FlowRunEventRecord
                    {
                        RunId = item.RunId.ToString("D"),
                        Sequence = item.Sequence,
                        Type = item.Type,
                        NodeId = item.NodeId,
                        PayloadJson = item.PayloadJson,
                        Timestamp = item.Timestamp.ToString("O")
                    };
                    await _events.AddAsync(flowRunEventRecord4, cancellationToken);
                }

                return true;
            });
            var getAwaiterResult = executeAsyncResult.GetAwaiter();
            getAwaiterResult.GetResult();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Flow run event append failed; the batch was rolled back. 流程运行事件追加失败，批处理已回滚。", exception);
        }
    }

    public IReadOnlyList<FlowRunEvent> GetAfter(Guid runId, long sequenceExclusive)
    {
        var listAsyncResult = _events.ListAsync();
        var getAwaiterResult6 = listAsyncResult.GetAwaiter();
        var getResultResult = getAwaiterResult6.GetResult();
        var whereResult = getResultResult.Where(row =>
        {
            var evaluatedValue5 = row.RunId == runId.ToString("D") && row.Sequence > sequenceExclusive;
            return evaluatedValue5;
        });
        var orderByResult7 = whereResult.OrderBy(static row => row.Sequence);
        var selectResult8 = orderByResult7.Select(Map);
        var toArrayResult9 = selectResult8.ToArray();
        return toArrayResult9;
    }

    private static FlowRunEvent Map(FlowRunEventRecord row)
    {
        var parseResult = Guid.Parse(row.RunId);
        var parseResult10 = DateTimeOffset.Parse(row.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        FlowRunEvent evaluatedValue11 = new(parseResult, row.Sequence, parseResult10, row.Type, row.NodeId, row.PayloadJson);
        return evaluatedValue11;
    }
}

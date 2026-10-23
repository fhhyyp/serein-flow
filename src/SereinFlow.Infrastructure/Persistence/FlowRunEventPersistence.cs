using System.Globalization;
using SereinFlow.Application.Persistence;
using SereinFlow.Domain;

namespace SereinFlow.Infrastructure.Persistence;

public sealed class SqlSugarFlowRunEventStore : IFlowRunEventStore
{
    private readonly IRepository<FlowRunEventRecord> _events;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFlowRunEventQueries _queries;
    public SqlSugarFlowRunEventStore(IRepository<FlowRunEventRecord> events, IUnitOfWork unitOfWork, IFlowRunEventQueries queries)
    {
        _events = events;
        _unitOfWork = unitOfWork;
        _queries = queries;
    }

    public SqlSugarFlowRunEventStore(SqliteDatabase database)
    {
        var events = new SqlSugarRepository<FlowRunEventRecord>(database.Client);
        var unitOfWork = new SqlSugarUnitOfWork(database.Client);
        _events = events;
        _unitOfWork = unitOfWork;
        _queries = new SqlSugarFlowRunEventQueries(database.Client);
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
            var grouped = events
                .OrderBy(static item => item.Sequence)
                .GroupBy(static item => (item.RunId, item.Sequence))
                .Select(static group =>
                {
                    return group.First();
                })
                .ToArray();
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
                    var eventKey = (item.RunId.ToString("D"), item.Sequence);
                    if (existingKeys.Contains(eventKey))
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
        return (await _events.ListAsync(row => row.RunId == key && row.Sequence > sequenceExclusive, cancellationToken))
            .OrderBy(static row => row.Sequence)
            .Select(Map)
            .ToArray();
    }

    public Task<long> GetLastSequenceAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        return _queries.GetLastSequenceAsync(runId, cancellationToken);
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
            var task = _unitOfWork.ExecuteAsync(async cancellationToken =>
            {
                foreach (var item in events.OrderBy(static item => item.Sequence))
                {
                    var entity = new FlowRunEventRecord
                    {
                        RunId = item.RunId.ToString("D"),
                        Sequence = item.Sequence,
                        Type = item.Type,
                        NodeId = item.NodeId,
                        PayloadJson = item.PayloadJson,
                        Timestamp = item.Timestamp.ToString("O")
                    };
                    await _events.AddAsync(entity, cancellationToken);
                }

                return true;
            });
            task.GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Flow run event append failed; the batch was rolled back. 流程运行事件追加失败，批处理已回滚。", exception);
        }
    }

    public IReadOnlyList<FlowRunEvent> GetAfter(Guid runId, long sequenceExclusive)
    {
        return _events
            .ListAsync()
            .GetAwaiter()
            .GetResult()
            .Where(row =>
            {
                return row.RunId == runId.ToString("D") && row.Sequence > sequenceExclusive;
            })
            .OrderBy(static row => row.Sequence)
            .Select(Map)
            .ToArray();
    }

    private static FlowRunEvent Map(FlowRunEventRecord row)
    {
        var runId = Guid.Parse(row.RunId);
        var timestamp = DateTimeOffset.Parse(row.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return new(runId, row.Sequence, timestamp, row.Type, row.NodeId, row.PayloadJson);
    }
}

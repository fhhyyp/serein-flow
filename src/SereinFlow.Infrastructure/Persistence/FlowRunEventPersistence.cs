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
        : this(new SqlSugarRepository<FlowRunEventRecord>(database.Client), new SqlSugarUnitOfWork(database.Client))
    {
    }

    public async Task AppendAsync(IReadOnlyList<FlowRunEvent> events, CancellationToken cancellationToken = default)
    {
        if (events is null)
            throw new ArgumentNullException(nameof(events), "Flow run events cannot be null. 流程运行事件集合不能为空。");
        if (events.Count == 0)
            return;

        try
        {
            var grouped = events
                .OrderBy(static item => item.Sequence)
                .GroupBy(static item => (item.RunId, item.Sequence))
                .Select(static group => group.First())
                .ToArray();
            var existingKeys = new HashSet<(string RunId, long Sequence)>();
            foreach (var runGroup in grouped.GroupBy(static item => item.RunId))
            {
                var minimum = runGroup.Min(static item => item.Sequence);
                var maximum = runGroup.Max(static item => item.Sequence);
                var existing = await _events.ListAsync(
                    row => row.RunId == runGroup.Key.ToString("D")
                        && row.Sequence >= minimum
                        && row.Sequence <= maximum,
                    cancellationToken);
                foreach (var row in existing)
                    existingKeys.Add((row.RunId, row.Sequence));
            }

            await _unitOfWork.ExecuteAsync(async token =>
            {
                foreach (var item in grouped)
                {
                    if (existingKeys.Contains((item.RunId.ToString("D"), item.Sequence)))
                        continue;
                    await _events.AddAsync(new FlowRunEventRecord
                    {
                        RunId = item.RunId.ToString("D"),
                        Sequence = item.Sequence,
                        Type = item.Type,
                        NodeId = item.NodeId,
                        PayloadJson = item.PayloadJson,
                        Timestamp = item.Timestamp.ToString("O")
                    }, token);
                }
                return true;
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Flow run event append failed; the batch was rolled back. 流程运行事件追加失败，批处理已回滚。",
                exception);
        }
    }

    public async Task<IReadOnlyList<FlowRunEvent>> GetAfterAsync(Guid runId, long sequenceExclusive, CancellationToken cancellationToken = default)
    {
        var key = runId.ToString("D");
        var rows = await _events.ListAsync(row => row.RunId == key && row.Sequence > sequenceExclusive, cancellationToken);
        return rows
            .OrderBy(static row => row.Sequence)
            .Select(Map)
            .ToArray();
    }

    public async Task<long> GetLastSequenceAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var key = runId.ToString("D");
        var rows = await _events.ListAsync(row => row.RunId == key, cancellationToken);
        return rows.Count == 0 ? 0 : rows.Max(static row => row.Sequence);
    }

    public void Append(IReadOnlyList<FlowRunEvent> events)
    {
        if (events is null)
            throw new ArgumentNullException(nameof(events), "Flow run events cannot be null. 流程运行事件集合不能为空。");
        if (events.Count == 0)
        {
            return;
        }

        try
        {
            _unitOfWork.ExecuteAsync(async cancellationToken =>
            {
                foreach (var item in events.OrderBy(static item => item.Sequence))
                {
                    await _events.AddAsync(new FlowRunEventRecord
                    {
                        RunId = item.RunId.ToString("D"),
                        Sequence = item.Sequence,
                        Type = item.Type,
                        NodeId = item.NodeId,
                        PayloadJson = item.PayloadJson,
                        Timestamp = item.Timestamp.ToString("O")
                    }, cancellationToken);
                }
                return true;
            }).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Flow run event append failed; the batch was rolled back. 流程运行事件追加失败，批处理已回滚。",
                exception);
        }
    }

    public IReadOnlyList<FlowRunEvent> GetAfter(Guid runId, long sequenceExclusive)
    {
        return _events.ListAsync()
            .GetAwaiter().GetResult()
            .Where(row => row.RunId == runId.ToString("D") && row.Sequence > sequenceExclusive)
            .OrderBy(static row => row.Sequence)
            .Select(Map)
            .ToArray();
    }

    private static FlowRunEvent Map(FlowRunEventRecord row)
        => new(
            Guid.Parse(row.RunId),
            row.Sequence,
            DateTimeOffset.Parse(row.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            row.Type,
            row.NodeId,
            row.PayloadJson);

}

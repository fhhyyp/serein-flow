using System.Globalization;
using SereinFlow.Application.Persistence;
using SereinFlow.Domain;
using SqlSugar;

namespace SereinFlow.Infrastructure.Persistence;

public sealed class SqlSugarFlowRunEventStore : IFlowRunEventStore
{
    private readonly SqliteDatabase _database;

    public SqlSugarFlowRunEventStore(SqliteDatabase database)
    {
        _database = database;
    }

    public void Append(IReadOnlyList<FlowRunEvent> events)
    {
        if (events is null)
            throw new ArgumentNullException(nameof(events), "Flow run events cannot be null. 流程运行事件集合不能为空。");
        if (events.Count == 0)
        {
            return;
        }

        _database.Client.Ado.BeginTran();
        try
        {
            foreach (var item in events.OrderBy(static item => item.Sequence))
            {
                var affected = _database.Execute(
                    "INSERT INTO FlowRunEvents (RunId, Sequence, Type, NodeId, PayloadJson, Timestamp) VALUES (@runId, @sequence, @type, @nodeId, @payloadJson, @timestamp)",
                    new SugarParameter("@runId", item.RunId.ToString("D")),
                    new SugarParameter("@sequence", item.Sequence),
                    new SugarParameter("@type", item.Type),
                    new SugarParameter("@nodeId", item.NodeId),
                    new SugarParameter("@payloadJson", item.PayloadJson),
                    new SugarParameter("@timestamp", item.Timestamp.ToString("O")));
                if (affected != 1)
                {
                    throw new InvalidOperationException($"Event sequence {item.Sequence} could not be inserted. 事件序列 {item.Sequence} 无法写入。");
                }
            }

            _database.Client.Ado.CommitTran();
        }
        catch (Exception exception)
        {
            _database.Client.Ado.RollbackTran();
            throw new InvalidOperationException("Flow run event append failed; the batch was rolled back. 流程运行事件追加失败，批处理已回滚。", exception);
        }
    }

    public IReadOnlyList<FlowRunEvent> GetAfter(Guid runId, long sequenceExclusive)
    {
        var rows = _database.Query<FlowRunEventRow>(
            "SELECT RunId, Sequence, Type, NodeId, PayloadJson, Timestamp FROM FlowRunEvents WHERE RunId = @runId AND Sequence > @sequence ORDER BY Sequence",
            new SugarParameter("@runId", runId.ToString("D")),
            new SugarParameter("@sequence", sequenceExclusive));

        return rows
            .Select(static row => new FlowRunEvent(
                Guid.Parse(row.RunId),
                row.Sequence,
                DateTimeOffset.Parse(row.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                row.Type,
                row.NodeId,
                row.PayloadJson))
            .ToArray();
    }

    private sealed class FlowRunEventRow
    {
        public string RunId { get; set; } = string.Empty;
        public long Sequence { get; set; }
        public string Type { get; set; } = string.Empty;
        public string? NodeId { get; set; }
        public string PayloadJson { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
    }
}

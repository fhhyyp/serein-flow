using System.Globalization;
using SereinFlow.Application.Persistence;
using SereinFlow.Domain;

namespace SereinFlow.Infrastructure.Persistence;
public sealed class SqlSugarFlowRunOutputStore : IFlowRunOutputStore
{
    private readonly IRepository<FlowRunOutputRecord> _outputs;
    private readonly IUnitOfWork _unitOfWork;
    public SqlSugarFlowRunOutputStore(IRepository<FlowRunOutputRecord> outputs, IUnitOfWork unitOfWork)
    {
        _outputs = outputs;
        _unitOfWork = unitOfWork;
    }

    // Kept for the existing Infrastructure test construction path only.
    // 仅保留给既有基础设施测试的构造方式。
    public SqlSugarFlowRunOutputStore(SqliteDatabase database)
    {
        var outputs = new SqlSugarRepository<FlowRunOutputRecord>(database.Client);
        var unitOfWork = new SqlSugarUnitOfWork(database.Client);
        _outputs = outputs;
        _unitOfWork = unitOfWork;
    }

    public async Task AppendAsync(IReadOnlyList<FlowRunOutput> outputs, CancellationToken cancellationToken = default)
    {
        if (outputs is null)
        {
            throw new ArgumentNullException(nameof(outputs), "Flow run outputs cannot be null. 流程运行输出集合不能为空。");
        }

        if (outputs.Count == 0)
        {
            return;
        }

        try
        {
            var orderByResult = outputs.OrderBy(static item => item.Sequence);
            var groupByResult = orderByResult.GroupBy(static item => (item.RunId, item.Sequence));
            var selectResult = groupByResult.Select(static group =>
            {
                var firstResult = group.First();
                return firstResult;
            });
            var distinct = selectResult.ToArray();
            var existingKeys = new HashSet<(string RunId, long Sequence)>();
            foreach (var runGroup in distinct.GroupBy(static item => item.RunId))
            {
                var runId = runGroup.Key.ToString("D");
                var minimum = runGroup.Min(static item => item.Sequence);
                var maximum = runGroup.Max(static item => item.Sequence);
                var existing = await _outputs.ListAsync(row => row.RunId == runId && row.Sequence >= minimum && row.Sequence <= maximum, cancellationToken);
                foreach (var row in existing)
                {
                    existingKeys.Add((row.RunId, row.Sequence));
                }
            }

            await _unitOfWork.ExecuteAsync(async token =>
            {
                foreach (var item in distinct)
                {
                    var evaluatedValue = (item.RunId.ToString("D"), item.Sequence);
                    if (existingKeys.Contains(evaluatedValue))
                    {
                        continue;
                    }

                    var flowRunOutputRecord = new FlowRunOutputRecord
                    {
                        RunId = item.RunId.ToString("D"),
                        Sequence = item.Sequence,
                        NodeId = item.NodeId,
                        Outcome = item.Outcome,
                        Branch = item.Branch,
                        OutputsJson = item.OutputsJson,
                        InputsJson = item.InputsJson,
                        ErrorCode = item.ErrorCode,
                        ErrorMessage = item.ErrorMessage,
                        Timestamp = item.Timestamp.ToString("O")
                    };
                    await _outputs.AddAsync(flowRunOutputRecord, token);
                }

                return true;
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Flow run output append failed; the batch was rolled back. 流程运行输出追加失败，批处理已回滚。", exception);
        }
    }

    public async Task<IReadOnlyList<FlowRunOutput>> ListAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var runIdText = runId.ToString("D");
        var rows = await _outputs.ListAsync(row => row.RunId == runIdText, cancellationToken);
        var orderByResult1 = rows.OrderBy(static row => row.Sequence);
        var selectResult2 = orderByResult1.Select(Map);
        var toArrayResult = selectResult2.ToArray();
        return toArrayResult;
    }

    private static FlowRunOutput Map(FlowRunOutputRecord row)
    {
        var parseResult = Guid.Parse(row.RunId);
        var parseResult3 = DateTimeOffset.Parse(row.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        FlowRunOutput evaluatedValue4 = new(parseResult, row.Sequence, parseResult3, row.NodeId, row.Outcome, row.Branch, row.OutputsJson, row.ErrorCode, row.ErrorMessage, row.InputsJson);
        return evaluatedValue4;
    }
}

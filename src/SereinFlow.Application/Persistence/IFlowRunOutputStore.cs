using SereinFlow.Domain;

namespace SereinFlow.Application.Persistence;

/// <summary>
/// Durable, queryable node outputs for a flow run. This is intentionally
/// separate from IFlowRunEventStore, which remains a diagnostic event stream.
/// 流程运行的可查询持久化节点输出；它与诊断事件流 IFlowRunEventStore 分离。
/// </summary>
public interface IFlowRunOutputStore
{
    Task AppendAsync(IReadOnlyList<FlowRunOutput> outputs, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FlowRunOutput>> ListAsync(Guid runId, CancellationToken cancellationToken = default);
}

using SereinFlow.Contracts;

namespace SereinFlow.Worker.Client;

/// <summary>
/// Trusted API-side port for submitting a serialized flow snapshot to the Worker boundary.
/// Implementations must not deserialize or load user assemblies in the API process.
/// 用于向 Worker 边界提交序列化流程快照的可信 API 端口；实现不得在 API 进程反序列化或加载用户程序集。
/// </summary>
public interface IWorkerRunClient
{
    Task<WorkerRunResultDto> RunAsync(
        WorkerRunRequestDto request,
        IWorkerRunEventSink eventSink,
        CancellationToken cancellationToken = default);
}

public interface IWorkerRunEventSink
{
    ValueTask PublishAsync(WorkerEventEnvelopeDto workerEvent, CancellationToken cancellationToken = default);
}

public sealed class DelegateWorkerRunEventSink(Func<WorkerEventEnvelopeDto, CancellationToken, ValueTask> publish) : IWorkerRunEventSink
{
    private readonly Func<WorkerEventEnvelopeDto, CancellationToken, ValueTask> _publish = publish
        ?? throw new ArgumentNullException(nameof(publish), "The worker event publisher cannot be null. Worker 事件发布器不能为空。");

    public ValueTask PublishAsync(WorkerEventEnvelopeDto workerEvent, CancellationToken cancellationToken = default)
        => _publish(workerEvent, cancellationToken);
}

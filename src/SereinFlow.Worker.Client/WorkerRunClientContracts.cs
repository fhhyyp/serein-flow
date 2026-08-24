using SereinFlow.Contracts;

namespace SereinFlow.Worker.Client;

/// <summary>
/// Trusted API-side port for submitting a serialized flow snapshot to the Worker boundary.
/// Implementations must not deserialize or load user assemblies in the API process.
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
    private readonly Func<WorkerEventEnvelopeDto, CancellationToken, ValueTask> _publish = publish ?? throw new ArgumentNullException(nameof(publish));

    public ValueTask PublishAsync(WorkerEventEnvelopeDto workerEvent, CancellationToken cancellationToken = default)
        => _publish(workerEvent, cancellationToken);
}

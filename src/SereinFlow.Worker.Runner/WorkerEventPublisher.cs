using System.Text.Json;
using SereinFlow.Contracts;
using SereinFlow.Runtime;
using SereinFlow.Runtime.Abstractions;
using SereinFlow.ScriptAdapter;
using SereinFlow.Worker.Protocol;

namespace SereinFlow.Worker.Runner;
internal sealed class WorkerEventPublisher(IWorkerTransport transport, Guid runId) : IRunEventPublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SortedDictionary<long, WorkerEventEnvelopeDto> _pending = [];
    // RunStarted is written by RunnerHost before this publisher is created,
    // so the first runtime event has sequence 2.
    private long _lastWrittenSequence = 1;
    public async ValueTask PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken)
    {
        var eventType = runtimeEvent.Type switch
        {
            RunErrorCodes.Started => WorkerEventType.RunStarted,
            NodeErrorCodes.Started => WorkerEventType.NodeStarted,
            NodeErrorCodes.Completed => WorkerEventType.NodeCompleted,
            NodeErrorCodes.Failed => WorkerEventType.NodeFailed,
            NodeErrorCodes.Error => WorkerEventType.NodeErrored,
            DebugErrorCodes.Paused => WorkerEventType.DebugPaused,
            DebugErrorCodes.TriggerReceived => WorkerEventType.DebugTriggerReceived,
            DebugErrorCodes.TriggerQueued => WorkerEventType.DebugTriggerQueued,
            DebugErrorCodes.TriggerAdmitted => WorkerEventType.DebugTriggerAdmitted,
            DebugErrorCodes.TriggerRejected => WorkerEventType.DebugTriggerRejected,
            DebugErrorCodes.TriggerCompleted => WorkerEventType.DebugTriggerCompleted,
            DebugErrorCodes.TriggerFailed => WorkerEventType.DebugTriggerFailed,
            _ => WorkerEventType.Log
        };
        var toAuditValueResult = ScriptValueConverter.ToAuditValue(runtimeEvent.Payload);
        var createWebOptionsResult = SereinJsonSerialization.CreateWebOptions();
        // Runtime sessions can retain ScriptLang.Value instances so a following
        // DLL node can convert them against its declared type. The protocol
        // boundary must receive only a safe audit projection.
        var payload = JsonSerializer.Serialize(toAuditValueResult, createWebOptionsResult);
        var envelope = new WorkerEventEnvelopeDto(WorkerProtocolConstants.Version, runId, runtimeEvent.Sequence, runtimeEvent.Timestamp, eventType, runtimeEvent.NodeId, payload);
        await PublishEnvelopeAsync(envelope, cancellationToken);
    }

    public ValueTask PublishDebugPausedAsync(WorkerDebugPauseDto pause, long sequence, CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["debugSessionId"] = pause.DebugSessionId,
            ["runId"] = pause.RunId,
            ["nodeId"] = pause.NodeId,
            ["nodeType"] = pause.NodeType,
            ["step"] = pause.Step,
            ["inputs"] = ScriptValueConverter.ToAuditValue(pause.Inputs),
            ["frameDepth"] = pause.FrameDepth,
            ["triggerInvocationId"] = pause.TriggerInvocationId,
            ["executionId"] = pause.ExecutionId
        };
        var createWebOptionsResult1 = SereinJsonSerialization.CreateWebOptions();
        var serializeResult = JsonSerializer.Serialize(payload, createWebOptionsResult1);
        var workerEventEnvelopeDto = new WorkerEventEnvelopeDto(WorkerProtocolConstants.Version, runId, sequence, DateTimeOffset.UtcNow, WorkerEventType.DebugPaused, pause.NodeId, serializeResult);
        var publishEnvelopeAsyncResult = PublishEnvelopeAsync(workerEventEnvelopeDto, cancellationToken);
        return publishEnvelopeAsyncResult;
    }

    private async ValueTask PublishEnvelopeAsync(WorkerEventEnvelopeDto envelope, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _pending[envelope.Sequence] = envelope;
            while (_pending.Remove(_lastWrittenSequence + 1, out var next))
            {
                var serializePayloadResult = WorkerProtocolCodec.SerializePayload(next);
                var createResult = WorkerMessage.Create(WorkerProtocolConstants.EventKind, serializePayloadResult, runId, sequence: next.Sequence);
                await transport.SendAsync(createResult, cancellationToken);
                _lastWrittenSequence = next.Sequence;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }
}

using System.Text.Json;
using SereinFlow.Contracts;
using SereinFlow.Runtime;
using SereinFlow.Runtime.Abstractions;
using SereinFlow.ScriptAdapter;
using SereinFlow.Worker.Protocol;

namespace SereinFlow.Worker.Runner;
internal sealed class WorkerEventPublisher(IWorkerTransport transport, Guid runId) : IRunEventPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateWebOptions();
    internal const int MaximumAuditBytes = 128 * 1024;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SortedDictionary<long, WorkerEventEnvelopeDto> _pending = [];
    // RunStarted is written by RunnerHost before this publisher is created,
    // so the first runtime event has sequence 2.
    // RunnerHost 在创建此发布器之前已写入 RunStarted，因此首个运行时事件的序号为 2。
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
        // Runtime sessions can retain ScriptLang.Value instances so a following
        // DLL node can convert them against its declared type. The protocol
        // boundary must receive only a safe audit projection.
        // 运行时会话可保留 ScriptLang.Value，供后续 DLL 节点按声明类型转换；协议边界只接收安全的审计投影。
        var orderedPayload = runtimeEvent.Payload.OrderBy(static pair => pair.Key is "inputs" or "outputs" or "value" ? 1 : 0).ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        var operationResult1 = ScriptValueConverter.ToBoundedAuditValue(orderedPayload);
        var payload = SerializeAudit(operationResult1);
        var envelope = new WorkerEventEnvelopeDto(WorkerProtocolConstants.Version, runId, runtimeEvent.Sequence, runtimeEvent.Timestamp, eventType, runtimeEvent.NodeId, payload);
        await PublishEnvelopeAsync(envelope, cancellationToken);
    }

    public ValueTask PublishDebugPausedAsync(WorkerDebugPauseDto pause, long sequence, CancellationToken cancellationToken)
    {
        var payload = pause with
        {
            Inputs = ScriptValueConverter.ToBoundedAuditValue(pause.Inputs)
        };
        var serializeResult = SerializeAudit(payload);
        var workerEventEnvelopeDto = new WorkerEventEnvelopeDto(WorkerProtocolConstants.Version, runId, sequence, DateTimeOffset.UtcNow, WorkerEventType.DebugPaused, pause.NodeId, serializeResult);
        var publishEnvelopeAsyncResult = PublishEnvelopeAsync(workerEventEnvelopeDto, cancellationToken);
        return publishEnvelopeAsyncResult;
    }

    private async ValueTask PublishEnvelopeAsync(WorkerEventEnvelopeDto envelope, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_pending.Count >= 1024)
            {
                throw new WorkerProtocolException(WorkerErrorCodes.EventQueueFull, "The ordered event buffer is full. 有序事件缓冲已满。");
            }

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

    private static string SerializeAudit(object? value)
    {
        using var stream = new ScriptValueConverter.AuditBuffer(MaximumAuditBytes);
        JsonSerializer.Serialize(stream, value, JsonOptions);
        var operationResult2 = stream.GetBuffer();
        var operationResult1 = System.Text.Encoding.UTF8.GetString(operationResult2, 0, (int)stream.Length);
        return operationResult1;
    }

    public ValueTask DisposeAsync()
    {
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }
}

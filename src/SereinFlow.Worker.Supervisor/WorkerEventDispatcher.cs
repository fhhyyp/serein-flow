using System.Text;
using System.Threading.Channels;
using SereinFlow.Contracts;
using SereinFlow.Worker.Protocol;

namespace SereinFlow.Worker.Supervisor;

/// <summary>
/// The protocol reader never waits for storage. Saturation explicitly fails the
/// run rather than dropping audit events or blocking control acknowledgements.
/// 协议读取不等待存储；缓冲耗尽时显式失败，不丢弃审计事件或阻塞控制应答。
/// </summary>
internal sealed class WorkerEventDispatcher : IAsyncDisposable
{
    private readonly Channel<WorkerEventEnvelopeDto> _events;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Func<IReadOnlyList<WorkerEventEnvelopeDto>, CancellationToken, ValueTask> _publish;
    private readonly int _maximumBytes;
    private readonly int _batchSize;
    private readonly TimeSpan _processingTimeout;
    private long _bufferedBytes;

    public WorkerEventDispatcher(Func<IReadOnlyList<WorkerEventEnvelopeDto>, CancellationToken, ValueTask> publish, int capacity = 1024, int maximumBytes = 16 * 1024 * 1024, int batchSize = 64, TimeSpan? processingTimeout = null)
    {
        _publish = publish;
        _maximumBytes = maximumBytes;
        _batchSize = batchSize;
        _processingTimeout = processingTimeout ?? TimeSpan.FromSeconds(30);
        var operationResult1 = new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        };
        _events = Channel.CreateBounded<WorkerEventEnvelopeDto>(operationResult1);
        Completion = Task.Run(ConsumeAsync);
    }

    public Task Completion { get; }

    public ValueTask PublishAsync(WorkerEventEnvelopeDto item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = EstimateBytes(item);
        if (Interlocked.Add(ref _bufferedBytes, bytes) > _maximumBytes || !_events.Writer.TryWrite(item))
        {
            Interlocked.Add(ref _bufferedBytes, -bytes);
            throw new WorkerProtocolException(WorkerErrorCodes.EventQueueFull, "The Worker audit event buffer is full. Worker 审计事件缓冲已满。");
        }

        return ValueTask.CompletedTask;
    }

    public async Task CompleteAsync()
    {
        _events.Writer.TryComplete();
        _shutdown.CancelAfter(_processingTimeout);
        await Completion;
    }

    private async Task ConsumeAsync()
    {
        try
        {
            while (await _events.Reader.WaitToReadAsync(_shutdown.Token))
            {
                // A short coalescing window bounds latency while amortizing transactions.
                if (_events.Reader.Count < _batchSize && !_events.Reader.Completion.IsCompleted)
                {
                    var operationResult2 = TimeSpan.FromMilliseconds(10);
                    await Task.Delay(operationResult2, _shutdown.Token);
                }

                var batch = new List<WorkerEventEnvelopeDto>(_batchSize);
                while (batch.Count < _batchSize && _events.Reader.TryRead(out var item))
                    batch.Add(item);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                timeout.CancelAfter(_processingTimeout);
                var publishing = _publish(batch, timeout.Token).AsTask();
                try
                {
                    await publishing.WaitAsync(timeout.Token);
                }
                catch
                {
                    // A custom sink may ignore cancellation; still observe its eventual failure.
                    _ = publishing.ContinueWith(static task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    throw;
                }

                foreach (var item in batch)
                {
                    var bytes = EstimateBytes(item);
                    Interlocked.Add(ref _bufferedBytes, -bytes);
                }
            }
        }
        catch (Exception exception)
        {
            throw new WorkerProtocolException(WorkerErrorCodes.EventProcessingFailed, "Worker event persistence failed or timed out. Worker 事件持久化失败或超时。", exception);
        }
    }

    private static int EstimateBytes(WorkerEventEnvelopeDto item)
    {
        int operationResult1 = Encoding.UTF8.GetByteCount(item.PayloadJson) + item.PayloadJson.Length * 2 + 512;
        return operationResult1;
    }
    public async ValueTask DisposeAsync()
    {
        _events.Writer.TryComplete();
        await _shutdown.CancelAsync();
        try
        {
            await Completion;
        }
        catch (WorkerProtocolException)
        {
            // The monitor reports the original failure.
        }
        _shutdown.Dispose();
    }
}

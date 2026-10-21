using SereinFlow.Contracts;
using SereinFlow.Worker.Protocol;
using SereinFlow.Worker.Supervisor;

namespace SereinFlow.Worker.IntegrationTests;

public sealed class WorkerEventDispatcherTests
{
    [Fact]
    public async Task DrainPreservesOrderAndBatchesBeforeCompletion()
    {
        var sequences = new List<long>();
        var batches = new List<int>();
        await using var dispatcher = new WorkerEventDispatcher((batch, _) =>
        {
            batches.Add(batch.Count);
            var operationResult1 = batch.Select(item => item.Sequence);
            sequences.AddRange(operationResult1);
            return ValueTask.CompletedTask;
        });
        for (var index = 1; index <= 100; index++)
        {
            var item = Event(index);
            await dispatcher.PublishAsync(item, CancellationToken.None);
        }
        await dispatcher.CompleteAsync();
        var operationResult2 = Enumerable.Range(1, 100).Select(index => (long)index);
        Assert.Equal(operationResult2, sequences);
        Assert.Contains(batches, count => count > 1);
    }

    [Fact]
    public async Task SaturationFailsExplicitlyWithoutWaitingForSlowSink()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var dispatcher = new WorkerEventDispatcher(async (_, token) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }, capacity: 1);
        var operationResult4 = Event(1);
        await dispatcher.PublishAsync(operationResult4, CancellationToken.None);
        var operationResult5 = TimeSpan.FromSeconds(5);
        await entered.Task.WaitAsync(operationResult5);
        var operationResult6 = Event(2);
        await dispatcher.PublishAsync(operationResult6, CancellationToken.None);
        var failure = await Assert.ThrowsAsync<WorkerProtocolException>(() =>
        {
            var operationResult3 = Event(3);
            var operationResult1 = dispatcher.PublishAsync(operationResult3, CancellationToken.None).AsTask();
            return operationResult1;
        });
        Assert.Equal(WorkerErrorCodes.EventQueueFull, failure.Code);
    }

    [Fact]
    public async Task ByteBudgetAndPersistenceFailuresAreExplicit()
    {
        await using var small = new WorkerEventDispatcher((_, _) => ValueTask.CompletedTask, maximumBytes: 1);
        var overflow = await Assert.ThrowsAsync<WorkerProtocolException>(() =>
        {
            var operationResult7 = Event(1);
            var operationResult2 = small.PublishAsync(operationResult7, CancellationToken.None).AsTask();
            return operationResult2;
        });
        Assert.Equal(WorkerErrorCodes.EventQueueFull, overflow.Code);
        await using var failing = new WorkerEventDispatcher((_, _) =>
        {
            throw new IOException("storage unavailable");
        });
        var operationResult8 = Event(1);
        await failing.PublishAsync(operationResult8, CancellationToken.None);
        var failure = await Assert.ThrowsAsync<WorkerProtocolException>(() =>
        {
            var operationResult3 = failing.CompleteAsync();
            return operationResult3;
        });
        Assert.Equal(WorkerErrorCodes.EventProcessingFailed, failure.Code);
    }

    [Fact]
    public async Task UncooperativeSinkCannotHoldDrainForever()
    {
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operationResult10 = TimeSpan.FromMilliseconds(100);
        await using var dispatcher = new WorkerEventDispatcher((_, _) =>
        {
            var operationResult4 = new ValueTask(blocked.Task);
            return operationResult4;
        }, processingTimeout: operationResult10);
        var operationResult11 = Event(1);
        await dispatcher.PublishAsync(operationResult11, CancellationToken.None);
        try
        {
            var failure = await Assert.ThrowsAsync<WorkerProtocolException>(() =>
            {
                var operationResult9 = TimeSpan.FromSeconds(5);
                var operationResult5 = dispatcher.CompleteAsync().WaitAsync(operationResult9);
                return operationResult5;
            });
            Assert.Equal(WorkerErrorCodes.EventProcessingFailed, failure.Code);
        }
        finally
        {
            blocked.TrySetResult();
        }
    }

    private static WorkerEventEnvelopeDto Event(long sequence)
    {
        WorkerEventEnvelopeDto operationResult6 = new(WorkerProtocol.Version, Guid.Empty, sequence, DateTimeOffset.UtcNow, WorkerEventType.Log, null, "{}");
        return operationResult6;
    }
}

using System.Collections.Concurrent;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Runtime;

public sealed class FlowExecutionSession : IExecutionContext, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, object?> _values = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _cancellationSource;
    private long _sequence;
    private int _cancelled;

    public FlowExecutionSession(Guid? runId = null, CancellationToken cancellationToken = default)
    {
        RunId = runId ?? Guid.NewGuid();
        _cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Resources = new ResourceLeaseRegistry();
    }

    public Guid RunId { get; }

    public ResourceLeaseRegistry Resources { get; }

    public CancellationToken CancellationToken => _cancellationSource.Token;

    public object? Read(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Execution context keys cannot be empty. 执行上下文键不能为空。", nameof(key));
        return _values.TryGetValue(key, out var value) ? value : null;
    }

    public void Write(string key, object? value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Execution context keys cannot be empty. 执行上下文键不能为空。", nameof(key));
        _values[key] = value;
    }

    public IReadOnlyDictionary<string, object?> Snapshot() => new Dictionary<string, object?>(_values, StringComparer.Ordinal);

    public long NextSequence() => Interlocked.Increment(ref _sequence);

    public bool Cancel()
    {
        if (Interlocked.Exchange(ref _cancelled, 1) == 1)
        {
            return false;
        }

        _cancellationSource.Cancel();
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        await Resources.DisposeAsync();
        _cancellationSource.Dispose();
    }
}

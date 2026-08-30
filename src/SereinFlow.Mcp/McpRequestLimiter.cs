using System.Collections.Concurrent;

namespace SereinFlow.Mcp;

/// <summary>
/// Bounds remote MCP work per authenticated principal. The limiter is kept in
/// the HTTP host and is deliberately independent from business authorization.
/// 按已认证主体限制远程 MCP 请求；它只负责流量边界，不替代业务权限检查。
/// </summary>
public sealed class McpRequestLimiter : IDisposable
{
    private readonly SemaphoreSlim _concurrency;
    private readonly int _maxRequestsPerMinute;
    private readonly ConcurrentDictionary<string, RateWindow> _windows = new(StringComparer.Ordinal);

    public McpRequestLimiter(int maxConcurrentRequests = 16, int maxRequestsPerMinute = 120)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrentRequests, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRequestsPerMinute, 1);
        _concurrency = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
        _maxRequestsPerMinute = maxRequestsPerMinute;
    }

    public bool TryAcquire(string principalId, out IDisposable? lease)
    {
        if (string.IsNullOrWhiteSpace(principalId) || !_concurrency.Wait(0))
        {
            lease = null;
            return false;
        }

        var window = _windows.GetOrAdd(principalId, static _ => new RateWindow());
        var now = DateTimeOffset.UtcNow;
        var accepted = false;
        lock (window.Gate)
        {
            while (window.Requests.Count > 0 && now - window.Requests.Peek() >= TimeSpan.FromMinutes(1))
                window.Requests.Dequeue();
            if (window.Requests.Count < _maxRequestsPerMinute)
            {
                window.Requests.Enqueue(now);
                accepted = true;
            }
        }

        if (!accepted)
        {
            _concurrency.Release();
            lease = null;
            return false;
        }

        lease = new LimiterLease(_concurrency);
        return true;
    }

    public void Dispose() => _concurrency.Dispose();

    private sealed class RateWindow
    {
        public object Gate { get; } = new();
        public Queue<DateTimeOffset> Requests { get; } = new();
    }

    private sealed class LimiterLease(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                semaphore.Release();
        }
    }
}

namespace SereinFlow.Runtime;
public sealed class ResourceLeaseRegistry : IAsyncDisposable, IDisposable
{
    private readonly object _gate = new();
    private readonly List<IAsyncDisposable> _leases = [];
    private bool _disposed;
    public void Register(IDisposable resource)
    {
        if (resource is null)
        {
            throw new ArgumentNullException(nameof(resource), "The resource cannot be null. 资源不能为空。");
        }

        var asyncDisposableAdapter = new AsyncDisposableAdapter(resource);
        Register(asyncDisposableAdapter);
    }

    public void Register(IAsyncDisposable resource)
    {
        if (resource is null)
        {
            throw new ArgumentNullException(nameof(resource), "The resource cannot be null. 资源不能为空。");
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _leases.Add(resource);
        }
    }

    public void Dispose()
    {
        var disposeAsyncResult = DisposeAsync();
        var asTaskResult = disposeAsyncResult.AsTask();
        var getAwaiterResult = asTaskResult.GetAwaiter();
        getAwaiterResult.GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        List<IAsyncDisposable> leases;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            leases = [.._leases];
            _leases.Clear();
        }

        for (var index = leases.Count - 1; index >= 0; index--)
        {
            await leases[index].DisposeAsync();
        }
    }

    private sealed class AsyncDisposableAdapter : IAsyncDisposable
    {
        private readonly IDisposable _resource;
        public AsyncDisposableAdapter(IDisposable resource)
        {
            _resource = resource;
        }

        public ValueTask DisposeAsync()
        {
            _resource.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

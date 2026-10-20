using System.Linq.Expressions;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Infrastructure.Persistence;

namespace SereinFlow.Infrastructure.Tests;

public sealed class EnvironmentConsolePersistenceTests
{
    [Fact]
    public async Task EnvironmentSettingsStorePersistsTheOperatorConfiguration()
    {
        var repository = new InMemoryRepository<RunEnvironmentSettingsRecord>(static item => item.Id);
        var store = new SqlSugarRunEnvironmentSettingsStore(repository);
        var configured = new RunExecutionSettingsDto(QueueCapacity: 120, MaxConcurrentRuns: 8, MaxConcurrentListenerRuns: 3, MaxConcurrentRunsPerProject: 4, QueueWaitTimeoutSeconds: 75, ShutdownGracePeriodSeconds: 15, SynchronousInvocationTimeoutSeconds: 25, MaxLibraryUploadBytes: 128 * 1024 * 1024);
        var saved = await store.SaveAsync(configured);
        var loaded = await store.GetAsync();
        Assert.Equal(configured, saved);
        Assert.Equal(configured, loaded);
        var evaluatedValue = repository.Items.Single().UpdatedAt;
        Assert.NotEmpty(evaluatedValue);
    }

    [Fact]
    public async Task FlowInterfaceRepositorySupportsPublishedInterfaceLifecycle()
    {
        var repository = new InMemoryRepository<FlowInterfaceRecord>(static item => item.Id);
        var interfaces = new SqlSugarFlowInterfaceRepository(repository);
        var createdAt = DateTimeOffset.UtcNow;
        var newGuidResult = Guid.NewGuid();
        var newGuidResult1 = Guid.NewGuid();
        var newGuidResult2 = Guid.NewGuid();
        var published = new FlowInterfaceDto(newGuidResult, newGuidResult1, newGuidResult2, "Order status callback", FlowInvocationModeDto.Synchronous, true, createdAt, createdAt);
        await interfaces.AddAsync(published);
        var stored = await interfaces.FindAsync(published.Id);
        var updated = published with
        {
            Name = "Order status polling",
            InvocationMode = FlowInvocationModeDto.Asynchronous,
            IsEnabled = false,
            UpdatedAt = createdAt.AddMinutes(1)
        };
        Assert.Equal(published, stored);
        var updateAsyncResult = await interfaces.UpdateAsync(updated);
        Assert.True(updateAsyncResult);
        var findAsyncResult = await interfaces.FindAsync(updated.Id);
        Assert.Equal(updated, findAsyncResult);
        var listAsyncResult = await interfaces.ListAsync();
        Assert.Single(listAsyncResult);
        var deleteAsyncResult = await interfaces.DeleteAsync(updated.Id);
        Assert.True(deleteAsyncResult);
        var findAsyncResult3 = await interfaces.FindAsync(updated.Id);
        Assert.Null(findAsyncResult3);
    }

    private sealed class InMemoryRepository<TEntity> : IRepository<TEntity> where TEntity : class
    {
        private readonly Func<TEntity, object> _keySelector;
        private readonly Dictionary<object, TEntity> _items = [];
        public InMemoryRepository(Func<TEntity, object> keySelector)
        {
            _keySelector = keySelector;
        }

        public IReadOnlyCollection<TEntity> Items => _items.Values;

        public Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TEntity? evaluatedValue4 = _items.TryGetValue(id, out var item) ? item : null;
            var fromResultResult = Task.FromResult(evaluatedValue4);
            return fromResultResult;
        }

        public Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filter = predicate?.Compile();
            IReadOnlyList<TEntity> result = _items.Values
                .Where(item =>
                {
                    var evaluatedValue5 = filter?.Invoke(item) ?? true;
                    return evaluatedValue5;
                })
                .ToArray();
            var fromResultResult6 = Task.FromResult(result);
            return fromResultResult6;
        }

        public Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var keySelectorResult = _keySelector(entity);
            _items.Add(keySelectorResult, entity);
            var fromResultResult7 = Task.FromResult(entity);
            return fromResultResult7;
        }

        public Task<bool> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = _keySelector(entity);
            if (!_items.ContainsKey(key))
            {
                var fromResultResult8 = Task.FromResult(false);
                return fromResultResult8;
            }

            _items[key] = entity;
            var fromResultResult9 = Task.FromResult(true);
            return fromResultResult9;
        }

        public Task<bool> DeleteAsync(object id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var removeResult = _items.Remove(id);
            var fromResultResult10 = Task.FromResult(removeResult);
            return fromResultResult10;
        }
    }
}

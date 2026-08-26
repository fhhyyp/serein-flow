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
        var configured = new RunExecutionSettingsDto(
            QueueCapacity: 120,
            MaxConcurrentRuns: 8,
            MaxConcurrentListenerRuns: 3,
            MaxConcurrentRunsPerProject: 4,
            QueueWaitTimeoutSeconds: 75,
            ShutdownGracePeriodSeconds: 15,
            SynchronousInvocationTimeoutSeconds: 25);

        var saved = await store.SaveAsync(configured);
        var loaded = await store.GetAsync();

        Assert.Equal(configured, saved);
        Assert.Equal(configured, loaded);
        Assert.NotEmpty(repository.Items.Single().UpdatedAt);
    }

    [Fact]
    public async Task FlowInterfaceRepositorySupportsPublishedInterfaceLifecycle()
    {
        var repository = new InMemoryRepository<FlowInterfaceRecord>(static item => item.Id);
        var interfaces = new SqlSugarFlowInterfaceRepository(repository);
        var createdAt = DateTimeOffset.UtcNow;
        var published = new FlowInterfaceDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Order status callback",
            FlowInvocationModeDto.Synchronous,
            true,
            createdAt,
            createdAt);

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
        Assert.True(await interfaces.UpdateAsync(updated));
        Assert.Equal(updated, await interfaces.FindAsync(updated.Id));
        Assert.Single(await interfaces.ListAsync());
        Assert.True(await interfaces.DeleteAsync(updated.Id));
        Assert.Null(await interfaces.FindAsync(updated.Id));
    }

    private sealed class InMemoryRepository<TEntity> : IRepository<TEntity>
        where TEntity : class
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
            return Task.FromResult(_items.TryGetValue(id, out var item) ? item : null);
        }

        public Task<IReadOnlyList<TEntity>> ListAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filter = predicate?.Compile();
            IReadOnlyList<TEntity> result = _items.Values
                .Where(item => filter?.Invoke(item) ?? true)
                .ToArray();
            return Task.FromResult(result);
        }

        public Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _items.Add(_keySelector(entity), entity);
            return Task.FromResult(entity);
        }

        public Task<bool> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = _keySelector(entity);
            if (!_items.ContainsKey(key))
                return Task.FromResult(false);
            _items[key] = entity;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(object id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_items.Remove(id));
        }
    }
}

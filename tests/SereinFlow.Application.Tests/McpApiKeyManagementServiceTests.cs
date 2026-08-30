using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application.Tests;

public sealed class McpApiKeyManagementServiceTests
{
    [Fact]
    public async Task InitialSetupCreatesOneAdministratorAndPersistsOnlyTheHash()
    {
        var store = new InMemoryApiKeyStore();
        var service = new McpApiKeyManagementService(store, new EmptyProjectRepository());

        var created = await service.CreateInitialAdministratorAsync();

        Assert.True(created.Key.IsAdministrator);
        Assert.StartsWith("sfk_", created.Secret, StringComparison.Ordinal);
        var saved = Assert.Single(await store.ListAsync());
        Assert.NotEqual(created.Secret, saved.SecretHash);
        Assert.DoesNotContain(created.Secret, saved.SecretHash, StringComparison.Ordinal);

        var secondAttempt = await Assert.ThrowsAsync<McpSecurityException>(
            () => service.CreateInitialAdministratorAsync());
        Assert.Equal("mcp.key_setup_already_completed", secondAttempt.Code);
        Assert.Equal(409, secondAttempt.StatusCode);
        Assert.Single(await store.ListAsync());
    }

    [Fact]
    public async Task RotationRevokesTheOldKeyAndReturnsAUsableReplacementSecret()
    {
        var store = new InMemoryApiKeyStore();
        var service = new McpApiKeyManagementService(store, new EmptyProjectRepository());
        var initial = await service.CreateInitialAdministratorAsync();

        var rotated = await service.RotateAsync(initial.Key.Id);

        Assert.Equal(initial.Key.Id, rotated.RevokedKeyId);
        Assert.NotNull(rotated.Secret);
        Assert.NotEqual(initial.Secret, rotated.Secret);
        Assert.NotNull((await store.FindAsync(initial.Key.Id))!.RevokedAt);
        Assert.Null((await store.FindAsync(rotated.Key.Id))!.RevokedAt);

        var security = new McpSecurityService(store);
        Assert.Null(await security.AuthenticateAsync(initial.Secret));
        Assert.NotNull(await security.AuthenticateAsync(rotated.Secret));
    }

    private sealed class InMemoryApiKeyStore : IMcpApiKeyStore
    {
        private readonly List<McpApiKeyEntry> _entries = [];

        public Task<IReadOnlyList<McpApiKeyEntry>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<McpApiKeyEntry>>(_entries.ToArray());

        public Task<McpApiKeyEntry?> FindAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(_entries.FirstOrDefault(entry => entry.Id == id));

        public Task AddAsync(McpApiKeyEntry entry, CancellationToken cancellationToken = default)
        {
            _entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<bool> UpdateAsync(McpApiKeyEntry entry, CancellationToken cancellationToken = default)
        {
            var index = _entries.FindIndex(item => item.Id == entry.Id);
            if (index < 0)
                return Task.FromResult(false);
            _entries[index] = entry;
            return Task.FromResult(true);
        }
    }

    private sealed class EmptyProjectRepository : IProjectRepository
    {
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Project>>([]);

        public Task<Project?> FindAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Project?>(null);

        public Task AddAsync(Project project, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> TryUpdateAsync(Project project, long expectedVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public IReadOnlyList<Project> List() => [];

        public Project? Find(Guid id) => null;

        public void Add(Project project) { }

        public bool TryUpdate(Project project, long expectedVersion) => false;
    }
}

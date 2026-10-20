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
        var emptyProjectRepository = new EmptyProjectRepository();
        var service = new McpApiKeyManagementService(store, emptyProjectRepository);
        var created = await service.CreateInitialAdministratorAsync();
        Assert.True(created.Key.IsAdministrator);
        Assert.StartsWith("sfk_", created.Secret, StringComparison.Ordinal);
        var listAsyncResult = await store.ListAsync();
        var saved = Assert.Single(listAsyncResult);
        Assert.NotEqual(created.Secret, saved.SecretHash);
        Assert.DoesNotContain(created.Secret, saved.SecretHash, StringComparison.Ordinal);
        var secondAttempt = await Assert.ThrowsAsync<McpSecurityException>(() =>
        {
            var createInitialAdministratorAsyncResult = service.CreateInitialAdministratorAsync();
            return createInitialAdministratorAsyncResult;
        });
        Assert.Equal(McpErrorCodes.KeySetupAlreadyCompleted, secondAttempt.Code);
        Assert.Equal(409, secondAttempt.StatusCode);
        var listAsyncResult1 = await store.ListAsync();
        Assert.Single(listAsyncResult1);
    }

    [Fact]
    public async Task RotationRevokesTheOldKeyAndReturnsAUsableReplacementSecret()
    {
        var store = new InMemoryApiKeyStore();
        var emptyProjectRepository2 = new EmptyProjectRepository();
        var service = new McpApiKeyManagementService(store, emptyProjectRepository2);
        var initial = await service.CreateInitialAdministratorAsync();
        var rotated = await service.RotateAsync(initial.Key.Id);
        Assert.Equal(initial.Key.Id, rotated.RevokedKeyId);
        Assert.NotNull(rotated.Secret);
        Assert.NotEqual(initial.Secret, rotated.Secret);
        var evaluatedValue = (await store.FindAsync(initial.Key.Id))!.RevokedAt;
        Assert.NotNull(evaluatedValue);
        var evaluatedValue3 = (await store.FindAsync(rotated.Key.Id))!.RevokedAt;
        Assert.Null(evaluatedValue3);
        var security = new McpSecurityService(store);
        var authenticateAsyncResult = await security.AuthenticateAsync(initial.Secret);
        Assert.Null(authenticateAsyncResult);
        var authenticateAsyncResult4 = await security.AuthenticateAsync(rotated.Secret);
        Assert.NotNull(authenticateAsyncResult4);
    }

    private sealed class InMemoryApiKeyStore : IMcpApiKeyStore
    {
        private readonly List<McpApiKeyEntry> _entries = [];
        public Task<IReadOnlyList<McpApiKeyEntry>> ListAsync(CancellationToken cancellationToken = default)
        {
            var toArrayResult = _entries.ToArray();
            var fromResultResult = Task.FromResult<IReadOnlyList<McpApiKeyEntry>>(toArrayResult);
            return fromResultResult;
        }

        public Task<McpApiKeyEntry?> FindAsync(string id, CancellationToken cancellationToken = default)
        {
            var firstOrDefaultResult = _entries.FirstOrDefault(entry => entry.Id == id);
            var fromResultResult5 = Task.FromResult(firstOrDefaultResult);
            return fromResultResult5;
        }

        public Task AddAsync(McpApiKeyEntry entry, CancellationToken cancellationToken = default)
        {
            _entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<bool> UpdateAsync(McpApiKeyEntry entry, CancellationToken cancellationToken = default)
        {
            var index = _entries.FindIndex(item => item.Id == entry.Id);
            if (index < 0)
            {
                var fromResultResult6 = Task.FromResult(false);
                return fromResultResult6;
            }

            _entries[index] = entry;
            var fromResultResult7 = Task.FromResult(true);
            return fromResultResult7;
        }
    }

    private sealed class EmptyProjectRepository : IProjectRepository
    {
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
        {
            var fromResultResult8 = Task.FromResult<IReadOnlyList<Project>>([]);
            return fromResultResult8;
        }

        public Task<Project?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var fromResultResult9 = Task.FromResult<Project?>(null);
            return fromResultResult9;
        }

        public Task AddAsync(Project project, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> TryUpdateAsync(Project project, long expectedVersion, CancellationToken cancellationToken = default)
        {
            var fromResultResult10 = Task.FromResult(false);
            return fromResultResult10;
        }

        public IReadOnlyList<Project> List() => [];
        public Project? Find(Guid id) => null;
        public void Add(Project project)
        {
        }

        public bool TryUpdate(Project project, long expectedVersion) => false;
    }
}

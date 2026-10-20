using System.Text.Json;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Application.Tests;
public sealed class McpSecurityAndIdempotencyTests
{
    [Fact]
    public async Task BootstrapKeyAuthenticatesAsAdministrator()
    {
        var inMemoryApiKeyStore = new InMemoryApiKeyStore();
        var security = new McpSecurityService(inMemoryApiKeyStore, "bootstrap-secret");
        var principal = await security.AuthenticateAsync("bootstrap-secret");
        Assert.NotNull(principal);
        Assert.True(principal.IsAdministrator);
        Assert.Null(principal.ProjectId);
        Assert.Contains(McpPermissionDto.RunMessagePublish, principal.Permissions);
        var newGuidResult = Guid.NewGuid();
        security.Require(principal, McpPermissionDto.SensitiveRead, newGuidResult);
    }

    [Fact]
    public async Task ProjectKeyCannotCrossProjectOrReadSensitiveDataWithoutPermission()
    {
        var newGuidResult2 = Guid.NewGuid();
        var createMcpApiKeyRequestDto = new CreateMcpApiKeyRequestDto(newGuidResult2, "project-key", [McpPermissionDto.ProjectRead]);
        var created = McpSecurityService.CreateKeyWithEntry(createMcpApiKeyRequestDto);
        var store = new InMemoryApiKeyStore(created.Entry);
        var security = new McpSecurityService(store);
        var principal = await security.AuthenticateAsync(created.Dto.Secret);
        Assert.NotNull(principal);
        security.Require(principal, McpPermissionDto.ProjectRead, created.Entry.ProjectId!.Value);
        Assert.Throws<McpSecurityException>(() =>
        {
            var newGuidResult1 = Guid.NewGuid();
            security.Require(principal, McpPermissionDto.ProjectRead, newGuidResult1);
        });
        Assert.Throws<McpSecurityException>(() =>
        {
            security.Require(principal, McpPermissionDto.SensitiveRead, created.Entry.ProjectId.Value);
        });
    }

    [Fact]
    public async Task SuccessfulAuthenticationWaitsForLastUsedTracking()
    {
        var newGuidResult3 = Guid.NewGuid();
        var createMcpApiKeyRequestDto4 = new CreateMcpApiKeyRequestDto(newGuidResult3, "tracked-key", [McpPermissionDto.ProjectRead]);
        var created = McpSecurityService.CreateKeyWithEntry(createMcpApiKeyRequestDto4);
        var store = new InMemoryApiKeyStore(created.Entry);
        var security = new McpSecurityService(store);
        var principal = await security.AuthenticateAsync(created.Dto.Secret);
        Assert.NotNull(principal);
        var listAsyncResult = await store.ListAsync();
        var evaluatedValue = Assert.Single(listAsyncResult).LastUsedAt;
        Assert.NotNull(evaluatedValue);
    }

    [Fact]
    public async Task ExpiredAndRevokedKeysCannotAuthenticate()
    {
        var newGuidResult5 = Guid.NewGuid();
        var createMcpApiKeyRequestDto6 = new CreateMcpApiKeyRequestDto(newGuidResult5, "expired", []);
        var expiredCreated = McpSecurityService.CreateKeyWithEntry(createMcpApiKeyRequestDto6);
        var expired = expiredCreated.Entry with
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        var newGuidResult7 = Guid.NewGuid();
        var createMcpApiKeyRequestDto8 = new CreateMcpApiKeyRequestDto(newGuidResult7, "revoked", []);
        var revokedCreated = McpSecurityService.CreateKeyWithEntry(createMcpApiKeyRequestDto8);
        var revoked = revokedCreated.Entry with
        {
            RevokedAt = DateTimeOffset.UtcNow
        };
        var inMemoryApiKeyStore9 = new InMemoryApiKeyStore(expired, revoked);
        var security = new McpSecurityService(inMemoryApiKeyStore9);
        var authenticateAsyncResult = await security.AuthenticateAsync(expiredCreated.Dto.Secret);
        Assert.Null(authenticateAsyncResult);
        var authenticateAsyncResult10 = await security.AuthenticateAsync(revokedCreated.Dto.Secret);
        Assert.Null(authenticateAsyncResult10);
    }

    [Fact]
    public async Task ExpiredPreviewIsMarkedAndCannotBeApplied()
    {
        var store = new InMemoryPreviewStore();
        var service = new McpPreviewService(store);
        var newGuidResult11 = Guid.NewGuid();
        var toHashSetResult = new[]
        {
            McpPermissionDto.FlowWrite
        }.ToHashSet();
        var principal = new McpPrincipal("preview-caller", newGuidResult11, toHashSetResult);
        var newGuidResult12 = Guid.NewGuid();
        var preview = await service.CreateAsync("flow.patch", principal, principal.ProjectId, newGuidResult12, 1, new { value = "candidate" });
        var evaluatedValue13 = preview with
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        await store.UpdateAsync(evaluatedValue13);
        var exception = await Assert.ThrowsAsync<McpSecurityException>(() =>
        {
            var requireAsyncResult = service.RequireAsync(preview.Id, preview.PreviewFingerprint, principal);
            return requireAsyncResult;
        });
        Assert.Equal(McpErrorCodes.PreviewExpired, exception.Code);
        var evaluatedValue14 = (await store.FindAsync(preview.Id))!.Status;
        Assert.Equal(McpMutationPreviewStatusDto.Expired, evaluatedValue14);
    }

    [Fact]
    public void KeyScopeRulesRejectInvalidRequests()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            var createMcpApiKeyRequestDto15 = new CreateMcpApiKeyRequestDto(null, "ordinary", []);
            var createKeyWithEntryResult = McpSecurityService.CreateKeyWithEntry(createMcpApiKeyRequestDto15);
            return createKeyWithEntryResult;
        });
        Assert.Throws<ArgumentException>(() =>
        {
            var newGuidResult16 = Guid.NewGuid();
            var createMcpApiKeyRequestDto17 = new CreateMcpApiKeyRequestDto(newGuidResult16, "admin", [], IsAdministrator: true);
            var createKeyWithEntryResult18 = McpSecurityService.CreateKeyWithEntry(createMcpApiKeyRequestDto17);
            return createKeyWithEntryResult18;
        });
        Assert.Throws<ArgumentException>(() =>
        {
            var newGuidResult19 = Guid.NewGuid();
            var addSecondsResult = DateTimeOffset.UtcNow.AddSeconds(-1);
            var createMcpApiKeyRequestDto20 = new CreateMcpApiKeyRequestDto(newGuidResult19, "expired", [], addSecondsResult);
            var createKeyWithEntryResult21 = McpSecurityService.CreateKeyWithEntry(createMcpApiKeyRequestDto20);
            return createKeyWithEntryResult21;
        });
    }

    [Fact]
    public void PermissionWireNamesUseDottedContractAndReadLegacyNames()
    {
        var options = SereinJsonSerialization.CreateWebOptions();
        var json = JsonSerializer.Serialize(McpPermissionDto.FlowWrite, options);
        Assert.Equal("\"flow.write\"", json);
        var tryParseResult = McpPermissionNames.TryParse("project.read", out var dotted);
        Assert.True(tryParseResult);
        Assert.Equal(McpPermissionDto.ProjectRead, dotted);
        var tryParseResult22 = McpPermissionNames.TryParse("FlowWrite", out var legacy);
        Assert.True(tryParseResult22);
        Assert.Equal(McpPermissionDto.FlowWrite, legacy);
        var deserializeResult = JsonSerializer.Deserialize<McpPermissionDto>("\"libraryImport\"", options);
        Assert.Equal(McpPermissionDto.LibraryImport, deserializeResult);
    }

    [Fact]
    public async Task IdempotencyRejectsRequestFingerprintReuse()
    {
        var store = new InMemoryIdempotencyStore();
        var service = new McpIdempotencyService(store);
        await service.SaveAsync("principal", "flow.patch", "request-1", new { accepted = true }, "{\"value\":1}");
        var findAsyncResult23 = await service.FindAsync("principal", "flow.patch", "request-1", "{\"value\":1}");
        Assert.NotNull(findAsyncResult23);
        var exception = await Assert.ThrowsAsync<McpSecurityException>(() =>
        {
            var findAsyncResult = service.FindAsync("principal", "flow.patch", "request-1", "{\"value\":2}");
            return findAsyncResult;
        });
        Assert.Equal(McpErrorCodes.IdempotencyConflict, exception.Code);
    }

    private sealed class InMemoryApiKeyStore(params McpApiKeyEntry[] entries) : IMcpApiKeyStore
    {
        private readonly List<McpApiKeyEntry> _entries = entries.ToList();
        public Task<IReadOnlyList<McpApiKeyEntry>> ListAsync(CancellationToken cancellationToken = default)
        {
            var toArrayResult = _entries.ToArray();
            var fromResultResult = Task.FromResult<IReadOnlyList<McpApiKeyEntry>>(toArrayResult);
            return fromResultResult;
        }

        public Task<McpApiKeyEntry?> FindAsync(string id, CancellationToken cancellationToken = default)
        {
            var firstOrDefaultResult = _entries.FirstOrDefault(item => item.Id == id);
            var fromResultResult24 = Task.FromResult(firstOrDefaultResult);
            return fromResultResult24;
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
                var fromResultResult25 = Task.FromResult(false);
                return fromResultResult25;
            }

            _entries[index] = entry;
            var fromResultResult26 = Task.FromResult(true);
            return fromResultResult26;
        }
    }

    private sealed class InMemoryIdempotencyStore : IMcpIdempotencyStore
    {
        private readonly List<McpIdempotencyEntry> _entries = [];
        public Task<McpIdempotencyEntry?> FindAsync(string principalId, string operation, string keyHash, CancellationToken cancellationToken = default)
        {
            var firstOrDefaultResult27 = _entries.FirstOrDefault(item => item.PrincipalId == principalId && item.Operation == operation && item.KeyHash == keyHash);
            var fromResultResult28 = Task.FromResult(firstOrDefaultResult27);
            return fromResultResult28;
        }

        public Task AddAsync(McpIdempotencyEntry entry, CancellationToken cancellationToken = default)
        {
            _entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryPreviewStore : IMcpPreviewStore
    {
        private readonly List<McpPreviewEntry> _entries = [];
        public Task<McpPreviewEntry?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var firstOrDefaultResult29 = _entries.FirstOrDefault(item => item.Id == id);
            var fromResultResult30 = Task.FromResult(firstOrDefaultResult29);
            return fromResultResult30;
        }

        public Task AddAsync(McpPreviewEntry entry, CancellationToken cancellationToken = default)
        {
            _entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<bool> UpdateAsync(McpPreviewEntry entry, CancellationToken cancellationToken = default)
        {
            var index = _entries.FindIndex(item => item.Id == entry.Id);
            if (index < 0)
            {
                var fromResultResult31 = Task.FromResult(false);
                return fromResultResult31;
            }

            _entries[index] = entry;
            var fromResultResult32 = Task.FromResult(true);
            return fromResultResult32;
        }
    }
}

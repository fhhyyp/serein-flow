using SereinFlow.Security.ApiKeys;
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
        var security = new ApiKeyService(inMemoryApiKeyStore, "bootstrap-secret");
        var principal = await security.AuthenticateAsync("bootstrap-secret");
        Assert.NotNull(principal);
        {
            var actual1 = principal.Principal.HasClaim("sereinflow.admin", "true");
            Assert.True(actual1);
        }

        {
            var actual2 = SecurityTestSupport.ProjectId(principal);
            Assert.Null(actual2);
        }

        {
            var actual3 = SecurityTestSupport.Permissions(principal);
            Assert.Contains(SereinFlowPermission.RunMessagePublish, actual3);
        }

        var newGuidResult = Guid.NewGuid();
        security.Require(principal, SereinFlowPermission.SensitiveRead, newGuidResult);
    }

    [Fact]
    public async Task ProjectKeyCannotCrossProjectOrReadSensitiveDataWithoutPermission()
    {
        var newGuidResult2 = Guid.NewGuid();
        var createMcpApiKeyRequestDto = new CreateApiKeyRequest(newGuidResult2, "project-key", [SereinFlowPermission.ProjectRead]);
        var created = ApiKeyService.CreateKeyWithEntry(createMcpApiKeyRequestDto);
        var store = new InMemoryApiKeyStore(created.Entry);
        var security = new ApiKeyService(store);
        var principal = await security.AuthenticateAsync(created.Dto.Secret);
        Assert.NotNull(principal);
        security.Require(principal, SereinFlowPermission.ProjectRead, created.Entry.ProjectId!.Value);
        Assert.Throws<SereinFlowAccessException>(() =>
        {
            var newGuidResult1 = Guid.NewGuid();
            security.Require(principal, SereinFlowPermission.ProjectRead, newGuidResult1);
        });
        Assert.Throws<SereinFlowAccessException>(() =>
        {
            security.Require(principal, SereinFlowPermission.SensitiveRead, created.Entry.ProjectId.Value);
        });
    }

    [Fact]
    public async Task SuccessfulAuthenticationWaitsForLastUsedTracking()
    {
        var newGuidResult3 = Guid.NewGuid();
        var createMcpApiKeyRequestDto4 = new CreateApiKeyRequest(newGuidResult3, "tracked-key", [SereinFlowPermission.ProjectRead]);
        var created = ApiKeyService.CreateKeyWithEntry(createMcpApiKeyRequestDto4);
        var store = new InMemoryApiKeyStore(created.Entry);
        var security = new ApiKeyService(store);
        var principal = await security.AuthenticateAsync(created.Dto.Secret);
        Assert.NotNull(principal);
        var listAsyncResult = await store.ListAsync();
        var evaluatedValue = Assert.Single(listAsyncResult).LastUsedAt;
        Assert.NotNull(evaluatedValue);
    }

    [Fact]
    public async Task AuthenticationTelemetryPreservesRevocationAfterTheKeySnapshotWasRead()
    {
        var argument2 = Guid.NewGuid();
        var argument1 = new CreateApiKeyRequest(argument2, "concurrently-revoked", [SereinFlowPermission.ProjectRead]);
        var created = ApiKeyService.CreateKeyWithEntry(argument1);
        var store = new InMemoryApiKeyStore(created.Entry);
        var revokedAt = DateTimeOffset.UtcNow;
        store.AfterSnapshotRead = () =>
        {
            store.AfterSnapshotRead = null;
            {
                var result4 = store.UpdateAsync(created.Entry with { RevokedAt = revokedAt });
                return result4;
            }
        };
        var security = new ApiKeyService(store);
        await security.AuthenticateAsync(created.Dto.Secret);
        var persisted = await store.FindAsync(created.Entry.Id);
        Assert.NotNull(persisted);
        Assert.Equal(revokedAt, persisted.RevokedAt);
        Assert.NotNull(persisted.LastUsedAt);
        {
            var actual5 = await security.AuthenticateAsync(created.Dto.Secret);
            Assert.Null(actual5);
        }
    }

    [Fact]
    public async Task ExpiredAndRevokedKeysCannotAuthenticate()
    {
        var newGuidResult5 = Guid.NewGuid();
        var createMcpApiKeyRequestDto6 = new CreateApiKeyRequest(newGuidResult5, "expired", []);
        var expiredCreated = ApiKeyService.CreateKeyWithEntry(createMcpApiKeyRequestDto6);
        var expired = expiredCreated.Entry with
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        var newGuidResult7 = Guid.NewGuid();
        var createMcpApiKeyRequestDto8 = new CreateApiKeyRequest(newGuidResult7, "revoked", []);
        var revokedCreated = ApiKeyService.CreateKeyWithEntry(createMcpApiKeyRequestDto8);
        var revoked = revokedCreated.Entry with
        {
            RevokedAt = DateTimeOffset.UtcNow
        };
        var inMemoryApiKeyStore9 = new InMemoryApiKeyStore(expired, revoked);
        var security = new ApiKeyService(inMemoryApiKeyStore9);
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
            SereinFlowPermission.FlowWrite
        }.ToHashSet();
        var principal = ApiKeyActors.Create("preview-caller", newGuidResult11, toHashSetResult);
        var newGuidResult12 = Guid.NewGuid();
        var argument3 = SecurityTestSupport.ProjectId(principal);
        var preview = await service.CreateAsync("flow.patch", principal, argument3, newGuidResult12, 1, new { value = "candidate" });
        var evaluatedValue13 = preview with
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        await store.UpdateAsync(evaluatedValue13);
        var exception = await Assert.ThrowsAsync<SereinFlowAccessException>(() =>
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
            var createMcpApiKeyRequestDto15 = new CreateApiKeyRequest(null, "ordinary", []);
            var createKeyWithEntryResult = ApiKeyService.CreateKeyWithEntry(createMcpApiKeyRequestDto15);
            return createKeyWithEntryResult;
        });
        Assert.Throws<ArgumentException>(() =>
        {
            var newGuidResult16 = Guid.NewGuid();
            var createMcpApiKeyRequestDto17 = new CreateApiKeyRequest(newGuidResult16, "admin", [], IsAdministrator: true);
            var createKeyWithEntryResult18 = ApiKeyService.CreateKeyWithEntry(createMcpApiKeyRequestDto17);
            return createKeyWithEntryResult18;
        });
        Assert.Throws<ArgumentException>(() =>
        {
            var newGuidResult19 = Guid.NewGuid();
            var addSecondsResult = DateTimeOffset.UtcNow.AddSeconds(-1);
            var createMcpApiKeyRequestDto20 = new CreateApiKeyRequest(newGuidResult19, "expired", [], addSecondsResult);
            var createKeyWithEntryResult21 = ApiKeyService.CreateKeyWithEntry(createMcpApiKeyRequestDto20);
            return createKeyWithEntryResult21;
        });
    }

    [Fact]
    public void PermissionWireNamesUseDottedContractWithoutPublicLegacyAliases()
    {
        var options = SereinJsonSerialization.CreateWebOptions();
        var json = JsonSerializer.Serialize(SereinFlowPermission.FlowWrite, options);
        Assert.Equal("\"flow.write\"", json);
        var tryParseResult = SereinFlowOperationNames.TryParse("project.read", out var dotted);
        Assert.True(tryParseResult);
        Assert.Equal(SereinFlowPermission.ProjectRead, dotted);
        var legacyAccepted = SereinFlowOperationNames.TryParse("FlowWrite", out _);
        Assert.False(legacyAccepted);
        Assert.Throws<JsonException>(() =>
        {
            JsonSerializer.Deserialize<SereinFlowPermission>("\"libraryImport\"", options);
        });
        Assert.Throws<JsonException>(() =>
        {
            JsonSerializer.Deserialize<SereinFlowPermission>("0", options);
        });
    }

    [Fact]
    public async Task IdempotencyRejectsRequestFingerprintReuse()
    {
        var store = new InMemoryIdempotencyStore();
        var service = new McpIdempotencyService(store);
        await service.SaveAsync("principal", "flow.patch", "request-1", new { accepted = true }, "{\"value\":1}");
        var findAsyncResult23 = await service.FindAsync("principal", "flow.patch", "request-1", "{\"value\":1}");
        Assert.NotNull(findAsyncResult23);
        var exception = await Assert.ThrowsAsync<SereinFlowAccessException>(() =>
        {
            var findAsyncResult = service.FindAsync("principal", "flow.patch", "request-1", "{\"value\":2}");
            return findAsyncResult;
        });
        Assert.Equal(McpErrorCodes.IdempotencyConflict, exception.Code);
    }

    private sealed class InMemoryApiKeyStore(params ApiKeyEntry[] entries) : IApiKeyStore
    {
        private readonly List<ApiKeyEntry> _entries = entries.ToList();
        public Func<Task>? AfterSnapshotRead { get; set; }

        public async Task<IReadOnlyList<ApiKeyEntry>> ListAsync(CancellationToken cancellationToken = default)
        {
            var snapshot = _entries.ToArray();
            if (AfterSnapshotRead is { } callback)
            {
                await callback();
            }

            return snapshot;
        }

        public Task<ApiKeyEntry?> FindAsync(string id, CancellationToken cancellationToken = default)
        {
            var firstOrDefaultResult = _entries.FirstOrDefault(item => item.Id == id);
            var fromResultResult24 = Task.FromResult(firstOrDefaultResult);
            return fromResultResult24;
        }

        public Task AddAsync(ApiKeyEntry entry, CancellationToken cancellationToken = default)
        {
            _entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<bool> UpdateAsync(ApiKeyEntry entry, CancellationToken cancellationToken = default)
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

        public Task<bool> UpdateLastUsedAsync(string id, DateTimeOffset lastUsedAt, CancellationToken cancellationToken = default)
        {
            var index = _entries.FindIndex(entry => entry.Id == id);
            if (index < 0)
            {
                var result6 = Task.FromResult(false);
                return result6;
            }

            _entries[index] = _entries[index] with
            {
                LastUsedAt = lastUsedAt
            };
            {
                var result7 = Task.FromResult(true);
                return result7;
            }
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

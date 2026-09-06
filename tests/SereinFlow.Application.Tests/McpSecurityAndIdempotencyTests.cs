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
        var security = new McpSecurityService(new InMemoryApiKeyStore(), "bootstrap-secret");

        var principal = await security.AuthenticateAsync("bootstrap-secret");

        Assert.NotNull(principal);
        Assert.True(principal.IsAdministrator);
        Assert.Null(principal.ProjectId);
        Assert.Contains(McpPermissionDto.RunMessagePublish, principal.Permissions);
        security.Require(principal, McpPermissionDto.SensitiveRead, Guid.NewGuid());
    }

    [Fact]
    public async Task ProjectKeyCannotCrossProjectOrReadSensitiveDataWithoutPermission()
    {
        var created = McpSecurityService.CreateKeyWithEntry(new CreateMcpApiKeyRequestDto(
            Guid.NewGuid(),
            "project-key",
            [McpPermissionDto.ProjectRead]));
        var store = new InMemoryApiKeyStore(created.Entry);
        var security = new McpSecurityService(store);
        var principal = await security.AuthenticateAsync(created.Dto.Secret);

        Assert.NotNull(principal);
        security.Require(principal, McpPermissionDto.ProjectRead, created.Entry.ProjectId!.Value);
        Assert.Throws<McpSecurityException>(() => security.Require(principal, McpPermissionDto.ProjectRead, Guid.NewGuid()));
        Assert.Throws<McpSecurityException>(() => security.Require(principal, McpPermissionDto.SensitiveRead, created.Entry.ProjectId.Value));
    }

    [Fact]
    public async Task SuccessfulAuthenticationWaitsForLastUsedTracking()
    {
        var created = McpSecurityService.CreateKeyWithEntry(new CreateMcpApiKeyRequestDto(
            Guid.NewGuid(),
            "tracked-key",
            [McpPermissionDto.ProjectRead]));
        var store = new InMemoryApiKeyStore(created.Entry);
        var security = new McpSecurityService(store);

        var principal = await security.AuthenticateAsync(created.Dto.Secret);

        Assert.NotNull(principal);
        Assert.NotNull(Assert.Single(await store.ListAsync()).LastUsedAt);
    }

    [Fact]
    public async Task ExpiredAndRevokedKeysCannotAuthenticate()
    {
        var expiredCreated = McpSecurityService.CreateKeyWithEntry(new CreateMcpApiKeyRequestDto(
            Guid.NewGuid(), "expired", []));
        var expired = expiredCreated.Entry with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) };
        var revokedCreated = McpSecurityService.CreateKeyWithEntry(new CreateMcpApiKeyRequestDto(
            Guid.NewGuid(), "revoked", []));
        var revoked = revokedCreated.Entry with { RevokedAt = DateTimeOffset.UtcNow };
        var security = new McpSecurityService(new InMemoryApiKeyStore(expired, revoked));

        Assert.Null(await security.AuthenticateAsync(expiredCreated.Dto.Secret));
        Assert.Null(await security.AuthenticateAsync(revokedCreated.Dto.Secret));
    }

    [Fact]
    public async Task ExpiredPreviewIsMarkedAndCannotBeApplied()
    {
        var store = new InMemoryPreviewStore();
        var service = new McpPreviewService(store);
        var principal = new McpPrincipal(
            "preview-caller",
            Guid.NewGuid(),
            new[] { McpPermissionDto.FlowWrite }.ToHashSet());
        var preview = await service.CreateAsync(
            "flow.patch",
            principal,
            principal.ProjectId,
            Guid.NewGuid(),
            1,
            new { value = "candidate" });
        await store.UpdateAsync(preview with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) });

        var exception = await Assert.ThrowsAsync<McpSecurityException>(() => service.RequireAsync(
            preview.Id,
            preview.PreviewFingerprint,
            principal));

        Assert.Equal(McpErrorCodes.PreviewExpired, exception.Code);
        Assert.Equal(McpMutationPreviewStatusDto.Expired, (await store.FindAsync(preview.Id))!.Status);
    }

    [Fact]
    public void KeyScopeRulesRejectInvalidRequests()
    {
        Assert.Throws<ArgumentException>(() => McpSecurityService.CreateKeyWithEntry(
            new CreateMcpApiKeyRequestDto(null, "ordinary", [])));
        Assert.Throws<ArgumentException>(() => McpSecurityService.CreateKeyWithEntry(
            new CreateMcpApiKeyRequestDto(Guid.NewGuid(), "admin", [], IsAdministrator: true)));
        Assert.Throws<ArgumentException>(() => McpSecurityService.CreateKeyWithEntry(
            new CreateMcpApiKeyRequestDto(Guid.NewGuid(), "expired", [], DateTimeOffset.UtcNow.AddSeconds(-1))));
    }

    [Fact]
    public void PermissionWireNamesUseDottedContractAndReadLegacyNames()
    {
        var options = SereinJsonSerialization.CreateWebOptions();
        var json = JsonSerializer.Serialize(McpPermissionDto.FlowWrite, options);

        Assert.Equal("\"flow.write\"", json);
        Assert.True(McpPermissionNames.TryParse("project.read", out var dotted));
        Assert.Equal(McpPermissionDto.ProjectRead, dotted);
        Assert.True(McpPermissionNames.TryParse("FlowWrite", out var legacy));
        Assert.Equal(McpPermissionDto.FlowWrite, legacy);
        Assert.Equal(
            McpPermissionDto.LibraryImport,
            JsonSerializer.Deserialize<McpPermissionDto>("\"libraryImport\"", options));
    }

    [Fact]
    public async Task IdempotencyRejectsRequestFingerprintReuse()
    {
        var store = new InMemoryIdempotencyStore();
        var service = new McpIdempotencyService(store);

        await service.SaveAsync("principal", "flow.patch", "request-1", new { accepted = true }, "{\"value\":1}");
        Assert.NotNull(await service.FindAsync("principal", "flow.patch", "request-1", "{\"value\":1}"));

        var exception = await Assert.ThrowsAsync<McpSecurityException>(() =>
            service.FindAsync("principal", "flow.patch", "request-1", "{\"value\":2}"));

        Assert.Equal(McpErrorCodes.IdempotencyConflict, exception.Code);
    }

    private sealed class InMemoryApiKeyStore(params McpApiKeyEntry[] entries) : IMcpApiKeyStore
    {
        private readonly List<McpApiKeyEntry> _entries = entries.ToList();

        public Task<IReadOnlyList<McpApiKeyEntry>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<McpApiKeyEntry>>(_entries.ToArray());

        public Task<McpApiKeyEntry?> FindAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(_entries.FirstOrDefault(item => item.Id == id));

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

    private sealed class InMemoryIdempotencyStore : IMcpIdempotencyStore
    {
        private readonly List<McpIdempotencyEntry> _entries = [];

        public Task<McpIdempotencyEntry?> FindAsync(string principalId, string operation, string keyHash, CancellationToken cancellationToken = default)
            => Task.FromResult(_entries.FirstOrDefault(item => item.PrincipalId == principalId && item.Operation == operation && item.KeyHash == keyHash));

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
            => Task.FromResult(_entries.FirstOrDefault(item => item.Id == id));

        public Task AddAsync(McpPreviewEntry entry, CancellationToken cancellationToken = default)
        {
            _entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<bool> UpdateAsync(McpPreviewEntry entry, CancellationToken cancellationToken = default)
        {
            var index = _entries.FindIndex(item => item.Id == entry.Id);
            if (index < 0)
                return Task.FromResult(false);
            _entries[index] = entry;
            return Task.FromResult(true);
        }
    }
}

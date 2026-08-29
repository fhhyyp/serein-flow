using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Infrastructure.Persistence;
using SereinFlow.McpServer;

namespace SereinFlow.McpServer.Tests;

public sealed class McpBackendIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] ProjectReadPermissionNames = ["project.read"];

    [Fact]
    public async Task ApiKeyLifecycleIsIdempotentAndReturnsSecretsOnlyOnce()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        accessor.Current = new McpPrincipal(
            "key-admin",
            null,
            Enum.GetValues<McpPermissionDto>().ToHashSet(),
            IsAdministrator: true);

        using var dataScope = host.Services.CreateScope();
        var project = Project.Create("MCP key project");
        await dataScope.ServiceProvider.GetRequiredService<IProjectRepository>().AddAsync(project);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();

        var createArguments = JsonSerializer.SerializeToElement(new
        {
            projectId = project.Id,
            name = "project-key",
            permissions = ProjectReadPermissionNames,
            idempotencyKey = "create-key-once",
        });
        var created = Deserialize<CreatedMcpApiKeyDto>(
            (await backend.CallToolAsync("sereinflow_create_mcp_api_key", createArguments, CancellationToken.None)).Value);
        Assert.NotNull(created.Secret);

        var replayDocument = JsonDocument.Parse(JsonSerializer.Serialize(
            (await backend.CallToolAsync("sereinflow_create_mcp_api_key", createArguments, CancellationToken.None)).Value,
            JsonOptions));
        Assert.Null(replayDocument.RootElement.GetProperty("secret").GetString());
        Assert.True(replayDocument.RootElement.GetProperty("replayed").GetBoolean());

        var rotateArguments = JsonSerializer.SerializeToElement(new
        {
            keyId = created.Key.Id,
            idempotencyKey = "rotate-key-once",
        });
        var rotated = Deserialize<RotatedMcpApiKeyDto>(
            (await backend.CallToolAsync("sereinflow_rotate_mcp_api_key", rotateArguments, CancellationToken.None)).Value);
        Assert.Equal(created.Key.Id, rotated.RevokedKeyId);
        Assert.NotNull(rotated.Secret);

        var security = dataScope.ServiceProvider.GetRequiredService<McpSecurityService>();
        Assert.Null(await security.AuthenticateAsync(created.Secret));
        Assert.NotNull(await security.AuthenticateAsync(rotated.Secret));

        var rotatedReplay = Deserialize<RotatedMcpApiKeyDto>(
            (await backend.CallToolAsync("sereinflow_rotate_mcp_api_key", rotateArguments, CancellationToken.None)).Value);
        Assert.True(rotatedReplay.Replayed);
        Assert.Null(rotatedReplay.Secret);

        var revokeArguments = JsonSerializer.SerializeToElement(new
        {
            keyId = rotated.Key.Id,
            idempotencyKey = "revoke-key-once",
        });
        await backend.CallToolAsync("sereinflow_revoke_mcp_api_key", revokeArguments, CancellationToken.None);
        Assert.Null(await security.AuthenticateAsync(rotated.Secret));

        var auditRecords = await dataScope.ServiceProvider
            .GetRequiredService<IRepository<McpAuditRecord>>()
            .ListAsync();
        Assert.DoesNotContain(auditRecords, record =>
            record.Summary?.Contains(created.Secret, StringComparison.Ordinal) == true
            || record.Summary?.Contains(rotated.Secret, StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task PublishPreviewApplyAndReplayUseTheSameProductionGuard()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        accessor.Current = new McpPrincipal(
            "integration-admin",
            null,
            Enum.GetValues<McpPermissionDto>().ToHashSet(),
            IsAdministrator: true);

        using var dataScope = host.Services.CreateScope();
        var project = Project.Create("MCP publish project");
        var projectRepository = dataScope.ServiceProvider.GetRequiredService<IProjectRepository>();
        await projectRepository.AddAsync(project);
        var flow = CreateFlow();
        await dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>().AddAsync(project.Id, flow);

        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var preview = await backend.CallToolAsync(
            "sereinflow_preview_publish_flow",
            JsonSerializer.SerializeToElement(new
            {
                projectId = project.Id,
                flowId = flow.Id,
                expectedDevelopmentVersion = flow.Version,
            }),
            CancellationToken.None);
        var previewDocument = JsonDocument.Parse(JsonSerializer.Serialize(preview.Value, JsonOptions));
        var previewId = previewDocument.RootElement.GetProperty("previewId").GetGuid();
        var fingerprint = previewDocument.RootElement.GetProperty("previewFingerprint").GetString()!;

        var published = await backend.CallToolAsync(
            "sereinflow_apply_publish_flow",
            JsonSerializer.SerializeToElement(new
            {
                previewId,
                previewFingerprint = fingerprint,
                confirmation = "APPLY",
                idempotencyKey = "publish-once",
            }),
            CancellationToken.None);
        Assert.NotNull(published.Value);

        var replay = await backend.CallToolAsync(
            "sereinflow_apply_publish_flow",
            JsonSerializer.SerializeToElement(new
            {
                previewId,
                previewFingerprint = fingerprint,
                confirmation = "APPLY",
                idempotencyKey = "publish-once",
            }),
            CancellationToken.None);
        using var publishedDocument = JsonDocument.Parse(JsonSerializer.Serialize(published.Value, JsonOptions));
        using var replayDocument = JsonDocument.Parse(JsonSerializer.Serialize(replay.Value, JsonOptions));
        Assert.True(JsonElement.DeepEquals(publishedDocument.RootElement, replayDocument.RootElement));

        var auditRecords = await dataScope.ServiceProvider
            .GetRequiredService<IRepository<McpAuditRecord>>()
            .ListAsync(item => item.Operation == "sereinflow_apply_publish_flow");
        Assert.NotEmpty(auditRecords);
        Assert.All(auditRecords, record =>
            Assert.Equal(McpIdempotencyService.HashKey("publish-once"), record.RequestHash));
    }

    [Fact]
    public async Task PublishPreviewRejectsWhenProductionHeadChangesBeforeApply()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        accessor.Current = new McpPrincipal(
            "integration-admin",
            null,
            Enum.GetValues<McpPermissionDto>().ToHashSet(),
            IsAdministrator: true);

        using var dataScope = host.Services.CreateScope();
        var project = Project.Create("MCP conflict project");
        await dataScope.ServiceProvider.GetRequiredService<IProjectRepository>().AddAsync(project);
        var flow = CreateFlow();
        var flows = dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        await flows.AddAsync(project.Id, flow);

        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var preview = await backend.CallToolAsync(
            "sereinflow_preview_publish_flow",
            JsonSerializer.SerializeToElement(new
            {
                projectId = project.Id,
                flowId = flow.Id,
                expectedDevelopmentVersion = flow.Version,
            }),
            CancellationToken.None);
        var previewDocument = JsonDocument.Parse(JsonSerializer.Serialize(preview.Value, JsonOptions));

        var versions = dataScope.ServiceProvider.GetRequiredService<IFlowVersionRepository>();
        var directPublish = await versions.PublishAsync(
            project.Id,
            flow.Id,
            flow.Version,
            remark: "direct publish",
            expectedProductionVersion: null);
        Assert.True(directPublish.IsCommitted);

        var exception = await Assert.ThrowsAsync<McpProtocolException>(() => backend.CallToolAsync(
            "sereinflow_apply_publish_flow",
            JsonSerializer.SerializeToElement(new
            {
                previewId = previewDocument.RootElement.GetProperty("previewId").GetGuid(),
                previewFingerprint = previewDocument.RootElement.GetProperty("previewFingerprint").GetString(),
                confirmation = "APPLY",
                idempotencyKey = "stale-publish",
            }),
            CancellationToken.None));

        Assert.Equal(-32010, exception.Code);
        Assert.Equal(directPublish.Version!.Version, await versions.FindProductionVersionAsync(project.Id, flow.Id));
    }

    [Fact]
    public async Task RollbackToolsKeepDevelopmentAndProductionTracksIndependent()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        accessor.Current = new McpPrincipal(
            "rollback-admin",
            null,
            Enum.GetValues<McpPermissionDto>().ToHashSet(),
            IsAdministrator: true);

        using var dataScope = host.Services.CreateScope();
        var project = Project.Create("MCP rollback project");
        await dataScope.ServiceProvider.GetRequiredService<IProjectRepository>().AddAsync(project);
        var flow = CreateFlow();
        var flows = dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        await flows.AddAsync(project.Id, flow);
        var versions = dataScope.ServiceProvider.GetRequiredService<IFlowVersionRepository>();
        var firstDevelopmentCandidate = flow with
        {
            Ui = new FlowUiMetadataDto(new FlowConnectionLineTypesDto("orthogonal", "straight")),
        };
        var saved = await flows.TryUpdateAsync(
            project.Id,
            firstDevelopmentCandidate,
            flow.Version);
        Assert.NotNull(saved);
        var published = await versions.PublishAsync(project.Id, flow.Id, saved!.Version, "publish rollback test");
        Assert.True(published.IsCommitted);
        var secondDevelopmentCandidate = saved with
        {
            Ui = new FlowUiMetadataDto(new FlowConnectionLineTypesDto("bezier", "straight")),
        };
        var savedAgain = await flows.TryUpdateAsync(project.Id, secondDevelopmentCandidate, saved.Version);
        Assert.NotNull(savedAgain);
        var publishedAgain = await versions.PublishAsync(
            project.Id,
            flow.Id,
            savedAgain!.Version,
            "publish rollback head",
            expectedProductionVersion: published.Version!.Version);
        Assert.True(publishedAgain.IsCommitted);
        var developmentVersionBeforeRollback = (await flows.FindAsync(project.Id, flow.Id))!.Version;
        var productionVersionBeforeRollback = await versions.FindProductionVersionAsync(project.Id, flow.Id);

        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var preview = await backend.CallToolAsync(
            "sereinflow_preview_rollback_flow",
            JsonSerializer.SerializeToElement(new
            {
                projectId = project.Id,
                flowId = flow.Id,
                track = "production",
                sourceVersion = published.Version!.Version,
                expectedHeadVersion = publishedAgain.Version!.Version,
            }),
            CancellationToken.None);
        var previewDocument = JsonDocument.Parse(JsonSerializer.Serialize(preview.Value, JsonOptions));
        var apply = await backend.CallToolAsync(
            "sereinflow_apply_rollback_flow",
            JsonSerializer.SerializeToElement(new
            {
                previewId = previewDocument.RootElement.GetProperty("previewId").GetGuid(),
                previewFingerprint = previewDocument.RootElement.GetProperty("previewFingerprint").GetString(),
                confirmation = "APPLY",
                idempotencyKey = "production-rollback-once",
            }),
            CancellationToken.None);

        Assert.NotNull(apply.Value);
        Assert.Equal(developmentVersionBeforeRollback, (await flows.FindAsync(project.Id, flow.Id))!.Version);
        Assert.NotEqual(productionVersionBeforeRollback, await versions.FindProductionVersionAsync(project.Id, flow.Id));
        Assert.Equal(
            FlowVersionTrackDto.Production,
            (await versions.ListVersionsAsync(project.Id, flow.Id, FlowVersionTrackDto.Production)).Single(item => item.IsCurrent).Track);
    }

    [Fact]
    public async Task ProjectCallerCannotReadAnotherCallersPreviewInTheSameProject()
    {
        using var host = CreateHost();
        using var dataScope = host.Services.CreateScope();
        var project = Project.Create("MCP preview ownership project");
        await dataScope.ServiceProvider.GetRequiredService<IProjectRepository>().AddAsync(project);
        var flow = CreateFlow();
        await dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>().AddAsync(project.Id, flow);

        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        accessor.Current = new McpPrincipal(
            "owner-key",
            project.Id,
            new[] { McpPermissionDto.FlowWrite, McpPermissionDto.ProjectRead }.ToHashSet());
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var preview = await backend.CallToolAsync(
            "sereinflow_preview_flow_patch",
            JsonSerializer.SerializeToElement(new
            {
                projectId = project.Id,
                flowId = flow.Id,
                expectedDevelopmentVersion = flow.Version,
                operations = Array.Empty<object>(),
            }),
            CancellationToken.None);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(preview.Value, JsonOptions));
        var previewId = document.RootElement.GetProperty("previewId").GetGuid();

        accessor.Current = new McpPrincipal(
            "other-key",
            project.Id,
            new[] { McpPermissionDto.ProjectRead }.ToHashSet());
        var exception = await Assert.ThrowsAsync<McpSecurityException>(() => backend.ReadResourceAsync(
            $"sereinflow://mcp-previews/{previewId:D}",
            CancellationToken.None));

        Assert.Equal("mcp.preview_owner_mismatch", exception.Code);
    }

    private static TestHost CreateHost()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sereinflow-mcp-test-{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SereinFlow:DatabasePath"] = Path.Combine(root, "sereinflow.db"),
                ["SereinFlow:LibraryDirectory"] = Path.Combine(root, "libraries"),
                ["SereinFlow:Mcp:PackageStagingDirectory"] = Path.Combine(root, "staging"),
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSereinFlowInfrastructure(configuration, root);
        services.AddScoped<AiReadModelService>();
        services.AddScoped<ProjectLibraryService>();
        services.AddScoped<FlowDefinitionWriteService>();
        services.AddScoped<FlowDiffService>();
        services.AddScoped<FlowPatchService>();
        services.AddSingleton<IBuiltinNodeCatalog, BuiltinNodeCatalog>();
        services.AddScoped<McpPreviewService>();
        services.AddScoped<McpIdempotencyService>();
        services.AddScoped<McpSecurityService>();
        services.AddSingleton<IMcpPrincipalAccessor, McpPrincipalAccessor>();
        services.AddSingleton<ISereinFlowMcpBackend, SereinFlowMcpBackend>();
        services.AddSingleton<SereinFlowMcpBackend>(provider =>
            (SereinFlowMcpBackend)provider.GetRequiredService<ISereinFlowMcpBackend>());
        return new TestHost(
            services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true }),
            root);
    }

    private static FlowDefinitionDto CreateFlow()
    {
        var flowId = Guid.NewGuid();
        var source = "return 0";
        var nodeId = "script-node";
        var node = new NodeDto(
            nodeId,
            NodeTypeDto.Script,
            "Script",
            0,
            0,
            [],
            [],
            new ScriptNodeDataDto(
                nodeId,
                source,
                "0.1",
                SereinFlow.Domain.ScriptNodeDefinition.ComputeSourceHash(source),
                [],
                [new ScriptValueContractDto("result", "ScriptLang.Runtime.Value", false, "result")]));
        return new FlowDefinitionDto(
            flowId,
            FlowDefinition.CurrentSchemaVersion,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [])],
            nodeId,
            "initial",
            RunPolicy: new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel));
    }

    private static T Deserialize<T>(object? value)
        => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)
            ?? throw new InvalidOperationException("The integration response could not be deserialized.");

    private sealed class TestHost(ServiceProvider provider, string root) : IDisposable
    {
        public IServiceProvider Services => provider;

        public void Dispose()
        {
            provider.Dispose();
            if (Directory.Exists(root))
            {
                try
                {
                    Directory.Delete(root, recursive: true);
                }
                catch (IOException)
                {
                    // SQLite can release a platform file handle just after the
                    // provider has disposed its client. Test cleanup is best effort.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}

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
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateContractOptions();

    [Fact]
    public async Task FlowPatchToolPublishesTypedOperationSchema()
    {
        using var host = CreateHost();
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();

        var tool = (await backend.ListToolsAsync(CancellationToken.None))
            .Single(item => item.Name == "sereinflow_preview_flow_patch");
        var operations = tool.InputSchema.GetProperty("properties").GetProperty("operations");
        var alternatives = operations.GetProperty("items").GetProperty("oneOf");

        Assert.Equal(26, alternatives.GetArrayLength());
        Assert.Contains(
            alternatives.EnumerateArray(),
            item => item.GetProperty("properties").TryGetProperty("op", out var op)
                && op.GetProperty("enum")[0].GetString() == "setRunPolicy");
        var runPolicy = alternatives.EnumerateArray()
            .Single(item => item.GetProperty("properties").TryGetProperty("op", out var op)
                && op.GetProperty("enum")[0].GetString() == "setRunPolicy");
        Assert.Equal("string", runPolicy.GetProperty("properties").GetProperty("runPolicy").GetProperty("properties").GetProperty("concurrencyMode").GetProperty("oneOf")[0].GetProperty("type").GetString());
        Assert.False(runPolicy.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public async Task FlowPatchV2NormalizesLegacySnakeCaseAndNumericEnums()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        accessor.Current = new McpPrincipal(
            "integration-admin",
            null,
            Enum.GetValues<McpPermissionDto>().ToHashSet(),
            IsAdministrator: true);

        using var dataScope = host.Services.CreateScope();
        var project = Project.Create("MCP canonical patch project");
        await dataScope.ServiceProvider.GetRequiredService<IProjectRepository>().AddAsync(project);
        var flow = CreateFlow();
        await dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>().AddAsync(project.Id, flow);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();

        var canonical = await backend.CallToolAsync(
            "sereinflow_preview_flow_patch",
            JsonSerializer.SerializeToElement(new
            {
                projectId = project.Id,
                flowId = flow.Id,
                expectedDevelopmentVersion = flow.Version,
                schemaVersion = "2.0",
                operations = new[]
                {
                    new
                    {
                        op = "setRunPolicy",
                        runPolicy = new { concurrencyMode = "exclusiveReject" },
                    },
                },
            }),
            CancellationToken.None);
        var legacy = await backend.CallToolAsync(
            "sereinflow_preview_flow_patch",
            JsonSerializer.SerializeToElement(new
            {
                projectId = project.Id,
                flowId = flow.Id,
                expectedDevelopmentVersion = flow.Version,
                schemaVersion = "1.0",
                operations = new[]
                {
                    new
                    {
                        operation = "set_run_policy",
                        value = new { concurrencyMode = 1 },
                    },
                },
            }),
            CancellationToken.None);

        using var canonicalDocument = JsonDocument.Parse(JsonSerializer.Serialize(canonical.Value, JsonOptions));
        using var legacyDocument = JsonDocument.Parse(JsonSerializer.Serialize(legacy.Value, JsonOptions));
        var canonicalRoot = canonicalDocument.RootElement;
        var legacyRoot = legacyDocument.RootElement;
        Assert.Equal("2.0", canonicalRoot.GetProperty("schemaVersion").GetString());
        Assert.Equal("camelCase", canonicalRoot.GetProperty("enumEncoding").GetString());
        Assert.Equal("setRunPolicy", canonicalRoot.GetProperty("normalizedOperations")[0].GetProperty("op").GetString());
        Assert.Equal("exclusiveReject", canonicalRoot.GetProperty("normalizedOperations")[0].GetProperty("runPolicy").GetProperty("concurrencyMode").GetString());
        Assert.Equal(
            canonicalRoot.GetProperty("diff").GetProperty("candidateChecksum").GetString(),
            legacyRoot.GetProperty("diff").GetProperty("candidateChecksum").GetString());
        Assert.True(legacyRoot.GetProperty("normalizationWarnings").GetArrayLength() > 0);
    }

    [Fact]
    public async Task FlowPatchV2RejectsUnknownFieldsWithStructuredDiagnostic()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        accessor.Current = new McpPrincipal(
            "integration-admin",
            null,
            Enum.GetValues<McpPermissionDto>().ToHashSet(),
            IsAdministrator: true);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();

        var exception = await Assert.ThrowsAsync<McpProtocolException>(() => backend.CallToolAsync(
            "sereinflow_preview_flow_patch",
            JsonSerializer.SerializeToElement(new
            {
                projectId = Guid.NewGuid(),
                flowId = Guid.NewGuid(),
                expectedDevelopmentVersion = 1,
                schemaVersion = "2.0",
                operations = new[]
                {
                    new
                    {
                        op = "setRunPolicy",
                        runPolicy = new { concurrencyMode = "exclusiveReject" },
                        ignored = true,
                    },
                },
            }),
            CancellationToken.None));

        Assert.Equal(-32602, exception.Code);
        var diagnostic = JsonSerializer.SerializeToElement(exception.ErrorData, JsonOptions);
        Assert.Equal("mcp.flow_patch.unexpected_field", diagnostic.GetProperty("code").GetString());
        Assert.Equal("$.operations[0].ignored", diagnostic.GetProperty("fieldPath").GetString());
        Assert.Equal("2.0", diagnostic.GetProperty("schemaVersion").GetString());
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.GetProperty("diagnosticId").GetString()));
    }

    [Fact]
    public async Task LibraryNodeTemplateToolPublishesAReadOnlyTypedSchema()
    {
        using var host = CreateHost();
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var tool = (await backend.ListToolsAsync(CancellationToken.None))
            .Single(item => item.Name == "sereinflow_create_library_node_template");
        var properties = tool.InputSchema.GetProperty("properties");
        Assert.Equal("object", properties.GetProperty("position").GetProperty("type").GetString());
        Assert.Equal("number", properties.GetProperty("position").GetProperty("properties").GetProperty("x").GetProperty("type").GetString());
        Assert.Contains("projectId", tool.InputSchema.GetProperty("required").EnumerateArray().Select(static item => item.GetString()));
    }

    [Fact]
    public async Task FlowPatchAcceptsStringEnumsAndApplyReadsBackAuthoritativeState()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        accessor.Current = new McpPrincipal(
            "integration-admin",
            null,
            Enum.GetValues<McpPermissionDto>().ToHashSet(),
            IsAdministrator: true);

        using var dataScope = host.Services.CreateScope();
        var project = Project.Create("MCP enum patch project");
        await dataScope.ServiceProvider.GetRequiredService<IProjectRepository>().AddAsync(project);
        var flow = CreateFlow();
        await dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>().AddAsync(project.Id, flow);

        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var previewResult = await backend.CallToolAsync(
            "sereinflow_preview_flow_patch",
            JsonSerializer.SerializeToElement(new
            {
                projectId = project.Id,
                flowId = flow.Id,
                expectedDevelopmentVersion = flow.Version,
                operations = new[]
                {
                    new
                    {
                        operation = "setRunPolicy",
                        value = new { concurrencyMode = "exclusiveReject" },
                    },
                },
            }),
            CancellationToken.None);
        using var previewDocument = JsonDocument.Parse(JsonSerializer.Serialize(previewResult.Value, JsonOptions));
        var previewId = previewDocument.RootElement.GetProperty("previewId").GetGuid();
        var fingerprint = previewDocument.RootElement.GetProperty("previewFingerprint").GetString()!;

        await backend.CallToolAsync(
            "sereinflow_apply_flow_patch",
            JsonSerializer.SerializeToElement(new
            {
                previewId,
                previewFingerprint = fingerprint,
                confirmation = "APPLY",
                idempotencyKey = "enum-patch-once",
            }),
            CancellationToken.None);

        var persisted = await dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>()
            .FindAsync(project.Id, flow.Id);
        Assert.NotNull(persisted);
        Assert.Equal(2, persisted!.Version);
        Assert.Equal(FlowConcurrencyModeDto.ExclusiveReject, persisted.RunPolicy!.ConcurrencyMode);
        Assert.Equal(FlowDiffService.GetChecksum(persisted), persisted.Checksum);
    }

    [Fact]
    public async Task MalformedContractArgumentsReturnInvalidParamsInsteadOfInternalError()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        accessor.Current = new McpPrincipal(
            "integration-admin",
            null,
            Enum.GetValues<McpPermissionDto>().ToHashSet(),
            IsAdministrator: true);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();

        var exception = await Assert.ThrowsAsync<McpProtocolException>(() => backend.CallToolAsync(
            "sereinflow_preview_flow_patch",
            JsonSerializer.SerializeToElement(new
            {
                projectId = Guid.NewGuid(),
                flowId = Guid.NewGuid(),
                expectedDevelopmentVersion = "one",
                operations = Array.Empty<object>(),
            }),
            CancellationToken.None));

        Assert.Equal(-32602, exception.Code);
        Assert.Equal("mcp.flow_patch.field_invalid", ((JsonElement)JsonSerializer.SerializeToElement(exception.ErrorData, JsonOptions)).GetProperty("code").GetString());
    }
    private static readonly string[] ProjectReadPermissionNames = ["project.read"];

    [Fact]
    public async Task ProjectCreatePreviewApplyCreatesEmptyDraftProjectAndIsIdempotent()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        accessor.Current = new McpPrincipal(
            "project-admin",
            null,
            Enum.GetValues<McpPermissionDto>().ToHashSet(),
            IsAdministrator: true);

        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var preview = Deserialize<ProjectCreatePreviewDto>(
            (await backend.CallToolAsync(
                "sereinflow_preview_create_project",
                JsonSerializer.SerializeToElement(new { name = "OpenCV demo", flowName = "Main flow" }),
                CancellationToken.None)).Value);

        Assert.True(preview.CanApply);
        Assert.True(preview.Validation.IsValid);
        Assert.Equal("draft", preview.Workspace.Project.Status);
        Assert.Equal("Main flow", preview.FlowName);
        Assert.Equal(preview.ProjectId, preview.Workspace.Project.Id);
        Assert.Equal(preview.FlowId, preview.Workspace.Flows.Single().Id);

        var previewResource = Deserialize<ProjectCreatePreviewDto>(
            (await backend.ReadResourceAsync(
                $"sereinflow://mcp-previews/{preview.PreviewId:D}",
                CancellationToken.None)).Value);
        Assert.Equal(preview.PreviewFingerprint, previewResource.PreviewFingerprint);
        Assert.Equal(preview.ProjectId, previewResource.ProjectId);

        using (var dataScope = host.Services.CreateScope())
        {
            Assert.Null(await dataScope.ServiceProvider.GetRequiredService<IProjectRepository>()
                .FindAsync(preview.ProjectId));
        }

        var applyArguments = JsonSerializer.SerializeToElement(new
        {
            previewId = preview.PreviewId,
            previewFingerprint = preview.PreviewFingerprint,
            confirmation = "APPLY",
            idempotencyKey = "create-project-once",
        });
        var created = Deserialize<ProjectWorkspaceDto>(
            (await backend.CallToolAsync("sereinflow_apply_create_project", applyArguments, CancellationToken.None)).Value);

        Assert.Equal(preview.ProjectId, created.Project.Id);
        Assert.Equal("OpenCV demo", created.Project.Name);
        Assert.Equal(preview.FlowId, created.Flows.Single().Id);
        Assert.Equal(0, created.Flows.Single().NodeCount);
        Assert.Equal(string.Empty, created.Flows.Single().EntryNodeId);

        var replay = Deserialize<ProjectWorkspaceDto>(
            (await backend.CallToolAsync("sereinflow_apply_create_project", applyArguments, CancellationToken.None)).Value);
        Assert.True(JsonElement.DeepEquals(
            JsonSerializer.SerializeToElement(created, JsonOptions),
            JsonSerializer.SerializeToElement(replay, JsonOptions)));

        using var verifyScope = host.Services.CreateScope();
        var project = await verifyScope.ServiceProvider.GetRequiredService<IProjectRepository>()
            .FindAsync(preview.ProjectId);
        var flow = await verifyScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>()
            .FindAsync(preview.ProjectId, preview.FlowId);
        Assert.NotNull(project);
        Assert.NotNull(flow);
        Assert.Equal(1, flow!.Version);
    }

    [Fact]
    public async Task ProjectCreateRequiresAnAdministratorEvenWhenProjectWriteIsGranted()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        accessor.Current = new McpPrincipal(
            "project-scoped-key",
            Guid.NewGuid(),
            new[] { McpPermissionDto.ProjectWrite }.ToHashSet());

        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var exception = await Assert.ThrowsAsync<McpProtocolException>(() => backend.CallToolAsync(
            "sereinflow_preview_create_project",
            JsonSerializer.SerializeToElement(new { name = "Not allowed" }),
            CancellationToken.None));

        Assert.Equal(-32003, exception.Code);
    }

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
        services.AddScoped<ProjectCreationService>();
        services.AddScoped<FlowDefinitionWriteService>();
        services.AddScoped<FlowDiffService>();
        services.AddScoped<FlowPatchService>();
        services.AddScoped<FlowPatchContractNormalizer>();
        services.AddScoped<LibraryNodeTemplateService>();
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

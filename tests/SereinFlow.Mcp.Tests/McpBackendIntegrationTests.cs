using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Infrastructure.Persistence;
using SereinFlow.Mcp;

namespace SereinFlow.Mcp.Tests;

public sealed class McpBackendIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateContractOptions();

    [Fact]
    public async Task ToolListIsServedFromTheRegisteredCatalog()
    {
        using var host = CreateHost();
        var catalog = host.Services.GetRequiredService<McpToolCatalog>();
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();

        var tools = await backend.ListToolsAsync(CancellationToken.None);

        Assert.Equal(
            catalog.Descriptors.Select(static item => item.Name),
            tools.Select(static item => item.Name));
        Assert.True(catalog.TryGet("sereinflow_preview_flow_patch", out var flowPatch));
        Assert.NotNull(flowPatch);
    }

    [Fact]
    public async Task CatalogToolsAndResourcesSeparateActiveAndArchivedRecords()
    {
        var availableLibrary = CreateLibrary("available-library", LibraryLifecycleDto.Available);
        var archivedLibrary = CreateLibrary("archived-library", LibraryLifecycleDto.Archived);
        using var host = CreateHost(services => services.AddSingleton<ILibraryCatalogService>(
            new TestLibraryCatalog(availableLibrary, archivedLibrary)));
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        accessor.Current = new McpPrincipal(
            "catalog-admin",
            null,
            Enum.GetValues<McpPermissionDto>().ToHashSet(),
            IsAdministrator: true);

        var activeProject = Project.Create("Active MCP project");
        var archivedProject = Project.Create("Archived MCP project");
        archivedProject.Archive();
        using (var dataScope = host.Services.CreateScope())
        {
            var projects = dataScope.ServiceProvider.GetRequiredService<IProjectRepository>();
            await projects.AddAsync(activeProject);
            await projects.AddAsync(archivedProject);
        }

        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var toolNames = (await backend.ListToolsAsync(CancellationToken.None))
            .Select(static tool => tool.Name)
            .ToArray();
        var resourceUris = (await backend.ListResourcesAsync(CancellationToken.None))
            .Select(static resource => resource.Uri)
            .ToArray();
        var resourceTemplates = (await backend.ListResourceTemplatesAsync(CancellationToken.None))
            .Select(static resource => resource.UriTemplate)
            .ToArray();

        Assert.Contains("sereinflow_list_archived_projects", toolNames);
        Assert.Contains("sereinflow_list_archived_libraries", toolNames);
        Assert.Contains("sereinflow_list_library_families", toolNames);
        Assert.Contains("sereinflow_get_library_family", toolNames);
        Assert.Contains("sereinflow_get_project_libraries", toolNames);
        Assert.Contains("sereinflow_get_library_upgrade", toolNames);
        Assert.Contains("sereinflow_preview_library_family_assignment", toolNames);
        Assert.Contains("sereinflow_apply_library_family_assignment", toolNames);
        Assert.Contains("sereinflow_preview_library_upgrade", toolNames);
        Assert.Contains("sereinflow_apply_library_upgrade", toolNames);
        Assert.Contains("sereinflow://archived-projects", resourceUris);
        Assert.Contains("sereinflow://archived-libraries", resourceUris);
        Assert.Contains("sereinflow://library-families", resourceUris);
        Assert.Contains(McpAiGuidance.SereinFlowFlowsResourceUri, resourceUris);
        Assert.Contains(McpAiGuidance.SereinLangSyntaxResourceUri, resourceUris);
        Assert.Contains(McpAiGuidance.LibraryImportResourceUri, resourceUris);
        Assert.Contains("sereinflow://library-families/{familyId}", resourceTemplates);
        Assert.Contains("sereinflow://projects/{projectId}/libraries", resourceTemplates);
        Assert.Contains("sereinflow://projects/{projectId}/library-upgrades/{upgradeId}", resourceTemplates);

        var activeProjects = Deserialize<AiPageDto<AiProjectSummaryDto>>(
            (await backend.CallToolAsync("sereinflow_list_projects", JsonSerializer.SerializeToElement(new { }), CancellationToken.None)).Value);
        var archivedProjects = Deserialize<AiPageDto<AiProjectSummaryDto>>(
            (await backend.CallToolAsync("sereinflow_list_archived_projects", JsonSerializer.SerializeToElement(new { }), CancellationToken.None)).Value);
        var activeLibraries = Deserialize<AiPageDto<AiLibrarySummaryDto>>(
            (await backend.CallToolAsync("sereinflow_list_libraries", JsonSerializer.SerializeToElement(new { }), CancellationToken.None)).Value);
        var archivedLibraries = Deserialize<AiPageDto<AiLibrarySummaryDto>>(
            (await backend.CallToolAsync("sereinflow_list_archived_libraries", JsonSerializer.SerializeToElement(new { }), CancellationToken.None)).Value);
        var allLibraries = Deserialize<AiPageDto<AiLibrarySummaryDto>>(
            (await backend.CallToolAsync(
                "sereinflow_list_libraries",
                JsonSerializer.SerializeToElement(new { includeArchived = true }),
                CancellationToken.None)).Value);

        Assert.Equal(activeProject.Id, Assert.Single(activeProjects.Items).Id);
        Assert.Equal(archivedProject.Id, Assert.Single(archivedProjects.Items).Id);
        Assert.Equal(availableLibrary.Id, Assert.Single(activeLibraries.Items).Id);
        Assert.Equal(archivedLibrary.Id, Assert.Single(archivedLibraries.Items).Id);
        Assert.Equal(2, allLibraries.Items.Count);

        var activeProjectResource = Deserialize<AiPageDto<AiProjectSummaryDto>>(
            (await backend.ReadResourceAsync("sereinflow://projects", CancellationToken.None)).Value);
        var archivedProjectResource = Deserialize<AiPageDto<AiProjectSummaryDto>>(
            (await backend.ReadResourceAsync("sereinflow://archived-projects", CancellationToken.None)).Value);
        var activeLibraryResource = Deserialize<AiPageDto<AiLibrarySummaryDto>>(
            (await backend.ReadResourceAsync("sereinflow://libraries", CancellationToken.None)).Value);
        var archivedLibraryResource = Deserialize<AiPageDto<AiLibrarySummaryDto>>(
            (await backend.ReadResourceAsync("sereinflow://archived-libraries", CancellationToken.None)).Value);

        Assert.Equal(activeProject.Id, Assert.Single(activeProjectResource.Items).Id);
        Assert.Equal(archivedProject.Id, Assert.Single(archivedProjectResource.Items).Id);
        Assert.Equal(availableLibrary.Id, Assert.Single(activeLibraryResource.Items).Id);
        Assert.Equal(archivedLibrary.Id, Assert.Single(archivedLibraryResource.Items).Id);

        var directProject = Deserialize<AiProjectSummaryDto>(
            (await backend.CallToolAsync(
                "sereinflow_get_project",
                JsonSerializer.SerializeToElement(new { projectId = archivedProject.Id }),
                CancellationToken.None)).Value);
        var directLibrary = Deserialize<AiLibrarySummaryDto>(
            (await backend.CallToolAsync(
                "sereinflow_get_library",
                JsonSerializer.SerializeToElement(new { libraryId = archivedLibrary.Id }),
                CancellationToken.None)).Value);

        Assert.Equal(ProjectStatus.Archived.ToString(), directProject.Status);
        Assert.Equal(LibraryLifecycleDto.Archived.ToString(), directLibrary.Lifecycle);
    }

    [Fact]
    public async Task ProjectScopedCatalogCallsDoNotExpandProjectOrLibraryVisibility()
    {
        var availableLibrary = CreateLibrary("scoped-available", LibraryLifecycleDto.Available);
        var archivedLibrary = CreateLibrary("scoped-archived", LibraryLifecycleDto.Archived);
        var unreferencedArchivedLibrary = CreateLibrary("unreferenced-archived", LibraryLifecycleDto.Archived);
        var references = new TestProjectLibraryReferenceRepository();
        using var host = CreateHost(services =>
        {
            services.AddSingleton<ILibraryCatalogService>(new TestLibraryCatalog(
                availableLibrary,
                archivedLibrary,
                unreferencedArchivedLibrary));
            services.AddSingleton<IProjectLibraryReferenceRepository>(references);
        });

        var project = Project.Create("Archived scoped MCP project");
        project.Archive();
        using (var dataScope = host.Services.CreateScope())
        {
            await dataScope.ServiceProvider.GetRequiredService<IProjectRepository>().AddAsync(project);
        }
        await references.AddAsync(project.Id, availableLibrary.Id);
        await references.AddAsync(project.Id, archivedLibrary.Id);

        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        accessor.Current = new McpPrincipal(
            "catalog-project-key",
            project.Id,
            new[] { McpPermissionDto.ProjectRead, McpPermissionDto.LibraryRead }.ToHashSet());
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();

        var activeProjects = Deserialize<AiPageDto<AiProjectSummaryDto>>(
            (await backend.CallToolAsync("sereinflow_list_projects", JsonSerializer.SerializeToElement(new { }), CancellationToken.None)).Value);
        var archivedProjects = Deserialize<AiPageDto<AiProjectSummaryDto>>(
            (await backend.CallToolAsync("sereinflow_list_archived_projects", JsonSerializer.SerializeToElement(new { }), CancellationToken.None)).Value);
        var activeLibraries = Deserialize<AiPageDto<AiLibrarySummaryDto>>(
            (await backend.CallToolAsync("sereinflow_list_libraries", JsonSerializer.SerializeToElement(new { }), CancellationToken.None)).Value);
        var archivedLibraries = Deserialize<AiPageDto<AiLibrarySummaryDto>>(
            (await backend.CallToolAsync("sereinflow_list_archived_libraries", JsonSerializer.SerializeToElement(new { }), CancellationToken.None)).Value);
        var allLibraries = Deserialize<AiPageDto<AiLibrarySummaryDto>>(
            (await backend.CallToolAsync(
                "sereinflow_list_libraries",
                JsonSerializer.SerializeToElement(new { includeArchived = true }),
                CancellationToken.None)).Value);

        Assert.Empty(activeProjects.Items);
        Assert.Equal(project.Id, Assert.Single(archivedProjects.Items).Id);
        Assert.Equal(availableLibrary.Id, Assert.Single(activeLibraries.Items).Id);
        Assert.Equal(archivedLibrary.Id, Assert.Single(archivedLibraries.Items).Id);
        Assert.Equal(2, allLibraries.Items.Count);
        Assert.DoesNotContain(allLibraries.Items, library => library.Id == unreferencedArchivedLibrary.Id);
    }

    [Fact]
    public async Task ProjectScopedFamilyReadsDoNotLeakUnreferencedArtifacts()
    {
        const string visibleFamilyId = "visible-family";
        var visible = CreateLibrary("family-visible", LibraryLifecycleDto.Available) with
        {
            FamilyId = visibleFamilyId,
            FamilyName = "Visible family",
            SemanticVersion = "1.0.0",
        };
        var hiddenInSameFamily = CreateLibrary("family-hidden", LibraryLifecycleDto.Available) with
        {
            FamilyId = visibleFamilyId,
            FamilyName = "Visible family",
            SemanticVersion = "2.0.0",
        };
        var hiddenFamilyArtifact = CreateLibrary("family-other", LibraryLifecycleDto.Available) with
        {
            FamilyId = "hidden-family",
            FamilyName = "Hidden family",
        };
        var references = new TestProjectLibraryReferenceRepository();
        using var host = CreateHost(services =>
        {
            services.AddSingleton<ILibraryCatalogService>(new TestLibraryCatalog(
                visible,
                hiddenInSameFamily,
                hiddenFamilyArtifact));
            services.AddSingleton<IProjectLibraryReferenceRepository>(references);
        });

        var project = Project.Create("Scoped family reads");
        using (var dataScope = host.Services.CreateScope())
        {
            await dataScope.ServiceProvider.GetRequiredService<IProjectRepository>().AddAsync(project);
        }
        await references.AddAsync(project.Id, visible.Id);

        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        accessor.Current = new McpPrincipal(
            "family-project-key",
            project.Id,
            new[] { McpPermissionDto.ProjectRead, McpPermissionDto.LibraryRead }.ToHashSet());
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();

        var families = Deserialize<AiPageDto<LibraryFamilyDto>>(
            (await backend.CallToolAsync("sereinflow_list_library_families", JsonSerializer.SerializeToElement(new { }), CancellationToken.None)).Value);
        var family = Assert.Single(families.Items);
        Assert.Equal(visibleFamilyId, family.Id);
        Assert.Equal(visible.Id, Assert.Single(family.Artifacts!).Id);
        Assert.Equal(visible.Id, family.LatestArtifactId);

        var direct = Deserialize<LibraryFamilyDto>(
            (await backend.CallToolAsync(
                "sereinflow_get_library_family",
                JsonSerializer.SerializeToElement(new { familyId = visibleFamilyId }),
                CancellationToken.None)).Value);
        Assert.Equal(visible.Id, Assert.Single(direct.Artifacts!).Id);

        var hidden = await backend.CallToolAsync(
            "sereinflow_get_library_family",
            JsonSerializer.SerializeToElement(new { familyId = "hidden-family" }),
            CancellationToken.None);
        Assert.Null(hidden.Value);

        var resource = Deserialize<AiPageDto<LibraryFamilyDto>>(
            (await backend.ReadResourceAsync("sereinflow://library-families", CancellationToken.None)).Value);
        Assert.Equal(visible.Id, Assert.Single(Assert.Single(resource.Items).Artifacts!).Id);
    }

    [Fact]
    public async Task LibraryFamilyAssignmentPreviewsWithoutMutationThenAppliesIdempotently()
    {
        var artifact = CreateLibrary("family-assignment-artifact", LibraryLifecycleDto.Available);
        var catalog = new TestLibraryCatalog(artifact);
        using var host = CreateHost(services => services.AddSingleton<ILibraryCatalogService>(catalog));
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        accessor.Current = new McpPrincipal(
            "family-admin",
            null,
            Enum.GetValues<McpPermissionDto>().ToHashSet(),
            IsAdministrator: true);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();

        var preview = Deserialize<LibraryFamilyAssignmentMcpPreviewDto>(
            (await backend.CallToolAsync(
                "sereinflow_preview_library_family_assignment",
                JsonSerializer.SerializeToElement(new
                {
                    libraryId = artifact.Id,
                    name = "Image processing",
                    description = "Immutable versions of the image processing package",
                }),
                CancellationToken.None)).Value);

        Assert.True(preview.CanApply);
        Assert.Null(catalog.Find(artifact.Id)!.FamilyId);
        var previewResource = Deserialize<LibraryFamilyAssignmentMcpPreviewDto>(
            (await backend.ReadResourceAsync($"sereinflow://mcp-previews/{preview.PreviewId:D}", CancellationToken.None)).Value);
        Assert.True(previewResource.CanApply);

        var applyArguments = JsonSerializer.SerializeToElement(new
        {
            previewId = preview.PreviewId,
            previewFingerprint = preview.PreviewFingerprint,
            confirmation = "APPLY",
            idempotencyKey = "assign-library-family-once",
        });
        var assigned = Deserialize<LibraryFamilyDto>(
            (await backend.CallToolAsync("sereinflow_apply_library_family_assignment", applyArguments, CancellationToken.None)).Value);
        var replay = Deserialize<LibraryFamilyDto>(
            (await backend.CallToolAsync("sereinflow_apply_library_family_assignment", applyArguments, CancellationToken.None)).Value);

        Assert.Equal("Image processing", assigned.Name);
        Assert.Equal(assigned.Id, catalog.Find(artifact.Id)!.FamilyId);
        Assert.Equal(assigned.Id, replay.Id);
        var appliedPreview = Deserialize<LibraryFamilyAssignmentMcpPreviewDto>(
            (await backend.ReadResourceAsync($"sereinflow://mcp-previews/{preview.PreviewId:D}", CancellationToken.None)).Value);
        Assert.False(appliedPreview.CanApply);
    }

    [Fact]
    public async Task LibraryFamilyAssignmentRequiresAdministratorAndLibraryManage()
    {
        var artifact = CreateLibrary("family-permission-artifact", LibraryLifecycleDto.Available);
        using var host = CreateHost(services => services.AddSingleton<ILibraryCatalogService>(new TestLibraryCatalog(artifact)));
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        accessor.Current = new McpPrincipal(
            "family-project-key",
            Guid.NewGuid(),
            new[] { McpPermissionDto.LibraryManage }.ToHashSet());
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();

        var exception = await Assert.ThrowsAsync<McpProtocolException>(() => backend.CallToolAsync(
            "sereinflow_preview_library_family_assignment",
            JsonSerializer.SerializeToElement(new { libraryId = artifact.Id, name = "Denied" }),
            CancellationToken.None));

        Assert.Equal(-32003, exception.Code);
    }

    [Fact]
    public async Task LibraryUpgradeRequiresBothFlowWriteAndLibraryManage()
    {
        var source = CreateUpgradeLibrary("upgrade-permission-source", "1.0.0");
        var target = CreateUpgradeLibrary("upgrade-permission-target", "2.0.0");
        var references = new TestProjectLibraryReferenceRepository();
        using var host = CreateHost(services =>
        {
            services.AddSingleton<ILibraryCatalogService>(new TestLibraryCatalog(source, target));
            services.AddSingleton<IProjectLibraryReferenceRepository>(references);
        });
        var project = Project.Create("Upgrade permissions");
        var flow = CreateLibraryFlow(source.Id);
        using (var dataScope = host.Services.CreateScope())
        {
            await dataScope.ServiceProvider.GetRequiredService<IProjectRepository>().AddAsync(project);
            await dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>().AddAsync(project.Id, flow);
        }
        await references.AddAsync(project.Id, source.Id);

        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        accessor.Current = new McpPrincipal(
            "upgrade-flow-only-key",
            project.Id,
            new[] { McpPermissionDto.FlowWrite }.ToHashSet());
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();

        var exception = await Assert.ThrowsAsync<McpProtocolException>(() => backend.CallToolAsync(
            "sereinflow_preview_library_upgrade",
            JsonSerializer.SerializeToElement(new
            {
                projectId = project.Id,
                sourceArtifactId = source.Id,
                targetArtifactId = target.Id,
                flowIds = new[] { flow.Id },
            }),
            CancellationToken.None));

        Assert.Equal(-32003, exception.Code);
    }

    [Fact]
    public async Task LibraryUpgradePreviewBindsApplyAndPersistsTheResult()
    {
        var source = CreateUpgradeLibrary("upgrade-source", "1.0.0");
        var target = CreateUpgradeLibrary("upgrade-target", "2.0.0");
        var references = new TestProjectLibraryReferenceRepository();
        using var host = CreateHost(services =>
        {
            services.AddSingleton<ILibraryCatalogService>(new TestLibraryCatalog(source, target));
            services.AddSingleton<IProjectLibraryReferenceRepository>(references);
        });
        var project = Project.Create("Upgrade one flow");
        var flow = CreateLibraryFlow(source.Id);
        using (var dataScope = host.Services.CreateScope())
        {
            await dataScope.ServiceProvider.GetRequiredService<IProjectRepository>().AddAsync(project);
            await dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>().AddAsync(project.Id, flow);
        }
        await references.AddAsync(project.Id, source.Id);

        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        accessor.Current = new McpPrincipal(
            "upgrade-project-key",
            project.Id,
            new[]
            {
                McpPermissionDto.ProjectRead,
                McpPermissionDto.LibraryRead,
                McpPermissionDto.FlowWrite,
                McpPermissionDto.LibraryManage,
            }.ToHashSet());
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();

        var preview = Deserialize<McpLibraryUpgradePreviewDto>(
            (await backend.CallToolAsync(
                "sereinflow_preview_library_upgrade",
                JsonSerializer.SerializeToElement(new
                {
                    projectId = project.Id,
                    sourceArtifactId = source.Id,
                    targetArtifactId = target.Id,
                    flowIds = new[] { flow.Id },
                }),
                CancellationToken.None)).Value);
        Assert.True(preview.CanApply);
        Assert.Equal(source.Id, preview.Plan.SourceArtifactId);
        Assert.Equal(target.Id, preview.Plan.TargetArtifactId);

        var applyTool = (await backend.ListToolsAsync(CancellationToken.None))
            .Single(tool => tool.Name == "sereinflow_apply_library_upgrade");
        var applyProperties = applyTool.InputSchema.GetProperty("properties");
        Assert.False(applyProperties.TryGetProperty("projectId", out _));
        Assert.False(applyProperties.TryGetProperty("planId", out _));
        Assert.False(applyProperties.TryGetProperty("sourceArtifactId", out _));
        Assert.False(applyProperties.TryGetProperty("targetArtifactId", out _));

        var applyArguments = JsonSerializer.SerializeToElement(new
        {
            previewId = preview.PreviewId,
            previewFingerprint = preview.PreviewFingerprint,
            confirmation = "APPLY",
            idempotencyKey = "upgrade-one-flow-once",
            flows = new[] { new { flowId = flow.Id, expectedFlowVersion = flow.Version } },
        });
        var applied = Deserialize<LibraryUpgradeApplyResultDto>(
            (await backend.CallToolAsync("sereinflow_apply_library_upgrade", applyArguments, CancellationToken.None)).Value);
        var replay = Deserialize<LibraryUpgradeApplyResultDto>(
            (await backend.CallToolAsync("sereinflow_apply_library_upgrade", applyArguments, CancellationToken.None)).Value);

        Assert.Equal(flow.Id, applied.FlowId);
        Assert.Equal(applied.NewVersion, replay.NewVersion);
        var persistedPlan = Deserialize<LibraryUpgradePlanDto>(
            (await backend.CallToolAsync(
                "sereinflow_get_library_upgrade",
                JsonSerializer.SerializeToElement(new { projectId = project.Id, upgradeId = preview.Plan.Id }),
                CancellationToken.None)).Value);
        Assert.Equal(LibraryUpgradePlanStatusDto.Applied, persistedPlan.Status);
        Assert.Equal(flow.Id, Assert.Single(persistedPlan.AppliedFlows!).FlowId);

        FlowDefinitionDto? persistedFlow;
        using (var readScope = host.Services.CreateScope())
        {
            persistedFlow = await readScope.ServiceProvider
                .GetRequiredService<IFlowDefinitionRepository>()
                .FindAsync(project.Id, flow.Id);
        }
        Assert.Equal(target.Id, persistedFlow!.Canvases.Single().Nodes.Single().Ui!.LibraryId);
        Assert.False(await references.IsReferencedAsync(project.Id, source.Id));
        Assert.True(await references.IsReferencedAsync(project.Id, target.Id));

        var previewResource = Deserialize<McpLibraryUpgradePreviewDto>(
            (await backend.ReadResourceAsync($"sereinflow://mcp-previews/{preview.PreviewId:D}", CancellationToken.None)).Value);
        Assert.False(previewResource.CanApply);
        var libraryReferences = Deserialize<AiPageDto<ProjectLibraryReferenceDto>>(
            (await backend.ReadResourceAsync($"sereinflow://projects/{project.Id:D}/libraries", CancellationToken.None)).Value);
        Assert.Equal(target.Id, Assert.Single(libraryReferences.Items).LibraryId);
    }

    [Fact]
    public async Task LibraryUpgradeBatchReportsPartialResultsAndCompletesPreviewAfterDurableSuccess()
    {
        var source = CreateUpgradeLibrary("upgrade-batch-source", "1.0.0");
        var target = CreateUpgradeLibrary("upgrade-batch-target", "2.0.0");
        var references = new TestProjectLibraryReferenceRepository();
        using var host = CreateHost(services =>
        {
            services.AddSingleton<ILibraryCatalogService>(new TestLibraryCatalog(source, target));
            services.AddSingleton<IProjectLibraryReferenceRepository>(references);
        });
        var project = Project.Create("Upgrade batch");
        var firstFlow = CreateLibraryFlow(source.Id);
        var secondFlow = CreateLibraryFlow(source.Id);
        using (var dataScope = host.Services.CreateScope())
        {
            var projects = dataScope.ServiceProvider.GetRequiredService<IProjectRepository>();
            var flows = dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
            await projects.AddAsync(project);
            await flows.AddAsync(project.Id, firstFlow);
            await flows.AddAsync(project.Id, secondFlow);
        }
        await references.AddAsync(project.Id, source.Id);

        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        accessor.Current = new McpPrincipal(
            "upgrade-batch-key",
            project.Id,
            new[] { McpPermissionDto.FlowWrite, McpPermissionDto.LibraryManage }.ToHashSet());
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var preview = Deserialize<McpLibraryUpgradePreviewDto>(
            (await backend.CallToolAsync(
                "sereinflow_preview_library_upgrade",
                JsonSerializer.SerializeToElement(new
                {
                    projectId = project.Id,
                    sourceArtifactId = source.Id,
                    targetArtifactId = target.Id,
                    flowIds = new[] { firstFlow.Id, secondFlow.Id },
                }),
                CancellationToken.None)).Value);

        using (var dataScope = host.Services.CreateScope())
        {
            var flows = dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
            var changed = await flows.TryUpdateAsync(project.Id, secondFlow with { Checksum = "changed-after-preview" }, secondFlow.Version);
            Assert.NotNull(changed);
        }

        var batch = Deserialize<LibraryUpgradeBatchApplyResultDto>(
            (await backend.CallToolAsync(
                "sereinflow_apply_library_upgrade",
                JsonSerializer.SerializeToElement(new
                {
                    previewId = preview.PreviewId,
                    previewFingerprint = preview.PreviewFingerprint,
                    confirmation = "APPLY",
                    idempotencyKey = "upgrade-batch-partial",
                    flows = new[]
                    {
                        new { flowId = firstFlow.Id, expectedFlowVersion = firstFlow.Version },
                        new { flowId = secondFlow.Id, expectedFlowVersion = secondFlow.Version },
                    },
                }),
                CancellationToken.None)).Value);

        Assert.Equal(firstFlow.Id, Assert.Single(batch.Succeeded).FlowId);
        Assert.Equal(secondFlow.Id, Assert.Single(batch.Failed).FlowId);
        Assert.Equal("flow.version_conflict", batch.Failed.Single().Code);
        var previewResource = Deserialize<McpLibraryUpgradePreviewDto>(
            (await backend.ReadResourceAsync($"sereinflow://mcp-previews/{preview.PreviewId:D}", CancellationToken.None)).Value);
        Assert.False(previewResource.CanApply);
    }

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

    private static TestHost CreateHost(Action<IServiceCollection>? configureServices = null)
    {
        var root = Path.Combine(Path.GetTempPath(), $"sereinflow-mcp-test-{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SereinFlow:DataRoot"] = root,
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSereinFlowStorage(configuration, root);
        services.AddSereinFlowApplication();
        services.AddSereinFlowMcp(configuration);
        configureServices?.Invoke(services);
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

    private static FlowDefinitionDto CreateLibraryFlow(string artifactId)
    {
        var node = new NodeDto(
            "image-transform-node",
            NodeTypeDto.Action,
            "Image transform",
            0,
            0,
            [],
            [],
            null,
            new NodeUiMetadataDto(
                "action",
                "Image transform",
                string.Empty,
                null,
                "ready",
                true,
                null,
                "image-library",
                artifactId,
                "Image.Nodes",
                "Transform",
                "Image.dll",
                "1.0.0",
                "System.String",
                LibraryNodeContractId: "image-transform"));
        return new FlowDefinitionDto(
            Guid.NewGuid(),
            FlowDefinition.CurrentSchemaVersion,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [])],
            node.Id,
            "initial");
    }

    private static LibraryDto CreateLibrary(string id, LibraryLifecycleDto lifecycle)
        => new(
            id,
            id,
            "1.0.0",
            $"{id}.zip",
            1,
            id,
            DateTimeOffset.UtcNow,
            [],
            lifecycle);

    private static LibraryDto CreateUpgradeLibrary(string artifactId, string version)
    {
        var manifestNode = new LibraryManifestNodeDto(
            "image-transform",
            LibraryContractIdentityConfidenceDto.Explicit,
            NodeTypeDto.Action,
            "Image.Nodes",
            "Transform",
            "Image.Nodes::Transform()",
            "System.String",
            false,
            []);
        var node = new LibraryNodeDto(
            manifestNode.ContractId,
            manifestNode.Type,
            "Image transform",
            null,
            artifactId,
            manifestNode.DeclaringType,
            manifestNode.MethodName,
            "Image.dll",
            version,
            manifestNode.ReturnType,
            [],
            manifestNode.IsAwaitable,
            manifestNode.ContractId,
            manifestNode.OverloadSignature,
            manifestNode.IdentityConfidence,
            "Image library");
        return new LibraryDto(
            artifactId,
            "Image library",
            version,
            $"Image-{version}.zip",
            1024,
            artifactId,
            DateTimeOffset.UtcNow,
            [node],
            LibraryLifecycleDto.Available,
            "image-library-family",
            version,
            new LibraryArtifactManifestDto(artifactId, "Image", version, version, [manifestNode]),
            "Image library");
    }

    private static T Deserialize<T>(object? value)
        => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)
            ?? throw new InvalidOperationException("The integration response could not be deserialized.");

    private sealed class TestLibraryCatalog : ILibraryCatalogService
    {
        private readonly Dictionary<string, LibraryDto> _libraries;
        private readonly Dictionary<string, LibraryFamilyDto> _families;

        public TestLibraryCatalog(params LibraryDto[] libraries)
        {
            _libraries = libraries.ToDictionary(static library => library.Id, StringComparer.OrdinalIgnoreCase);
            _families = libraries
                .Where(static library => !string.IsNullOrWhiteSpace(library.FamilyId))
                .GroupBy(static library => library.FamilyId!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    static group => group.Key,
                    group => new LibraryFamilyDto(
                        group.Key,
                        group.First().FamilyName ?? group.Key,
                        null,
                        null,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow),
                    StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyList<LibraryDto> List() => _libraries.Values.ToArray();
        public LibraryDto? Find(string libraryId) => _libraries.GetValueOrDefault(libraryId);
        public Task<IReadOnlyList<LibraryDto>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LibraryDto>>(_libraries.Values
                .Where(library => includeArchived || library.Lifecycle == LibraryLifecycleDto.Available)
                .ToArray());
        public Task<LibraryDto?> FindAsync(string libraryId, CancellationToken cancellationToken = default) => Task.FromResult(Find(libraryId));
        public Task<LibraryUploadResultDto> UploadAsync(Stream package, string fileName, long? declaredLength = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public bool Delete(string libraryId) => false;
        public Task<bool> ArchiveAsync(string libraryId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<LibraryDto?> ReindexAsync(string libraryId, CancellationToken cancellationToken = default) => Task.FromResult(Find(libraryId));
        public Task<int> ReindexOutdatedAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<IReadOnlyList<LibraryFamilyDto>> ListFamiliesAsync(
            bool includeArchivedArtifacts = true,
            CancellationToken cancellationToken = default)
        {
            var families = _families.Values
                .Select(family =>
                {
                    var artifacts = _libraries.Values
                        .Where(library => string.Equals(library.FamilyId, family.Id, StringComparison.OrdinalIgnoreCase))
                        .Where(library => includeArchivedArtifacts || library.Lifecycle == LibraryLifecycleDto.Available)
                        .OrderByDescending(library => ParseVersion(library.SemanticVersion ?? library.Version))
                        .ThenByDescending(static library => library.UploadedAt)
                        .ThenBy(static library => library.Id, StringComparer.Ordinal)
                        .ToArray();
                    return family with
                    {
                        LatestArtifactId = artifacts.FirstOrDefault(static library => library.Lifecycle == LibraryLifecycleDto.Available)?.Id,
                        Artifacts = artifacts,
                    };
                })
                .OrderBy(static family => family.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static family => family.Id, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult<IReadOnlyList<LibraryFamilyDto>>(families);
        }

        public async Task<LibraryFamilyDto?> AssignFamilyAsync(
            string libraryId,
            AssignLibraryFamilyRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (!_libraries.TryGetValue(libraryId, out var library))
                return null;

            LibraryFamilyDto family;
            if (!string.IsNullOrWhiteSpace(request.FamilyId))
            {
                if (!_families.TryGetValue(request.FamilyId.Trim(), out var existingFamily))
                    throw new ArgumentException("The requested library family does not exist.", nameof(request));
                family = existingFamily;
            }
            else
            {
                var name = request.Name?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException("A family name is required.", nameof(request));
                family = _families.Values.SingleOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
                    ?? new LibraryFamilyDto(Guid.NewGuid().ToString("N"), name, request.Description?.Trim(), null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
                _families[family.Id] = family;
            }

            _libraries[libraryId] = library with
            {
                FamilyId = family.Id,
                FamilyName = family.Name,
                SemanticVersion = library.SemanticVersion ?? library.Version,
            };
            return (await ListFamiliesAsync(cancellationToken: cancellationToken))
                .Single(item => string.Equals(item.Id, family.Id, StringComparison.OrdinalIgnoreCase));
        }

        private static Version ParseVersion(string value)
            => Version.TryParse(value?.Trim().TrimStart('v', 'V'), out var parsed)
                ? parsed
                : new Version(0, 0);
    }

    private sealed class TestProjectLibraryReferenceRepository : IProjectLibraryReferenceRepository
    {
        private readonly List<ProjectLibraryReference> _references = [];

        public Task<IReadOnlyList<ProjectLibraryReference>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectLibraryReference>>(_references
                .Where(reference => reference.ProjectId == projectId)
                .ToArray());

        public Task<bool> IsReferencedAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
            => Task.FromResult(_references.Any(reference =>
                reference.ProjectId == projectId
                && string.Equals(reference.LibraryId, libraryId, StringComparison.OrdinalIgnoreCase)));

        public Task<ProjectLibraryReference> AddAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
        {
            var reference = new ProjectLibraryReference(projectId, libraryId, DateTimeOffset.UtcNow);
            _references.Add(reference);
            return Task.FromResult(reference);
        }

        public Task<bool> RemoveAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
        {
            var removed = _references.RemoveAll(reference =>
                reference.ProjectId == projectId
                && string.Equals(reference.LibraryId, libraryId, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(removed > 0);
        }
    }

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

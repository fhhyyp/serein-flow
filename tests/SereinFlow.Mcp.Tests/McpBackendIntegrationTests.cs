using System;
using System.Collections.Generic;
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
        var selectResult = catalog.Descriptors.Select(static item => item.Name);
        var selectResult1 = tools.Select(static item => item.Name);
        Assert.Equal(selectResult, selectResult1);
        var tryGetResult = catalog.TryGet("sereinflow_preview_flow_patch", out var flowPatch);
        Assert.True(tryGetResult);
        Assert.NotNull(flowPatch);
    }

    [Fact]
    public async Task CatalogToolsAndResourcesSeparateActiveAndArchivedRecords()
    {
        var availableLibrary = CreateLibrary("available-library", LibraryLifecycleDto.Available);
        var archivedLibrary = CreateLibrary("archived-library", LibraryLifecycleDto.Archived);
        using var host = CreateHost(services =>
        {
            var testLibraryCatalog = new TestLibraryCatalog(availableLibrary, archivedLibrary);
            services.AddSingleton<ILibraryCatalogService>(testLibraryCatalog);
        });
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        var toHashSetResult = Enum.GetValues<McpPermissionDto>()
            .ToHashSet();
        accessor.Current = new McpPrincipal("catalog-admin", null, toHashSetResult, IsAdministrator: true);
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
        var evaluatedValue = (await backend.ListToolsAsync(CancellationToken.None));
        var selectResult2 = evaluatedValue.Select(static tool => tool.Name);
        var toolNames = selectResult2.ToArray();
        var evaluatedValue3 = (await backend.ListResourcesAsync(CancellationToken.None));
        var selectResult4 = evaluatedValue3.Select(static resource => resource.Uri);
        var resourceUris = selectResult4.ToArray();
        var evaluatedValue5 = (await backend.ListResourceTemplatesAsync(CancellationToken.None));
        var selectResult6 = evaluatedValue5.Select(static resource => resource.UriTemplate);
        var resourceTemplates = selectResult6.ToArray();
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
        Assert.Contains(McpAiGuidance.SereinFlowWorkpiecesResourceUri, resourceUris);
        Assert.Contains(McpAiGuidance.SereinLangSyntaxResourceUri, resourceUris);
        Assert.Contains(McpAiGuidance.LibraryImportResourceUri, resourceUris);
        Assert.Contains("sereinflow://library-families/{familyId}", resourceTemplates);
        Assert.Contains("sereinflow://projects/{projectId}/libraries", resourceTemplates);
        Assert.Contains("sereinflow://projects/{projectId}/library-upgrades/{upgradeId}", resourceTemplates);
        var serializeToElementResult = JsonSerializer.SerializeToElement(new { });
        var evaluatedValue7 = (await backend.CallToolAsync("sereinflow_list_projects", serializeToElementResult, CancellationToken.None)).Value;
        var activeProjects = Deserialize<AiPageDto<AiProjectSummaryDto>>(evaluatedValue7);
        var serializeToElementResult8 = JsonSerializer.SerializeToElement(new { });
        var evaluatedValue9 = (await backend.CallToolAsync("sereinflow_list_archived_projects", serializeToElementResult8, CancellationToken.None)).Value;
        var archivedProjects = Deserialize<AiPageDto<AiProjectSummaryDto>>(evaluatedValue9);
        var serializeToElementResult10 = JsonSerializer.SerializeToElement(new { });
        var evaluatedValue11 = (await backend.CallToolAsync("sereinflow_list_libraries", serializeToElementResult10, CancellationToken.None)).Value;
        var activeLibraries = Deserialize<AiPageDto<AiLibrarySummaryDto>>(evaluatedValue11);
        var serializeToElementResult12 = JsonSerializer.SerializeToElement(new { });
        var evaluatedValue13 = (await backend.CallToolAsync("sereinflow_list_archived_libraries", serializeToElementResult12, CancellationToken.None)).Value;
        var archivedLibraries = Deserialize<AiPageDto<AiLibrarySummaryDto>>(evaluatedValue13);
        var serializeToElementResult14 = JsonSerializer.SerializeToElement(new { includeArchived = true });
        var evaluatedValue15 = (await backend.CallToolAsync("sereinflow_list_libraries", serializeToElementResult14, CancellationToken.None)).Value;
        var allLibraries = Deserialize<AiPageDto<AiLibrarySummaryDto>>(evaluatedValue15);
        var evaluatedValue16 = Assert.Single(activeProjects.Items).Id;
        Assert.Equal(activeProject.Id, evaluatedValue16);
        var evaluatedValue17 = Assert.Single(archivedProjects.Items).Id;
        Assert.Equal(archivedProject.Id, evaluatedValue17);
        var evaluatedValue18 = Assert.Single(activeLibraries.Items).Id;
        Assert.Equal(availableLibrary.Id, evaluatedValue18);
        var evaluatedValue19 = Assert.Single(archivedLibraries.Items).Id;
        Assert.Equal(archivedLibrary.Id, evaluatedValue19);
        Assert.Equal(2, allLibraries.Items.Count);
        var evaluatedValue20 = (await backend.ReadResourceAsync("sereinflow://projects", CancellationToken.None)).Value;
        var activeProjectResource = Deserialize<AiPageDto<AiProjectSummaryDto>>(evaluatedValue20);
        var evaluatedValue21 = (await backend.ReadResourceAsync("sereinflow://archived-projects", CancellationToken.None)).Value;
        var archivedProjectResource = Deserialize<AiPageDto<AiProjectSummaryDto>>(evaluatedValue21);
        var evaluatedValue22 = (await backend.ReadResourceAsync("sereinflow://libraries", CancellationToken.None)).Value;
        var activeLibraryResource = Deserialize<AiPageDto<AiLibrarySummaryDto>>(evaluatedValue22);
        var evaluatedValue23 = (await backend.ReadResourceAsync("sereinflow://archived-libraries", CancellationToken.None)).Value;
        var archivedLibraryResource = Deserialize<AiPageDto<AiLibrarySummaryDto>>(evaluatedValue23);
        var evaluatedValue24 = Assert.Single(activeProjectResource.Items).Id;
        Assert.Equal(activeProject.Id, evaluatedValue24);
        var evaluatedValue25 = Assert.Single(archivedProjectResource.Items).Id;
        Assert.Equal(archivedProject.Id, evaluatedValue25);
        var evaluatedValue26 = Assert.Single(activeLibraryResource.Items).Id;
        Assert.Equal(availableLibrary.Id, evaluatedValue26);
        var evaluatedValue27 = Assert.Single(archivedLibraryResource.Items).Id;
        Assert.Equal(archivedLibrary.Id, evaluatedValue27);
        var serializeToElementResult28 = JsonSerializer.SerializeToElement(new { projectId = archivedProject.Id });
        var evaluatedValue29 = (await backend.CallToolAsync("sereinflow_get_project", serializeToElementResult28, CancellationToken.None)).Value;
        var directProject = Deserialize<AiProjectSummaryDto>(evaluatedValue29);
        var serializeToElementResult30 = JsonSerializer.SerializeToElement(new { libraryId = archivedLibrary.Id });
        var evaluatedValue31 = (await backend.CallToolAsync("sereinflow_get_library", serializeToElementResult30, CancellationToken.None)).Value;
        var directLibrary = Deserialize<AiLibrarySummaryDto>(evaluatedValue31);
        var toStringResult = ProjectStatus.Archived.ToString();
        Assert.Equal(toStringResult, directProject.Status);
        var toStringResult32 = LibraryLifecycleDto.Archived.ToString();
        Assert.Equal(toStringResult32, directLibrary.Lifecycle);
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
            var testLibraryCatalog33 = new TestLibraryCatalog(availableLibrary, archivedLibrary, unreferencedArchivedLibrary);
            services.AddSingleton<ILibraryCatalogService>(testLibraryCatalog33);
            services.AddSingleton<IProjectLibraryReferenceRepository>(references);
        });
        var project = Project.Create("Archived scoped MCP project");
        project.Archive();
        using (var dataScope = host.Services.CreateScope())
        {
            var getRequiredServiceResult = dataScope.ServiceProvider.GetRequiredService<IProjectRepository>();
            await getRequiredServiceResult.AddAsync(project);
        }

        await references.AddAsync(project.Id, availableLibrary.Id);
        await references.AddAsync(project.Id, archivedLibrary.Id);
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        var toHashSetResult34 = new[]
        {
            McpPermissionDto.ProjectRead,
            McpPermissionDto.LibraryRead
        }.ToHashSet();
        accessor.Current = new McpPrincipal("catalog-project-key", project.Id, toHashSetResult34);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var serializeToElementResult35 = JsonSerializer.SerializeToElement(new { });
        var evaluatedValue36 = (await backend.CallToolAsync("sereinflow_list_projects", serializeToElementResult35, CancellationToken.None)).Value;
        var activeProjects = Deserialize<AiPageDto<AiProjectSummaryDto>>(evaluatedValue36);
        var serializeToElementResult37 = JsonSerializer.SerializeToElement(new { });
        var evaluatedValue38 = (await backend.CallToolAsync("sereinflow_list_archived_projects", serializeToElementResult37, CancellationToken.None)).Value;
        var archivedProjects = Deserialize<AiPageDto<AiProjectSummaryDto>>(evaluatedValue38);
        var serializeToElementResult39 = JsonSerializer.SerializeToElement(new { });
        var evaluatedValue40 = (await backend.CallToolAsync("sereinflow_list_libraries", serializeToElementResult39, CancellationToken.None)).Value;
        var activeLibraries = Deserialize<AiPageDto<AiLibrarySummaryDto>>(evaluatedValue40);
        var serializeToElementResult41 = JsonSerializer.SerializeToElement(new { });
        var evaluatedValue42 = (await backend.CallToolAsync("sereinflow_list_archived_libraries", serializeToElementResult41, CancellationToken.None)).Value;
        var archivedLibraries = Deserialize<AiPageDto<AiLibrarySummaryDto>>(evaluatedValue42);
        var serializeToElementResult43 = JsonSerializer.SerializeToElement(new { includeArchived = true });
        var evaluatedValue44 = (await backend.CallToolAsync("sereinflow_list_libraries", serializeToElementResult43, CancellationToken.None)).Value;
        var allLibraries = Deserialize<AiPageDto<AiLibrarySummaryDto>>(evaluatedValue44);
        Assert.Empty(activeProjects.Items);
        var evaluatedValue45 = Assert.Single(archivedProjects.Items).Id;
        Assert.Equal(project.Id, evaluatedValue45);
        var evaluatedValue46 = Assert.Single(activeLibraries.Items).Id;
        Assert.Equal(availableLibrary.Id, evaluatedValue46);
        var evaluatedValue47 = Assert.Single(archivedLibraries.Items).Id;
        Assert.Equal(archivedLibrary.Id, evaluatedValue47);
        Assert.Equal(2, allLibraries.Items.Count);
        Assert.DoesNotContain(allLibraries.Items, library => library.Id == unreferencedArchivedLibrary.Id);
    }

    [Fact]
    public async Task LibraryFamilyAssignmentPreviewsWithoutMutationThenAppliesIdempotently()
    {
        var artifact = CreateLibrary("family-assignment-artifact", LibraryLifecycleDto.Available);
        var catalog = new TestLibraryCatalog(artifact);
        using var host = CreateHost(services =>
        {
            services.AddSingleton<ILibraryCatalogService>(catalog);
        });
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        var toHashSetResult49 = Enum.GetValues<McpPermissionDto>()
            .ToHashSet();
        accessor.Current = new McpPrincipal("family-admin", null, toHashSetResult49, IsAdministrator: true);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var serializeToElementResult50 = JsonSerializer.SerializeToElement(new { libraryId = artifact.Id, name = "Image processing", description = "Immutable versions of the image processing package", });
        var evaluatedValue51 = (await backend.CallToolAsync("sereinflow_preview_library_family_assignment", serializeToElementResult50, CancellationToken.None)).Value;
        var preview = Deserialize<LibraryFamilyAssignmentMcpPreviewDto>(evaluatedValue51);
        Assert.True(preview.CanApply);
        var evaluatedValue52 = catalog.Find(artifact.Id)!.FamilyId;
        Assert.Null(evaluatedValue52);
        var evaluatedValue53 = (await backend.ReadResourceAsync($"sereinflow://mcp-previews/{preview.PreviewId:D}", CancellationToken.None)).Value;
        var previewResource = Deserialize<LibraryFamilyAssignmentMcpPreviewDto>(evaluatedValue53);
        Assert.True(previewResource.CanApply);
        var applyArguments = JsonSerializer.SerializeToElement(new { previewId = preview.PreviewId, previewFingerprint = preview.PreviewFingerprint, confirmation = "APPLY", idempotencyKey = "assign-library-family-once", });
        var evaluatedValue54 = (await backend.CallToolAsync("sereinflow_apply_library_family_assignment", applyArguments, CancellationToken.None)).Value;
        var assigned = Deserialize<LibraryFamilyDto>(evaluatedValue54);
        var evaluatedValue55 = (await backend.CallToolAsync("sereinflow_apply_library_family_assignment", applyArguments, CancellationToken.None)).Value;
        var replay = Deserialize<LibraryFamilyDto>(evaluatedValue55);
        Assert.Equal("Image processing", assigned.Name);
        var evaluatedValue56 = catalog.Find(artifact.Id)!.FamilyId;
        Assert.Equal(assigned.Id, evaluatedValue56);
        Assert.Equal(assigned.Id, replay.Id);
        var evaluatedValue57 = (await backend.ReadResourceAsync($"sereinflow://mcp-previews/{preview.PreviewId:D}", CancellationToken.None)).Value;
        var appliedPreview = Deserialize<LibraryFamilyAssignmentMcpPreviewDto>(evaluatedValue57);
        Assert.False(appliedPreview.CanApply);
    }

    [Fact]
    public async Task LibraryFamilyAssignmentRequiresAdministratorAndLibraryManage()
    {
        var artifact = CreateLibrary("family-permission-artifact", LibraryLifecycleDto.Available);
        using var host = CreateHost(services =>
        {
            var testLibraryCatalog58 = new TestLibraryCatalog(artifact);
            services.AddSingleton<ILibraryCatalogService>(testLibraryCatalog58);
        });
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        var newGuidResult = Guid.NewGuid();
        var toHashSetResult60 = new[]
        {
            McpPermissionDto.LibraryManage
        }.ToHashSet();
        accessor.Current = new McpPrincipal("family-project-key", newGuidResult, toHashSetResult60);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var exception = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            var serializeToElementResult59 = JsonSerializer.SerializeToElement(new { libraryId = artifact.Id, name = "Denied" });
            var callToolAsyncResult = backend.CallToolAsync("sereinflow_preview_library_family_assignment", serializeToElementResult59, CancellationToken.None);
            return callToolAsyncResult;
        });
        Assert.Equal(McpProtocolErrorCodes.PermissionDenied, exception.Code);
    }

    [Fact]
    public async Task LibraryUpgradeRequiresBothFlowWriteAndLibraryManage()
    {
        var source = CreateUpgradeLibrary("upgrade-permission-source", "1.0.0");
        var target = CreateUpgradeLibrary("upgrade-permission-target", "2.0.0");
        var references = new TestProjectLibraryReferenceRepository();
        using var host = CreateHost(services =>
        {
            var testLibraryCatalog61 = new TestLibraryCatalog(source, target);
            services.AddSingleton<ILibraryCatalogService>(testLibraryCatalog61);
            services.AddSingleton<IProjectLibraryReferenceRepository>(references);
        });
        var project = Project.Create("Upgrade permissions");
        var flow = CreateLibraryFlow(source.Id);
        using (var dataScope = host.Services.CreateScope())
        {
            var getRequiredServiceResult62 = dataScope.ServiceProvider.GetRequiredService<IProjectRepository>();
            await getRequiredServiceResult62.AddAsync(project);
            var getRequiredServiceResult63 = dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
            await getRequiredServiceResult63.AddAsync(project.Id, flow);
        }

        await references.AddAsync(project.Id, source.Id);
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        var toHashSetResult66 = new[]
        {
            McpPermissionDto.FlowWrite
        }.ToHashSet();
        accessor.Current = new McpPrincipal("upgrade-flow-only-key", project.Id, toHashSetResult66);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var exception = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            var serializeToElementResult64 = JsonSerializer.SerializeToElement(new { projectId = project.Id, sourceArtifactId = source.Id, targetArtifactId = target.Id, flowIds = new[] { flow.Id }, });
            var callToolAsyncResult65 = backend.CallToolAsync("sereinflow_preview_library_upgrade", serializeToElementResult64, CancellationToken.None);
            return callToolAsyncResult65;
        });
        Assert.Equal(McpProtocolErrorCodes.PermissionDenied, exception.Code);
    }

    [Fact]
    public async Task FlowPatchToolPublishesTypedOperationSchema()
    {
        using var host = CreateHost();
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var evaluatedValue98 = (await backend.ListToolsAsync(CancellationToken.None));
        var tool = evaluatedValue98.Single(item => item.Name == "sereinflow_preview_flow_patch");
        var getPropertyResult99 = tool.InputSchema.GetProperty("properties");
        var operations = getPropertyResult99.GetProperty("operations");
        var getPropertyResult100 = operations.GetProperty("items");
        var alternatives = getPropertyResult100.GetProperty("oneOf");
        var getArrayLengthResult = alternatives.GetArrayLength();
        Assert.Equal(30, getArrayLengthResult);
        var enumerateArrayResult = alternatives.EnumerateArray();
        Assert.Contains(enumerateArrayResult, item =>
        {
            var getPropertyResult1 = item.GetProperty("properties");
            var evaluatedValue68 = getPropertyResult1.TryGetProperty("op", out var op) && resolveGetStringResult() == "setRunPolicy";
            return evaluatedValue68;
            string? resolveGetStringResult()
            {
                var evaluatedValue67 = op.GetProperty("enum")[0];
                var getStringResult = evaluatedValue67.GetString();
                return getStringResult;
            }
        });
        var enumerateArrayResult101 = alternatives.EnumerateArray();
        Assert.Contains(enumerateArrayResult101, item =>
        {
            var getPropertyResult2 = item.GetProperty("properties");
            var evaluatedValue72 = getPropertyResult2.TryGetProperty("op", out var op) && resolveGetStringResult69() == "addNodeParameter";
            return evaluatedValue72;
            string? resolveGetStringResult69()
            {
                var evaluatedValue70 = op.GetProperty("enum")[0];
                var getStringResult71 = evaluatedValue70.GetString();
                return getStringResult71;
            }
        });
        var enumerateArrayResult102 = alternatives.EnumerateArray();
        Assert.Contains(enumerateArrayResult102, item =>
        {
            var getPropertyResult3 = item.GetProperty("properties");
            var evaluatedValue76 = getPropertyResult3.TryGetProperty("op", out var op) && resolveGetStringResult73() == "removeNodeParameter";
            return evaluatedValue76;
            string? resolveGetStringResult73()
            {
                var evaluatedValue74 = op.GetProperty("enum")[0];
                var getStringResult75 = evaluatedValue74.GetString();
                return getStringResult75;
            }
        });
        var enumerateArrayResult103 = alternatives.EnumerateArray();
        var addConnection = enumerateArrayResult103.Single(item =>
        {
            var getPropertyResult4 = item.GetProperty("properties");
            var evaluatedValue80 = getPropertyResult4.TryGetProperty("op", out var op) && resolveGetStringResult77() == "addConnection";
            return evaluatedValue80;
            string? resolveGetStringResult77()
            {
                var evaluatedValue78 = op.GetProperty("enum")[0];
                var getStringResult79 = evaluatedValue78.GetString();
                return getStringResult79;
            }
        });
        var getPropertyResult104 = addConnection.GetProperty("properties");
        var connection = getPropertyResult104.GetProperty("connection");
        var getPropertyResult105 = connection.GetProperty("required");
        var enumerateArrayResult106 = getPropertyResult105.EnumerateArray();
        var selectResult107 = enumerateArrayResult106.Select(item =>
        {
            var evaluatedValue81 = item.GetString()!;
            return evaluatedValue81;
        });
        var connectionRequired = selectResult107.ToArray();
        Assert.Contains("dataSource", connectionRequired);
        Assert.Contains("branch", connectionRequired);
        var evaluatedValue115 = resolveGetPropertyResult108()[0];
        var dataSourceEnum = evaluatedValue115.GetProperty("enum");
        var toArrayResult = dataSourceEnum.EnumerateArray()
            .Select(item =>
            {
                var evaluatedValue82 = item.GetString()!;
                return evaluatedValue82;
            })
            .ToArray();
        Assert.Equal(["literal", "previousNode", "projectInput", "expression"], toArrayResult);
        var evaluatedValue122 = resolveGetPropertyResult118()[1];
        var getPropertyResult123 = evaluatedValue122.GetProperty("type");
        var getStringResult124 = getPropertyResult123.GetString();
        Assert.Equal("null", getStringResult124);
        var enumerateArrayResult125 = alternatives.EnumerateArray();
        var runPolicy = enumerateArrayResult125.Single(item =>
        {
            var getPropertyResult5 = item.GetProperty("properties");
            var evaluatedValue86 = getPropertyResult5.TryGetProperty("op", out var op) && resolveGetStringResult83() == "setRunPolicy";
            return evaluatedValue86;
            string? resolveGetStringResult83()
            {
                var evaluatedValue84 = op.GetProperty("enum")[0];
                var getStringResult85 = evaluatedValue84.GetString();
                return getStringResult85;
            }
        });
        var evaluatedValue132 = resolveGetPropertyResult126()[0];
        var getPropertyResult133 = evaluatedValue132.GetProperty("type");
        var getStringResult134 = getPropertyResult133.GetString();
        Assert.Equal("string", getStringResult134);
        var getPropertyResult135 = runPolicy.GetProperty("additionalProperties");
        var getBooleanResult = getPropertyResult135.GetBoolean();
        Assert.False(getBooleanResult);
        var enumerateArrayResult136 = alternatives.EnumerateArray();
        var addNode = enumerateArrayResult136.Single(item =>
        {
            var getPropertyResult6 = item.GetProperty("properties");
            var evaluatedValue90 = getPropertyResult6.TryGetProperty("op", out var op) && resolveGetStringResult87() == "addNode";
            return evaluatedValue90;
            string? resolveGetStringResult87()
            {
                var evaluatedValue88 = op.GetProperty("enum")[0];
                var getStringResult89 = evaluatedValue88.GetString();
                return getStringResult89;
            }
        });
        var getPropertyResult137 = addNode.GetProperty("properties");
        var getPropertyResult138 = getPropertyResult137.GetProperty("node");
        var getPropertyResult139 = getPropertyResult138.GetProperty("oneOf");
        var enumerateArrayResult140 = getPropertyResult139.EnumerateArray();
        var scriptNode = enumerateArrayResult140.Single(item =>
        {
            var evaluatedValue96 = resolveGetStringResult91() == "script";
            return evaluatedValue96;
            string? resolveGetStringResult91()
            {
                var evaluatedValue94 = resolveGetPropertyResult()[0];
                var getStringResult95 = evaluatedValue94.GetString();
                return getStringResult95;
                JsonElement resolveGetPropertyResult()
                {
                    var getPropertyResult = item.GetProperty("properties");
                    var getPropertyResult92 = getPropertyResult.GetProperty("type");
                    var getPropertyResult93 = getPropertyResult92.GetProperty("enum");
                    return getPropertyResult93;
                }
            }
        });
        var getPropertyResult141 = scriptNode.GetProperty("properties");
        var script = getPropertyResult141.GetProperty("script");
        var getPropertyResult142 = script.GetProperty("required");
        var enumerateArrayResult143 = getPropertyResult142.EnumerateArray();
        var selectResult144 = enumerateArrayResult143.Select(item =>
        {
            var getStringResult97 = item.GetString();
            return getStringResult97;
        });
        Assert.DoesNotContain("sourceHash", selectResult144);
        var evaluatedValue149 = resolveGetPropertyResult145()[1];
        var getPropertyResult150 = evaluatedValue149.GetProperty("type");
        var getStringResult151 = getPropertyResult150.GetString();
        Assert.Equal("null", getStringResult151);
        JsonElement resolveGetPropertyResult108()
        {
            var evaluatedValue113 = resolveGetPropertyResult109()[0];
            var getPropertyResult114 = evaluatedValue113.GetProperty("oneOf");
            return getPropertyResult114;
            JsonElement resolveGetPropertyResult109()
            {
                var getPropertyResult110 = connection.GetProperty("properties");
                var getPropertyResult111 = getPropertyResult110.GetProperty("dataSource");
                var getPropertyResult112 = getPropertyResult111.GetProperty("oneOf");
                return getPropertyResult112;
            }
        }

        JsonElement resolveGetPropertyResult118()
        {
            var getPropertyResult119 = connection.GetProperty("properties");
            var getPropertyResult120 = getPropertyResult119.GetProperty("dataSource");
            var getPropertyResult121 = getPropertyResult120.GetProperty("oneOf");
            return getPropertyResult121;
        }

        JsonElement resolveGetPropertyResult126()
        {
            var getPropertyResult127 = runPolicy.GetProperty("properties");
            var getPropertyResult128 = getPropertyResult127.GetProperty("runPolicy");
            var getPropertyResult129 = getPropertyResult128.GetProperty("properties");
            var getPropertyResult130 = getPropertyResult129.GetProperty("concurrencyMode");
            var getPropertyResult131 = getPropertyResult130.GetProperty("oneOf");
            return getPropertyResult131;
        }

        JsonElement resolveGetPropertyResult145()
        {
            var getPropertyResult146 = script.GetProperty("properties");
            var getPropertyResult147 = getPropertyResult146.GetProperty("sourceHash");
            var getPropertyResult148 = getPropertyResult147.GetProperty("oneOf");
            return getPropertyResult148;
        }
    }

    [Fact]
    public async Task ToolCatalogPublishesCanonicalScalarAndPermissionEnums()
    {
        using var host = CreateHost();
        var getRequiredServiceResult155 = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var tools = await getRequiredServiceResult155.ListToolsAsync(CancellationToken.None);
        var topology = tools.Single(item => item.Name == "sereinflow_get_flow_topology");
        var getPropertyResult156 = topology.InputSchema.GetProperty("properties");
        var getPropertyResult157 = getPropertyResult156.GetProperty("track");
        var getPropertyResult158 = getPropertyResult157.GetProperty("enum");
        var enumerateArrayResult159 = getPropertyResult158.EnumerateArray();
        var selectResult160 = enumerateArrayResult159.Select(item =>
        {
            var evaluatedValue152 = item.GetString()!;
            return evaluatedValue152;
        });
        Assert.Equal(["development", "production"], selectResult160);
        var runList = tools.Single(item => item.Name == "sereinflow_list_runs");
        var getPropertyResult161 = runList.InputSchema.GetProperty("properties");
        var getPropertyResult162 = getPropertyResult161.GetProperty("status");
        var getPropertyResult163 = getPropertyResult162.GetProperty("enum");
        var enumerateArrayResult164 = getPropertyResult163.EnumerateArray();
        var selectResult165 = enumerateArrayResult164.Select(item =>
        {
            var evaluatedValue153 = item.GetString()!;
            return evaluatedValue153;
        });
        Assert.Equal(["pending", "running", "succeeded", "failed", "cancelled", "timedOut", "interrupted"], selectResult165);
        var apiKey = tools.Single(item => item.Name == "sereinflow_create_mcp_api_key");
        var selectResult167 = Enum.GetValues<McpPermissionDto>()
            .Select(McpPermissionNames.ToName);
        var getPropertyResult168 = apiKey.InputSchema.GetProperty("properties");
        var getPropertyResult169 = getPropertyResult168.GetProperty("permissions");
        var getPropertyResult170 = getPropertyResult169.GetProperty("items");
        var getPropertyResult171 = getPropertyResult170.GetProperty("enum");
        var enumerateArrayResult172 = getPropertyResult171.EnumerateArray();
        var selectResult173 = enumerateArrayResult172.Select(item =>
        {
            var evaluatedValue154 = item.GetString()!;
            return evaluatedValue154;
        });
        Assert.Equal(selectResult167, selectResult173);
    }

    [Fact]
    public async Task FlowPatchCanAddAndRemoveAParameterThroughMcp()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        var toHashSetResult175 = Enum.GetValues<McpPermissionDto>()
            .ToHashSet();
        accessor.Current = new McpPrincipal("parameter-patch-admin", null, toHashSetResult175, IsAdministrator: true);
        using var dataScope = host.Services.CreateScope();
        var project = Project.Create("MCP parameter patch project");
        var getRequiredServiceResult176 = dataScope.ServiceProvider.GetRequiredService<IProjectRepository>();
        await getRequiredServiceResult176.AddAsync(project);
        var flow = CreateFlow();
        var getRequiredServiceResult177 = dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        await getRequiredServiceResult177.AddAsync(project.Id, flow);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var parameter = new
        {
            name = "values",
            valueJson = "",
            source = "literal",
            required = false,
            ui = new
            {
                id = "values",
                nameKey = "values",
                valueKind = "System.Int32",
                literalValue = "",
                isVariadic = true,
                variadicGroupId = "values",
                elementType = "System.Int32",
                variadicMode = "expanded",
            }
        };
        var serializeToElementResult178 = JsonSerializer.SerializeToElement(new { projectId = project.Id, flowId = flow.Id, expectedDevelopmentVersion = flow.Version, schemaVersion = "2.0", operations = new[] { new { op = "addNodeParameter", canvasId = "main", nodeId = "script-node", parameter, }, }, });
        var addPreview = await backend.CallToolAsync("sereinflow_preview_flow_patch", serializeToElementResult178, CancellationToken.None);
        var serializeResult = JsonSerializer.Serialize(addPreview.Value, JsonOptions);
        using var addDocument = JsonDocument.Parse(serializeResult);
        var getPropertyResult179 = addDocument.RootElement.GetProperty("previewId");
        var addPreviewId = getPropertyResult179.GetGuid();
        var getPropertyResult180 = addDocument.RootElement.GetProperty("previewFingerprint");
        var addFingerprint = getPropertyResult180.GetString();
        var serializeToElementResult181 = JsonSerializer.SerializeToElement(new { previewId = addPreviewId, previewFingerprint = addFingerprint, confirmation = "APPLY", idempotencyKey = "parameter-add-once", });
        await backend.CallToolAsync("sereinflow_apply_flow_patch", serializeToElementResult181, CancellationToken.None);
        var getRequiredServiceResult182 = dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        var afterAdd = await getRequiredServiceResult182.FindAsync(project.Id, flow.Id);
        var evaluatedValue183 = Assert.Single(afterAdd!.Canvases).Nodes;
        var evaluatedValue184 = evaluatedValue183.Single().Parameters;
        Assert.Contains(evaluatedValue184, item => item.Ui!.Id == "values");
        var serializeToElementResult185 = JsonSerializer.SerializeToElement(new { projectId = project.Id, flowId = flow.Id, expectedDevelopmentVersion = afterAdd.Version, schemaVersion = "2.0", operations = new[] { new { op = "removeNodeParameter", canvasId = "main", nodeId = "script-node", parameterId = "values", }, }, });
        var removePreview = await backend.CallToolAsync("sereinflow_preview_flow_patch", serializeToElementResult185, CancellationToken.None);
        var serializeResult186 = JsonSerializer.Serialize(removePreview.Value, JsonOptions);
        using var removeDocument = JsonDocument.Parse(serializeResult186);
        var getPropertyResult187 = removeDocument.RootElement.GetProperty("previewId");
        var getGuidResult = getPropertyResult187.GetGuid();
        var getPropertyResult188 = removeDocument.RootElement.GetProperty("previewFingerprint");
        var getStringResult189 = getPropertyResult188.GetString();
        var evaluatedValue190 = new
        {
            previewId = getGuidResult,
            previewFingerprint = getStringResult189,
            confirmation = "APPLY",
            idempotencyKey = "parameter-remove-once"
        };
        var serializeToElementResult191 = JsonSerializer.SerializeToElement(evaluatedValue190);
        await backend.CallToolAsync("sereinflow_apply_flow_patch", serializeToElementResult191, CancellationToken.None);
        var getRequiredServiceResult192 = dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        var afterRemove = await getRequiredServiceResult192.FindAsync(project.Id, flow.Id);
        var evaluatedValue193 = Assert.Single(afterRemove!.Canvases).Nodes;
        var evaluatedValue194 = evaluatedValue193.Single().Parameters;
        Assert.DoesNotContain(evaluatedValue194, item => item.Ui!.Id == "values");
    }

    [Fact]
    public async Task FlowPatchCanRemoveTheEntryNodeAndPersistAnEmptyDraftThroughMcp()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        var toHashSetResult196 = Enum.GetValues<McpPermissionDto>()
            .ToHashSet();
        accessor.Current = new McpPrincipal("empty-draft-admin", null, toHashSetResult196, IsAdministrator: true);
        using var dataScope = host.Services.CreateScope();
        var project = Project.Create("MCP empty draft project");
        var getRequiredServiceResult197 = dataScope.ServiceProvider.GetRequiredService<IProjectRepository>();
        await getRequiredServiceResult197.AddAsync(project);
        var flow = CreateFlow();
        var getRequiredServiceResult198 = dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        await getRequiredServiceResult198.AddAsync(project.Id, flow);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var serializeToElementResult199 = JsonSerializer.SerializeToElement(new { projectId = project.Id, flowId = flow.Id, expectedDevelopmentVersion = flow.Version, schemaVersion = "2.0", operations = new[] { new { op = "removeNode", canvasId = "main", nodeId = "script-node", }, }, });
        var preview = await backend.CallToolAsync("sereinflow_preview_flow_patch", serializeToElementResult199, CancellationToken.None);
        var serializeResult200 = JsonSerializer.Serialize(preview.Value, JsonOptions);
        using var previewDocument = JsonDocument.Parse(serializeResult200);
        var previewRoot = previewDocument.RootElement;
        var getPropertyResult201 = previewRoot.GetProperty("canApply");
        var getBooleanResult202 = getPropertyResult201.GetBoolean();
        Assert.True(getBooleanResult202);
        var getPropertyResult203 = previewRoot.GetProperty("candidateDefinition");
        var getPropertyResult204 = getPropertyResult203.GetProperty("entryNodeId");
        var getStringResult205 = getPropertyResult204.GetString();
        Assert.Equal("", getStringResult205);
        var evaluatedValue209 = resolveGetPropertyResult206()[0];
        var getPropertyResult210 = evaluatedValue209.GetProperty("nodes");
        var enumerateArrayResult211 = getPropertyResult210.EnumerateArray();
        Assert.Empty(enumerateArrayResult211);
        var getPropertyResult212 = previewRoot.GetProperty("previewId");
        var getGuidResult213 = getPropertyResult212.GetGuid();
        var getPropertyResult214 = previewRoot.GetProperty("previewFingerprint");
        var getStringResult215 = getPropertyResult214.GetString();
        var evaluatedValue216 = new
        {
            previewId = getGuidResult213,
            previewFingerprint = getStringResult215,
            confirmation = "APPLY",
            idempotencyKey = "empty-draft-remove-entry-once"
        };
        var serializeToElementResult217 = JsonSerializer.SerializeToElement(evaluatedValue216);
        await backend.CallToolAsync("sereinflow_apply_flow_patch", serializeToElementResult217, CancellationToken.None);
        var getRequiredServiceResult218 = dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        var saved = await getRequiredServiceResult218.FindAsync(project.Id, flow.Id);
        Assert.NotNull(saved);
        Assert.Empty(saved!.EntryNodeId);
        var evaluatedValue219 = Assert.Single(saved.Canvases).Nodes;
        Assert.Empty(evaluatedValue219);
        JsonElement resolveGetPropertyResult206()
        {
            var getPropertyResult207 = previewRoot.GetProperty("candidateDefinition");
            var getPropertyResult208 = getPropertyResult207.GetProperty("canvases");
            return getPropertyResult208;
        }
    }

    [Fact]
    public async Task FlowPatchV2NormalizesLegacySnakeCaseAndNumericEnums()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        var toHashSetResult221 = Enum.GetValues<McpPermissionDto>()
            .ToHashSet();
        accessor.Current = new McpPrincipal("integration-admin", null, toHashSetResult221, IsAdministrator: true);
        using var dataScope = host.Services.CreateScope();
        var project = Project.Create("MCP canonical patch project");
        var getRequiredServiceResult222 = dataScope.ServiceProvider.GetRequiredService<IProjectRepository>();
        await getRequiredServiceResult222.AddAsync(project);
        var flow = CreateFlow();
        var getRequiredServiceResult223 = dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        await getRequiredServiceResult223.AddAsync(project.Id, flow);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var serializeToElementResult224 = JsonSerializer.SerializeToElement(new { projectId = project.Id, flowId = flow.Id, expectedDevelopmentVersion = flow.Version, schemaVersion = "2.0", operations = new[] { new { op = "setRunPolicy", runPolicy = new { concurrencyMode = "exclusiveReject" }, }, }, });
        var canonical = await backend.CallToolAsync("sereinflow_preview_flow_patch", serializeToElementResult224, CancellationToken.None);
        var serializeToElementResult225 = JsonSerializer.SerializeToElement(new { projectId = project.Id, flowId = flow.Id, expectedDevelopmentVersion = flow.Version, schemaVersion = "1.0", operations = new[] { new { operation = "set_run_policy", value = new { concurrencyMode = 1 }, }, }, });
        var legacy = await backend.CallToolAsync("sereinflow_preview_flow_patch", serializeToElementResult225, CancellationToken.None);
        var serializeResult226 = JsonSerializer.Serialize(canonical.Value, JsonOptions);
        using var canonicalDocument = JsonDocument.Parse(serializeResult226);
        var serializeResult227 = JsonSerializer.Serialize(legacy.Value, JsonOptions);
        using var legacyDocument = JsonDocument.Parse(serializeResult227);
        var canonicalRoot = canonicalDocument.RootElement;
        var legacyRoot = legacyDocument.RootElement;
        var getPropertyResult228 = canonicalRoot.GetProperty("schemaVersion");
        var getStringResult229 = getPropertyResult228.GetString();
        Assert.Equal("2.0", getStringResult229);
        var getPropertyResult230 = canonicalRoot.GetProperty("enumEncoding");
        var getStringResult231 = getPropertyResult230.GetString();
        Assert.Equal("camelCase", getStringResult231);
        var evaluatedValue232 = canonicalRoot.GetProperty("normalizedOperations")[0];
        var getPropertyResult233 = evaluatedValue232.GetProperty("op");
        var getStringResult234 = getPropertyResult233.GetString();
        Assert.Equal("setRunPolicy", getStringResult234);
        var evaluatedValue235 = canonicalRoot.GetProperty("normalizedOperations")[0];
        var getPropertyResult236 = evaluatedValue235.GetProperty("runPolicy");
        var getPropertyResult237 = getPropertyResult236.GetProperty("concurrencyMode");
        var getStringResult238 = getPropertyResult237.GetString();
        Assert.Equal("exclusiveReject", getStringResult238);
        var getPropertyResult239 = canonicalRoot.GetProperty("diff");
        var getPropertyResult240 = getPropertyResult239.GetProperty("candidateChecksum");
        var getStringResult241 = getPropertyResult240.GetString();
        var getPropertyResult242 = legacyRoot.GetProperty("diff");
        var getPropertyResult243 = getPropertyResult242.GetProperty("candidateChecksum");
        var getStringResult244 = getPropertyResult243.GetString();
        Assert.Equal(getStringResult241, getStringResult244);
        var evaluatedValue247 = resolveGetArrayLengthResult() > 0;
        Assert.True(evaluatedValue247);
        int resolveGetArrayLengthResult()
        {
            var getPropertyResult245 = legacyRoot.GetProperty("normalizationWarnings");
            var getArrayLengthResult246 = getPropertyResult245.GetArrayLength();
            return getArrayLengthResult246;
        }
    }

    [Fact]
    public async Task FlowPatchV2RejectsUnknownFieldsWithStructuredDiagnostic()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        var toHashSetResult254 = Enum.GetValues<McpPermissionDto>()
            .ToHashSet();
        accessor.Current = new McpPrincipal("integration-admin", null, toHashSetResult254, IsAdministrator: true);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var exception = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            var newGuidResult248 = Guid.NewGuid();
            var newGuidResult249 = Guid.NewGuid();
            var evaluatedValue250 = new
            {
                projectId = newGuidResult248,
                flowId = newGuidResult249,
                expectedDevelopmentVersion = 1,
                schemaVersion = "2.0",
                operations = new[]
                {
                    new
                    {
                        op = "setRunPolicy",
                        runPolicy = new
                        {
                            concurrencyMode = "exclusiveReject"
                        },
                        ignored = true,
                    },
                }
            };
            var serializeToElementResult251 = JsonSerializer.SerializeToElement(evaluatedValue250);
            var callToolAsyncResult252 = backend.CallToolAsync("sereinflow_preview_flow_patch", serializeToElementResult251, CancellationToken.None);
            return callToolAsyncResult252;
        });
        Assert.Equal(McpProtocolErrorCodes.InvalidParams, exception.Code);
        var diagnostic = JsonSerializer.SerializeToElement(exception.ErrorData, JsonOptions);
        var getPropertyResult255 = diagnostic.GetProperty("code");
        var getStringResult256 = getPropertyResult255.GetString();
        Assert.Equal(McpErrorCodes.FlowPatchUnexpectedField, getStringResult256);
        var getPropertyResult257 = diagnostic.GetProperty("fieldPath");
        var getStringResult258 = getPropertyResult257.GetString();
        Assert.Equal("$.operations[0].ignored", getStringResult258);
        var getPropertyResult259 = diagnostic.GetProperty("schemaVersion");
        var getStringResult260 = getPropertyResult259.GetString();
        Assert.Equal("2.0", getStringResult260);
        var getPropertyResult261 = diagnostic.GetProperty("diagnosticId");
        var getStringResult262 = getPropertyResult261.GetString();
        var isNullOrWhiteSpaceResult = string.IsNullOrWhiteSpace(getStringResult262);
        Assert.False(isNullOrWhiteSpaceResult);
    }

    [Fact]
    public async Task LibraryNodeTemplateToolPublishesAReadOnlyTypedSchema()
    {
        using var host = CreateHost();
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var evaluatedValue264 = (await backend.ListToolsAsync(CancellationToken.None));
        var tool = evaluatedValue264.Single(item => item.Name == "sereinflow_create_library_node_template");
        var properties = tool.InputSchema.GetProperty("properties");
        var getPropertyResult265 = properties.GetProperty("position");
        var getPropertyResult266 = getPropertyResult265.GetProperty("type");
        var getStringResult267 = getPropertyResult266.GetString();
        Assert.Equal("object", getStringResult267);
        var getPropertyResult268 = properties.GetProperty("position");
        var getPropertyResult269 = getPropertyResult268.GetProperty("properties");
        var getPropertyResult270 = getPropertyResult269.GetProperty("x");
        var getPropertyResult271 = getPropertyResult270.GetProperty("type");
        var getStringResult272 = getPropertyResult271.GetString();
        Assert.Equal("number", getStringResult272);
        var getPropertyResult273 = tool.InputSchema.GetProperty("required");
        var enumerateArrayResult274 = getPropertyResult273.EnumerateArray();
        var selectResult275 = enumerateArrayResult274.Select(static item =>
        {
            var getStringResult263 = item.GetString();
            return getStringResult263;
        });
        Assert.Contains("projectId", selectResult275);
    }

    [Fact]
    public async Task BuiltinNodeTemplateToolPublishesAReadOnlyTypedSchema()
    {
        using var host = CreateHost();
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var evaluatedValue277 = (await backend.ListToolsAsync(CancellationToken.None));
        var tool = evaluatedValue277.Single(item => item.Name == "sereinflow_create_builtin_node_template");
        var properties = tool.InputSchema.GetProperty("properties");
        var getPropertyResult278 = properties.GetProperty("builtinNodeId");
        var getPropertyResult279 = getPropertyResult278.GetProperty("type");
        var getStringResult280 = getPropertyResult279.GetString();
        Assert.Equal("string", getStringResult280);
        var getPropertyResult281 = properties.GetProperty("position");
        var getPropertyResult282 = getPropertyResult281.GetProperty("type");
        var getStringResult283 = getPropertyResult282.GetString();
        Assert.Equal("object", getStringResult283);
        var getPropertyResult284 = tool.InputSchema.GetProperty("required");
        var enumerateArrayResult285 = getPropertyResult284.EnumerateArray();
        var selectResult286 = enumerateArrayResult285.Select(static item =>
        {
            var getStringResult276 = item.GetString();
            return getStringResult276;
        });
        Assert.Contains("builtinNodeId", selectResult286);
    }

    [Fact]
    public async Task FlowPatchAcceptsStringEnumsAndApplyReadsBackAuthoritativeState()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        var toHashSetResult288 = Enum.GetValues<McpPermissionDto>()
            .ToHashSet();
        accessor.Current = new McpPrincipal("integration-admin", null, toHashSetResult288, IsAdministrator: true);
        using var dataScope = host.Services.CreateScope();
        var project = Project.Create("MCP enum patch project");
        var getRequiredServiceResult289 = dataScope.ServiceProvider.GetRequiredService<IProjectRepository>();
        await getRequiredServiceResult289.AddAsync(project);
        var flow = CreateFlow();
        var getRequiredServiceResult290 = dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        await getRequiredServiceResult290.AddAsync(project.Id, flow);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var serializeToElementResult291 = JsonSerializer.SerializeToElement(new { projectId = project.Id, flowId = flow.Id, expectedDevelopmentVersion = flow.Version, operations = new[] { new { operation = "setRunPolicy", value = new { concurrencyMode = "exclusiveReject" }, }, }, });
        var previewResult = await backend.CallToolAsync("sereinflow_preview_flow_patch", serializeToElementResult291, CancellationToken.None);
        var serializeResult292 = JsonSerializer.Serialize(previewResult.Value, JsonOptions);
        using var previewDocument = JsonDocument.Parse(serializeResult292);
        var getPropertyResult293 = previewDocument.RootElement.GetProperty("previewId");
        var previewId = getPropertyResult293.GetGuid();
        var fingerprint = resolveGetStringResult294()!;
        var serializeToElementResult297 = JsonSerializer.SerializeToElement(new { previewId, previewFingerprint = fingerprint, confirmation = "APPLY", idempotencyKey = "enum-patch-once", });
        await backend.CallToolAsync("sereinflow_apply_flow_patch", serializeToElementResult297, CancellationToken.None);
        var getRequiredServiceResult298 = dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        var persisted = await getRequiredServiceResult298.FindAsync(project.Id, flow.Id);
        Assert.NotNull(persisted);
        Assert.Equal(2, persisted!.Version);
        Assert.Equal(FlowConcurrencyModeDto.ExclusiveReject, persisted.RunPolicy!.ConcurrencyMode);
        var getChecksumResult = FlowDiffService.GetChecksum(persisted);
        Assert.Equal(getChecksumResult, persisted.Checksum);
        string? resolveGetStringResult294()
        {
            var getPropertyResult295 = previewDocument.RootElement.GetProperty("previewFingerprint");
            var getStringResult296 = getPropertyResult295.GetString();
            return getStringResult296;
        }
    }

    [Fact]
    public async Task MalformedContractArgumentsReturnInvalidParamsInsteadOfInternalError()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        var toHashSetResult305 = Enum.GetValues<McpPermissionDto>()
            .ToHashSet();
        accessor.Current = new McpPrincipal("integration-admin", null, toHashSetResult305, IsAdministrator: true);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var exception = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            var newGuidResult299 = Guid.NewGuid();
            var newGuidResult300 = Guid.NewGuid();
            var emptyResult = Array.Empty<object>();
            var evaluatedValue301 = new
            {
                projectId = newGuidResult299,
                flowId = newGuidResult300,
                expectedDevelopmentVersion = "one",
                operations = emptyResult
            };
            var serializeToElementResult302 = JsonSerializer.SerializeToElement(evaluatedValue301);
            var callToolAsyncResult303 = backend.CallToolAsync("sereinflow_preview_flow_patch", serializeToElementResult302, CancellationToken.None);
            return callToolAsyncResult303;
        });
        Assert.Equal(McpProtocolErrorCodes.InvalidParams, exception.Code);
        var evaluatedValue306 = ((JsonElement)JsonSerializer.SerializeToElement(exception.ErrorData, JsonOptions));
        var getPropertyResult307 = evaluatedValue306.GetProperty("code");
        var getStringResult308 = getPropertyResult307.GetString();
        Assert.Equal(McpErrorCodes.FlowPatchFieldInvalid, getStringResult308);
    }

    private static readonly string[] ProjectReadPermissionNames = ["project.read"];
    [Fact]
    public async Task ProjectCreatePreviewApplyCreatesEmptyDraftProjectAndIsIdempotent()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        var toHashSetResult311 = Enum.GetValues<McpPermissionDto>()
            .ToHashSet();
        accessor.Current = new McpPrincipal("project-admin", null, toHashSetResult311, IsAdministrator: true);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var serializeToElementResult312 = JsonSerializer.SerializeToElement(new { name = "OpenCV demo", flowName = "Main flow" });
        var evaluatedValue313 = (await backend.CallToolAsync("sereinflow_preview_create_project", serializeToElementResult312, CancellationToken.None)).Value;
        var preview = Deserialize<ProjectCreatePreviewDto>(evaluatedValue313);
        Assert.True(preview.CanApply);
        Assert.True(preview.Validation.IsValid);
        Assert.Equal("draft", preview.Workspace.Project.Status);
        Assert.Equal("Main flow", preview.FlowName);
        Assert.Equal(preview.ProjectId, preview.Workspace.Project.Id);
        var evaluatedValue314 = preview.Workspace.Flows.Single().Id;
        Assert.Equal(preview.FlowId, evaluatedValue314);
        var evaluatedValue315 = (await backend.ReadResourceAsync($"sereinflow://mcp-previews/{preview.PreviewId:D}", CancellationToken.None)).Value;
        var previewResource = Deserialize<ProjectCreatePreviewDto>(evaluatedValue315);
        Assert.Equal(preview.PreviewFingerprint, previewResource.PreviewFingerprint);
        Assert.Equal(preview.ProjectId, previewResource.ProjectId);
        using (var dataScope = host.Services.CreateScope())
        {
            var getRequiredServiceResult309 = dataScope.ServiceProvider.GetRequiredService<IProjectRepository>();
            var findAsyncResult = await getRequiredServiceResult309.FindAsync(preview.ProjectId);
            Assert.Null(findAsyncResult);
        }

        var applyArguments = JsonSerializer.SerializeToElement(new { previewId = preview.PreviewId, previewFingerprint = preview.PreviewFingerprint, confirmation = "APPLY", idempotencyKey = "create-project-once", });
        var evaluatedValue316 = (await backend.CallToolAsync("sereinflow_apply_create_project", applyArguments, CancellationToken.None)).Value;
        var created = Deserialize<ProjectWorkspaceDto>(evaluatedValue316);
        Assert.Equal(preview.ProjectId, created.Project.Id);
        Assert.Equal("OpenCV demo", created.Project.Name);
        var evaluatedValue317 = created.Flows.Single().Id;
        Assert.Equal(preview.FlowId, evaluatedValue317);
        var evaluatedValue318 = created.Flows.Single().NodeCount;
        Assert.Equal(0, evaluatedValue318);
        var evaluatedValue319 = created.Flows.Single().EntryNodeId;
        Assert.Equal(string.Empty, evaluatedValue319);
        var evaluatedValue320 = (await backend.CallToolAsync("sereinflow_apply_create_project", applyArguments, CancellationToken.None)).Value;
        var replay = Deserialize<ProjectWorkspaceDto>(evaluatedValue320);
        var serializeToElementResult321 = JsonSerializer.SerializeToElement(created, JsonOptions);
        var serializeToElementResult322 = JsonSerializer.SerializeToElement(replay, JsonOptions);
        var deepEqualsResult = JsonElement.DeepEquals(serializeToElementResult321, serializeToElementResult322);
        Assert.True(deepEqualsResult);
        using var verifyScope = host.Services.CreateScope();
        var getRequiredServiceResult323 = verifyScope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var project = await getRequiredServiceResult323.FindAsync(preview.ProjectId);
        var getRequiredServiceResult324 = verifyScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        var flow = await getRequiredServiceResult324.FindAsync(preview.ProjectId, preview.FlowId);
        Assert.NotNull(project);
        Assert.NotNull(flow);
        Assert.Equal(1, flow!.Version);
    }

    [Fact]
    public async Task ProjectCreateRequiresAnAdministratorEvenWhenProjectWriteIsGranted()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        var newGuidResult327 = Guid.NewGuid();
        var toHashSetResult328 = new[]
        {
            McpPermissionDto.ProjectWrite
        }.ToHashSet();
        accessor.Current = new McpPrincipal("project-scoped-key", newGuidResult327, toHashSetResult328);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var exception = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            var serializeToElementResult325 = JsonSerializer.SerializeToElement(new { name = "Not allowed" });
            var callToolAsyncResult326 = backend.CallToolAsync("sereinflow_preview_create_project", serializeToElementResult325, CancellationToken.None);
            return callToolAsyncResult326;
        });
        Assert.Equal(McpProtocolErrorCodes.PermissionDenied, exception.Code);
    }

    [Fact]
    public async Task ApiKeyLifecycleIsIdempotentAndReturnsSecretsOnlyOnce()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        var toHashSetResult331 = Enum.GetValues<McpPermissionDto>()
            .ToHashSet();
        accessor.Current = new McpPrincipal("key-admin", null, toHashSetResult331, IsAdministrator: true);
        using var dataScope = host.Services.CreateScope();
        var project = Project.Create("MCP key project");
        var getRequiredServiceResult332 = dataScope.ServiceProvider.GetRequiredService<IProjectRepository>();
        await getRequiredServiceResult332.AddAsync(project);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var createArguments = JsonSerializer.SerializeToElement(new { projectId = project.Id, name = "project-key", permissions = ProjectReadPermissionNames, idempotencyKey = "create-key-once", });
        var evaluatedValue333 = (await backend.CallToolAsync("sereinflow_create_mcp_api_key", createArguments, CancellationToken.None)).Value;
        var created = Deserialize<CreatedMcpApiKeyDto>(evaluatedValue333);
        Assert.NotNull(created.Secret);
        var evaluatedValue334 = (await backend.CallToolAsync("sereinflow_create_mcp_api_key", createArguments, CancellationToken.None)).Value;
        var serializeResult335 = JsonSerializer.Serialize(evaluatedValue334, JsonOptions);
        var replayDocument = JsonDocument.Parse(serializeResult335);
        var getPropertyResult336 = replayDocument.RootElement.GetProperty("secret");
        var getStringResult337 = getPropertyResult336.GetString();
        Assert.Null(getStringResult337);
        var getPropertyResult338 = replayDocument.RootElement.GetProperty("replayed");
        var getBooleanResult339 = getPropertyResult338.GetBoolean();
        Assert.True(getBooleanResult339);
        var rotateArguments = JsonSerializer.SerializeToElement(new { keyId = created.Key.Id, idempotencyKey = "rotate-key-once", });
        var evaluatedValue340 = (await backend.CallToolAsync("sereinflow_rotate_mcp_api_key", rotateArguments, CancellationToken.None)).Value;
        var rotated = Deserialize<RotatedMcpApiKeyDto>(evaluatedValue340);
        Assert.Equal(created.Key.Id, rotated.RevokedKeyId);
        Assert.NotNull(rotated.Secret);
        var security = dataScope.ServiceProvider.GetRequiredService<McpSecurityService>();
        var authenticateAsyncResult = await security.AuthenticateAsync(created.Secret);
        Assert.Null(authenticateAsyncResult);
        var authenticateAsyncResult341 = await security.AuthenticateAsync(rotated.Secret);
        Assert.NotNull(authenticateAsyncResult341);
        var evaluatedValue342 = (await backend.CallToolAsync("sereinflow_rotate_mcp_api_key", rotateArguments, CancellationToken.None)).Value;
        var rotatedReplay = Deserialize<RotatedMcpApiKeyDto>(evaluatedValue342);
        Assert.True(rotatedReplay.Replayed);
        Assert.Null(rotatedReplay.Secret);
        var revokeArguments = JsonSerializer.SerializeToElement(new { keyId = rotated.Key.Id, idempotencyKey = "revoke-key-once", });
        await backend.CallToolAsync("sereinflow_revoke_mcp_api_key", revokeArguments, CancellationToken.None);
        var authenticateAsyncResult343 = await security.AuthenticateAsync(rotated.Secret);
        Assert.Null(authenticateAsyncResult343);
        var getRequiredServiceResult344 = dataScope.ServiceProvider.GetRequiredService<IRepository<McpAuditRecord>>();
        var auditRecords = await getRequiredServiceResult344.ListAsync();
        Assert.DoesNotContain(auditRecords, record =>
        {
            var evaluatedValue329 = record.Summary?.Contains(created.Secret, StringComparison.Ordinal) == true || record.Summary?.Contains(rotated.Secret, StringComparison.Ordinal) == true;
            return evaluatedValue329;
        });
    }

    [Fact]
    public async Task ApiKeyUsageUpdatesPreserveAuthorizationAndRevocationFields()
    {
        using var host = CreateHost();
        using var scope = host.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMcpApiKeyStore>();
        var project = Project.Create("MCP key usage tracking");
        await scope.ServiceProvider.GetRequiredService<IProjectRepository>().AddAsync(project);
        var created = McpSecurityService.CreateKeyWithEntry(new CreateMcpApiKeyRequestDto(
            project.Id, "tracked-key", [McpPermissionDto.ProjectRead]));
        await store.AddAsync(created.Entry);
        var revoked = created.Entry with { RevokedAt = DateTimeOffset.UtcNow };
        await store.UpdateAsync(revoked);
        var lastUsedAt = DateTimeOffset.UtcNow;

        Assert.True(await store.UpdateLastUsedAsync(created.Entry.Id, lastUsedAt));

        var persisted = await store.FindAsync(created.Entry.Id);
        Assert.NotNull(persisted);
        var expectedJson = JsonSerializer.Serialize(revoked with { LastUsedAt = lastUsedAt }, JsonOptions);
        var persistedJson = JsonSerializer.Serialize(persisted, JsonOptions);
        Assert.Equal(expectedJson, persistedJson);
    }

    [Theory]
    [InlineData("sereinflow_apply_publish_flow")]
    [InlineData("sereinflow_apply_rollback_flow")]
    public async Task ReleaseToolsRejectPatchPreviewsBeforeChangingFlowState(string toolName)
    {
        using var host = CreateHost();
        var principal = new McpPrincipal("release-admin", null, new HashSet<McpPermissionDto>(), IsAdministrator: true);
        host.Services.GetRequiredService<IMcpPrincipalAccessor>().Current = principal;
        using var scope = host.Services.CreateScope();
        var project = Project.Create("MCP preview isolation");
        await scope.ServiceProvider.GetRequiredService<IProjectRepository>().AddAsync(project);
        var flow = CreateFlow();
        var flows = scope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        await flows.AddAsync(project.Id, flow);
        var previews = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var preview = await previews.CreateAsync("flow.patch", principal, project.Id, flow.Id, flow.Version, new
        {
            request = new { projectId = project.Id, flowId = flow.Id, expectedDevelopmentVersion = flow.Version },
            validation = new FlowValidationResultDto(true, []),
            diff = new FlowDiffService().Compare(flow, flow)
        });
        var arguments = JsonSerializer.SerializeToElement(new
        {
            previewId = preview.Id, previewFingerprint = preview.PreviewFingerprint,
            confirmation = "APPLY", idempotencyKey = "wrong-operation-apply"
        });
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();

        var error = await Assert.ThrowsAsync<McpProtocolException>(() =>
            backend.CallToolAsync(toolName, arguments, CancellationToken.None));

        Assert.Equal(McpProtocolErrorCodes.InvalidParams, error.Code);
        var versions = scope.ServiceProvider.GetRequiredService<IFlowVersionRepository>();
        Assert.Null(await versions.FindProductionVersionAsync(project.Id, flow.Id));
        Assert.Equal(flow.Version, (await flows.FindAsync(project.Id, flow.Id))!.Version);
        var persistedPreview = await scope.ServiceProvider.GetRequiredService<IMcpPreviewStore>().FindAsync(preview.Id);
        Assert.Equal(McpMutationPreviewStatusDto.Pending, persistedPreview!.Status);
    }

    [Fact]
    public async Task PublishPreviewApplyAndReplayUseTheSameProductionGuard()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        var toHashSetResult346 = Enum.GetValues<McpPermissionDto>()
            .ToHashSet();
        accessor.Current = new McpPrincipal("integration-admin", null, toHashSetResult346, IsAdministrator: true);
        using var dataScope = host.Services.CreateScope();
        var project = Project.Create("MCP publish project");
        var projectRepository = dataScope.ServiceProvider.GetRequiredService<IProjectRepository>();
        await projectRepository.AddAsync(project);
        var flow = CreateFlow();
        var getRequiredServiceResult347 = dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        await getRequiredServiceResult347.AddAsync(project.Id, flow);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var serializeToElementResult348 = JsonSerializer.SerializeToElement(new { projectId = project.Id, flowId = flow.Id, expectedDevelopmentVersion = flow.Version, });
        var preview = await backend.CallToolAsync("sereinflow_preview_publish_flow", serializeToElementResult348, CancellationToken.None);
        var serializeResult349 = JsonSerializer.Serialize(preview.Value, JsonOptions);
        var previewDocument = JsonDocument.Parse(serializeResult349);
        var getPropertyResult350 = previewDocument.RootElement.GetProperty("previewId");
        var previewId = getPropertyResult350.GetGuid();
        var fingerprint = resolveGetStringResult351()!;
        var serializeToElementResult354 = JsonSerializer.SerializeToElement(new { previewId, previewFingerprint = fingerprint, confirmation = "APPLY", idempotencyKey = "publish-once", });
        var published = await backend.CallToolAsync("sereinflow_apply_publish_flow", serializeToElementResult354, CancellationToken.None);
        Assert.NotNull(published.Value);
        var serializeToElementResult355 = JsonSerializer.SerializeToElement(new { previewId, previewFingerprint = fingerprint, confirmation = "APPLY", idempotencyKey = "publish-once", });
        var replay = await backend.CallToolAsync("sereinflow_apply_publish_flow", serializeToElementResult355, CancellationToken.None);
        var serializeResult356 = JsonSerializer.Serialize(published.Value, JsonOptions);
        using var publishedDocument = JsonDocument.Parse(serializeResult356);
        var serializeResult357 = JsonSerializer.Serialize(replay.Value, JsonOptions);
        using var replayDocument = JsonDocument.Parse(serializeResult357);
        var deepEqualsResult358 = JsonElement.DeepEquals(publishedDocument.RootElement, replayDocument.RootElement);
        Assert.True(deepEqualsResult358);
        var getRequiredServiceResult359 = dataScope.ServiceProvider.GetRequiredService<IRepository<McpAuditRecord>>();
        var auditRecords = await getRequiredServiceResult359.ListAsync(item => item.Operation == "sereinflow_apply_publish_flow");
        Assert.NotEmpty(auditRecords);
        Assert.All(auditRecords, record =>
        {
            var hashKeyResult = McpIdempotencyService.HashKey("publish-once");
            Assert.Equal(hashKeyResult, record.RequestHash);
        });
        string? resolveGetStringResult351()
        {
            var getPropertyResult352 = previewDocument.RootElement.GetProperty("previewFingerprint");
            var getStringResult353 = getPropertyResult352.GetString();
            return getStringResult353;
        }
    }

    [Fact]
    public async Task PublishPreviewRejectsWhenProductionHeadChangesBeforeApply()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        var toHashSetResult368 = Enum.GetValues<McpPermissionDto>()
            .ToHashSet();
        accessor.Current = new McpPrincipal("integration-admin", null, toHashSetResult368, IsAdministrator: true);
        using var dataScope = host.Services.CreateScope();
        var project = Project.Create("MCP conflict project");
        var getRequiredServiceResult369 = dataScope.ServiceProvider.GetRequiredService<IProjectRepository>();
        await getRequiredServiceResult369.AddAsync(project);
        var flow = CreateFlow();
        var flows = dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        await flows.AddAsync(project.Id, flow);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var serializeToElementResult370 = JsonSerializer.SerializeToElement(new { projectId = project.Id, flowId = flow.Id, expectedDevelopmentVersion = flow.Version, });
        var preview = await backend.CallToolAsync("sereinflow_preview_publish_flow", serializeToElementResult370, CancellationToken.None);
        var serializeResult371 = JsonSerializer.Serialize(preview.Value, JsonOptions);
        var previewDocument = JsonDocument.Parse(serializeResult371);
        var versions = dataScope.ServiceProvider.GetRequiredService<IFlowVersionRepository>();
        var directPublish = await versions.PublishAsync(project.Id, flow.Id, flow.Version, remark: "direct publish", expectedProductionVersion: null);
        Assert.True(directPublish.IsCommitted);
        var exception = await Assert.ThrowsAsync<McpProtocolException>(() =>
        {
            var getPropertyResult360 = previewDocument.RootElement.GetProperty("previewId");
            var getGuidResult361 = getPropertyResult360.GetGuid();
            var getPropertyResult362 = previewDocument.RootElement.GetProperty("previewFingerprint");
            var getStringResult363 = getPropertyResult362.GetString();
            var evaluatedValue364 = new
            {
                previewId = getGuidResult361,
                previewFingerprint = getStringResult363,
                confirmation = "APPLY",
                idempotencyKey = "stale-publish"
            };
            var serializeToElementResult365 = JsonSerializer.SerializeToElement(evaluatedValue364);
            var callToolAsyncResult366 = backend.CallToolAsync("sereinflow_apply_publish_flow", serializeToElementResult365, CancellationToken.None);
            return callToolAsyncResult366;
        });
        Assert.Equal(McpProtocolErrorCodes.Conflict, exception.Code);
        var findProductionVersionAsyncResult = await versions.FindProductionVersionAsync(project.Id, flow.Id);
        Assert.Equal(directPublish.Version!.Version, findProductionVersionAsyncResult);
    }

    [Fact]
    public async Task RollbackToolsKeepDevelopmentAndProductionTracksIndependent()
    {
        using var host = CreateHost();
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        var toHashSetResult373 = Enum.GetValues<McpPermissionDto>()
            .ToHashSet();
        accessor.Current = new McpPrincipal("rollback-admin", null, toHashSetResult373, IsAdministrator: true);
        using var dataScope = host.Services.CreateScope();
        var project = Project.Create("MCP rollback project");
        var getRequiredServiceResult374 = dataScope.ServiceProvider.GetRequiredService<IProjectRepository>();
        await getRequiredServiceResult374.AddAsync(project);
        var flow = CreateFlow();
        var flows = dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        await flows.AddAsync(project.Id, flow);
        var versions = dataScope.ServiceProvider.GetRequiredService<IFlowVersionRepository>();
        var firstDevelopmentCandidate = flow with
        {
            Ui = resolveFlowUiMetadataDto(),
        };
        var saved = await flows.TryUpdateAsync(project.Id, firstDevelopmentCandidate, flow.Version);
        Assert.NotNull(saved);
        var published = await versions.PublishAsync(project.Id, flow.Id, saved!.Version, "publish rollback test");
        Assert.True(published.IsCommitted);
        var secondDevelopmentCandidate = saved with
        {
            Ui = resolveFlowUiMetadataDto375(),
        };
        var savedAgain = await flows.TryUpdateAsync(project.Id, secondDevelopmentCandidate, saved.Version);
        Assert.NotNull(savedAgain);
        var publishedAgain = await versions.PublishAsync(project.Id, flow.Id, savedAgain!.Version, "publish rollback head", expectedProductionVersion: published.Version!.Version);
        Assert.True(publishedAgain.IsCommitted);
        var developmentVersionBeforeRollback = (await flows.FindAsync(project.Id, flow.Id))!.Version;
        var productionVersionBeforeRollback = await versions.FindProductionVersionAsync(project.Id, flow.Id);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var serializeToElementResult378 = JsonSerializer.SerializeToElement(new { projectId = project.Id, flowId = flow.Id, track = "production", sourceVersion = published.Version!.Version, expectedHeadVersion = publishedAgain.Version!.Version, });
        var preview = await backend.CallToolAsync("sereinflow_preview_rollback_flow", serializeToElementResult378, CancellationToken.None);
        var serializeResult379 = JsonSerializer.Serialize(preview.Value, JsonOptions);
        var previewDocument = JsonDocument.Parse(serializeResult379);
        var getPropertyResult380 = previewDocument.RootElement.GetProperty("previewId");
        var getGuidResult381 = getPropertyResult380.GetGuid();
        var getPropertyResult382 = previewDocument.RootElement.GetProperty("previewFingerprint");
        var getStringResult383 = getPropertyResult382.GetString();
        var evaluatedValue384 = new
        {
            previewId = getGuidResult381,
            previewFingerprint = getStringResult383,
            confirmation = "APPLY",
            idempotencyKey = "production-rollback-once"
        };
        var serializeToElementResult385 = JsonSerializer.SerializeToElement(evaluatedValue384);
        var apply = await backend.CallToolAsync("sereinflow_apply_rollback_flow", serializeToElementResult385, CancellationToken.None);
        Assert.NotNull(apply.Value);
        var evaluatedValue386 = (await flows.FindAsync(project.Id, flow.Id))!.Version;
        Assert.Equal(developmentVersionBeforeRollback, evaluatedValue386);
        var findProductionVersionAsyncResult387 = await versions.FindProductionVersionAsync(project.Id, flow.Id);
        Assert.NotEqual(productionVersionBeforeRollback, findProductionVersionAsyncResult387);
        var evaluatedValue388 = (await versions.ListVersionsAsync(project.Id, flow.Id, FlowVersionTrackDto.Production));
        var evaluatedValue389 = evaluatedValue388.Single(item => item.IsCurrent).Track;
        Assert.Equal(FlowVersionTrackDto.Production, evaluatedValue389);
        FlowUiMetadataDto resolveFlowUiMetadataDto()
        {
            var flowConnectionLineTypesDto = new FlowConnectionLineTypesDto("orthogonal", "straight");
            var flowUiMetadataDto = new FlowUiMetadataDto(flowConnectionLineTypesDto);
            return flowUiMetadataDto;
        }

        FlowUiMetadataDto resolveFlowUiMetadataDto375()
        {
            var flowConnectionLineTypesDto376 = new FlowConnectionLineTypesDto("bezier", "straight");
            var flowUiMetadataDto377 = new FlowUiMetadataDto(flowConnectionLineTypesDto376);
            return flowUiMetadataDto377;
        }
    }

    [Fact]
    public async Task ProjectCallerCannotReadAnotherCallersPreviewInTheSameProject()
    {
        using var host = CreateHost();
        using var dataScope = host.Services.CreateScope();
        var project = Project.Create("MCP preview ownership project");
        var getRequiredServiceResult390 = dataScope.ServiceProvider.GetRequiredService<IProjectRepository>();
        await getRequiredServiceResult390.AddAsync(project);
        var flow = CreateFlow();
        var getRequiredServiceResult391 = dataScope.ServiceProvider.GetRequiredService<IFlowDefinitionRepository>();
        await getRequiredServiceResult391.AddAsync(project.Id, flow);
        var accessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        var toHashSetResult392 = new[]
        {
            McpPermissionDto.FlowWrite,
            McpPermissionDto.ProjectRead
        }.ToHashSet();
        accessor.Current = new McpPrincipal("owner-key", project.Id, toHashSetResult392);
        var backend = host.Services.GetRequiredService<SereinFlowMcpBackend>();
        var emptyResult393 = Array.Empty<object>();
        var evaluatedValue394 = new
        {
            projectId = project.Id,
            flowId = flow.Id,
            expectedDevelopmentVersion = flow.Version,
            operations = emptyResult393
        };
        var serializeToElementResult395 = JsonSerializer.SerializeToElement(evaluatedValue394);
        var preview = await backend.CallToolAsync("sereinflow_preview_flow_patch", serializeToElementResult395, CancellationToken.None);
        var serializeResult396 = JsonSerializer.Serialize(preview.Value, JsonOptions);
        using var document = JsonDocument.Parse(serializeResult396);
        var getPropertyResult397 = document.RootElement.GetProperty("previewId");
        var previewId = getPropertyResult397.GetGuid();
        var toHashSetResult398 = new[]
        {
            McpPermissionDto.ProjectRead
        }.ToHashSet();
        accessor.Current = new McpPrincipal("other-key", project.Id, toHashSetResult398);
        var exception = await Assert.ThrowsAsync<McpSecurityException>(() =>
        {
            var readResourceAsyncResult = backend.ReadResourceAsync($"sereinflow://mcp-previews/{previewId:D}", CancellationToken.None);
            return readResourceAsyncResult;
        });
        Assert.Equal(McpErrorCodes.PreviewOwnerMismatch, exception.Code);
    }

    private static TestHost CreateHost(Action<IServiceCollection>? configureServices = null)
    {
        var getTempPathResult = Path.GetTempPath();
        var evaluatedValue400 = $"sereinflow-mcp-test-{Guid.NewGuid():N}";
        var root = Path.Combine(getTempPathResult, evaluatedValue400);
        var configurationBuilder = new ConfigurationBuilder();
        var dictionary = new Dictionary<string, string?>
        {
            ["SereinFlow:DataRoot"] = root,
        };
        var addInMemoryCollectionResult = configurationBuilder.AddInMemoryCollection(dictionary);
        var configuration = addInMemoryCollectionResult.Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSereinFlowStorage(configuration, root);
        services.AddSereinFlowApplication();
        services.AddSereinFlowMcp(configuration);
        configureServices?.Invoke(services);
        services.AddSingleton<SereinFlowMcpBackend>(provider =>
        {
            var evaluatedValue399 = (SereinFlowMcpBackend)provider.GetRequiredService<ISereinFlowMcpBackend>();
            return evaluatedValue399;
        });
        var serviceProviderOptions = new ServiceProviderOptions
        {
            ValidateScopes = true
        };
        var buildServiceProviderResult = services.BuildServiceProvider(serviceProviderOptions);
        var testHost = new TestHost(buildServiceProviderResult, root);
        return testHost;
    }

    private static FlowDefinitionDto CreateFlow()
    {
        var flowId = Guid.NewGuid();
        var source = "return 0";
        var nodeId = "script-node";
        var computeSourceHashResult = SereinFlow.Domain.ScriptNodeDefinition.ComputeSourceHash(source);
        IReadOnlyList<ScriptValueContractDto> evaluatedValue401 = [new ScriptValueContractDto("result", "ScriptLang.Runtime.Value", false, "result")];
        var scriptNodeDataDto = new ScriptNodeDataDto(nodeId, source, "0.1", computeSourceHashResult, [], evaluatedValue401);
        var node = new NodeDto(nodeId, NodeTypeDto.Script, "Script", 0, 0, [], [], scriptNodeDataDto);
        IReadOnlyList<CanvasDto> evaluatedValue402 = [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [])];
        var flowRunPolicyDto = new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel);
        var flowDefinitionDto = new FlowDefinitionDto(flowId, FlowDefinition.CurrentSchemaVersion, 1, evaluatedValue402, nodeId, "initial", RunPolicy: flowRunPolicyDto);
        return flowDefinitionDto;
    }

    private static FlowDefinitionDto CreateLibraryFlow(string artifactId)
    {
        var nodeUiMetadataDto = new NodeUiMetadataDto("action", "Image transform", string.Empty, null, "ready", true, null, "image-library", artifactId, "Image.Nodes", "Transform", "Image.dll", "1.0.0", "System.String", LibraryNodeContractId: "image-transform");
        var node = new NodeDto("image-transform-node", NodeTypeDto.Action, "Image transform", 0, 0, [], [], null, nodeUiMetadataDto);
        var newGuidResult403 = Guid.NewGuid();
        IReadOnlyList<CanvasDto> evaluatedValue404 = [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [])];
        var flowDefinitionDto405 = new FlowDefinitionDto(newGuidResult403, FlowDefinition.CurrentSchemaVersion, 1, evaluatedValue404, node.Id, "initial");
        return flowDefinitionDto405;
    }

    private static LibraryDto CreateLibrary(string id, LibraryLifecycleDto lifecycle)
    {
        LibraryDto evaluatedValue406 = new(id, id, "1.0.0", $"{id}.zip", 1, id, DateTimeOffset.UtcNow, [], lifecycle);
        return evaluatedValue406;
    }

    private static LibraryDto CreateUpgradeLibrary(string artifactId, string version)
    {
        var manifestNode = new LibraryManifestNodeDto("image-transform", LibraryContractIdentityConfidenceDto.Explicit, NodeTypeDto.Action, "Image.Nodes", "Transform", "Image.Nodes::Transform()", "System.String", false, []);
        var node = new LibraryNodeDto(manifestNode.ContractId, manifestNode.Type, "Image transform", null, artifactId, manifestNode.DeclaringType, manifestNode.MethodName, "Image.dll", version, manifestNode.ReturnType, [], manifestNode.IsAwaitable, manifestNode.ContractId, manifestNode.OverloadSignature, manifestNode.IdentityConfidence, "Image library");
        var libraryArtifactManifestDto = new LibraryArtifactManifestDto(artifactId, "Image", version, version, [manifestNode]);
        var libraryDto = new LibraryDto(artifactId, "Image library", version, $"Image-{version}.zip", 1024, artifactId, DateTimeOffset.UtcNow, [node], LibraryLifecycleDto.Available, "image-library-family", version, libraryArtifactManifestDto, "Image library");
        return libraryDto;
    }

    private static T Deserialize<T>(object? value)
    {
        var evaluatedValue408 = resolveDeserializeResult() ?? throw new InvalidOperationException("The integration response could not be deserialized.");
        return evaluatedValue408;
        T? resolveDeserializeResult()
        {
            var serializeResult407 = JsonSerializer.Serialize(value, JsonOptions);
            var deserializeResult = JsonSerializer.Deserialize<T>(serializeResult407, JsonOptions);
            return deserializeResult;
        }
    }

    private sealed class TestLibraryCatalog : ILibraryCatalogService
    {
        private readonly Dictionary<string, LibraryDto> _libraries;
        private readonly Dictionary<string, LibraryFamilyDto> _families;
        public TestLibraryCatalog(params LibraryDto[] libraries)
        {
            _libraries = libraries.ToDictionary(static library => library.Id, StringComparer.OrdinalIgnoreCase);
            var groupByResult = libraries
                .Where(static library =>
                {
                    var evaluatedValue409 = !string.IsNullOrWhiteSpace(library.FamilyId);
                    return evaluatedValue409;
                })
                .GroupBy(static library => library.FamilyId!, StringComparer.OrdinalIgnoreCase);
            _families = groupByResult.ToDictionary(static group => group.Key, group =>
            {
                var evaluatedValue410 = group.First().FamilyName ?? group.Key;
                var libraryFamilyDto = new LibraryFamilyDto(group.Key, evaluatedValue410, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
                return libraryFamilyDto;
            }, StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyList<LibraryDto> List()
        {
            var toArrayResult411 = _libraries.Values.ToArray();
            return toArrayResult411;
        }

        public LibraryDto? Find(string libraryId)
        {
            var getValueOrDefaultResult = _libraries.GetValueOrDefault(libraryId);
            return getValueOrDefaultResult;
        }

        public Task<IReadOnlyList<LibraryDto>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
        {
            var toArrayResult413 = _libraries.Values
                .Where(library => includeArchived || library.Lifecycle == LibraryLifecycleDto.Available)
                .ToArray();
            var fromResultResult = Task.FromResult<IReadOnlyList<LibraryDto>>(toArrayResult413);
            return fromResultResult;
        }

        public Task<LibraryDto?> FindAsync(string libraryId, CancellationToken cancellationToken = default)
        {
            var findResult = Find(libraryId);
            var fromResultResult414 = Task.FromResult(findResult);
            return fromResultResult414;
        }

        public Task<LibraryUploadResultDto> UploadAsync(Stream package, string fileName, long? declaredLength = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public bool Delete(string libraryId) => false;
        public Task<bool> ArchiveAsync(string libraryId, CancellationToken cancellationToken = default)
        {
            var fromResultResult415 = Task.FromResult(false);
            return fromResultResult415;
        }

        public Task<LibraryDto?> ReindexAsync(string libraryId, CancellationToken cancellationToken = default)
        {
            var findResult416 = Find(libraryId);
            var fromResultResult417 = Task.FromResult(findResult416);
            return fromResultResult417;
        }

        public Task<int> ReindexOutdatedAsync(CancellationToken cancellationToken = default)
        {
            var fromResultResult418 = Task.FromResult(0);
            return fromResultResult418;
        }

        public Task<IReadOnlyList<LibraryFamilyDto>> ListFamiliesAsync(bool includeArchivedArtifacts = true, CancellationToken cancellationToken = default)
        {
            var families = _families.Values
                .Select(family =>
                {
                    var artifacts = _libraries.Values
                        .Where(library =>
                        {
                            var equalsResult = string.Equals(library.FamilyId, family.Id, StringComparison.OrdinalIgnoreCase);
                            return equalsResult;
                        })
                        .Where(library => includeArchivedArtifacts || library.Lifecycle == LibraryLifecycleDto.Available)
                        .OrderByDescending(library =>
                        {
                            var parseVersionResult = ParseVersion(library.SemanticVersion ?? library.Version);
                            return parseVersionResult;
                        })
                        .ThenByDescending(static library => library.UploadedAt)
                        .ThenBy(static library => library.Id, StringComparer.Ordinal)
                        .ToArray();
                    var evaluatedValue421 = family with
                    {
                        LatestArtifactId = artifacts.FirstOrDefault(static library => library.Lifecycle == LibraryLifecycleDto.Available)?.Id,
                        Artifacts = artifacts,
                    };
                    return evaluatedValue421;
                })
                .OrderBy(static family => family.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static family => family.Id, StringComparer.Ordinal)
                .ToArray();
            var fromResultResult424 = Task.FromResult<IReadOnlyList<LibraryFamilyDto>>(families);
            return fromResultResult424;
        }

        public async Task<LibraryFamilyDto?> AssignFamilyAsync(string libraryId, AssignLibraryFamilyRequestDto request, CancellationToken cancellationToken = default)
        {
            if (!_libraries.TryGetValue(libraryId, out var library))
            {
                return null;
            }

            LibraryFamilyDto family;
            if (!string.IsNullOrWhiteSpace(request.FamilyId))
            {
                var familyId = request.FamilyId.Trim();
                if (!_families.TryGetValue(familyId, out var existingFamily))
                {
                    throw new ArgumentException("The requested library family does not exist.", nameof(request));
                }

                family = existingFamily;
            }
            else
            {
                var name = request.Name?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException("A family name is required.", nameof(request));
                }

                family = _families.Values.SingleOrDefault(item =>
                {
                    var equalsResult425 = string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase);
                    return equalsResult425;
                }) ?? resolveLibraryFamilyDto();
                _families[family.Id] = family;
                LibraryFamilyDto resolveLibraryFamilyDto()
                {
                    var newGuidResult7 = Guid.NewGuid();
                    var toStringResult8 = newGuidResult7.ToString("N");
                    var evaluatedValue10 = request.Description?.Trim();
                    var libraryFamilyDto11 = new LibraryFamilyDto(toStringResult8, name, evaluatedValue10, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
                    return libraryFamilyDto11;
                }
            }

            _libraries[libraryId] = library with
            {
                FamilyId = family.Id,
                FamilyName = family.Name,
                SemanticVersion = library.SemanticVersion ?? library.Version,
            };
            var singleResult = (await ListFamiliesAsync(cancellationToken: cancellationToken))
                .Single(item =>
                {
                    var equalsResult426 = string.Equals(item.Id, family.Id, StringComparison.OrdinalIgnoreCase);
                    return equalsResult426;
                });
            return singleResult;
        }

        private static Version ParseVersion(string value)
        {
            var optionalReceiver = value;
            string? optionalResult = default(string);
            if (optionalReceiver is not null)
            {
                var trimResult = optionalReceiver.Trim();
                optionalResult = trimResult.TrimStart('v', 'V');
            }

            var evaluatedValue1 = optionalResult;
            Version conditionalValue;
            if (Version.TryParse(evaluatedValue1, out var parsed))
            {
                conditionalValue = parsed;
            }
            else
            {
                conditionalValue = new Version(0, 0);
            }

            Version evaluatedValue428 = conditionalValue;
            return evaluatedValue428;
        }
    }

    private sealed class TestProjectLibraryReferenceRepository : IProjectLibraryReferenceRepository
    {
        private readonly List<ProjectLibraryReference> _references = [];
        public Task<IReadOnlyList<ProjectLibraryReference>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var toArrayResult430 = _references
                .Where(reference => reference.ProjectId == projectId)
                .ToArray();
            var fromResultResult431 = Task.FromResult<IReadOnlyList<ProjectLibraryReference>>(toArrayResult430);
            return fromResultResult431;
        }

        public Task<bool> IsReferencedAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
        {
            var anyResult = _references.Any(reference =>
            {
                var evaluatedValue432 = reference.ProjectId == projectId && string.Equals(reference.LibraryId, libraryId, StringComparison.OrdinalIgnoreCase);
                return evaluatedValue432;
            });
            var fromResultResult433 = Task.FromResult(anyResult);
            return fromResultResult433;
        }

        public Task<ProjectLibraryReference> AddAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
        {
            var reference = new ProjectLibraryReference(projectId, libraryId, DateTimeOffset.UtcNow);
            _references.Add(reference);
            var fromResultResult434 = Task.FromResult(reference);
            return fromResultResult434;
        }

        public Task<bool> RemoveAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
        {
            var removed = _references.RemoveAll(reference =>
            {
                var evaluatedValue435 = reference.ProjectId == projectId && string.Equals(reference.LibraryId, libraryId, StringComparison.OrdinalIgnoreCase);
                return evaluatedValue435;
            });
            var fromResultResult436 = Task.FromResult(removed > 0);
            return fromResultResult436;
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

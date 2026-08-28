using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Infrastructure.Persistence;

namespace SereinFlow.Infrastructure.Tests;

public sealed class FlowDefinitionPersistenceTests
{
    [Fact]
    public void FlowDefinitionRepositoryPreservesDocumentAndRejectsStaleUpdates()
    {
        using var database = CreateDatabase();
        database.Initialize();
        var project = Project.Create("Workflow");
        new SqlSugarProjectRepository(database).Add(project);
        var repository = new SqlSugarFlowDefinitionRepository(database);
        var initial = CreateDefinition();

        repository.Add(project.Id, initial);

        var loaded = repository.Find(project.Id, initial.Id);
        Assert.NotNull(loaded);
        Assert.Equal(NodeTypeDto.Action, loaded!.Canvases.Single().Nodes.Single().Type);
        Assert.Equal("node.httpTrigger", loaded.Canvases.Single().Nodes.Single().Ui!.TitleKey);
        Assert.Equal("method", loaded.Canvases.Single().Nodes.Single().Ui!.Category);
        Assert.Equal("Orders.Create", $"{loaded.Canvases.Single().Nodes.Single().Ui!.ClassName}.{loaded.Canvases.Single().Nodes.Single().Ui!.MethodName}");
        Assert.Equal("System.String", loaded.Canvases.Single().Nodes.Single().Parameters.Single().Ui!.Type);

        var updated = initial with { Checksum = "updated" };
        var saved = repository.TryUpdate(project.Id, updated, expectedVersion: 1);

        Assert.NotNull(saved);
        Assert.Equal(2, saved!.Version);
        Assert.Equal("updated", saved.Checksum);
        Assert.Null(repository.TryUpdate(project.Id, updated, expectedVersion: 1));
        Assert.Equal(2L, database.Scalar<long>(
            "SELECT COUNT(*) FROM FlowDefinitionVersions WHERE FlowId = @flowId",
            new SqlSugar.SugarParameter("@flowId", initial.Id.ToString("D"))));
    }

    [Fact]
    public async Task LibraryUpgradeCommitRemovesUnusedSourceReferenceAndPreservesSourceBindings()
    {
        using var database = CreateDatabase();
        database.Initialize();
        var project = Project.Create("Library upgrade workflow");
        new SqlSugarProjectRepository(database).Add(project);

        const string sourceArtifactId = "library-artifact-source";
        const string targetArtifactId = "library-artifact-target";
        database.Client.Insertable(new[]
        {
            CreateLibraryRecord(sourceArtifactId, "1.0.0"),
            CreateLibraryRecord(targetArtifactId, "2.0.0"),
        }).ExecuteCommand();

        var definitions = new SqlSugarFlowDefinitionRepository(database);
        var original = CreateDefinition(sourceArtifactId);
        var secondOriginal = CreateDefinition(sourceArtifactId);
        await definitions.AddAsync(project.Id, original);
        await definitions.AddAsync(project.Id, secondOriginal);
        var references = new SqlSugarProjectLibraryReferenceRepository(database);
        await references.AddAsync(project.Id, sourceArtifactId);

        var store = new SqlSugarFlowLibraryUpgradeStore(database);
        var plan = new LibraryUpgradePlanDto(
            Guid.NewGuid(),
            project.Id,
            sourceArtifactId,
            targetArtifactId,
            LibraryUpgradePlanStatusDto.Analyzed,
            [
                new FlowLibraryUpgradePreviewDto(original.Id, original.Version, true, 1, []),
                new FlowLibraryUpgradePreviewDto(secondOriginal.Id, secondOriginal.Version, true, 1, []),
            ],
            DateTimeOffset.UtcNow);
        await store.SavePlanAsync(plan);

        var originalCanvas = Assert.Single(original.Canvases);
        var originalNode = Assert.Single(originalCanvas.Nodes);
        var upgraded = original with
        {
            Checksum = "upgraded",
            Canvases = [originalCanvas with
            {
                Nodes = [originalNode with { Ui = originalNode.Ui! with { LibraryId = targetArtifactId, DllVersion = "2.0.0" } }],
            }],
        };

        var staleCommit = await store.CommitAsync(project.Id, plan.Id, upgraded, expectedVersion: 2, targetArtifactId);
        Assert.False(staleCommit.IsCommitted);
        Assert.Equal(1, staleCommit.CurrentVersion);
        Assert.Single(await references.ListByProjectAsync(project.Id));
        Assert.Equal(LibraryUpgradePlanStatusDto.Analyzed, (await store.FindPlanAsync(project.Id, plan.Id))!.Status);

        var commit = await store.CommitAsync(project.Id, plan.Id, upgraded, expectedVersion: 1, targetArtifactId);

        Assert.True(commit.IsCommitted);
        Assert.Equal(2, commit.Definition!.Version);
        Assert.Equal(targetArtifactId, Assert.Single(commit.Definition.Canvases).Nodes.Single().Ui!.LibraryId);
        var partiallyAppliedPlan = await store.FindPlanAsync(project.Id, plan.Id);
        Assert.Equal(LibraryUpgradePlanStatusDto.Analyzed, partiallyAppliedPlan!.Status);
        Assert.Single(partiallyAppliedPlan.AppliedFlows!);
        var referencesAfterFirstUpgrade = await references.ListByProjectAsync(project.Id);
        Assert.Equal(
            [sourceArtifactId, targetArtifactId],
            referencesAfterFirstUpgrade.Select(item => item.LibraryId).OrderBy(static item => item, StringComparer.Ordinal).ToArray());

        var secondCanvas = Assert.Single(secondOriginal.Canvases);
        var secondNode = Assert.Single(secondCanvas.Nodes);
        var secondUpgraded = secondOriginal with
        {
            Checksum = "second-upgraded",
            Canvases = [secondCanvas with
            {
                Nodes = [secondNode with { Ui = secondNode.Ui! with { LibraryId = targetArtifactId, DllVersion = "2.0.0" } }],
            }],
        };
        var secondCommit = await store.CommitAsync(project.Id, plan.Id, secondUpgraded, expectedVersion: 1, targetArtifactId);
        Assert.True(secondCommit.IsCommitted);
        Assert.Equal(2, secondCommit.Definition!.Version);

        var versions = database.Query<FlowDefinitionVersionRecord>(
            "SELECT FlowId, Version, DefinitionJson, Checksum FROM FlowDefinitionVersions WHERE FlowId = @flowId ORDER BY Version",
            new SqlSugar.SugarParameter("@flowId", original.Id.ToString("D")));
        Assert.Equal([1L, 2L], versions.Select(item => item.Version).ToArray());
        Assert.Contains(sourceArtifactId, versions[0].DefinitionJson, StringComparison.Ordinal);
        Assert.Contains(targetArtifactId, versions[1].DefinitionJson, StringComparison.Ordinal);

        var projectReferences = await references.ListByProjectAsync(project.Id);
        Assert.Equal(
            [targetArtifactId],
            projectReferences.Select(item => item.LibraryId).OrderBy(static item => item, StringComparer.Ordinal).ToArray());

        var bindings = database.Query<FlowLibraryBindingRecord>(
            "SELECT Id, ProjectId, FlowId, FlowVersion, LibraryArtifactId, CreatedAt FROM FlowLibraryBindings WHERE FlowId = @flowId ORDER BY FlowVersion",
            new SqlSugar.SugarParameter("@flowId", original.Id.ToString("D")));
        Assert.Contains(bindings, item => item.FlowVersion == 1 && item.LibraryArtifactId == sourceArtifactId);
        Assert.Contains(bindings, item => item.FlowVersion == 2 && item.LibraryArtifactId == targetArtifactId);
        var appliedPlan = await store.FindPlanAsync(project.Id, plan.Id);
        Assert.Equal(LibraryUpgradePlanStatusDto.Applied, appliedPlan!.Status);
        Assert.Equal(2, appliedPlan.AppliedFlows!.Count);
    }

    private static FlowDefinitionDto CreateDefinition(string libraryId = "library-orders")
    {
        var flowId = Guid.NewGuid();
        var node = new NodeDto(
            "trigger",
            NodeTypeDto.Action,
            "HTTP trigger",
            120,
            80,
            [new NodePortDto("exec-success", "Success", "Output", false),
             new NodePortDto("exec-failure", "Failure", "Output", false),
             new NodePortDto("exec-error", "Error", "Output", false)],
            [new NodeParameterDto(
                "payload",
                "\"order\"",
                DataSourceDto.Literal,
                false,
                new NodeParameterUiMetadataDto("payload", "parameter.payload", "string", "order", null, null, null, null, "System.String", "Order payload", "manual"))],
            null,
            new NodeUiMetadataDto("trigger", "node.httpTrigger", "node.triggerSubtitle", null, "ready", true, 224, "method", libraryId, "Orders", "Create", "Orders.dll", "1.2.0", "System.String"));
        return new FlowDefinitionDto(
            flowId,
            4,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [])],
            "trigger",
            "initial",
            RunPolicy: new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel));
    }

    private static LibraryRecord CreateLibraryRecord(string id, string version)
        => new()
        {
            Id = id,
            Name = "Orders library",
            Version = version,
            FileName = $"Orders-{version}.zip",
            SizeBytes = 1024,
            Sha256 = id,
            UploadedAt = DateTimeOffset.UtcNow.ToString("O"),
            PackagePath = Path.Combine(Path.GetTempPath(), $"{id}.zip"),
            NodeCatalogJson = "[]",
            SemanticVersion = version,
            CatalogSchemaVersion = 4,
            Status = LibraryLifecycleDto.Available.ToString(),
        };

    private static SqliteDatabase CreateDatabase()
        => new(new SqliteDatabaseOptions(Path.Combine(Path.GetTempPath(), $"sereinflow-flow-{Guid.NewGuid():N}.db")));
}

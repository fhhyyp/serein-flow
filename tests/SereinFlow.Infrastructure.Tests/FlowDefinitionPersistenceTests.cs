using System.Collections.Generic;
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
        var sqlSugarProjectRepository = new SqlSugarProjectRepository(database);
        sqlSugarProjectRepository.Add(project);
        var repository = new SqlSugarFlowDefinitionRepository(database);
        var initial = CreateDefinition();
        repository.Add(project.Id, initial);
        var loaded = repository.Find(project.Id, initial.Id);
        Assert.NotNull(loaded);
        var evaluatedValue = loaded!.Canvases.Single().Nodes;
        var evaluatedValue1 = evaluatedValue.Single().Type;
        Assert.Equal(NodeTypeDto.Action, evaluatedValue1);
        var evaluatedValue3 = resolveSingleResult().Ui!.TitleKey;
        Assert.Equal("node.httpTrigger", evaluatedValue3);
        var evaluatedValue7 = resolveSingleResult4().Ui!.Category;
        Assert.Equal("method", evaluatedValue7);
        var evaluatedValue14 = $"{resolveSingleResult8().Ui!.ClassName}.{resolveSingleResult11().Ui!.MethodName}";
        Assert.Equal("Orders.Create", evaluatedValue14);
        var evaluatedValue19 = resolveSingleResult15().Ui!.Type;
        Assert.Equal("System.String", evaluatedValue19);
        var updated = initial with
        {
            Checksum = "updated"
        };
        var saved = repository.TryUpdate(project.Id, updated, expectedVersion: 1);
        Assert.NotNull(saved);
        Assert.Equal(2, saved!.Version);
        Assert.Equal("updated", saved.Checksum);
        var tryUpdateResult = repository.TryUpdate(project.Id, updated, expectedVersion: 1);
        Assert.Null(tryUpdateResult);
        var toStringResult = initial.Id.ToString("D");
        var sugarParameter = new SqlSugar.SugarParameter("@flowId", toStringResult);
        var scalarResult = database.Scalar<long>("SELECT COUNT(*) FROM FlowDefinitionVersions WHERE FlowId = @flowId", sugarParameter);
        Assert.Equal(2L, scalarResult);
        NodeDto resolveSingleResult()
        {
            var evaluatedValue2 = loaded.Canvases.Single().Nodes;
            var singleResult = evaluatedValue2.Single();
            return singleResult;
        }

        NodeDto resolveSingleResult4()
        {
            var evaluatedValue5 = loaded.Canvases.Single().Nodes;
            var singleResult6 = evaluatedValue5.Single();
            return singleResult6;
        }

        NodeDto resolveSingleResult8()
        {
            var evaluatedValue9 = loaded.Canvases.Single().Nodes;
            var singleResult10 = evaluatedValue9.Single();
            return singleResult10;
        }

        NodeDto resolveSingleResult11()
        {
            var evaluatedValue12 = loaded.Canvases.Single().Nodes;
            var singleResult13 = evaluatedValue12.Single();
            return singleResult13;
        }

        NodeParameterDto resolveSingleResult15()
        {
            var evaluatedValue16 = loaded.Canvases.Single().Nodes;
            var evaluatedValue17 = evaluatedValue16.Single().Parameters;
            var singleResult18 = evaluatedValue17.Single();
            return singleResult18;
        }
    }

    [Fact]
    public async Task VersionTracksUseOneSequenceAndRollbackCreatesNewImmutableHeads()
    {
        using var database = CreateDatabase();
        database.Initialize();
        var project = Project.Create("Versioned workflow");
        var sqlSugarProjectRepository20 = new SqlSugarProjectRepository(database);
        sqlSugarProjectRepository20.Add(project);
        var repository = new SqlSugarFlowDefinitionRepository(database);
        var initial = CreateDefinition();
        await repository.AddAsync(project.Id, initial);
        var developmentV2 = await repository.TryUpdateAsync(project.Id, initial with { Checksum = "development-v2" }, expectedVersion: 1);
        Assert.NotNull(developmentV2);
        Assert.Equal(2, developmentV2!.Version);
        var toStringResult21 = initial.Id.ToString("D");
        var sugarParameter22 = new SqlSugar.SugarParameter("@flowId", toStringResult21);
        var storedDevelopment = database.Query<FlowDefinitionVersionRecord>("SELECT FlowId, Version, DefinitionJson, Checksum, Track, Operation, ParentVersion, SourceVersion, Remark, CreatedAt FROM FlowDefinitionVersions WHERE FlowId = @flowId ORDER BY Version", sugarParameter22);
        var toStringResult23 = FlowVersionTrackDto.Development.ToString();
        var evaluatedValue24 = storedDevelopment.Single(item => item.Version == developmentV2.Version).Track;
        Assert.Equal(toStringResult23, evaluatedValue24);
        var published = await repository.PublishAsync(project.Id, initial.Id, expectedDevelopmentVersion: developmentV2.Version, remark: null);
        Assert.True(published.IsCommitted);
        Assert.Equal(3, published.Version!.Version);
        Assert.Equal(FlowVersionTrackDto.Production, published.Version.Track);
        Assert.Equal("由开发版本 v2 发布", published.Version.Remark);
        var developmentV4 = await repository.TryUpdateAsync(project.Id, developmentV2 with { Checksum = "development-v4" }, expectedVersion: developmentV2.Version);
        Assert.NotNull(developmentV4);
        Assert.Equal(4, developmentV4!.Version);
        var rejectedPublish = await repository.PublishAsync(project.Id, initial.Id, expectedDevelopmentVersion: developmentV4.Version, remark: null, expectedProductionVersion: null);
        Assert.False(rejectedPublish.IsCommitted);
        Assert.Equal(published.Version.Version, rejectedPublish.CurrentHeadVersion);
        var findProductionVersionAsyncResult = await repository.FindProductionVersionAsync(project.Id, initial.Id);
        Assert.Equal(published.Version.Version, findProductionVersionAsyncResult);
        var productionRollback = await repository.RollbackAsync(project.Id, initial.Id, sourceVersion: published.Version.Version, FlowVersionTrackDto.Production, expectedHeadVersion: published.Version.Version);
        Assert.True(productionRollback.IsCommitted);
        Assert.Equal(5, productionRollback.Version!.Version);
        Assert.Equal("由生产版本 v3 回滚", productionRollback.Version.Remark);
        var developmentRollback = await repository.RollbackAsync(project.Id, initial.Id, sourceVersion: initial.Version, FlowVersionTrackDto.Development, expectedHeadVersion: developmentV4.Version);
        Assert.True(developmentRollback.IsCommitted);
        Assert.Equal(6, developmentRollback.Version!.Version);
        Assert.Equal("由开发版本 v1 回滚", developmentRollback.Version.Remark);
        var currentDevelopment = await repository.FindAsync(project.Id, initial.Id);
        var currentProduction = await repository.FindProductionDefinitionAsync(project.Id, initial.Id);
        Assert.Equal(6, currentDevelopment!.Version);
        Assert.Equal(initial.Checksum, currentDevelopment.Checksum);
        Assert.Equal(5, currentProduction!.Version);
        Assert.Equal("development-v2", currentProduction.Checksum);
        var isLibraryReferencedByProductionHistoryAsyncResult = await repository.IsLibraryReferencedByProductionHistoryAsync(project.Id, "library-orders");
        Assert.True(isLibraryReferencedByProductionHistoryAsyncResult);
        var developmentHistory = await repository.ListVersionsAsync(project.Id, initial.Id, FlowVersionTrackDto.Development);
        var productionHistory = await repository.ListVersionsAsync(project.Id, initial.Id, FlowVersionTrackDto.Production);
        var selectResult = developmentHistory.Select(static item => item.Version);
        Assert.Equal([6L, 4L, 2L, 1L], selectResult);
        var selectResult25 = productionHistory.Select(static item => item.Version);
        Assert.Equal([5L, 3L], selectResult25);
        Assert.True(developmentHistory[0].IsCurrent);
        Assert.True(productionHistory[0].IsCurrent);
    }

    [Fact]
    public async Task LibraryUpgradeCommitRemovesUnusedSourceReferenceAndPreservesSourceBindings()
    {
        using var database = CreateDatabase();
        database.Initialize();
        var project = Project.Create("Library upgrade workflow");
        var sqlSugarProjectRepository26 = new SqlSugarProjectRepository(database);
        sqlSugarProjectRepository26.Add(project);
        const string sourceArtifactId = "library-artifact-source";
        const string targetArtifactId = "library-artifact-target";
        var evaluatedValue27 = new[]
        {
            CreateLibraryRecord(sourceArtifactId, "1.0.0"),
            CreateLibraryRecord(targetArtifactId, "2.0.0"),
        };
        var insertableResult = database.Client.Insertable(evaluatedValue27);
        insertableResult.ExecuteCommand();
        var definitions = new SqlSugarFlowDefinitionRepository(database);
        var original = CreateDefinition(sourceArtifactId);
        var secondOriginal = CreateDefinition(sourceArtifactId);
        await definitions.AddAsync(project.Id, original);
        await definitions.AddAsync(project.Id, secondOriginal);
        var references = new SqlSugarProjectLibraryReferenceRepository(database);
        await references.AddAsync(project.Id, sourceArtifactId);
        var store = new SqlSugarFlowLibraryUpgradeStore(database);
        var newGuidResult = Guid.NewGuid();
        IReadOnlyList<FlowLibraryUpgradePreviewDto> evaluatedValue28 = [new FlowLibraryUpgradePreviewDto(original.Id, original.Version, true, 1, []), new FlowLibraryUpgradePreviewDto(secondOriginal.Id, secondOriginal.Version, true, 1, []),];
        var plan = new LibraryUpgradePlanDto(newGuidResult, project.Id, sourceArtifactId, targetArtifactId, LibraryUpgradePlanStatusDto.Analyzed, evaluatedValue28, DateTimeOffset.UtcNow);
        await store.SavePlanAsync(plan);
        var originalCanvas = Assert.Single(original.Canvases);
        var originalNode = Assert.Single(originalCanvas.Nodes);
        var upgraded = original with
        {
            Checksum = "upgraded",
            Canvases = [originalCanvas with
            {
                Nodes = [originalNode with
                {
                    Ui = originalNode.Ui!with
                    {
                        LibraryId = targetArtifactId,
                        DllVersion = "2.0.0"
                    }
                }

                ],
            }

            ],
        };
        var staleCommit = await store.CommitAsync(project.Id, plan.Id, upgraded, expectedVersion: 2, targetArtifactId);
        Assert.False(staleCommit.IsCommitted);
        Assert.Equal(1, staleCommit.CurrentVersion);
        var listByProjectAsyncResult = await references.ListByProjectAsync(project.Id);
        Assert.Single(listByProjectAsyncResult);
        var evaluatedValue29 = (await store.FindPlanAsync(project.Id, plan.Id))!.Status;
        Assert.Equal(LibraryUpgradePlanStatusDto.Analyzed, evaluatedValue29);
        var commit = await store.CommitAsync(project.Id, plan.Id, upgraded, expectedVersion: 1, targetArtifactId);
        Assert.True(commit.IsCommitted);
        Assert.Equal(2, commit.Definition!.Version);
        var evaluatedValue33 = resolveSingleResult30().Ui!.LibraryId;
        Assert.Equal(targetArtifactId, evaluatedValue33);
        var partiallyAppliedPlan = await store.FindPlanAsync(project.Id, plan.Id);
        Assert.Equal(LibraryUpgradePlanStatusDto.Analyzed, partiallyAppliedPlan!.Status);
        Assert.Single(partiallyAppliedPlan.AppliedFlows!);
        var referencesAfterFirstUpgrade = await references.ListByProjectAsync(project.Id);
        var toArrayResult = referencesAfterFirstUpgrade
            .Select(item => item.LibraryId)
            .OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal([sourceArtifactId, targetArtifactId], toArrayResult);
        var secondCanvas = Assert.Single(secondOriginal.Canvases);
        var secondNode = Assert.Single(secondCanvas.Nodes);
        var secondUpgraded = secondOriginal with
        {
            Checksum = "second-upgraded",
            Canvases = [secondCanvas with
            {
                Nodes = [secondNode with
                {
                    Ui = secondNode.Ui!with
                    {
                        LibraryId = targetArtifactId,
                        DllVersion = "2.0.0"
                    }
                }

                ],
            }

            ],
        };
        var secondCommit = await store.CommitAsync(project.Id, plan.Id, secondUpgraded, expectedVersion: 1, targetArtifactId);
        Assert.True(secondCommit.IsCommitted);
        Assert.Equal(2, secondCommit.Definition!.Version);
        var toStringResult35 = original.Id.ToString("D");
        var sugarParameter36 = new SqlSugar.SugarParameter("@flowId", toStringResult35);
        var versions = database.Query<FlowDefinitionVersionRecord>("SELECT FlowId, Version, DefinitionJson, Checksum FROM FlowDefinitionVersions WHERE FlowId = @flowId ORDER BY Version", sugarParameter36);
        var toArrayResult38 = versions
            .Select(item => item.Version)
            .ToArray();
        Assert.Equal([1L, 2L], toArrayResult38);
        Assert.Contains(sourceArtifactId, versions[0].DefinitionJson, StringComparison.Ordinal);
        Assert.Contains(targetArtifactId, versions[1].DefinitionJson, StringComparison.Ordinal);
        var projectReferences = await references.ListByProjectAsync(project.Id);
        var toArrayResult41 = projectReferences
            .Select(item => item.LibraryId)
            .OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal([targetArtifactId], toArrayResult41);
        var toStringResult42 = original.Id.ToString("D");
        var sugarParameter43 = new SqlSugar.SugarParameter("@flowId", toStringResult42);
        var bindings = database.Query<FlowLibraryBindingRecord>("SELECT Id, ProjectId, FlowId, FlowVersion, LibraryArtifactId, CreatedAt FROM FlowLibraryBindings WHERE FlowId = @flowId ORDER BY FlowVersion", sugarParameter43);
        Assert.Contains(bindings, item => item.FlowVersion == 1 && item.LibraryArtifactId == sourceArtifactId);
        Assert.Contains(bindings, item => item.FlowVersion == 2 && item.LibraryArtifactId == targetArtifactId);
        var appliedPlan = await store.FindPlanAsync(project.Id, plan.Id);
        Assert.Equal(LibraryUpgradePlanStatusDto.Applied, appliedPlan!.Status);
        Assert.Equal(2, appliedPlan.AppliedFlows!.Count);
        NodeDto resolveSingleResult30()
        {
            var evaluatedValue31 = Assert.Single(commit.Definition.Canvases).Nodes;
            var singleResult32 = evaluatedValue31.Single();
            return singleResult32;
        }
    }

    private static FlowDefinitionDto CreateDefinition(string libraryId = "library-orders")
    {
        var flowId = Guid.NewGuid();
        IReadOnlyList<NodePortDto> evaluatedValue44 = [new NodePortDto("exec-success", "Success", "Output", false), new NodePortDto("exec-failure", "Failure", "Output", false), new NodePortDto("exec-error", "Error", "Output", false)];
        IReadOnlyList<NodeParameterDto> evaluatedValue45 = [resolveNodeParameterDto()];
        var nodeUiMetadataDto = new NodeUiMetadataDto("trigger", "node.httpTrigger", "node.triggerSubtitle", null, "ready", true, 224, "method", libraryId, "Orders", "Create", "Orders.dll", "1.2.0", "System.String");
        var node = new NodeDto("trigger", NodeTypeDto.Action, "HTTP trigger", 120, 80, evaluatedValue44, evaluatedValue45, null, nodeUiMetadataDto);
        IReadOnlyList<CanvasDto> evaluatedValue46 = [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [])];
        var flowRunPolicyDto = new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel);
        var flowDefinitionDto = new FlowDefinitionDto(flowId, 4, 1, evaluatedValue46, "trigger", "initial", RunPolicy: flowRunPolicyDto);
        return flowDefinitionDto;
        NodeParameterDto resolveNodeParameterDto()
        {
            var nodeParameterUiMetadataDto = new NodeParameterUiMetadataDto("payload", "parameter.payload", "string", "order", null, null, null, null, "System.String", "Order payload", "manual");
            var nodeParameterDto = new NodeParameterDto("payload", "\"order\"", DataSourceDto.Literal, false, nodeParameterUiMetadataDto);
            return nodeParameterDto;
        }
    }

    private static LibraryRecord CreateLibraryRecord(string id, string version)
    {
        LibraryRecord evaluatedValue47 = new()
        {
            Id = id,
            Name = "Orders library",
            Version = version,
            FileName = $"Orders-{version}.zip",
            SizeBytes = 1024,
            Sha256 = id,
            UploadedAt = DateTimeOffset.UtcNow.ToString("O"),
            PackagePath = resolveCombineResult(),
            NodeCatalogJson = "[]",
            SemanticVersion = version,
            CatalogSchemaVersion = 4,
            Status = LibraryLifecycleDto.Available.ToString()
        };
        return evaluatedValue47;
        string resolveCombineResult()
        {
            var getTempPathResult1 = Path.GetTempPath();
            var combineResult2 = Path.Combine(getTempPathResult1, $"{id}.zip");
            return combineResult2;
        }
    }

    private static SqliteDatabase CreateDatabase()
    {
        var getTempPathResult = Path.GetTempPath();
        var evaluatedValue48 = $"sereinflow-flow-{Guid.NewGuid():N}.db";
        var combineResult = Path.Combine(getTempPathResult, evaluatedValue48);
        var sqliteDatabaseOptions = new SqliteDatabaseOptions(combineResult);
        SqliteDatabase evaluatedValue49 = new(sqliteDatabaseOptions);
        return evaluatedValue49;
    }
}

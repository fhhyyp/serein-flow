using SereinFlow.Contracts;
using SereinFlow.Infrastructure.Persistence;

namespace SereinFlow.Infrastructure.Tests;
public sealed class LibraryArtifactUsageStoreTests
{
    [Fact]
    public async Task ListAsyncSeparatesCurrentFlowAndHistoricalAuditUsage()
    {
        var getTempPathResult = Path.GetTempPath();
        var evaluatedValue = $"sereinflow-library-usage-{Guid.NewGuid():N}.db";
        var combineResult = Path.Combine(getTempPathResult, evaluatedValue);
        var sqliteDatabaseOptions = new SqliteDatabaseOptions(combineResult);
        using var database = new SqliteDatabase(sqliteDatabaseOptions);
        database.Initialize();
        const string sourceArtifactId = "source-artifact";
        const string targetArtifactId = "target-artifact";
        const string projectId = "4f06b971-5f88-4171-a30b-1b8293f95dc0";
        const string otherProjectId = "bb4ca61b-6398-4f64-937b-3c6503136281";
        const string firstFlowId = "d789c5a4-ae07-4e89-bd9d-9b7f00157a34";
        const string secondFlowId = "2f8b2143-8696-4d28-a646-c7690e760764";
        const string otherFlowId = "d312fe1f-43c7-4834-a34a-08e5a765c7b4";
        var evaluatedValue1 = new[]
        {
            CreateLibrary(sourceArtifactId),
            CreateLibrary(targetArtifactId),
        };
        var insertableResult = database.Client.Insertable(evaluatedValue1);
        insertableResult.ExecuteCommand();
        var projectRecord = new ProjectRecord
        {
            Id = projectId,
            Name = "Usage test project",
            Version = 1,
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("O"),
        };
        var insertableResult2 = database.Client.Insertable(projectRecord);
        insertableResult2.ExecuteCommand();
        var projectRecord3 = new ProjectRecord
        {
            Id = otherProjectId,
            Name = "Other usage test project",
            Version = 1,
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("O"),
        };
        var insertableResult4 = database.Client.Insertable(projectRecord3);
        insertableResult4.ExecuteCommand();
        var evaluatedValue5 = new[]
        {
            new FlowDefinitionRecord
            {
                Id = firstFlowId,
                ProjectId = projectId,
                Version = 2,
                DefinitionJson = "{}",
                Checksum = "v2"
            },
            new FlowDefinitionRecord
            {
                Id = secondFlowId,
                ProjectId = projectId,
                Version = 1,
                DefinitionJson = "{}",
                Checksum = "v1"
            },
            new FlowDefinitionRecord
            {
                Id = otherFlowId,
                ProjectId = otherProjectId,
                Version = 1,
                DefinitionJson = "{}",
                Checksum = "other-v1"
            },
        };
        var insertableResult6 = database.Client.Insertable(evaluatedValue5);
        insertableResult6.ExecuteCommand();
        var evaluatedValue7 = new[]
        {
            CreateFlowBinding(projectId, firstFlowId, 1, sourceArtifactId),
            CreateFlowBinding(projectId, firstFlowId, 2, targetArtifactId),
            CreateFlowBinding(projectId, secondFlowId, 1, sourceArtifactId),
            CreateFlowBinding(otherProjectId, otherFlowId, 1, sourceArtifactId),
        };
        var insertableResult8 = database.Client.Insertable(evaluatedValue7);
        insertableResult8.ExecuteCommand();
        var evaluatedValue9 = new[]
        {
            CreateRun("af02f5bb-a5a8-4f5c-b307-3ac02c43f6d4", projectId, firstFlowId),
            CreateRun("2220e904-f427-4a9b-a9de-7f442f4a1eb0", projectId, firstFlowId),
            CreateRun("7c4602b6-e68c-4d9c-8ad0-727a0d95d8f0", projectId, firstFlowId),
            CreateRun("3cf31511-1538-4f15-a7ce-6cbe5f258bb0", otherProjectId, otherFlowId),
        };
        var insertableResult10 = database.Client.Insertable(evaluatedValue9);
        insertableResult10.ExecuteCommand();
        var evaluatedValue11 = new[]
        {
            CreateRunBinding("af02f5bb-a5a8-4f5c-b307-3ac02c43f6d4", sourceArtifactId),
            CreateRunBinding("2220e904-f427-4a9b-a9de-7f442f4a1eb0", targetArtifactId),
            CreateRunBinding("7c4602b6-e68c-4d9c-8ad0-727a0d95d8f0", targetArtifactId),
            CreateRunBinding("3cf31511-1538-4f15-a7ce-6cbe5f258bb0", sourceArtifactId),
        };
        var insertableResult12 = database.Client.Insertable(evaluatedValue11);
        insertableResult12.ExecuteCommand();
        var sqlSugarLibraryArtifactUsageStore = new SqlSugarLibraryArtifactUsageStore(database);
        var usage = await sqlSugarLibraryArtifactUsageStore.ListAsync();
        var source = Assert.Single(usage, item => item.LibraryArtifactId == sourceArtifactId);
        Assert.Equal(2, source.CurrentFlowCount);
        Assert.Equal(3, source.FlowVersionCount);
        Assert.Equal(2, source.RunSnapshotCount);
        var target = Assert.Single(usage, item => item.LibraryArtifactId == targetArtifactId);
        Assert.Equal(1, target.CurrentFlowCount);
        Assert.Equal(1, target.FlowVersionCount);
        Assert.Equal(2, target.RunSnapshotCount);
        var sqlSugarLibraryArtifactUsageStore13 = new SqlSugarLibraryArtifactUsageStore(database);
        var parseResult = Guid.Parse(projectId);
        var projectUsage = await sqlSugarLibraryArtifactUsageStore13.ListByProjectAsync(parseResult);
        var projectSource = Assert.Single(projectUsage, item => item.LibraryArtifactId == sourceArtifactId);
        Assert.Equal(1, projectSource.CurrentFlowCount);
        Assert.Equal(2, projectSource.FlowVersionCount);
        Assert.Equal(1, projectSource.RunSnapshotCount);
        var evaluatedValue14 = Assert.Single(projectUsage, item => item.LibraryArtifactId == targetArtifactId).RunSnapshotCount;
        Assert.Equal(2, evaluatedValue14);
    }

    private static LibraryRecord CreateLibrary(string id)
    {
        LibraryRecord evaluatedValue15 = new()
        {
            Id = id,
            Name = id,
            Version = "1.0.0",
            FileName = $"{id}.zip",
            Sha256 = id,
            UploadedAt = DateTimeOffset.UtcNow.ToString("O"),
            PackagePath = resolveCombineResult(),
            NodeCatalogJson = "[]",
            Status = LibraryLifecycleDto.Available.ToString()
        };
        return evaluatedValue15;
        string resolveCombineResult()
        {
            var getTempPathResult1 = Path.GetTempPath();
            var combineResult2 = Path.Combine(getTempPathResult1, $"{id}.zip");
            return combineResult2;
        }
    }

    private static FlowLibraryBindingRecord CreateFlowBinding(string projectId, string flowId, long flowVersion, string artifactId)
    {
        FlowLibraryBindingRecord evaluatedValue16 = new()
        {
            Id = resolveToStringResult(),
            ProjectId = projectId,
            FlowId = flowId,
            FlowVersion = flowVersion,
            LibraryArtifactId = artifactId,
            CreatedAt = DateTimeOffset.UtcNow.ToString("O")
        };
        return evaluatedValue16;
        string resolveToStringResult()
        {
            var newGuidResult = Guid.NewGuid();
            var toStringResult = newGuidResult.ToString("N");
            return toStringResult;
        }
    }

    private static RunLibraryBindingRecord CreateRunBinding(string runId, string artifactId)
    {
        RunLibraryBindingRecord evaluatedValue17 = new()
        {
            Id = resolveToStringResult3(),
            RunId = runId,
            LibraryArtifactId = artifactId,
            CreatedAt = DateTimeOffset.UtcNow.ToString("O")
        };
        return evaluatedValue17;
        string resolveToStringResult3()
        {
            var newGuidResult4 = Guid.NewGuid();
            var toStringResult5 = newGuidResult4.ToString("N");
            return toStringResult5;
        }
    }

    private static FlowRunRecord CreateRun(string runId, string projectId, string flowId)
    {
        FlowRunRecord evaluatedValue18 = new()
        {
            Id = runId,
            ProjectId = projectId,
            FlowId = flowId,
            FlowVersion = 2,
            Status = "Succeeded",
            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
            TimeoutSeconds = 300,
            MaxSteps = 10_000,
            MaxNodeVisits = 1_000,
            ConcurrencyMode = "Parallel",
            QueuedAt = DateTimeOffset.UtcNow.ToString("O")
        };
        return evaluatedValue18;
    }
}

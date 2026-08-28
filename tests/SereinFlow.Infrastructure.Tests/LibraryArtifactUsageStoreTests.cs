using SereinFlow.Contracts;
using SereinFlow.Infrastructure.Persistence;

namespace SereinFlow.Infrastructure.Tests;

public sealed class LibraryArtifactUsageStoreTests
{
    [Fact]
    public async Task ListAsyncSeparatesCurrentFlowAndHistoricalAuditUsage()
    {
        using var database = new SqliteDatabase(new SqliteDatabaseOptions(
            Path.Combine(Path.GetTempPath(), $"sereinflow-library-usage-{Guid.NewGuid():N}.db")));
        database.Initialize();

        const string sourceArtifactId = "source-artifact";
        const string targetArtifactId = "target-artifact";
        const string projectId = "4f06b971-5f88-4171-a30b-1b8293f95dc0";
        const string otherProjectId = "bb4ca61b-6398-4f64-937b-3c6503136281";
        const string firstFlowId = "d789c5a4-ae07-4e89-bd9d-9b7f00157a34";
        const string secondFlowId = "2f8b2143-8696-4d28-a646-c7690e760764";
        const string otherFlowId = "d312fe1f-43c7-4834-a34a-08e5a765c7b4";
        database.Client.Insertable(new[]
        {
            CreateLibrary(sourceArtifactId),
            CreateLibrary(targetArtifactId),
        }).ExecuteCommand();
        database.Client.Insertable(new ProjectRecord
        {
            Id = projectId,
            Name = "Usage test project",
            Version = 1,
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("O"),
        }).ExecuteCommand();
        database.Client.Insertable(new ProjectRecord
        {
            Id = otherProjectId,
            Name = "Other usage test project",
            Version = 1,
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("O"),
        }).ExecuteCommand();
        database.Client.Insertable(new[]
        {
            new FlowDefinitionRecord { Id = firstFlowId, ProjectId = projectId, Version = 2, DefinitionJson = "{}", Checksum = "v2" },
            new FlowDefinitionRecord { Id = secondFlowId, ProjectId = projectId, Version = 1, DefinitionJson = "{}", Checksum = "v1" },
            new FlowDefinitionRecord { Id = otherFlowId, ProjectId = otherProjectId, Version = 1, DefinitionJson = "{}", Checksum = "other-v1" },
        }).ExecuteCommand();
        database.Client.Insertable(new[]
        {
            CreateFlowBinding(projectId, firstFlowId, 1, sourceArtifactId),
            CreateFlowBinding(projectId, firstFlowId, 2, targetArtifactId),
            CreateFlowBinding(projectId, secondFlowId, 1, sourceArtifactId),
            CreateFlowBinding(otherProjectId, otherFlowId, 1, sourceArtifactId),
        }).ExecuteCommand();
        database.Client.Insertable(new[]
        {
            CreateRun("af02f5bb-a5a8-4f5c-b307-3ac02c43f6d4", projectId, firstFlowId),
            CreateRun("2220e904-f427-4a9b-a9de-7f442f4a1eb0", projectId, firstFlowId),
            CreateRun("7c4602b6-e68c-4d9c-8ad0-727a0d95d8f0", projectId, firstFlowId),
            CreateRun("3cf31511-1538-4f15-a7ce-6cbe5f258bb0", otherProjectId, otherFlowId),
        }).ExecuteCommand();
        database.Client.Insertable(new[]
        {
            CreateRunBinding("af02f5bb-a5a8-4f5c-b307-3ac02c43f6d4", sourceArtifactId),
            CreateRunBinding("2220e904-f427-4a9b-a9de-7f442f4a1eb0", targetArtifactId),
            CreateRunBinding("7c4602b6-e68c-4d9c-8ad0-727a0d95d8f0", targetArtifactId),
            CreateRunBinding("3cf31511-1538-4f15-a7ce-6cbe5f258bb0", sourceArtifactId),
        }).ExecuteCommand();

        var usage = await new SqlSugarLibraryArtifactUsageStore(database).ListAsync();

        var source = Assert.Single(usage, item => item.LibraryArtifactId == sourceArtifactId);
        Assert.Equal(2, source.CurrentFlowCount);
        Assert.Equal(3, source.FlowVersionCount);
        Assert.Equal(2, source.RunSnapshotCount);

        var target = Assert.Single(usage, item => item.LibraryArtifactId == targetArtifactId);
        Assert.Equal(1, target.CurrentFlowCount);
        Assert.Equal(1, target.FlowVersionCount);
        Assert.Equal(2, target.RunSnapshotCount);

        var projectUsage = await new SqlSugarLibraryArtifactUsageStore(database).ListByProjectAsync(Guid.Parse(projectId));

        var projectSource = Assert.Single(projectUsage, item => item.LibraryArtifactId == sourceArtifactId);
        Assert.Equal(1, projectSource.CurrentFlowCount);
        Assert.Equal(2, projectSource.FlowVersionCount);
        Assert.Equal(1, projectSource.RunSnapshotCount);
        Assert.Equal(2, Assert.Single(projectUsage, item => item.LibraryArtifactId == targetArtifactId).RunSnapshotCount);
    }

    private static LibraryRecord CreateLibrary(string id)
        => new()
        {
            Id = id,
            Name = id,
            Version = "1.0.0",
            FileName = $"{id}.zip",
            Sha256 = id,
            UploadedAt = DateTimeOffset.UtcNow.ToString("O"),
            PackagePath = Path.Combine(Path.GetTempPath(), $"{id}.zip"),
            NodeCatalogJson = "[]",
            Status = LibraryLifecycleDto.Available.ToString(),
        };

    private static FlowLibraryBindingRecord CreateFlowBinding(
        string projectId,
        string flowId,
        long flowVersion,
        string artifactId)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            ProjectId = projectId,
            FlowId = flowId,
            FlowVersion = flowVersion,
            LibraryArtifactId = artifactId,
            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
        };

    private static RunLibraryBindingRecord CreateRunBinding(string runId, string artifactId)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            RunId = runId,
            LibraryArtifactId = artifactId,
            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
        };

    private static FlowRunRecord CreateRun(string runId, string projectId, string flowId)
        => new()
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
            QueuedAt = DateTimeOffset.UtcNow.ToString("O"),
        };
}

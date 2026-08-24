using SereinFlow.Domain;
using SereinFlow.Infrastructure.Persistence;

namespace SereinFlow.Infrastructure.Tests;

public sealed class SqlitePersistenceTests
{
    [Fact]
    public void MigratorIsIdempotentAndEnablesSqliteSafetyPragmas()
    {
        using var database = CreateDatabase();

        database.Initialize();
        database.Initialize();

        Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 1"));
        Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 2"));
        Assert.Equal(1L, database.Scalar<long>("PRAGMA foreign_keys"));
        Assert.Equal(5000L, database.Scalar<long>("PRAGMA busy_timeout"));
    }

    [Fact]
    public void ProjectRepositorySupportsOptimisticVersionUpdates()
    {
        using var database = CreateDatabase();
        database.Initialize();
        var repository = new SqlSugarProjectRepository(database);
        var project = Project.Create("Demo");

        repository.Add(project);
        var loaded = repository.Find(project.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Demo", loaded!.Name);

        loaded.Rename("Renamed");
        Assert.True(repository.TryUpdate(loaded, expectedVersion: 1));
        Assert.False(repository.TryUpdate(loaded, expectedVersion: 1));
        Assert.Equal("Renamed", repository.Find(project.Id)!.Name);
    }

    [Fact]
    public void EventStorePreservesSequenceOrderAndRejectsDuplicateSequence()
    {
        using var database = CreateDatabase();
        database.Initialize();
        var store = new SqlSugarFlowRunEventStore(database);
        var runId = Guid.NewGuid();
        var flowId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        database.Execute(
            "INSERT INTO Projects (Id, Name, Version, Status, CreatedAt, UpdatedAt) VALUES (@id, @name, 1, @status, @now, @now)",
            new SqlSugar.SugarParameter("@id", projectId.ToString("D")),
            new SqlSugar.SugarParameter("@name", "Events"),
            new SqlSugar.SugarParameter("@status", ProjectStatus.Ready.ToString()),
            new SqlSugar.SugarParameter("@now", DateTimeOffset.UtcNow.ToString("O")));
        database.Execute(
            "INSERT INTO FlowDefinitions (Id, ProjectId, Version, DefinitionJson, Checksum) VALUES (@id, @projectId, 1, '{}', '')",
            new SqlSugar.SugarParameter("@id", flowId.ToString("D")),
            new SqlSugar.SugarParameter("@projectId", projectId.ToString("D")));
        database.Execute(
            "INSERT INTO FlowRuns (Id, FlowId, FlowVersion, Status) VALUES (@id, @flowId, 1, 'Running')",
            new SqlSugar.SugarParameter("@id", runId.ToString("D")),
            new SqlSugar.SugarParameter("@flowId", flowId.ToString("D")));

        store.Append(
        [
            new FlowRunEvent(runId, 1, DateTimeOffset.UtcNow, "run.started", null, "{}"),
            new FlowRunEvent(runId, 2, DateTimeOffset.UtcNow, "node.completed", "node-1", "{\"ok\":true}")
        ]);

        Assert.Throws<InvalidOperationException>(() => store.Append(
        [new FlowRunEvent(runId, 2, DateTimeOffset.UtcNow, "duplicate", null, "{}")]));

        var events = store.GetAfter(runId, 0);
        Assert.Equal([1L, 2L], events.Select(static item => item.Sequence));
    }

    [Fact]
    public void BackupCanBeReopenedAsAConsistentDatabase()
    {
        using var database = CreateDatabase();
        database.Initialize();
        var project = Project.Create("Backup");
        new SqlSugarProjectRepository(database).Add(project);
        var backupPath = Path.Combine(Path.GetTempPath(), $"sereinflow-backup-{Guid.NewGuid():N}.db");

        database.BackupTo(backupPath);

        using var restored = new SqliteDatabase(new SqliteDatabaseOptions(backupPath));
        restored.Initialize();

        Assert.Equal(project.Name, new SqlSugarProjectRepository(restored).Find(project.Id)!.Name);
    }

    private static SqliteDatabase CreateDatabase()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sereinflow-{Guid.NewGuid():N}.db");
        return new SqliteDatabase(new SqliteDatabaseOptions(path));
    }
}

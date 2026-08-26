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
        Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 3"));
        Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 9"));
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
    public async Task OutputStorePersistsNodeOutcomesInSequenceOrder()
    {
        using var database = CreateDatabase();
        database.Initialize();
        var runId = Guid.NewGuid();
        var flowId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        database.Execute(
            "INSERT INTO Projects (Id, Name, Version, Status, CreatedAt, UpdatedAt) VALUES (@id, @name, 1, @status, @now, @now)",
            new SqlSugar.SugarParameter("@id", projectId.ToString("D")),
            new SqlSugar.SugarParameter("@name", "Outputs"),
            new SqlSugar.SugarParameter("@status", ProjectStatus.Ready.ToString()),
            new SqlSugar.SugarParameter("@now", now.ToString("O")));
        database.Execute(
            "INSERT INTO FlowDefinitions (Id, ProjectId, Version, DefinitionJson, Checksum) VALUES (@id, @projectId, 1, '{}', '')",
            new SqlSugar.SugarParameter("@id", flowId.ToString("D")),
            new SqlSugar.SugarParameter("@projectId", projectId.ToString("D")));
        database.Execute(
            "INSERT INTO FlowRuns (Id, FlowId, FlowVersion, Status) VALUES (@id, @flowId, 1, 'Running')",
            new SqlSugar.SugarParameter("@id", runId.ToString("D")),
            new SqlSugar.SugarParameter("@flowId", flowId.ToString("D")));

        var store = new SqlSugarFlowRunOutputStore(database);
        await store.AppendAsync(
        [
            new FlowRunOutput(runId, 3, now, "node-1", "completed", "Success", "{\"result\":42}", null, null, "{\"left\":10,\"right\":20}"),
            new FlowRunOutput(runId, 5, now.AddSeconds(1), "node-2", "error", "Error", "{}", "node.input_missing", "Input is missing. 缺少输入。", "{\"value\":null}")
        ]);
        await store.AppendAsync(
        [
            new FlowRunOutput(runId, 3, now, "node-1", "completed", "Success", "{\"result\":42}", null, null, "{\"left\":999}")
        ]);

        var outputs = await store.ListAsync(runId);

        Assert.Equal([3L, 5L], outputs.Select(static item => item.Sequence));
        Assert.Equal("node-1", outputs[0].NodeId);
        Assert.Equal("{\"left\":10,\"right\":20}", outputs[0].InputsJson);
        Assert.Equal("{\"result\":42}", outputs[0].OutputsJson);
        Assert.Equal("node.input_missing", outputs[1].ErrorCode);
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

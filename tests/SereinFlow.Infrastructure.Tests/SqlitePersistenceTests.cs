using SereinFlow.Domain;
using SereinFlow.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

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
        Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 15"));
        Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 17"));
        Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 18"));
        Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 25"));
        Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'FlowProductionHeads'"));
        Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'FlowVersionCounters'"));
        Assert.Equal(1L, database.Scalar<long>("PRAGMA foreign_keys"));
        Assert.Equal(5000L, database.Scalar<long>("PRAGMA busy_timeout"));
    }

    [Fact]
    public void MigratorRepairsEnvironmentSettingsColumnWhenMigrationVersionAlreadyExists()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"sereinflow-upload-settings-{Guid.NewGuid():N}.db");
        try
        {
            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE SchemaMigrations (Version INTEGER NOT NULL PRIMARY KEY, AppliedAt TEXT NOT NULL, Checksum TEXT NOT NULL);
                    CREATE TABLE RunEnvironmentSettings (
                        Id TEXT NOT NULL PRIMARY KEY,
                        QueueCapacity INTEGER NOT NULL,
                        MaxConcurrentRuns INTEGER NOT NULL,
                        MaxConcurrentListenerRuns INTEGER NOT NULL,
                        MaxConcurrentRunsPerProject INTEGER NOT NULL,
                        QueueWaitTimeoutSeconds INTEGER NOT NULL,
                        ShutdownGracePeriodSeconds INTEGER NOT NULL,
                        SynchronousInvocationTimeoutSeconds INTEGER NOT NULL,
                        UpdatedAt TEXT NOT NULL
                    );
                    """;
                command.ExecuteNonQuery();

                for (var version = 1; version <= 27; version++)
                {
                    command.CommandText = "INSERT INTO SchemaMigrations (Version, AppliedAt, Checksum) VALUES ($version, $now, 'legacy');";
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("$version", version);
                    command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
                    command.ExecuteNonQuery();
                }
            }

            using var database = new SqliteDatabase(new SqliteDatabaseOptions(databasePath));
            database.Initialize();

            Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM pragma_table_info('RunEnvironmentSettings') WHERE name = 'MaxLibraryUploadBytes'"));
            Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 28"));
        }
        finally
        {
            DeleteSqliteFiles(databasePath);
        }
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
    public async Task ProjectLibraryReferenceRepositoryKeepsProjectsAndArtifactsIsolated()
    {
        using var database = CreateDatabase();
        database.Initialize();
        var repository = new SqlSugarProjectLibraryReferenceRepository(database);
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        const string oldArtifact = "a1b2c3d4";
        const string newArtifact = "e5f6a7b8";

        new SqlSugarProjectRepository(database).Add(Project.Create("Project A"));
        var persistedProjectA = new SqlSugarProjectRepository(database).List().Single(static project => project.Name == "Project A");
        new SqlSugarProjectRepository(database).Add(Project.Create("Project B"));
        var persistedProjectB = new SqlSugarProjectRepository(database).List().Single(static project => project.Name == "Project B");
        AddLibraryArtifact(database, oldArtifact);
        AddLibraryArtifact(database, newArtifact);

        projectA = persistedProjectA.Id;
        projectB = persistedProjectB.Id;
        await repository.AddAsync(projectA, oldArtifact);
        await repository.AddAsync(projectA, oldArtifact.ToUpperInvariant());
        await repository.AddAsync(projectB, newArtifact);

        var referencesForA = await repository.ListByProjectAsync(projectA);
        var referencesForB = await repository.ListByProjectAsync(projectB);
        Assert.Equal([oldArtifact], referencesForA.Select(static item => item.LibraryId));
        Assert.Equal([newArtifact], referencesForB.Select(static item => item.LibraryId));
        Assert.True(await repository.IsReferencedAsync(projectA, oldArtifact));
        Assert.False(await repository.IsReferencedAsync(projectA, newArtifact));

        Assert.True(await repository.RemoveAsync(projectA, oldArtifact));
        Assert.Empty(await repository.ListByProjectAsync(projectA));
        Assert.Equal([newArtifact], (await repository.ListByProjectAsync(projectB)).Select(static item => item.LibraryId));
    }

    [Fact]
    public void MigrationBackfillsReferencesForExistingFlowDefinitions()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"sereinflow-migration-{Guid.NewGuid():N}.db");
        var projectId = Guid.NewGuid();
        const string libraryId = "abc123artifact";
        try
        {
            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE SchemaMigrations (Version INTEGER NOT NULL PRIMARY KEY, AppliedAt TEXT NOT NULL, Checksum TEXT NOT NULL);
                    CREATE TABLE Projects (Id TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL, Version INTEGER NOT NULL, Status TEXT NOT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
                    CREATE TABLE Libraries (Id TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL, Version TEXT NOT NULL, FileName TEXT NOT NULL, SizeBytes INTEGER NOT NULL, Sha256 TEXT NOT NULL UNIQUE, UploadedAt TEXT NOT NULL, PackagePath TEXT NOT NULL, NodeCatalogJson TEXT NOT NULL);
                    CREATE TABLE FlowDefinitions (Id TEXT NOT NULL PRIMARY KEY, ProjectId TEXT NOT NULL, Version INTEGER NOT NULL, DefinitionJson TEXT NOT NULL, Checksum TEXT NOT NULL);
                    """;
                command.ExecuteNonQuery();

                for (var version = 1; version <= 10; version++)
                {
                    command.CommandText = "INSERT INTO SchemaMigrations (Version, AppliedAt, Checksum) VALUES ($version, $now, $checksum);";
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("$version", version);
                    command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
                    command.Parameters.AddWithValue("$checksum", "legacy");
                    command.ExecuteNonQuery();
                }

                command.CommandText = "INSERT INTO Projects (Id, Name, Version, Status, CreatedAt, UpdatedAt) VALUES ($id, 'Legacy', 1, 'Ready', $now, $now);";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$id", projectId.ToString("D"));
                command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
                command.ExecuteNonQuery();
                command.CommandText = "INSERT INTO Libraries (Id, Name, Version, FileName, SizeBytes, Sha256, UploadedAt, PackagePath, NodeCatalogJson) VALUES ($id, 'LegacyLibrary', '1.0.0', 'LegacyLibrary-1.0.0.zip', 1, $sha, $now, 'legacy.zip', '[]');";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$id", libraryId);
                command.Parameters.AddWithValue("$sha", libraryId);
                command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
                command.ExecuteNonQuery();
                command.CommandText = "INSERT INTO FlowDefinitions (Id, ProjectId, Version, DefinitionJson, Checksum) VALUES ($id, $projectId, 1, $definition, 'legacy');";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
                command.Parameters.AddWithValue("$projectId", projectId.ToString("D"));
                command.Parameters.AddWithValue("$definition", "{\"canvases\":[{\"nodes\":[{\"ui\":{\"libraryId\":\"" + libraryId + "\"}}]}]}");
                command.ExecuteNonQuery();
            }

            using (var database = new SqliteDatabase(new SqliteDatabaseOptions(databasePath)))
            {
                database.Initialize();

                Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM ProjectLibraryReferences WHERE ProjectId = @projectId AND LibraryId = @libraryId",
                    new SqlSugar.SugarParameter("@projectId", projectId.ToString("D")),
                    new SqlSugar.SugarParameter("@libraryId", libraryId)));
                Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 11"));
            }
        }
        finally
        {
            DeleteSqliteFiles(databasePath);
        }
    }

    [Fact]
    public void MigrationFromSchema18AddsMcpSecurityAndAuditColumns()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"sereinflow-schema18-{Guid.NewGuid():N}.db");
        try
        {
            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE SchemaMigrations (Version INTEGER NOT NULL PRIMARY KEY, AppliedAt TEXT NOT NULL, Checksum TEXT NOT NULL);
                    CREATE TABLE Projects (Id TEXT NOT NULL PRIMARY KEY);
                    CREATE TABLE Libraries (Id TEXT NOT NULL PRIMARY KEY);
                    """;
                command.ExecuteNonQuery();

                for (var version = 1; version <= 18; version++)
                {
                    command.CommandText = "INSERT INTO SchemaMigrations (Version, AppliedAt, Checksum) VALUES ($version, $now, 'schema18');";
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("$version", version);
                    command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
                    command.ExecuteNonQuery();
                }
            }

            using var database = new SqliteDatabase(new SqliteDatabaseOptions(databasePath));
            database.Initialize();

            foreach (var version in Enumerable.Range(19, 7))
                Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = @version", new SqlSugar.SugarParameter("@version", version)));
            Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'McpApiKeys'"));
            Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'McpMutationPreviews'"));
            Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'McpIdempotencyRecords'"));
            Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'McpAuditEntries'"));
            Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM pragma_table_info('McpAuditEntries') WHERE name = 'InputBytes'"));
            Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM pragma_table_info('McpAuditEntries') WHERE name = 'OutputBytes'"));
            Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM pragma_table_info('McpAuditEntries') WHERE name = 'Track'"));
            Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM pragma_table_info('McpAuditEntries') WHERE name = 'FlowVersion'"));
            Assert.Equal(1L, database.Scalar<long>("SELECT COUNT(*) FROM pragma_table_info('Libraries') WHERE name = 'DllSha256'"));
        }
        finally
        {
            DeleteSqliteFiles(databasePath);
        }
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
    public async Task DebugSessionStorePersistsBreakpointsAndPausedState()
    {
        using var database = CreateDatabase();
        database.Initialize();
        var projectId = Guid.NewGuid();
        var flowId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        database.Execute(
            "INSERT INTO Projects (Id, Name, Version, Status, CreatedAt, UpdatedAt) VALUES (@id, @name, 1, @status, @now, @now)",
            new SqlSugar.SugarParameter("@id", projectId.ToString("D")),
            new SqlSugar.SugarParameter("@name", "Debug"),
            new SqlSugar.SugarParameter("@status", ProjectStatus.Ready.ToString()),
            new SqlSugar.SugarParameter("@now", now.ToString("O")));
        database.Execute(
            "INSERT INTO FlowDefinitions (Id, ProjectId, Version, DefinitionJson, Checksum) VALUES (@id, @projectId, 1, '{}', '')",
            new SqlSugar.SugarParameter("@id", flowId.ToString("D")),
            new SqlSugar.SugarParameter("@projectId", projectId.ToString("D")));
        database.Execute(
            "INSERT INTO FlowRuns (Id, FlowId, FlowVersion, Status, ExecutionKind) VALUES (@id, @flowId, 1, 'Running', 'Debug')",
            new SqlSugar.SugarParameter("@id", runId.ToString("D")),
            new SqlSugar.SugarParameter("@flowId", flowId.ToString("D")));

        var store = new SqlSugarFlowDebugSessionStore(database);
        var session = FlowDebugSession.Create(runId, projectId, flowId, ["second", "first", "first"], now);
        await store.CreateAsync(session);
        session.MarkRunning(now.AddSeconds(1));
        session.Pause(
            new FlowDebugPauseState(
                "first",
                "Action",
                8,
                1,
                Guid.NewGuid(),
                12,
                "{\"input\":true}",
                now.AddSeconds(2)),
            now.AddSeconds(2));
        session.RecordNodeResult(
            new FlowDebugNodeResult(
                "first",
                13,
                now.AddSeconds(3),
                "completed",
                "Success",
                "{\"input\":true}",
                "{\"output\":\"ok\"}",
                null,
                null),
            now.AddSeconds(3));
        session.AcceptCommand(7, now.AddSeconds(3));
        Assert.True(await store.SaveAsync(session));

        var restored = await store.FindAsync(session.Id);

        Assert.NotNull(restored);
        Assert.Equal(FlowDebugSessionStatus.Paused, restored!.Status);
        Assert.Equal("first", restored.CurrentNodeId);
        Assert.Equal(["first", "second"], restored.BreakpointNodeIds);
        Assert.Equal(runId, restored.RunId);
        Assert.Equal(7, restored.LastCommandSequence);
        Assert.Equal(4, restored.StateRevision);
        Assert.Equal("Action", restored.PauseState!.NodeType);
        Assert.Equal(8, restored.PauseState.Step);
        Assert.Equal(1, restored.PauseState.FrameDepth);
        Assert.Equal(12, restored.PauseState.BoundarySequence);
        Assert.Equal("{\"input\":true}", restored.PauseState.InputsJson);
        Assert.Equal("{\"output\":\"ok\"}", restored.LastNodeResult!.OutputsJson);
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

    private static void AddLibraryArtifact(SqliteDatabase database, string libraryId)
    {
        database.Execute(
            """
            INSERT INTO Libraries (Id, Name, Version, FileName, SizeBytes, Sha256, UploadedAt, PackagePath, NodeCatalogJson, Status)
            VALUES (@id, @name, '1.0.0', @fileName, 1, @sha256, @uploadedAt, @packagePath, '[]', 'Available')
            """,
            new SqlSugar.SugarParameter("@id", libraryId),
            new SqlSugar.SugarParameter("@name", libraryId),
            new SqlSugar.SugarParameter("@fileName", $"{libraryId}-1.0.0.zip"),
            new SqlSugar.SugarParameter("@sha256", libraryId),
            new SqlSugar.SugarParameter("@uploadedAt", DateTimeOffset.UtcNow.ToString("O")),
            new SqlSugar.SugarParameter("@packagePath", $"{libraryId}.zip"));
    }

    private static void DeleteSqliteFiles(string databasePath)
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { databasePath, $"{databasePath}-wal", $"{databasePath}-shm" })
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
                // Test cleanup must not hide a successful migration assertion.
                // 测试清理不能掩盖已成功的迁移断言。
            }
        }
    }
}

using System.Collections.Generic;
using SereinFlow.Contracts;
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
        var scalarResult = database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 1");
        Assert.Equal(1L, scalarResult);
        var scalarResult1 = database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 2");
        Assert.Equal(1L, scalarResult1);
        var scalarResult2 = database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 3");
        Assert.Equal(1L, scalarResult2);
        var scalarResult3 = database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 9");
        Assert.Equal(1L, scalarResult3);
        var scalarResult4 = database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 15");
        Assert.Equal(1L, scalarResult4);
        var scalarResult5 = database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 17");
        Assert.Equal(1L, scalarResult5);
        var scalarResult6 = database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 18");
        Assert.Equal(1L, scalarResult6);
        var scalarResult7 = database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 25");
        Assert.Equal(1L, scalarResult7);
        var scalarResult8 = database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 29");
        Assert.Equal(1L, scalarResult8);
        var scalarResult9 = database.Scalar<long>("SELECT COUNT(*) FROM pragma_table_info('FlowRuns') WHERE name = 'DefinitionChecksum'");
        Assert.Equal(1L, scalarResult9);
        var scalarResult10 = database.Scalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'FlowProductionHeads'");
        Assert.Equal(1L, scalarResult10);
        var scalarResult11 = database.Scalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'FlowVersionCounters'");
        Assert.Equal(1L, scalarResult11);
        var scalarResult12 = database.Scalar<long>("PRAGMA foreign_keys");
        Assert.Equal(1L, scalarResult12);
        var scalarResult13 = database.Scalar<long>("PRAGMA busy_timeout");
        Assert.Equal(5000L, scalarResult13);
    }

    [Fact]
    public void MigratorRepairsEnvironmentSettingsColumnWhenMigrationVersionAlreadyExists()
    {
        var getTempPathResult = Path.GetTempPath();
        var evaluatedValue = $"sereinflow-upload-settings-{Guid.NewGuid():N}.db";
        var databasePath = Path.Combine(getTempPathResult, evaluatedValue);
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
                    var toStringResult = DateTimeOffset.UtcNow.ToString("O");
                    command.Parameters.AddWithValue("$now", toStringResult);
                    command.ExecuteNonQuery();
                }
            }

            var sqliteDatabaseOptions = new SqliteDatabaseOptions(databasePath);
            using var database = new SqliteDatabase(sqliteDatabaseOptions);
            database.Initialize();
            var scalarResult14 = database.Scalar<long>("SELECT COUNT(*) FROM pragma_table_info('RunEnvironmentSettings') WHERE name = 'MaxLibraryUploadBytes'");
            Assert.Equal(1L, scalarResult14);
            var scalarResult15 = database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 28");
            Assert.Equal(1L, scalarResult15);
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
        var tryUpdateResult = repository.TryUpdate(loaded, expectedVersion: 1);
        Assert.True(tryUpdateResult);
        var tryUpdateResult16 = repository.TryUpdate(loaded, expectedVersion: 1);
        Assert.False(tryUpdateResult16);
        var evaluatedValue17 = repository.Find(project.Id)!.Name;
        Assert.Equal("Renamed", evaluatedValue17);
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
        var sqlSugarProjectRepository = new SqlSugarProjectRepository(database);
        var createResult = Project.Create("Project A");
        sqlSugarProjectRepository.Add(createResult);
        var sqlSugarProjectRepository18 = new SqlSugarProjectRepository(database);
        var listResult = sqlSugarProjectRepository18.List();
        var persistedProjectA = listResult.Single(static project => project.Name == "Project A");
        var sqlSugarProjectRepository19 = new SqlSugarProjectRepository(database);
        var createResult20 = Project.Create("Project B");
        sqlSugarProjectRepository19.Add(createResult20);
        var sqlSugarProjectRepository21 = new SqlSugarProjectRepository(database);
        var listResult22 = sqlSugarProjectRepository21.List();
        var persistedProjectB = listResult22.Single(static project => project.Name == "Project B");
        AddLibraryArtifact(database, oldArtifact);
        AddLibraryArtifact(database, newArtifact);
        projectA = persistedProjectA.Id;
        projectB = persistedProjectB.Id;
        await repository.AddAsync(projectA, oldArtifact);
        var toUpperInvariantResult = oldArtifact.ToUpperInvariant();
        await repository.AddAsync(projectA, toUpperInvariantResult);
        await repository.AddAsync(projectB, newArtifact);
        var referencesForA = await repository.ListByProjectAsync(projectA);
        var referencesForB = await repository.ListByProjectAsync(projectB);
        var selectResult = referencesForA.Select(static item => item.LibraryId);
        Assert.Equal([oldArtifact], selectResult);
        var selectResult23 = referencesForB.Select(static item => item.LibraryId);
        Assert.Equal([newArtifact], selectResult23);
        var isReferencedAsyncResult = await repository.IsReferencedAsync(projectA, oldArtifact);
        Assert.True(isReferencedAsyncResult);
        var isReferencedAsyncResult24 = await repository.IsReferencedAsync(projectA, newArtifact);
        Assert.False(isReferencedAsyncResult24);
        var removeAsyncResult = await repository.RemoveAsync(projectA, oldArtifact);
        Assert.True(removeAsyncResult);
        var listByProjectAsyncResult = await repository.ListByProjectAsync(projectA);
        Assert.Empty(listByProjectAsyncResult);
        var evaluatedValue25 = (await repository.ListByProjectAsync(projectB));
        var selectResult26 = evaluatedValue25.Select(static item => item.LibraryId);
        Assert.Equal([newArtifact], selectResult26);
    }

    [Fact]
    public void MigrationBackfillsReferencesForExistingFlowDefinitions()
    {
        var getTempPathResult38 = Path.GetTempPath();
        var evaluatedValue39 = $"sereinflow-migration-{Guid.NewGuid():N}.db";
        var databasePath = Path.Combine(getTempPathResult38, evaluatedValue39);
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
                    var toStringResult27 = DateTimeOffset.UtcNow.ToString("O");
                    command.Parameters.AddWithValue("$now", toStringResult27);
                    command.Parameters.AddWithValue("$checksum", "legacy");
                    command.ExecuteNonQuery();
                }

                command.CommandText = "INSERT INTO Projects (Id, Name, Version, Status, CreatedAt, UpdatedAt) VALUES ($id, 'Legacy', 1, 'Ready', $now, $now);";
                command.Parameters.Clear();
                var toStringResult28 = projectId.ToString("D");
                command.Parameters.AddWithValue("$id", toStringResult28);
                var toStringResult29 = DateTimeOffset.UtcNow.ToString("O");
                command.Parameters.AddWithValue("$now", toStringResult29);
                command.ExecuteNonQuery();
                command.CommandText = "INSERT INTO Libraries (Id, Name, Version, FileName, SizeBytes, Sha256, UploadedAt, PackagePath, NodeCatalogJson) VALUES ($id, 'LegacyLibrary', '1.0.0', 'LegacyLibrary-1.0.0.zip', 1, $sha, $now, 'legacy.zip', '[]');";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$id", libraryId);
                command.Parameters.AddWithValue("$sha", libraryId);
                var toStringResult30 = DateTimeOffset.UtcNow.ToString("O");
                command.Parameters.AddWithValue("$now", toStringResult30);
                command.ExecuteNonQuery();
                command.CommandText = "INSERT INTO FlowDefinitions (Id, ProjectId, Version, DefinitionJson, Checksum) VALUES ($id, $projectId, 1, $definition, 'legacy');";
                command.Parameters.Clear();
                var newGuidResult = Guid.NewGuid();
                var toStringResult31 = newGuidResult.ToString("D");
                command.Parameters.AddWithValue("$id", toStringResult31);
                var toStringResult32 = projectId.ToString("D");
                command.Parameters.AddWithValue("$projectId", toStringResult32);
                command.Parameters.AddWithValue("$definition", "{\"canvases\":[{\"nodes\":[{\"ui\":{\"libraryId\":\"" + libraryId + "\"}}]}]}");
                command.ExecuteNonQuery();
            }

            var sqliteDatabaseOptions37 = new SqliteDatabaseOptions(databasePath);
            using (var database = new SqliteDatabase(sqliteDatabaseOptions37))
            {
                database.Initialize();
                var toStringResult33 = projectId.ToString("D");
                var sugarParameter = new SqlSugar.SugarParameter("@projectId", toStringResult33);
                var sugarParameter34 = new SqlSugar.SugarParameter("@libraryId", libraryId);
                var scalarResult35 = database.Scalar<long>("SELECT COUNT(*) FROM ProjectLibraryReferences WHERE ProjectId = @projectId AND LibraryId = @libraryId", sugarParameter, sugarParameter34);
                Assert.Equal(1L, scalarResult35);
                var scalarResult36 = database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 11");
                Assert.Equal(1L, scalarResult36);
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
        var getTempPathResult53 = Path.GetTempPath();
        var evaluatedValue54 = $"sereinflow-schema18-{Guid.NewGuid():N}.db";
        var databasePath = Path.Combine(getTempPathResult53, evaluatedValue54);
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
                    var toStringResult40 = DateTimeOffset.UtcNow.ToString("O");
                    command.Parameters.AddWithValue("$now", toStringResult40);
                    command.ExecuteNonQuery();
                }
            }

            var sqliteDatabaseOptions43 = new SqliteDatabaseOptions(databasePath);
            using var database = new SqliteDatabase(sqliteDatabaseOptions43);
            database.Initialize();
            foreach (var version in Enumerable.Range(19, 7))
            {
                var sugarParameter41 = new SqlSugar.SugarParameter("@version", version);
                var scalarResult42 = database.Scalar<long>("SELECT COUNT(*) FROM SchemaMigrations WHERE Version = @version", sugarParameter41);
                Assert.Equal(1L, scalarResult42);
            }

            var scalarResult44 = database.Scalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'McpApiKeys'");
            Assert.Equal(1L, scalarResult44);
            var scalarResult45 = database.Scalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'McpMutationPreviews'");
            Assert.Equal(1L, scalarResult45);
            var scalarResult46 = database.Scalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'McpIdempotencyRecords'");
            Assert.Equal(1L, scalarResult46);
            var scalarResult47 = database.Scalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'McpAuditEntries'");
            Assert.Equal(1L, scalarResult47);
            var scalarResult48 = database.Scalar<long>("SELECT COUNT(*) FROM pragma_table_info('McpAuditEntries') WHERE name = 'InputBytes'");
            Assert.Equal(1L, scalarResult48);
            var scalarResult49 = database.Scalar<long>("SELECT COUNT(*) FROM pragma_table_info('McpAuditEntries') WHERE name = 'OutputBytes'");
            Assert.Equal(1L, scalarResult49);
            var scalarResult50 = database.Scalar<long>("SELECT COUNT(*) FROM pragma_table_info('McpAuditEntries') WHERE name = 'Track'");
            Assert.Equal(1L, scalarResult50);
            var scalarResult51 = database.Scalar<long>("SELECT COUNT(*) FROM pragma_table_info('McpAuditEntries') WHERE name = 'FlowVersion'");
            Assert.Equal(1L, scalarResult51);
            var scalarResult52 = database.Scalar<long>("SELECT COUNT(*) FROM pragma_table_info('Libraries') WHERE name = 'DllSha256'");
            Assert.Equal(1L, scalarResult52);
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
        var toStringResult56 = projectId.ToString("D");
        var sugarParameter57 = new SqlSugar.SugarParameter("@id", toStringResult56);
        var sugarParameter58 = new SqlSugar.SugarParameter("@name", "Events");
        var toStringResult59 = ProjectStatus.Ready.ToString();
        var sugarParameter60 = new SqlSugar.SugarParameter("@status", toStringResult59);
        var toStringResult61 = DateTimeOffset.UtcNow.ToString("O");
        var sugarParameter62 = new SqlSugar.SugarParameter("@now", toStringResult61);
        database.Execute("INSERT INTO Projects (Id, Name, Version, Status, CreatedAt, UpdatedAt) VALUES (@id, @name, 1, @status, @now, @now)", sugarParameter57, sugarParameter58, sugarParameter60, sugarParameter62);
        var toStringResult63 = flowId.ToString("D");
        var sugarParameter64 = new SqlSugar.SugarParameter("@id", toStringResult63);
        var toStringResult65 = projectId.ToString("D");
        var sugarParameter66 = new SqlSugar.SugarParameter("@projectId", toStringResult65);
        database.Execute("INSERT INTO FlowDefinitions (Id, ProjectId, Version, DefinitionJson, Checksum) VALUES (@id, @projectId, 1, '{}', '')", sugarParameter64, sugarParameter66);
        var toStringResult67 = runId.ToString("D");
        var sugarParameter68 = new SqlSugar.SugarParameter("@id", toStringResult67);
        var toStringResult69 = flowId.ToString("D");
        var sugarParameter70 = new SqlSugar.SugarParameter("@flowId", toStringResult69);
        database.Execute("INSERT INTO FlowRuns (Id, FlowId, FlowVersion, Status) VALUES (@id, @flowId, 1, 'Running')", sugarParameter68, sugarParameter70);
        IReadOnlyList<FlowRunEvent> evaluatedValue71 = [new FlowRunEvent(runId, 1, DateTimeOffset.UtcNow, "run.started", null, "{}"), new FlowRunEvent(runId, 2, DateTimeOffset.UtcNow, "node.completed", "node-1", "{\"ok\":true}")];
        store.Append(evaluatedValue71);
        Assert.Throws<InvalidOperationException>(() =>
        {
            IReadOnlyList<FlowRunEvent> evaluatedValue55 = [new FlowRunEvent(runId, 2, DateTimeOffset.UtcNow, "duplicate", null, "{}")];
            store.Append(evaluatedValue55);
        });
        var events = store.GetAfter(runId, 0);
        var selectResult72 = events.Select(static item => item.Sequence);
        Assert.Equal([1L, 2L], selectResult72);
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
        var toStringResult73 = projectId.ToString("D");
        var sugarParameter74 = new SqlSugar.SugarParameter("@id", toStringResult73);
        var sugarParameter75 = new SqlSugar.SugarParameter("@name", "Outputs");
        var toStringResult76 = ProjectStatus.Ready.ToString();
        var sugarParameter77 = new SqlSugar.SugarParameter("@status", toStringResult76);
        var toStringResult78 = now.ToString("O");
        var sugarParameter79 = new SqlSugar.SugarParameter("@now", toStringResult78);
        database.Execute("INSERT INTO Projects (Id, Name, Version, Status, CreatedAt, UpdatedAt) VALUES (@id, @name, 1, @status, @now, @now)", sugarParameter74, sugarParameter75, sugarParameter77, sugarParameter79);
        var toStringResult80 = flowId.ToString("D");
        var sugarParameter81 = new SqlSugar.SugarParameter("@id", toStringResult80);
        var toStringResult82 = projectId.ToString("D");
        var sugarParameter83 = new SqlSugar.SugarParameter("@projectId", toStringResult82);
        database.Execute("INSERT INTO FlowDefinitions (Id, ProjectId, Version, DefinitionJson, Checksum) VALUES (@id, @projectId, 1, '{}', '')", sugarParameter81, sugarParameter83);
        var toStringResult84 = runId.ToString("D");
        var sugarParameter85 = new SqlSugar.SugarParameter("@id", toStringResult84);
        var toStringResult86 = flowId.ToString("D");
        var sugarParameter87 = new SqlSugar.SugarParameter("@flowId", toStringResult86);
        database.Execute("INSERT INTO FlowRuns (Id, FlowId, FlowVersion, Status) VALUES (@id, @flowId, 1, 'Running')", sugarParameter85, sugarParameter87);
        var store = new SqlSugarFlowRunOutputStore(database);
        IReadOnlyList<FlowRunOutput> evaluatedValue88 = [new FlowRunOutput(runId, 3, now, "node-1", "completed", "Success", "{\"result\":42}", null, null, "{\"left\":10,\"right\":20}"), resolveFlowRunOutput()];
        await store.AppendAsync(evaluatedValue88);
        IReadOnlyList<FlowRunOutput> evaluatedValue89 = [new FlowRunOutput(runId, 3, now, "node-1", "completed", "Success", "{\"result\":42}", null, null, "{\"left\":999}")];
        await store.AppendAsync(evaluatedValue89);
        var outputs = await store.ListAsync(runId);
        var selectResult90 = outputs.Select(static item => item.Sequence);
        Assert.Equal([3L, 5L], selectResult90);
        Assert.Equal("node-1", outputs[0].NodeId);
        Assert.Equal("{\"left\":10,\"right\":20}", outputs[0].InputsJson);
        Assert.Equal("{\"result\":42}", outputs[0].OutputsJson);
        Assert.Equal(NodeErrorCodes.InputMissing, outputs[1].ErrorCode);
        FlowRunOutput resolveFlowRunOutput()
        {
            var addSecondsResult = now.AddSeconds(1);
            var flowRunOutput = new FlowRunOutput(runId, 5, addSecondsResult, "node-2", "error", "Error", "{}", NodeErrorCodes.InputMissing, "Input is missing. 缺少输入。", "{\"value\":null}");
            return flowRunOutput;
        }
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
        var toStringResult91 = projectId.ToString("D");
        var sugarParameter92 = new SqlSugar.SugarParameter("@id", toStringResult91);
        var sugarParameter93 = new SqlSugar.SugarParameter("@name", "Debug");
        var toStringResult94 = ProjectStatus.Ready.ToString();
        var sugarParameter95 = new SqlSugar.SugarParameter("@status", toStringResult94);
        var toStringResult96 = now.ToString("O");
        var sugarParameter97 = new SqlSugar.SugarParameter("@now", toStringResult96);
        database.Execute("INSERT INTO Projects (Id, Name, Version, Status, CreatedAt, UpdatedAt) VALUES (@id, @name, 1, @status, @now, @now)", sugarParameter92, sugarParameter93, sugarParameter95, sugarParameter97);
        var toStringResult98 = flowId.ToString("D");
        var sugarParameter99 = new SqlSugar.SugarParameter("@id", toStringResult98);
        var toStringResult100 = projectId.ToString("D");
        var sugarParameter101 = new SqlSugar.SugarParameter("@projectId", toStringResult100);
        database.Execute("INSERT INTO FlowDefinitions (Id, ProjectId, Version, DefinitionJson, Checksum) VALUES (@id, @projectId, 1, '{}', '')", sugarParameter99, sugarParameter101);
        var toStringResult102 = runId.ToString("D");
        var sugarParameter103 = new SqlSugar.SugarParameter("@id", toStringResult102);
        var toStringResult104 = flowId.ToString("D");
        var sugarParameter105 = new SqlSugar.SugarParameter("@flowId", toStringResult104);
        database.Execute("INSERT INTO FlowRuns (Id, FlowId, FlowVersion, Status, ExecutionKind) VALUES (@id, @flowId, 1, 'Running', 'Debug')", sugarParameter103, sugarParameter105);
        var store = new SqlSugarFlowDebugSessionStore(database);
        var session = FlowDebugSession.Create(runId, projectId, flowId, ["second", "first", "first"], now);
        await store.CreateAsync(session);
        var addSecondsResult106 = now.AddSeconds(1);
        session.MarkRunning(addSecondsResult106);
        var newGuidResult107 = Guid.NewGuid();
        var addSecondsResult108 = now.AddSeconds(2);
        var flowDebugPauseState = new FlowDebugPauseState("first", "Action", 8, 1, newGuidResult107, 12, "{\"input\":true}", addSecondsResult108);
        var addSecondsResult109 = now.AddSeconds(2);
        session.Pause(flowDebugPauseState, addSecondsResult109);
        var addSecondsResult110 = now.AddSeconds(3);
        var flowDebugNodeResult = new FlowDebugNodeResult("first", 13, addSecondsResult110, "completed", "Success", "{\"input\":true}", "{\"output\":\"ok\"}", null, null);
        var addSecondsResult111 = now.AddSeconds(3);
        session.RecordNodeResult(flowDebugNodeResult, addSecondsResult111);
        var addSecondsResult112 = now.AddSeconds(3);
        session.AcceptCommand(7, addSecondsResult112);
        var saveAsyncResult = await store.SaveAsync(session);
        Assert.True(saveAsyncResult);
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
        var sqlSugarProjectRepository113 = new SqlSugarProjectRepository(database);
        sqlSugarProjectRepository113.Add(project);
        var getTempPathResult114 = Path.GetTempPath();
        var evaluatedValue115 = $"sereinflow-backup-{Guid.NewGuid():N}.db";
        var backupPath = Path.Combine(getTempPathResult114, evaluatedValue115);
        database.BackupTo(backupPath);
        var sqliteDatabaseOptions116 = new SqliteDatabaseOptions(backupPath);
        using var restored = new SqliteDatabase(sqliteDatabaseOptions116);
        restored.Initialize();
        var evaluatedValue118 = resolveFindResult()!.Name;
        Assert.Equal(project.Name, evaluatedValue118);
        Project? resolveFindResult()
        {
            var sqlSugarProjectRepository117 = new SqlSugarProjectRepository(restored);
            var findResult = sqlSugarProjectRepository117.Find(project.Id);
            return findResult;
        }
    }

    private static SqliteDatabase CreateDatabase()
    {
        var getTempPathResult119 = Path.GetTempPath();
        var evaluatedValue120 = $"sereinflow-{Guid.NewGuid():N}.db";
        var path = Path.Combine(getTempPathResult119, evaluatedValue120);
        var sqliteDatabaseOptions121 = new SqliteDatabaseOptions(path);
        var sqliteDatabase = new SqliteDatabase(sqliteDatabaseOptions121);
        return sqliteDatabase;
    }

    private static void AddLibraryArtifact(SqliteDatabase database, string libraryId)
    {
        var sugarParameter122 = new SqlSugar.SugarParameter("@id", libraryId);
        var sugarParameter123 = new SqlSugar.SugarParameter("@name", libraryId);
        var sugarParameter124 = new SqlSugar.SugarParameter("@fileName", $"{libraryId}-1.0.0.zip");
        var sugarParameter125 = new SqlSugar.SugarParameter("@sha256", libraryId);
        var toStringResult126 = DateTimeOffset.UtcNow.ToString("O");
        var sugarParameter127 = new SqlSugar.SugarParameter("@uploadedAt", toStringResult126);
        var sugarParameter128 = new SqlSugar.SugarParameter("@packagePath", $"{libraryId}.zip");
        database.Execute("""
            INSERT INTO Libraries (Id, Name, Version, FileName, SizeBytes, Sha256, UploadedAt, PackagePath, NodeCatalogJson, Status)
            VALUES (@id, @name, '1.0.0', @fileName, 1, @sha256, @uploadedAt, @packagePath, '[]', 'Available')
            """, sugarParameter122, sugarParameter123, sugarParameter124, sugarParameter125, sugarParameter127, sugarParameter128);
    }

    private static void DeleteSqliteFiles(string databasePath)
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[]
        {
            databasePath,
            $"{databasePath}-wal",
            $"{databasePath}-shm"}

        )
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            // Test cleanup must not hide a successful migration assertion.
            // 测试清理不能掩盖已成功的迁移断言。
            }
        }
    }
}

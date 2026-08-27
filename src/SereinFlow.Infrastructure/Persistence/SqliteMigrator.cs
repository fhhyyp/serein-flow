using SqlSugar;
using System.Text.Json;

namespace SereinFlow.Infrastructure.Persistence;

public sealed class SqliteMigrator
{
    private const int InitialSchemaVersion = 1;
    private const int RemoveOrderPipelineSeedVersion = 2;
    private const int AddLibraryCatalogVersion = 3;
    private const int AddFlowRunSnapshotsVersion = 4;
    private const int AddFlowRunExecutionOptionsVersion = 5;
    private const int AddFlowRunOrchestrationVersion = 6;
    private const int AddFlowRunTimeoutVersion = 7;
    private const int AddEnvironmentConsoleVersion = 8;
    private const int AddFlowRunOutputsVersion = 9;
    private const int AddFlowRunOutputInputsVersion = 10;
    private const int AddProjectLibraryReferencesVersion = 11;
    private const int AddLibraryEnumCatalogVersion = 12;
    private readonly SqlSugarClient _client;

    public SqliteMigrator(SqlSugarClient client)
    {
        _client = client;
    }

    public void Migrate()
    {
        _client.Ado.ExecuteCommand("""
            CREATE TABLE IF NOT EXISTS SchemaMigrations (
                Version INTEGER NOT NULL PRIMARY KEY,
                AppliedAt TEXT NOT NULL,
                Checksum TEXT NOT NULL
            );
            """);

        var applied = _client.Ado.SqlQuery<int>("SELECT Version FROM SchemaMigrations ORDER BY Version");
        if (!applied.Contains(InitialSchemaVersion))
        {
            _client.Ado.BeginTran();
            try
            {
                _client.Ado.ExecuteCommand("""
                CREATE TABLE IF NOT EXISTS Projects (
                    Id TEXT NOT NULL PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Version INTEGER NOT NULL,
                    Status TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS FlowDefinitions (
                    Id TEXT NOT NULL PRIMARY KEY,
                    ProjectId TEXT NOT NULL,
                    Version INTEGER NOT NULL,
                    DefinitionJson TEXT NOT NULL,
                    Checksum TEXT NOT NULL,
                    FOREIGN KEY (ProjectId) REFERENCES Projects(Id)
                );
                CREATE TABLE IF NOT EXISTS FlowDefinitionVersions (
                    FlowId TEXT NOT NULL,
                    Version INTEGER NOT NULL,
                    DefinitionJson TEXT NOT NULL,
                    Checksum TEXT NOT NULL,
                    PRIMARY KEY (FlowId, Version),
                    FOREIGN KEY (FlowId) REFERENCES FlowDefinitions(Id)
                );
                CREATE TABLE IF NOT EXISTS PluginManifests (
                    Id TEXT NOT NULL PRIMARY KEY,
                    ProjectId TEXT NOT NULL,
                    Version TEXT NOT NULL,
                    Hash TEXT NOT NULL,
                    ManifestJson TEXT NOT NULL,
                    FOREIGN KEY (ProjectId) REFERENCES Projects(Id)
                );
                CREATE TABLE IF NOT EXISTS FlowRuns (
                    Id TEXT NOT NULL PRIMARY KEY,
                    FlowId TEXT NOT NULL,
                    FlowVersion INTEGER NOT NULL,
                    Status TEXT NOT NULL,
                    StartedAt TEXT NULL,
                    EndedAt TEXT NULL,
                    ErrorSummary TEXT NULL,
                    FOREIGN KEY (FlowId) REFERENCES FlowDefinitions(Id)
                );
                CREATE TABLE IF NOT EXISTS FlowRunEvents (
                    RunId TEXT NOT NULL,
                    Sequence INTEGER NOT NULL,
                    Type TEXT NOT NULL,
                    NodeId TEXT NULL,
                    PayloadJson TEXT NOT NULL,
                    Timestamp TEXT NOT NULL,
                    PRIMARY KEY (RunId, Sequence),
                    FOREIGN KEY (RunId) REFERENCES FlowRuns(Id)
                );
                """);
                RecordMigration(InitialSchemaVersion, "t0-sqlite-schema-v1");
                _client.Ado.CommitTran();
            }
            catch
            {
                _client.Ado.RollbackTran();
                throw;
            }
        }

        if (!applied.Contains(RemoveOrderPipelineSeedVersion))
        {
            _client.Ado.BeginTran();
            try
            {
                const string seedNameZh = "订单处理流程";
                const string seedNameEn = "Order pipeline";
                var parameters = new[]
                {
                    new SugarParameter("@seedNameZh", seedNameZh),
                    new SugarParameter("@seedNameEn", seedNameEn),
                };
                _client.Ado.ExecuteCommand(
                    "DELETE FROM FlowRunEvents WHERE RunId IN (SELECT Id FROM FlowRuns WHERE FlowId IN (SELECT Id FROM FlowDefinitions WHERE ProjectId IN (SELECT Id FROM Projects WHERE Name IN (@seedNameZh, @seedNameEn))))",
                    parameters);
                _client.Ado.ExecuteCommand(
                    "DELETE FROM FlowRuns WHERE FlowId IN (SELECT Id FROM FlowDefinitions WHERE ProjectId IN (SELECT Id FROM Projects WHERE Name IN (@seedNameZh, @seedNameEn)))",
                    parameters);
                _client.Ado.ExecuteCommand(
                    "DELETE FROM FlowDefinitionVersions WHERE FlowId IN (SELECT Id FROM FlowDefinitions WHERE ProjectId IN (SELECT Id FROM Projects WHERE Name IN (@seedNameZh, @seedNameEn)))",
                    parameters);
                _client.Ado.ExecuteCommand(
                    "DELETE FROM FlowDefinitions WHERE ProjectId IN (SELECT Id FROM Projects WHERE Name IN (@seedNameZh, @seedNameEn))",
                    parameters);
                _client.Ado.ExecuteCommand(
                    "DELETE FROM PluginManifests WHERE ProjectId IN (SELECT Id FROM Projects WHERE Name IN (@seedNameZh, @seedNameEn))",
                    parameters);
                _client.Ado.ExecuteCommand(
                    "DELETE FROM Projects WHERE Name IN (@seedNameZh, @seedNameEn)",
                    parameters);
                RecordMigration(RemoveOrderPipelineSeedVersion, "remove-order-pipeline-seed-v1");
                _client.Ado.CommitTran();
            }
            catch
            {
                _client.Ado.RollbackTran();
                throw;
            }
        }

        if (!applied.Contains(AddLibraryCatalogVersion))
        {
            _client.Ado.BeginTran();
            try
            {
                _client.Ado.ExecuteCommand("""
                    CREATE TABLE IF NOT EXISTS Libraries (
                        Id TEXT NOT NULL PRIMARY KEY,
                        Name TEXT NOT NULL,
                        Version TEXT NOT NULL,
                        FileName TEXT NOT NULL,
                        SizeBytes INTEGER NOT NULL,
                        Sha256 TEXT NOT NULL UNIQUE,
                        UploadedAt TEXT NOT NULL,
                        PackagePath TEXT NOT NULL,
                        NodeCatalogJson TEXT NOT NULL
                    );
                    """);
                RecordMigration(AddLibraryCatalogVersion, "library-catalog-v1");
                _client.Ado.CommitTran();
            }
            catch
            {
                _client.Ado.RollbackTran();
                throw;
            }
        }

        if (!applied.Contains(AddFlowRunSnapshotsVersion))
        {
            _client.Ado.BeginTran();
            try
            {
                _client.Ado.ExecuteCommand("""
                    ALTER TABLE FlowRuns ADD COLUMN ProjectId TEXT NULL;
                    ALTER TABLE FlowRuns ADD COLUMN CreatedAt TEXT NULL;
                    ALTER TABLE FlowRuns ADD COLUMN CancellationReason TEXT NULL;
                    CREATE TABLE IF NOT EXISTS FlowRunDefinitions (
                        RunId TEXT NOT NULL PRIMARY KEY,
                        FlowId TEXT NOT NULL,
                        FlowVersion INTEGER NOT NULL,
                        SchemaVersion INTEGER NOT NULL,
                        Checksum TEXT NOT NULL,
                        DefinitionJson TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        FOREIGN KEY (RunId) REFERENCES FlowRuns(Id)
                    );
                    CREATE INDEX IF NOT EXISTS IX_FlowRuns_ProjectId_CreatedAt ON FlowRuns(ProjectId, CreatedAt);
                    CREATE INDEX IF NOT EXISTS IX_FlowRunEvents_RunId_Sequence ON FlowRunEvents(RunId, Sequence);
                    """);
                RecordMigration(AddFlowRunSnapshotsVersion, "flow-run-snapshots-v1");
                _client.Ado.CommitTran();
            }
            catch
            {
                _client.Ado.RollbackTran();
                throw;
            }
        }

        applied = _client.Ado.SqlQuery<int>("SELECT Version FROM SchemaMigrations ORDER BY Version");
        if (!applied.Contains(AddFlowRunExecutionOptionsVersion))
        {
            _client.Ado.BeginTran();
            try
            {
                _client.Ado.ExecuteCommand("""
                    ALTER TABLE FlowRuns ADD COLUMN Deadline TEXT NULL;
                    ALTER TABLE FlowRuns ADD COLUMN MaxSteps INTEGER NULL;
                    ALTER TABLE FlowRuns ADD COLUMN MaxNodeVisits INTEGER NULL;
                    ALTER TABLE FlowRuns ADD COLUMN ProjectInputsJson TEXT NULL;
                    """);
                RecordMigration(AddFlowRunExecutionOptionsVersion, "flow-run-execution-options-v1");
                _client.Ado.CommitTran();
            }
            catch
            {
                _client.Ado.RollbackTran();
                throw;
            }
        }

        applied = _client.Ado.SqlQuery<int>("SELECT Version FROM SchemaMigrations ORDER BY Version");
        if (!applied.Contains(AddFlowRunOrchestrationVersion))
        {
            _client.Ado.BeginTran();
            try
            {
                _client.Ado.ExecuteCommand("""
                    ALTER TABLE FlowRuns ADD COLUMN ConcurrencyMode TEXT NULL;
                    ALTER TABLE FlowRuns ADD COLUMN ExclusivityKey TEXT NULL;
                    ALTER TABLE FlowRuns ADD COLUMN IsListenerRun INTEGER NOT NULL DEFAULT 0;
                    ALTER TABLE FlowRuns ADD COLUMN QueuedAt TEXT NULL;
                    CREATE INDEX IF NOT EXISTS IX_FlowRuns_Status_QueuedAt ON FlowRuns(Status, QueuedAt);
                    CREATE INDEX IF NOT EXISTS IX_FlowRuns_ProjectId_Status ON FlowRuns(ProjectId, Status);
                    CREATE UNIQUE INDEX IF NOT EXISTS UX_FlowRuns_ActiveExclusiveFlow
                    ON FlowRuns(ExclusivityKey)
                    WHERE ExclusivityKey IS NOT NULL AND Status IN ('Pending', 'Running');
                    """);
                RecordMigration(AddFlowRunOrchestrationVersion, "flow-run-orchestration-v1");
                _client.Ado.CommitTran();
            }
            catch
            {
                _client.Ado.RollbackTran();
                throw;
            }
        }

        applied = _client.Ado.SqlQuery<int>("SELECT Version FROM SchemaMigrations ORDER BY Version");
        if (!applied.Contains(AddFlowRunTimeoutVersion))
        {
            _client.Ado.BeginTran();
            try
            {
                _client.Ado.ExecuteCommand("""
                    ALTER TABLE FlowRuns ADD COLUMN TimeoutSeconds INTEGER NOT NULL DEFAULT 300;
                    """);
                RecordMigration(AddFlowRunTimeoutVersion, "flow-run-execution-timeout-v1");
                _client.Ado.CommitTran();
            }
            catch
            {
                _client.Ado.RollbackTran();
                throw;
            }
        }

        applied = _client.Ado.SqlQuery<int>("SELECT Version FROM SchemaMigrations ORDER BY Version");
        if (!applied.Contains(AddEnvironmentConsoleVersion))
        {
            _client.Ado.BeginTran();
            try
            {
                _client.Ado.ExecuteCommand("""
                    CREATE TABLE IF NOT EXISTS RunEnvironmentSettings (
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
                    CREATE TABLE IF NOT EXISTS FlowInterfaces (
                        Id TEXT NOT NULL PRIMARY KEY,
                        ProjectId TEXT NOT NULL,
                        FlowId TEXT NOT NULL,
                        Name TEXT NOT NULL,
                        InvocationMode TEXT NOT NULL,
                        IsEnabled INTEGER NOT NULL DEFAULT 1,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL,
                        FOREIGN KEY (ProjectId) REFERENCES Projects(Id),
                        FOREIGN KEY (FlowId) REFERENCES FlowDefinitions(Id)
                    );
                    CREATE INDEX IF NOT EXISTS IX_FlowInterfaces_ProjectId_FlowId ON FlowInterfaces(ProjectId, FlowId);
                    """);
                RecordMigration(AddEnvironmentConsoleVersion, "environment-console-v1");
                _client.Ado.CommitTran();
            }
            catch
            {
                _client.Ado.RollbackTran();
                throw;
            }
        }

        applied = _client.Ado.SqlQuery<int>("SELECT Version FROM SchemaMigrations ORDER BY Version");
        if (!applied.Contains(AddFlowRunOutputsVersion))
        {
            _client.Ado.BeginTran();
            try
            {
                _client.Ado.ExecuteCommand("""
                    CREATE TABLE IF NOT EXISTS FlowRunOutputs (
                        RunId TEXT NOT NULL,
                        Sequence INTEGER NOT NULL,
                        NodeId TEXT NOT NULL,
                        Outcome TEXT NOT NULL,
                        Branch TEXT NULL,
                        OutputsJson TEXT NOT NULL,
                        ErrorCode TEXT NULL,
                        ErrorMessage TEXT NULL,
                        Timestamp TEXT NOT NULL,
                        PRIMARY KEY (RunId, Sequence),
                        FOREIGN KEY (RunId) REFERENCES FlowRuns(Id)
                    );
                    CREATE INDEX IF NOT EXISTS IX_FlowRunOutputs_RunId_Sequence ON FlowRunOutputs(RunId, Sequence);
                    """);
                RecordMigration(AddFlowRunOutputsVersion, "flow-run-outputs-v1");
                _client.Ado.CommitTran();
            }
            catch
            {
                _client.Ado.RollbackTran();
                throw;
            }
        }

        applied = _client.Ado.SqlQuery<int>("SELECT Version FROM SchemaMigrations ORDER BY Version");
        if (!applied.Contains(AddFlowRunOutputInputsVersion))
        {
            _client.Ado.BeginTran();
            try
            {
                _client.Ado.ExecuteCommand("ALTER TABLE FlowRunOutputs ADD COLUMN InputsJson TEXT NOT NULL DEFAULT '{}';");
                RecordMigration(AddFlowRunOutputInputsVersion, "flow-run-output-inputs-v1");
                _client.Ado.CommitTran();
            }
            catch
            {
                _client.Ado.RollbackTran();
                throw;
            }
        }

        applied = _client.Ado.SqlQuery<int>("SELECT Version FROM SchemaMigrations ORDER BY Version");
        if (!applied.Contains(AddProjectLibraryReferencesVersion))
        {
            _client.Ado.BeginTran();
            try
            {
                _client.Ado.ExecuteCommand("""
                    ALTER TABLE Libraries ADD COLUMN Status TEXT NOT NULL DEFAULT 'Available';
                    ALTER TABLE Libraries ADD COLUMN ArchivedAt TEXT NULL;
                    CREATE TABLE IF NOT EXISTS ProjectLibraryReferences (
                        Id TEXT NOT NULL PRIMARY KEY,
                        ProjectId TEXT NOT NULL,
                        LibraryId TEXT NOT NULL,
                        ReferencedAt TEXT NOT NULL,
                        FOREIGN KEY (ProjectId) REFERENCES Projects(Id),
                        FOREIGN KEY (LibraryId) REFERENCES Libraries(Id)
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS UX_ProjectLibraryReferences_ProjectId_LibraryId
                    ON ProjectLibraryReferences(ProjectId, LibraryId);
                    CREATE INDEX IF NOT EXISTS IX_ProjectLibraryReferences_LibraryId
                    ON ProjectLibraryReferences(LibraryId);
                    CREATE INDEX IF NOT EXISTS IX_Libraries_Status_Name_Version
                    ON Libraries(Status, Name, Version);
                    """);
                PopulateProjectLibraryReferencesFromExistingFlows();
                RecordMigration(AddProjectLibraryReferencesVersion, "project-library-references-v1");
                _client.Ado.CommitTran();
            }
            catch
            {
                _client.Ado.RollbackTran();
                throw;
            }
        }

        applied = _client.Ado.SqlQuery<int>("SELECT Version FROM SchemaMigrations ORDER BY Version");
        if (!applied.Contains(AddLibraryEnumCatalogVersion))
        {
            _client.Ado.BeginTran();
            try
            {
                _client.Ado.ExecuteCommand("ALTER TABLE Libraries ADD COLUMN CatalogSchemaVersion INTEGER NOT NULL DEFAULT 0;");
                RecordMigration(AddLibraryEnumCatalogVersion, "library-enum-catalog-v1");
                _client.Ado.CommitTran();
            }
            catch
            {
                _client.Ado.RollbackTran();
                throw;
            }
        }
    }

    /// <summary>
    /// Makes the project-reference boundary safe for databases created before
    /// project library references existed. The data is read only from the
    /// persisted definitions; package metadata remains the authoritative
    /// catalog and unknown artifacts are deliberately not invented.
    /// 为引入项目类库引用前创建的数据库补齐归属关系。只读取现有流程定义；
    /// 类库目录仍是唯一权威来源，不会为未知制品伪造引用。
    /// </summary>
    private void PopulateProjectLibraryReferencesFromExistingFlows()
    {
        var knownLibraryIds = _client.Ado.SqlQuery<string>("SELECT Id FROM Libraries")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (knownLibraryIds.Count == 0)
        {
            return;
        }

        var definitions = _client.Ado.SqlQuery<FlowDefinitionLibraryReferenceMigrationRow>(
            "SELECT ProjectId, DefinitionJson FROM FlowDefinitions");
        var referencedAt = DateTimeOffset.UtcNow.ToString("O");
        foreach (var definition in definitions)
        {
            if (!Guid.TryParse(definition.ProjectId, out var projectId))
            {
                continue;
            }

            foreach (var libraryId in ReadLibraryIds(definition.DefinitionJson))
            {
                if (!knownLibraryIds.Contains(libraryId))
                {
                    continue;
                }

                var normalizedLibraryId = libraryId.Trim().ToLowerInvariant();
                _client.Ado.ExecuteCommand(
                    """
                    INSERT OR IGNORE INTO ProjectLibraryReferences (Id, ProjectId, LibraryId, ReferencedAt)
                    VALUES (@id, @projectId, @libraryId, @referencedAt)
                    """,
                    new SugarParameter("@id", $"{projectId:N}:{normalizedLibraryId}"),
                    new SugarParameter("@projectId", projectId.ToString("D")),
                    new SugarParameter("@libraryId", normalizedLibraryId),
                    new SugarParameter("@referencedAt", referencedAt));
            }
        }
    }

    private static List<string> ReadLibraryIds(string? definitionJson)
    {
        var libraryIds = new List<string>();
        if (string.IsNullOrWhiteSpace(definitionJson))
        {
            return libraryIds;
        }

        try
        {
            using var document = JsonDocument.Parse(definitionJson);
            if (!TryGetProperty(document.RootElement, "canvases", out var canvases)
                || canvases.ValueKind != JsonValueKind.Array)
            {
                return libraryIds;
            }

            foreach (var canvas in canvases.EnumerateArray())
            {
                if (!TryGetProperty(canvas, "nodes", out var nodes)
                    || nodes.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var node in nodes.EnumerateArray())
                {
                    if (!TryGetProperty(node, "ui", out var ui)
                        || ui.ValueKind != JsonValueKind.Object
                        || !TryGetProperty(ui, "libraryId", out var libraryId)
                        || libraryId.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var value = libraryId.GetString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        libraryIds.Add(value);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // A malformed historical definition is validated when it is next
            // saved or run. Do not block the entire database migration.
            // 历史流程定义损坏时留待保存或运行校验，不能阻塞整个数据库迁移。
        }

        return libraryIds;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private sealed class FlowDefinitionLibraryReferenceMigrationRow
    {
        public string ProjectId { get; set; } = string.Empty;

        public string DefinitionJson { get; set; } = string.Empty;
    }

    private void RecordMigration(int version, string checksum)
    {
        _client.Ado.ExecuteCommand(
            "INSERT INTO SchemaMigrations (Version, AppliedAt, Checksum) VALUES (@version, @appliedAt, @checksum)",
            new SugarParameter("@version", version),
            new SugarParameter("@appliedAt", DateTimeOffset.UtcNow.ToString("O")),
            new SugarParameter("@checksum", checksum));
    }
}

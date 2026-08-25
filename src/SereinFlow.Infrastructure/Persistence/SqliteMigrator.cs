using SqlSugar;

namespace SereinFlow.Infrastructure.Persistence;

public sealed class SqliteMigrator
{
    private const int InitialSchemaVersion = 1;
    private const int RemoveOrderPipelineSeedVersion = 2;
    private const int AddLibraryCatalogVersion = 3;
    private const int AddFlowRunSnapshotsVersion = 4;
    private const int AddFlowRunExecutionOptionsVersion = 5;
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

using SqlSugar;

namespace SereinFlow.Infrastructure.Persistence;

public sealed class SqliteMigrator
{
    private const int InitialSchemaVersion = 1;
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
        if (applied.Contains(InitialSchemaVersion))
        {
            return;
        }

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
            _client.Ado.ExecuteCommand(
                "INSERT INTO SchemaMigrations (Version, AppliedAt, Checksum) VALUES (@version, @appliedAt, @checksum)",
                new SugarParameter("@version", InitialSchemaVersion),
                new SugarParameter("@appliedAt", DateTimeOffset.UtcNow.ToString("O")),
                new SugarParameter("@checksum", "t0-sqlite-schema-v1"));
            _client.Ado.CommitTran();
        }
        catch
        {
            _client.Ado.RollbackTran();
            throw;
        }
    }
}

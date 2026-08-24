using System.Text.Json;
using System.Text.Json.Serialization;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SqlSugar;

namespace SereinFlow.Infrastructure.Persistence;

public sealed class SqlSugarFlowDefinitionRepository : IFlowDefinitionRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly SqliteDatabase _database;

    public SqlSugarFlowDefinitionRepository(SqliteDatabase database)
    {
        _database = database;
    }

    public IReadOnlyList<FlowDefinitionDto> ListByProject(Guid projectId)
        => _database.Query<FlowDefinitionRow>(
                "SELECT Id, ProjectId, Version, DefinitionJson, Checksum FROM FlowDefinitions WHERE ProjectId = @projectId ORDER BY Id",
                new SugarParameter("@projectId", projectId.ToString("D")))
            .Select(Deserialize)
            .ToArray();

    public FlowDefinitionDto? Find(Guid projectId, Guid flowId)
    {
        var row = _database.Query<FlowDefinitionRow>(
            "SELECT Id, ProjectId, Version, DefinitionJson, Checksum FROM FlowDefinitions WHERE Id = @id AND ProjectId = @projectId",
            new SugarParameter("@id", flowId.ToString("D")),
            new SugarParameter("@projectId", projectId.ToString("D"))).SingleOrDefault();
        return row is null ? null : Deserialize(row);
    }

    public void Add(Guid projectId, FlowDefinitionDto definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var serialized = Serialize(definition);
        _database.Execute(
            "INSERT INTO FlowDefinitions (Id, ProjectId, Version, DefinitionJson, Checksum) VALUES (@id, @projectId, @version, @definitionJson, @checksum)",
            new SugarParameter("@id", definition.Id.ToString("D")),
            new SugarParameter("@projectId", projectId.ToString("D")),
            new SugarParameter("@version", definition.Version),
            new SugarParameter("@definitionJson", serialized),
            new SugarParameter("@checksum", definition.Checksum));
        _database.Execute(
            "INSERT INTO FlowDefinitionVersions (FlowId, Version, DefinitionJson, Checksum) VALUES (@flowId, @version, @definitionJson, @checksum)",
            new SugarParameter("@flowId", definition.Id.ToString("D")),
            new SugarParameter("@version", definition.Version),
            new SugarParameter("@definitionJson", serialized),
            new SugarParameter("@checksum", definition.Checksum));
    }

    public FlowDefinitionDto? TryUpdate(Guid projectId, FlowDefinitionDto definition, long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedVersion, 1);
        var saved = definition with { Version = expectedVersion + 1 };
        var serialized = Serialize(saved);

        _database.Client.Ado.BeginTran();
        try
        {
            var affected = _database.Execute(
                "UPDATE FlowDefinitions SET Version = @version, DefinitionJson = @definitionJson, Checksum = @checksum WHERE Id = @id AND ProjectId = @projectId AND Version = @expectedVersion",
                new SugarParameter("@version", saved.Version),
                new SugarParameter("@definitionJson", serialized),
                new SugarParameter("@checksum", saved.Checksum),
                new SugarParameter("@id", saved.Id.ToString("D")),
                new SugarParameter("@projectId", projectId.ToString("D")),
                new SugarParameter("@expectedVersion", expectedVersion));
            if (affected != 1)
            {
                _database.Client.Ado.RollbackTran();
                return null;
            }

            _database.Execute(
                "INSERT INTO FlowDefinitionVersions (FlowId, Version, DefinitionJson, Checksum) VALUES (@flowId, @version, @definitionJson, @checksum)",
                new SugarParameter("@flowId", saved.Id.ToString("D")),
                new SugarParameter("@version", saved.Version),
                new SugarParameter("@definitionJson", serialized),
                new SugarParameter("@checksum", saved.Checksum));
            _database.Client.Ado.CommitTran();
            return saved;
        }
        catch
        {
            _database.Client.Ado.RollbackTran();
            throw;
        }
    }

    private static string Serialize(FlowDefinitionDto definition)
        => JsonSerializer.Serialize(definition, SerializerOptions);

    private static FlowDefinitionDto Deserialize(FlowDefinitionRow row)
    {
        var definition = JsonSerializer.Deserialize<FlowDefinitionDto>(row.DefinitionJson, SerializerOptions)
            ?? throw new InvalidOperationException($"Flow definition '{row.Id}' is empty.");
        return definition with { Version = row.Version, Checksum = row.Checksum };
    }

    private sealed class FlowDefinitionRow
    {
        public string Id { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public long Version { get; set; }
        public string DefinitionJson { get; set; } = string.Empty;
        public string Checksum { get; set; } = string.Empty;
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Infrastructure.Persistence;

public sealed class SqlSugarFlowDefinitionRepository : IFlowDefinitionRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = SereinJsonSerialization.CreateWebOptions(options =>
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

    private readonly IRepository<FlowDefinitionRecord> _definitions;
    private readonly IRepository<FlowDefinitionVersionRecord> _versions;
    private readonly IUnitOfWork _unitOfWork;

    public SqlSugarFlowDefinitionRepository(
        IRepository<FlowDefinitionRecord> definitions,
        IRepository<FlowDefinitionVersionRecord> versions,
        IUnitOfWork unitOfWork)
    {
        _definitions = definitions;
        _versions = versions;
        _unitOfWork = unitOfWork;
    }

    public SqlSugarFlowDefinitionRepository(SqliteDatabase database)
        : this(
            new SqlSugarRepository<FlowDefinitionRecord>(database.Client),
            new SqlSugarRepository<FlowDefinitionVersionRecord>(database.Client),
            new SqlSugarUnitOfWork(database.Client))
    {
    }

    public async Task<IReadOnlyList<FlowDefinitionDto>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => (await _definitions.ListAsync(cancellationToken: cancellationToken))
            .Where(row => row.ProjectId == projectId.ToString("D"))
            .Select(Deserialize)
            .OrderBy(static definition => definition.Id)
            .ToArray();

    public async Task<FlowDefinitionDto?> FindAsync(Guid projectId, Guid flowId, CancellationToken cancellationToken = default)
    {
        var rows = await _definitions.ListAsync(cancellationToken: cancellationToken);
        var row = rows.SingleOrDefault(item => item.Id == flowId.ToString("D") && item.ProjectId == projectId.ToString("D"));
        return row is null ? null : Deserialize(row);
    }

    public Task AddAsync(Guid projectId, FlowDefinitionDto definition, CancellationToken cancellationToken = default)
    {
        if (definition is null)
            throw new ArgumentNullException(nameof(definition), "The flow definition cannot be null. 流程定义不能为空。");
        var serialized = Serialize(definition);
        return _unitOfWork.ExecuteAsync(async token =>
        {
            await _definitions.AddAsync(ToRecord(projectId, definition, serialized), token);
            await _versions.AddAsync(ToVersionRecord(definition, serialized), token);
            return true;
        }, cancellationToken);
    }

    public Task<FlowDefinitionDto?> TryUpdateAsync(Guid projectId, FlowDefinitionDto definition, long expectedVersion, CancellationToken cancellationToken = default)
    {
        if (definition is null)
            throw new ArgumentNullException(nameof(definition), "The flow definition cannot be null. 流程定义不能为空。");
        if (expectedVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(expectedVersion), "Expected flow version must be positive. 期望流程版本必须为正数。");
        var saved = definition with { Version = expectedVersion + 1 };
        var serialized = Serialize(saved);
        return _unitOfWork.ExecuteAsync(async token =>
        {
            var current = (await _definitions.ListAsync(cancellationToken: token))
                .SingleOrDefault(item => item.Id == saved.Id.ToString("D") && item.ProjectId == projectId.ToString("D"));
            if (current is null || current.Version != expectedVersion)
                return null;
            await _definitions.UpdateAsync(ToRecord(projectId, saved, serialized), token);
            await _versions.AddAsync(ToVersionRecord(saved, serialized), token);
            return saved;
        }, cancellationToken);
    }

    public IReadOnlyList<FlowDefinitionDto> ListByProject(Guid projectId)
        => _definitions.ListAsync()
            .GetAwaiter().GetResult()
            .Where(row => row.ProjectId == projectId.ToString("D"))
            .Select(Deserialize)
            .OrderBy(static definition => definition.Id)
            .ToArray();

    public FlowDefinitionDto? Find(Guid projectId, Guid flowId)
    {
        var row = _definitions.ListAsync().GetAwaiter().GetResult()
            .SingleOrDefault(item => item.Id == flowId.ToString("D") && item.ProjectId == projectId.ToString("D"));
        return row is null ? null : Deserialize(row);
    }

    public void Add(Guid projectId, FlowDefinitionDto definition)
    {
        if (definition is null)
            throw new ArgumentNullException(nameof(definition), "The flow definition cannot be null. 流程定义不能为空。");
        var serialized = Serialize(definition);
        _unitOfWork.ExecuteAsync(async cancellationToken =>
        {
            await _definitions.AddAsync(ToRecord(projectId, definition, serialized), cancellationToken);
            await _versions.AddAsync(ToVersionRecord(definition, serialized), cancellationToken);
            return true;
        }).GetAwaiter().GetResult();
    }

    public FlowDefinitionDto? TryUpdate(Guid projectId, FlowDefinitionDto definition, long expectedVersion)
    {
        if (definition is null)
            throw new ArgumentNullException(nameof(definition), "The flow definition cannot be null. 流程定义不能为空。");
        if (expectedVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(expectedVersion), "Expected flow version must be positive. 期望流程版本必须为正数。");
        var saved = definition with { Version = expectedVersion + 1 };
        var serialized = Serialize(saved);

        return _unitOfWork.ExecuteAsync(async cancellationToken =>
        {
            var current = (await _definitions.ListAsync(cancellationToken: cancellationToken))
                .SingleOrDefault(item => item.Id == saved.Id.ToString("D") && item.ProjectId == projectId.ToString("D"));
            if (current is null || current.Version != expectedVersion)
                return null;
            await _definitions.UpdateAsync(ToRecord(projectId, saved, serialized), cancellationToken);
            await _versions.AddAsync(ToVersionRecord(saved, serialized), cancellationToken);
            return saved;
        }).GetAwaiter().GetResult();
    }

    private static string Serialize(FlowDefinitionDto definition)
        => JsonSerializer.Serialize(definition, SerializerOptions);

    private static FlowDefinitionRecord ToRecord(Guid projectId, FlowDefinitionDto definition, string serialized)
        => new()
        {
            Id = definition.Id.ToString("D"),
            ProjectId = projectId.ToString("D"),
            Version = definition.Version,
            DefinitionJson = serialized,
            Checksum = definition.Checksum
        };

    private static FlowDefinitionVersionRecord ToVersionRecord(FlowDefinitionDto definition, string serialized)
        => new()
        {
            FlowId = definition.Id.ToString("D"),
            Version = definition.Version,
            DefinitionJson = serialized,
            Checksum = definition.Checksum
        };

    private static FlowDefinitionDto Deserialize(FlowDefinitionRecord row)
    {
        var definition = JsonSerializer.Deserialize<FlowDefinitionDto>(row.DefinitionJson, SerializerOptions)
            ?? throw new InvalidOperationException($"Flow definition '{row.Id}' is empty. 流程定义“{row.Id}”为空。");
        return definition with { Version = row.Version, Checksum = row.Checksum };
    }

}

using System.Text.Json;
using System.Text.Json.Serialization;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Infrastructure.Persistence;

public sealed class SqlSugarFlowDefinitionRepository : IFlowDefinitionRepository, IFlowVersionRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = SereinJsonSerialization.CreateWebOptions(options =>
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

    private readonly IRepository<FlowDefinitionRecord> _definitions;
    private readonly IRepository<FlowDefinitionVersionRecord> _versions;
    private readonly IRepository<FlowProductionHeadRecord> _productionHeads;
    private readonly IRepository<FlowVersionCounterRecord> _versionCounters;
    private readonly IRepository<LibraryRecord>? _libraries;
    private readonly IRepository<FlowLibraryBindingRecord>? _libraryBindings;
    private readonly IUnitOfWork _unitOfWork;

    public SqlSugarFlowDefinitionRepository(
        IRepository<FlowDefinitionRecord> definitions,
        IRepository<FlowDefinitionVersionRecord> versions,
        IRepository<FlowProductionHeadRecord> productionHeads,
        IRepository<FlowVersionCounterRecord> versionCounters,
        IRepository<LibraryRecord>? libraries,
        IRepository<FlowLibraryBindingRecord>? libraryBindings,
        IUnitOfWork unitOfWork)
    {
        _definitions = definitions;
        _versions = versions;
        _productionHeads = productionHeads;
        _versionCounters = versionCounters;
        _libraries = libraries;
        _libraryBindings = libraryBindings;
        _unitOfWork = unitOfWork;
    }

    public SqlSugarFlowDefinitionRepository(SqliteDatabase database)
        : this(
            new SqlSugarRepository<FlowDefinitionRecord>(database.Client),
            new SqlSugarRepository<FlowDefinitionVersionRecord>(database.Client),
            new SqlSugarRepository<FlowProductionHeadRecord>(database.Client),
            new SqlSugarRepository<FlowVersionCounterRecord>(database.Client),
            new SqlSugarRepository<LibraryRecord>(database.Client),
            new SqlSugarRepository<FlowLibraryBindingRecord>(database.Client),
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
            await _versions.AddAsync(ToVersionRecord(
                definition,
                serialized,
                FlowVersionTrackDto.Development,
                FlowVersionOperationDto.Created,
                parentVersion: null,
                sourceVersion: null,
                remark: "创建开发版本",
                createdAt: DateTimeOffset.UtcNow), token);
            await _versionCounters.AddAsync(new FlowVersionCounterRecord
            {
                FlowId = definition.Id.ToString("D"),
                NextVersion = checked(definition.Version + 1),
            }, token);
            await WriteLibraryBindingsAsync(projectId, definition, token);
            return true;
        }, cancellationToken);
    }

    public Task<FlowDefinitionDto?> TryUpdateAsync(Guid projectId, FlowDefinitionDto definition, long expectedVersion, CancellationToken cancellationToken = default)
    {
        if (definition is null)
            throw new ArgumentNullException(nameof(definition), "The flow definition cannot be null. 流程定义不能为空。");
        if (expectedVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(expectedVersion), "Expected flow version must be positive. 期望流程版本必须为正数。");
        return _unitOfWork.ExecuteAsync(async token =>
        {
            var current = (await _definitions.ListAsync(cancellationToken: token))
                .SingleOrDefault(item => item.Id == definition.Id.ToString("D") && item.ProjectId == projectId.ToString("D"));
            if (current is null || current.Version != expectedVersion)
                return null;
            var saved = definition with { Version = await AllocateVersionAsync(definition.Id, token) };
            var serialized = Serialize(saved);
            await _definitions.UpdateAsync(ToRecord(projectId, saved, serialized), token);
            await _versions.AddAsync(ToVersionRecord(
                saved,
                serialized,
                FlowVersionTrackDto.Development,
                FlowVersionOperationDto.Saved,
                parentVersion: expectedVersion,
                sourceVersion: null,
                remark: "保存开发版本",
                createdAt: DateTimeOffset.UtcNow), token);
            await WriteLibraryBindingsAsync(projectId, saved, token);
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
            await _versions.AddAsync(ToVersionRecord(
                definition,
                serialized,
                FlowVersionTrackDto.Development,
                FlowVersionOperationDto.Created,
                parentVersion: null,
                sourceVersion: null,
                remark: "创建开发版本",
                createdAt: DateTimeOffset.UtcNow), cancellationToken);
            await _versionCounters.AddAsync(new FlowVersionCounterRecord
            {
                FlowId = definition.Id.ToString("D"),
                NextVersion = checked(definition.Version + 1),
            }, cancellationToken);
            await WriteLibraryBindingsAsync(projectId, definition, cancellationToken);
            return true;
        }).GetAwaiter().GetResult();
    }

    public FlowDefinitionDto? TryUpdate(Guid projectId, FlowDefinitionDto definition, long expectedVersion)
    {
        if (definition is null)
            throw new ArgumentNullException(nameof(definition), "The flow definition cannot be null. 流程定义不能为空。");
        if (expectedVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(expectedVersion), "Expected flow version must be positive. 期望流程版本必须为正数。");
        return _unitOfWork.ExecuteAsync(async cancellationToken =>
        {
            var current = (await _definitions.ListAsync(cancellationToken: cancellationToken))
                .SingleOrDefault(item => item.Id == definition.Id.ToString("D") && item.ProjectId == projectId.ToString("D"));
            if (current is null || current.Version != expectedVersion)
                return null;
            var saved = definition with { Version = await AllocateVersionAsync(definition.Id, cancellationToken) };
            var serialized = Serialize(saved);
            await _definitions.UpdateAsync(ToRecord(projectId, saved, serialized), cancellationToken);
            await _versions.AddAsync(ToVersionRecord(
                saved,
                serialized,
                FlowVersionTrackDto.Development,
                FlowVersionOperationDto.Saved,
                parentVersion: expectedVersion,
                sourceVersion: null,
                remark: "保存开发版本",
                createdAt: DateTimeOffset.UtcNow), cancellationToken);
            await WriteLibraryBindingsAsync(projectId, saved, cancellationToken);
            return saved;
        }).GetAwaiter().GetResult();
    }

    public async Task<IReadOnlyList<FlowVersionSummaryDto>> ListVersionsAsync(
        Guid projectId,
        Guid flowId,
        FlowVersionTrackDto track,
        CancellationToken cancellationToken = default)
    {
        var current = await FindCurrentRecordAsync(projectId, flowId, cancellationToken);
        if (current is null)
            return [];

        var productionVersion = track == FlowVersionTrackDto.Production
            ? await FindProductionVersionAsync(projectId, flowId, cancellationToken)
            : null;
        var flowKey = flowId.ToString("D");
        var versions = await _versions.ListAsync(row => row.FlowId == flowKey, cancellationToken);
        return versions
            .Where(row => ParseTrack(row.Track) == track)
            .OrderByDescending(static row => row.Version)
            .Select(row => ToSummary(
                flowId,
                row,
                isCurrent: track == FlowVersionTrackDto.Development
                    ? row.Version == current.Version
                    : row.Version == productionVersion))
            .ToArray();
    }

    public async Task<FlowVersionDetailDto?> FindVersionAsync(
        Guid projectId,
        Guid flowId,
        long version,
        CancellationToken cancellationToken = default)
    {
        var current = await FindCurrentRecordAsync(projectId, flowId, cancellationToken);
        if (current is null)
            return null;

        var record = await FindVersionRecordAsync(flowId, version, cancellationToken);
        if (record is null)
            return null;

        var track = ParseTrack(record.Track);
        var productionVersion = track == FlowVersionTrackDto.Production
            ? await FindProductionVersionAsync(projectId, flowId, cancellationToken)
            : null;
        return new FlowVersionDetailDto(
            ToSummary(
                flowId,
                record,
                track == FlowVersionTrackDto.Development
                    ? record.Version == current.Version
                    : record.Version == productionVersion),
            Deserialize(record));
    }

    public async Task<FlowDefinitionDto?> FindProductionDefinitionAsync(
        Guid projectId,
        Guid flowId,
        CancellationToken cancellationToken = default)
    {
        if (await FindCurrentRecordAsync(projectId, flowId, cancellationToken) is null)
            return null;

        var head = await _productionHeads.GetByIdAsync(flowId.ToString("D"), cancellationToken);
        if (head is null)
            return null;
        var version = await FindVersionRecordAsync(flowId, head.Version, cancellationToken);
        return version is null || ParseTrack(version.Track) != FlowVersionTrackDto.Production
            ? null
            : Deserialize(version);
    }

    public async Task<long?> FindProductionVersionAsync(
        Guid projectId,
        Guid flowId,
        CancellationToken cancellationToken = default)
    {
        if (await FindCurrentRecordAsync(projectId, flowId, cancellationToken) is null)
            return null;
        return (await _productionHeads.GetByIdAsync(flowId.ToString("D"), cancellationToken))?.Version;
    }

    public Task<FlowVersionMutationResult> PublishAsync(
        Guid projectId,
        Guid flowId,
        long expectedDevelopmentVersion,
        string? remark,
        long? expectedProductionVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (expectedDevelopmentVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(expectedDevelopmentVersion), "Expected development version must be positive. 期望开发版本必须为正数。");

        return _unitOfWork.ExecuteAsync(async token =>
        {
            var current = await FindCurrentRecordAsync(projectId, flowId, token);
            if (current is null || current.Version != expectedDevelopmentVersion)
                return new FlowVersionMutationResult(null, null, current?.Version);

            var source = await FindVersionRecordAsync(flowId, current.Version, token);
            if (source is null || ParseTrack(source.Track) != FlowVersionTrackDto.Development)
                return new FlowVersionMutationResult(null, null, current.Version);

            var previousHead = await _productionHeads.GetByIdAsync(flowId.ToString("D"), token);
            if (previousHead?.Version != expectedProductionVersion)
                return new FlowVersionMutationResult(null, null, previousHead?.Version);

            var published = Deserialize(source) with { Version = await AllocateVersionAsync(flowId, token) };
            var serialized = Serialize(published);
            var createdAt = DateTimeOffset.UtcNow;
            var version = ToVersionRecord(
                published,
                serialized,
                FlowVersionTrackDto.Production,
                FlowVersionOperationDto.Published,
                previousHead?.Version,
                source.Version,
                NormalizeRemark(remark, $"由开发版本 v{source.Version} 发布"),
                createdAt);
            await _versions.AddAsync(version, token);
            await WriteLibraryBindingsAsync(projectId, published, token);
            await UpsertProductionHeadAsync(flowId, published.Version, createdAt, token);
            return new FlowVersionMutationResult(
                Deserialize(current),
                ToSummary(flowId, version, isCurrent: true));
        }, cancellationToken);
    }

    public Task<FlowVersionMutationResult> RollbackAsync(
        Guid projectId,
        Guid flowId,
        long sourceVersion,
        FlowVersionTrackDto track,
        long expectedHeadVersion,
        CancellationToken cancellationToken = default)
    {
        if (sourceVersion < 1 || expectedHeadVersion < 1 || !Enum.IsDefined(track))
            throw new ArgumentOutOfRangeException(nameof(sourceVersion), "The rollback source and expected head must be valid. 回滚来源和期望头版本必须有效。");

        return _unitOfWork.ExecuteAsync(async token =>
        {
            var current = await FindCurrentRecordAsync(projectId, flowId, token);
            if (current is null)
                return new FlowVersionMutationResult(null, null);

            var currentHead = track == FlowVersionTrackDto.Development
                ? current.Version
                : (await _productionHeads.GetByIdAsync(flowId.ToString("D"), token))?.Version;
            if (currentHead != expectedHeadVersion)
                return new FlowVersionMutationResult(null, null, currentHead);

            var source = await FindVersionRecordAsync(flowId, sourceVersion, token);
            if (source is null || ParseTrack(source.Track) != track)
                return new FlowVersionMutationResult(null, null, currentHead);

            var restored = Deserialize(source) with { Version = await AllocateVersionAsync(flowId, token) };
            var serialized = Serialize(restored);
            var createdAt = DateTimeOffset.UtcNow;
            var version = ToVersionRecord(
                restored,
                serialized,
                track,
                FlowVersionOperationDto.RolledBack,
                currentHead,
                sourceVersion,
                $"由{TrackName(track)}版本 v{sourceVersion} 回滚",
                createdAt);
            await _versions.AddAsync(version, token);
            await WriteLibraryBindingsAsync(projectId, restored, token);

            if (track == FlowVersionTrackDto.Development)
            {
                await _definitions.UpdateAsync(ToRecord(projectId, restored, serialized), token);
                return new FlowVersionMutationResult(restored, ToSummary(flowId, version, isCurrent: true));
            }

            await UpsertProductionHeadAsync(flowId, restored.Version, createdAt, token);
            return new FlowVersionMutationResult(Deserialize(current), ToSummary(flowId, version, isCurrent: true));
        }, cancellationToken);
    }

    public async Task<bool> IsLibraryReferencedByProductionHistoryAsync(
        Guid projectId,
        string libraryId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(libraryId))
            return false;

        var projectKey = projectId.ToString("D");
        var flowIds = (await _definitions.ListAsync(row => row.ProjectId == projectKey, cancellationToken))
            .Select(static row => row.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (flowIds.Count == 0)
            return false;

        var versions = await _versions.ListAsync(cancellationToken: cancellationToken);
        foreach (var version in versions.Where(row => flowIds.Contains(row.FlowId) && ParseTrack(row.Track) == FlowVersionTrackDto.Production))
        {
            try
            {
                if (LibraryBindingIndex.Extract(Deserialize(version)).Contains(libraryId.Trim(), StringComparer.OrdinalIgnoreCase))
                    return true;
            }
            catch (JsonException)
            {
                // An unreadable production history must be treated as in use.
                // 无法读取的生产历史必须按仍在使用处理，不能据此删除依赖。
                return true;
            }
        }
        return false;
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

    private static FlowDefinitionVersionRecord ToVersionRecord(
        FlowDefinitionDto definition,
        string serialized,
        FlowVersionTrackDto track,
        FlowVersionOperationDto operation,
        long? parentVersion,
        long? sourceVersion,
        string remark,
        DateTimeOffset? createdAt)
        => new()
        {
            FlowId = definition.Id.ToString("D"),
            Version = definition.Version,
            DefinitionJson = serialized,
            Checksum = definition.Checksum,
            Track = track.ToString(),
            Operation = operation.ToString(),
            ParentVersion = parentVersion,
            SourceVersion = sourceVersion,
            Remark = remark,
            CreatedAt = createdAt?.ToString("O"),
        };

    private static FlowDefinitionDto Deserialize(FlowDefinitionRecord row)
    {
        var definition = JsonSerializer.Deserialize<FlowDefinitionDto>(row.DefinitionJson, SerializerOptions)
            ?? throw new InvalidOperationException($"Flow definition '{row.Id}' is empty. 流程定义“{row.Id}”为空。");
        return definition with { Version = row.Version, Checksum = row.Checksum };
    }

    private static FlowDefinitionDto Deserialize(FlowDefinitionVersionRecord row)
    {
        var definition = JsonSerializer.Deserialize<FlowDefinitionDto>(row.DefinitionJson, SerializerOptions)
            ?? throw new InvalidOperationException($"Flow definition version '{row.FlowId}:{row.Version}' is empty. 流程定义版本“{row.FlowId}:{row.Version}”为空。");
        return definition with { Version = row.Version, Checksum = row.Checksum };
    }

    private async Task<FlowDefinitionRecord?> FindCurrentRecordAsync(
        Guid projectId,
        Guid flowId,
        CancellationToken cancellationToken)
    {
        var row = await _definitions.GetByIdAsync(flowId.ToString("D"), cancellationToken);
        return row is not null && string.Equals(row.ProjectId, projectId.ToString("D"), StringComparison.OrdinalIgnoreCase)
            ? row
            : null;
    }

    private async Task<FlowDefinitionVersionRecord?> FindVersionRecordAsync(
        Guid flowId,
        long version,
        CancellationToken cancellationToken)
    {
        var flowKey = flowId.ToString("D");
        return (await _versions.ListAsync(row => row.FlowId == flowKey, cancellationToken))
            .SingleOrDefault(row => row.Version == version);
    }

    private async Task<long> AllocateVersionAsync(Guid flowId, CancellationToken cancellationToken)
    {
        var key = flowId.ToString("D");
        var counter = await _versionCounters.GetByIdAsync(key, cancellationToken);
        if (counter is null)
        {
            var knownVersions = await _versions.ListAsync(row => row.FlowId == key, cancellationToken);
            var nextVersion = Math.Max(1, knownVersions.Count == 0 ? 1 : checked(knownVersions.Max(static row => row.Version) + 1));
            counter = new FlowVersionCounterRecord { FlowId = key, NextVersion = checked(nextVersion + 1) };
            await _versionCounters.AddAsync(counter, cancellationToken);
            return nextVersion;
        }

        var allocated = Math.Max(1, counter.NextVersion);
        counter.NextVersion = checked(allocated + 1);
        await _versionCounters.UpdateAsync(counter, cancellationToken);
        return allocated;
    }

    private async Task UpsertProductionHeadAsync(
        Guid flowId,
        long version,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        var key = flowId.ToString("D");
        var head = await _productionHeads.GetByIdAsync(key, cancellationToken);
        if (head is null)
        {
            await _productionHeads.AddAsync(new FlowProductionHeadRecord
            {
                FlowId = key,
                Version = version,
                UpdatedAt = updatedAt.ToString("O"),
            }, cancellationToken);
            return;
        }

        head.Version = version;
        head.UpdatedAt = updatedAt.ToString("O");
        await _productionHeads.UpdateAsync(head, cancellationToken);
    }

    private static FlowVersionSummaryDto ToSummary(
        Guid flowId,
        FlowDefinitionVersionRecord record,
        bool isCurrent)
        => new(
            flowId,
            record.Version,
            ParseTrack(record.Track),
            ParseOperation(record.Operation),
            record.ParentVersion,
            record.SourceVersion,
            record.Remark,
            DateTimeOffset.TryParse(record.CreatedAt, out var createdAt) ? createdAt : null,
            isCurrent);

    private static FlowVersionTrackDto ParseTrack(string? value)
        => Enum.TryParse<FlowVersionTrackDto>(value, ignoreCase: true, out var track)
            ? track
            : FlowVersionTrackDto.Development;

    private static FlowVersionOperationDto ParseOperation(string? value)
        => Enum.TryParse<FlowVersionOperationDto>(value, ignoreCase: true, out var operation)
            ? operation
            : FlowVersionOperationDto.Imported;

    private static string NormalizeRemark(string? remark, string fallback)
        => string.IsNullOrWhiteSpace(remark) ? fallback : remark.Trim();

    private static string TrackName(FlowVersionTrackDto track)
        => track == FlowVersionTrackDto.Development ? "开发" : "生产";

    private async Task WriteLibraryBindingsAsync(
        Guid projectId,
        FlowDefinitionDto definition,
        CancellationToken cancellationToken)
    {
        if (_libraryBindings is null || _libraries is null)
            return;

        foreach (var artifactId in LibraryBindingIndex.Extract(definition))
        {
            // The index is derived metadata. Historical definitions may carry
            // an invalid or manually authored library ID, so never manufacture
            // a catalog row just to satisfy this foreign key.
            // 该索引属于派生元数据。历史流程可能保留无效或手工填写的类库 ID，
            // 因此不能为了满足外键而伪造类库目录记录。
            if (await _libraries.GetByIdAsync(artifactId, cancellationToken) is null)
                continue;

            await _libraryBindings.AddAsync(new FlowLibraryBindingRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                ProjectId = projectId.ToString("D"),
                FlowId = definition.Id.ToString("D"),
                FlowVersion = definition.Version,
                LibraryArtifactId = artifactId,
                CreatedAt = DateTimeOffset.UtcNow.ToString("O")
            }, cancellationToken);
        }
    }

}

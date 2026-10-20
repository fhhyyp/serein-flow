using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Infrastructure.Persistence;

public sealed class SqlSugarFlowDefinitionRepository : IFlowDefinitionRepository, IFlowVersionRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = SereinJsonSerialization.CreateWebOptions(options =>
    {
        var jsonStringEnumConverter = new JsonStringEnumConverter(JsonNamingPolicy.CamelCase);
        options.Converters.Add(jsonStringEnumConverter);
    });
    private readonly IRepository<FlowDefinitionRecord> _definitions;
    private readonly IRepository<FlowDefinitionVersionRecord> _versions;
    private readonly IRepository<FlowProductionHeadRecord> _productionHeads;
    private readonly IRepository<FlowVersionCounterRecord> _versionCounters;
    private readonly IRepository<LibraryRecord>? _libraries;
    private readonly IRepository<FlowLibraryBindingRecord>? _libraryBindings;
    private readonly IUnitOfWork _unitOfWork;
    public SqlSugarFlowDefinitionRepository(IRepository<FlowDefinitionRecord> definitions, IRepository<FlowDefinitionVersionRecord> versions, IRepository<FlowProductionHeadRecord> productionHeads, IRepository<FlowVersionCounterRecord> versionCounters, IRepository<LibraryRecord>? libraries, IRepository<FlowLibraryBindingRecord>? libraryBindings, IUnitOfWork unitOfWork)
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
    {
        var definitions = new SqlSugarRepository<FlowDefinitionRecord>(database.Client);
        var versions = new SqlSugarRepository<FlowDefinitionVersionRecord>(database.Client);
        var productionHeads = new SqlSugarRepository<FlowProductionHeadRecord>(database.Client);
        var versionCounters = new SqlSugarRepository<FlowVersionCounterRecord>(database.Client);
        var libraries = new SqlSugarRepository<LibraryRecord>(database.Client);
        var libraryBindings = new SqlSugarRepository<FlowLibraryBindingRecord>(database.Client);
        var unitOfWork = new SqlSugarUnitOfWork(database.Client);
        _definitions = definitions;
        _versions = versions;
        _productionHeads = productionHeads;
        _versionCounters = versionCounters;
        _libraries = libraries;
        _libraryBindings = libraryBindings;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<FlowDefinitionDto>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var toArrayResult = (await _definitions.ListAsync(cancellationToken: cancellationToken))
            .Where(row =>
            {
                var evaluatedValue = row.ProjectId == projectId.ToString("D");
                return evaluatedValue;
            })
            .Select(Deserialize)
            .OrderBy(static definition => definition.Id)
            .ToArray();
        return toArrayResult;
    }

    public async Task<FlowDefinitionDto?> FindAsync(Guid projectId, Guid flowId, CancellationToken cancellationToken = default)
    {
        var rows = await _definitions.ListAsync(cancellationToken: cancellationToken);
        var row = rows.SingleOrDefault(item =>
        {
            var evaluatedValue2 = item.Id == flowId.ToString("D") && item.ProjectId == projectId.ToString("D");
            return evaluatedValue2;
        });
        FlowDefinitionDto? evaluatedValue3 = row is null ? null : Deserialize(row);
        return evaluatedValue3;
    }

    public Task AddAsync(Guid projectId, FlowDefinitionDto definition, CancellationToken cancellationToken = default)
    {
        if (definition is null)
        {
            throw new ArgumentNullException(nameof(definition), "The flow definition cannot be null. 流程定义不能为空。");
        }

        var serialized = Serialize(definition);
        var executeAsyncResult = _unitOfWork.ExecuteAsync(async token =>
        {
            var toRecordResult = ToRecord(projectId, definition, serialized);
            await _definitions.AddAsync(toRecordResult, token);
            var toVersionRecordResult = ToVersionRecord(definition, serialized, FlowVersionTrackDto.Development, FlowVersionOperationDto.Created, parentVersion: null, sourceVersion: null, remark: "创建开发版本", createdAt: DateTimeOffset.UtcNow);
            await _versions.AddAsync(toVersionRecordResult, token);
            var flowVersionCounterRecord = new FlowVersionCounterRecord
            {
                FlowId = definition.Id.ToString("D"),
                NextVersion = checked(definition.Version + 1),
            };
            await _versionCounters.AddAsync(flowVersionCounterRecord, token);
            await WriteLibraryBindingsAsync(projectId, definition, token);
            return true;
        }, cancellationToken);
        return executeAsyncResult;
    }

    public Task<FlowDefinitionDto?> TryUpdateAsync(Guid projectId, FlowDefinitionDto definition, long expectedVersion, CancellationToken cancellationToken = default)
    {
        if (definition is null)
        {
            throw new ArgumentNullException(nameof(definition), "The flow definition cannot be null. 流程定义不能为空。");
        }

        if (expectedVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedVersion), "Expected flow version must be positive. 期望流程版本必须为正数。");
        }

        var executeAsyncResult8 = _unitOfWork.ExecuteAsync(async token =>
        {
            var current = (await _definitions.ListAsync(cancellationToken: token))
                .SingleOrDefault(item =>
                {
                    var evaluatedValue4 = item.Id == definition.Id.ToString("D") && item.ProjectId == projectId.ToString("D");
                    return evaluatedValue4;
                });
            if (current is null || current.Version != expectedVersion)
            {
                return null;
            }

            var saved = definition with
            {
                Version = await AllocateVersionAsync(definition.Id, token)
            };
            var serialized = Serialize(saved);
            var toRecordResult6 = ToRecord(projectId, saved, serialized);
            await _definitions.UpdateAsync(toRecordResult6, token);
            var toVersionRecordResult7 = ToVersionRecord(saved, serialized, FlowVersionTrackDto.Development, FlowVersionOperationDto.Saved, parentVersion: expectedVersion, sourceVersion: null, remark: "保存开发版本", createdAt: DateTimeOffset.UtcNow);
            await _versions.AddAsync(toVersionRecordResult7, token);
            await WriteLibraryBindingsAsync(projectId, saved, token);
            return saved;
        }, cancellationToken);
        return executeAsyncResult8;
    }

    public IReadOnlyList<FlowDefinitionDto> ListByProject(Guid projectId)
    {
        var listAsyncResult = _definitions.ListAsync();
        var getAwaiterResult = listAsyncResult.GetAwaiter();
        var toArrayResult13 = getAwaiterResult.GetResult()
            .Where(row =>
            {
                var evaluatedValue9 = row.ProjectId == projectId.ToString("D");
                return evaluatedValue9;
            })
            .Select(Deserialize)
            .OrderBy(static definition => definition.Id)
            .ToArray();
        return toArrayResult13;
    }

    public FlowDefinitionDto? Find(Guid projectId, Guid flowId)
    {
        var listAsyncResult15 = _definitions.ListAsync();
        var getAwaiterResult16 = listAsyncResult15.GetAwaiter();
        var row = getAwaiterResult16.GetResult()
            .SingleOrDefault(item =>
            {
                var evaluatedValue14 = item.Id == flowId.ToString("D") && item.ProjectId == projectId.ToString("D");
                return evaluatedValue14;
            });
        FlowDefinitionDto? evaluatedValue18 = row is null ? null : Deserialize(row);
        return evaluatedValue18;
    }

    public void Add(Guid projectId, FlowDefinitionDto definition)
    {
        if (definition is null)
        {
            throw new ArgumentNullException(nameof(definition), "The flow definition cannot be null. 流程定义不能为空。");
        }

        var serialized = Serialize(definition);
        var executeAsyncResult22 = _unitOfWork.ExecuteAsync(async cancellationToken =>
        {
            var toRecordResult19 = ToRecord(projectId, definition, serialized);
            await _definitions.AddAsync(toRecordResult19, cancellationToken);
            var toVersionRecordResult20 = ToVersionRecord(definition, serialized, FlowVersionTrackDto.Development, FlowVersionOperationDto.Created, parentVersion: null, sourceVersion: null, remark: "创建开发版本", createdAt: DateTimeOffset.UtcNow);
            await _versions.AddAsync(toVersionRecordResult20, cancellationToken);
            var flowVersionCounterRecord21 = new FlowVersionCounterRecord
            {
                FlowId = definition.Id.ToString("D"),
                NextVersion = checked(definition.Version + 1),
            };
            await _versionCounters.AddAsync(flowVersionCounterRecord21, cancellationToken);
            await WriteLibraryBindingsAsync(projectId, definition, cancellationToken);
            return true;
        });
        var getAwaiterResult23 = executeAsyncResult22.GetAwaiter();
        getAwaiterResult23.GetResult();
    }

    public FlowDefinitionDto? TryUpdate(Guid projectId, FlowDefinitionDto definition, long expectedVersion)
    {
        if (definition is null)
        {
            throw new ArgumentNullException(nameof(definition), "The flow definition cannot be null. 流程定义不能为空。");
        }

        if (expectedVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedVersion), "Expected flow version must be positive. 期望流程版本必须为正数。");
        }

        var executeAsyncResult28 = _unitOfWork.ExecuteAsync(async cancellationToken =>
        {
            var current = (await _definitions.ListAsync(cancellationToken: cancellationToken))
                .SingleOrDefault(item =>
                {
                    var evaluatedValue24 = item.Id == definition.Id.ToString("D") && item.ProjectId == projectId.ToString("D");
                    return evaluatedValue24;
                });
            if (current is null || current.Version != expectedVersion)
            {
                return null;
            }

            var saved = definition with
            {
                Version = await AllocateVersionAsync(definition.Id, cancellationToken)
            };
            var serialized = Serialize(saved);
            var toRecordResult26 = ToRecord(projectId, saved, serialized);
            await _definitions.UpdateAsync(toRecordResult26, cancellationToken);
            var toVersionRecordResult27 = ToVersionRecord(saved, serialized, FlowVersionTrackDto.Development, FlowVersionOperationDto.Saved, parentVersion: expectedVersion, sourceVersion: null, remark: "保存开发版本", createdAt: DateTimeOffset.UtcNow);
            await _versions.AddAsync(toVersionRecordResult27, cancellationToken);
            await WriteLibraryBindingsAsync(projectId, saved, cancellationToken);
            return saved;
        });
        var getAwaiterResult29 = executeAsyncResult28.GetAwaiter();
        var getResultResult30 = getAwaiterResult29.GetResult();
        return getResultResult30;
    }

    public async Task<IReadOnlyList<FlowVersionSummaryDto>> ListVersionsAsync(Guid projectId, Guid flowId, FlowVersionTrackDto track, CancellationToken cancellationToken = default)
    {
        var current = await FindCurrentRecordAsync(projectId, flowId, cancellationToken);
        if (current is null)
        {
            return [];
        }

        var productionVersion = track == FlowVersionTrackDto.Production ? await FindProductionVersionAsync(projectId, flowId, cancellationToken) : null;
        var flowKey = flowId.ToString("D");
        var versions = await _versions.ListAsync(row => row.FlowId == flowKey, cancellationToken);
        var toArrayResult34 = versions
            .Where(row =>
            {
                var evaluatedValue31 = ParseTrack(row.Track) == track;
                return evaluatedValue31;
            })
            .OrderByDescending(static row => row.Version)
            .Select(row =>
            {
                var toSummaryResult = ToSummary(flowId, row, isCurrent: track == FlowVersionTrackDto.Development ? row.Version == current.Version : row.Version == productionVersion);
                return toSummaryResult;
            })
            .ToArray();
        return toArrayResult34;
    }

    public async Task<FlowVersionDetailDto?> FindVersionAsync(Guid projectId, Guid flowId, long version, CancellationToken cancellationToken = default)
    {
        var current = await FindCurrentRecordAsync(projectId, flowId, cancellationToken);
        if (current is null)
        {
            return null;
        }

        var record = await FindVersionRecordAsync(flowId, version, cancellationToken);
        if (record is null)
        {
            return null;
        }

        var track = ParseTrack(record.Track);
        var productionVersion = track == FlowVersionTrackDto.Production ? await FindProductionVersionAsync(projectId, flowId, cancellationToken) : null;
        var toSummaryResult35 = ToSummary(flowId, record, track == FlowVersionTrackDto.Development ? record.Version == current.Version : record.Version == productionVersion);
        var deserializeResult = Deserialize(record);
        var flowVersionDetailDto = new FlowVersionDetailDto(toSummaryResult35, deserializeResult);
        return flowVersionDetailDto;
    }

    public async Task<FlowDefinitionDto?> FindProductionDefinitionAsync(Guid projectId, Guid flowId, CancellationToken cancellationToken = default)
    {
        if (await FindCurrentRecordAsync(projectId, flowId, cancellationToken) is null)
        {
            return null;
        }

        var toStringResult = flowId.ToString("D");
        var head = await _productionHeads.GetByIdAsync(toStringResult, cancellationToken);
        if (head is null)
        {
            return null;
        }

        var version = await FindVersionRecordAsync(flowId, head.Version, cancellationToken);
        FlowDefinitionDto? evaluatedValue36 = version is null || ParseTrack(version.Track) != FlowVersionTrackDto.Production ? null : Deserialize(version);
        return evaluatedValue36;
    }

    public async Task<long?> FindProductionVersionAsync(Guid projectId, Guid flowId, CancellationToken cancellationToken = default)
    {
        if (await FindCurrentRecordAsync(projectId, flowId, cancellationToken) is null)
        {
            return null;
        }

        var toStringResult37 = flowId.ToString("D");
        var optionalReceiver = (await _productionHeads.GetByIdAsync(toStringResult37, cancellationToken));
        long? optionalResult = default(long?);
        if (optionalReceiver is not null)
        {
            optionalResult = optionalReceiver.Version;
        }

        return optionalResult;
    }

    public Task<FlowVersionMutationResult> PublishAsync(Guid projectId, Guid flowId, long expectedDevelopmentVersion, string? remark, long? expectedProductionVersion = null, CancellationToken cancellationToken = default)
    {
        if (expectedDevelopmentVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedDevelopmentVersion), "Expected development version must be positive. 期望开发版本必须为正数。");
        }

        var executeAsyncResult44 = _unitOfWork.ExecuteAsync(async token =>
        {
            var current = await FindCurrentRecordAsync(projectId, flowId, token);
            if (current is null || current.Version != expectedDevelopmentVersion)
            {
                var flowVersionMutationResult = new FlowVersionMutationResult(null, null, current?.Version);
                return flowVersionMutationResult;
            }

            var source = await FindVersionRecordAsync(flowId, current.Version, token);
            if (source is null || ParseTrack(source.Track) != FlowVersionTrackDto.Development)
            {
                var flowVersionMutationResult38 = new FlowVersionMutationResult(null, null, current.Version);
                return flowVersionMutationResult38;
            }

            var toStringResult40 = flowId.ToString("D");
            var previousHead = await _productionHeads.GetByIdAsync(toStringResult40, token);
            if (previousHead?.Version != expectedProductionVersion)
            {
                var flowVersionMutationResult39 = new FlowVersionMutationResult(null, null, previousHead?.Version);
                return flowVersionMutationResult39;
            }

            var published = Deserialize(source) with
            {
                Version = await AllocateVersionAsync(flowId, token)
            };
            var serialized = Serialize(published);
            var createdAt = DateTimeOffset.UtcNow;
            var normalizeRemarkResult = NormalizeRemark(remark, $"由开发版本 v{source.Version} 发布");
            var version = ToVersionRecord(published, serialized, FlowVersionTrackDto.Production, FlowVersionOperationDto.Published, previousHead?.Version, source.Version, normalizeRemarkResult, createdAt);
            await _versions.AddAsync(version, token);
            await WriteLibraryBindingsAsync(projectId, published, token);
            await UpsertProductionHeadAsync(flowId, published.Version, createdAt, token);
            var deserializeResult41 = Deserialize(current);
            var toSummaryResult42 = ToSummary(flowId, version, isCurrent: true);
            var flowVersionMutationResult43 = new FlowVersionMutationResult(deserializeResult41, toSummaryResult42);
            return flowVersionMutationResult43;
        }, cancellationToken);
        return executeAsyncResult44;
    }

    public Task<FlowVersionMutationResult> RollbackAsync(Guid projectId, Guid flowId, long sourceVersion, FlowVersionTrackDto track, long expectedHeadVersion, CancellationToken cancellationToken = default)
    {
        if (sourceVersion < 1 || expectedHeadVersion < 1 || !Enum.IsDefined(track))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceVersion), "The rollback source and expected head must be valid. 回滚来源和期望头版本必须有效。");
        }

        var executeAsyncResult59 = _unitOfWork.ExecuteAsync(async token =>
        {
            var current = await FindCurrentRecordAsync(projectId, flowId, token);
            if (current is null)
            {
                var flowVersionMutationResult45 = new FlowVersionMutationResult(null, null);
                return flowVersionMutationResult45;
            }

            long? currentHead;
            if (track == FlowVersionTrackDto.Development)
            {
                currentHead = current.Version;
            }
            else
            {
                var productionFlowKey = flowId.ToString("D");
                var productionHead = await _productionHeads.GetByIdAsync(productionFlowKey, token);
                currentHead = productionHead?.Version;
            }

            if (currentHead != expectedHeadVersion)
            {
                var flowVersionMutationResult46 = new FlowVersionMutationResult(null, null, currentHead);
                return flowVersionMutationResult46;
            }

            var source = await FindVersionRecordAsync(flowId, sourceVersion, token);
            if (source is null || ParseTrack(source.Track) != track)
            {
                var flowVersionMutationResult47 = new FlowVersionMutationResult(null, null, currentHead);
                return flowVersionMutationResult47;
            }

            var restored = Deserialize(source) with
            {
                Version = await AllocateVersionAsync(flowId, token)
            };
            var serialized = Serialize(restored);
            var createdAt = DateTimeOffset.UtcNow;
            var evaluatedValue55 = $"由{TrackName(track)}版本 v{sourceVersion} 回滚";
            var version = ToVersionRecord(restored, serialized, track, FlowVersionOperationDto.RolledBack, currentHead, sourceVersion, evaluatedValue55, createdAt);
            await _versions.AddAsync(version, token);
            await WriteLibraryBindingsAsync(projectId, restored, token);
            if (track == FlowVersionTrackDto.Development)
            {
                var toRecordResult48 = ToRecord(projectId, restored, serialized);
                await _definitions.UpdateAsync(toRecordResult48, token);
                var toSummaryResult49 = ToSummary(flowId, version, isCurrent: true);
                var flowVersionMutationResult50 = new FlowVersionMutationResult(restored, toSummaryResult49);
                return flowVersionMutationResult50;
            }

            await UpsertProductionHeadAsync(flowId, restored.Version, createdAt, token);
            var deserializeResult56 = Deserialize(current);
            var toSummaryResult57 = ToSummary(flowId, version, isCurrent: true);
            var flowVersionMutationResult58 = new FlowVersionMutationResult(deserializeResult56, toSummaryResult57);
            return flowVersionMutationResult58;
        }, cancellationToken);
        return executeAsyncResult59;
    }

    public async Task<bool> IsLibraryReferencedByProductionHistoryAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(libraryId))
        {
            return false;
        }

        var projectKey = projectId.ToString("D");
        var flowIds = (await _definitions.ListAsync(row => row.ProjectId == projectKey, cancellationToken))
            .Select(static row => row.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (flowIds.Count == 0)
        {
            return false;
        }

        var versions = await _versions.ListAsync(cancellationToken: cancellationToken);
        foreach (var version in versions.Where(row =>
        {
            var evaluatedValue60 = flowIds.Contains(row.FlowId) && ParseTrack(row.Track) == FlowVersionTrackDto.Production;
            return evaluatedValue60;
        }))
        {
            try
            {
                var deserializeResult61 = Deserialize(version);
                var extractResult = LibraryBindingIndex.Extract(deserializeResult61);
                var trimResult = libraryId.Trim();
                if (extractResult.Contains(trimResult, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
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
    {
        var serializeResult = JsonSerializer.Serialize(definition, SerializerOptions);
        return serializeResult;
    }

    private static FlowDefinitionRecord ToRecord(Guid projectId, FlowDefinitionDto definition, string serialized)
    {
        FlowDefinitionRecord evaluatedValue64 = new()
        {
            Id = definition.Id.ToString("D"),
            ProjectId = projectId.ToString("D"),
            Version = definition.Version,
            DefinitionJson = serialized,
            Checksum = definition.Checksum
        };
        return evaluatedValue64;
    }

    private static FlowDefinitionVersionRecord ToVersionRecord(FlowDefinitionDto definition, string serialized, FlowVersionTrackDto track, FlowVersionOperationDto operation, long? parentVersion, long? sourceVersion, string remark, DateTimeOffset? createdAt)
    {
        FlowDefinitionVersionRecord evaluatedValue65 = new()
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
            CreatedAt = createdAt?.ToString("O")
        };
        return evaluatedValue65;
    }

    private static FlowDefinitionDto Deserialize(FlowDefinitionRecord row)
    {
        var definition = JsonSerializer.Deserialize<FlowDefinitionDto>(row.DefinitionJson, SerializerOptions) ?? throw new InvalidOperationException($"Flow definition '{row.Id}' is empty. 流程定义“{row.Id}”为空。");
        return definition with
        {
            Version = row.Version,
            Checksum = row.Checksum
        };
    }

    private static FlowDefinitionDto Deserialize(FlowDefinitionVersionRecord row)
    {
        var definition = JsonSerializer.Deserialize<FlowDefinitionDto>(row.DefinitionJson, SerializerOptions) ?? throw new InvalidOperationException($"Flow definition version '{row.FlowId}:{row.Version}' is empty. 流程定义版本“{row.FlowId}:{row.Version}”为空。");
        return definition with
        {
            Version = row.Version,
            Checksum = row.Checksum
        };
    }

    private async Task<FlowDefinitionRecord?> FindCurrentRecordAsync(Guid projectId, Guid flowId, CancellationToken cancellationToken)
    {
        var toStringResult66 = flowId.ToString("D");
        var row = await _definitions.GetByIdAsync(toStringResult66, cancellationToken);
        FlowDefinitionRecord? evaluatedValue68 = row is not null && resolveEqualsResult() ? row : null;
        return evaluatedValue68;
        bool resolveEqualsResult()
        {
            var toStringResult67 = projectId.ToString("D");
            var equalsResult = string.Equals(row.ProjectId, toStringResult67, StringComparison.OrdinalIgnoreCase);
            return equalsResult;
        }
    }

    private async Task<FlowDefinitionVersionRecord?> FindVersionRecordAsync(Guid flowId, long version, CancellationToken cancellationToken)
    {
        var flowKey = flowId.ToString("D");
        var singleOrDefaultResult = (await _versions.ListAsync(row => row.FlowId == flowKey, cancellationToken))
            .SingleOrDefault(row => row.Version == version);
        return singleOrDefaultResult;
    }

    private async Task<long> AllocateVersionAsync(Guid flowId, CancellationToken cancellationToken)
    {
        var key = flowId.ToString("D");
        var counter = await _versionCounters.GetByIdAsync(key, cancellationToken);
        if (counter is null)
        {
            var knownVersions = await _versions.ListAsync(row => row.FlowId == key, cancellationToken);
            long evaluatedValue70 = knownVersions.Count == 0 ? 1 : checked(knownVersions.Max(static row => row.Version) + 1);
            var nextVersion = Math.Max(1, evaluatedValue70);
            counter = new FlowVersionCounterRecord
            {
                FlowId = key,
                NextVersion = checked(nextVersion + 1)
            };
            await _versionCounters.AddAsync(counter, cancellationToken);
            return nextVersion;
        }

        var allocated = Math.Max(1, counter.NextVersion);
        counter.NextVersion = checked(allocated + 1);
        await _versionCounters.UpdateAsync(counter, cancellationToken);
        return allocated;
    }

    private async Task UpsertProductionHeadAsync(Guid flowId, long version, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        var key = flowId.ToString("D");
        var head = await _productionHeads.GetByIdAsync(key, cancellationToken);
        if (head is null)
        {
            var flowProductionHeadRecord = new FlowProductionHeadRecord
            {
                FlowId = key,
                Version = version,
                UpdatedAt = updatedAt.ToString("O"),
            };
            await _productionHeads.AddAsync(flowProductionHeadRecord, cancellationToken);
            return;
        }

        head.Version = version;
        head.UpdatedAt = updatedAt.ToString("O");
        await _productionHeads.UpdateAsync(head, cancellationToken);
    }

    private static FlowVersionSummaryDto ToSummary(Guid flowId, FlowDefinitionVersionRecord record, bool isCurrent)
    {
        var parseTrackResult = ParseTrack(record.Track);
        var parseOperationResult = ParseOperation(record.Operation);
        DateTimeOffset? evaluatedValue71 = DateTimeOffset.TryParse(record.CreatedAt, out var createdAt) ? createdAt : null;
        FlowVersionSummaryDto evaluatedValue72 = new(flowId, record.Version, parseTrackResult, parseOperationResult, record.ParentVersion, record.SourceVersion, record.Remark, evaluatedValue71, isCurrent);
        return evaluatedValue72;
    }

    private static FlowVersionTrackDto ParseTrack(string? value)
    {
        FlowVersionTrackDto evaluatedValue73 = Enum.TryParse<FlowVersionTrackDto>(value, ignoreCase: true, out var track) ? track : FlowVersionTrackDto.Development;
        return evaluatedValue73;
    }

    private static FlowVersionOperationDto ParseOperation(string? value)
    {
        FlowVersionOperationDto evaluatedValue74 = Enum.TryParse<FlowVersionOperationDto>(value, ignoreCase: true, out var operation) ? operation : FlowVersionOperationDto.Imported;
        return evaluatedValue74;
    }

    private static string NormalizeRemark(string? remark, string fallback)
    {
        string evaluatedValue75 = string.IsNullOrWhiteSpace(remark) ? fallback : remark.Trim();
        return evaluatedValue75;
    }

    private static string TrackName(FlowVersionTrackDto track) => track == FlowVersionTrackDto.Development ? "开发" : "生产";
    private async Task WriteLibraryBindingsAsync(Guid projectId, FlowDefinitionDto definition, CancellationToken cancellationToken)
    {
        if (_libraryBindings is null || _libraries is null)
        {
            return;
        }

        foreach (var artifactId in LibraryBindingIndex.Extract(definition))
        {
            // The index is derived metadata. Historical definitions may carry
            // an invalid or manually authored library ID, so never manufacture
            // a catalog row just to satisfy this foreign key.
            // 该索引属于派生元数据。历史流程可能保留无效或手工填写的类库 ID，
            // 因此不能为了满足外键而伪造类库目录记录。
            if (await _libraries.GetByIdAsync(artifactId, cancellationToken) is null)
            {
                continue;
            }

            var flowLibraryBindingRecord = new FlowLibraryBindingRecord
            {
                Id = resolveToStringResult(),
                ProjectId = projectId.ToString("D"),
                FlowId = definition.Id.ToString("D"),
                FlowVersion = definition.Version,
                LibraryArtifactId = artifactId,
                CreatedAt = DateTimeOffset.UtcNow.ToString("O")
            };
            await _libraryBindings.AddAsync(flowLibraryBindingRecord, cancellationToken);
            string resolveToStringResult()
            {
                var newGuidResult = Guid.NewGuid();
                var toStringResult76 = newGuidResult.ToString("N");
                return toStringResult76;
            }
        }
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Infrastructure.Persistence;
/// <summary>
/// SqlSugar implementation of the application-level library-upgrade commit
/// boundary. All records touched by a successful apply share one transaction.
/// 类库升级提交边界的 SqlSugar 实现。成功应用所影响的全部记录共享同一个事务。
/// </summary>
public sealed class SqlSugarFlowLibraryUpgradeStore : IFlowLibraryUpgradeStore
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateWebOptions(options =>
    {
        var jsonStringEnumConverter = new JsonStringEnumConverter(JsonNamingPolicy.CamelCase);
        options.Converters.Add(jsonStringEnumConverter);
    });
    private readonly IRepository<LibraryUpgradePlanRecord> _plans;
    private readonly IRepository<FlowDefinitionRecord> _definitions;
    private readonly IRepository<FlowDefinitionVersionRecord> _versions;
    private readonly IRepository<FlowVersionCounterRecord> _versionCounters;
    private readonly IRepository<ProjectLibraryReferenceRecord> _references;
    private readonly IRepository<LibraryRecord> _libraries;
    private readonly IRepository<FlowLibraryBindingRecord> _bindings;
    private readonly IUnitOfWork _unitOfWork;
    public SqlSugarFlowLibraryUpgradeStore(IRepository<LibraryUpgradePlanRecord> plans, IRepository<FlowDefinitionRecord> definitions, IRepository<FlowDefinitionVersionRecord> versions, IRepository<FlowVersionCounterRecord> versionCounters, IRepository<ProjectLibraryReferenceRecord> references, IRepository<LibraryRecord> libraries, IRepository<FlowLibraryBindingRecord> bindings, IUnitOfWork unitOfWork)
    {
        _plans = plans;
        _definitions = definitions;
        _versions = versions;
        _versionCounters = versionCounters;
        _references = references;
        _libraries = libraries;
        _bindings = bindings;
        _unitOfWork = unitOfWork;
    }

    public SqlSugarFlowLibraryUpgradeStore(SqliteDatabase database)
    {
        var plans = new SqlSugarRepository<LibraryUpgradePlanRecord>(database.Client);
        var definitions = new SqlSugarRepository<FlowDefinitionRecord>(database.Client);
        var versions = new SqlSugarRepository<FlowDefinitionVersionRecord>(database.Client);
        var versionCounters = new SqlSugarRepository<FlowVersionCounterRecord>(database.Client);
        var references = new SqlSugarRepository<ProjectLibraryReferenceRecord>(database.Client);
        var libraries = new SqlSugarRepository<LibraryRecord>(database.Client);
        var bindings = new SqlSugarRepository<FlowLibraryBindingRecord>(database.Client);
        var unitOfWork = new SqlSugarUnitOfWork(database.Client);
        _plans = plans;
        _definitions = definitions;
        _versions = versions;
        _versionCounters = versionCounters;
        _references = references;
        _libraries = libraries;
        _bindings = bindings;
        _unitOfWork = unitOfWork;
    }

    public async Task<LibraryUpgradePlanDto> SavePlanAsync(LibraryUpgradePlanDto plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var record = ToRecord(plan);
        await _plans.AddAsync(record, cancellationToken);
        return plan;
    }

    public async Task<LibraryUpgradePlanDto?> FindPlanAsync(Guid projectId, Guid planId, CancellationToken cancellationToken = default)
    {
        var id = planId.ToString("D");
        var record = await _plans.GetByIdAsync(id, cancellationToken);
        if (record is null || !RecordBelongsToProject())
        {
            return null;
        }
        return Map(record);
        bool RecordBelongsToProject()
        {
            var projectIdText = projectId.ToString("D");
            return string.Equals(record.ProjectId, projectIdText, StringComparison.OrdinalIgnoreCase);
        }
    }

    public Task<FlowLibraryUpgradeCommitResult> CommitAsync(Guid projectId, Guid planId, FlowDefinitionDto upgradedDefinition, long expectedVersion, string targetArtifactId, LibraryUpgradePlanFlowResultDto? appliedFlow = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upgradedDefinition);
        if (expectedVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedVersion), "Expected flow version must be positive. 期望流程版本必须为正数。");
        }

        if (string.IsNullOrWhiteSpace(targetArtifactId))
        {
            throw new ArgumentException("Target artifact ID cannot be empty. 目标工件 ID 不能为空。", nameof(targetArtifactId));
        }

        var task = _unitOfWork.ExecuteAsync(async token =>
        {
            var id = upgradedDefinition.Id.ToString("D");
            var current = await _definitions.GetByIdAsync(id, token);
            if (current is null || !CurrentBelongsToProject() || current.Version != expectedVersion)
            {
                return new FlowLibraryUpgradeCommitResult(null, current?.Version);
            }

            var planIdText = planId.ToString("D");
            var plan = await _plans.GetByIdAsync(planIdText, token);
            if (plan is null || !PlanBelongsToProject() || (!IsAnalyzed() && !IsApplied()))
            {
                return new FlowLibraryUpgradeCommitResult(null, current.Version);
            }

            var planDto = Map(plan);
            if (!planDto.Flows.Any(preview => preview.FlowId == upgradedDefinition.Id) || (planDto.AppliedFlows ?? []).Any(result => result.FlowId == upgradedDefinition.Id))
            {
                return new FlowLibraryUpgradeCommitResult(null, current.Version);
            }

            var saved = upgradedDefinition with
            {
                Version = await AllocateVersionAsync(upgradedDefinition.Id, token)
            };
            var savedJson = JsonSerializer.Serialize(saved, JsonOptions);
            var flowDefinitionRecord = new FlowDefinitionRecord
            {
                Id = saved.Id.ToString("D"),
                ProjectId = projectId.ToString("D"),
                Version = saved.Version,
                DefinitionJson = savedJson,
                Checksum = saved.Checksum,
            };
            await _definitions.UpdateAsync(flowDefinitionRecord, token);
            var flowDefinitionVersionRecord = new FlowDefinitionVersionRecord
            {
                FlowId = saved.Id.ToString("D"),
                Version = saved.Version,
                DefinitionJson = savedJson,
                Checksum = saved.Checksum,
                Track = FlowVersionTrackDto.Development.ToString(),
                Operation = FlowVersionOperationDto.LibraryUpgraded.ToString(),
                ParentVersion = expectedVersion,
                SourceVersion = null,
                Remark = "类库升级",
                CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
            };
            await _versions.AddAsync(flowDefinitionVersionRecord, token);
            var referenceId = CreateReferenceId(projectId, targetArtifactId);
            if (await _references.GetByIdAsync(referenceId, token) is null)
            {
                var projectLibraryReferenceRecord = new ProjectLibraryReferenceRecord
                {
                    Id = referenceId,
                    ProjectId = projectId.ToString("D"),
                    LibraryId = targetArtifactId.Trim(),
                    ReferencedAt = DateTimeOffset.UtcNow.ToString("O"),
                };
                await _references.AddAsync(projectLibraryReferenceRecord, token);
            }

            await RemoveSourceReferenceWhenUnusedAsync(projectId, planDto.SourceArtifactId, token);
            foreach (var artifactId in LibraryBindingIndex.Extract(saved))
            {
                // A binding is a secondary index, while the serialized flow
                // definition is the source of truth. Preserve historical
                // definitions that mention an unavailable legacy artifact.
                // 绑定是二级索引，流程定义序列化内容才是真实来源。历史流程提及
                // 不可用的旧工件时，仍应保留其定义而不是让绑定索引导致事务失败。
                if (await _libraries.GetByIdAsync(artifactId, token) is null)
                {
                    continue;
                }

                var flowLibraryBindingRecord = new FlowLibraryBindingRecord
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ProjectId = projectId.ToString("D"),
                    FlowId = saved.Id.ToString("D"),
                    FlowVersion = saved.Version,
                    LibraryArtifactId = artifactId,
                    CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
                };
                await _bindings.AddAsync(flowLibraryBindingRecord, token);
            }

            var result = (appliedFlow ?? new LibraryUpgradePlanFlowResultDto(saved.Id, expectedVersion, saved.Version, DateTimeOffset.UtcNow)) with
            {
                PreviousVersion = expectedVersion,
                NewVersion = saved.Version,
            };
            var appliedFlows = (planDto.AppliedFlows ?? [])
                .Append(result)
                .OrderBy(item => item.FlowId)
                .ToArray();
            var allFlowsApplied = planDto.Flows.Count == 0 || planDto.Flows.All(preview =>
            {
                return appliedFlows.Any(result => result.FlowId == preview.FlowId);
            });
            var updatedPlan = planDto with
            {
                Status = allFlowsApplied ? LibraryUpgradePlanStatusDto.Applied : LibraryUpgradePlanStatusDto.Analyzed,
                AppliedAt = allFlowsApplied ? result.AppliedAt : null,
                FailureMessage = null,
                AppliedFlows = appliedFlows,
            };
            plan.Status = updatedPlan.Status.ToString();
            plan.AnalysisJson = JsonSerializer.Serialize(updatedPlan, JsonOptions);
            plan.AppliedAt = updatedPlan.AppliedAt?.ToString("O");
            plan.FailureMessage = null;
            await _plans.UpdateAsync(plan, token);
            return new FlowLibraryUpgradeCommitResult(saved);
            bool CurrentBelongsToProject()
            {
                var projectIdText = projectId.ToString("D");
                return string.Equals(current.ProjectId, projectIdText, StringComparison.OrdinalIgnoreCase);
            }

            bool PlanBelongsToProject()
            {
                var mappedProjectIdText = projectId.ToString("D");
                return string.Equals(plan.ProjectId, mappedProjectIdText, StringComparison.OrdinalIgnoreCase);
            }

            bool IsAnalyzed()
            {
                var analyzedText = LibraryUpgradePlanStatusDto.Analyzed.ToString();
                return string.Equals(plan.Status, analyzedText, StringComparison.OrdinalIgnoreCase);
            }

            bool IsApplied()
            {
                var appliedText = LibraryUpgradePlanStatusDto.Applied.ToString();
                return string.Equals(plan.Status, appliedText, StringComparison.OrdinalIgnoreCase);
            }
        }, cancellationToken);
        return task;
    }

    private async Task RemoveSourceReferenceWhenUnusedAsync(Guid projectId, string sourceArtifactId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceArtifactId))
        {
            return;
        }

        var referenceId = CreateReferenceId(projectId, sourceArtifactId);
        if (await _references.GetByIdAsync(referenceId, cancellationToken) is null)
        {
            return;
        }

        var projectIdValue = projectId.ToString("D");
        var currentDefinitions = await _definitions.ListAsync(definition => definition.ProjectId == projectIdValue, cancellationToken);
        foreach (var currentDefinition in currentDefinitions)
        {
            FlowDefinitionDto? definition;
            try
            {
                definition = JsonSerializer.Deserialize<FlowDefinitionDto>(currentDefinition.DefinitionJson, JsonOptions);
            }
            catch (JsonException)
            {
                // An unreadable current definition cannot safely be treated as unused.
                // 无法读取当前定义时，不能将其视为未使用该类库。
                return;
            }

            if (definition is null || ReferencesSourceArtifact())
            {
                return;
            }

            bool ReferencesSourceArtifact()
            {
                var extract = LibraryBindingIndex.Extract(definition);
                var normalizedSourceArtifactId = sourceArtifactId.Trim();
                return extract.Contains(normalizedSourceArtifactId, StringComparer.OrdinalIgnoreCase);
            }
        }

        var projectFlowIds = currentDefinitions
            .Select(static definition => definition.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var productionVersions = await _versions.ListAsync(cancellationToken: cancellationToken);
        foreach (var version in productionVersions.Where(version =>
        {
            return projectFlowIds.Contains(version.FlowId) && IsProductionVersion();
            bool IsProductionVersion()
            {
                var productionText = FlowVersionTrackDto.Production.ToString();
                return string.Equals(version.Track, productionText, StringComparison.OrdinalIgnoreCase);
            }
        }))
        {
            try
            {
                var definition = JsonSerializer.Deserialize<FlowDefinitionDto>(version.DefinitionJson, JsonOptions);
                if (definition is null || ProductionReferencesSourceArtifact())
                {
                    return;
                }

                bool ProductionReferencesSourceArtifact()
                {
                    var mappedExtract = LibraryBindingIndex.Extract(definition);
                    var normalizedSourceArtifactId = sourceArtifactId.Trim();
                    return mappedExtract.Contains(normalizedSourceArtifactId, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (JsonException)
            {
                // A corrupt production snapshot must retain the old reference.
                // 损坏的生产快照必须保留旧引用，不能据此判断来源类库已无用。
                return;
            }
        }

        await _references.DeleteAsync(referenceId, cancellationToken);
    }

    private async Task<long> AllocateVersionAsync(Guid flowId, CancellationToken cancellationToken)
    {
        var key = flowId.ToString("D");
        var counter = await _versionCounters.GetByIdAsync(key, cancellationToken);
        if (counter is null)
        {
            var versions = await _versions.ListAsync(version => version.FlowId == key, cancellationToken);
            var allocated = versions.Count == 0 ? 1 : checked(versions.Max(static version => version.Version) + 1);
            var flowVersionCounterRecord = new FlowVersionCounterRecord
            {
                FlowId = key,
                NextVersion = checked(allocated + 1),
            };
            await _versionCounters.AddAsync(flowVersionCounterRecord, cancellationToken);
            return allocated;
        }

        var next = Math.Max(1, counter.NextVersion);
        counter.NextVersion = checked(next + 1);
        await _versionCounters.UpdateAsync(counter, cancellationToken);
        return next;
    }

    private static LibraryUpgradePlanRecord ToRecord(LibraryUpgradePlanDto plan)
    {
        return new()
        {
            Id = plan.Id.ToString("D"),
            ProjectId = plan.ProjectId.ToString("D"),
            SourceArtifactId = plan.SourceArtifactId,
            TargetArtifactId = plan.TargetArtifactId,
            Status = plan.Status.ToString(),
            AnalysisJson = JsonSerializer.Serialize(plan, JsonOptions),
            CreatedAt = plan.CreatedAt.ToString("O"),
            AppliedAt = plan.AppliedAt?.ToString("O"),
            FailureMessage = plan.FailureMessage
        };
    }

    private static LibraryUpgradePlanDto Map(LibraryUpgradePlanRecord record)
    {
        var plan = JsonSerializer.Deserialize<LibraryUpgradePlanDto>(record.AnalysisJson, JsonOptions) ?? throw new InvalidOperationException("The library upgrade plan is empty. 类库升级计划为空。");
        var status = Enum.TryParse<LibraryUpgradePlanStatusDto>(record.Status, ignoreCase: true, out var parsedStatus) ? parsedStatus : LibraryUpgradePlanStatusDto.Failed;
        var createdAt = DateTimeOffset.TryParse(record.CreatedAt, out var parsedCreated) ? parsedCreated : plan.CreatedAt;
        var appliedAt = DateTimeOffset.TryParse(record.AppliedAt, out var parsedApplied) ? parsedApplied : plan.AppliedAt;
        return plan with
        {
            Status = status,
            CreatedAt = createdAt,
            AppliedAt = appliedAt,
            FailureMessage = record.FailureMessage,
            AppliedFlows = plan.AppliedFlows ?? [],
        };
    }

    private static string CreateReferenceId(Guid projectId, string artifactId)
    {
        return $"{projectId:N}:{artifactId.Trim().ToLowerInvariant()}";
    }
}

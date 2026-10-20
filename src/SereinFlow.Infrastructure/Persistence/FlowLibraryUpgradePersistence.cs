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
        var toStringResult = planId.ToString("D");
        var record = await _plans.GetByIdAsync(toStringResult, cancellationToken);
        if (record is null || !resolveEqualsResult())
        {
            return null;
        }

        var mapResult = Map(record);
        return mapResult;
        bool resolveEqualsResult()
        {
            var toStringResult1 = projectId.ToString("D");
            var equalsResult = string.Equals(record.ProjectId, toStringResult1, StringComparison.OrdinalIgnoreCase);
            return equalsResult;
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

        var executeAsyncResult = _unitOfWork.ExecuteAsync(async token =>
        {
            var toStringResult5 = upgradedDefinition.Id.ToString("D");
            var current = await _definitions.GetByIdAsync(toStringResult5, token);
            if (current is null || !resolveEqualsResult6() || current.Version != expectedVersion)
            {
                var flowLibraryUpgradeCommitResult = new FlowLibraryUpgradeCommitResult(null, current?.Version);
                return flowLibraryUpgradeCommitResult;
            }

            var toStringResult9 = planId.ToString("D");
            var plan = await _plans.GetByIdAsync(toStringResult9, token);
            if (plan is null || !resolveEqualsResult10() || (!resolveEqualsResult13() && !resolveEqualsResult16()))
            {
                var flowLibraryUpgradeCommitResult2 = new FlowLibraryUpgradeCommitResult(null, current.Version);
                return flowLibraryUpgradeCommitResult2;
            }

            var planDto = Map(plan);
            if (!planDto.Flows.Any(preview => preview.FlowId == upgradedDefinition.Id) || (planDto.AppliedFlows ?? []).Any(result => result.FlowId == upgradedDefinition.Id))
            {
                var flowLibraryUpgradeCommitResult3 = new FlowLibraryUpgradeCommitResult(null, current.Version);
                return flowLibraryUpgradeCommitResult3;
            }

            var saved = upgradedDefinition with
            {
                Version = await AllocateVersionAsync(upgradedDefinition.Id, token)
            };
            var definitionJson = JsonSerializer.Serialize(saved, JsonOptions);
            var flowDefinitionRecord = new FlowDefinitionRecord
            {
                Id = saved.Id.ToString("D"),
                ProjectId = projectId.ToString("D"),
                Version = saved.Version,
                DefinitionJson = definitionJson,
                Checksum = saved.Checksum,
            };
            await _definitions.UpdateAsync(flowDefinitionRecord, token);
            var flowDefinitionVersionRecord = new FlowDefinitionVersionRecord
            {
                FlowId = saved.Id.ToString("D"),
                Version = saved.Version,
                DefinitionJson = definitionJson,
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
            if (await _references.GetByIdAsync(referenceId, token)is null)
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
                if (await _libraries.GetByIdAsync(artifactId, token)is null)
                {
                    continue;
                }

                var flowLibraryBindingRecord = new FlowLibraryBindingRecord
                {
                    Id = resolveToStringResult(),
                    ProjectId = projectId.ToString("D"),
                    FlowId = saved.Id.ToString("D"),
                    FlowVersion = saved.Version,
                    LibraryArtifactId = artifactId,
                    CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
                };
                await _bindings.AddAsync(flowLibraryBindingRecord, token);
                string resolveToStringResult()
                {
                    var newGuidResult = Guid.NewGuid();
                    var toStringResult4 = newGuidResult.ToString("N");
                    return toStringResult4;
                }
            }

            var result = (appliedFlow ?? new LibraryUpgradePlanFlowResultDto(saved.Id, expectedVersion, saved.Version, DateTimeOffset.UtcNow))with
            {
                PreviousVersion = expectedVersion,
                NewVersion = saved.Version,
            };
            var appendResult = (planDto.AppliedFlows ?? []).Append(result);
            var orderByResult = appendResult.OrderBy(item => item.FlowId);
            var appliedFlows = orderByResult.ToArray();
            var allFlowsApplied = planDto.Flows.Count == 0 || planDto.Flows.All(preview =>
            {
                var anyResult = appliedFlows.Any(result => result.FlowId == preview.FlowId);
                return anyResult;
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
            var flowLibraryUpgradeCommitResult19 = new FlowLibraryUpgradeCommitResult(saved);
            return flowLibraryUpgradeCommitResult19;
            bool resolveEqualsResult6()
            {
                var toStringResult7 = projectId.ToString("D");
                var equalsResult8 = string.Equals(current.ProjectId, toStringResult7, StringComparison.OrdinalIgnoreCase);
                return equalsResult8;
            }

            bool resolveEqualsResult10()
            {
                var toStringResult11 = projectId.ToString("D");
                var equalsResult12 = string.Equals(plan.ProjectId, toStringResult11, StringComparison.OrdinalIgnoreCase);
                return equalsResult12;
            }

            bool resolveEqualsResult13()
            {
                var toStringResult14 = LibraryUpgradePlanStatusDto.Analyzed.ToString();
                var equalsResult15 = string.Equals(plan.Status, toStringResult14, StringComparison.OrdinalIgnoreCase);
                return equalsResult15;
            }

            bool resolveEqualsResult16()
            {
                var toStringResult17 = LibraryUpgradePlanStatusDto.Applied.ToString();
                var equalsResult18 = string.Equals(plan.Status, toStringResult17, StringComparison.OrdinalIgnoreCase);
                return equalsResult18;
            }
        }, cancellationToken);
        return executeAsyncResult;
    }

    private async Task RemoveSourceReferenceWhenUnusedAsync(Guid projectId, string sourceArtifactId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceArtifactId))
        {
            return;
        }

        var referenceId = CreateReferenceId(projectId, sourceArtifactId);
        if (await _references.GetByIdAsync(referenceId, cancellationToken)is null)
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
                return;
            }

            if (definition is null || resolveContainsResult())
            {
                return;
            }

            bool resolveContainsResult()
            {
                var extractResult = LibraryBindingIndex.Extract(definition);
                var trimResult = sourceArtifactId.Trim();
                var containsResult = extractResult.Contains(trimResult, StringComparer.OrdinalIgnoreCase);
                return containsResult;
            }
        }

        var selectResult = currentDefinitions.Select(static definition => definition.Id);
        var projectFlowIds = selectResult.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var productionVersions = await _versions.ListAsync(cancellationToken: cancellationToken);
        foreach (var version in productionVersions.Where(version =>
        {
            var evaluatedValue = projectFlowIds.Contains(version.FlowId) && resolveEqualsResult20();
            return evaluatedValue;
            bool resolveEqualsResult20()
            {
                var toStringResult21 = FlowVersionTrackDto.Production.ToString();
                var equalsResult22 = string.Equals(version.Track, toStringResult21, StringComparison.OrdinalIgnoreCase);
                return equalsResult22;
            }
        }))
        {
            try
            {
                var definition = JsonSerializer.Deserialize<FlowDefinitionDto>(version.DefinitionJson, JsonOptions);
                if (definition is null || resolveContainsResult23())
                {
                    return;
                }

                bool resolveContainsResult23()
                {
                    var extractResult24 = LibraryBindingIndex.Extract(definition);
                    var trimResult25 = sourceArtifactId.Trim();
                    var containsResult26 = extractResult24.Contains(trimResult25, StringComparer.OrdinalIgnoreCase);
                    return containsResult26;
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
        LibraryUpgradePlanRecord evaluatedValue27 = new()
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
        return evaluatedValue27;
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
        var evaluatedValue29 = $"{projectId:N}:{resolveToLowerInvariantResult()}";
        return evaluatedValue29;
        string resolveToLowerInvariantResult()
        {
            var trimResult28 = artifactId.Trim();
            var toLowerInvariantResult = trimResult28.ToLowerInvariant();
            return toLowerInvariantResult;
        }
    }
}

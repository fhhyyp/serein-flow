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
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

    private readonly IRepository<LibraryUpgradePlanRecord> _plans;
    private readonly IRepository<FlowDefinitionRecord> _definitions;
    private readonly IRepository<FlowDefinitionVersionRecord> _versions;
    private readonly IRepository<ProjectLibraryReferenceRecord> _references;
    private readonly IRepository<LibraryRecord> _libraries;
    private readonly IRepository<FlowLibraryBindingRecord> _bindings;
    private readonly IUnitOfWork _unitOfWork;

    public SqlSugarFlowLibraryUpgradeStore(
        IRepository<LibraryUpgradePlanRecord> plans,
        IRepository<FlowDefinitionRecord> definitions,
        IRepository<FlowDefinitionVersionRecord> versions,
        IRepository<ProjectLibraryReferenceRecord> references,
        IRepository<LibraryRecord> libraries,
        IRepository<FlowLibraryBindingRecord> bindings,
        IUnitOfWork unitOfWork)
    {
        _plans = plans;
        _definitions = definitions;
        _versions = versions;
        _references = references;
        _libraries = libraries;
        _bindings = bindings;
        _unitOfWork = unitOfWork;
    }

    public SqlSugarFlowLibraryUpgradeStore(SqliteDatabase database)
        : this(
            new SqlSugarRepository<LibraryUpgradePlanRecord>(database.Client),
            new SqlSugarRepository<FlowDefinitionRecord>(database.Client),
            new SqlSugarRepository<FlowDefinitionVersionRecord>(database.Client),
            new SqlSugarRepository<ProjectLibraryReferenceRecord>(database.Client),
            new SqlSugarRepository<LibraryRecord>(database.Client),
            new SqlSugarRepository<FlowLibraryBindingRecord>(database.Client),
            new SqlSugarUnitOfWork(database.Client))
    {
    }

    public async Task<LibraryUpgradePlanDto> SavePlanAsync(
        LibraryUpgradePlanDto plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var record = ToRecord(plan);
        await _plans.AddAsync(record, cancellationToken);
        return plan;
    }

    public async Task<LibraryUpgradePlanDto?> FindPlanAsync(
        Guid projectId,
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        var record = await _plans.GetByIdAsync(planId.ToString("D"), cancellationToken);
        if (record is null || !string.Equals(record.ProjectId, projectId.ToString("D"), StringComparison.OrdinalIgnoreCase))
            return null;
        return Map(record);
    }

    public Task<FlowLibraryUpgradeCommitResult> CommitAsync(
        Guid projectId,
        Guid planId,
        FlowDefinitionDto upgradedDefinition,
        long expectedVersion,
        string targetArtifactId,
        LibraryUpgradePlanFlowResultDto? appliedFlow = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upgradedDefinition);
        if (expectedVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(expectedVersion), "Expected flow version must be positive. 期望流程版本必须为正数。");
        if (string.IsNullOrWhiteSpace(targetArtifactId))
            throw new ArgumentException("Target artifact ID cannot be empty. 目标工件 ID 不能为空。", nameof(targetArtifactId));

        return _unitOfWork.ExecuteAsync(async token =>
        {
            var current = await _definitions.GetByIdAsync(upgradedDefinition.Id.ToString("D"), token);
            if (current is null
                || !string.Equals(current.ProjectId, projectId.ToString("D"), StringComparison.OrdinalIgnoreCase)
                || current.Version != expectedVersion)
            {
                return new FlowLibraryUpgradeCommitResult(null, current?.Version);
            }

            var plan = await _plans.GetByIdAsync(planId.ToString("D"), token);
            if (plan is null
                || !string.Equals(plan.ProjectId, projectId.ToString("D"), StringComparison.OrdinalIgnoreCase)
                || (!string.Equals(plan.Status, LibraryUpgradePlanStatusDto.Analyzed.ToString(), StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(plan.Status, LibraryUpgradePlanStatusDto.Applied.ToString(), StringComparison.OrdinalIgnoreCase)))
            {
                return new FlowLibraryUpgradeCommitResult(null, current.Version);
            }

            var planDto = Map(plan);
            if (!planDto.Flows.Any(preview => preview.FlowId == upgradedDefinition.Id)
                || (planDto.AppliedFlows ?? []).Any(result => result.FlowId == upgradedDefinition.Id))
            {
                return new FlowLibraryUpgradeCommitResult(null, current.Version);
            }

            var saved = upgradedDefinition with { Version = expectedVersion + 1 };
            var definitionJson = JsonSerializer.Serialize(saved, JsonOptions);
            await _definitions.UpdateAsync(new FlowDefinitionRecord
            {
                Id = saved.Id.ToString("D"),
                ProjectId = projectId.ToString("D"),
                Version = saved.Version,
                DefinitionJson = definitionJson,
                Checksum = saved.Checksum,
            }, token);
            await _versions.AddAsync(new FlowDefinitionVersionRecord
            {
                FlowId = saved.Id.ToString("D"),
                Version = saved.Version,
                DefinitionJson = definitionJson,
                Checksum = saved.Checksum,
            }, token);

            var referenceId = CreateReferenceId(projectId, targetArtifactId);
            if (await _references.GetByIdAsync(referenceId, token) is null)
            {
                await _references.AddAsync(new ProjectLibraryReferenceRecord
                {
                    Id = referenceId,
                    ProjectId = projectId.ToString("D"),
                    LibraryId = targetArtifactId.Trim(),
                    ReferencedAt = DateTimeOffset.UtcNow.ToString("O"),
                }, token);
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
                    continue;

                await _bindings.AddAsync(new FlowLibraryBindingRecord
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ProjectId = projectId.ToString("D"),
                    FlowId = saved.Id.ToString("D"),
                    FlowVersion = saved.Version,
                    LibraryArtifactId = artifactId,
                    CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
                }, token);
            }

            var result = appliedFlow ?? new LibraryUpgradePlanFlowResultDto(
                saved.Id,
                expectedVersion,
                saved.Version,
                DateTimeOffset.UtcNow);
            var appliedFlows = (planDto.AppliedFlows ?? [])
                .Append(result)
                .OrderBy(item => item.FlowId)
                .ToArray();
            var allFlowsApplied = planDto.Flows.Count == 0
                || planDto.Flows.All(preview => appliedFlows.Any(result => result.FlowId == preview.FlowId));
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
        }, cancellationToken);
    }

    private async Task RemoveSourceReferenceWhenUnusedAsync(
        Guid projectId,
        string sourceArtifactId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceArtifactId))
            return;

        var referenceId = CreateReferenceId(projectId, sourceArtifactId);
        if (await _references.GetByIdAsync(referenceId, cancellationToken) is null)
            return;

        var projectIdValue = projectId.ToString("D");
        var currentDefinitions = await _definitions.ListAsync(
            definition => definition.ProjectId == projectIdValue,
            cancellationToken);
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

            if (definition is null
                || LibraryBindingIndex.Extract(definition).Contains(sourceArtifactId.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                return;
            }
        }

        await _references.DeleteAsync(referenceId, cancellationToken);
    }

    private static LibraryUpgradePlanRecord ToRecord(LibraryUpgradePlanDto plan)
        => new()
        {
            Id = plan.Id.ToString("D"),
            ProjectId = plan.ProjectId.ToString("D"),
            SourceArtifactId = plan.SourceArtifactId,
            TargetArtifactId = plan.TargetArtifactId,
            Status = plan.Status.ToString(),
            AnalysisJson = JsonSerializer.Serialize(plan, JsonOptions),
            CreatedAt = plan.CreatedAt.ToString("O"),
            AppliedAt = plan.AppliedAt?.ToString("O"),
            FailureMessage = plan.FailureMessage,
        };

    private static LibraryUpgradePlanDto Map(LibraryUpgradePlanRecord record)
    {
        var plan = JsonSerializer.Deserialize<LibraryUpgradePlanDto>(record.AnalysisJson, JsonOptions)
            ?? throw new InvalidOperationException("The library upgrade plan is empty. 类库升级计划为空。");
        var status = Enum.TryParse<LibraryUpgradePlanStatusDto>(record.Status, ignoreCase: true, out var parsedStatus)
            ? parsedStatus
            : LibraryUpgradePlanStatusDto.Failed;
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
        => $"{projectId:N}:{artifactId.Trim().ToLowerInvariant()}";
}

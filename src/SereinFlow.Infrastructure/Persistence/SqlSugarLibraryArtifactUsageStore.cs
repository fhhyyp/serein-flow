using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Infrastructure.Persistence;

/// <summary>
/// Repository-backed usage projection for immutable class-library artifacts.
/// It uses the binding indexes as the source of truth and never parses or
/// modifies flow definitions while answering an impact query.
/// 不可变类库工件的仓储化使用投影。它以绑定索引为事实来源，查询影响范围时
/// 不解析或修改流程定义。
/// </summary>
public sealed class SqlSugarLibraryArtifactUsageStore : ILibraryArtifactUsageStore
{
    private readonly IRepository<FlowDefinitionRecord> _definitions;
    private readonly IRepository<FlowLibraryBindingRecord> _flowBindings;
    private readonly IRepository<FlowRunRecord> _runs;
    private readonly IRepository<RunLibraryBindingRecord> _runBindings;

    public SqlSugarLibraryArtifactUsageStore(
        IRepository<FlowDefinitionRecord> definitions,
        IRepository<FlowLibraryBindingRecord> flowBindings,
        IRepository<FlowRunRecord> runs,
        IRepository<RunLibraryBindingRecord> runBindings)
    {
        _definitions = definitions;
        _flowBindings = flowBindings;
        _runs = runs;
        _runBindings = runBindings;
    }

    /// <summary>
    /// Compatibility constructor for isolated Infrastructure tests. Production
    /// composition must use the repository-backed constructor registered by
    /// <see cref="SereinFlowInfrastructureRegistration"/>.
    /// 用于独立 Infrastructure 测试的兼容构造函数。生产组合必须使用
    /// <see cref="SereinFlowInfrastructureRegistration"/> 注册的仓储构造函数。
    /// </summary>
    public SqlSugarLibraryArtifactUsageStore(SqliteDatabase database)
        : this(
            new SqlSugarRepository<FlowDefinitionRecord>(database?.Client ?? throw new ArgumentNullException(nameof(database))),
            new SqlSugarRepository<FlowLibraryBindingRecord>(database.Client),
            new SqlSugarRepository<FlowRunRecord>(database.Client),
            new SqlSugarRepository<RunLibraryBindingRecord>(database.Client))
    {
    }

    public async Task<IReadOnlyList<LibraryArtifactUsageDto>> ListAsync(
        CancellationToken cancellationToken = default)
        => await ListCoreAsync(projectId: null, cancellationToken);

    public async Task<IReadOnlyList<LibraryArtifactUsageDto>> ListByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => await ListCoreAsync(projectId.ToString("D"), cancellationToken);

    private async Task<IReadOnlyList<LibraryArtifactUsageDto>> ListCoreAsync(
        string? projectId,
        CancellationToken cancellationToken)
    {
        var definitionsTask = _definitions.ListAsync(cancellationToken: cancellationToken);
        var flowBindingsTask = _flowBindings.ListAsync(cancellationToken: cancellationToken);
        var runsTask = _runs.ListAsync(cancellationToken: cancellationToken);
        var runBindingsTask = _runBindings.ListAsync(cancellationToken: cancellationToken);
        await Task.WhenAll(definitionsTask, flowBindingsTask, runsTask, runBindingsTask);

        var definitions = definitionsTask.Result
            .Where(definition => projectId is null
                || string.Equals(definition.ProjectId, projectId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var scopedRunIds = runsTask.Result
            .Where(run => projectId is null
                || string.Equals(run.ProjectId, projectId, StringComparison.OrdinalIgnoreCase))
            .Select(static run => run.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var currentVersions = definitions.ToDictionary(
            definition => definition.Id,
            definition => definition.Version,
            StringComparer.OrdinalIgnoreCase);
        var flowBindings = flowBindingsTask.Result
            .Where(binding => !string.IsNullOrWhiteSpace(binding.LibraryArtifactId)
                && (projectId is null || string.Equals(binding.ProjectId, projectId, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var runBindings = runBindingsTask.Result
            .Where(binding => !string.IsNullOrWhiteSpace(binding.LibraryArtifactId)
                && scopedRunIds.Contains(binding.RunId))
            .ToArray();
        var artifactIds = flowBindings
            .Select(static binding => binding.LibraryArtifactId)
            .Concat(runBindings.Select(static binding => binding.LibraryArtifactId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return artifactIds
            .Select(artifactId => new LibraryArtifactUsageDto(
                artifactId,
                flowBindings
                    .Where(binding => string.Equals(binding.LibraryArtifactId, artifactId, StringComparison.OrdinalIgnoreCase)
                        && currentVersions.TryGetValue(binding.FlowId, out var currentVersion)
                        && currentVersion == binding.FlowVersion)
                    .Select(static binding => binding.FlowId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                flowBindings
                    .Where(binding => string.Equals(binding.LibraryArtifactId, artifactId, StringComparison.OrdinalIgnoreCase))
                    .Select(binding => (binding.FlowId, binding.FlowVersion))
                    .Distinct()
                    .Count(),
                runBindings
                    .Where(binding => string.Equals(binding.LibraryArtifactId, artifactId, StringComparison.OrdinalIgnoreCase))
                    .Select(static binding => binding.RunId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count()))
            .ToArray();
    }
}

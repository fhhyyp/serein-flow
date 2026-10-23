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
    public SqlSugarLibraryArtifactUsageStore(IRepository<FlowDefinitionRecord> definitions, IRepository<FlowLibraryBindingRecord> flowBindings, IRepository<FlowRunRecord> runs, IRepository<RunLibraryBindingRecord> runBindings)
    {
        _definitions = definitions;
        _flowBindings = flowBindings;
        _runs = runs;
        _runBindings = runBindings;
    }

    /// <summary>
    /// Compatibility constructor for isolated Infrastructure tests. Production
    /// composition must use the repository-backed constructor registered by
    /// <see cref = "SereinFlowInfrastructureRegistration"/>.
    /// 用于独立 Infrastructure 测试的兼容构造函数。生产组合必须使用
    /// <see cref = "SereinFlowInfrastructureRegistration"/> 注册的仓储构造函数。
    /// </summary>
    public SqlSugarLibraryArtifactUsageStore(SqliteDatabase database)
    {
        var definitionsClient = database?.Client ?? throw new ArgumentNullException(nameof(database));
        _definitions = new SqlSugarRepository<FlowDefinitionRecord>(definitionsClient);
        _flowBindings = new SqlSugarRepository<FlowLibraryBindingRecord>(database.Client);
        _runs = new SqlSugarRepository<FlowRunRecord>(database.Client);
        _runBindings = new SqlSugarRepository<RunLibraryBindingRecord>(database.Client);
    }

    public async Task<IReadOnlyList<LibraryArtifactUsageDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await ListCoreAsync(projectId: null, cancellationToken);
    }

    public async Task<IReadOnlyList<LibraryArtifactUsageDto>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var projectIdText = projectId.ToString("D");
        return await ListCoreAsync(projectIdText, cancellationToken);
    }

    private async Task<IReadOnlyList<LibraryArtifactUsageDto>> ListCoreAsync(string? projectId, CancellationToken cancellationToken)
    {
        var definitionsTask = _definitions.ListAsync(cancellationToken: cancellationToken);
        var flowBindingsTask = _flowBindings.ListAsync(cancellationToken: cancellationToken);
        var runsTask = _runs.ListAsync(cancellationToken: cancellationToken);
        var runBindingsTask = _runBindings.ListAsync(cancellationToken: cancellationToken);
        await Task.WhenAll(definitionsTask, flowBindingsTask, runsTask, runBindingsTask);
        var definitions = definitionsTask.Result
            .Where(definition =>
            {
                return projectId is null || string.Equals(definition.ProjectId, projectId, StringComparison.OrdinalIgnoreCase);
            })
            .ToArray();
        var scopedRunIds = runsTask.Result
            .Where(run =>
            {
                return projectId is null || string.Equals(run.ProjectId, projectId, StringComparison.OrdinalIgnoreCase);
            })
            .Select(static run => run.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentVersions = definitions.ToDictionary(definition => definition.Id, definition => definition.Version, StringComparer.OrdinalIgnoreCase);
        var flowBindings = flowBindingsTask.Result
            .Where(binding =>
            {
                return !string.IsNullOrWhiteSpace(binding.LibraryArtifactId) && (projectId is null || string.Equals(binding.ProjectId, projectId, StringComparison.OrdinalIgnoreCase));
            })
            .ToArray();
        var runBindings = runBindingsTask.Result
            .Where(binding =>
            {
                return !string.IsNullOrWhiteSpace(binding.LibraryArtifactId) && scopedRunIds.Contains(binding.RunId);
            })
            .ToArray();
        var mappedFlowBindings = flowBindings.Select(static binding => binding.LibraryArtifactId);
        var mappedRunBindings = runBindings.Select(static binding => binding.LibraryArtifactId);
        return mappedFlowBindings
            .Concat(mappedRunBindings)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray()
            .Select(artifactId =>
            {
                var currentFlowCount = flowBindings
                    .Where(binding =>
                    {
                        return string.Equals(binding.LibraryArtifactId, artifactId, StringComparison.OrdinalIgnoreCase) && currentVersions.TryGetValue(binding.FlowId, out var currentVersion) && currentVersion == binding.FlowVersion;
                    })
                    .Select(static binding => binding.FlowId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                var flowVersionCount = flowBindings
                    .Where(binding =>
                    {
                        return string.Equals(binding.LibraryArtifactId, artifactId, StringComparison.OrdinalIgnoreCase);
                    })
                    .Select(binding => (binding.FlowId, binding.FlowVersion))
                    .Distinct()
                    .Count();
                var runSnapshotCount = runBindings
                    .Where(binding =>
                    {
                        return string.Equals(binding.LibraryArtifactId, artifactId, StringComparison.OrdinalIgnoreCase);
                    })
                    .Select(static binding => binding.RunId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                return new LibraryArtifactUsageDto(artifactId, currentFlowCount, flowVersionCount, runSnapshotCount);
            })
            .ToArray();
    }
}

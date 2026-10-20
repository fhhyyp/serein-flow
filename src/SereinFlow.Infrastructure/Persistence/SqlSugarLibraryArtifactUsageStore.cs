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
        var listCoreAsyncResult = await ListCoreAsync(projectId: null, cancellationToken);
        return listCoreAsyncResult;
    }

    public async Task<IReadOnlyList<LibraryArtifactUsageDto>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var toStringResult = projectId.ToString("D");
        var listCoreAsyncResult1 = await ListCoreAsync(toStringResult, cancellationToken);
        return listCoreAsyncResult1;
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
                var evaluatedValue = projectId is null || string.Equals(definition.ProjectId, projectId, StringComparison.OrdinalIgnoreCase);
                return evaluatedValue;
            })
            .ToArray();
        var scopedRunIds = runsTask.Result
            .Where(run =>
            {
                var evaluatedValue2 = projectId is null || string.Equals(run.ProjectId, projectId, StringComparison.OrdinalIgnoreCase);
                return evaluatedValue2;
            })
            .Select(static run => run.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentVersions = definitions.ToDictionary(definition => definition.Id, definition => definition.Version, StringComparer.OrdinalIgnoreCase);
        var flowBindings = flowBindingsTask.Result
            .Where(binding =>
            {
                var evaluatedValue3 = !string.IsNullOrWhiteSpace(binding.LibraryArtifactId) && (projectId is null || string.Equals(binding.ProjectId, projectId, StringComparison.OrdinalIgnoreCase));
                return evaluatedValue3;
            })
            .ToArray();
        var runBindings = runBindingsTask.Result
            .Where(binding =>
            {
                var evaluatedValue4 = !string.IsNullOrWhiteSpace(binding.LibraryArtifactId) && scopedRunIds.Contains(binding.RunId);
                return evaluatedValue4;
            })
            .ToArray();
        var selectResult20 = flowBindings.Select(static binding => binding.LibraryArtifactId);
        var selectResult21 = runBindings.Select(static binding => binding.LibraryArtifactId);
        var artifactIds = selectResult20
            .Concat(selectResult21)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var toArrayResult = artifactIds
            .Select(artifactId =>
            {
                var countResult = flowBindings
                    .Where(binding =>
                    {
                        var evaluatedValue5 = string.Equals(binding.LibraryArtifactId, artifactId, StringComparison.OrdinalIgnoreCase) && currentVersions.TryGetValue(binding.FlowId, out var currentVersion) && currentVersion == binding.FlowVersion;
                        return evaluatedValue5;
                    })
                    .Select(static binding => binding.FlowId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                var countResult10 = flowBindings
                    .Where(binding =>
                    {
                        var equalsResult = string.Equals(binding.LibraryArtifactId, artifactId, StringComparison.OrdinalIgnoreCase);
                        return equalsResult;
                    })
                    .Select(binding => (binding.FlowId, binding.FlowVersion))
                    .Distinct()
                    .Count();
                var countResult14 = runBindings
                    .Where(binding =>
                    {
                        var equalsResult6 = string.Equals(binding.LibraryArtifactId, artifactId, StringComparison.OrdinalIgnoreCase);
                        return equalsResult6;
                    })
                    .Select(static binding => binding.RunId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                var libraryArtifactUsageDto = new LibraryArtifactUsageDto(artifactId, countResult, countResult10, countResult14);
                return libraryArtifactUsageDto;
            })
            .ToArray();
        return toArrayResult;
    }
}

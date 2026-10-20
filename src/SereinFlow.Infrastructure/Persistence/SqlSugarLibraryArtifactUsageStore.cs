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
        var whereResult15 = definitionsTask.Result.Where(definition =>
        {
            var evaluatedValue = projectId is null || string.Equals(definition.ProjectId, projectId, StringComparison.OrdinalIgnoreCase);
            return evaluatedValue;
        });
        var definitions = whereResult15.ToArray();
        var whereResult16 = runsTask.Result.Where(run =>
        {
            var evaluatedValue2 = projectId is null || string.Equals(run.ProjectId, projectId, StringComparison.OrdinalIgnoreCase);
            return evaluatedValue2;
        });
        var selectResult17 = whereResult16.Select(static run => run.Id);
        var scopedRunIds = selectResult17.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentVersions = definitions.ToDictionary(definition => definition.Id, definition => definition.Version, StringComparer.OrdinalIgnoreCase);
        var whereResult18 = flowBindingsTask.Result.Where(binding =>
        {
            var evaluatedValue3 = !string.IsNullOrWhiteSpace(binding.LibraryArtifactId) && (projectId is null || string.Equals(binding.ProjectId, projectId, StringComparison.OrdinalIgnoreCase));
            return evaluatedValue3;
        });
        var flowBindings = whereResult18.ToArray();
        var whereResult19 = runBindingsTask.Result.Where(binding =>
        {
            var evaluatedValue4 = !string.IsNullOrWhiteSpace(binding.LibraryArtifactId) && scopedRunIds.Contains(binding.RunId);
            return evaluatedValue4;
        });
        var runBindings = whereResult19.ToArray();
        var selectResult20 = flowBindings.Select(static binding => binding.LibraryArtifactId);
        var selectResult21 = runBindings.Select(static binding => binding.LibraryArtifactId);
        var concatResult = selectResult20.Concat(selectResult21);
        var distinctResult22 = concatResult.Distinct(StringComparer.OrdinalIgnoreCase);
        var orderResult = distinctResult22.Order(StringComparer.Ordinal);
        var artifactIds = orderResult.ToArray();
        var selectResult23 = artifactIds.Select(artifactId =>
        {
            var whereResult = flowBindings.Where(binding =>
            {
                var evaluatedValue5 = string.Equals(binding.LibraryArtifactId, artifactId, StringComparison.OrdinalIgnoreCase) && currentVersions.TryGetValue(binding.FlowId, out var currentVersion) && currentVersion == binding.FlowVersion;
                return evaluatedValue5;
            });
            var selectResult = whereResult.Select(static binding => binding.FlowId);
            var distinctResult = selectResult.Distinct(StringComparer.OrdinalIgnoreCase);
            var countResult = distinctResult.Count();
            var whereResult7 = flowBindings.Where(binding =>
            {
                var equalsResult = string.Equals(binding.LibraryArtifactId, artifactId, StringComparison.OrdinalIgnoreCase);
                return equalsResult;
            });
            var selectResult8 = whereResult7.Select(binding => (binding.FlowId, binding.FlowVersion));
            var distinctResult9 = selectResult8.Distinct();
            var countResult10 = distinctResult9.Count();
            var whereResult11 = runBindings.Where(binding =>
            {
                var equalsResult6 = string.Equals(binding.LibraryArtifactId, artifactId, StringComparison.OrdinalIgnoreCase);
                return equalsResult6;
            });
            var selectResult12 = whereResult11.Select(static binding => binding.RunId);
            var distinctResult13 = selectResult12.Distinct(StringComparer.OrdinalIgnoreCase);
            var countResult14 = distinctResult13.Count();
            var libraryArtifactUsageDto = new LibraryArtifactUsageDto(artifactId, countResult, countResult10, countResult14);
            return libraryArtifactUsageDto;
        });
        var toArrayResult = selectResult23.ToArray();
        return toArrayResult;
    }
}

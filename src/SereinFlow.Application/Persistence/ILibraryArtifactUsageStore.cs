using SereinFlow.Contracts;

namespace SereinFlow.Application.Persistence;

/// <summary>
/// Reads the persisted impact of immutable library artifacts. The contract is
/// intentionally aggregate-only: callers cannot access database-specific
/// query objects or infer a replacement artifact.
/// 读取不可变类库工件的持久化影响范围。该契约只暴露聚合结果：调用方不能访问
/// 特定数据库查询对象，也不能由此推断或替换为其他工件。
/// </summary>
public interface ILibraryArtifactUsageStore
{
    Task<IReadOnlyList<LibraryArtifactUsageDto>> ListAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads artifact usage limited to one project. The counts preserve the
    /// same meaning as <see cref="ListAsync"/>, but never include bindings
    /// owned by another project.
    /// 读取限定在单个项目内的工件使用情况。计数含义与 <see cref="ListAsync"/>
    /// 相同，但绝不包含其它项目拥有的绑定记录。
    /// </summary>
    Task<IReadOnlyList<LibraryArtifactUsageDto>> ListByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}

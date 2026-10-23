namespace SereinFlow.Domain;
/// <summary>
/// Represents a project and its lifecycle state.
/// 表示项目及其生命周期状态。
/// </summary>
public sealed class Project
{
    private Project(Guid id, string name, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    /// <summary>
    /// Identifies the project.
    /// 项目标识。
    /// </summary>
    public Guid Id { get; }
    /// <summary>
    /// Stores the project name.
    /// 项目名称。
    /// </summary>
    public string Name { get; private set; }
    /// <summary>
    /// Tracks the project version.
    /// 项目版本号。
    /// </summary>
    public long Version { get; private set; } = 1;
    /// <summary>
    /// Stores the project lifecycle state.
    /// 项目生命周期状态。
    /// </summary>
    public ProjectStatus Status { get; private set; } = ProjectStatus.Draft;
    /// <summary>
    /// Records when the project was created.
    /// 项目创建时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; }
    /// <summary>
    /// Records when the project was last changed.
    /// 项目最近更新时间。
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Creates a project with a validated name.
    /// 使用通过校验的名称创建项目。
    /// </summary>
    public static Project Create(string name, DateTimeOffset? now = null, Guid? id = null)
    {
        ValidateName(name);
        return new Project((id ?? Guid.NewGuid()), name.Trim(), now ?? DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Restores a project from persisted state.
    /// 根据持久化状态还原项目。
    /// </summary>
    public static Project Rehydrate(Guid id, string name, long version, ProjectStatus status, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        ValidateName(name);
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Project version must be positive. 项目版本必须为正数。");
        }
        return new Project(id, name.Trim(), createdAt)
        {
            Version = version,
            Status = status,
            UpdatedAt = updatedAt
        };
    }

    /// <summary>
    /// Renames the project and advances its version.
    /// 重命名项目并递增版本。
    /// </summary>
    public void Rename(string name, DateTimeOffset? now = null)
    {
        ValidateName(name);
        Name = name.Trim();
        Version++;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks the project as ready for use.
    /// 将项目标记为可用。
    /// </summary>
    public void MarkReady(DateTimeOffset? now = null)
    {
        Status = ProjectStatus.Ready;
        Version++;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks the project as having an invalid script.
    /// 将项目标记为脚本无效。
    /// </summary>
    public void MarkScriptInvalid(DateTimeOffset? now = null)
    {
        Status = ProjectStatus.ScriptInvalid;
        Version++;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Archives the project.
    /// 归档项目。
    /// </summary>
    public void Archive(DateTimeOffset? now = null)
    {
        if (Status == ProjectStatus.Archived)
        {
            return;
        }

        Status = ProjectStatus.Archived;
        Version++;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Project name cannot be empty. 项目名称不能为空。", nameof(name));
        }
    }
}

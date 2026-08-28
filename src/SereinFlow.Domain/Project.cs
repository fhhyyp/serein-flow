namespace SereinFlow.Domain;

public sealed class Project
{
    private Project(Guid id, string name, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    public long Version { get; private set; } = 1;

    public ProjectStatus Status { get; private set; } = ProjectStatus.Draft;

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Project Create(string name, DateTimeOffset? now = null, Guid? id = null)
    {
        ValidateName(name);
        return new Project(id ?? Guid.NewGuid(), name.Trim(), now ?? DateTimeOffset.UtcNow);
    }

    public static Project Rehydrate(
        Guid id,
        string name,
        long version,
        ProjectStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        ValidateName(name);
        if (version < 1)
            throw new ArgumentOutOfRangeException(nameof(version), "Project version must be positive. 项目版本必须为正数。");
        var project = new Project(id, name.Trim(), createdAt)
        {
            Version = version,
            Status = status,
            UpdatedAt = updatedAt
        };
        return project;
    }

    public void Rename(string name, DateTimeOffset? now = null)
    {
        ValidateName(name);
        Name = name.Trim();
        Version++;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    public void MarkReady(DateTimeOffset? now = null)
    {
        Status = ProjectStatus.Ready;
        Version++;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    public void MarkScriptInvalid(DateTimeOffset? now = null)
    {
        Status = ProjectStatus.ScriptInvalid;
        Version++;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    public void Archive(DateTimeOffset? now = null)
    {
        if (Status == ProjectStatus.Archived)
            return;

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

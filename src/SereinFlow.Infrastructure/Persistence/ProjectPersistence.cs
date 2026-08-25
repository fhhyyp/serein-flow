using System.Globalization;
using SereinFlow.Application.Persistence;
using SereinFlow.Domain;

namespace SereinFlow.Infrastructure.Persistence;

public sealed class SqlSugarProjectRepository : IProjectRepository
{
    private readonly IRepository<ProjectRecord> _projects;
    private readonly IUnitOfWork _unitOfWork;

    public SqlSugarProjectRepository(IRepository<ProjectRecord> projects, IUnitOfWork unitOfWork)
    {
        _projects = projects;
        _unitOfWork = unitOfWork;
    }

    public SqlSugarProjectRepository(SqliteDatabase database)
        : this(new SqlSugarRepository<ProjectRecord>(database.Client), new SqlSugarUnitOfWork(database.Client))
    {
    }

    public async Task<Project?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        => Map(await _projects.GetByIdAsync(id.ToString("D"), cancellationToken));

    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
        => (await _projects.ListAsync(cancellationToken: cancellationToken))
            .Select(Map)
            .Where(static project => project is not null)
            .Cast<Project>()
            .OrderBy(static project => project.CreatedAt)
            .ToArray();

    public Task AddAsync(Project project, CancellationToken cancellationToken = default)
    {
        if (project is null)
            throw new ArgumentNullException(nameof(project), "The project cannot be null. 项目不能为空。");
        return _projects.AddAsync(ToRecord(project), cancellationToken);
    }

    public Task<bool> TryUpdateAsync(Project project, long expectedVersion, CancellationToken cancellationToken = default)
    {
        if (project is null)
            throw new ArgumentNullException(nameof(project), "The project cannot be null. 项目不能为空。");
        return _unitOfWork.ExecuteAsync(async token =>
        {
            var existing = await _projects.GetByIdAsync(project.Id.ToString("D"), token);
            if (existing is null || existing.Version != expectedVersion)
                return false;
            return await _projects.UpdateAsync(ToRecord(project), token);
        }, cancellationToken);
    }

    public Project? Find(Guid id)
    {
        return Map(_projects.GetByIdAsync(id.ToString("D")).GetAwaiter().GetResult());
    }

    public IReadOnlyList<Project> List()
        => _projects.ListAsync(cancellationToken: CancellationToken.None).GetAwaiter().GetResult()
            .Select(Map)
            .Where(static project => project is not null)
            .Cast<Project>()
            .OrderBy(static project => project.CreatedAt)
            .ToArray();

    public void Add(Project project)
    {
        if (project is null)
            throw new ArgumentNullException(nameof(project), "The project cannot be null. 项目不能为空。");
        _projects.AddAsync(ToRecord(project)).GetAwaiter().GetResult();
    }

    public bool TryUpdate(Project project, long expectedVersion)
    {
        if (project is null)
            throw new ArgumentNullException(nameof(project), "The project cannot be null. 项目不能为空。");
        return _unitOfWork.ExecuteAsync(async cancellationToken =>
        {
            var existing = await _projects.GetByIdAsync(project.Id.ToString("D"), cancellationToken);
            if (existing is null || existing.Version != expectedVersion)
                return false;
            return await _projects.UpdateAsync(ToRecord(project), cancellationToken);
        }).GetAwaiter().GetResult();
    }

    private static ProjectRecord ToRecord(Project project)
        => new()
        {
            Id = project.Id.ToString("D"),
            Name = project.Name,
            Version = project.Version,
            Status = project.Status.ToString(),
            CreatedAt = project.CreatedAt.ToString("O"),
            UpdatedAt = project.UpdatedAt.ToString("O")
        };

    private static Project? Map(ProjectRecord? row)
    {
        if (row is null)
            return null;
        return Project.Rehydrate(
            Guid.Parse(row.Id),
            row.Name,
            row.Version,
            Enum.Parse<ProjectStatus>(row.Status, ignoreCase: false),
            DateTimeOffset.Parse(row.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            DateTimeOffset.Parse(row.UpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }
}

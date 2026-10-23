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
    {
        var projects = new SqlSugarRepository<ProjectRecord>(database.Client);
        var unitOfWork = new SqlSugarUnitOfWork(database.Client);
        _projects = projects;
        _unitOfWork = unitOfWork;
    }

    public async Task<Project?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var idText = id.ToString("D");
        return Map((await _projects.GetByIdAsync(idText, cancellationToken)));
    }

    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
    {
        return (await _projects.ListAsync(cancellationToken: cancellationToken))
            .Select(Map)
            .Where(static project => project is not null)
            .Cast<Project>()
            .OrderBy(static project => project.CreatedAt)
            .ToArray();
    }

    public Task AddAsync(Project project, CancellationToken cancellationToken = default)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project), "The project cannot be null. 项目不能为空。");
        }

        var entity = ToRecord(project);
        return _projects.AddAsync(entity, cancellationToken);
    }

    public Task<bool> TryUpdateAsync(Project project, long expectedVersion, CancellationToken cancellationToken = default)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project), "The project cannot be null. 项目不能为空。");
        }
        return _unitOfWork.ExecuteAsync(async token =>
        {
            var id = project.Id.ToString("D");
            var existing = await _projects.GetByIdAsync(id, token);
            if (existing is null || existing.Version != expectedVersion)
            {
                return false;
            }

            var entity = ToRecord(project);
            return await _projects.UpdateAsync(entity, token);
        }, cancellationToken);
    }

    public Project? Find(Guid id)
    {
        var idText = id.ToString("D");
        return Map(_projects
            .GetByIdAsync(idText)
            .GetAwaiter()
            .GetResult());
    }

    public IReadOnlyList<Project> List()
    {
        return _projects
            .ListAsync(cancellationToken: CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            .Select(Map)
            .Where(static project => project is not null)
            .Cast<Project>()
            .OrderBy(static project => project.CreatedAt)
            .ToArray();
    }

    public void Add(Project project)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project), "The project cannot be null. 项目不能为空。");
        }

        var entity = ToRecord(project);
        _projects
            .AddAsync(entity)
            .GetAwaiter()
            .GetResult();
    }

    public bool TryUpdate(Project project, long expectedVersion)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project), "The project cannot be null. 项目不能为空。");
        }

        var task = _unitOfWork.ExecuteAsync(async cancellationToken =>
        {
            var id = project.Id.ToString("D");
            var existing = await _projects.GetByIdAsync(id, cancellationToken);
            if (existing is null || existing.Version != expectedVersion)
            {
                return false;
            }

            var entity = ToRecord(project);
            return await _projects.UpdateAsync(entity, cancellationToken);
        });
        return task.GetAwaiter().GetResult();
    }

    private static ProjectRecord ToRecord(Project project)
    {
        return new()
        {
            Id = project.Id.ToString("D"),
            Name = project.Name,
            Version = project.Version,
            Status = project.Status.ToString(),
            CreatedAt = project.CreatedAt.ToString("O"),
            UpdatedAt = project.UpdatedAt.ToString("O")
        };
    }

    private static Project? Map(ProjectRecord? row)
    {
        if (row is null)
        {
            return null;
        }

        var id = Guid.Parse(row.Id);
        var status = Enum.Parse<ProjectStatus>(row.Status, ignoreCase: false);
        var createdAt = DateTimeOffset.Parse(row.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var updatedAt = DateTimeOffset.Parse(row.UpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return Project.Rehydrate(id, row.Name, row.Version, status, createdAt, updatedAt);
    }
}

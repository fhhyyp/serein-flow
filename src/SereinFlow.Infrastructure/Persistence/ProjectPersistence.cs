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
        var toStringResult = id.ToString("D");
        var getByIdAsyncResult = await _projects.GetByIdAsync(toStringResult, cancellationToken);
        var mapResult = Map(getByIdAsyncResult);
        return mapResult;
    }

    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
    {
        var toArrayResult = (await _projects.ListAsync(cancellationToken: cancellationToken))
            .Select(Map)
            .Where(static project => project is not null)
            .Cast<Project>()
            .OrderBy(static project => project.CreatedAt)
            .ToArray();
        return toArrayResult;
    }

    public Task AddAsync(Project project, CancellationToken cancellationToken = default)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project), "The project cannot be null. 项目不能为空。");
        }

        var toRecordResult = ToRecord(project);
        var addAsyncResult = _projects.AddAsync(toRecordResult, cancellationToken);
        return addAsyncResult;
    }

    public Task<bool> TryUpdateAsync(Project project, long expectedVersion, CancellationToken cancellationToken = default)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project), "The project cannot be null. 项目不能为空。");
        }

        var executeAsyncResult = _unitOfWork.ExecuteAsync(async token =>
        {
            var toStringResult1 = project.Id.ToString("D");
            var existing = await _projects.GetByIdAsync(toStringResult1, token);
            if (existing is null || existing.Version != expectedVersion)
            {
                return false;
            }

            var toRecordResult2 = ToRecord(project);
            var updateAsyncResult = await _projects.UpdateAsync(toRecordResult2, token);
            return updateAsyncResult;
        }, cancellationToken);
        return executeAsyncResult;
    }

    public Project? Find(Guid id)
    {
        var toStringResult3 = id.ToString("D");
        var getByIdAsyncResult4 = _projects.GetByIdAsync(toStringResult3);
        var getAwaiterResult = getByIdAsyncResult4.GetAwaiter();
        var getResultResult = getAwaiterResult.GetResult();
        var mapResult5 = Map(getResultResult);
        return mapResult5;
    }

    public IReadOnlyList<Project> List()
    {
        var listAsyncResult = _projects.ListAsync(cancellationToken: CancellationToken.None);
        var getAwaiterResult6 = listAsyncResult.GetAwaiter();
        var toArrayResult12 = getAwaiterResult6.GetResult()
            .Select(Map)
            .Where(static project => project is not null)
            .Cast<Project>()
            .OrderBy(static project => project.CreatedAt)
            .ToArray();
        return toArrayResult12;
    }

    public void Add(Project project)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project), "The project cannot be null. 项目不能为空。");
        }

        var toRecordResult13 = ToRecord(project);
        var addAsyncResult14 = _projects.AddAsync(toRecordResult13);
        var getAwaiterResult15 = addAsyncResult14.GetAwaiter();
        getAwaiterResult15.GetResult();
    }

    public bool TryUpdate(Project project, long expectedVersion)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project), "The project cannot be null. 项目不能为空。");
        }

        var executeAsyncResult19 = _unitOfWork.ExecuteAsync(async cancellationToken =>
        {
            var toStringResult16 = project.Id.ToString("D");
            var existing = await _projects.GetByIdAsync(toStringResult16, cancellationToken);
            if (existing is null || existing.Version != expectedVersion)
            {
                return false;
            }

            var toRecordResult17 = ToRecord(project);
            var updateAsyncResult18 = await _projects.UpdateAsync(toRecordResult17, cancellationToken);
            return updateAsyncResult18;
        });
        var getAwaiterResult20 = executeAsyncResult19.GetAwaiter();
        var getResultResult21 = getAwaiterResult20.GetResult();
        return getResultResult21;
    }

    private static ProjectRecord ToRecord(Project project)
    {
        ProjectRecord evaluatedValue22 = new()
        {
            Id = project.Id.ToString("D"),
            Name = project.Name,
            Version = project.Version,
            Status = project.Status.ToString(),
            CreatedAt = project.CreatedAt.ToString("O"),
            UpdatedAt = project.UpdatedAt.ToString("O")
        };
        return evaluatedValue22;
    }

    private static Project? Map(ProjectRecord? row)
    {
        if (row is null)
        {
            return null;
        }

        var parseResult = Guid.Parse(row.Id);
        var parseResult23 = Enum.Parse<ProjectStatus>(row.Status, ignoreCase: false);
        var parseResult24 = DateTimeOffset.Parse(row.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var parseResult25 = DateTimeOffset.Parse(row.UpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var rehydrateResult = Project.Rehydrate(parseResult, row.Name, row.Version, parseResult23, parseResult24, parseResult25);
        return rehydrateResult;
    }
}

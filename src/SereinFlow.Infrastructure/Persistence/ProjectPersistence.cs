using System.Globalization;
using SereinFlow.Application.Persistence;
using SereinFlow.Domain;
using SqlSugar;

namespace SereinFlow.Infrastructure.Persistence;

public sealed class SqlSugarProjectRepository : IProjectRepository
{
    private readonly SqliteDatabase _database;

    public SqlSugarProjectRepository(SqliteDatabase database)
    {
        _database = database;
    }

    public Project? Find(Guid id)
    {
        var row = _database.Query<ProjectRow>(
            "SELECT Id, Name, Version, Status, CreatedAt, UpdatedAt FROM Projects WHERE Id = @id",
            new SugarParameter("@id", id.ToString("D"))).SingleOrDefault();

        return row is null ? null : Map(row);
    }

    public IReadOnlyList<Project> List()
        => _database.Query<ProjectRow>(
                "SELECT Id, Name, Version, Status, CreatedAt, UpdatedAt FROM Projects ORDER BY CreatedAt")
            .Select(Map)
            .ToArray();

    public void Add(Project project)
    {
        if (project is null)
            throw new ArgumentNullException(nameof(project), "The project cannot be null. 项目不能为空。");
        _database.Execute(
            "INSERT INTO Projects (Id, Name, Version, Status, CreatedAt, UpdatedAt) VALUES (@id, @name, @version, @status, @createdAt, @updatedAt)",
            new SugarParameter("@id", project.Id.ToString("D")),
            new SugarParameter("@name", project.Name),
            new SugarParameter("@version", project.Version),
            new SugarParameter("@status", project.Status.ToString()),
            new SugarParameter("@createdAt", project.CreatedAt.ToString("O")),
            new SugarParameter("@updatedAt", project.UpdatedAt.ToString("O")));
    }

    public bool TryUpdate(Project project, long expectedVersion)
    {
        if (project is null)
            throw new ArgumentNullException(nameof(project), "The project cannot be null. 项目不能为空。");
        var affected = _database.Execute(
            "UPDATE Projects SET Name = @name, Version = @version, Status = @status, UpdatedAt = @updatedAt WHERE Id = @id AND Version = @expectedVersion",
            new SugarParameter("@name", project.Name),
            new SugarParameter("@version", project.Version),
            new SugarParameter("@status", project.Status.ToString()),
            new SugarParameter("@updatedAt", project.UpdatedAt.ToString("O")),
            new SugarParameter("@id", project.Id.ToString("D")),
            new SugarParameter("@expectedVersion", expectedVersion));
        return affected == 1;
    }

    private static Project Map(ProjectRow row)
        => Project.Rehydrate(
            Guid.Parse(row.Id),
            row.Name,
            row.Version,
            Enum.Parse<ProjectStatus>(row.Status, ignoreCase: false),
            DateTimeOffset.Parse(row.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            DateTimeOffset.Parse(row.UpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private sealed class ProjectRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public long Version { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
    }
}

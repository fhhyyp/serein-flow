using SereinFlow.Domain;

namespace SereinFlow.Application.Persistence;

public interface IProjectRepository
{
    IReadOnlyList<Project> List();

    Project? Find(Guid id);

    void Add(Project project);

    bool TryUpdate(Project project, long expectedVersion);
}

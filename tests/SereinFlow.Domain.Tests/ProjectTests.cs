using SereinFlow.Domain;

namespace SereinFlow.Domain.Tests;
public sealed class ProjectTests
{
    [Fact]
    public void ArchiveMarksProjectArchivedAndAdvancesVersionOnce()
    {
        var createdAt = new DateTimeOffset(2026, 8, 28, 9, 0, 0, TimeSpan.Zero);
        var archivedAt = createdAt.AddMinutes(5);
        var project = Project.Create("Archive test", createdAt);
        project.Archive(archivedAt);
        var addMinutesResult = archivedAt.AddMinutes(1);
        project.Archive(addMinutesResult);
        Assert.Equal(ProjectStatus.Archived, project.Status);
        Assert.Equal(2, project.Version);
        Assert.Equal(archivedAt, project.UpdatedAt);
    }
}

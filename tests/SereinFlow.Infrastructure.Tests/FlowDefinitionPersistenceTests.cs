using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Infrastructure.Persistence;

namespace SereinFlow.Infrastructure.Tests;

public sealed class FlowDefinitionPersistenceTests
{
    [Fact]
    public void FlowDefinitionRepositoryPreservesDocumentAndRejectsStaleUpdates()
    {
        using var database = CreateDatabase();
        database.Initialize();
        var project = Project.Create("Workflow");
        new SqlSugarProjectRepository(database).Add(project);
        var repository = new SqlSugarFlowDefinitionRepository(database);
        var initial = CreateDefinition();

        repository.Add(project.Id, initial);

        var loaded = repository.Find(project.Id, initial.Id);
        Assert.NotNull(loaded);
        Assert.Equal(NodeTypeDto.Trigger, loaded!.Canvases.Single().Nodes.Single().Type);
        Assert.Equal("node.httpTrigger", loaded.Canvases.Single().Nodes.Single().Ui!.TitleKey);

        var updated = initial with { Checksum = "updated" };
        var saved = repository.TryUpdate(project.Id, updated, expectedVersion: 1);

        Assert.NotNull(saved);
        Assert.Equal(2, saved!.Version);
        Assert.Equal("updated", saved.Checksum);
        Assert.Null(repository.TryUpdate(project.Id, updated, expectedVersion: 1));
        Assert.Equal(2L, database.Scalar<long>(
            "SELECT COUNT(*) FROM FlowDefinitionVersions WHERE FlowId = @flowId",
            new SqlSugar.SugarParameter("@flowId", initial.Id.ToString("D"))));
    }

    private static FlowDefinitionDto CreateDefinition()
    {
        var flowId = Guid.NewGuid();
        var node = new NodeDto(
            "trigger",
            NodeTypeDto.Trigger,
            "HTTP trigger",
            120,
            80,
            [new NodePortDto("exec-out", "Execution", "Output", false)],
            [],
            null,
            new NodeUiMetadataDto("trigger", "node.httpTrigger", "node.triggerSubtitle", null, "ready", true, 224));
        return new FlowDefinitionDto(
            flowId,
            1,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [])],
            "trigger",
            "initial");
    }

    private static SqliteDatabase CreateDatabase()
        => new(new SqliteDatabaseOptions(Path.Combine(Path.GetTempPath(), $"sereinflow-flow-{Guid.NewGuid():N}.db")));
}

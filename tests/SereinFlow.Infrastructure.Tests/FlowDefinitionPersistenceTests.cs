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
        Assert.Equal(NodeTypeDto.Action, loaded!.Canvases.Single().Nodes.Single().Type);
        Assert.Equal("node.httpTrigger", loaded.Canvases.Single().Nodes.Single().Ui!.TitleKey);
        Assert.Equal("method", loaded.Canvases.Single().Nodes.Single().Ui!.Category);
        Assert.Equal("Orders.Create", $"{loaded.Canvases.Single().Nodes.Single().Ui!.ClassName}.{loaded.Canvases.Single().Nodes.Single().Ui!.MethodName}");
        Assert.Equal("System.String", loaded.Canvases.Single().Nodes.Single().Parameters.Single().Ui!.Type);

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
            NodeTypeDto.Action,
            "HTTP trigger",
            120,
            80,
            [new NodePortDto("exec-success", "Success", "Output", false),
             new NodePortDto("exec-failure", "Failure", "Output", false),
             new NodePortDto("exec-error", "Error", "Output", false)],
            [new NodeParameterDto(
                "payload",
                "\"order\"",
                DataSourceDto.Literal,
                false,
                new NodeParameterUiMetadataDto("payload", "parameter.payload", "string", "order", null, null, null, null, "System.String", "Order payload", "manual"))],
            null,
            new NodeUiMetadataDto("trigger", "node.httpTrigger", "node.triggerSubtitle", null, "ready", true, 224, "method", "library-orders", "Orders", "Create", "Orders.dll", "1.2.0", "System.String"));
        return new FlowDefinitionDto(
            flowId,
            4,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [node], [])],
            "trigger",
            "initial",
            RunPolicy: new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel));
    }

    private static SqliteDatabase CreateDatabase()
        => new(new SqliteDatabaseOptions(Path.Combine(Path.GetTempPath(), $"sereinflow-flow-{Guid.NewGuid():N}.db")));
}

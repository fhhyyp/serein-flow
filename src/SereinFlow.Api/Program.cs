using System.Text.Json;
using System.Text.Json.Serialization;
using SereinFlow.Api;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod()));
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

var configuredDatabasePath = builder.Configuration["SereinFlow:DatabasePath"] ?? "data/sereinflow.db";
var databasePath = Path.IsPathRooted(configuredDatabasePath)
    ? configuredDatabasePath
    : Path.Combine(builder.Environment.ContentRootPath, configuredDatabasePath);
var database = new SqliteDatabase(new SqliteDatabaseOptions(databasePath));
database.Initialize();
builder.Services.AddSingleton(database);
builder.Services.AddSingleton<IProjectRepository, SqlSugarProjectRepository>();
builder.Services.AddSingleton<IFlowDefinitionRepository, SqlSugarFlowDefinitionRepository>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();

app.MapGet("/healthz", () => Results.Ok(new HealthCheckResponse("Healthy")))
    .WithName("GetHealth")
    .AllowAnonymous();

var projects = app.MapGroup("/api/projects");

projects.MapGet("", (IProjectRepository projectRepository, IFlowDefinitionRepository flowRepository) =>
{
    var workspaces = projectRepository.List()
        .Select(project => new ProjectWorkspaceDto(
            ToProjectDto(project),
            flowRepository.ListByProject(project.Id)
                .Select(flow => new FlowDefinitionSummaryDto(flow.Id, flow.Version, flow.EntryNodeId))
                .ToArray()))
        .ToArray();
    return Results.Ok(workspaces);
});

projects.MapPost("", (CreateProjectRequestDto request, IProjectRepository projectRepository, IFlowDefinitionRepository flowRepository) =>
{
    var validation = FlowDefinitionContractValidator.Validate(request.Definition);
    if (!validation.IsValid)
    {
        return Results.BadRequest(validation);
    }

    var project = Project.Create(request.Name);
    projectRepository.Add(project);
    flowRepository.Add(project.Id, request.Definition);
    var workspace = new ProjectWorkspaceDto(
        ToProjectDto(project),
        [new FlowDefinitionSummaryDto(request.Definition.Id, request.Definition.Version, request.Definition.EntryNodeId)]);
    return Results.Created($"/api/projects/{project.Id:D}/flows/{request.Definition.Id:D}", workspace);
});

projects.MapGet("/{projectId:guid}/flows/{flowId:guid}", (Guid projectId, Guid flowId, IProjectRepository projectRepository, IFlowDefinitionRepository flowRepository) =>
{
    if (projectRepository.Find(projectId) is null)
    {
        return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Project not found.");
    }

    var flow = flowRepository.Find(projectId, flowId);
    return flow is null
        ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Flow definition not found.")
        : Results.Ok(flow);
});

projects.MapPut("/{projectId:guid}/flows/{flowId:guid}", (Guid projectId, Guid flowId, UpdateFlowDefinitionRequestDto request, IProjectRepository projectRepository, IFlowDefinitionRepository flowRepository) =>
{
    if (request.ExpectedVersion < 1 || request.Definition.Id != flowId)
    {
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "The flow route and version must match the update request.");
    }

    if (projectRepository.Find(projectId) is null || flowRepository.Find(projectId, flowId) is null)
    {
        return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Flow definition not found.");
    }

    var validation = FlowDefinitionContractValidator.Validate(request.Definition);
    if (!validation.IsValid)
    {
        return Results.BadRequest(validation);
    }

    var saved = flowRepository.TryUpdate(projectId, request.Definition, request.ExpectedVersion);
    return saved is null
        ? Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Flow definition was changed by another editor.",
            extensions: new Dictionary<string, object?>
            {
                ["currentVersion"] = flowRepository.Find(projectId, flowId)?.Version
            })
        : Results.Ok(saved);
});

app.Run();

static ProjectDto ToProjectDto(Project project)
    => new(project.Id, project.Name, project.Version, project.Status.ToString(), project.CreatedAt, project.UpdatedAt);

public partial class Program;

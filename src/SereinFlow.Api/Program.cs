using System.Text.Json;
using System.Text.Json.Serialization;
using SereinFlow.Api;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        if (context.HttpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            context.ProblemDetails.Title = "An unexpected server error occurred. 服务器发生未预期错误。";
            context.ProblemDetails.Detail = "Please retry the request or contact support. 请重试请求或联系支持人员。";
        }
    };
});
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
var configuredLibraryDirectory = builder.Configuration["SereinFlow:LibraryDirectory"] ?? "data/libraries";
var libraryDirectory = Path.IsPathRooted(configuredLibraryDirectory)
    ? configuredLibraryDirectory
    : Path.Combine(builder.Environment.ContentRootPath, configuredLibraryDirectory);
builder.Services.AddSingleton(database);
builder.Services.AddSingleton<IProjectRepository, SqlSugarProjectRepository>();
builder.Services.AddSingleton<IFlowDefinitionRepository, SqlSugarFlowDefinitionRepository>();
builder.Services.AddSingleton<ILibraryCatalogService>(_ => new SqliteLibraryCatalogService(
    database,
    new LibraryCatalogOptions(libraryDirectory)));

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

projects.MapPut("/{projectId:guid}", (Guid projectId, RenameProjectRequestDto request, IProjectRepository projectRepository, IFlowDefinitionRepository flowRepository) =>
{
    if (request.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "A non-empty project name and a positive expected version are required. 项目名称不能为空，期望版本必须为正数。");
    }

    var project = projectRepository.Find(projectId);
    if (project is null)
    {
        return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Project not found. 未找到项目。");
    }

    if (project.Version != request.ExpectedVersion)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "The project was changed by another editor. 项目已被其他编辑器修改。",
            extensions: new Dictionary<string, object?>
            {
                ["currentVersion"] = project.Version
            });
    }

    project.Rename(request.Name);
    if (!projectRepository.TryUpdate(project, request.ExpectedVersion))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "The project was changed by another editor. 项目已被其他编辑器修改。",
            extensions: new Dictionary<string, object?>
            {
                ["currentVersion"] = projectRepository.Find(projectId)?.Version
            });
    }

    var workspace = new ProjectWorkspaceDto(
        ToProjectDto(project),
        flowRepository.ListByProject(project.Id)
            .Select(flow => new FlowDefinitionSummaryDto(flow.Id, flow.Version, flow.EntryNodeId))
            .ToArray());
    return Results.Ok(workspace);
});

projects.MapGet("/{projectId:guid}/flows/{flowId:guid}", (Guid projectId, Guid flowId, IProjectRepository projectRepository, IFlowDefinitionRepository flowRepository) =>
{
    if (projectRepository.Find(projectId) is null)
    {
        return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Project not found. 未找到项目。");
    }

    var flow = flowRepository.Find(projectId, flowId);
    return flow is null
        ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Flow definition not found. 未找到流程定义。")
        : Results.Ok(flow);
});

projects.MapPut("/{projectId:guid}/flows/{flowId:guid}", (Guid projectId, Guid flowId, UpdateFlowDefinitionRequestDto request, IProjectRepository projectRepository, IFlowDefinitionRepository flowRepository) =>
{
    if (request.ExpectedVersion < 1 || request.Definition.Id != flowId)
    {
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "The flow route and version must match the update request. 流程路由和版本必须与更新请求一致。");
    }

    if (projectRepository.Find(projectId) is null || flowRepository.Find(projectId, flowId) is null)
    {
        return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Flow definition not found. 未找到流程定义。");
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
            title: "Flow definition was changed by another editor. 流程定义已被其他编辑器修改。",
            extensions: new Dictionary<string, object?>
            {
                ["currentVersion"] = flowRepository.Find(projectId, flowId)?.Version
            })
        : Results.Ok(saved);
});

var libraries = app.MapGroup("/api/libraries");

libraries.MapGet("", (ILibraryCatalogService libraryCatalog) => Results.Ok(libraryCatalog.List()));

libraries.MapGet("/{libraryId}", (string libraryId, ILibraryCatalogService libraryCatalog) =>
{
    var library = libraryCatalog.Find(libraryId);
    return library is null
        ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Library not found. 未找到类库。")
        : Results.Ok(library);
});

async Task<IResult> UploadLibraryAsync(HttpRequest request, ILibraryCatalogService libraryCatalog, CancellationToken cancellationToken)
{
    if (!request.HasFormContentType)
    {
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "A multipart form upload is required. 必须使用 multipart 表单上传文件。");
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file") ?? (form.Files.Count > 0 ? form.Files[0] : null);
    if (file is null)
    {
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "The upload must include a file field named 'file'. 上传请求必须包含名为 file 的文件字段。");
    }

    try
    {
        await using var stream = file.OpenReadStream();
        var result = await libraryCatalog.UploadAsync(stream, file.FileName, file.Length, cancellationToken);
        return Results.Ok(result);
    }
    catch (LibraryUploadException exception)
    {
        return Results.Problem(statusCode: exception.StatusCode, title: "Library upload rejected. 类库上传已拒绝。", detail: exception.Message);
    }
}

// Keep both names during the transition so the TRAE client contract
// (POST /api/Libraries/upload-zip) and the new lower-case REST contract work.
// 过渡期间保留两个路径，以兼容 TRAE 客户端契约和新的小写 REST 契约。
libraries.MapPost("/upload", UploadLibraryAsync);
libraries.MapPost("/upload-zip", UploadLibraryAsync);

libraries.MapDelete("/{libraryId}", (string libraryId, ILibraryCatalogService libraryCatalog) =>
    libraryCatalog.Delete(libraryId)
        ? Results.NoContent()
        : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Library not found. 未找到类库。"));

// Singular aliases mirror the existing TRAE client service paths while the
// plural route remains the canonical REST contract for this project.
// 单数别名对应现有 TRAE 客户端服务路径，复数路径仍是本项目的标准 REST 契约。
var legacyLibraries = app.MapGroup("/api/library");
legacyLibraries.MapGet("", (ILibraryCatalogService libraryCatalog) => Results.Ok(libraryCatalog.List()));
legacyLibraries.MapGet("/{libraryId}", (string libraryId, ILibraryCatalogService libraryCatalog) =>
{
    var library = libraryCatalog.Find(libraryId);
    return library is null
        ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Library not found. 未找到类库。")
        : Results.Ok(library);
});
legacyLibraries.MapGet("/{libraryId}/nodes", (string libraryId, ILibraryCatalogService libraryCatalog) =>
{
    var library = libraryCatalog.Find(libraryId);
    return library is null
        ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Library not found. 未找到类库。")
        : Results.Ok(library.Nodes);
});
legacyLibraries.MapPost("/upload", UploadLibraryAsync);
legacyLibraries.MapPost("/upload-zip", UploadLibraryAsync);
legacyLibraries.MapDelete("/{libraryId}", (string libraryId, ILibraryCatalogService libraryCatalog) =>
    libraryCatalog.Delete(libraryId)
        ? Results.NoContent()
        : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Library not found. 未找到类库。"));

app.Run();

static ProjectDto ToProjectDto(Project project)
    => new(project.Id, project.Name, project.Version, project.Status.ToString(), project.CreatedAt, project.UpdatedAt);

public partial class Program;

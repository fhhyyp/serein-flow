using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using SereinFlow.Api;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Infrastructure.Persistence;
using SereinFlow.Worker.Client;

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
{
    options.SerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
builder.Services.AddSignalR();

builder.Services.AddSereinFlowInfrastructure(
    builder.Configuration,
    builder.Environment.ContentRootPath);
builder.Services.AddScoped<RunApplicationService>();

var workerRunnerPath = ResolveWorkerRunnerPath(
    builder.Configuration["SereinFlow:WorkerRunnerPath"],
    builder.Environment.ContentRootPath);
var workerRunnerFileName = IsManagedWorkerAssembly(workerRunnerPath) ? "dotnet" : workerRunnerPath;
builder.Services.AddSingleton<IWorkerRunClient>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<SupervisorWorkerRunClient>>();
    return new SupervisorWorkerRunClient(
        new SupervisorWorkerRunClientOptions(
            workerRunnerPath,
            RunnerFileName: workerRunnerFileName,
            WorkingDirectory: Path.GetDirectoryName(workerRunnerPath),
            AllowedScriptArtifactRoot: ResolveServicePath(builder.Configuration["SereinFlow:ScriptArtifactRoot"] ?? "data/script-artifacts", builder.Environment.ContentRootPath),
            AllowedLibraryPackageRoot: ResolveServicePath(builder.Configuration["SereinFlow:LibraryDirectory"] ?? "data/libraries", builder.Environment.ContentRootPath),
            DiagnosticLogger: message => WorkerLog.WorkerDiagnostic(logger, message, null)));
});
builder.Services.AddSingleton<RunExecutionQueue>();
builder.Services.AddSingleton<RunEventBroadcaster>();
builder.Services.AddHostedService<RunExecutionHostedService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();

app.MapGet("/healthz", () => Results.Ok(new HealthCheckResponse("Healthy")))
    .WithName("GetHealth")
    .AllowAnonymous();

var projects = app.MapGroup("/api/projects");

projects.MapPost("/{projectId:guid}/flows/{flowId:guid}/runs", async (
    Guid projectId,
    Guid flowId,
    RunFlowRequestDto request,
    RunApplicationService runService,
    RunExecutionQueue queue,
    CancellationToken cancellationToken) =>
{
    var preparation = await runService.PrepareAsync(projectId, flowId, request, cancellationToken);
    if (!preparation.IsSuccess)
    {
        if (preparation.ErrorBody is not null)
            return Results.BadRequest(preparation.ErrorBody);
        return Results.Problem(
            statusCode: preparation.StatusCode,
            title: preparation.ErrorTitle,
            extensions: preparation.CurrentVersion is null
                ? null
                : new Dictionary<string, object?> { ["currentVersion"] = preparation.CurrentVersion });
    }

    var prepared = preparation.Preparation!;
    await queue.EnqueueAsync(new RunWorkItem(
        prepared.Run.Id,
        projectId,
        prepared.Definition,
        prepared.ProjectInputs,
        prepared.Deadline,
        prepared.MaxSteps,
        prepared.MaxNodeVisits), cancellationToken);
    return Results.Accepted($"/api/runs/{prepared.Run.Id:D}", ToRunDto(prepared.Run));
});

projects.MapGet("", async (IProjectRepository projectRepository, IFlowDefinitionRepository flowRepository, CancellationToken cancellationToken) =>
{
    var workspaceList = new List<ProjectWorkspaceDto>();
    foreach (var project in await projectRepository.ListAsync(cancellationToken))
    {
        var flows = await flowRepository.ListByProjectAsync(project.Id, cancellationToken);
        workspaceList.Add(new ProjectWorkspaceDto(
            ToProjectDto(project),
            flows.Select(flow => new FlowDefinitionSummaryDto(flow.Id, flow.Version, flow.EntryNodeId)).ToArray()));
    }
    var workspaces = workspaceList.ToArray();
    return Results.Ok(workspaces);
});

projects.MapPost("", async (CreateProjectRequestDto request, IProjectRepository projectRepository, IFlowDefinitionRepository flowRepository, CancellationToken cancellationToken) =>
{
    var validation = FlowDefinitionContractValidator.Validate(request.Definition);
    if (!validation.IsValid)
    {
        return Results.BadRequest(validation);
    }

    var normalizedDefinition = FlowDefinitionContractNormalizer.Normalize(request.Definition);
    var project = Project.Create(request.Name);
    await projectRepository.AddAsync(project, cancellationToken);
    await flowRepository.AddAsync(project.Id, normalizedDefinition, cancellationToken);
    var workspace = new ProjectWorkspaceDto(
        ToProjectDto(project),
        [new FlowDefinitionSummaryDto(normalizedDefinition.Id, normalizedDefinition.Version, normalizedDefinition.EntryNodeId)]);
    return Results.Created($"/api/projects/{project.Id:D}/flows/{request.Definition.Id:D}", workspace);
});

projects.MapPut("/{projectId:guid}", async (Guid projectId, RenameProjectRequestDto request, IProjectRepository projectRepository, IFlowDefinitionRepository flowRepository, CancellationToken cancellationToken) =>
{
    if (request.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "A non-empty project name and a positive expected version are required. 项目名称不能为空，期望版本必须为正数。");
    }

    var project = await projectRepository.FindAsync(projectId, cancellationToken);
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
    if (!await projectRepository.TryUpdateAsync(project, request.ExpectedVersion, cancellationToken))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "The project was changed by another editor. 项目已被其他编辑器修改。",
            extensions: new Dictionary<string, object?>
            {
                ["currentVersion"] = (await projectRepository.FindAsync(projectId, cancellationToken))?.Version
            });
    }

    var flows = await flowRepository.ListByProjectAsync(project.Id, cancellationToken);
    var workspace = new ProjectWorkspaceDto(
        ToProjectDto(project),
        flows.Select(flow => new FlowDefinitionSummaryDto(flow.Id, flow.Version, flow.EntryNodeId)).ToArray());
    return Results.Ok(workspace);
});

projects.MapGet("/{projectId:guid}/flows/{flowId:guid}", async (Guid projectId, Guid flowId, IProjectRepository projectRepository, IFlowDefinitionRepository flowRepository, CancellationToken cancellationToken) =>
{
    if (await projectRepository.FindAsync(projectId, cancellationToken) is null)
    {
        return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Project not found. 未找到项目。");
    }

    var flow = await flowRepository.FindAsync(projectId, flowId, cancellationToken);
    return flow is null
        ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Flow definition not found. 未找到流程定义。")
        : Results.Ok(flow);
});

projects.MapPut("/{projectId:guid}/flows/{flowId:guid}", async (Guid projectId, Guid flowId, UpdateFlowDefinitionRequestDto request, IProjectRepository projectRepository, IFlowDefinitionRepository flowRepository, CancellationToken cancellationToken) =>
{
    if (request.ExpectedVersion < 1 || request.Definition.Id != flowId)
    {
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "The flow route and version must match the update request. 流程路由和版本必须与更新请求一致。");
    }

    if (await projectRepository.FindAsync(projectId, cancellationToken) is null || await flowRepository.FindAsync(projectId, flowId, cancellationToken) is null)
    {
        return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Flow definition not found. 未找到流程定义。");
    }

    var validation = FlowDefinitionContractValidator.Validate(request.Definition);
    if (!validation.IsValid)
    {
        return Results.BadRequest(validation);
    }

    var normalizedDefinition = FlowDefinitionContractNormalizer.Normalize(request.Definition);
    var saved = await flowRepository.TryUpdateAsync(projectId, normalizedDefinition, request.ExpectedVersion, cancellationToken);
    return saved is null
        ? Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Flow definition was changed by another editor. 流程定义已被其他编辑器修改。",
            extensions: new Dictionary<string, object?>
            {
                ["currentVersion"] = (await flowRepository.FindAsync(projectId, flowId, cancellationToken))?.Version
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

app.MapGet("/api/runs/{runId:guid}", async (Guid runId, IFlowRunStore runStore, CancellationToken cancellationToken) =>
{
    var run = await runStore.FindAsync(runId, cancellationToken);
    return run is null ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Run not found. 未找到运行实例。") : Results.Ok(ToRunDto(run));
});

app.MapGet("/api/runs/{runId:guid}/snapshot", async (Guid runId, IFlowRunStore runStore, CancellationToken cancellationToken) =>
{
    var snapshot = await runStore.GetSnapshotAsync(runId, cancellationToken);
    return snapshot is null ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Run snapshot not found. 未找到运行快照。") : Results.Ok(snapshot);
});

app.MapPost("/api/runs/{runId:guid}/cancel", async (Guid runId, RunExecutionQueue queue, IFlowRunStore runStore, CancellationToken cancellationToken) =>
{
    var run = await runStore.FindAsync(runId, cancellationToken);
    if (run is null)
        return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Run not found. 未找到运行实例。");
    if (run.IsTerminal)
        return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "The run is already complete. 运行实例已经完成。");
    return queue.Cancel(runId)
        ? Results.Accepted($"/api/runs/{runId:D}")
        : Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Run is not currently cancellable. 当前运行实例不可取消。");
});

app.MapGet("/api/runs/{runId:guid}/events", async (Guid runId, long? afterSequence, IFlowRunStore runStore, IFlowRunEventStore eventStore, CancellationToken cancellationToken) =>
{
    if (await runStore.FindAsync(runId, cancellationToken) is null)
        return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Run not found. 未找到运行实例。");
    var events = (await eventStore.GetAfterAsync(runId, afterSequence ?? 0, cancellationToken))
        .Select(item => new FlowRunEventDto(item.RunId, item.Sequence, item.Timestamp, item.Type, item.NodeId, item.PayloadJson));
    return Results.Ok(events);
});

app.MapGet("/api/runs/{runId:guid}/events/stream", async (
    Guid runId,
    HttpContext context,
    IFlowRunStore runStore,
    IFlowRunEventStore eventStore,
    RunEventBroadcaster broadcaster,
    CancellationToken cancellationToken) =>
{
    if (await runStore.FindAsync(runId, cancellationToken) is null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    var lastEventId = context.Request.Headers.TryGetValue("Last-Event-ID", out var value) && long.TryParse(value, out var parsed) ? parsed : 0;
    // Subscribe before replaying history so events committed during the
    // database read remain buffered and are de-duplicated by sequence.
    // 先订阅实时通道再回放历史，避免读取数据库期间提交的事件丢失。
    var reader = broadcaster.Subscribe(runId);
    foreach (var item in await eventStore.GetAfterAsync(runId, lastEventId, cancellationToken))
    {
        await WriteSseAsync(context, new FlowRunEventDto(item.RunId, item.Sequence, item.Timestamp, item.Type, item.NodeId, item.PayloadJson));
        lastEventId = item.Sequence;
    }

    await foreach (var item in reader.ReadAllAsync(context.RequestAborted))
    {
        if (item.Sequence <= lastEventId)
            continue;
        await WriteSseAsync(context, item);
        lastEventId = item.Sequence;
    }
});

app.MapHub<RunEventsHub>("/hubs/runs");

app.Run();

static ProjectDto ToProjectDto(Project project)
    => new(project.Id, project.Name, project.Version, project.Status.ToString(), project.CreatedAt, project.UpdatedAt);

static FlowRunDto ToRunDto(FlowRun run)
    => new(run.Id, run.FlowId, run.FlowVersion, (FlowRunStatusDto)run.Status, run.StartedAt, run.EndedAt, run.ErrorSummary, run.ProjectId == Guid.Empty ? null : run.ProjectId, run.CreatedAt, run.CancellationReason);

static async Task WriteSseAsync(HttpContext context, FlowRunEventDto item)
{
    await context.Response.WriteAsync($"id: {item.Sequence}\nevent: {item.Type}\ndata: {JsonSerializer.Serialize(item, SereinJsonSerialization.CreateWebOptions())}\n\n", context.RequestAborted);
    await context.Response.Body.FlushAsync(context.RequestAborted);
}

static string ResolveServicePath(string value, string root)
    => Path.IsPathRooted(value) ? Path.GetFullPath(value) : Path.GetFullPath(Path.Combine(root, value));

static string ResolveWorkerRunnerPath(string? configuredPath, string contentRootPath)
{
    var candidates = new List<string>();
    if (!string.IsNullOrWhiteSpace(configuredPath))
    {
        candidates.Add(Path.IsPathRooted(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.GetFullPath(Path.Combine(contentRootPath, configuredPath)));
    }

    var contentRoot = new DirectoryInfo(contentRootPath);
    for (var ancestor = contentRoot; ancestor is not null; ancestor = ancestor.Parent)
    {
        foreach (var configuration in new[] { "Debug", "Release" })
        {
            var outputRoot = Path.Combine(ancestor.FullName, "SereinFlow.Worker.Runner", "bin", configuration, "net10.0");
            candidates.Add(Path.Combine(outputRoot, "SereinFlow.Worker.Runner.exe"));
            candidates.Add(Path.Combine(outputRoot, "SereinFlow.Worker.Runner"));
            candidates.Add(Path.Combine(outputRoot, "SereinFlow.Worker.Runner.dll"));
        }
    }

    candidates.Add(Path.Combine(AppContext.BaseDirectory, "SereinFlow.Worker.Runner.exe"));
    candidates.Add(Path.Combine(AppContext.BaseDirectory, "SereinFlow.Worker.Runner"));
    candidates.Add(Path.Combine(AppContext.BaseDirectory, "SereinFlow.Worker.Runner.dll"));

    var existing = candidates.FirstOrDefault(File.Exists);
    if (existing is not null)
        return existing;

    var requested = candidates.FirstOrDefault() ?? Path.Combine(AppContext.BaseDirectory, "SereinFlow.Worker.Runner.dll");
    throw new InvalidOperationException(
        $"Worker Runner executable was not found. Worker Runner 可执行文件不存在。 Configure SereinFlow:WorkerRunnerPath. 请配置 SereinFlow:WorkerRunnerPath。 Requested path: '{requested}'.");
}

static bool IsManagedWorkerAssembly(string path)
    => string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase);

public partial class Program;

internal static class WorkerLog
{
    public static readonly Action<ILogger, string, Exception?> WorkerDiagnostic =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(4102, "WorkerDiagnostic"),
            "{WorkerDiagnostic}");
}

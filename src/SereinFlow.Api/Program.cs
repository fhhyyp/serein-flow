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
builder.Services.AddScoped<RunSubmissionService>();
builder.Services.AddScoped<RunInterruptionService>();
builder.Services.AddScoped<ProjectArchiveService>();
builder.Services.AddScoped<ProjectLibraryService>();
builder.Services.AddSingleton<ILibraryCompatibilityAnalyzer, LibraryCompatibilityAnalyzer>();
builder.Services.AddScoped<LibraryUpgradeService>();
builder.Services.AddSingleton<IBuiltinNodeCatalog, BuiltinNodeCatalog>();
builder.Services.Configure<RunExecutionOptions>(builder.Configuration.GetSection("SereinFlow:RunExecution"));

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
builder.Services.AddHostedService<LibraryCatalogReindexHostedService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();

app.MapGet("/healthz", () => Results.Ok(new HealthCheckResponse("Healthy")))
    .WithName("GetHealth")
    .AllowAnonymous();

var projects = app.MapGroup("/api/projects");

app.MapGet("/api/node-catalog/builtins", (IBuiltinNodeCatalog catalog) =>
    Results.Ok(catalog.GetCatalog()));

projects.MapPost("/{projectId:guid}/flows/{flowId:guid}/runs", async (
    Guid projectId,
    Guid flowId,
    RunFlowRequestDto request,
    RunSubmissionService submissionService,
    CancellationToken cancellationToken) =>
{
    var submission = await submissionService.SubmitAsync(projectId, flowId, request, cancellationToken);
    return ToRunSubmissionResponse(submission);
});

projects.MapGet("", async (bool? includeArchived, IProjectRepository projectRepository, IFlowDefinitionRepository flowRepository, CancellationToken cancellationToken) =>
{
    var workspaceList = new List<ProjectWorkspaceDto>();
    foreach (var project in await projectRepository.ListAsync(cancellationToken))
    {
        if (includeArchived != true && project.Status == ProjectStatus.Archived)
            continue;
        var flows = await flowRepository.ListByProjectAsync(project.Id, cancellationToken);
        workspaceList.Add(new ProjectWorkspaceDto(
            ToProjectDto(project),
            flows.Select(ToFlowSummaryDto).ToArray()));
    }
    var workspaces = workspaceList.ToArray();
    return Results.Ok(workspaces);
});

projects.MapPost("", async (CreateProjectRequestDto request, IProjectRepository projectRepository, IFlowDefinitionRepository flowRepository, CancellationToken cancellationToken) =>
{
    var validation = FlowDefinitionContractValidator.ValidateForPersistence(request.Definition);
    if (!validation.IsValid)
    {
        return Results.BadRequest(validation);
    }

    var libraryValidation = ProjectLibraryService.ValidateNewProjectFlowLibraries(request.Definition);
    if (!libraryValidation.IsValid)
    {
        return Results.BadRequest(libraryValidation);
    }

    var normalizedDefinition = FlowDefinitionContractNormalizer.NormalizeForPersistence(request.Definition);
    var project = Project.Create(request.Name);
    await projectRepository.AddAsync(project, cancellationToken);
    await flowRepository.AddAsync(project.Id, normalizedDefinition, cancellationToken);
    var workspace = new ProjectWorkspaceDto(
        ToProjectDto(project),
        [ToFlowSummaryDto(normalizedDefinition)]);
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
    if (project.Status == ProjectStatus.Archived)
    {
        return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Archived projects cannot be renamed. 已归档项目不能重命名。");
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
        flows.Select(ToFlowSummaryDto).ToArray());
    return Results.Ok(workspace);
});

projects.MapPost("/{projectId:guid}/archive", async (
    Guid projectId,
    ProjectArchiveService projectArchiveService,
    CancellationToken cancellationToken) =>
{
    var result = await projectArchiveService.ArchiveAsync(projectId, cancellationToken);
    if (result.IsSuccess)
        return Results.Ok(ToProjectDto(result.Project!));

    return Results.Problem(
        statusCode: result.StatusCode,
        title: result.Message,
        extensions: new Dictionary<string, object?> { ["code"] = result.Code });
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

projects.MapPut("/{projectId:guid}/flows/{flowId:guid}", async (Guid projectId, Guid flowId, UpdateFlowDefinitionRequestDto request, IProjectRepository projectRepository, IFlowDefinitionRepository flowRepository, ProjectLibraryService projectLibraries, CancellationToken cancellationToken) =>
{
    if (request.ExpectedVersion < 1 || request.Definition.Id != flowId)
    {
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "The flow route and version must match the update request. 流程路由和版本必须与更新请求一致。");
    }

    var project = await projectRepository.FindAsync(projectId, cancellationToken);
    if (project is null || await flowRepository.FindAsync(projectId, flowId, cancellationToken) is null)
    {
        return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Flow definition not found. 未找到流程定义。");
    }
    if (project.Status == ProjectStatus.Archived)
    {
        return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Archived projects cannot save flow definitions. 已归档项目不能保存流程定义。");
    }

    var validation = FlowDefinitionContractValidator.ValidateForPersistence(request.Definition);
    if (!validation.IsValid)
    {
        return Results.BadRequest(validation);
    }

    var libraryValidation = await projectLibraries.ValidateFlowLibrariesAsync(projectId, request.Definition, cancellationToken);
    if (!libraryValidation.IsValid)
    {
        return Results.BadRequest(libraryValidation);
    }

    var normalizedDefinition = FlowDefinitionContractNormalizer.NormalizeForPersistence(request.Definition);
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

projects.MapGet("/{projectId:guid}/libraries", async (
    Guid projectId,
    ProjectLibraryService projectLibraries,
    CancellationToken cancellationToken) =>
    ToProjectLibraryResponse(await projectLibraries.ListAsync(projectId, cancellationToken)));

projects.MapGet("/{projectId:guid}/libraries/usage", async (
    Guid projectId,
    IProjectRepository projectRepository,
    ILibraryArtifactUsageStore usageStore,
    CancellationToken cancellationToken) =>
{
    if (await projectRepository.FindAsync(projectId, cancellationToken) is null)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Project was not found. 未找到项目。");
    }

    return Results.Ok(await usageStore.ListByProjectAsync(projectId, cancellationToken));
});

projects.MapPut("/{projectId:guid}/libraries/{libraryId}", async (
    Guid projectId,
    string libraryId,
    ProjectLibraryService projectLibraries,
    CancellationToken cancellationToken) =>
    ToProjectLibraryResponse(await projectLibraries.AddAsync(projectId, libraryId, cancellationToken)));

projects.MapDelete("/{projectId:guid}/libraries/{libraryId}", async (
    Guid projectId,
    string libraryId,
    ProjectLibraryService projectLibraries,
    CancellationToken cancellationToken) =>
    ToProjectLibraryResponse(await projectLibraries.RemoveAsync(projectId, libraryId, cancellationToken)));

projects.MapPost("/{projectId:guid}/library-upgrades/preview", async (
    Guid projectId,
    LibraryUpgradePreviewRequestDto request,
    LibraryUpgradeService upgrades,
    CancellationToken cancellationToken) =>
    ToLibraryUpgradeResponse(await upgrades.PreviewAsync(projectId, request, cancellationToken)));

projects.MapGet("/{projectId:guid}/library-upgrades/{upgradeId:guid}", async (
    Guid projectId,
    Guid upgradeId,
    LibraryUpgradeService upgrades,
    CancellationToken cancellationToken) =>
    ToLibraryUpgradeResponse(await upgrades.GetPlanAsync(projectId, upgradeId, cancellationToken)));

projects.MapPost("/{projectId:guid}/library-upgrades/{upgradeId:guid}/apply", async (
    Guid projectId,
    Guid upgradeId,
    ApplyLibraryUpgradeRequestDto request,
    LibraryUpgradeService upgrades,
    CancellationToken cancellationToken) =>
    ToLibraryUpgradeResponse(await upgrades.ApplyAsync(projectId, upgradeId, request, cancellationToken)));

projects.MapPost("/{projectId:guid}/library-upgrades/{upgradeId:guid}/apply-batch", async (
    Guid projectId,
    Guid upgradeId,
    ApplyLibraryUpgradeBatchRequestDto request,
    LibraryUpgradeService upgrades,
    CancellationToken cancellationToken) =>
    ToLibraryUpgradeResponse(await upgrades.ApplyBatchAsync(projectId, upgradeId, request, cancellationToken)));

var libraries = app.MapGroup("/api/libraries");

app.MapGet("/api/library-families", async (ILibraryCatalogService libraryCatalog, CancellationToken cancellationToken) =>
    Results.Ok(await libraryCatalog.ListFamiliesAsync(cancellationToken: cancellationToken)));

app.MapGet("/api/library-families/{familyId}/artifacts", async (
    string familyId,
    ILibraryCatalogService libraryCatalog,
    CancellationToken cancellationToken) =>
{
    var family = (await libraryCatalog.ListFamiliesAsync(cancellationToken: cancellationToken))
        .SingleOrDefault(item => string.Equals(item.Id, familyId, StringComparison.OrdinalIgnoreCase));
    return family is null
        ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Library family not found. 未找到类库族。")
        : Results.Ok(family.Artifacts ?? []);
});

libraries.MapGet("", async (ILibraryCatalogService libraryCatalog, CancellationToken cancellationToken) =>
    Results.Ok(await libraryCatalog.ListAsync(cancellationToken: cancellationToken)));

libraries.MapGet("/usage", async (ILibraryArtifactUsageStore usageStore, CancellationToken cancellationToken) =>
    Results.Ok(await usageStore.ListAsync(cancellationToken)));

libraries.MapGet("/{libraryId}", async (string libraryId, ILibraryCatalogService libraryCatalog, CancellationToken cancellationToken) =>
{
    var library = await libraryCatalog.FindAsync(libraryId, cancellationToken);
    return library is null
        ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Library not found. 未找到类库。")
        : Results.Ok(library);
});

libraries.MapPatch("/{libraryId}/family", async (
    string libraryId,
    AssignLibraryFamilyRequestDto request,
    ILibraryCatalogService libraryCatalog,
    CancellationToken cancellationToken) =>
{
    try
    {
        var family = await libraryCatalog.AssignFamilyAsync(libraryId, request, cancellationToken);
        return family is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Library artifact not found. 未找到类库制品。")
            : Results.Ok(family);
    }
    catch (ArgumentException exception)
    {
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: exception.Message);
    }
});

libraries.MapPatch("/{libraryId}/lifecycle", async (
    string libraryId,
    UpdateLibraryLifecycleRequestDto request,
    ILibraryCatalogService libraryCatalog,
    CancellationToken cancellationToken) =>
{
    if (!Enum.IsDefined(request.Lifecycle))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "The library lifecycle is invalid. 类库生命周期无效。");
    }
    return await libraryCatalog.SetLifecycleAsync(libraryId, request.Lifecycle, cancellationToken)
        ? Results.NoContent()
        : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Library artifact not found. 未找到类库制品。");
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

libraries.MapDelete("/{libraryId}", async (string libraryId, ILibraryCatalogService libraryCatalog, CancellationToken cancellationToken) =>
    await libraryCatalog.ArchiveAsync(libraryId, cancellationToken)
        ? Results.NoContent()
        : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Library not found. 未找到类库。"));

// Singular aliases mirror the existing TRAE client service paths while the
// plural route remains the canonical REST contract for this project.
// 单数别名对应现有 TRAE 客户端服务路径，复数路径仍是本项目的标准 REST 契约。
var legacyLibraries = app.MapGroup("/api/library");
legacyLibraries.MapGet("", async (ILibraryCatalogService libraryCatalog, CancellationToken cancellationToken) =>
    Results.Ok(await libraryCatalog.ListAsync(cancellationToken: cancellationToken)));
legacyLibraries.MapGet("/{libraryId}", async (string libraryId, ILibraryCatalogService libraryCatalog, CancellationToken cancellationToken) =>
{
    var library = await libraryCatalog.FindAsync(libraryId, cancellationToken);
    return library is null
        ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Library not found. 未找到类库。")
        : Results.Ok(library);
});
legacyLibraries.MapGet("/{libraryId}/nodes", async (string libraryId, ILibraryCatalogService libraryCatalog, CancellationToken cancellationToken) =>
{
    var library = await libraryCatalog.FindAsync(libraryId, cancellationToken);
    return library is null
        ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Library not found. 未找到类库。")
        : Results.Ok(library.Nodes);
});
legacyLibraries.MapPost("/upload", UploadLibraryAsync);
legacyLibraries.MapPost("/upload-zip", UploadLibraryAsync);
legacyLibraries.MapDelete("/{libraryId}", async (string libraryId, ILibraryCatalogService libraryCatalog, CancellationToken cancellationToken) =>
    await libraryCatalog.ArchiveAsync(libraryId, cancellationToken)
        ? Results.NoContent()
        : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Library not found. 未找到类库。"));

var environmentApi = app.MapGroup("/api/environment");

environmentApi.MapGet("/libraries", async (ILibraryCatalogService libraryCatalog, CancellationToken cancellationToken) =>
    Results.Ok(await libraryCatalog.ListAsync(includeArchived: true, cancellationToken: cancellationToken)));

environmentApi.MapPost("/libraries/upload", UploadLibraryAsync);

environmentApi.MapPost("/libraries/{libraryId}/archive", async (
    string libraryId,
    ILibraryCatalogService libraryCatalog,
    CancellationToken cancellationToken) =>
    await libraryCatalog.ArchiveAsync(libraryId, cancellationToken)
        ? Results.NoContent()
        : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Library artifact not found. 未找到类库制品。"));

environmentApi.MapPost("/libraries/{libraryId}/reindex", async (
    string libraryId,
    ILibraryCatalogService libraryCatalog,
    CancellationToken cancellationToken) =>
{
    try
    {
        var library = await libraryCatalog.ReindexAsync(libraryId, cancellationToken);
        return library is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Library artifact not found. 未找到类库制品。")
            : Results.Ok(library);
    }
    catch (LibraryUploadException exception)
    {
        return Results.Problem(statusCode: exception.StatusCode, title: exception.Message);
    }
});

environmentApi.MapGet("/settings", (RunExecutionQueue queue) =>
    Results.Ok(queue.Options.ToDto()));

environmentApi.MapPut("/settings", async (
    RunExecutionSettingsDto settings,
    RunExecutionQueue queue,
    IRunEnvironmentSettingsStore settingsStore,
    CancellationToken cancellationToken) =>
{
    var normalized = RunExecutionOptions.FromDto(settings).ToDto();
    var saved = await settingsStore.SaveAsync(normalized, cancellationToken);
    return Results.Ok(queue.Configure(saved));
});

environmentApi.MapGet("/interfaces", async (IFlowInterfaceRepository interfaces, CancellationToken cancellationToken) =>
    Results.Ok(await interfaces.ListAsync(cancellationToken)));

environmentApi.MapPost("/interfaces", async (
    CreateFlowInterfaceRequestDto request,
    IProjectRepository projects,
    IFlowDefinitionRepository flows,
    IFlowInterfaceRepository interfaces,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 80)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "The interface name must contain 1 to 80 characters. 接口名称长度必须为 1 到 80 个字符。");
    }
    if (!Enum.IsDefined(request.InvocationMode))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "The interface invocation mode is invalid. 接口调用模式无效。");
    }
    var project = await projects.FindAsync(request.ProjectId, cancellationToken);
    if (project is null || await flows.FindAsync(request.ProjectId, request.FlowId, cancellationToken) is null)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "The selected project or flow was not found. 所选项目或流程不存在。");
    }
    if (project.Status == ProjectStatus.Archived)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Archived projects cannot be published through environment interfaces. 已归档项目不能发布为环境接口。");
    }

    var now = DateTimeOffset.UtcNow;
    var flowInterface = new FlowInterfaceDto(
        Guid.NewGuid(),
        request.ProjectId,
        request.FlowId,
        request.Name.Trim(),
        request.InvocationMode,
        request.IsEnabled,
        now,
        now);
    await interfaces.AddAsync(flowInterface, cancellationToken);
    return Results.Created($"/api/environment/interfaces/{flowInterface.Id:D}", flowInterface);
});

environmentApi.MapPut("/interfaces/{interfaceId:guid}", async (
    Guid interfaceId,
    UpdateFlowInterfaceRequestDto request,
    IFlowInterfaceRepository interfaces,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 80 || !Enum.IsDefined(request.InvocationMode))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "The interface configuration is invalid. 接口配置无效。");
    }
    var existing = await interfaces.FindAsync(interfaceId, cancellationToken);
    if (existing is null)
        return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Flow interface not found. 流程接口不存在。");

    var updated = existing with
    {
        Name = request.Name.Trim(),
        InvocationMode = request.InvocationMode,
        IsEnabled = request.IsEnabled,
        UpdatedAt = DateTimeOffset.UtcNow,
    };
    await interfaces.UpdateAsync(updated, cancellationToken);
    return Results.Ok(updated);
});

environmentApi.MapDelete("/interfaces/{interfaceId:guid}", async (
    Guid interfaceId,
    IFlowInterfaceRepository interfaces,
    CancellationToken cancellationToken) =>
    await interfaces.DeleteAsync(interfaceId, cancellationToken)
        ? Results.NoContent()
        : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Flow interface not found. 流程接口不存在。"));

app.MapPost("/api/public/flows/{interfaceId:guid}/invoke", async (
    Guid interfaceId,
    PublicFlowInvocationRequestDto request,
    IFlowInterfaceRepository interfaces,
    RunSubmissionService submissionService,
    IFlowRunStore runStore,
    IFlowRunOutputStore outputStore,
    RunExecutionQueue queue,
    CancellationToken cancellationToken) =>
{
    var flowInterface = await interfaces.FindAsync(interfaceId, cancellationToken);
    if (flowInterface is null || !flowInterface.IsEnabled)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "The flow interface is unavailable. 流程接口不可用。");
    }

    var submission = await submissionService.SubmitAsync(
        flowInterface.ProjectId,
        flowInterface.FlowId,
        new RunFlowRequestDto(null, request.ProjectInputs, request.TimeoutSeconds, request.MaxSteps, request.MaxNodeVisits),
        cancellationToken);
    if (!submission.IsAccepted)
        return ToRunSubmissionResponse(submission);

    var submittedRun = submission.Run!;
    if (flowInterface.InvocationMode == FlowInvocationModeDto.Asynchronous)
    {
        return Results.Accepted(
            $"/api/public/tasks/{submittedRun.Id:D}",
            new PublicFlowInvocationResponseDto(
                submittedRun.Id,
                FlowRunStatusDto.Pending,
                false,
                []));
    }

    var maximumWait = TimeSpan.FromSeconds(queue.Options.SynchronousInvocationTimeoutSeconds);
    var completedRun = await WaitForTerminalRunAsync(runStore, submittedRun.Id, maximumWait, cancellationToken);
    if (completedRun is null || !completedRun.IsTerminal)
    {
        return Results.Accepted(
            $"/api/public/tasks/{submittedRun.Id:D}",
            new PublicFlowInvocationResponseDto(
                submittedRun.Id,
                FlowRunStatusDto.Pending,
                false,
                []));
    }

    return Results.Ok(new PublicFlowInvocationResponseDto(
        completedRun.Id,
        (FlowRunStatusDto)completedRun.Status,
        true,
        await ReadNodeDataAsync(outputStore, completedRun.Id, cancellationToken)));
});

app.MapGet("/api/public/tasks/{taskId:guid}", async (
    Guid taskId,
    IFlowRunStore runStore,
    IFlowRunOutputStore outputStore,
    CancellationToken cancellationToken) =>
{
    var run = await runStore.FindAsync(taskId, cancellationToken);
    if (run is null)
        return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Task not found. 任务不存在。");

    return Results.Ok(new PublicFlowInvocationResponseDto(
        run.Id,
        (FlowRunStatusDto)run.Status,
        run.IsTerminal,
        run.IsTerminal ? await ReadNodeDataAsync(outputStore, run.Id, cancellationToken) : []));
});

app.MapGet("/api/runs", async (
    string? status,
    Guid? projectId,
    int? take,
    IFlowRunStore runStore,
    CancellationToken cancellationToken) =>
{
    IReadOnlyCollection<FlowRunStatus>? statuses = null;
    if (!string.IsNullOrWhiteSpace(status))
    {
        var parsed = new List<FlowRunStatus>();
        foreach (var value in status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<FlowRunStatus>(value, ignoreCase: true, out var item))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "A run status filter is invalid. 运行状态筛选条件无效。",
                    extensions: new Dictionary<string, object?> { ["code"] = "run.status_invalid" });
            }
            parsed.Add(item);
        }
        statuses = parsed.Distinct().ToArray();
    }

    var runs = await runStore.ListAsync(new FlowRunQuery(statuses, projectId, take ?? 100), cancellationToken);
    return Results.Ok(runs.Select(ToRunDto).ToArray());
});

app.MapGet("/api/runs/overview", async (
    IFlowRunStore runStore,
    RunExecutionQueue queue,
    CancellationToken cancellationToken) =>
{
    var queued = await runStore.ListAsync(new FlowRunQuery([FlowRunStatus.Pending], Take: 50), cancellationToken);
    var active = await runStore.ListAsync(new FlowRunQuery([FlowRunStatus.Running], Take: 50), cancellationToken);
    var recent = await runStore.ListAsync(new FlowRunQuery(Take: 100), cancellationToken);
    var snapshot = queue.GetSnapshot();
    return Results.Ok(new FlowRunOverviewDto(
        snapshot.QueueCapacity,
        snapshot.QueuedCount,
        snapshot.ActiveRunCount,
        snapshot.ActiveListenerRunCount,
        snapshot.MaxConcurrentRuns,
        snapshot.MaxConcurrentListenerRuns,
        snapshot.MaxConcurrentRunsPerProject,
        queued.Select(ToRunDto).ToArray(),
        active.Select(ToRunDto).ToArray(),
        recent.Where(static run => run.IsTerminal).Take(50).Select(ToRunDto).ToArray()));
});

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

app.MapGet("/api/runs/{runId:guid}/outputs", async (
    Guid runId,
    IFlowRunStore runStore,
    IFlowRunOutputStore outputStore,
    CancellationToken cancellationToken) =>
{
    if (await runStore.FindAsync(runId, cancellationToken) is null)
        return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Run not found. 未找到运行实例。");

    var outputs = await outputStore.ListAsync(runId, cancellationToken);
    return Results.Ok(outputs.Select(ToRunOutputDto).ToArray());
});

app.MapPost("/api/runs/{runId:guid}/cancel", async (Guid runId, RunExecutionQueue queue, IFlowRunStore runStore, CancellationToken cancellationToken) =>
{
    var run = await runStore.FindAsync(runId, cancellationToken);
    if (run is null)
        return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Run not found. 未找到运行实例。");
    if (run.IsTerminal)
        return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "The run is already complete. 运行实例已经完成。");
    return queue.Cancel(runId, RunCancellationSources.Api)
        ? Results.Accepted($"/api/runs/{runId:D}")
        : Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Run is not currently cancellable. 当前运行实例不可取消。");
});

app.MapPost("/api/runs/{runId:guid}/interrupt", async (
    Guid runId,
    RunExecutionQueue queue,
    IFlowRunStore runStore,
    RunInterruptionService interruptionService,
    CancellationToken cancellationToken) =>
{
    var run = await runStore.FindAsync(runId, cancellationToken);
    if (run is null)
        return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Run not found. 未找到运行实例。");
    if (run.IsTerminal)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "The run is already complete. 运行实例已经完成。");
    }
    if (run.Status != FlowRunStatus.Running)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Only an orphaned running run can be marked as interrupted. 只有孤儿运行中的实例可以标记为中断。");
    }
    if (queue.IsTracked(runId))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "The run is currently supervised and cannot be interrupted manually. 当前运行实例正在受控执行，不能手动标记为中断。");
    }

    var result = await interruptionService.InterruptAsync(
        runId,
        "operator_reconciliation",
        "run.operator_interrupted",
        "The orphaned run was marked as interrupted by an operator. 孤儿运行实例已由操作人员标记为中断。",
        cancellationToken);
    return result.Disposition switch
    {
        RunInterruptionDisposition.Interrupted => Results.Accepted($"/api/runs/{runId:D}", ToRunDto(result.Run!)),
        RunInterruptionDisposition.NotFound => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Run not found. 未找到运行实例。"),
        RunInterruptionDisposition.AlreadyTerminal => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "The run is already complete. 运行实例已经完成。"),
        RunInterruptionDisposition.NotRunning => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Only an orphaned running run can be marked as interrupted. 只有孤儿运行中的实例可以标记为中断。"),
        _ => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "The run could not be marked as interrupted. 无法将运行实例标记为中断。")
    };
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
    var run = await runStore.FindAsync(runId, cancellationToken);
    if (run is null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    var lastEventId = context.Request.Headers.TryGetValue("Last-Event-ID", out var value) && long.TryParse(value, out var parsed) ? parsed : 0;
    // Terminal runs only require a durable event replay. Active runs subscribe
    // before reading history, so events committed during the read remain
    // buffered and are de-duplicated by sequence.
    // 已结束实例只需回放持久化事件；活动实例先订阅再读取历史，避免读取期间提交的事件丢失。
    var reader = run.IsTerminal ? null : broadcaster.Subscribe(runId, context.RequestAborted);
    foreach (var item in await eventStore.GetAfterAsync(runId, lastEventId, cancellationToken))
    {
        await WriteSseAsync(context, new FlowRunEventDto(item.RunId, item.Sequence, item.Timestamp, item.Type, item.NodeId, item.PayloadJson));
        lastEventId = item.Sequence;
    }

    if (reader is null)
        return;

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
    => new(project.Id, project.Name, project.Version, ToProjectStatusValue(project.Status), project.CreatedAt, project.UpdatedAt);

static string ToProjectStatusValue(ProjectStatus status)
    => status switch
    {
        ProjectStatus.Draft => "draft",
        ProjectStatus.Ready => "ready",
        ProjectStatus.ScriptInvalid => "scriptInvalid",
        ProjectStatus.Archived => "archived",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The project status is not supported. 项目状态不受支持。")
    };

static FlowRunDto ToRunDto(FlowRun run)
    => new(
        run.Id,
        run.FlowId,
        run.FlowVersion,
        (FlowRunStatusDto)run.Status,
        run.StartedAt,
        run.EndedAt,
        run.ErrorSummary,
        run.ProjectId == Guid.Empty ? null : run.ProjectId,
        run.CreatedAt,
        run.CancellationReason,
        (FlowConcurrencyModeDto)run.ConcurrencyMode,
        run.IsListenerRun,
        run.QueuedAt);

static FlowDefinitionSummaryDto ToFlowSummaryDto(FlowDefinitionDto definition)
    => new(
        definition.Id,
        definition.Version,
        definition.EntryNodeId,
        definition.Canvases.Count,
        definition.Canvases.Sum(static canvas => canvas.Nodes.Count));

static IResult ToRunSubmissionResponse(RunSubmissionResult submission)
{
    if (submission.IsAccepted)
        return Results.Accepted($"/api/runs/{submission.Run!.Id:D}", ToRunDto(submission.Run));
    if (submission.ErrorBody is not null)
        return Results.Json(submission.ErrorBody, statusCode: submission.StatusCode);
    return Results.Problem(
        statusCode: submission.StatusCode,
        title: submission.ErrorTitle,
        extensions: submission.CurrentVersion is null
            ? null
            : new Dictionary<string, object?> { ["currentVersion"] = submission.CurrentVersion });
}

static IResult ToProjectLibraryResponse(ProjectLibraryOperationResult result)
{
    if (result.IsSuccess)
        return Results.Ok(result.References ?? []);

    return Results.Problem(
        statusCode: result.StatusCode,
        title: result.Message,
        extensions: string.IsNullOrWhiteSpace(result.Code)
            ? null
            : new Dictionary<string, object?> { ["code"] = result.Code });
}

static IResult ToLibraryUpgradeResponse<T>(LibraryUpgradeOperationResult<T> result)
{
    if (result.IsSuccess)
    {
        return result.StatusCode == StatusCodes.Status201Created
            ? Results.Created($"/api/projects/library-upgrades/{GetUpgradeId(result.Value)}", result.Value)
            : Results.Ok(result.Value);
    }

    var extensions = new Dictionary<string, object?> { ["code"] = result.Code };
    if (result.CurrentVersion is not null)
        extensions["currentVersion"] = result.CurrentVersion;
    return Results.Problem(
        statusCode: result.StatusCode,
        title: result.Message ?? "Library upgrade request failed. 类库升级请求失败。",
        extensions: extensions);
}

static string GetUpgradeId<T>(T? value)
    => value is LibraryUpgradePlanDto plan ? plan.Id.ToString("D") : string.Empty;

static async Task<FlowRun?> WaitForTerminalRunAsync(
    IFlowRunStore runStore,
    Guid runId,
    TimeSpan timeout,
    CancellationToken cancellationToken)
{
    var deadline = DateTimeOffset.UtcNow + timeout;
    while (DateTimeOffset.UtcNow < deadline)
    {
        var run = await runStore.FindAsync(runId, cancellationToken);
        if (run is null || run.IsTerminal)
            return run;
        await Task.Delay(TimeSpan.FromMilliseconds(80), cancellationToken);
    }
    return await runStore.FindAsync(runId, cancellationToken);
}

static async Task<IReadOnlyList<FlowNodeDataDto>> ReadNodeDataAsync(
    IFlowRunOutputStore outputStore,
    Guid runId,
    CancellationToken cancellationToken)
{
    var data = new List<FlowNodeDataDto>();
    foreach (var item in await outputStore.ListAsync(runId, cancellationToken))
    {
        if (!string.Equals(item.Outcome, "completed", StringComparison.OrdinalIgnoreCase))
            continue;

        using var outputs = JsonDocument.Parse(item.OutputsJson);
        data.Add(new FlowNodeDataDto(item.NodeId, outputs.RootElement.Clone()));
    }

    return data;
}

static FlowRunOutputDto ToRunOutputDto(FlowRunOutput output)
{
    using var inputs = JsonDocument.Parse(output.InputsJson);
    using var outputs = JsonDocument.Parse(output.OutputsJson);
    return new FlowRunOutputDto(
        output.RunId,
        output.Sequence,
        output.Timestamp,
        output.NodeId,
        output.Outcome,
        output.Branch,
        inputs.RootElement.Clone(),
        outputs.RootElement.Clone(),
        output.ErrorCode,
        output.ErrorMessage);
}

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

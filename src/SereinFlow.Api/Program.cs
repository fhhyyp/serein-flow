using SereinFlow.Api;
using SereinFlow.Application;
using SereinFlow.Infrastructure.Configuration;
using SereinFlow.Infrastructure.Persistence;
using SereinFlow.Mcp;

if (args.Any(static argument => string.Equals(argument, "--mcp-stdio", StringComparison.OrdinalIgnoreCase)))
{
    await McpStdioHost.RunAsync(args);
    return;
}

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
builder.Services.AddSereinFlowWebApi();
builder.Services.AddSereinFlowApiDocumentation();
builder.Services.AddSignalR();

var storageOptions = SereinFlowStorageOptions.FromConfiguration(
    builder.Configuration,
    builder.Environment.ContentRootPath);
builder.Services.AddSereinFlowStorage(storageOptions);
builder.Services.AddSereinFlowApplication();
var mcpOptions = SereinFlowMcpOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSereinFlowMcp(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddSereinFlowExecution(
    builder.Configuration,
    builder.Environment.ContentRootPath);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = FileUploadLimits.GetApiRequestBodyLimit(
        FileUploadLimits.MaximumMaxFileSizeBytes);
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = Math.Max(
        FileUploadLimits.GetApiRequestBodyLimit(FileUploadLimits.MaximumMaxFileSizeBytes),
        FileUploadLimits.GetMcpRequestBodyLimit(
            FileUploadLimits.MaximumMaxFileSizeBytes,
            mcpOptions.MaxRequestBytes));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseSereinFlowApiDocumentation();
app.MapSereinFlowMcp();
app.MapControllers();
app.MapHub<RunEventsHub>("/hubs/runs");
app.MapHub<WorkspaceEventsHub>("/hubs/workspace");


app.Run();

public partial class Program;

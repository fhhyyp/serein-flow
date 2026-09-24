using SereinFlow.Api;
using SereinFlow.AspNetCore;

if (args.Any(static argument => string.Equals(argument, "--mcp-stdio", StringComparison.OrdinalIgnoreCase)))
{
    await McpStdioHost.RunAsync(args);
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration["SereinFlow:Api:RequireAuthentication"] ??= "false";

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
builder.Services.AddSereinFlow(options =>
{
    options.UseWebApi(api => api.AllowAnyOrigin = true);
    options.UseMcp();
});

var app = builder.Build();

app.UseSereinFlow();
app.MapSereinFlow();

app.Run();

public partial class Program;

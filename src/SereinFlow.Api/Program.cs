using SereinFlow.Security.ApiKeys;
using SereinFlow.Api;
using SereinFlow.AspNetCore;

if (args.Any(static argument =>
{
    var equalsResult = string.Equals(argument, "--mcp-stdio", StringComparison.OrdinalIgnoreCase);
    return equalsResult;
}))
{
    await McpStdioHost.RunAsync(args);
    return;
}

var builder = WebApplication.CreateBuilder(args);
var securityMode = builder.Configuration["SereinFlow:Security:Mode"] ?? "None";
if (!string.Equals(securityMode, "None", StringComparison.OrdinalIgnoreCase)
    && !string.Equals(securityMode, "ApiKey", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("The standalone HTTP host supports None (default) or ApiKey security. Use UseHostSecurity() in an embedding host for external authentication.");
}

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
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowAnyOriginResult = policy.AllowAnyOrigin();
        var allowAnyHeaderResult = allowAnyOriginResult.AllowAnyHeader();
        allowAnyHeaderResult.AllowAnyMethod();
    });
});
builder.Services.AddSereinFlow(options =>
{
    options.UseWebApi();
    options.UseMcp();
    if (string.Equals(securityMode, "ApiKey", StringComparison.OrdinalIgnoreCase))
    {
        options.UseApiKeySecurity();
    }
});
if (string.Equals(securityMode, "None", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddControllers().ConfigureApplicationPartManager(manager =>
    {
        var keyAssembly = typeof(ApiKeyHosting).Assembly.GetName().Name;
        foreach (var part in manager.ApplicationParts.Where(part => part.Name == keyAssembly).ToArray())
        {
            manager.ApplicationParts.Remove(part);
        }
    });
}
var app = builder.Build();
app.UseSereinFlow();
app.MapSereinFlow();
app.Run();
public partial class Program;

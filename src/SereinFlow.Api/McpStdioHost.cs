using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using SereinFlow.Api;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Infrastructure.Configuration;
using SereinFlow.Infrastructure.Persistence;
using SereinFlow.Mcp;
using SereinFlow.Worker.Client;

namespace SereinFlow.Api;

internal static class McpStdioHost
{
    public static async Task RunAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        var storageOptions = SereinFlowStorageOptions.FromConfiguration(
            builder.Configuration,
            builder.Environment.ContentRootPath);
        builder.Services.AddSereinFlowStorage(storageOptions);
        builder.Services.AddSereinFlowApplication();
        builder.Services.AddSereinFlowMcp(builder.Configuration, builder.Environment.ContentRootPath);

        var apiKey = builder.Configuration["SereinFlow:Mcp:Stdio:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "SereinFlow:Mcp:Stdio:ApiKey must be configured explicitly for --mcp-stdio.");
        }

        using var host = builder.Build();
        var principalAccessor = host.Services.GetRequiredService<IMcpPrincipalAccessor>();
        var requestContextAccessor = host.Services.GetRequiredService<IMcpRequestContextAccessor>();
        var security = host.Services.GetRequiredService<McpSecurityService>();
        var principal = await security.AuthenticateAsync(apiKey, CancellationToken.None);
        if (principal is null)
        {
            throw new InvalidOperationException(
                "The configured stdio MCP API key is invalid, expired or revoked.");
        }

        principalAccessor.Current = principal;
        requestContextAccessor.Current = new McpRequestContext(principal, "stdio");
        try
        {
            await host.Services.GetRequiredService<SereinFlowMcpServer>()
                .RunAsync(Console.In, Console.Out);
        }
        finally
        {
            requestContextAccessor.Current = null;
            principalAccessor.Current = null;
        }
    }

}

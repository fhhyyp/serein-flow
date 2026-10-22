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
        var storageOptions = SereinFlowStorageOptions.FromConfiguration(builder.Configuration, builder.Environment.ContentRootPath);
        builder.Services.AddSereinFlowStorage(storageOptions);
        builder.Services.AddSereinFlowApplication();
        builder.Services.AddSignalR();
        builder.Services.AddSereinFlowMcp(builder.Configuration, builder.Environment.ContentRootPath);
        builder.Services.AddSereinFlowExecution(builder.Configuration, builder.Environment.ContentRootPath);
        builder.Services.AddSereinFlowLocalAccess();
        using var host = builder.Build();
        var principalAccessor = host.Services.GetRequiredService<ISereinFlowActorAccessor>();
        var requestContextAccessor = host.Services.GetRequiredService<IMcpRequestContextAccessor>();
        var principal = LocalTrustedAuthorization.CreateActor(storageOptions.DataRoot);
        using var actorScope = principalAccessor.Push(principal);
        var previousContext = requestContextAccessor.Current;
        requestContextAccessor.Current = new McpRequestContext("stdio");
        try
        {
            var getRequiredServiceResult = host.Services.GetRequiredService<SereinFlowMcpServer>();
            await getRequiredServiceResult.RunAsync(Console.In, Console.Out);
        }
        finally
        {
            requestContextAccessor.Current = previousContext;
        }
    }
}

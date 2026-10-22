using Microsoft.AspNetCore.SignalR;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.AspNetCore;
public sealed class SereinFlowHubSecurity : IHubFilter
{
    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        var http = context.Context.GetHttpContext()!;
        if (SereinFlowHttpActor.Get(http) is null)
        {
            throw new HubException("Authentication is required.");
        }

        await next(context);
    }

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var http = context.Context.GetHttpContext()!;
        var actor = SereinFlowHttpActor.Get(http);
        var access = context.ServiceProvider.GetRequiredService<SereinFlowAccess>();
        if (context.HubMethodName == "Subscribe")
        {
            var runId = (Guid)context.HubMethodArguments[0]!;
            var store = context.ServiceProvider.GetRequiredService<IFlowRunStore>();
            var run = await store.FindAsync(runId, context.Context.ConnectionAborted) ?? throw new HubException("Run not found.");
            var resource = SereinFlowResource.Run(runId, run.ProjectId);
            await access.RequireResourceAsync(actor, SereinFlowOperations.RunRead, resource, context.Context.ConnectionAborted);
        }
        else if (context.HubMethodName == "SubscribeProject")
        {
            await access.RequireAsync(actor, SereinFlowPermission.ProjectRead, (Guid)context.HubMethodArguments[0]!, context.Context.ConnectionAborted);
        }
        else if (context.HubMethodName == "SubscribeGlobal")
        {
            await access.RequireWorkspaceAsync(actor);
        }

        {
            var result1 = await next(context);
            return result1;
        }
    }
}

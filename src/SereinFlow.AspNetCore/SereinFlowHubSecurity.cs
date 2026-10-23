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

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var http = invocationContext.Context.GetHttpContext()!;
        var actor = SereinFlowHttpActor.Get(http);
        var access = invocationContext.ServiceProvider.GetRequiredService<SereinFlowAccess>();
        if (invocationContext.HubMethodName == "Subscribe")
        {
            var runId = (Guid)invocationContext.HubMethodArguments[0]!;
            var store = invocationContext.ServiceProvider.GetRequiredService<IFlowRunStore>();
            var run = await store.FindAsync(runId, invocationContext.Context.ConnectionAborted) ?? throw new HubException("Run not found.");
            var resource = SereinFlowResource.Run(runId, run.ProjectId);
            await access.RequireResourceAsync(actor, SereinFlowOperations.RunRead, resource, invocationContext.Context.ConnectionAborted);
        }
        else if (invocationContext.HubMethodName == "SubscribeProject")
        {
            await access.RequireAsync(actor, SereinFlowPermission.ProjectRead, (Guid)invocationContext.HubMethodArguments[0]!, invocationContext.Context.ConnectionAborted);
        }
        else if (invocationContext.HubMethodName == "SubscribeGlobal")
        {
            await access.RequireWorkspaceAsync(actor, invocationContext.Context.ConnectionAborted);
        }

        {
            var result1 = await next(invocationContext);
            return result1;
        }
    }
}

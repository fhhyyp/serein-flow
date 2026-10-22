using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace SereinFlow.Api.IntegrationTests;
internal sealed class LoopbackTestStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, continuation) =>
        {
            context.Connection.RemoteIpAddress ??= IPAddress.Loopback;
            {
                var result1 = continuation(context);
                return result1;
            }
        });
        next(app);
    };
}

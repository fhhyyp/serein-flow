using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SereinFlow.Client;

namespace SereinFlow.Client.DependencyInjection;

public static class SereinFlowClientServiceCollectionExtensions
{
    public static IServiceCollection AddSereinFlowClient(this IServiceCollection services, Action<SereinFlowClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var addOptions = services.AddOptions<SereinFlowClientOptions>();
        addOptions.Configure(configure);
        services.AddHttpClient(SereinFlowClientHttpClientName);
        services.TryAddSingleton<SereinFlowClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<SereinFlowClientOptions>>().Value;
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(SereinFlowClientHttpClientName);
            return new SereinFlowClient(httpClient, options);
        });
        services.TryAddSingleton<ISereinFlowClient>(serviceProvider =>
        {
            return serviceProvider.GetRequiredService<SereinFlowClient>();
        });
        return services;
    }

    public const string SereinFlowClientHttpClientName = "SereinFlow.SDK";
}

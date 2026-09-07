using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SereinFlow.Client;

namespace SereinFlow.Client.DependencyInjection;

public static class SereinFlowClientServiceCollectionExtensions
{
    public static IServiceCollection AddSereinFlowClient(
        this IServiceCollection services,
        Action<SereinFlowClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<SereinFlowClientOptions>().Configure(configure);
        services.AddHttpClient(SereinFlowClientHttpClientName);
        services.TryAddSingleton<SereinFlowClient>(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<SereinFlowClientOptions>>()
                .Value;
            var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            return new SereinFlowClient(factory.CreateClient(SereinFlowClientHttpClientName), options);
        });
        services.TryAddSingleton<ISereinFlowClient>(serviceProvider =>
            serviceProvider.GetRequiredService<SereinFlowClient>());
        return services;
    }

    public const string SereinFlowClientHttpClientName = "SereinFlow.SDK";
}

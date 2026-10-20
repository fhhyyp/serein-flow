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
        var addOptionsResult = services.AddOptions<SereinFlowClientOptions>();
        addOptionsResult.Configure(configure);
        services.AddHttpClient(SereinFlowClientHttpClientName);
        services.TryAddSingleton<SereinFlowClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<SereinFlowClientOptions>>().Value;
            var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var createClientResult = factory.CreateClient(SereinFlowClientHttpClientName);
            var sereinFlowClient = new SereinFlowClient(createClientResult, options);
            return sereinFlowClient;
        });
        services.TryAddSingleton<ISereinFlowClient>(serviceProvider =>
        {
            var getRequiredServiceResult = serviceProvider.GetRequiredService<SereinFlowClient>();
            return getRequiredServiceResult;
        });
        return services;
    }

    public const string SereinFlowClientHttpClientName = "SereinFlow.SDK";
}

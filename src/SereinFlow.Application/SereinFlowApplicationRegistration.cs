using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SereinFlow.Application;

public static class SereinFlowApplicationRegistration
{
    public static IServiceCollection AddSereinFlowApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IFileUploadSettings, FileUploadSettings>();
        services.AddScoped<RunApplicationService>();
        services.AddScoped<AiReadModelService>();
        services.AddScoped<ProjectArchiveService>();
        services.AddScoped<ProjectLibraryService>();
        services.AddScoped<ProjectCreationService>();
        services.AddScoped<FlowDefinitionWriteService>();
        services.AddSingleton<ILibraryCompatibilityAnalyzer, LibraryCompatibilityAnalyzer>();
        services.AddScoped<LibraryUpgradeService>();
        services.AddSingleton<IBuiltinNodeCatalog, BuiltinNodeCatalog>();
        services.AddScoped<FlowDiffService>();
        services.AddScoped<FlowPatchService>();
        services.AddScoped<FlowPatchContractNormalizer>();
        services.AddScoped<LibraryNodeTemplateService>();
        services.AddScoped<McpPreviewService>();
        services.AddScoped<McpIdempotencyService>();
        services.AddScoped<McpApiKeyManagementService>();

        return services;
    }
}

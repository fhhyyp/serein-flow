using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SqlSugar;
using SereinFlow.Application.Persistence;
using SereinFlow.Application;

namespace SereinFlow.Infrastructure.Persistence;

public static class SereinFlowInfrastructureRegistration
{
    public static IServiceCollection AddSereinFlowInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        var configuredDatabasePath = configuration["SereinFlow:DatabasePath"] ?? "data/sereinflow.db";
        var databasePath = Path.IsPathRooted(configuredDatabasePath)
            ? configuredDatabasePath
            : Path.Combine(contentRootPath, configuredDatabasePath);
        var database = new SqliteDatabase(new SqliteDatabaseOptions(databasePath));
        database.Initialize();

        services.AddSingleton(database);
        // SqlSugarClient contains mutable connection/transaction state. Use a
        // scoped client so concurrent API requests cannot share one ADO reader
        // or transaction and block event persistence/flow reads.
        // SqlSugarClient 包含可变连接/事务状态，必须按作用域创建，避免并发
        // API 请求共享 ADO 读取器或事务，进而阻塞事件持久化和流程读取。
        services.AddScoped<ISqlSugarClient>(_ => database.CreateClient());
        services.AddScoped(typeof(IRepository<>), typeof(SqlSugarRepository<>));
        services.AddScoped<IUnitOfWork, SqlSugarUnitOfWork>();
        // These adapters retain a SqliteDatabase compatibility constructor for
        // infrastructure tests.  Register the repository-backed constructor
        // explicitly so ASP.NET Core does not see two equally applicable
        // constructors when resolving the application abstractions.
        services.AddScoped<IProjectRepository>(serviceProvider =>
            new SqlSugarProjectRepository(
                serviceProvider.GetRequiredService<IRepository<ProjectRecord>>(),
                serviceProvider.GetRequiredService<IUnitOfWork>()));
        services.AddScoped<IFlowDefinitionRepository>(serviceProvider =>
            new SqlSugarFlowDefinitionRepository(
                serviceProvider.GetRequiredService<IRepository<FlowDefinitionRecord>>(),
                serviceProvider.GetRequiredService<IRepository<FlowDefinitionVersionRecord>>(),
                serviceProvider.GetRequiredService<IRepository<LibraryRecord>>(),
                serviceProvider.GetRequiredService<IRepository<FlowLibraryBindingRecord>>(),
                serviceProvider.GetRequiredService<IUnitOfWork>()));
        services.AddScoped<IFlowRunEventStore>(serviceProvider =>
            new SqlSugarFlowRunEventStore(
                serviceProvider.GetRequiredService<IRepository<FlowRunEventRecord>>(),
                serviceProvider.GetRequiredService<IUnitOfWork>()));
        services.AddScoped<IFlowRunOutputStore>(serviceProvider =>
            new SqlSugarFlowRunOutputStore(
                serviceProvider.GetRequiredService<IRepository<FlowRunOutputRecord>>(),
                serviceProvider.GetRequiredService<IUnitOfWork>()));
        services.AddScoped<IFlowRunStore>(serviceProvider =>
            new SqlSugarFlowRunStore(
                serviceProvider.GetRequiredService<IRepository<FlowRunRecord>>(),
                serviceProvider.GetRequiredService<IRepository<FlowRunDefinitionRecord>>(),
                serviceProvider.GetRequiredService<IRepository<LibraryRecord>>(),
                serviceProvider.GetRequiredService<IRepository<RunLibraryBindingRecord>>(),
                serviceProvider.GetRequiredService<IUnitOfWork>()));
        services.AddScoped<IFlowDebugSessionStore>(serviceProvider =>
            new SqlSugarFlowDebugSessionStore(
                serviceProvider.GetRequiredService<IRepository<FlowDebugSessionRecord>>()));
        services.AddScoped<IRunEnvironmentSettingsStore>(serviceProvider =>
            new SqlSugarRunEnvironmentSettingsStore(
                serviceProvider.GetRequiredService<IRepository<RunEnvironmentSettingsRecord>>()));
        services.AddScoped<IFlowInterfaceRepository>(serviceProvider =>
            new SqlSugarFlowInterfaceRepository(
                serviceProvider.GetRequiredService<IRepository<FlowInterfaceRecord>>()));
        services.AddScoped<IProjectLibraryReferenceRepository>(serviceProvider =>
            new SqlSugarProjectLibraryReferenceRepository(
                serviceProvider.GetRequiredService<IRepository<ProjectLibraryReferenceRecord>>()));
        services.AddScoped<ILibraryArtifactUsageStore>(serviceProvider =>
            new SqlSugarLibraryArtifactUsageStore(
                serviceProvider.GetRequiredService<IRepository<FlowDefinitionRecord>>(),
                serviceProvider.GetRequiredService<IRepository<FlowLibraryBindingRecord>>(),
                serviceProvider.GetRequiredService<IRepository<FlowRunRecord>>(),
                serviceProvider.GetRequiredService<IRepository<RunLibraryBindingRecord>>()));
        services.AddScoped<IFlowLibraryUpgradeStore>(serviceProvider =>
            new SqlSugarFlowLibraryUpgradeStore(
                serviceProvider.GetRequiredService<IRepository<LibraryUpgradePlanRecord>>(),
                serviceProvider.GetRequiredService<IRepository<FlowDefinitionRecord>>(),
                serviceProvider.GetRequiredService<IRepository<FlowDefinitionVersionRecord>>(),
                serviceProvider.GetRequiredService<IRepository<ProjectLibraryReferenceRecord>>(),
                serviceProvider.GetRequiredService<IRepository<LibraryRecord>>(),
                serviceProvider.GetRequiredService<IRepository<FlowLibraryBindingRecord>>(),
                serviceProvider.GetRequiredService<IUnitOfWork>()));

        var configuredLibraryDirectory = configuration["SereinFlow:LibraryDirectory"] ?? "data/libraries";
        var libraryDirectory = Path.IsPathRooted(configuredLibraryDirectory)
            ? configuredLibraryDirectory
            : Path.Combine(contentRootPath, configuredLibraryDirectory);
        services.AddScoped<ILibraryCatalogService>(serviceProvider =>
            new SqliteLibraryCatalogService(
                serviceProvider.GetRequiredService<IRepository<LibraryRecord>>(),
                serviceProvider.GetRequiredService<IRepository<LibraryFamilyRecord>>(),
                serviceProvider.GetRequiredService<IUnitOfWork>(),
                new LibraryCatalogOptions(libraryDirectory),
                serviceProvider.GetRequiredService<ILogger<SqliteLibraryCatalogService>>()));
        return services;
    }
}

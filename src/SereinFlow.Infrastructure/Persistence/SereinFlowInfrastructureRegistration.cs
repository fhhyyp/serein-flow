using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlSugar;
using SereinFlow.Application.Persistence;
using SereinFlow.Application;
using SereinFlow.Infrastructure.Configuration;

namespace SereinFlow.Infrastructure.Persistence;

public static class SereinFlowInfrastructureRegistration
{
    public static IServiceCollection AddSereinFlowStorage(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        var options = SereinFlowStorageOptions.FromConfiguration(configuration, contentRootPath);
        return services.AddSereinFlowStorage(options);
    }

    public static IServiceCollection AddSereinFlowStorage(this IServiceCollection services, SereinFlowStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton(options);
        return services.AddSereinFlowStorage();
    }

    public static IServiceCollection AddSereinFlowStorage(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IFileUploadSettings, FileUploadSettings>();
        services.TryAddSingleton(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var contentRootPath = serviceProvider.GetRequiredService<IHostEnvironment>().ContentRootPath;
            return SereinFlowStorageOptions.FromConfiguration(configuration, contentRootPath);
        });
        services.AddSingleton(serviceProvider =>
        {
            var sqliteDatabaseOptions = new SqliteDatabaseOptions(serviceProvider.GetRequiredService<SereinFlowStorageOptions>().DatabasePath);
            var database = new SqliteDatabase(sqliteDatabaseOptions);
            database.Initialize();
            return database;
        });
        services.AddSingleton<IFlowWorkpieceStore, FileFlowWorkpieceStore>();
        // SqlSugarClient contains mutable connection/transaction state. Use a
        // scoped client so concurrent API requests cannot share one ADO reader
        // or transaction and block event persistence/flow reads.
        // SqlSugarClient 包含可变连接/事务状态，必须按作用域创建，避免并发
        // API 请求共享 ADO 读取器或事务，进而阻塞事件持久化和流程读取。
        services.AddScoped<ISqlSugarClient>(serviceProvider =>
        {
            return serviceProvider.GetRequiredService<SqliteDatabase>().CreateClient();
        });
        services.AddScoped(typeof(IRepository<>), typeof(SqlSugarRepository<>));
        services.AddScoped<IUnitOfWork, SqlSugarUnitOfWork>();
        services.AddScoped<IFlowRunEventQueries, SqlSugarFlowRunEventQueries>();
        // These adapters retain a SqliteDatabase compatibility constructor for
        // infrastructure tests.  Register the repository-backed constructor
        // explicitly so ASP.NET Core does not see two equally applicable
        // constructors when resolving the application abstractions.
        // 这些适配器为基础设施测试保留 SqliteDatabase 兼容构造函数；显式注册基于仓储的构造函数，避免 ASP.NET Core 解析时出现歧义。
        services.AddScoped<IProjectRepository>(serviceProvider =>
        {
            var projects = serviceProvider.GetRequiredService<IRepository<ProjectRecord>>();
            var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
            return new SqlSugarProjectRepository(projects, unitOfWork);
        });
        services.AddScoped<SqlSugarFlowDefinitionRepository>(serviceProvider =>
        {
            var definitions = serviceProvider.GetRequiredService<IRepository<FlowDefinitionRecord>>();
            var versions = serviceProvider.GetRequiredService<IRepository<FlowDefinitionVersionRecord>>();
            var productionHeads = serviceProvider.GetRequiredService<IRepository<FlowProductionHeadRecord>>();
            var versionCounters = serviceProvider.GetRequiredService<IRepository<FlowVersionCounterRecord>>();
            var libraries = serviceProvider.GetRequiredService<IRepository<LibraryRecord>>();
            var libraryBindings = serviceProvider.GetRequiredService<IRepository<FlowLibraryBindingRecord>>();
            var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
            return new SqlSugarFlowDefinitionRepository(definitions, versions, productionHeads, versionCounters, libraries, libraryBindings, unitOfWork);
        });
        services.AddScoped<IFlowDefinitionRepository>(serviceProvider =>
        {
            return serviceProvider.GetRequiredService<SqlSugarFlowDefinitionRepository>();
        });
        services.AddScoped<IFlowVersionRepository>(serviceProvider =>
        {
            return serviceProvider.GetRequiredService<SqlSugarFlowDefinitionRepository>();
        });
        services.AddScoped<IFlowRunEventStore>(serviceProvider =>
        {
            var events = serviceProvider.GetRequiredService<IRepository<FlowRunEventRecord>>();
            var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
            var flowRunEventQueries = serviceProvider.GetRequiredService<IFlowRunEventQueries>();
            return new SqlSugarFlowRunEventStore(events, unitOfWork, flowRunEventQueries);
        });
        services.AddScoped<IFlowRunOutputStore>(serviceProvider =>
        {
            var outputs = serviceProvider.GetRequiredService<IRepository<FlowRunOutputRecord>>();
            var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
            return new SqlSugarFlowRunOutputStore(outputs, unitOfWork);
        });
        services.AddScoped<IFlowRunStore>(serviceProvider =>
        {
            var runs = serviceProvider.GetRequiredService<IRepository<FlowRunRecord>>();
            var definitions = serviceProvider.GetRequiredService<IRepository<FlowRunDefinitionRecord>>();
            var libraries = serviceProvider.GetRequiredService<IRepository<LibraryRecord>>();
            var libraryBindings = serviceProvider.GetRequiredService<IRepository<RunLibraryBindingRecord>>();
            var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
            return new SqlSugarFlowRunStore(runs, definitions, libraries, libraryBindings, unitOfWork);
        });
        services.AddScoped<IFlowDebugSessionStore>(serviceProvider =>
        {
            return new SqlSugarFlowDebugSessionStore(serviceProvider.GetRequiredService<IRepository<FlowDebugSessionRecord>>());
        });
        services.AddScoped<IRunEnvironmentSettingsStore>(serviceProvider =>
        {
            return new SqlSugarRunEnvironmentSettingsStore(serviceProvider.GetRequiredService<IRepository<RunEnvironmentSettingsRecord>>());
        });
        services.AddScoped<IFlowInterfaceRepository>(serviceProvider =>
        {
            return new SqlSugarFlowInterfaceRepository(serviceProvider.GetRequiredService<IRepository<FlowInterfaceRecord>>());
        });
        services.AddScoped<IProjectLibraryReferenceRepository>(serviceProvider =>
        {
            return new SqlSugarProjectLibraryReferenceRepository(serviceProvider.GetRequiredService<IRepository<ProjectLibraryReferenceRecord>>());
        });
        services.AddScoped<ILibraryArtifactUsageStore>(serviceProvider =>
        {
            var definitions = serviceProvider.GetRequiredService<IRepository<FlowDefinitionRecord>>();
            var flowBindings = serviceProvider.GetRequiredService<IRepository<FlowLibraryBindingRecord>>();
            var runs = serviceProvider.GetRequiredService<IRepository<FlowRunRecord>>();
            var runBindings = serviceProvider.GetRequiredService<IRepository<RunLibraryBindingRecord>>();
            return new SqlSugarLibraryArtifactUsageStore(definitions, flowBindings, runs, runBindings);
        });
        services.AddScoped<IFlowLibraryUpgradeStore>(serviceProvider =>
        {
            var plans = serviceProvider.GetRequiredService<IRepository<LibraryUpgradePlanRecord>>();
            var definitions = serviceProvider.GetRequiredService<IRepository<FlowDefinitionRecord>>();
            var versions = serviceProvider.GetRequiredService<IRepository<FlowDefinitionVersionRecord>>();
            var versionCounters = serviceProvider.GetRequiredService<IRepository<FlowVersionCounterRecord>>();
            var references = serviceProvider.GetRequiredService<IRepository<ProjectLibraryReferenceRecord>>();
            var libraries = serviceProvider.GetRequiredService<IRepository<LibraryRecord>>();
            var bindings = serviceProvider.GetRequiredService<IRepository<FlowLibraryBindingRecord>>();
            var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
            return new SqlSugarFlowLibraryUpgradeStore(plans, definitions, versions, versionCounters, references, libraries, bindings, unitOfWork);
        });
        services.AddScoped<IMcpPreviewStore>(serviceProvider =>
        {
            return new SqlSugarMcpPreviewStore(serviceProvider.GetRequiredService<IRepository<McpPreviewRecord>>());
        });
        services.AddScoped<IMcpAuditStore>(serviceProvider =>
        {
            return new SqlSugarMcpAuditStore(serviceProvider.GetRequiredService<IRepository<McpAuditRecord>>());
        });
        services.AddScoped<IMcpIdempotencyStore>(serviceProvider =>
        {
            return new SqlSugarMcpIdempotencyStore(serviceProvider.GetRequiredService<IRepository<McpIdempotencyRecord>>());
        });
        services.AddSingleton(serviceProvider =>
        {
            var rootPath = serviceProvider.GetRequiredService<SereinFlowStorageOptions>().McpPackageStagingDirectory;
            var fileUploadSettings = serviceProvider.GetRequiredService<IFileUploadSettings>();
            return new McpPackageStagingService(rootPath, fileUploadSettings: fileUploadSettings);
        });
        services.AddScoped<ILibraryCatalogService>(serviceProvider =>
        {
            var libraries = serviceProvider.GetRequiredService<IRepository<LibraryRecord>>();
            var families = serviceProvider.GetRequiredService<IRepository<LibraryFamilyRecord>>();
            var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
            var libraryCatalogOptions = new LibraryCatalogOptions(serviceProvider.GetRequiredService<SereinFlowStorageOptions>().LibraryDirectory);
            var logger = serviceProvider.GetRequiredService<ILogger<SqliteLibraryCatalogService>>();
            var fileUploadSettings = serviceProvider.GetRequiredService<IFileUploadSettings>();
            var changePublisher = serviceProvider.GetService<IWorkspaceChangePublisher>();
            return new SqliteLibraryCatalogService(libraries, families, unitOfWork, libraryCatalogOptions, logger, fileUploadSettings, changePublisher);
        });
        return services;
    }
}

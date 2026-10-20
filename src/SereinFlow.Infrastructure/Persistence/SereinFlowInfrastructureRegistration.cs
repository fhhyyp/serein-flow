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
        var addSereinFlowStorageResult = services.AddSereinFlowStorage(options);
        return addSereinFlowStorageResult;
    }

    public static IServiceCollection AddSereinFlowStorage(this IServiceCollection services, SereinFlowStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton(options);
        var addSereinFlowStorageResult1 = services.AddSereinFlowStorage();
        return addSereinFlowStorageResult1;
    }

    public static IServiceCollection AddSereinFlowStorage(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IFileUploadSettings, FileUploadSettings>();
        services.TryAddSingleton(serviceProvider =>
        {
            var getRequiredServiceResult = serviceProvider.GetRequiredService<IConfiguration>();
            var evaluatedValue = serviceProvider.GetRequiredService<IHostEnvironment>().ContentRootPath;
            var fromConfigurationResult = SereinFlowStorageOptions.FromConfiguration(getRequiredServiceResult, evaluatedValue);
            return fromConfigurationResult;
        });
        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<SereinFlowStorageOptions>();
            var sqliteDatabaseOptions = new SqliteDatabaseOptions(options.DatabasePath);
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
            var getRequiredServiceResult2 = serviceProvider.GetRequiredService<SqliteDatabase>();
            var createClientResult = getRequiredServiceResult2.CreateClient();
            return createClientResult;
        });
        services.AddScoped(typeof(IRepository<>), typeof(SqlSugarRepository<>));
        services.AddScoped<IUnitOfWork, SqlSugarUnitOfWork>();
        // These adapters retain a SqliteDatabase compatibility constructor for
        // infrastructure tests.  Register the repository-backed constructor
        // explicitly so ASP.NET Core does not see two equally applicable
        // constructors when resolving the application abstractions.
        services.AddScoped<IProjectRepository>(serviceProvider =>
        {
            var getRequiredServiceResult3 = serviceProvider.GetRequiredService<IRepository<ProjectRecord>>();
            var getRequiredServiceResult4 = serviceProvider.GetRequiredService<IUnitOfWork>();
            var sqlSugarProjectRepository = new SqlSugarProjectRepository(getRequiredServiceResult3, getRequiredServiceResult4);
            return sqlSugarProjectRepository;
        });
        services.AddScoped<SqlSugarFlowDefinitionRepository>(serviceProvider =>
        {
            var getRequiredServiceResult5 = serviceProvider.GetRequiredService<IRepository<FlowDefinitionRecord>>();
            var getRequiredServiceResult6 = serviceProvider.GetRequiredService<IRepository<FlowDefinitionVersionRecord>>();
            var getRequiredServiceResult7 = serviceProvider.GetRequiredService<IRepository<FlowProductionHeadRecord>>();
            var getRequiredServiceResult8 = serviceProvider.GetRequiredService<IRepository<FlowVersionCounterRecord>>();
            var getRequiredServiceResult9 = serviceProvider.GetRequiredService<IRepository<LibraryRecord>>();
            var getRequiredServiceResult10 = serviceProvider.GetRequiredService<IRepository<FlowLibraryBindingRecord>>();
            var getRequiredServiceResult11 = serviceProvider.GetRequiredService<IUnitOfWork>();
            var sqlSugarFlowDefinitionRepository = new SqlSugarFlowDefinitionRepository(getRequiredServiceResult5, getRequiredServiceResult6, getRequiredServiceResult7, getRequiredServiceResult8, getRequiredServiceResult9, getRequiredServiceResult10, getRequiredServiceResult11);
            return sqlSugarFlowDefinitionRepository;
        });
        services.AddScoped<IFlowDefinitionRepository>(serviceProvider =>
        {
            var getRequiredServiceResult12 = serviceProvider.GetRequiredService<SqlSugarFlowDefinitionRepository>();
            return getRequiredServiceResult12;
        });
        services.AddScoped<IFlowVersionRepository>(serviceProvider =>
        {
            var getRequiredServiceResult13 = serviceProvider.GetRequiredService<SqlSugarFlowDefinitionRepository>();
            return getRequiredServiceResult13;
        });
        services.AddScoped<IFlowRunEventStore>(serviceProvider =>
        {
            var getRequiredServiceResult14 = serviceProvider.GetRequiredService<IRepository<FlowRunEventRecord>>();
            var getRequiredServiceResult15 = serviceProvider.GetRequiredService<IUnitOfWork>();
            var sqlSugarFlowRunEventStore = new SqlSugarFlowRunEventStore(getRequiredServiceResult14, getRequiredServiceResult15);
            return sqlSugarFlowRunEventStore;
        });
        services.AddScoped<IFlowRunOutputStore>(serviceProvider =>
        {
            var getRequiredServiceResult16 = serviceProvider.GetRequiredService<IRepository<FlowRunOutputRecord>>();
            var getRequiredServiceResult17 = serviceProvider.GetRequiredService<IUnitOfWork>();
            var sqlSugarFlowRunOutputStore = new SqlSugarFlowRunOutputStore(getRequiredServiceResult16, getRequiredServiceResult17);
            return sqlSugarFlowRunOutputStore;
        });
        services.AddScoped<IFlowRunStore>(serviceProvider =>
        {
            var getRequiredServiceResult18 = serviceProvider.GetRequiredService<IRepository<FlowRunRecord>>();
            var getRequiredServiceResult19 = serviceProvider.GetRequiredService<IRepository<FlowRunDefinitionRecord>>();
            var getRequiredServiceResult20 = serviceProvider.GetRequiredService<IRepository<LibraryRecord>>();
            var getRequiredServiceResult21 = serviceProvider.GetRequiredService<IRepository<RunLibraryBindingRecord>>();
            var getRequiredServiceResult22 = serviceProvider.GetRequiredService<IUnitOfWork>();
            var sqlSugarFlowRunStore = new SqlSugarFlowRunStore(getRequiredServiceResult18, getRequiredServiceResult19, getRequiredServiceResult20, getRequiredServiceResult21, getRequiredServiceResult22);
            return sqlSugarFlowRunStore;
        });
        services.AddScoped<IFlowDebugSessionStore>(serviceProvider =>
        {
            var getRequiredServiceResult23 = serviceProvider.GetRequiredService<IRepository<FlowDebugSessionRecord>>();
            var sqlSugarFlowDebugSessionStore = new SqlSugarFlowDebugSessionStore(getRequiredServiceResult23);
            return sqlSugarFlowDebugSessionStore;
        });
        services.AddScoped<IRunEnvironmentSettingsStore>(serviceProvider =>
        {
            var getRequiredServiceResult24 = serviceProvider.GetRequiredService<IRepository<RunEnvironmentSettingsRecord>>();
            var sqlSugarRunEnvironmentSettingsStore = new SqlSugarRunEnvironmentSettingsStore(getRequiredServiceResult24);
            return sqlSugarRunEnvironmentSettingsStore;
        });
        services.AddScoped<IFlowInterfaceRepository>(serviceProvider =>
        {
            var getRequiredServiceResult25 = serviceProvider.GetRequiredService<IRepository<FlowInterfaceRecord>>();
            var sqlSugarFlowInterfaceRepository = new SqlSugarFlowInterfaceRepository(getRequiredServiceResult25);
            return sqlSugarFlowInterfaceRepository;
        });
        services.AddScoped<IProjectLibraryReferenceRepository>(serviceProvider =>
        {
            var getRequiredServiceResult26 = serviceProvider.GetRequiredService<IRepository<ProjectLibraryReferenceRecord>>();
            var sqlSugarProjectLibraryReferenceRepository = new SqlSugarProjectLibraryReferenceRepository(getRequiredServiceResult26);
            return sqlSugarProjectLibraryReferenceRepository;
        });
        services.AddScoped<ILibraryArtifactUsageStore>(serviceProvider =>
        {
            var getRequiredServiceResult27 = serviceProvider.GetRequiredService<IRepository<FlowDefinitionRecord>>();
            var getRequiredServiceResult28 = serviceProvider.GetRequiredService<IRepository<FlowLibraryBindingRecord>>();
            var getRequiredServiceResult29 = serviceProvider.GetRequiredService<IRepository<FlowRunRecord>>();
            var getRequiredServiceResult30 = serviceProvider.GetRequiredService<IRepository<RunLibraryBindingRecord>>();
            var sqlSugarLibraryArtifactUsageStore = new SqlSugarLibraryArtifactUsageStore(getRequiredServiceResult27, getRequiredServiceResult28, getRequiredServiceResult29, getRequiredServiceResult30);
            return sqlSugarLibraryArtifactUsageStore;
        });
        services.AddScoped<IFlowLibraryUpgradeStore>(serviceProvider =>
        {
            var getRequiredServiceResult31 = serviceProvider.GetRequiredService<IRepository<LibraryUpgradePlanRecord>>();
            var getRequiredServiceResult32 = serviceProvider.GetRequiredService<IRepository<FlowDefinitionRecord>>();
            var getRequiredServiceResult33 = serviceProvider.GetRequiredService<IRepository<FlowDefinitionVersionRecord>>();
            var getRequiredServiceResult34 = serviceProvider.GetRequiredService<IRepository<FlowVersionCounterRecord>>();
            var getRequiredServiceResult35 = serviceProvider.GetRequiredService<IRepository<ProjectLibraryReferenceRecord>>();
            var getRequiredServiceResult36 = serviceProvider.GetRequiredService<IRepository<LibraryRecord>>();
            var getRequiredServiceResult37 = serviceProvider.GetRequiredService<IRepository<FlowLibraryBindingRecord>>();
            var getRequiredServiceResult38 = serviceProvider.GetRequiredService<IUnitOfWork>();
            var sqlSugarFlowLibraryUpgradeStore = new SqlSugarFlowLibraryUpgradeStore(getRequiredServiceResult31, getRequiredServiceResult32, getRequiredServiceResult33, getRequiredServiceResult34, getRequiredServiceResult35, getRequiredServiceResult36, getRequiredServiceResult37, getRequiredServiceResult38);
            return sqlSugarFlowLibraryUpgradeStore;
        });
        services.AddScoped<IMcpApiKeyStore>(serviceProvider =>
        {
            var getRequiredServiceResult39 = serviceProvider.GetRequiredService<IRepository<McpApiKeyRecord>>();
            var sqlSugarMcpApiKeyStore = new SqlSugarMcpApiKeyStore(getRequiredServiceResult39);
            return sqlSugarMcpApiKeyStore;
        });
        services.AddScoped<IMcpPreviewStore>(serviceProvider =>
        {
            var getRequiredServiceResult40 = serviceProvider.GetRequiredService<IRepository<McpPreviewRecord>>();
            var sqlSugarMcpPreviewStore = new SqlSugarMcpPreviewStore(getRequiredServiceResult40);
            return sqlSugarMcpPreviewStore;
        });
        services.AddScoped<IMcpAuditStore>(serviceProvider =>
        {
            var getRequiredServiceResult41 = serviceProvider.GetRequiredService<IRepository<McpAuditRecord>>();
            var sqlSugarMcpAuditStore = new SqlSugarMcpAuditStore(getRequiredServiceResult41);
            return sqlSugarMcpAuditStore;
        });
        services.AddScoped<IMcpIdempotencyStore>(serviceProvider =>
        {
            var getRequiredServiceResult42 = serviceProvider.GetRequiredService<IRepository<McpIdempotencyRecord>>();
            var sqlSugarMcpIdempotencyStore = new SqlSugarMcpIdempotencyStore(getRequiredServiceResult42);
            return sqlSugarMcpIdempotencyStore;
        });
        services.AddSingleton(serviceProvider =>
        {
            var evaluatedValue43 = serviceProvider.GetRequiredService<SereinFlowStorageOptions>().McpPackageStagingDirectory;
            var getRequiredServiceResult44 = serviceProvider.GetRequiredService<IFileUploadSettings>();
            var mcpPackageStagingService = new McpPackageStagingService(evaluatedValue43, fileUploadSettings: getRequiredServiceResult44);
            return mcpPackageStagingService;
        });
        services.AddScoped<ILibraryCatalogService>(serviceProvider =>
        {
            var getRequiredServiceResult45 = serviceProvider.GetRequiredService<IRepository<LibraryRecord>>();
            var getRequiredServiceResult46 = serviceProvider.GetRequiredService<IRepository<LibraryFamilyRecord>>();
            var getRequiredServiceResult47 = serviceProvider.GetRequiredService<IUnitOfWork>();
            var evaluatedValue48 = serviceProvider.GetRequiredService<SereinFlowStorageOptions>().LibraryDirectory;
            var libraryCatalogOptions = new LibraryCatalogOptions(evaluatedValue48);
            var getRequiredServiceResult49 = serviceProvider.GetRequiredService<ILogger<SqliteLibraryCatalogService>>();
            var getRequiredServiceResult50 = serviceProvider.GetRequiredService<IFileUploadSettings>();
            var getServiceResult = serviceProvider.GetService<IWorkspaceChangePublisher>();
            var sqliteLibraryCatalogService = new SqliteLibraryCatalogService(getRequiredServiceResult45, getRequiredServiceResult46, getRequiredServiceResult47, libraryCatalogOptions, getRequiredServiceResult49, getRequiredServiceResult50, getServiceResult);
            return sqliteLibraryCatalogService;
        });
        return services;
    }
}

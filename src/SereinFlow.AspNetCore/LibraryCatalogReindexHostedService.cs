using SereinFlow.Application;

namespace SereinFlow.Api;

/// <summary>
/// Updates metadata for immutable library packages after the catalog schema
/// evolves, without delaying API startup or loading uploaded assemblies.
/// 类库目录结构升级后，异步更新不可变包的元数据；不阻塞 API 启动，也不加载上传程序集。
/// </summary>
public sealed class LibraryCatalogReindexHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<LibraryCatalogReindexHostedService> logger) : BackgroundService
{
    private static readonly Action<ILogger, int, Exception?> ReindexCompletedLog = LoggerMessage.Define<int>(
        LogLevel.Information,
        new EventId(1001, nameof(ReindexCompletedLog)),
        "Reindexed {LibraryCount} outdated library catalogs. 已完成过期类库目录重新索引。");
    private static readonly Action<ILogger, Exception?> ReindexFailedLog = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(1002, nameof(ReindexFailedLog)),
        "Library catalog background reindex failed. 类库目录后台重新索引失败。");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        try
        {
            using var scope = scopeFactory.CreateScope();
            var catalog = scope.ServiceProvider.GetRequiredService<ILibraryCatalogService>();
            var count = await catalog.ReindexOutdatedAsync(stoppingToken);
            if (count > 0)
            {
                ReindexCompletedLog(logger, count, null);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ReindexFailedLog(logger, exception);
        }
    }
}

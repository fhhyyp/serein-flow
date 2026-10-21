using Microsoft.Data.Sqlite;
using SqlSugar;

namespace SereinFlow.Infrastructure.Persistence;
/// <summary>
/// Configures sqlite database behavior.
/// 配置SqliteDatabase行为。
/// </summary>
public sealed record SqliteDatabaseOptions
{
    public SqliteDatabaseOptions(string databasePath, int busyTimeoutMilliseconds = 5000)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path cannot be empty. 数据库路径不能为空。", nameof(databasePath));
        }

        if (busyTimeoutMilliseconds < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(busyTimeoutMilliseconds), "Busy timeout must be positive. 忙等待超时时间必须为正数。");
        }

        DatabasePath = Path.GetFullPath(databasePath);
        BusyTimeoutMilliseconds = busyTimeoutMilliseconds;
    }

    public string DatabasePath { get; }
    public int BusyTimeoutMilliseconds { get; }
}

public sealed class SqliteDatabase : IDisposable
{
    private readonly SqliteDatabaseOptions _options;
    private bool _initialized;
    public SqliteDatabase(SqliteDatabaseOptions options)
    {
        _options = options;
        var directory = Path.GetDirectoryName(options.DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connectionConfig = new ConnectionConfig
        {
            ConnectionString = $"Data Source={options.DatabasePath}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false,
            InitKeyType = InitKeyType.Attribute
        };
        Client = new SqlSugarClient(connectionConfig);
        ConfigurePragmas();
    }

    public SqlSugarClient Client { get; }
    public string DatabasePath => _options.DatabasePath;
    public int BusyTimeoutMilliseconds => _options.BusyTimeoutMilliseconds;

    /// <summary>
    /// Creates a short-lived SqlSugar client for one DI scope. SqlSugar clients
    /// own mutable ADO state and must not be shared by concurrent HTTP requests.
    /// 创建一个供单个 DI 作用域使用的 SqlSugar 客户端。SqlSugar 客户端包含可变
    /// ADO 状态，不能在并发 HTTP 请求之间共享。
    /// </summary>
    public SqlSugarClient CreateClient()
    {
        var connectionConfig1 = new ConnectionConfig
        {
            ConnectionString = $"Data Source={DatabasePath}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        };
        var client = new SqlSugarClient(connectionConfig1);
        ConfigurePragmas(client);
        return client;
    }

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        var sqliteMigrator = new SqliteMigrator(Client);
        sqliteMigrator.Migrate();
        _initialized = true;
    }

    public int Execute(string sql, params SugarParameter[] parameters)
    {
        var executeCommandResult = Client.Ado.ExecuteCommand(sql, parameters);
        return executeCommandResult;
    }

    public IReadOnlyList<T> Query<T>(string sql, params SugarParameter[] parameters)
    {
        var sqlQueryResult = Client.Ado.SqlQuery<T>(sql, parameters);
        return sqlQueryResult;
    }

    public T Scalar<T>(string sql, params SugarParameter[] parameters)
    {
        var values = Client.Ado.SqlQuery<T>(sql, parameters);
        var evaluatedValue = values.FirstOrDefault() ?? default!;
        return evaluatedValue;
    }

    public void BackupTo(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            throw new ArgumentException("Backup path cannot be empty. 备份路径不能为空。", nameof(targetPath));
        }

        var fullTargetPath = Path.GetFullPath(targetPath);
        var directory = Path.GetDirectoryName(fullTargetPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var source = new SqliteConnection($"Data Source={DatabasePath}");
        using var destination = new SqliteConnection($"Data Source={fullTargetPath}");
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
    }

    public void Dispose()
    {
        Client.Dispose();
    }

    private void ConfigurePragmas()
    {
        ConfigurePragmas(Client);
    }

    private void ConfigurePragmas(SqlSugarClient client)
    {
        client.Ado.ExecuteCommand("PRAGMA foreign_keys = ON;");
        client.Ado.ExecuteCommand("PRAGMA journal_mode = WAL;");
        client.Ado.ExecuteCommand($"PRAGMA busy_timeout = {_options.BusyTimeoutMilliseconds};");
    }
}

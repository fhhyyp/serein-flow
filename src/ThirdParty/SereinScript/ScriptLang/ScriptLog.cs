using System.Text;
using SystemDebug = System.Diagnostics.Debug;

namespace ScriptLang;

/// <summary>
/// 脚本日志级别
/// </summary>
public enum ScriptLogLevel
{
    /// <summary>精简：仅错误和异常信息</summary>
    Concise,
    /// <summary>常规：错误 + 关键操作信息（编译统计、执行耗时、模块加载等）</summary>
    Normal,
    /// <summary>详细：所有调试信息（VM 执行追踪、Lambda 编译、闭包分析等）</summary>
    Verbose
}

/// <summary>
/// 脚本日志静态类
/// 统一管理编译时、运行时和环境异常的日志输出
/// </summary>
public static class ScriptLog
{
    private static readonly object _lock = new();

    /// <summary>
    /// 日志文件路径
    /// </summary>
    public static string LogFilePath { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "logs", "script.log");

    /// <summary>
    /// 是否写入日志文件
    /// </summary>
    public static bool IsWriteLogFile { get; set; } = false;

    /// <summary>
    /// 是否启用 Debug 输出到 System.Diagnostics.Debug
    /// </summary>
    public static bool IsPrintOnDebug { get; set; } = false;

    public static bool IsPrint { get; set; } = false;

    /// <summary>
    /// 当前日志级别，默认 Normal。
    /// Concise = 仅错误，Normal = 错误 + 关键操作信息，Verbose = 全部调试信息。
    /// </summary>
    public static ScriptLogLevel Level { get; set; } = ScriptLogLevel.Normal;

    /// <summary>
    /// 详细调试信息（仅在 Verbose 级别输出）
    /// </summary>
    public static void Debug(string message)
    {
        if (Level < ScriptLogLevel.Verbose)
            return;

        Write("DEBUG", message);

        if (IsPrint)
            Console.WriteLine(message);

        if (IsPrintOnDebug)
            SystemDebug.WriteLine(message);
    }

    /// <summary>
    /// 常规操作信息（在 Normal 及以上级别输出）
    /// </summary>
    public static void Info(string message)
    {
        if (Level < ScriptLogLevel.Normal)
            return;

        Write("INFO", message);

        if (IsPrint)
            Console.WriteLine(message);

        if (IsPrintOnDebug)
            SystemDebug.WriteLine(message);
    }

    /// <summary>
    /// 错误信息（始终输出）
    /// </summary>
    public static void Error(string message)
    {
        Write("ERROR", message);

        Console.Error.WriteLine(message);
        
        if (IsPrintOnDebug)
            SystemDebug.WriteLine(message);
    }

    /// <summary>
    /// 异常信息（始终输出）
    /// </summary>
    public static void Error(Exception exception)
    {
        var message = exception.ToString();

        Write("ERROR", message);

        Console.Error.WriteLine(message);

        if (IsPrintOnDebug)
            SystemDebug.WriteLine(message);
    }

    private static void Write(string level, string message)
    {
        if (!IsWriteLogFile)
            return;

        try
        {
            var dir = Path.GetDirectoryName(LogFilePath);

            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var line =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";

            lock (_lock)
            {
                File.AppendAllText(
                    LogFilePath,
                    line,
                    Encoding.UTF8);
            }
        }
        catch
        {
            // 日志系统自身异常不能影响业务运行
        }
    }
}
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SereinFlow.Contracts;
using SereinFlow.ScriptModules;
using ScriptLang;

namespace SereinFlow.ScriptAdapter;

public interface ISereinLangCompiler
{
    Task<ScriptCompileResultDto> CompileAsync(
        ScriptCompileRequestDto request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Compiles SereinLang for diagnostics only. It deliberately does not create a
/// task, execute bytecode, persist an artifact, or resolve arbitrary files.
/// 仅用于 SereinLang 诊断编译；不会创建任务、执行字节码、持久化产物或解析任意文件。
/// </summary>
public sealed class SereinLangCompiler : ISereinLangCompiler
{
    public const string SupportedLanguageVersion = "0.1";
    public const int MaxSourceBytes = 256 * 1024;
    public const int MaxInputs = 128;
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private static readonly SemaphoreSlim CompileGate = new(1, 1);

    private static readonly Regex LocationPattern = new(
        "第\\s*(?<line>\\d+)\\s*行.*?第\\s*(?<column>\\d+)\\s*列|line\\s*(?<lineEn>\\d+).*?column\\s*(?<columnEn>\\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public async Task<ScriptCompileResultDto> CompileAsync(
        ScriptCompileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stopwatch = Stopwatch.StartNew();
        var source = request.Source ?? string.Empty;
        var sourceName = NormalizeSourceName(request.SourceName);
        var languageVersion = request.LanguageVersion?.Trim() ?? string.Empty;
        var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();

        var validation = ValidateRequest(source, languageVersion, request.Inputs);
        if (validation.Count > 0)
        {
            stopwatch.Stop();
            return Result(false, sourceHash, sourceName, languageVersion, stopwatch.Elapsed, validation);
        }

        try
        {
            // CompileSource is synchronous and has no cancellation hook in the
            // language runtime. Run it on a worker and bound the MCP response;
            // the compiler itself performs no user-code execution or I/O.
            // CompileSource 当前是同步 API 且没有取消钩子，因此在工作线程上执行并限制
            // MCP 响应时间；编译器本身不会执行用户代码或进行 I/O。
            await CompileWithTimeoutAsync(source, sourceName, request.Inputs, cancellationToken);
            stopwatch.Stop();
            return Result(true, sourceHash, sourceName, languageVersion, stopwatch.Elapsed, []);
        }
        catch (TimeoutException)
        {
            stopwatch.Stop();
            return Result(false, sourceHash, sourceName, languageVersion, stopwatch.Elapsed,
                [new ScriptCompileDiagnosticDto(
                    "script.compile_timeout",
                    $"SereinLang compilation exceeded {DefaultTimeout.TotalSeconds:0} seconds. SereinLang 编译超过 {DefaultTimeout.TotalSeconds:0} 秒。",
                    "error",
                    sourceName)]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return Result(false, sourceHash, sourceName, languageVersion, stopwatch.Elapsed, ParseDiagnostics(exception, sourceName));
        }
    }

    private static async Task CompileWithTimeoutAsync(
        string source,
        string sourceName,
        IReadOnlyList<ScriptValueContractDto> inputs,
        CancellationToken cancellationToken)
    {
        await CompileGate.WaitAsync(cancellationToken);
        var compilation = Task.Run(
            () => Compile(source, sourceName, inputs),
            CancellationToken.None);
        var releaseInWorker = false;
        try
        {
            await compilation.WaitAsync(DefaultTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            releaseInWorker = true;
            _ = ReleaseCompileGateWhenFinishedAsync(compilation);
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            releaseInWorker = true;
            _ = ReleaseCompileGateWhenFinishedAsync(compilation);
            throw;
        }
        finally
        {
            if (!releaseInWorker)
                CompileGate.Release();
        }
    }

    private static async Task ReleaseCompileGateWhenFinishedAsync(Task compilation)
    {
        try
        {
            await compilation.ConfigureAwait(false);
        }
        catch
        {
            // The original request already returned the timeout/cancellation
            // result; the background compiler must not surface a second error.
        }
        finally
        {
            CompileGate.Release();
        }
    }

    private static void Compile(
        string source,
        string sourceName,
        IReadOnlyList<ScriptValueContractDto> inputs)
    {
        var engine = new ScriptEngine();
        // Register only the built-in SereinFlow module. The null runtime context
        // makes log/env unavailable for execution while keeping module names
        // visible to the compiler.
        SereinFlowScriptModuleRegistration.Register(
            engine,
            new SereinFlowScriptModuleContext(null, CancellationToken.None));
        _ = engine.CompileSource(source, sourceName, inputs.Select(static input => input.Name));
    }

    private static List<ScriptCompileDiagnosticDto> ValidateRequest(
        string source,
        string languageVersion,
        IReadOnlyList<ScriptValueContractDto>? inputs)
    {
        var diagnostics = new List<ScriptCompileDiagnosticDto>();
        if (Encoding.UTF8.GetByteCount(source) > MaxSourceBytes)
        {
            diagnostics.Add(new ScriptCompileDiagnosticDto(
                "script.source_too_large",
                $"SereinLang source cannot exceed {MaxSourceBytes} bytes. SereinLang 源码不能超过 {MaxSourceBytes} 字节。",
                "error"));
        }

        if (!string.Equals(languageVersion, SupportedLanguageVersion, StringComparison.Ordinal))
        {
            diagnostics.Add(new ScriptCompileDiagnosticDto(
                "script.language_version_unsupported",
                $"Only SereinLang version {SupportedLanguageVersion} is supported. 仅支持 SereinLang {SupportedLanguageVersion}。",
                "error"));
        }

        if (inputs is null || inputs.Count > MaxInputs)
        {
            diagnostics.Add(new ScriptCompileDiagnosticDto(
                "script.inputs_too_many",
                $"A script may declare at most {MaxInputs} inputs. 脚本最多声明 {MaxInputs} 个输入。",
                "error"));
        }
        else
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var input in inputs)
            {
                if (string.IsNullOrWhiteSpace(input.Name)
                    || !IsIdentifier(input.Name)
                    || !names.Add(input.Name))
                {
                    diagnostics.Add(new ScriptCompileDiagnosticDto(
                        "script.input_name_invalid",
                        $"Script input name '{input.Name}' must be a unique SereinLang identifier. 脚本输入名称必须是唯一的 SereinLang 标识符。",
                        "error"));
                }
            }
        }

        return diagnostics;
    }

    private static IReadOnlyList<ScriptCompileDiagnosticDto> ParseDiagnostics(Exception exception, string sourceName)
    {
        var message = exception.Message;
        var lines = message
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Take(32)
            .ToArray();
        if (lines.Length == 0)
            lines = ["SereinLang compilation failed. SereinLang 编译失败。"];

        return lines.Select((line, index) =>
        {
            var match = LocationPattern.Match(line);
            var lineNumber = match.Success
                ? int.Parse(match.Groups["line"].Success ? match.Groups["line"].Value : match.Groups["lineEn"].Value, CultureInfo.InvariantCulture)
                : (int?)null;
            var column = match.Success
                ? int.Parse(match.Groups["column"].Success ? match.Groups["column"].Value : match.Groups["columnEn"].Value, CultureInfo.InvariantCulture)
                : (int?)null;
            return new ScriptCompileDiagnosticDto(
                index == 0 ? "script.compile_failed" : "script.compile_diagnostic",
                line,
                "error",
                sourceName,
                lineNumber,
                column);
        }).ToArray();
    }

    private static ScriptCompileResultDto Result(
        bool success,
        string sourceHash,
        string sourceName,
        string languageVersion,
        TimeSpan duration,
        IReadOnlyList<ScriptCompileDiagnosticDto> diagnostics)
        => new(success, sourceHash, languageVersion, sourceName, duration, diagnostics);

    private static string NormalizeSourceName(string? sourceName)
    {
        var value = string.IsNullOrWhiteSpace(sourceName) ? "mcp-script.serein" : sourceName.Trim();
        return value.Length <= 256 ? value : value[..256];
    }

    private static bool IsIdentifier(string value)
        => !string.IsNullOrWhiteSpace(value)
            && value.All(static character => char.IsLetterOrDigit(character) || character == '_')
            && (char.IsLetter(value[0]) || value[0] == '_');
}

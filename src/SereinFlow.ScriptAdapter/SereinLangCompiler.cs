using System.Collections.Generic;
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
    Task<ScriptCompileResultDto> CompileAsync(ScriptCompileRequestDto request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Compiles SereinLang for diagnostics only. It deliberately does not create a
/// task, execute bytecode, persist an artifact, or resolve arbitrary files.
/// 仅用于 SereinLang 诊断编译；不会创建任务、执行字节码、持久化产物或解析任意文件。
/// </summary>
public sealed class SereinLangCompiler : ISereinLangCompiler
{
    private const int MaximumDiagnosticLines = 32;
    public const string SupportedLanguageVersion = "0.1";
    public const int MaxSourceBytes = 256 * 1024;
    public const int MaxInputs = 128;
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    private static readonly SemaphoreSlim CompileGate = new(1, 1);
    private static readonly Regex LocationPattern = new("第\\s*(?<line>\\d+)\\s*行.*?第\\s*(?<column>\\d+)\\s*列|line\\s*(?<lineEn>\\d+).*?column\\s*(?<columnEn>\\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    public async Task<ScriptCompileResultDto> CompileAsync(ScriptCompileRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stopwatch = Stopwatch.StartNew();
        var source = request.Source ?? string.Empty;
        var sourceName = NormalizeSourceName(request.SourceName);
        var languageVersion = request.LanguageVersion?.Trim() ?? string.Empty;
        var getBytesResult = Encoding.UTF8.GetBytes(source);
        var hashDataResult = SHA256.HashData(getBytesResult);
        var toHexStringResult = Convert.ToHexString(hashDataResult);
        var sourceHash = toHexStringResult.ToLowerInvariant();
        var validation = ValidateRequest(source, languageVersion, request.Inputs);
        if (validation.Count > 0)
        {
            stopwatch.Stop();
            var resultResult = Result(false, sourceHash, sourceName, languageVersion, stopwatch.Elapsed, validation);
            return resultResult;
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
            var resultResult1 = Result(true, sourceHash, sourceName, languageVersion, stopwatch.Elapsed, []);
            return resultResult1;
        }
        catch (TimeoutException)
        {
            stopwatch.Stop();
            IReadOnlyList<ScriptCompileDiagnosticDto> evaluatedValue = [new ScriptCompileDiagnosticDto(ScriptErrorCodes.CompileTimeout, $"SereinLang compilation exceeded {DefaultTimeout.TotalSeconds:0} seconds. SereinLang 编译超过 {DefaultTimeout.TotalSeconds:0} 秒。", "error", sourceName)];
            var resultResult2 = Result(false, sourceHash, sourceName, languageVersion, stopwatch.Elapsed, evaluatedValue);
            return resultResult2;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            var parseDiagnosticsResult = ParseDiagnostics(exception, sourceName);
            var resultResult3 = Result(false, sourceHash, sourceName, languageVersion, stopwatch.Elapsed, parseDiagnosticsResult);
            return resultResult3;
        }
    }

    private static async Task CompileWithTimeoutAsync(string source, string sourceName, IReadOnlyList<ScriptValueContractDto> inputs, CancellationToken cancellationToken)
    {
        await CompileGate.WaitAsync(cancellationToken);
        var compilation = Task.Run(() =>
        {
            Compile(source, sourceName, inputs);
        }, CancellationToken.None);
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
            {
                CompileGate.Release();
            }
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

    private static void Compile(string source, string sourceName, IReadOnlyList<ScriptValueContractDto> inputs)
    {
        var engine = new ScriptEngine();
        var sereinFlowScriptModuleContext = new SereinFlowScriptModuleContext(null, CancellationToken.None);
        // Register only the built-in SereinFlow module. The null runtime context
        // makes log/env unavailable for execution while keeping module names
        // visible to the compiler.
        SereinFlowScriptModuleRegistration.Register(engine, sereinFlowScriptModuleContext);
        var selectResult = inputs.Select(static input => input.Name);
        _ = engine.CompileSource(source, sourceName, selectResult);
    }

    private static List<ScriptCompileDiagnosticDto> ValidateRequest(string source, string languageVersion, IReadOnlyList<ScriptValueContractDto>? inputs)
    {
        var diagnostics = new List<ScriptCompileDiagnosticDto>();
        if (Encoding.UTF8.GetByteCount(source) > MaxSourceBytes)
        {
            var scriptCompileDiagnosticDto = new ScriptCompileDiagnosticDto(ScriptErrorCodes.SourceTooLarge, $"SereinLang source cannot exceed {MaxSourceBytes} bytes. SereinLang 源码不能超过 {MaxSourceBytes} 字节。", "error");
            diagnostics.Add(scriptCompileDiagnosticDto);
        }

        if (!string.Equals(languageVersion, SupportedLanguageVersion, StringComparison.Ordinal))
        {
            var scriptCompileDiagnosticDto4 = new ScriptCompileDiagnosticDto(ScriptErrorCodes.LanguageVersionUnsupported, $"Only SereinLang version {SupportedLanguageVersion} is supported. 仅支持 SereinLang {SupportedLanguageVersion}。", "error");
            diagnostics.Add(scriptCompileDiagnosticDto4);
        }

        if (inputs is null || inputs.Count > MaxInputs)
        {
            var scriptCompileDiagnosticDto5 = new ScriptCompileDiagnosticDto(ScriptErrorCodes.InputsTooMany, $"A script may declare at most {MaxInputs} inputs. 脚本最多声明 {MaxInputs} 个输入。", "error");
            diagnostics.Add(scriptCompileDiagnosticDto5);
        }
        else
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var input in inputs)
            {
                if (string.IsNullOrWhiteSpace(input.Name) || !IsIdentifier(input.Name) || !names.Add(input.Name))
                {
                    var scriptCompileDiagnosticDto6 = new ScriptCompileDiagnosticDto(ScriptErrorCodes.InputNameInvalid, $"Script input name '{input.Name}' must be a unique SereinLang identifier. 脚本输入名称必须是唯一的 SereinLang 标识符。", "error");
                    diagnostics.Add(scriptCompileDiagnosticDto6);
                }
            }
        }

        return diagnostics;
    }

    private static IReadOnlyList<ScriptCompileDiagnosticDto> ParseDiagnostics(Exception exception, string sourceName)
    {
        var message = exception.Message;
        var lines = message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static line =>
            {
                var evaluatedValue7 = !string.IsNullOrWhiteSpace(line);
                return evaluatedValue7;
            })
            .Take(MaximumDiagnosticLines)
            .ToArray();
        if (lines.Length == 0)
        {
            lines = ["SereinLang compilation failed. SereinLang 编译失败。"];
        }

        var toArrayResult = lines
            .Select((line, index) =>
            {
                var match = LocationPattern.Match(line);
                var lineNumber = match.Success ? int.Parse(match.Groups["line"].Success ? match.Groups["line"].Value : match.Groups["lineEn"].Value, CultureInfo.InvariantCulture) : (int?)null;
                var column = match.Success ? int.Parse(match.Groups["column"].Success ? match.Groups["column"].Value : match.Groups["columnEn"].Value, CultureInfo.InvariantCulture) : (int?)null;
                var scriptCompileDiagnosticDto8 = new ScriptCompileDiagnosticDto(index == 0 ? ScriptErrorCodes.CompileFailed : ScriptErrorCodes.CompileDiagnostic, line, "error", sourceName, lineNumber, column);
                return scriptCompileDiagnosticDto8;
            })
            .ToArray();
        return toArrayResult;
    }

    private static ScriptCompileResultDto Result(bool success, string sourceHash, string sourceName, string languageVersion, TimeSpan duration, IReadOnlyList<ScriptCompileDiagnosticDto> diagnostics)
    {
        ScriptCompileResultDto evaluatedValue10 = new(success, sourceHash, languageVersion, sourceName, duration, diagnostics);
        return evaluatedValue10;
    }

    private static string NormalizeSourceName(string? sourceName)
    {
        var value = string.IsNullOrWhiteSpace(sourceName) ? "mcp-script.serein" : sourceName.Trim();
        return value.Length <= 256 ? value : value[..256];
    }

    private static bool IsIdentifier(string value)
    {
        var evaluatedValue12 = !string.IsNullOrWhiteSpace(value) && value.All(static character =>
        {
            var evaluatedValue11 = char.IsLetterOrDigit(character) || character == '_';
            return evaluatedValue11;
        }) && (char.IsLetter(value[0]) || value[0] == '_');
        return evaluatedValue12;
    }
}

namespace SereinFlow.ScriptAdapter;
/// <summary>
/// Identifies the severity of a script diagnostic.
/// 标识脚本诊断的严重程度。
/// </summary>
public enum ScriptDiagnosticSeverity
{
    /// <summary>
    /// Indicates the error value.
    /// 表示“错误”这一取值。
    /// </summary>
    Error,
    /// <summary>
    /// Indicates the warning value.
    /// 表示“警告”这一取值。
    /// </summary>
    Warning
}

/// <summary>
/// Represents script execution diagnostic data used by SereinFlow.
/// 表示 SereinFlow 使用的脚本执行诊断数据。
/// </summary>
public sealed record ScriptExecutionDiagnostic(string Code, string Message, ScriptDiagnosticSeverity Severity = ScriptDiagnosticSeverity.Error, string? NodeId = null, string? SourceName = null);
public sealed class ScriptExecutionException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>
/// Captures the outcome of script compilation operations.
/// 记录脚本Compilation操作的结果。
/// </summary>
public sealed record ScriptCompilationResult(bool IsSuccess, ScriptLang.Runtime.ByteCode.ByteCodeChunk? Chunk, IReadOnlyList<ScriptExecutionDiagnostic> Diagnostics)
{
    public static ScriptCompilationResult Success(ScriptLang.Runtime.ByteCode.ByteCodeChunk chunk)
    {
        ScriptCompilationResult evaluatedValue = new(true, chunk, []);
        return evaluatedValue;
    }

    public static ScriptCompilationResult Failure(params ScriptExecutionDiagnostic[] diagnostics)
    {
        ScriptCompilationResult evaluatedValue1 = new(false, null, diagnostics);
        return evaluatedValue1;
    }
}

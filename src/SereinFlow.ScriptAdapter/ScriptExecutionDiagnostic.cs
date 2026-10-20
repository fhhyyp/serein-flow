namespace SereinFlow.ScriptAdapter;
public enum ScriptDiagnosticSeverity
{
    Error,
    Warning
}

public sealed record ScriptExecutionDiagnostic(string Code, string Message, ScriptDiagnosticSeverity Severity = ScriptDiagnosticSeverity.Error, string? NodeId = null, string? SourceName = null);
public sealed class ScriptExecutionException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

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

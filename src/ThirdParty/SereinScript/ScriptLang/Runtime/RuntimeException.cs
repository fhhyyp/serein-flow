using ScriptLang.Parser;

namespace ScriptLang.Runtime;

/// <summary>
/// 运行时异常
/// </summary>
public class RuntimeException : Exception
{
    public RuntimeException(string message) : base(message) { }
    public RuntimeException(Expr expr, string message) : base(message) { }
    //public RuntimeException(string message) : base(message) { }
}

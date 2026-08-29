namespace ScriptLang.Lexer;

/// <summary>
/// 词法单元
/// </summary>
public record Token(
    int TokenIndex,
    TokenType Type,
    string Lexeme,
    int StartIndex,   // 全文字符偏移
    int Length,
    object? Literal = null,
    string? FilePath = null,
    int Line = 1,
    int Column = 1
)
{
    public override string ToString()
    {
        string lexemeDisplay = Lexeme.Length > 0 ? Lexeme : "(empty)";
        return Literal != null 
            ? $"{Type}({lexemeDisplay}:{Literal})" 
            : $"{Type}({lexemeDisplay})";
    }
}

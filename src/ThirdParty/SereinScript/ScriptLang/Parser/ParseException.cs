using ScriptLang.Lexer;

namespace ScriptLang.Parser;

/// <summary>
/// 解析异常
/// </summary>
public class ParseException : Exception
{
    public Token Token { get; }
    public int Line { get; }
    public int Column { get; }
    
    public ParseException(Token token, string message) : base(message)
    {
        Token = token;
        Line = token.Line;
        Column = token.Column;
    }
    
    public override string ToString()
    {
        return Message;
    }
}

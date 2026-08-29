using System.Globalization;
using System.Text;

namespace ScriptLang.Lexer;

/// <summary>
/// 词法解析器提示
/// </summary>
/// <param name="Message"></param>
/// <param name="FilePath"></param>
/// <param name="Line"></param>
/// <param name="Column"></param>
/// <param name="Length"></param>
public record LexDiagnostic(
    string Message,
    string FilePath,
    int Line,
    int Column,
    int Length
);



/// <summary>
/// 词法分析器
/// </summary>
public class Lexer(string source, string filePath)
{
    private readonly string _source = source ?? throw new ArgumentNullException(nameof(source));
    private readonly List<Token> _tokens = [];
    private int _position = 0;
    private int _line = 1;
    private int _column = 1;

    public string? FilePath { get; set; } = filePath;

    /// <summary>
    /// 
    /// </summary>
    public List<LexDiagnostic> Diagnostics { get; } = [];

    /// <summary>
    /// 执行词法分析
    /// </summary>
    public List<Token> ScanTokens()
    {
        while (!IsAtEnd())
        {
            ScanToken();
        }
        
        _tokens.Add(new Token(_tokens.Count, TokenType.EOF, "",  _position, 0, null, FilePath, _line, _column));
        return _tokens;
    }
    
    private void ScanToken()
    {
        char c = Advance();
        
        // DEBUG: 打印当前字符和位置
        // Console.WriteLine($"DEBUG: char='{c}' pos={_position} line={_line} col={_column}");
        
        switch (c)
        {
            // 跳过空白字符
            case ' ':
            case '\r':
            case '\t':
                break;
            case '\n':
                _line++;
                _column = 0; // Will be incremented to 1 on next Advance
                break;
                
            // 单字符 Token
            case '(': AddToken(TokenType.LeftParen, "("); break;
            case ')': AddToken(TokenType.RightParen, ")"); break;
            case '{': AddToken(TokenType.LeftBrace, "{"); break;
            case '}': AddToken(TokenType.RightBrace, "}"); break;
            case '[': AddToken(TokenType.LeftBracket, "["); break;
            case ']': AddToken(TokenType.RightBracket, "]"); break;
            case ',': AddToken(TokenType.Comma, ","); break;
            case '.': AddToken(TokenType.Dot, "."); break;
            case ':': AddToken(TokenType.Colon, ":"); break;
            case ';': AddToken(TokenType.Semicolon, ";"); break;
            case '+': AddToken(TokenType.Plus, "+"); break;
            case '*': AddToken(TokenType.Star, "*"); break;
            case '%': AddToken(TokenType.Percent, "%"); break;
            case '!':
                if (Match('='))
                    AddToken(TokenType.BangEqual, "!=");
                else
                    AddToken(TokenType.Bang, "!");
                break;
            case '=':
                if (Match('='))
                    AddToken(TokenType.EqualEqual, "==");
                else if (Match('>'))
                    AddToken(TokenType.Arrow, "=>");
                else
                    AddToken(TokenType.Equal, "=");
                break;
            case '<':
                if (Match('='))
                    AddToken(TokenType.LessEqual, "<=");
                else
                    AddToken(TokenType.Less, "<");
                break;
            case '>':
                if (Match('='))
                    AddToken(TokenType.GreaterEqual, ">=");
                else
                    AddToken(TokenType.Greater, ">");
                break;
            case '-':
                if (Match('>'))
                    AddToken(TokenType.Arrow, "->");
                else
                    AddToken(TokenType.Minus, "-");
                break;
            case '/':
                // 注释支持
                if (Match('/'))
                {
                    // 单行注释 //
                    while (Peek() != '\n' && !IsAtEnd()) Advance();
                }
                else if (Match('*'))
                {
                    // 跨行注释 /* */
                    ReadBlockComment();
                }
                else
                {
                    AddToken(TokenType.Slash, "/");
                }
                break;
            case '&':
                if (Match('&'))
                    AddToken(TokenType.And, "&&");
                else
                    AddToken(TokenType.Unknown, "&");
                break;
            case '|':
                if (Match('|'))
                    AddToken(TokenType.Or, "||");
                else
                    AddToken(TokenType.Unknown, "|");
                break;
            case '?':
                if (Match('.'))
                    AddToken(TokenType.QuestionDot, "?.");
                else
                    AddToken(TokenType.Unknown, "?");
                break;
                
            // 字符串字面量
            case '"': ReadString(); break;
                
            // 字面量
            default:
                if (char.IsDigit(c))
                    ReadNumber(); // 解析数值
                else if (char.IsLetter(c) || c == '_' || c == '@')
                    ReadIdentifier();
                //else
                //    AddToken(TokenType.Unknown, c.ToString()); 
                else
                {
                    // 新增错误提示
                    Diagnostics.Add(new LexDiagnostic(
                        $"意外的字符 '{c}'",
                        FilePath ?? string.Empty,
                        _line,
                        _column - 1,
                        1
                    ));
                    AddToken(TokenType.Unknown, c.ToString());
                }
                break;
        }
    }

    private void ReadString()
    {
        int startLine = _line;
        int startColumn = _column;
        int startPos = _position;

        StringBuilder sb = new();

        while (!IsAtEnd() && Peek() != '"')
        {
            if (Peek() == '\n')
                break;

            if (Peek() == '\\' && PeekNext() != '\0')
            {
                Advance();
                sb.Append(Advance());
            }
            else
            {
                sb.Append(Advance());
            }
        }

        bool closed = !IsAtEnd() && Peek() == '"';
        if (closed)
            Advance();

        AddToken(TokenType.String, sb.ToString(), sb.ToString());

        if (!closed)
        {
            Diagnostics.Add(new LexDiagnostic(
                "未终止的字符串字面量",
                FilePath ?? string.Empty,
                startLine,
                startColumn,
                _position - startPos
            ));
        }
    }

    /// <summary>
    /// 读取跨行注释 /* ... */
    /// </summary>
    private void ReadBlockComment()
    {
        int startLine = _line;
        int startColumn = _column - 2; // 回退到 /*
        int startPos = _position - 2;

        while (!IsAtEnd())
        {
            if (Peek() == '*' && PeekNext() == '/')
            {
                Advance(); // *
                Advance(); // /
                return;    // 注释正常结束
            }

            if (Peek() == '\n')
            {
                _line++;
                _column = 0;
            }

            Advance();
        }

        // EOF reached without closing */
        Diagnostics.Add(new LexDiagnostic(
            "未终止的跨行注释（缺少 */）",
            FilePath ?? string.Empty,
            startLine,
            startColumn,
            _position - startPos
        ));
    }

    private void ReadString_v1()
    {
        StringBuilder sb = new();
        
        while (Peek() != '"' && !IsAtEnd())
        {
            if (Peek() == '\\' && PeekNext() != '\0')
            {
                Advance(); // 跳过反斜杠
                char next = Advance();
                sb.Append(next switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '"' => '"',
                    '\\' => '\\',
                    _ => next
                });
            }
            else
            {
                sb.Append(Advance());
            }
        }
        
        if (IsAtEnd())
            throw new Exception($"Unterminated string at line {_line}, column {_column}");
            
        Advance(); // 跳过结束引号
        
        AddToken(TokenType.String, sb.ToString(), sb.ToString());
    }

    [Obsolete("已废弃", true)]
    private void ReadNumber_t()
    {
        StringBuilder sb = new();
        
        // 第一个数字已经在 ScanToken 中被读取，需要回退
        _position--;
        _column--;
        if (_column < 1) _column = 1;
        
        while (char.IsDigit(Peek()))
        {
            sb.Append(Advance());
        }
        
        // 处理小数部分
        if (Peek() == '.' && char.IsDigit(PeekNext()))
        {
            sb.Append(Advance()); // 小数点
            while (char.IsDigit(Peek()))
            {
                sb.Append(Advance());
            }
        }
        
        string numberStr = sb.ToString();
        if (double.TryParse(numberStr, NumberStyles.Any, 
            CultureInfo.InvariantCulture, out double number))
        {
            AddToken(TokenType.Number_Double, numberStr, number);
        }
        else
        {
            AddToken(TokenType.Unknown, numberStr);
        }
    }

    #region 解析数值字面量
    private void ReadNumber()
    {
        StringBuilder sb = new();

        // 第一个数字已经在 ScanToken 中被读取，需要回退
        _position--;
        _column--;
        if (_column < 1) _column = 1;

        // bool hasDecimalPoint = false;
        bool isHexLiteral = false;

        // 检查是否是十六进制 (0x 或 0X)
        if (Peek() == '0' && (PeekNext() == 'x' || PeekNext() == 'X'))
        {
            sb.Append(Advance()); // '0'
            sb.Append(Advance()); // 'x' 或 'X'
            isHexLiteral = true;

            // 读取十六进制数字
            while (IsHexDigit(Peek()))
            {
                sb.Append(Advance());
            }
        }
        // 检查是否是二进制 (0b 或 0B)
        else if (Peek() == '0' && (PeekNext() == 'b' || PeekNext() == 'B'))
        {
            sb.Append(Advance()); // '0'
            sb.Append(Advance()); // 'b' 或 'B'

            // 读取二进制数字
            while (Peek() == '0' || Peek() == '1')
            {
                sb.Append(Advance());
            }
        }
        else
        {
            // 读取整数部分（十进制）
            while (char.IsDigit(Peek()))
            {
                sb.Append(Advance());
            }

            // 处理小数部分
            if (Peek() == '.' && char.IsDigit(PeekNext()))
            {
                // hasDecimalPoint = true;
                sb.Append(Advance()); // 小数点
                while (char.IsDigit(Peek()))
                {
                    sb.Append(Advance());
                }
            }
        }

        // 读取数值类型后缀
        string? suffix = ReadNumberSuffix();
        if (suffix != null)
        {
            sb.Append(suffix);
        }

        string numberStr = sb.ToString();
        string numberPart = isHexLiteral || numberStr.StartsWith("0b")
            ? numberStr
            : (suffix != null ? numberStr[..^suffix.Length] : numberStr);

        // 解析并创建对应类型的Token
        if (TryParseNumberWithSuffix(numberPart, suffix, out TokenType tokenType, out object? value))
        {
            AddToken(tokenType, numberStr, value);
        }
        else
        {
            AddToken(TokenType.Unknown, numberStr);
        }
        //var t = this._tokens.Last();
    }

    /// <summary>
    /// 读取数值类型后缀，返回后缀字符串（不含数字部分）
    /// </summary>
    private string? ReadNumberSuffix()
    {
        int startPos = _position;

        if (char.ToLower(Peek()) == 'u')
        {
            Advance(); // 'u' 或 'U'

            // 检查是否为 ul/UL
            if (char.ToLower(Peek()) == 'l')
            {
                Advance(); // 'l' 或 'L'
                return GetSuffixFromSource(startPos);
            }

            return GetSuffixFromSource(startPos);
        }

        if (char.ToLower(Peek()) == 'l')
        {
            Advance(); // 'l' 或 'L'
            return GetSuffixFromSource(startPos);
        }

        if (char.ToLower(Peek()) == 'f')
        {
            Advance(); // 'f' 或 'F'
            return GetSuffixFromSource(startPos);
        }

        if (char.ToLower(Peek()) == 'd')
        {
            Advance(); // 'd' 或 'D'
            return GetSuffixFromSource(startPos);
        }

        if (char.ToLower(Peek()) == 'm')
        {
            Advance(); // 'm' 或 'M'
            return GetSuffixFromSource(startPos);
        }

        return null;
    }

    /// <summary>
    /// 从源位置提取后缀字符串
    /// </summary>
    private string GetSuffixFromSource(int startPos)
    {
        // 这里假设 _source 是源代码字符串
        int length = _position - startPos;
        return _source.Substring(startPos, length);
    }

    /// <summary>
    /// 判断字符是否是十六进制数字
    /// </summary>
    private static bool IsHexDigit(char c)
    {
        return char.IsDigit(c) ||
               (c >= 'a' && c <= 'f') ||
               (c >= 'A' && c <= 'F');
    }

    /// <summary>
    /// 根据后缀尝试解析数值
    /// </summary>
    private static bool TryParseNumberWithSuffix(string numberStr, string? suffix,
        out TokenType tokenType, out object? value)
    {
        tokenType = TokenType.Unknown;
        value = null;

        try
        {
            // 处理十六进制
            if (numberStr.StartsWith("0x") || numberStr.StartsWith("0X"))
            {
                return TryParseHexNumber(numberStr, suffix, out tokenType, out value);
            }

            // 处理二进制
            if (numberStr.StartsWith("0b") || numberStr.StartsWith("0B"))
            {
                return TryParseBinaryNumber(numberStr, suffix, out tokenType, out value);
            }

            // 根据后缀类型解析
            switch (suffix?.ToLower())
            {
                case null: // 无后缀 - 自动推断
                    if (numberStr.Contains('.'))
                    {
                        // 浮点数默认为 double
                        if (double.TryParse(numberStr, NumberStyles.Any,
                            CultureInfo.InvariantCulture, out double d))
                        {
                            tokenType = TokenType.Number_Double;
                            value = d;
                            return true;
                        }
                    }
                    else
                    {
                        // 整数默认为 int，超出范围则为 long
                        if (int.TryParse(numberStr, NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out int i))
                        {
                            tokenType = TokenType.Number_Int;
                            value = i;
                            return true;
                        }
                        if (long.TryParse(numberStr, NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out long l))
                        {
                            tokenType = TokenType.Number_Long;
                            value = l;
                            return true;
                        }
                        // 超大整数回退到 double
                        if (double.TryParse(numberStr, NumberStyles.Any,
                            CultureInfo.InvariantCulture, out double bigNum))
                        {
                            tokenType = TokenType.Number_Double;
                            value = bigNum;
                            return true;
                        }
                    }
                    break;

                case "f":
                    if (float.TryParse(numberStr, NumberStyles.Any,
                        CultureInfo.InvariantCulture, out float f))
                    {
                        tokenType = TokenType.Number_Float;
                        value = f;
                        return true;
                    }
                    break;

                case "d":
                    if (double.TryParse(numberStr, NumberStyles.Any,
                        CultureInfo.InvariantCulture, out double dVal))
                    {
                        tokenType = TokenType.Number_Double;
                        value = dVal;
                        return true;
                    }
                    break;

                case "m":
                    if (decimal.TryParse(numberStr, NumberStyles.Any,
                        CultureInfo.InvariantCulture, out decimal m))
                    {
                        tokenType = TokenType.Number_Decimal;
                        value = m;
                        return true;
                    }
                    break;

                case "l":
                    if (long.TryParse(numberStr, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out long lVal))
                    {
                        tokenType = TokenType.Number_Long;
                        value = lVal;
                        return true;
                    }
                    break;

               /* case "u":
                    if (uint.TryParse(numberStr, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out uint uVal))
                    {
                        tokenType = TokenType.Number_UInt;
                        value = uVal;
                        return true;
                    }
                    break;

                case "ul":
                    if (ulong.TryParse(numberStr, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out ulong ulVal))
                    {
                        tokenType = TokenType.Number_ULong;
                        value = ulVal;
                        return true;
                    }
                    break;*/
            }
        }
        catch
        {
            // 解析失败
        }

        return false;
    }

    /// <summary>
    /// 解析十六进制数字
    /// </summary>
    private static bool TryParseHexNumber(string hexStr, string? suffix,
        out TokenType tokenType, out object? value)
    {
        tokenType = TokenType.Unknown;
        value = null;

        // 移除 0x 前缀
        string hexPart = hexStr[2..];

        try
        {
            long intValue = Convert.ToInt64(hexPart, 16);

            switch (suffix?.ToLower())
            {
                case null:
                    if (intValue <= int.MaxValue)
                    {
                        tokenType = TokenType.Number_Int;
                        value = (int)intValue;
                    }
                    else
                    {
                        tokenType = TokenType.Number_Long;
                        value = intValue;
                    }
                    return true;

                case "l":
                    tokenType = TokenType.Number_Long;
                    value = intValue;
                    return true;

               /* case "u":
                    tokenType = TokenType.Number_UInt;
                    value = (uint)intValue;
                    return true;

                case "ul":
                    tokenType = TokenType.Number_ULong;
                    value = (ulong)intValue;
                    return true;*/
            }
        }
        catch
        {
            // 解析失败
        }

        return false;
    }

    /// <summary>
    /// 解析二进制数字
    /// </summary>
    private static bool TryParseBinaryNumber(string binStr, string? suffix,
        out TokenType tokenType, out object? value)
    {
        tokenType = TokenType.Unknown;
        value = null;

        // 移除 0b 前缀
        string binPart = binStr[2..];

        try
        {
            long intValue = Convert.ToInt64(binPart, 2);

            switch (suffix?.ToLower())
            {
                case null:
                    if (intValue <= int.MaxValue)
                    {
                        tokenType = TokenType.Number_Int;
                        value = (int)intValue;
                    }
                    else
                    {
                        tokenType = TokenType.Number_Long;
                        value = intValue;
                    }
                    return true;

                case "l":
                    tokenType = TokenType.Number_Long;
                    value = intValue;
                    return true;

                /*case "u":
                    tokenType = TokenType.Number_UInt;
                    value = (uint)intValue;
                    return true;

                case "ul":
                    tokenType = TokenType.Number_ULong;
                    value = (ulong)intValue;
                    return true;*/
            }
        }
        catch
        {
            // 解析失败
        }

        return false;
    }
    #endregion

    private void ReadIdentifier()
    {
        StringBuilder sb = new();
        
        // 第一个字符已经在 ScanToken 中被读取，需要从 _source 中获取
        _position--; // 回退位置
        _column--;    // 回退列
        if (_column < 1) _column = 1;
        
        while (char.IsLetterOrDigit(Peek()) || Peek() == '_'/*|| Peek() == '@'*/)
        {
            sb.Append(Advance());
        }
        
        string lexeme = sb.ToString();
        TokenType type = lexeme switch
        {
            "let" => TokenType.Let,
            "var" => TokenType.Var,
            "if" => TokenType.If,
            "then" => TokenType.Then,
            "else" => TokenType.Else,
            "when" => TokenType.When,
            "for" => TokenType.For,
            "in" => TokenType.In,
            "return" => TokenType.Return,
            "from" => TokenType.From,
            "import" => TokenType.Import,
            "true" => TokenType.True,
            "false" => TokenType.False,
            "null" => TokenType.Null,
            "print" => TokenType.Identifier, // 内置函数，作为标识符
            _ => TokenType.Identifier
        };
        
        object? literal = type switch
        {
            TokenType.True => true,
            TokenType.False => false,
            TokenType.Null => null,
            _ => null
        };
        
        AddToken(type, lexeme, literal);
    }
    
    private void AddToken(TokenType type, string lexeme, object? literal = null)
    {
        int startColumn = _column - lexeme.Length;
        _tokens.Add(new Token(_tokens.Count, type, lexeme, _position, lexeme.Length, literal, FilePath, _line, startColumn < 1 ? 1 : startColumn));
    }
    
    private char Advance()
    {
        _column++;
        return _source[_position++];
    }
    
    private bool IsAtEnd() => _position >= _source.Length;
    
    private char Peek()
    {
        if (IsAtEnd()) return '\0';
        return _source[_position];
    }
    
    private char PeekNext()
    {
        if (_position + 1 >= _source.Length) return '\0';
        return _source[_position + 1];
    }
    
    private bool Match(char expected)
    {
        if (IsAtEnd() || _source[_position] != expected) return false;
        _position++;
        _column++;
        return true;
    }
}

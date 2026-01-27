using System.Data.Common;
using System.Numerics;

namespace Serein.Script
{
    /// <summary>
    /// Serein脚本词法分析器的Token类型
    /// </summary>
    internal enum TokenType
    {
        /// <summary>
        /// 预料之外的值
        /// </summary>
        Null,
        /// <summary>
        /// 标识符（变量）
        /// </summary>
        Identifier,
        /// <summary>
        /// 布尔
        /// </summary>
        Boolean,
        /// <summary>
        /// int 整数
        /// </summary>
        NumberInt,
        /// <summary>
        /// long 整数
        /// </summary>
        NumberLong,
        /// <summary>
        /// float 浮点数
        /// </summary>
        NumberFloat, 
        /// <summary>
        /// double 浮点数
        /// </summary>
        NumberDouble,
        /// <summary>
        /// 字符串
        /// </summary>
        String,
        /// <summary>
        /// 原始字符串（多行字符串）
        /// </summary>
        RawString,
        /// <summary>
        /// Char字符
        /// </summary>
        Char,
        /// <summary>
        /// 插值字符串
        /// </summary>
        InterpolatedString,
        /// <summary>
        /// 关键字
        /// </summary>
        Keyword,
        /// <summary>
        /// 操作符
        /// </summary>
        Operator,
        /// <summary>
        /// 左小括号
        /// </summary>
        ParenthesisLeft,
        /// <summary>
        /// 右小括号
        /// </summary>
        ParenthesisRight,
        /// <summary>
        /// 左中括号
        /// </summary>
        SquareBracketsLeft,
        /// <summary>
        /// 右中括号
        /// </summary>
        SquareBracketsRight,
        /// <summary>
        /// 左大括号
        /// </summary>
        BraceLeft,
        /// <summary>
        /// 右大括号
        /// </summary>
        BraceRight,
        /// <summary>
        /// 点号
        /// </summary>
        Dot,
        /// <summary>
        /// 逗号
        /// </summary>
        Comma,

        /// <summary>
        /// 分号
        /// </summary>
        Semicolon,

        /// <summary>
        /// 行注释
        /// </summary>
        // RowComment,

        /// <summary>
        /// 解析完成
        /// </summary>
        EOF
    }

    /// <summary>
    /// Serein脚本词法分析器的Token结构体
    /// </summary>
    internal ref struct Token
    {
        public TokenType Type { get; }
        public string Value { get; }

        public int Row { get; set; }
        public string Code { get; set; }
        public int StartIndex { get; set; }
        public int Length { get; set; }

        internal Token(TokenType type, string value)
        {
            Type = type;
            Value = value;
        }

        public override string ToString()
        {
            return $"token in {Row} row, type is \"{Type}\", value is \"{Value}\"";
        }
    }


    /// <summary>
    /// Serein脚本词法分析器
    /// </summary>
    internal ref struct SereinScriptLexer
    {
        private readonly ReadOnlySpan<char> _input;
        private int _index;
        private int _row ;

        /// <summary>
        /// 关键字，防止声明为变量
        /// </summary>
        private string[] _keywords = [
            "let",
            "func", 
            "if", 
            "else", 
            "return",
            "while",
            "new",
            "class",
            "using",
            ];

        internal SereinScriptLexer(string input)
        {
            _input = input.AsSpan();
            _index = 0;
        }


        internal Token PeekToken(int count = 1)
        {
            if (count < 0) throw new Exception() ;
            int currentIndex = _index;  // 保存当前索引
            var currentRow = _row; // 保存当前行数
            Token nextToken = new Token(); ;
            for (var i = 0; i < count; i++)
            {
                nextToken = NextToken();  // 获取下一个 token
            }
            _index = currentIndex;  // 恢复索引到当前位置
            _row = currentRow; // 恢复到当前行数
            return nextToken;  // 返回下一个 token
        }

        /// <summary>
        /// 重置Lexer
        /// </summary>
        public void Reset()
        {
            this._row = 0;
            this._index = 0;
        }

        /// <summary>
        /// 根据 token 重置Lexer
        /// </summary>
        /// <param name="token"></param>
        public void SetToken(Token token)
        {
            this._row = token.Row;
            this._index = token.StartIndex;
        }

        internal Token NextToken()
        {
            
            // 跳过空白字符
            while (_index < _input.Length && char.IsWhiteSpace(_input[_index]))
            {
                if (_input[_index] == '\n')
                {
                    _row++;
                }

                _index++;
            }

            
            if (_index >= _input.Length) return new Token(TokenType.EOF, string.Empty); // 程序结束

            char currentChar = _input[_index];

            // 识别字符串字面量
            if (currentChar == '"')
            {
                if (_input[_index + 1] == '"'
                     && _input[_index + 2] == '"')
                {
                    //var value = _input.Slice(_index, 4).ToString();

                    // 原始字符串
                    return ReadRawString();
                }
                return ReadString();
            }
            

            if (currentChar == '\'')
            {
                if (_input[_index + 2] == '\'')
                {

                    return ReadChar();
                }
                else
                {
                    throw new Exception($"not is char: {currentChar},in Line.{_row}.");
                }
            }


            // 跳过注释
            if (_input[_index] == '/' && _input[_index + 1] == '/')
            {
                // 一直识别到换行符的出现
                while (_index < _input.Length && _input[_index] != '\n')
                {
                    _index++;
                }
                return NextToken(); // 跳过注释后，返回下一个识别token
            }

            // 识别null字面量
            if (currentChar == 'n')
            {
                if (_input[_index + 1] == 'u'
                    && _input[_index + 2] == 'l'
                    && _input[_index + 3] == 'l')
                {
                    var value = _input.Slice(_index, 4).ToString();

                    return CreateToken(TokenType.Null, "null");
                }
            }

            // 识别布尔字面量
            if (currentChar == 't')
            {
                if (_input[_index + 1] == 'r'
                    && _input[_index + 2] == 'u'
                    && _input[_index + 3] == 'e')
                {
                    return CreateToken(TokenType.Boolean, "true");
                }
            }
            else if (currentChar == 'f')
            {
                if (_input[_index + 1] == 'a'
                    && _input[_index + 2] == 'l'
                    && _input[_index + 3] == 's'
                    && _input[_index + 4] == 'e')
                {
                    return CreateToken(TokenType.Boolean, "false");
                }
            }


            // 识别数字
            if (char.IsDigit(currentChar))
            {
                #region 数值分析
                if (char.IsDigit(currentChar))
                {
                    var start = _index;
                    bool hasDot = false;
                    //bool hasSuffix = false;

                    while (_index < _input.Length)
                    {
                        var ch = _input[_index];

                        if (char.IsDigit(ch))
                        {
                            _index++;
                        }
                        else if (ch == '.' && !hasDot)
                        {
                            hasDot = true;
                            _index++;
                        }
                        else if (ch is 'f' or 'F' or 'd' or 'D' or 'l' or 'L')
                        {
                            //hasSuffix = true;
                            _index++;
                            break; // 后缀后应结束
                        }
                        else
                        {
                            break;
                        }
                    }

                    var raw = _input.Slice(start, _index - start).ToString();
                    _index = start; // 回退索引，仅 CreateToken 负责推进

                    TokenType type;

                    // 判断类型
                    if (hasDot)
                    {
                        if (raw.EndsWith("f", StringComparison.OrdinalIgnoreCase))
                            type = TokenType.NumberFloat;
                        else if (raw.EndsWith("d", StringComparison.OrdinalIgnoreCase))
                            type = TokenType.NumberDouble;
                        else
                            type = TokenType.NumberDouble; // 默认小数为 double
                    }
                    else
                    {
                        if (raw.EndsWith("l", StringComparison.OrdinalIgnoreCase))
                            type = TokenType.NumberLong;
                        else
                        {
                            // 自动根据位数判断 int 或 long
                            if (long.TryParse(raw, out var val))
                            {
                                if (val >= int.MinValue && val <= int.MaxValue)
                                    type = TokenType.NumberInt;
                                else
                                    type = TokenType.NumberLong;
                            }
                            else
                            {
                                type = TokenType.NumberLong; // 超出 long 会出错，默认成 long
                            }
                        }
                    }

                    return CreateToken(type, raw);
                } 
                #endregion
            }

            // 识别标识符（变量名、关键字）
            if (char.IsLetter(currentChar))
            {
                var start = _index;
                while (_index < _input.Length && (char.IsLetterOrDigit(_input[_index]) || _input[_index] == '_'))
                    _index++;
                var value = _input.Slice(start, _index - start).ToString();
                _index = start;  // 回退索引，索引必须只能在 CreateToken 方法内更新
                return CreateToken(_keywords.Contains(value) ? TokenType.Keyword : TokenType.Identifier, value);

            }

            // 识别符号
            switch (currentChar)
            {
                case '(': return CreateToken(TokenType.ParenthesisLeft, "(");
                case ')': return CreateToken(TokenType.ParenthesisRight, ")");
                case '[': return CreateToken(TokenType.SquareBracketsLeft, "[");
                case ']': return CreateToken(TokenType.SquareBracketsRight, "]");
                case '{': return CreateToken(TokenType.BraceLeft, "{");
                case '}': return CreateToken(TokenType.BraceRight, "}");
                case ',': return CreateToken(TokenType.Comma, ",");
                case ';': return CreateToken(TokenType.Semicolon, ";");
                case '+':
                case '-':
                case '*':
                case '/':
                     return CreateToken(TokenType.Operator, currentChar.ToString());
                case '>': // 识别 ">" 或 ">="
                    if (_index + 1 < _input.Length && _input[_index + 1] == '=')
                    {
                        return CreateToken(TokenType.Operator, ">=");
                    }
                    return CreateToken(TokenType.Operator, ">");
                case '<': // 识别 "<" 或 "<="
                    if (_index + 1 < _input.Length && _input[_index + 1] == '=')
                    {
                        return CreateToken(TokenType.Operator, "<=");
                    }
                    return CreateToken(TokenType.Operator, "<");
                case '!': // 识别 "!="
                    if (_index + 1 < _input.Length && _input[_index + 1] == '=')
                    {
                        return CreateToken(TokenType.Operator, "!=");
                    }
                    break;
                case '=': // 识别 "=="
                    if (_index + 1 < _input.Length && _input[_index + 1] == '=')
                    {
                        return CreateToken(TokenType.Operator, "==");
                    }
                    else
                    {
                        return CreateToken(TokenType.Operator, "=");
                    }
                case '.':
                    return CreateToken(TokenType.Dot, ".");

                //case '$':
                //    return CreateToken(TokenType.InterpolatedString, "$");  
            }

            throw new Exception("Unexpected character: " + currentChar);
        }

        /// <summary>
        /// 创建一个新的Token实例
        /// </summary>
        /// <param name="tokenType"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private Token CreateToken(TokenType tokenType, string value)
        {
            var code = GetLine(_row).ToString();
            var token = new Token(tokenType, value)
            {
                Row = _row,
                StartIndex = _index,
                Length = value.Length,
                Code = code,
            };
            _index += value.Length;

            return token;
        }

        private Token ReadRawString()
        {
            int startLine = _row;
            _index += 3; // 跳过开头 """
            int index = _index;
            var contentStart = index;
            while (index + 2 < _input.Length)
            {
                char current = _input[index];

                // 行号处理
                if (current == '\n')
                {
                    _row++;
                }

                // 检查是否是结束符 """
                if (_input[index] != '"' || _input[index + 1] != '"' || _input[index + 2] != '"')
                {
                    index++;
                }
                else
                {
                    var value = _input.Slice(contentStart, index - contentStart).ToString();
                    _index += 3;
                    // 构建带行号信息的 Token（假设 Token 有 StartLine 属性）
                    return CreateToken(TokenType.RawString, value);
                }

            }

            throw new Exception($"Unterminated raw string literal starting at line {startLine}");
        }

        /// <summary>
        /// 读取硬编码的文本
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private Token ReadString()
        {
            _index++;  // 跳过开头的引号
            var start = _index;

            while (_index < _input.Length && _input[_index] != '"')
            {
                if (_input[_index] == '\\' && _index + 1 < _input.Length && (_input[_index + 1] == '"' || _input[_index + 1] == '\\'))
                {
                    // 处理转义字符
                    _index++;
                }
                _index++;
            }

            if (_index >= _input.Length) throw new Exception("Unterminated string literal");

            var value = _input.Slice(start, _index - start).ToString();
            // var value = _input.Substring(start, _index - start);

            _index = start + 1; // 跳过引号
            return CreateToken(TokenType.String, value);

            // _index++;  // 跳过结束的引号
            //return new Token(TokenType.String, value.ToString());
        }

        /// <summary>
        /// 读取硬编码的Char字符
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private Token ReadChar()
        {
            _index++;  // 跳过开头的引号
            var start = _index;
            var cahrValue = _input.Slice(start, 1).ToString();
            _index++; // 跳过Char字符串后的引号
            return CreateToken(TokenType.Char, cahrValue);

            // _index++;  // 跳过结束的引号
            //return new Token(TokenType.String, value.ToString());
        }

        /// <summary>
        /// 获取对应行的代码文本
        /// </summary>
        /// <param name="lineNumber"></param>
        /// <returns></returns>
        private  ReadOnlySpan<char> GetLine( int lineNumber)
        {
            ReadOnlySpan<char> text = _input;
            int currentLine = 0;
            int start = 0;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')  // 找到换行符
                {
                    if (currentLine == lineNumber)
                    {
                        return text.Slice(start, i - start);  // 返回从start到当前位置的行文本
                    }
                    currentLine++;
                    start = i + 1;  // 下一行的起始位置
                }
            }

            // 如果没有找到指定行，返回空的Span
            return ReadOnlySpan<char>.Empty;
        }

        public int GetIndex()
        {
            return _index;
        }
        public string GetCoreContent(int index)
        {
            ReadOnlySpan<char> text = _input;
            var content = text.Slice(index, _index - index);  // 返回从start到当前位置的行文本
            return content.ToString();
        }
        
    }
    
}

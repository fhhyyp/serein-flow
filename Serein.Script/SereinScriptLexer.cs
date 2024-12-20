using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Serein.Script
{
    internal enum TokenType
    {
        /// <summary>
        /// 预料之外的值
        /// </summary>
        Null,
        /// <summary>
        /// 标识符
        /// </summary>
        Identifier,
        /// <summary>
        /// 布尔
        /// </summary>
        Boolean,
        /// <summary>
        /// 数值
        /// </summary>
        Number,
        /// <summary>
        /// 字符串
        /// </summary>
        String,
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
    }

    internal ref struct SereinScriptLexer
    {
        private readonly ReadOnlySpan<char> _input;
        private int _index;
        private int _row ;


        private string[] _keywords = [
            "let",
            "func",
            "if",
            "else",
            "return",
            "while",
            "new",
            "class",
            ];

        internal SereinScriptLexer(string input)
        {
            _input = input.AsSpan();
            _index = 0;
        }


        internal Token PeekToken()
        {
            int currentIndex = _index;  // 保存当前索引
            Token nextToken = NextToken();  // 获取下一个 token
            _index = currentIndex;  // 恢复索引到当前位置
            return nextToken;  // 返回下一个 token
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

            

            if (_index >= _input.Length) return new Token(TokenType.EOF, string.Empty);

            char currentChar = _input[_index];

            // 识别字符串字面量
            if (currentChar == '"')
            {
                return ReadString();
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
                var start = _index;
                while (_index < _input.Length && char.IsDigit(_input[_index]))
                    _index++;
                var value = _input.Slice(start, _index - start).ToString();
                _index = start; // 回退索引，索引必须只能在 CreateToken 方法内更新
                return CreateToken(TokenType.Number, value);
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
            }

            throw new Exception("Unexpected character: " + currentChar);
        }

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


        
    }
    
}

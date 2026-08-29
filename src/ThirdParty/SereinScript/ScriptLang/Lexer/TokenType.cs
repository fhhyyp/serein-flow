namespace ScriptLang.Lexer;

/// <summary>
/// Token 类型枚举
/// </summary>
public enum TokenType
{
    // 单字符 Token
    /// <summary>token 是 `(`</summary>
    LeftParen,
    /// <summary>token 是 `)`</summary>
    RightParen,
    /// <summary>token 是 `{`</summary>
    LeftBrace,
    /// <summary>token 是 `}`</summary>
    RightBrace,
    /// <summary>token 是 `[`</summary>
    LeftBracket,
    /// <summary>token 是 `]`</summary>
    RightBracket,
    /// <summary>token 是 `,`</summary>
    Comma,
    /// <summary>token 是 `.`</summary>
    Dot,
    /// <summary>token 是 `?.`</summary>
    QuestionDot,
    /// <summary>token 是 `:`</summary>
    Colon,
    /// <summary>token 是 `;`</summary>
    Semicolon,

    // 运算符
    /// <summary>token 是 `+`</summary>
    Plus,
    /// <summary>token 是 `-`</summary>
    Minus,
    /// <summary>token 是 `*`</summary>
    Star,
    /// <summary>token 是 `/`</summary>
    Slash,
    /// <summary>token 是 `%`</summary>
    Percent,

    /// <summary>token 是 `=`</summary>
    Equal,              // =
    /// <summary>token 是 `==`</summary>
    EqualEqual,
    /// <summary>token 是 `!=`</summary>
    BangEqual,

    /// <summary>token 是 `&lt;`</summary>  
    Less,
    /// <summary>token 是 `&lt;=`</summary>  
    LessEqual,
    /// <summary>token 是 `&gt;`</summary>
    Greater,            // >
    /// <summary>token 是 `&gt;=`</summary>
    GreaterEqual,

    /// <summary>token 是 `!`</summary>
    Bang,
    /// <summary>token 是 `&&`</summary>
    And,
    /// <summary>token 是 `||`</summary>
    Or,

    /// <summary>token 是 `=>`</summary>
    Arrow,

    // 关键字
    /// <summary>token 是 `let`</summary>
    Let,
    /// <summary>token 是 `var`</summary>
    Var,
    /// <summary>token 是 `if`</summary>
    If,
    /// <summary>token 是 `then`</summary>
    Then,
    /// <summary>token 是 `else`</summary>
    Else,
    /// <summary>token 是 `when`</summary>
    When,
    /// <summary>token 是 `for`</summary>
    For,
    /// <summary>token 是 `in`</summary>
    In,
    /// <summary>token 是 `return`</summary>
    Return,

    // 模块导入
    /// <summary>token 是 `import`</summary>
    Import,
    /// <summary>token 是 `from`</summary>
    From,

    // 字面量
    /// <summary>token 是 数值型</summary>
    Number_Int,
    /// <summary>token 是 数值型</summary>
    Number_Long,       // 42L
    /// <summary>token 是 数值型</summary>
    Number_Float,      // 3.14f
    /// <summary>token 是 数值型</summary>
    Number_Double,     // 3.14 或 3.14d
    /// <summary>token 是 数值型</summary>
    Number_Decimal,    // 10.5m
    /// <summary>token 是 数值型</summary>
    /*Number_UInt,       // 100u
    /// <summary>token 是 数值型</summary>
    Number_ULong,      // 100ul*/


    /// <summary>token 值是字符串 `hello`</summary>
    String,
    /// <summary>token 值是布尔值 `true`</summary>
    True,
    /// <summary>token 值是布尔值 `false`</summary>
    False,
    /// <summary>token 值是 null</summary>
    Null,

    // 标识符
    /// <summary>token 是一个标识符，其值为变量名、成员名或方法名</summary>
    Identifier,

    // 特殊
    /// <summary>文件结束</summary>
    EOF,
    /// <summary>未知 token</summary>
    Unknown
}

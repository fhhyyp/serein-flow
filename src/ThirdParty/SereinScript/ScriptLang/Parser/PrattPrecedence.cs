namespace ScriptLang.Parser;

/// <summary>
/// 运算符优先级定义（用于 Pratt Parser）
/// </summary>
public static class PrattPrecedence
{
    public const int None = 0;
    public const int Assignment = 1;       // =
    public const int Or = 2;               // ||
    public const int And = 3;              // &&
    public const int Equality = 4;         // ==, !=
    public const int Comparison = 5;       // <, >, <=, >=
    public const int Term = 6;             // +, -
    public const int Factor = 7;           // *, /, %
    public const int Unary = 8;            // !, -
    public const int Call = 9;             // ., ?. , (), []
    public const int Primary = 10;
}

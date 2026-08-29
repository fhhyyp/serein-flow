namespace ScriptLang.Parser;

/// <summary>
/// Expr在源代码中的Token信息
/// </summary>
/// <param name="FilePath"></param>
/// <param name="Start"></param>
/// <param name="Length"></param>
/// <param name="StartLine"></param>
/// <param name="StartColumn"></param>
/// <param name="EndLine"></param>
/// <param name="EndColumn"></param>
public record SourceSpan(
    string FilePath,
    int Start,
    int Length,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn
)
{
    public override string ToString()
    {
        return $"at {nameof(FilePath)}:{FilePath}  " +
         $"{nameof(StartLine)}:{StartLine}  " +
         $"{nameof(StartColumn)}:{StartColumn}  " +
         $"{nameof(EndLine)}:{EndLine}  " +
         $"{nameof(EndColumn)}:{EndColumn}  " +
         $"{nameof(Start)}:{Start}  " +
         $"{nameof(Length)}:{Length}  " +
         "";
    }
};

/// <summary>
/// 表达式基类
/// </summary>
public abstract record Expr(SourceSpan SourceSpan);


/// <summary>
/// 解析异常表达式(占位，让 AST 不断裂)
/// </summary>
/// <param name="Message"></param>
/// <param name="SourceSpan"></param>
public record ErrorExpr(string Message, SourceSpan SourceSpan) : Expr(SourceSpan);

// ==================== 字面量表达式 ====================

/// <summary>
/// 字面量表达式
/// </summary>
public record LiteralExpr(object? Value, SourceSpan SourceSpan) : Expr(SourceSpan)
{
    public override string ToString()
    {
        return Value?.ToString() ?? "Value.Null";
    }
}

/// <summary>
/// 标识符引用表达式
/// </summary>
public record IdentifierExpr(string Name, SourceSpan SourceSpan) : Expr(SourceSpan);

// ==================== 声明表达式 ====================

/// <summary>
/// Let 声明（不可变）
/// </summary>
public record LetExpr(string Name, Expr Value, SourceSpan SourceSpan) : Expr(SourceSpan);

/// <summary>
/// Var 声明（可变）
/// </summary>
public record VarExpr(string Name, Expr Value, SourceSpan SourceSpan) : Expr(SourceSpan);

/// <summary>
/// 赋值表达式（仅允许 var）
/// </summary>
public record AssignExpr(string Name, Expr Value, SourceSpan SourceSpan) : Expr(SourceSpan);

/// <summary>
/// 索引赋值表达式（arr[index] = value）
/// </summary>
public record IndexAssignExpr(Expr Target, Expr Index, Expr Value, SourceSpan SourceSpan) : Expr(SourceSpan);

// ==================== 运算符表达式 ====================

/// <summary>
/// 二元运算表达式
/// </summary>
public record BinaryExpr(Expr Left, string Op, Expr Right, SourceSpan SourceSpan) : Expr(SourceSpan);

/// <summary>
/// 一元运算表达式
/// </summary>
public record UnaryExpr(string Op, Expr Expr, SourceSpan SourceSpan) : Expr(SourceSpan);

/// <summary>
/// 三元条件表达式
/// </summary>
public record ConditionalExpr(Expr Cond, Expr Then, Expr Else, SourceSpan SourceSpan) : Expr(SourceSpan);

// ==================== 控制流表达式 ====================

/// <summary>
/// If-Then-Else 表达式
/// </summary>
public record ReturnExpr(Expr? Value, SourceSpan SourceSpan) : Expr(SourceSpan);

/// <summary>
/// If-Then-Else 表达式
/// </summary>
public record IfExpr(Expr Cond, Expr Then, Expr Else, SourceSpan SourceSpan) : Expr(SourceSpan);

/// <summary>
/// When 表达式（模式匹配）
/// </summary>
public record WhenExpr(Expr Value, List<WhenClause> Clauses, WhenClause? OtherClause, SourceSpan SourceSpan) : Expr(SourceSpan);

/// <summary>
/// When 子句
/// </summary>
public record WhenClause(Expr Pattern, Expr Body, SourceSpan SourceSpan) : Expr(SourceSpan);

/// <summary>
/// For 循环表达式
/// </summary>
public record ForExpr(string VarName, Expr Iterable, Expr Body, SourceSpan SourceSpan) : Expr(SourceSpan);

// ==================== 函数表达式 ====================

/// <summary>
/// Lambda 表达式
/// </summary>
public record LambdaExpr(List<string> Params, Expr Body, SourceSpan SourceSpan) : Expr(SourceSpan);

/// <summary>
/// 函数调用表达式
/// </summary>
public record CallExpr(Expr Target, List<Expr> Args, SourceSpan SourceSpan) : Expr(SourceSpan);

// =================/// <summary>
/// 代码块表达式（返回最后一个表达式的值）
/// </summary>
public record BlockExpr(List<Expr> Statements, SourceSpan SourceSpan) : Expr(SourceSpan);

// ==================== 数据结构表达式 ====================

/// <summary>
/// 数组字面量
/// </summary>
public record ArrayLiteralExpr(List<Expr> Elements, SourceSpan SourceSpan) : Expr(SourceSpan);

/// <summary>
/// 对象字面量（map/record）
/// </summary>
public record ObjectLiteralExpr(List<ObjectProperty> Properties, SourceSpan SourceSpan) : Expr(SourceSpan);

/// <summary>
/// 对象属性
/// </summary>
public record ObjectProperty(string Key, Expr Value, SourceSpan SourceSpan) : Expr(SourceSpan);

// =================
/// <summary>
/// 成员访问表达式
/// </summary>
public record MemberAccessExpr(Expr Target, string Property, bool SafeNull, SourceSpan SourceSpan) : Expr(SourceSpan);

/// <summary>
/// 成员赋值表达式
/// </summary>
/// <param name="Target"></param>
/// <param name="Property"></param>
/// <param name="Value"></param>
/// <param name="SafeNull"></param>
public record MemberAssignExpr(Expr Target, string Property, Expr Value, bool SafeNull, SourceSpan SourceSpan) : Expr(SourceSpan);

/// <summary>
/// 索引访问表达式
/// </summary>
public record IndexAccessExpr(Expr Target, Expr Index, SourceSpan SourceSpan) : Expr(SourceSpan);

/// <summary>
/// 程序（顶层表达式列表）
/// </summary>
public record ProgramExpr(List<Expr> Statements, SourceSpan SourceSpan) : Expr(SourceSpan);

// ==================== 模块导入表达式 ====================

/// <summary>
/// Import 语句：import { member1 [: alias], member2 [: aslis2] } from "FILEPATH"
/// </summary>
public record ImportStmt(List<(string member, string? alias)> Members, string FilePath, SourceSpan SourceSpan) : Expr(SourceSpan);

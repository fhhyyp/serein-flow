using ScriptLang.Parser;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptLang.Runtime.ByteCode;

/// <summary>字节码操作符</summary>
public enum OpCode : byte
{
    /// <summary>无操作</summary>
    Nop = 0x00,
    /// <summary>加载 null </summary>
    LoadNull = 0x01,
    /// <summary>加载 true </summary>
    LoadTrue = 0x02,
    /// <summary>加载 false </summary>
    LoadFalse = 0x03,
    /// <summary>加载常量（Let）</summary>
    LoadConst = 0x04,
    /// <summary>加载变量</summary>
    LoadVar = 0x05,
    /// <summary>存储变量</summary>
    StoreVar = 0x06,
    /// <summary>将栈顶元素弹出</summary>
    Pop = 0x07,
    /// <summary>将栈顶元素复制一份并压入栈顶</summary>
    Dup = 0x08,

    /// <summary>'+'操作符</summary>
    Add = 0x10,
    /// <summary>'-'操作符</summary>
    Sub = 0x11,
    /// <summary>'*'操作符</summary>
    Mul = 0x12,
    /// <summary>'/'操作符</summary>
    Div = 0x13,
    /// <summary>'%'操作符</summary>
    Mod = 0x14,
    /// <summary>负号</summary>
    Neg = 0x20,
    /// <summary>'!'操作符</summary>
    Not = 0x21,
    /// <summary>'=' 操作符</summary>
    Equal = 0x22,
    /// <summary>'!=' 操作符</summary>
    Ne = 0x23,
    /// <summary>'>' 操作符</summary>
    Gt = 0x24,
    /// <summary>'>=' 操作符</summary>
    Ge = 0x25,
    /// <summary>'<' 操作符</summary>
    Lt = 0x26,
    /// <summary>'<='操作符</summary>
    Le = 0x27,
    /// <summary>'&&' 操作符</summary>
    And = 0x28,
    /// <summary>'||' 操作符</summary>
    Or = 0x29,

    /// <summary>跳转</summary>
    Jmp = 0x30,
    /// <summary>真分支跳转</summary>
    JumpIfTrue = 0x31,
    /// <summary>假分支跳转</summary>
    JmpIfFalse = 0x32,

    /// <summary>创建闭包</summary>
    CreateClosure = 0x40,
    /// <summary>调用函数</summary>
    Call = 0x41,
    /// <summary>返回</summary>
    Return = 0x42,

    /// <summary>导入模块</summary>
    // ImportModule = 0x50,

    /// <summary>捕获变量</summary>
    Capture = 0x60,
    /// <summary>加载捕获的变量</summary>
    LoadCapture = 0x61,
    /// <summary>存储捕获的变量</summary>
    StoreCapture = 0x62,

    /// <summary>创建对象</summary>
    CreateObject = 0x70,
    /// <summary>获取成员</summary>
    GetMember = 0x71,
    /// <summary>设置成员</summary>
    SetMember = 0x72,

    /// <summary>创建对象</summary>
    CreateArray = 0x80,
    /// <summary>获取索引</summary>
    GetIndex = 0x81,
    /// <summary>设置索引</summary>
    SetIndex = 0x82,

    /// <summary>获取迭代器</summary>
    GetIterator = 0x90,
    /// <summary>移动到下一个元素</summary>
    MoveNext = 0x91,
    /// <summary>读取迭代器当前</summary>
    Current = 0x92,

}


public sealed class Compiler
{
    private readonly List<Instruction> _code = [];

    public IReadOnlyList<Instruction> Compile(Expr expr)
    {
        Visit(expr);
        return _code;
    }

    private void Emit(OpCode op)
    {
        _code.Add(new(op));
    }

    private void Emit(OpCode op, object operand)
    {
        _code.Add(new(op, operand));
    }

    private void Visit(Expr expr)
    {
        switch (expr)
        {
            // 程序（顶层表达式列表）
            case ProgramExpr e:
                CompileProgram(e);
                break;

            // 解析异常表达式
            case ErrorExpr e:
                CompileError(e);
                break;

            // 字面量表达式
            case LiteralExpr e:
                CompileLiteral(e);
                break;

            // 标识符引用表达式
            case IdentifierExpr e:
                CompileIdentifier(e);
                break;

            // Let 声明
            case LetExpr e:
                CompileLet(e);
                break;

            // Var 声明
            case VarExpr e:
                CompileVar(e);
                break;

            // 赋值表达式
            case AssignExpr e:
                CompileAssign(e);
                break;

            // 索引赋值表达式
            case IndexAssignExpr e:
                CompileIndexAssign(e);
                break;

            // 二元运算表达式
            case BinaryExpr e:
                CompileBinary(e);
                break;

            // 一元运算表达式
            case UnaryExpr e:
                CompileUnary(e);
                break;

            // 三元条件表达式
            case ConditionalExpr e:
                CompileConditional(e);
                break;

            // Return 表达式
            case ReturnExpr e:
                CompileReturn(e);
                break;

            // If-Then-Else 表达式
            case IfExpr e:
                CompileIf(e);
                break;

            // When 表达式（模式匹配）
            case WhenExpr e:
                CompileWhen(e);
                break;

            // When 子句
            case WhenClause e:
                CompileWhenClause(e);
                break;

            // For 循环表达式
            case ForExpr e:
                CompileFor(e);
                break;

            // Lambda 表达式
            case LambdaExpr e:
                CompileLambda(e);
                break;

            // 函数调用表达式
            case CallExpr e:
                CompileCall(e);
                break;

            // 代码块表达式
            case BlockExpr e:
                CompileBlock(e);
                break;

            // 数组字面量
            case ArrayLiteralExpr e:
                CompileArrayLiteral(e);
                break;

            // 对象字面量
            case ObjectLiteralExpr e:
                CompileObjectLiteral(e);
                break;

            // 对象属性
            case ObjectProperty e:
                CompileObjectProperty(e);
                break;

            // 成员访问表达式
            case MemberAccessExpr e:
                CompileMemberAccess(e);
                break;

            // 成员赋值表达式
            case MemberAssignExpr e:
                CompileMemberAssign(e);
                break;

            // 索引访问表达式
            case IndexAccessExpr e:
                CompileIndexAccess(e);
                break;

            // Import 语句
            case ImportStmt e:
                CompileImport(e);
                break;
        }
    }

    /// <summary>编译解析异常表达式</summary>
    private void CompileError(ErrorExpr expr) { }

    /// <summary>编译字面量表达式</summary>
    private void CompileLiteral(LiteralExpr expr) { }

    /// <summary>编译标识符引用表达式</summary>
    private void CompileIdentifier(IdentifierExpr expr) { }

    /// <summary>编译 Let 声明表达式</summary>
    private void CompileLet(LetExpr expr) { }

    /// <summary>编译 Var 声明表达式</summary>
    private void CompileVar(VarExpr expr) { }

    /// <summary>编译赋值表达式</summary>
    private void CompileAssign(AssignExpr expr) { }

    /// <summary>编译索引赋值表达式</summary>
    private void CompileIndexAssign(IndexAssignExpr expr) { }

    /// <summary>编译二元运算表达式</summary>
    private void CompileBinary(BinaryExpr expr) { }

    /// <summary>编译一元运算表达式</summary>
    private void CompileUnary(UnaryExpr expr) { }

    /// <summary>编译三元条件表达式</summary>
    private void CompileConditional(ConditionalExpr expr) { }

    /// <summary>编译 Return 表达式</summary>
    private void CompileReturn(ReturnExpr expr) { }

    /// <summary>编译 If-Then-Else 表达式</summary>
    private void CompileIf(IfExpr expr) { }

    /// <summary>编译 When 表达式（模式匹配）</summary>
    private void CompileWhen(WhenExpr expr) { }

    /// <summary>编译 When 子句</summary>
    private void CompileWhenClause(WhenClause expr) { }

    /// <summary>编译 For 循环表达式</summary>
    private void CompileFor(ForExpr expr) { }

    /// <summary>编译 Lambda 表达式</summary>
    private void CompileLambda(LambdaExpr expr) { }

    /// <summary>编译函数调用表达式</summary>
    private void CompileCall(CallExpr expr) { }

    /// <summary>编译代码块表达式</summary>
    private void CompileBlock(BlockExpr expr) { }

    /// <summary>编译数组字面量表达式</summary>
    private void CompileArrayLiteral(ArrayLiteralExpr expr) { }

    /// <summary>编译对象字面量表达式</summary>
    private void CompileObjectLiteral(ObjectLiteralExpr expr) { }

    /// <summary>编译对象属性表达式</summary>
    private void CompileObjectProperty(ObjectProperty expr) { }

    /// <summary>编译成员访问表达式</summary>
    private void CompileMemberAccess(MemberAccessExpr expr) { }

    /// <summary>编译成员赋值表达式</summary>
    private void CompileMemberAssign(MemberAssignExpr expr) { }

    /// <summary>编译索引访问表达式</summary>
    private void CompileIndexAccess(IndexAccessExpr expr) { }

    /// <summary>编译程序（顶层表达式列表）</summary>
    private void CompileProgram(ProgramExpr expr) { }

    /// <summary>编译 Import 语句</summary>
    private void CompileImport(ImportStmt expr) { }
}

/// <summary>表示一条字节码指令</summary>
/// <param name="OpCode">指令类型</param>
/// <param name="Operand">操作数</param>
internal sealed record Instruction(OpCode OpCode, object? Operand = null);
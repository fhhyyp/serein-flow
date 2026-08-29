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

    /// <summary>加载 null</summary>
    LoadNull = 0x01,

    /// <summary>加载 true</summary>
    LoadTrue = 0x02,

    /// <summary>加载 false</summary>
    LoadFalse = 0x03,

    /// <summary>加载常量（操作数：常量表索引）</summary>
    LoadConst = 0x04,

    /// <summary>加载槽位变量（操作数：int 槽位索引）</summary>
    LoadSlot = 0x05,

    /// <summary>存储槽位变量（操作数：int 槽位索引）</summary>
    StoreSlot = 0x08,

    /// <summary>-1</summary>
    LoadM1 = 0x93,

    /// <summary>0</summary>
    Load0 = 0x94,

    /// <summary>1</summary>
    Load1 = 0x95,

    /// <summary>将栈顶元素弹出</summary>
    Pop = 0x09,

    /// <summary>将栈顶元素复制一份并压入栈顶</summary>
    Dup = 0x10,

    // ===== 算术/逻辑/比较运算 =====
    /// <summary>将栈顶 NumberValue 转换为 MutableNumber</summary>
    ToMutable = 0x9F,

    /// <summary>原地加法：Slots[operand] += Pop()</summary>
    AddInPlace = 0xA1,

    /// <summary>原地减法：Slots[operand] -= Pop()</summary>
    SubInPlace = 0xA2,

    /// <summary>原地乘法：Slots[operand] *= Pop()</summary>
    MulInPlace = 0xA3,

    /// <summary>原地除法：Slots[operand] /= Pop()</summary>
    DivInPlace = 0xA4,

    /// <summary>原地取模：Slots[operand] %= Pop()</summary>
    ModInPlace = 0xA5,


    /// <summary>'+'操作符</summary>
    Add = 0x20,

    /// <summary>'-'操作符</summary>
    Sub = 0x21,

    /// <summary>'*'操作符</summary>
    Mul = 0x22,

    /// <summary>'/'操作符</summary>
    Div = 0x23,

    /// <summary>'%'操作符</summary>
    Mod = 0x24,

    /// <summary>负号</summary>
    Neg = 0x25,

    /// <summary>'!'操作符</summary>
    Not = 0x26,

    /// <summary>'==' 操作符</summary>
    Equal = 0x27,

    /// <summary>'!=' 操作符</summary>
    Ne = 0x28,

    /// <summary>'>' 操作符</summary>
    Gt = 0x29,

    /// <summary>'>=' 操作符</summary>
    Ge = 0x2A,

    /// <summary>'<' 操作符</summary>
    Lt = 0x2B,

    /// <summary>'<='操作符</summary>
    Le = 0x2C,

    /// <summary>'&&' 操作符</summary>
    And = 0x2D,

    /// <summary>'||' 操作符</summary>
    Or = 0x2E,

    // ===== 跳转 =====

    /// <summary>无条件跳转（操作数：目标 IP）</summary>
    Jmp = 0x30,

    /// <summary>真分支跳转（操作数：目标 IP）</summary>
    JumpIfTrue = 0x31,

    /// <summary>假分支跳转（操作数：目标 IP）</summary>
    JmpIfFalse = 0x32,

    // ===== 函数/闭包 =====

    /// <summary>创建闭包（操作数：(chunkIndex, paramSlots, captureSlots)）</summary>
    CreateClosure = 0x40,

    /// <summary>调用函数（操作数：参数数量）</summary>
    Call = 0x41,

    /// <summary>返回</summary>
    Return = 0x42,

    // ===== 模块 =====

    /// <summary>导入模块（操作数：常量表索引）</summary>
    Import = 0x50,

    // ===== 对象操作 =====

    /// <summary>创建对象（操作数：属性数量）</summary>
    CreateObject = 0x70,

    /// <summary>获取成员</summary>
    GetMember = 0x71,

    /// <summary>设置成员</summary>
    SetMember = 0x72,

    // ===== 数组操作 =====

    /// <summary>创建数组（操作数：元素数量）</summary>
    CreateArray = 0x80,

    /// <summary>获取索引</summary>
    GetIndex = 0x81,

    /// <summary>设置索引</summary>
    SetIndex = 0x82,

    // ===== 迭代器 =====

    /// <summary>获取迭代器</summary>
    GetIterator = 0x90,

    /// <summary>移动到下一个元素</summary>
    MoveNext = 0x91,

    /// <summary>读取迭代器当前</summary>
    Current = 0x92,

    /// <summary>将迭代器当前值直接写入槽位（操作数：槽位索引），不经过栈</summary>
    CurrentToSlot = 0x96,
}
using System;
using System.Collections.Generic;

namespace ScriptLang.Runtime.ByteCode;

/// <summary>
/// 编译时确定的变量表，描述帧内所有变量的槽位布局
/// 可序列化，支持字节码持久化
/// </summary>
[Serializable]
public sealed class VariableTable
{
    /// <summary>局部变量数量（let/var/参数）</summary>
    public int LocalCount { get; init; }

    /// <summary>闭包捕获变量数量</summary>
    public int CaptureCount { get; init; }

    /// <summary>全局变量数量</summary>
    public int GlobalCount { get; init; }

    /// <summary>内置函数数量</summary>
    public int BuiltinCount { get; init; }

    /// <summary>槽位总数</summary>
    public int TotalCount => LocalCount + CaptureCount + GlobalCount + BuiltinCount;

    // ===== 偏移量 =====

    public int CaptureOffset => LocalCount;
    public int GlobalOffset => LocalCount + CaptureCount;
    public int BuiltinOffset => LocalCount + CaptureCount + GlobalCount;

    // ===== 正向映射 =====

    /// <summary>参数名 → 局部槽位索引</summary>
    public Dictionary<string, int> ParamSlots { get; init; } = new();

    /// <summary>全局变量名列表（按槽位顺序）</summary>
    public string[] GlobalNames { get; init; } = [];

    /// <summary>内置函数名列表（按槽位顺序）</summary>
    public string[] BuiltinNames { get; init; } = [];

    // ===== 反向映射（VM 按名反查槽位） =====

    /// <summary>局部变量名 → 局部槽位索引（0-based，相对于 Local 区）</summary>
    public Dictionary<string, int> LocalNames { get; init; } = new();

    /// <summary>捕获变量名 → 捕获槽位索引（0-based，相对于 Capture 区）</summary>
    public Dictionary<string, int> CaptureNames { get; init; } = new();

    /// <summary>
    /// 判断给定的槽位索引属于哪个区域
    /// </summary>
    public SlotRegion GetRegion(int slot)
    {
        if (slot < 0 || slot >= TotalCount)
            throw new ArgumentOutOfRangeException(nameof(slot));

        if (slot < CaptureOffset) return SlotRegion.Local;
        if (slot < GlobalOffset) return SlotRegion.Capture;
        if (slot < BuiltinOffset) return SlotRegion.Global;
        return SlotRegion.Builtin;
    }
}

/// <summary>槽位区域类型</summary>
public enum SlotRegion
{
    Local,
    Capture,
    Global,
    Builtin
}
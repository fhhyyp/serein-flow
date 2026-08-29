namespace ScriptLang.Runtime.ByteCode;
/*

/// <summary>
/// 编译时的变量绑定信息
/// </summary>
public sealed class VariableBinding
{
    /// <summary>局部变量槽位索引（-1 表示非局部变量）</summary>
    public int LocalSlot { get; set; } = -1;

    /// <summary>捕获变量槽位索引（-1 表示未被捕获）</summary>
    public int CapturedSlot { get; set; } = -1;

    /// <summary>是否可变</summary>
    public bool IsMutable { get; set; }

    /// <summary>是否被闭包捕获</summary>
    public bool IsCaptured { get; set; }

    /// <summary>是否是全局变量</summary>
    public bool IsGlobal { get; set; }

    /// <summary>是否是导入变量</summary>
    public bool IsImported { get; set; }

    /// <summary>变量名称</summary>
    public string Name { get; }

    public VariableBinding(string name, bool isMutable, bool isCaptured = false)
    {
        Name = name;
        IsMutable = isMutable;
        IsCaptured = isCaptured;
    }
}*/
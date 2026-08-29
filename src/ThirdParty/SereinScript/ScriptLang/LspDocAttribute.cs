namespace ScriptLang;

/// <summary>
/// 为模块成员（方法/属性）提供 LSP 智能提示的描述文本。
/// 用于修饰 [PrototypeFunction] / [PrototypeProperty] 方法。
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LspDocAttribute : Attribute
{
    /// <summary>描述文本，显示在补全列表的 Detail 中</summary>
    public string Description { get; }

    public LspDocAttribute(string description)
    {
        Description = description;
    }
}

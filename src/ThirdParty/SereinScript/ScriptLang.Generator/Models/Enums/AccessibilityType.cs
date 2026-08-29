#nullable disable

namespace ScriptLang.Generator
{
    /// <summary>
    /// 可访问性类型
    /// </summary>
    public enum AccessibilityType
    {
        /// <summary>
        /// 未定义访问修饰符
        /// </summary>
        Undefined,
        /// <summary>
        /// 公共访问，任何代码都可以访问
        /// </summary>
        Public,
        /// <summary>
        /// 内部访问，仅限于当前类、结构体访问
        /// </summary>
        Private,
        /// <summary>
        /// 仅限于同一程序集中继承关系中的访问
        /// </summary>
        ProtectedInternal,
        /// <summary>
        /// 仅限于当前类或当前程序集中的派生类中访问
        /// </summary>
        PrivateProtected,
        /// <summary>
        /// 仅限继承关系中访问
        /// </summary>
        Protected,
        /// <summary>
        /// 仅限当前程序集访问
        /// </summary>
        Internal,
    }
}

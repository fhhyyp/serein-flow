
#nullable disable

using System;

namespace ScriptLang
{
    /// <summary> 定义原型扩展的属性 </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    internal sealed class PrototypeExtensionAttribute : Attribute
    {
        /// <summary> 是否将该类作为参数传递给方法（适用于需要拓展 Value 类型方法）</summary>
        public bool PushThis = false;

        /// <summary> 生成的属性、方法命名风格（如果属性、方法存在别名，会忽略此设置） </summary>
        public NamingFormat NamingFormat = NamingFormat.Net;
    }

    /// <summary> 命名风格 </summary>
    public enum NamingFormat
    {
        /// <summary> 首字母大写 </summary>
        Net,
        /// <summary> 首字母小写 </summary>
        Js,
    }

    /// <summary> 定义原型方法的属性 </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    internal sealed class PrototypePropertyAttribute : Attribute
    {
        /// <summary>重新定义属性名称</summary>
#nullable enable
        public string? Name = default;
    }
    /// <summary> 定义原型方法的属性 </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    internal sealed class PrototypeFunctionAttribute : Attribute
    {
        /// <summary>重新定义方法名称</summary>
#nullable enable
        public string? Name = default;
    }

}

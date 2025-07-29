using System;

namespace Serein.Library
{
    /// <summary>
    /// 绑定转换器
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class BindConvertorAttribute : Attribute
    {
        public Type EnumType { get; }
        public Type ConvertorType { get; }

        public BindConvertorAttribute(Type @enum,  Type convertor)
        {
            EnumType = @enum;
            ConvertorType = convertor;
        }
    }

}

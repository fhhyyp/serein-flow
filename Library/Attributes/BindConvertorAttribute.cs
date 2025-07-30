using System;

namespace Serein.Library
{
    /// <summary>
    /// 绑定转换器
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class BindConvertorAttribute : Attribute
    {
        /// <summary>
        /// 枚举类型
        /// </summary>
        public Type EnumType { get; }
        /// <summary>
        /// 转换器类型
        /// </summary>
        public Type ConvertorType { get; }

        /// <summary>
        /// 绑定转换器特性
        /// </summary>
        /// <param name="enum"></param>
        /// <param name="convertor"></param>
        public BindConvertorAttribute(Type @enum,  Type convertor)
        {
            EnumType = @enum;
            ConvertorType = convertor;
        }
    }

}

using System;

namespace Serein.Library
{
    /// <summary>
    /// 枚举值转换器，要求枚举项标记的BindValueAttribute特性，与搭配的参数类型一致，否则参数不会传入
    /// </summary>

    [AttributeUsage(AttributeTargets.Parameter)]
    public class EnumTypeConvertorAttribute : Attribute
    {
        public Type EnumType { get; }

        public EnumTypeConvertorAttribute(Type @enum)
        {
            if (@enum.IsEnum)
            {
                EnumType = @enum;
            }
            else
            {
                throw new ArgumentException("需要枚举类型");
            }
        }
    }

}

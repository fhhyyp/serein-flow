using System;

namespace Serein.Library
{
    /// <summary>
    /// 绑定值特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class BindValueAttribute : Attribute
    {
        /// <summary>
        /// 绑定的值
        /// </summary>
        public object Value { get; }

        /// <summary>
        /// 绑定值特性构造函数
        /// </summary>
        /// <param name="value"></param>
        public BindValueAttribute(object value)
        {
            Value = value;
        }
    }

}

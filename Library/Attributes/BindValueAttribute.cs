using System;

namespace Serein.Library
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class BindValueAttribute : Attribute
    {
        public object Value { get; }

        public BindValueAttribute(object value)
        {
            Value = value;
        }
    }

}

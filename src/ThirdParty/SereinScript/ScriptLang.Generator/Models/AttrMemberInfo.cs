#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;

namespace ScriptLang.Generator.Models
{
    internal class AttrMemberInfo
    {
        public string MemberName { get; }
        public List<object> Values { get; } = new List<object>();

        internal AttrMemberInfo(string memberName)
        {
            MemberName = memberName;
        }

        public void AddValue(object value)
        {
            Values.Add(value);
        }

        public void AddValues(List<object> values)
        {
            Values.AddRange(values);
        }

        public List<object> GetValues() => Values.ToList();

        public object GetFirstValue() => Values.FirstOrDefault();

        public T GetFirstValue<T>()
        {
            var value = Values.FirstOrDefault();
            if (typeof(T).IsEnum && value is int @int)
            {
                if (Enum.IsDefined(typeof(T), @int))
                {
                    return (T)value;
                }
                return default;
            }
            else if (value is T result)
            {
                return result;
            }
            return default;
        }

        public override string ToString()
        {
            return $"Member:{MemberName} -> {string.Join(",", Values)}";
        }

    }
}

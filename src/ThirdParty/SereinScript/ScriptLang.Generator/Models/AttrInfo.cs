#nullable disable

using System.Collections.Generic;

namespace ScriptLang.Generator.Models
{
    /// <summary>
    /// 当前符号的特性信息类，包含特性的类型名称和成员信息。一个符号可能存在多个特性，每个特性可能包含多个成员信息。
    /// </summary>
    internal class AttrInfo
    {
        /// <summary>
        /// 特性的类型名称，通常是特性的全名，例如 "global::System.ObsoleteAttribute"。
        /// </summary>
        public string AttrTypeName { get; }

        /// <summary>
        /// 当前特性所包含的成员信息，成员名称作为键，成员信息对象作为值。一个特性可能包含多个成员信息，例如属性、字段等，这些成员信息可以提供关于特性的更多细节和上下文。
        /// </summary>
        public Dictionary<string, AttrMemberInfo> Members { get; } = new Dictionary<string, AttrMemberInfo>();

        internal AttrInfo(string attrTypeName)
        {
            AttrTypeName = attrTypeName;
        }

        public bool ContainsMember(string memberName) => Members.ContainsKey(memberName);

        public void AddMember(string memberName, object value)
        {
            var key = memberName;
            if (!Members.TryGetValue(key, out var member))
            {
                member = new AttrMemberInfo(memberName);
                Members.Add(key, member);
            }
            member.AddValue(value);
        }

        public AttrMemberInfo GetMenber(string attrName)
        {
            if (Members.TryGetValue(attrName, out var item))
            {
                return item;
            }
            else
            {
                return null;
            }
        }

        public override string ToString()
        {
            return $"Attr:{AttrTypeName} -> {string.Join(",", Members.Values)}";
        }
    }
}

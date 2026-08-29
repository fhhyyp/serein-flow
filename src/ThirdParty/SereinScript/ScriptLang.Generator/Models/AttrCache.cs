#nullable disable

using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace ScriptLang.Generator.Models
{
    /// <summary>
    /// 特性缓存类，用于缓存特性信息，避免重复计算，提高性能。
    /// </summary>
    internal class AttrCache
    {
        /// <summary>
        /// string: attribute 名称
        /// </summary>
        private readonly Dictionary<string, AttrInfo> Attrs = new Dictionary<string, AttrInfo>();

        public bool ContainsAttr<TAttribute>() where TAttribute : Attribute
        {
            var type = typeof(TAttribute);
            var fullName = $"global::{type.FullName}";
            return Attrs.ContainsKey(fullName);
        }

        /// <summary>
        /// 添加特性信息
        /// </summary>
        /// <param name="symbolAttrInfo"></param>
        public void AddInfo(AttrInfo symbolAttrInfo)
        {
            var attrName = symbolAttrInfo.AttrTypeName;
            if (Attrs.TryGetValue(attrName, out var attrInfo))
            {
                foreach (var member in symbolAttrInfo.Members)
                {
                    var memberName = member.Key;
                    foreach(var value in member.Value.Values)
                    {
                        attrInfo.AddMember(memberName, value);
                    }
                }
            }
            else
            {
                Attrs.Add(attrName, symbolAttrInfo);
            }
        }

        public AttrInfo GetAttr<TAttribute>() where TAttribute : Attribute
        {
            var type = typeof(TAttribute);
            var fullName = $"global::{type.FullName}";

            if (Attrs.TryGetValue(fullName, out var attrInfo))
            {
                return attrInfo;
            }
            else
            {
                return null;
            }
        }

        public TMember GetAttr<TAttribute, TMember>(Expression<Func<TAttribute, TMember>> expression, TMember defaultValue = default) where TAttribute : Attribute
        {
            var type = typeof(TAttribute);
            var fullName = $"global::{type.FullName}";

            if (!Attrs.TryGetValue(fullName, out var attrInfo))
            {
                return defaultValue;
            }
            var memberName = GetPropName(expression);
            if (string.IsNullOrEmpty(memberName))
            {
                return defaultValue;
            }
            var memberInfo = attrInfo.GetMenber(memberName);
            if (memberInfo is null)
            {
                return defaultValue;
            }
            return memberInfo.GetFirstValue<TMember>();
        }

        public TReturn GetAttr<TAttribute, TMember, TReturn>(Expression<Func<TAttribute, TMember>> expression, TReturn defaultValue = default) where TAttribute : Attribute
        {
            var type = typeof(TAttribute);
            var fullName = $"global::{type.FullName}";

            if (!Attrs.TryGetValue(fullName, out var attrInfo))
            {
                return defaultValue;
            }
            var memberName = GetPropName(expression);
            if (string.IsNullOrEmpty(memberName))
            {
                return defaultValue;
            }
            var memberInfo = attrInfo.GetMenber(memberName);
            if (memberInfo is null)
            {
                return defaultValue;
            }
            return memberInfo.GetFirstValue<TReturn>();
        }
        private static string GetPropName<T, TResult>(Expression<Func<T, TResult>> expr)
        {
            if (expr.Body is MemberExpression memberExpr)
                return memberExpr.Member.Name;

            if (expr.Body is UnaryExpression unary && unary.Operand is MemberExpression innerMember)
                return innerMember.Member.Name;

            return null;
        }

        public override string ToString()
        {
            return $"Attr Cache:{string.Join(",", Attrs.Keys)}";
        }

    }
}

#nullable disable

using ScriptLang.Generator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScriptLang.Generator.Models
{
    /*internal class CacheBase
    {

    }*/


    /// <summary>
    /// 表示语法结构的基类，承载用于标识成员的名称。记录一个语法符号
    /// </summary>
    /// <remarks>通常作为解析或生成语法树时所有具体语法节点的共同基类。</remarks>
    internal class ItemCacheBase 
    {
        /// <summary>
        /// 访问修饰符类型
        /// </summary>
        public AccessibilityType AccessibilityType { get; set; }

        /// <summary>
        /// 成员名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 成员特性缓存
        /// </summary>
        public AttrCache AttrsCache { get; } = new AttrCache();
    }

    #region 方法参数信息
    internal class MethodCache(List<ParameterCache> parameters) : ItemCacheBase
    {
        /// <summary>
        /// 方法符号
        /// </summary>
        public MethodDeclarationSyntax Syntax { get; internal set; }

        /// <summary>
        /// 方法返回类型
        /// </summary>
        public TypeCache ReturnType { get; set; }

        /// <summary>
        /// 方法参数类型
        /// </summary>
        public List<ParameterCache> Parameters { get; private set; } = parameters;

        /// <summary>
        /// 是静态的
        /// </summary>
        public bool IsStatic { get; set; }

        /// <summary>
        /// 是分部的
        /// </summary>
        public bool IsPartial { get; set; }

        /// <summary>
        /// 是抽象的
        /// </summary>
        public bool IsAbstract { get; set; }

        /// <summary>
        /// 是隐藏父类成员的 
        /// </summary>
        public bool IsNew { get; set; }

        /// <summary>
        /// 是允许重写的 
        /// </summary>
        public bool IsVirtual { get; set; }

        /// <summary>
        /// 是重写的
        /// </summary>
        public bool IsOverride { get; set; }

        /// <summary>
        /// 是否为异步方法
        /// </summary>
        public bool IsAsync { get; internal set; }
    }

    /// <summary>
    /// 参数信息
    /// </summary>
    internal class ParameterCache : ItemCacheBase
    {
        public ParameterCache()
        {
            AccessibilityType = AccessibilityType.Undefined; // 参数类型不存在访问修饰符，使用 Undefined 表示
        }

        /// <summary>
        /// 参数符号
        /// </summary>
        public ParameterSyntax Syntax { get; internal set; }

        /// <summary>
        /// 参数类型
        /// </summary>
        public TypeCache Type { get; set; }

        /// <summary>
        /// 是 ref 修饰的
        /// </summary>
        public bool IsRef { get; set; }

        /// <summary>
        /// 是 in 修饰的
        /// </summary>
        public bool IsIn { get; set; }

        /// <summary>
        /// 是 out 修饰的
        /// </summary>
        public bool IsOut { get; set; }

        /// <summary>
        /// 是 params 修饰的
        /// </summary>
        public bool IsParams { get; set; }

        /// <summary>
        /// 参数默认值（如果存在）
        /// </summary>
        public string DefaultValue { get; set; }
    }

    /// <summary>
    /// 类型信息
    /// </summary>
    internal class TypeCache(string name, string fullName)
    {
        /// <summary>
        /// 类型全名
        /// </summary>
        public string FullName { get; } = fullName;

        /// <summary>
        /// 类型名称
        /// </summary>
        public string Name { get; } = name;

        private const string ValueTaskType = "global::System.Threading.Tasks.ValueTask";
        private const string TaskType = "global::System.Threading.Tasks.Task";
        private const string VoidType = "global::System.Void";

        
        
        /// <summary>是否为 Void 返回值</summary>
        public bool IsVoid => FullName.StartsWith(VoidType);

        /// <summary> 是否为异步类型</summary>
        public bool IsTask => FullName.StartsWith(TaskType) || FullName.StartsWith(ValueTaskType);

        public bool HasTaskReturnValue()
        {
            if (!IsTask) return false;

            // 非泛型的 Task/ValueTask 表示无返回值
            if (FullName == TaskType || FullName == ValueTaskType)
                return false;

            // 泛型异步类型表示有返回值
            return FullName.Contains('<') && FullName.Contains('>');
        }

#nullable enable
        public string? GetTaskReturnType()
        {
            if (!IsTask) return null;

            // 检查是否是非泛型的 Task/ValueTask（即无返回值）
            if (FullName == TaskType || FullName == ValueTaskType)
                return null;

            // 提取泛型参数，例如：ValueTask<ArrayValue> -> ArrayValue
            int startIndex = FullName.IndexOf('<');
            int endIndex = FullName.LastIndexOf('>');

            if (startIndex == -1 || endIndex == -1)
                return null;

            string genericArg = FullName.Substring(startIndex + 1, endIndex - startIndex - 1);

            // 处理嵌套泛型，如 ValueTask<List<int>>
            // 简单处理：直接返回整个泛型参数
            return genericArg;
        }


        public override string ToString()
        {
            return FullName;
        }
    }
    #endregion


    #region Field and Property

    /// <summary>
    /// 成员信息（字段和属性的公共信息）
    /// </summary>
    internal class MemberCache : ItemCacheBase
    {
        /// <summary>
        /// 是静态的
        /// </summary>
        public bool IsStatic { get; set; }

        /// <summary>
        /// 成员类型（仅对于字段和属性）
        /// </summary>
        public TypeCache Type { get; set; } = default!;

        /// <summary>
        /// 可为空（仅对于字段和属性）
        /// </summary>
        public bool IsNullable { get; set; }

        /// <summary>
        /// 默认值（仅对于字段和属性）
        /// </summary>
        public string DefaultValue { get; set; } = string.Empty;

    }

    /// <summary>
    /// 字段信息
    /// </summary>
    internal class FieldCache : MemberCache
    {

        /// <summary>
        /// 字段声明语法（一个字段声明语法树存在一个或多个不同名称的定义）
        /// </summary>
        public FieldDeclarationSyntax FieldSyntax { get; set; } = default!;

        /// <summary>
        /// 字段定义变量声明语法
        /// </summary>
        public VariableDeclaratorSyntax VariableSyntax { get; set; } = default!;


        /// <summary>
        /// 是常量（仅对于字段）
        /// </summary>
        public bool IsConst { get; set; }

        /// <summary>
        /// 是否为只读属性
        /// </summary>
        public bool IsReadOnly { get; set; }


    }

    /// <summary>
    /// 属性信息
    /// </summary>
    internal class PropertyCache : MemberCache
    {
        /// <summary>
        /// 属性声明语法
        /// </summary>
        public PropertyDeclarationSyntax Syntax { get; set; } = default!;

        /// <summary>
        /// 是分部的
        /// </summary>
        public bool IsPartial { get; set; }

        /// <summary>
        /// 是抽象的
        /// </summary>
        public bool IsAbstract { get; set; }

        /// <summary>
        /// 是隐藏父类成员的 
        /// </summary>
        public bool IsNew { get; set; }

        /// <summary>
        /// 是允许重写的 
        /// </summary>
        public bool IsVirtual { get; set; }

        /// <summary>
        /// 是重写的
        /// </summary>
        public bool IsOverride { get; set; }

        /// <summary>
        /// 是否具有 Setter 访问器
        /// </summary>
        public bool HasSetter { get; set; }

        /// <summary>
        /// 是否具有 Getter 访问器
        /// </summary>
        public bool HasGetter { get; set; }

        /// <summary>
        /// Setter 访问器修饰符
        /// </summary>
        public AccessibilityType SetterAccessibilty { get; set; }

        /// <summary>
        /// Getter 访问器修饰符
        /// </summary>
        public AccessibilityType GetterAccessibilty { get; set; }
    }


    #endregion





}

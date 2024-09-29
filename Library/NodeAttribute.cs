using Serein.Library.Enums;
using System;

namespace Serein.Library.Attributes
{
    /// <summary>
    /// 表示该属性为自动注入依赖项
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class AutoInjectionAttribute : Attribute
    {
    }

    /// <summary>
    /// 表示该类自动注册（单例模式）
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AutoRegisterAttribute : Attribute
    {
    }

    /// <summary>
    /// 用来判断一个类是否需要注册并构建节点
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DynamicFlowAttribute : Attribute
    {
        public DynamicFlowAttribute(bool scan = true)
        {
            Scan = scan;
        }
        public bool Scan { get; set; } = true;
    }



    /// <summary>
    /// 建议触发器手动设置返回类型
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class NodeActionAttribute : Attribute
    {
        public NodeActionAttribute(NodeType methodDynamicType,
                                       string methodTips = "",
                                       bool scan = true,
                                       string lockName = "")
        {
            Scan = scan;
            MethodDynamicType = methodDynamicType;
            MethodTips = methodTips;
            LockName = lockName;
        }
        public bool Scan;
        public string MethodTips;
        public NodeType MethodDynamicType;
        public Type ReturnType;
        public string LockName;
    }



    //[AttributeUsage(AttributeTargets.Field)]
    //public class BindTypeAttribute : Attribute
    //{
    //    public Type Type { get; }

    //    public BindTypeAttribute(Type type)
    //    {
    //        Type = type;
    //    }
    //}

    [AttributeUsage(AttributeTargets.Field)]
    public class BindValueAttribute : Attribute
    {
        public object Value { get; }

        public BindValueAttribute(object value)
        {
            Value = value;
        }
    }

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
            this.EnumType = @enum;
            this.ConvertorType = convertor;
        }
    }

    /// <summary>
    /// 枚举转换器接口
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public interface IEnumConvertor<TEnum, TValue>
    {
        TValue Convertor(TEnum e);
    }

}

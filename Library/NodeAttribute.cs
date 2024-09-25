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
    /// 用来判断一个类是否需要注册并构建实例（单例模式场景使用）
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

    [AttributeUsage(AttributeTargets.Field)]
    public class PLCValueAttribute : Attribute
    {
        public enum VarType
        {
            /// <summary>
            /// 可写入值
            /// </summary>
            Write,
            /// <summary>
            /// 定时读取的可写入值（用来写入前判断），应该几乎不会有这种类型？
            /// </summary>
            TimingReadOrWrite,
            /// <summary>
            /// 只读取值（使用时刷新）
            /// </summary>
            ReadOnly,
            /// <summary>
            /// 定时刷新的只读取值（定时刷新用来触发触发器）
            /// </summary>
            TimingReadOnly,
        }

        public bool IsProtected { get; }
        public Type DataType { get; }
        public string Var { get; }
        //public int Length { get; }
        //public double Offset { get; }
        public VarType Type { get; }
        //public int RefreshInterval { get; }



        public PLCValueAttribute(Type type,
                                string @var,
                                VarType varType
                                //int refreshInterval = 100
                                )
        {
            DataType = type;
            Var = @var;
            //Offset = offset;
            //RefreshInterval = refreshInterval;
            Type = varType;
            //Length = length;
        }
    }


    /// <summary>
    /// 枚举值转换器
    /// </summary>

    //[AttributeUsage(AttributeTargets.Parameter)]
    //public class EnumConvertorAttribute : Attribute
    //{
    //    public Type Enum { get; }

    //    public EnumConvertorAttribute(Type @enum)
    //    {
    //        if (@enum.IsEnum)
    //        {
    //            Enum = @enum;
    //        }
    //        else
    //        {
    //            throw new ArgumentException("需要枚举类型");
    //        }
    //    }
    //}

}

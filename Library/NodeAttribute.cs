using System;

namespace Serein.Library
{
    /// <summary>
    /// <para>表示该属性为自动注入依赖项。</para>
    /// <para>使用场景：构造函数中存在互相依赖的情况</para>
    /// <para>例如ServiceA类构造函数中需要传入ServiceB，ServiceB类构造函数中也需要传入ServiceA</para>
    /// <para>这种情况会导致流程启动时，IOC容器无法注入构造函数并创建类型，导致启动失败。</para>
    /// <para>解决方法：从ServiceA类的构造函数中移除ServiceB类型的入参，将该类型更改为公开可见的可写属性成员ServiceB serviceB{get;set;}，并在该属性上标记[AutoInjection]特性</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class AutoInjectionAttribute : Attribute
    {
    }


    /// <summary>
    /// 注册顺序
    /// </summary>
    public enum RegisterSequence
    {    /// <summary>
         /// 不自动初始化
         /// </summary>
        Node,
        /// <summary>
        /// 初始化后
        /// </summary>
        FlowInit,
        /// <summary>
        /// 加载后
        /// </summary>
        FlowLoading,
    }


    /// <summary>
    /// <para>启动流程时，会将标记了该特性的类自动注册到IOC容器中，从而无需手动进行注册绑定。</para>
    /// <para>流程启动后，IOC容器会进行5次注册绑定。</para>
    /// <para>第1次注册绑定：初始化所有节点所属的类（[DynamicFlow]标记的类）。</para>
    /// <para>第2次注册绑定：※初始化所有[AutoRegister(Class=FlowInit)]的类。</para>
    /// <para>第3次注册绑定：调用所有Init节点后，进行注册绑定。</para>
    /// <para>第4次注册绑定：※初始化所有[AutoRegister(Class=FlowLoading)]的类</para>
    /// <para>第5次注册绑定：调用所有Load节点后，进行注册绑定。</para>
    /// <para>需要注意的是，在第1次进行注册绑定的过程中，如果类的构造函数存在入参，那么也会将入参自动创建实例并托管到IOC容器中。</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AutoRegisterAttribute : Attribute
    {
        public AutoRegisterAttribute(RegisterSequence Class = RegisterSequence.FlowInit)
        {
            this.Class = Class;
        }
        public RegisterSequence Class ;
    }

    /// <summary>
    /// <para>表示该类中存在节点信息</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DynamicFlowAttribute : Attribute
    {
        public DynamicFlowAttribute(string name = "",bool scan = true)
        {
            Name = name;
            Scan = scan;
        }
        /// <summary>
        /// 补充名称，不影响运行流程
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 如果设置为false，将忽略该类
        /// </summary>
        public bool Scan { get; set; } = true;
    }



    /// <summary>
    ///  <para>表示该方法将会生成节点，或是加入到流程运行中</para>
    ///  <para>如果是Task类型的返回值，将会自动进行等待</para>
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
            AnotherName = methodTips;
            LockName = lockName;
        }
        /// <summary>
        /// 如果设置为false时将不会生成节点信息
        /// </summary>
        public bool Scan;
        /// <summary>
        /// 类似于注释的效果
        /// </summary>
        public string AnotherName;
        /// <summary>
        /// 标记节点行为
        /// </summary>
        public NodeType MethodDynamicType;
        /// <summary>
        /// 暂无意义
        /// </summary>
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

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
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

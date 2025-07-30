using Serein.Library;
using System;

namespace Serein.Library
{
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
        /// <summary>
        /// 自动注册特性
        /// </summary>
        /// <param name="Class"></param>
        public AutoRegisterAttribute(RegisterSequence Class = RegisterSequence.FlowInit)
        {
            this.Class = Class;
        }
        /// <summary>
        /// 注册顺序
        /// </summary>
        public RegisterSequence Class ;
    }

}

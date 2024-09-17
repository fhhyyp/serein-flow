using System;
using System.Collections.Generic;
using System.Text;

namespace Serein.Library.Api
{
    public interface ISereinIOC
    {
        /// <summary>
        /// 清空
        /// </summary>
        /// <returns></returns>
        ISereinIOC Reset();
        /// <summary>
        /// 注册实例
        /// </summary>
        ISereinIOC Register(Type type, params object[] parameters);
        ISereinIOC Register<T>(params object[] parameters);
        ISereinIOC Register<TService, TImplementation>(params object[] parameters) where TImplementation : TService;
        /// <summary>
        /// 获取或创建并注入目标类型
        /// </summary>
        T GetOrRegisterInstantiate<T>();
        /// <summary>
        /// 获取或创建并注入目标类型
        /// </summary>
        object GetOrRegisterInstantiate(Type type);

        /// <summary>
        /// 创建目标类型的对象， 并注入依赖项
        /// </summary>
        object Instantiate(Type type, params object[] parameters);
        ISereinIOC Build();
        ISereinIOC Run<T>(Action<T> action);
        ISereinIOC Run<T1, T2>(Action<T1, T2> action);
        ISereinIOC Run<T1, T2, T3>(Action<T1, T2, T3> action);
        ISereinIOC Run<T1, T2, T3, T4>(Action<T1, T2, T3, T4> action);
        ISereinIOC Run<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> action);
        ISereinIOC Run<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> action);
        ISereinIOC Run<T1, T2, T3, T4, T5, T6, T7>(Action<T1, T2, T3, T4, T5, T6, T7> action);
        ISereinIOC Run<T1, T2, T3, T4, T5, T6, T7, T8>(Action<T1, T2, T3, T4, T5, T6, T7, T8> action);
    }

}

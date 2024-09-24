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
        /// <summary>
        /// 注册实例
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="parameters"></param>
        /// <returns></returns>
        ISereinIOC Register<T>(params object[] parameters);
        /// <summary>
        /// 注册接口的实例
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="TImplementation"></typeparam>
        /// <param name="parameters"></param>
        /// <returns></returns>
        ISereinIOC Register<TService, TImplementation>(params object[] parameters) where TImplementation : TService;
        /// <summary>
        /// 获取或创建并注入目标类型，会记录到IOC容器中。
        /// </summary>
        T GetOrRegisterInstantiate<T>();
        /// <summary>
        /// 获取或创建并注入目标类型，会记录到IOC容器中。
        /// </summary>
        object GetOrRegisterInstantiate(Type type);
        /// <summary>
        /// 获取类型的实例
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        object Get(Type type);

        /// <summary>
        /// 获取指定名称的实例
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        T Get<T>(string key);

        /// <summary>
        /// 通过名称注册实例
        /// </summary>
        /// <param name="key">注入名称</param>
        /// <param name="instance">实例对象</param>
        /// <param name="needInjectProperty">是否需要注入依赖项</param>
        void CustomRegisterInstance(string key, object instance, bool needInjectProperty = true);

        /// <summary>
        /// 用于临时实例的创建，不注册到IOC容器中，依赖项注入失败时也不记录。
        /// </summary>
        object Instantiate(Type type, params object[] parameters);

        /// <summary>
        /// 实例化注册的类型，并注入依赖项
        /// </summary>
        /// <returns></returns>
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

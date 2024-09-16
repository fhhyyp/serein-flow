using System;
using System.Collections.Generic;
using System.Text;

namespace Serein.Library.Api
{
    public interface ISereinIoc
    {
        /// <summary>
        /// 获取或创建类型的实例（不注入依赖项）
        /// </summary>
        object GetOrCreateServiceInstance(Type serviceType, params object[] parameters);
        T GetOrCreateServiceInstance<T>(params object[] parameters);
        /// <summary>
        /// 清空
        /// </summary>
        /// <returns></returns>
        ISereinIoc Reset();
        /// <summary>
        /// 注册实例
        /// </summary>
        ISereinIoc Register(Type type, params object[] parameters);
        ISereinIoc Register<T>(params object[] parameters);
        ISereinIoc Register<TService, TImplementation>(params object[] parameters) where TImplementation : TService;
        /// <summary>
        /// 获取或创建并注入目标类型
        /// </summary>
        T GetOrInstantiate<T>();
        /// <summary>
        /// 获取或创建并注入目标类型
        /// </summary>
        object GetOrInstantiate(Type type);

        /// <summary>
        /// 创建目标类型的对象， 并注入依赖项
        /// </summary>
        object Instantiate(Type type, params object[] parameters);
        ISereinIoc Build();
        ISereinIoc Run<T>(Action<T> action);
        ISereinIoc Run<T1, T2>(Action<T1, T2> action);
        ISereinIoc Run<T1, T2, T3>(Action<T1, T2, T3> action);
        ISereinIoc Run<T1, T2, T3, T4>(Action<T1, T2, T3, T4> action);
        ISereinIoc Run<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> action);
        ISereinIoc Run<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> action);
        ISereinIoc Run<T1, T2, T3, T4, T5, T6, T7>(Action<T1, T2, T3, T4, T5, T6, T7> action);
        ISereinIoc Run<T1, T2, T3, T4, T5, T6, T7, T8>(Action<T1, T2, T3, T4, T5, T6, T7, T8> action);
    }

}

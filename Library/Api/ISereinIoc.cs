using System;
using System.Collections.Generic;
using System.Text;

namespace Serein.Library.Api
{
    /// <summary>
    /// <para>单例模式IOC容器，内部维护了一个实例字典，默认使用类型的FullName作为Key，</para>
    /// <para>如果以“接口-实现类”的方式注册，那么将使用接口类型的FullName作为Key。</para>
    /// <para>当某个类型注册绑定成功后，将不会因为其它地方尝试注册相同类型的行为导致类型被重新创建。</para>
    /// </summary>
    public interface ISereinIOC 
    {
        /// <summary>
        /// 慎用，重置IOC容器，除非再次注册绑定，否则将导致不能创建注入依赖类的临时对象。
        /// </summary>
        /// <returns></returns>
        ISereinIOC Reset();

        /// <summary>
        /// 通过指定类型的方式注册实例
        /// </summary>
        /// <param name="type">实例类型</param>
        /// <returns></returns>
        ISereinIOC Register(Type type);

        /// <summary>
        /// 通过指定类型的方式注册实例
        /// </summary>
        /// <param name="type">实例类型</param>
        /// <param name="getInstance">获取实例的回调函数</param>
        /// <returns></returns>
        ISereinIOC Register(Type type, Func<object> getInstance);
        
        /// <summary>
        /// 通过泛型的方式注册实例
        /// </summary>
        /// <typeparam name="T">实例类型</typeparam>
        /// <param name="getInstance">获取实例的回调函数</param>
        /// <returns></returns>
        ISereinIOC Register<T>(Func<T> getInstance);

        /// <summary>
        /// 注册接口的实例
        /// </summary>
        /// <typeparam name="TService">接口类型</typeparam>
        /// <typeparam name="TImplementation">实例类型</typeparam>
        /// <param name="getInstance">获取实例的回调函数</param>
        /// <returns></returns>
        ISereinIOC Register<TService, TImplementation>(Func<TService> getInstance) where TImplementation : TService;

        /// <summary>
        /// 注册接口的实现类
        /// </summary>
        /// <typeparam name="TService">接口类型</typeparam>
        /// <typeparam name="TImplementation">实例类型</typeparam>
        /// <returns></returns>
        ISereinIOC Register<TService, TImplementation>() where TImplementation : TService;

        /// <summary>
        /// 获取类型的实例。如果需要获取的类型以“接口-实现类”的方式注册，请使用接口的类型。
        /// </summary>
        object Get(Type type);

        /// <summary>
        /// 获取类型的实例。如果需要获取的类型以“接口-实现类”的方式注册，请使用接口的类型。
        /// </summary>
        T Get<T>();

        /// <summary>
        /// <para>给定一个类型，由IOC容器负责创建实例，如果存在多个构造函数，将由参数最多的构造函数开始尝试创建。</para>
        /// <para></para>
        /// </summary>
        object CreateTempObject(Type type);

        /// <summary>
        /// <para>给定一个类型，由IOC容器负责创建实例，如果存在多个构造函数，将由参数最多的构造函数开始尝试创建。</para>
        /// <para></para>
        /// </summary>
        T CreateTempObject<T>();

        /// <summary>
        /// 搜寻已注册的类型生成依赖关系，依次实例化并注入依赖项，缓存在由IOC容器维护的Map中，直到手动调用Reset()方法。
        /// </summary>
        /// <returns></returns>
        ISereinIOC Build();
    }

}

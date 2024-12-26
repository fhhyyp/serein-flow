using System;
using System.Collections.Generic;
using System.Text;

namespace Serein.Library.Api
{
    /// <summary>
    /// <para>单例模式IOC容器，内部维护了一个实例字典，默认使用类型的FullName作为Key，如果以“接口-实现类”的方式注册，那么将使用接口类型的FullName作为Key。</para>
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
        /// 注册实例，如果确定了params，那么将使用params入参构建实例对象。
        /// </summary>
        ISereinIOC Register(Type type, params object[] parameters);
        /// <summary>
        /// 通过泛型的方式注册实例，如果确定了params，那么将使用params入参构建实例对象。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="parameters"></param>
        /// <returns></returns>
        ISereinIOC Register<T>(params object[] parameters);
        /// <summary>
        /// 注册接口的实例
        /// </summary>
        /// <typeparam name="TService">接口类型</typeparam>
        /// <typeparam name="TImplementation">实例类型</typeparam>
        /// <param name="parameters"></param>
        /// <returns></returns>
        ISereinIOC Register<TService, TImplementation>(params object[] parameters) where TImplementation : TService;

        /// <summary>
        /// 指定一个Key登记一个持久化的实例。
        /// </summary>
        /// <param name="key">登记使用的名称</param>
        /// <param name="instance">实例对象</param>
        /// <returns>是否注册成功</returns>
        bool RegisterPersistennceInstance(string key, object instance);

        /// <summary>
        /// 指定一个Key登记一个实例。
        /// </summary>
        /// <param name="key">登记使用的名称</param>
        /// <param name="instance">实例对象</param>
        /// <returns>是否注册成功</returns>
        bool RegisterInstance(string key, object instance);

        /// <summary>
        /// 获取类型的实例。如果需要获取的类型以“接口-实现类”的方式注册，请使用接口的类型。
        /// </summary>
        object Get(Type type);
        /// <summary>
        /// 获取类型的实例。如果需要获取的类型以“接口-实现类”的方式注册，请使用接口的类型。
        /// </summary>
        T Get<T>();

        /// <summary>
        /// <para>获取指定名称的实例。</para>
        /// <para>正常情况下应该使用 Get(Type type) /  T Get&lt;T&gt;() 进行获取，但如果需要的实例是以CustomRegisterInstance()进行的登记，则需要通过这种方法进行获取。</para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">登记实例时使用的Key</param>
        /// <returns></returns>
        T Get<T>(string key);



        /// <summary>
        /// <para>创建实例并注入依赖项，不会注册到IOC容器中。</para>
        /// <para>使用场景：例如 View 的构造函数中需要创建 ViewModel，而 ViewModel 存在注册过的依赖项，可以通过该接口进行创建</para>
        /// <para></para>
        /// </summary>
        object Instantiate(Type type);

        /// <summary>
        /// <para>创建实例并注入依赖项，不会注册到IOC容器中。</para>
        /// <para>使用场景：例如 View 的构造函数中需要创建 ViewModel，而 ViewModel 存在注册过的依赖项，可以通过该接口进行创建</para>
        /// <para></para>
        /// </summary>
        T Instantiate<T>();

        /// <summary>
        /// 通过已注册的类型，生成依赖关系，然后依次实例化并注入依赖项，最后登记到容器中。
        /// </summary>
        /// <returns></returns>
        ISereinIOC Build();

        /// <summary>
        /// 从容器中获取某个类型的实例进行运行
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="action"></param>
        /// <returns></returns>
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

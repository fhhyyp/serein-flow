using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.Web;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Serein.Library.Utils
{
    /// <summary>
    /// IOC管理容器
    /// </summary>
    public class SereinIOC : ISereinIOC
    {
        /// <summary>
        /// 类型集合，暂放待实例化的类型，完成实例化之后移除
        /// </summary>
        private readonly ConcurrentDictionary<string, Type> _typeMappings;

        /// <summary>
        /// 实例集合（包含已完成注入、未完成注入的对象实例，计划在未来的版本中区分：）
        /// </summary>
        private readonly ConcurrentDictionary<string, object> _dependencies;

        /// <summary>
        /// 未完成注入的实例集合。
        /// 键：需要的类型名称
        /// 值：元组（对象实例，对象的属性）
        /// </summary>
        private readonly ConcurrentDictionary<string, List<(object,PropertyInfo)>> _unfinishedDependencies;


        /// <summary>
        /// 待实例化的类型
        /// </summary>
        // private readonly List<Type> _waitingForInstantiation;

        public SereinIOC()
        {
            // 首先注册自己
            _dependencies = new ConcurrentDictionary<string, object>();
            _typeMappings = new ConcurrentDictionary<string, Type>(); 
            _unfinishedDependencies = new ConcurrentDictionary<string, List<(object, PropertyInfo)>>();
        }

        public void InitRegister()
        {
            _dependencies[typeof(ISereinIOC).FullName] = this;
            Register<IRouter, Router>();
            /*foreach (var type in _typeMappings.Values)
            {
                Register(type);
            }
            Build();*/
        }

        #region 类型的注册

        /// <summary>
        /// 注册类型
        /// </summary>
        /// <param name="type">目标类型</param>
        /// <param name="parameters">参数</param>
        public ISereinIOC Register(Type type, params object[] parameters)
        {
            RegisterType(type?.FullName, type);
            return this;
        }
        /// <summary>
        /// 注册类型
        /// </summary>
        /// <param name="type">目标类型</param>
        /// <param name="parameters">参数</param>
        public ISereinIOC Register<T>(params object[] parameters)
        {
            var type = typeof(T);
            RegisterType(type.FullName, type);
            return this;
        }

        /// <summary>
        /// 注册接口类型
        /// </summary>
        /// <param name="type">目标类型</param>
        /// <param name="parameters">参数</param>
        public ISereinIOC Register<TService, TImplementation>(params object[] parameters)
            where TImplementation : TService
        {
            var typeFullName = typeof(TService).FullName;
            RegisterType(typeFullName, typeof(TImplementation));
            return this;
        } 
        #endregion


        /// <summary>
        /// 尝试从容器中获取对象，如果不存在目标类型的对象，则将类型信息登记到容器，并实例化注入依赖项。
        /// </summary>
        public object GetOrRegisterInstantiate(Type type)
        {
            // 尝试从容器中获取对象
            if (!_dependencies.TryGetValue(type.FullName, out object value))
            {
                Register(type);// 注册类型信息
                value = Instantiate(type); // 创建实例对象，并注入依赖
                _dependencies.TryAdd(type.FullName, value); // 登记到IOC容器中
            }
            return value; 
        }

        /// <summary>
        /// 尝试从容器中获取对象，如果不存在目标类型的对象，则将类型信息登记到容器，并实例化注入依赖项。
        /// </summary>
        public T GetOrRegisterInstantiate<T>()
        {
            var value = Instantiate(typeof(T));
            return (T)value;
        }

        /// <summary>
        /// 根据类型生成对应的实例，并注入其中的依赖项（类型信息不登记到IOC容器中）
        /// </summary>
        /// <param name="controllerType"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public object Instantiate(Type controllerType, params object[] parameters)
        {
            var instance = CreateInstance(controllerType, parameters); // 创建目标类型的实例
            if(instance != null)
            {   
                InjectDependencies(instance); // 完成创建后注入实例需要的依赖项
            }
            return instance;
        }

        #region 容器管理（清空，绑定）

        /// <summary>
        /// 清空容器对象
        /// </summary>
        /// <returns></returns>
        public ISereinIOC Reset()
        {
            // 检查是否存在非托管资源
            foreach (var instancei in _dependencies.Values)
            {
                if (typeof(IDisposable).IsAssignableFrom(instancei.GetType()) && instancei is IDisposable disposable)
                {
                    disposable?.Dispose();
                }
            }
            _unfinishedDependencies?.Clear();
            _typeMappings?.Clear();
            _dependencies?.Clear();
            // _waitingForInstantiation?.Clear();
            return this;
        }

        /// <summary>
        /// 实例化所有已注册的类型，并尝试绑定
        /// </summary>
        /// <returns></returns>
        public ISereinIOC Build()
        {
            InitRegister();
            // 遍历已注册类型
            foreach (var type in _typeMappings.Values.ToArray())
            {

                if (_dependencies.ContainsKey(type.FullName))
                {
                    // 已经存在实例，不用管
                }
                else
                {
                    // 如果没有创建实例，则创建对应的实例
                    _dependencies[type.FullName] = CreateInstance(type);
                }
                // 移除类型的注册记录
                _typeMappings.TryRemove(type.FullName, out _);
            }

            // 注入实例的依赖项
            foreach (var instance in _dependencies.Values)
            {
                InjectDependencies(instance);
            }

            //var instance = Instantiate(item.Value);

            // TryInstantiateWaitingDependencies();

            return this;
        } 
        #endregion

        #region 私有方法


        /// <summary>
        /// 注册类型
        /// </summary>
        /// <param name="typeFull"></param>
        /// <param name="type"></param>
        private void RegisterType(string typeFull, Type type)
        {
            if (!_typeMappings.ContainsKey(typeFull))
            {
                _typeMappings[typeFull] = type;
            }
        }

        /// <summary>
        /// 创建实例时，尝试注入到由ioc容器管理、并需要此实例的对象。
        /// </summary>
        private object CreateInstance(Type type, params object[] parameters)
        {
            var instance = Activator.CreateInstance(type);
            if (_unfinishedDependencies.TryGetValue(type.FullName, out var unfinishedPropertyList))
            {
                foreach ((object obj, PropertyInfo property) in unfinishedPropertyList)
                {
                    property.SetValue(obj, instance); //注入依赖项
                }

                if (_unfinishedDependencies.TryRemove(type.FullName, out unfinishedPropertyList))
                {
                    unfinishedPropertyList.Clear();
                }
            }
            return instance;
        }


        /// <summary>
        /// 注入目标实例的依赖项
        /// </summary>
        /// <param name="instance"></param>
        private bool InjectDependencies(object instance)
        {
            var properties = instance.GetType()
                                     .GetProperties(BindingFlags.Instance | BindingFlags.Public).ToArray()
                                     .Where(p => p.CanWrite // 可写属性
                                              && p.GetCustomAttribute<AutoInjectionAttribute>() != null // 有特性标注需要注入
                                              && p.GetValue(instance) == null); // 属性为空
            var isPass = true;
            foreach (var property in properties)
            {
                var propertyType = property.PropertyType;
                // 通过属性类型名称从ioc容器中获取对应的实例
                if (_dependencies.TryGetValue(propertyType.FullName, out var dependencyInstance))
                {
                    property.SetValue(instance, dependencyInstance); // 尝试写入到目标实例的属性中
                }
                else
                {
                    // 存在依赖项，但目标类型的实例暂未加载，需要等待需要实例完成注册
                    var unfinishedDependenciesList = _unfinishedDependencies.GetOrAdd(propertyType.FullName, _ = new List<(object, PropertyInfo)>());
                    var data = (instance, property);
                    if (!unfinishedDependenciesList.Contains(data))
                    {
                        unfinishedDependenciesList.Add(data);
                    }
                    isPass = false;
                }
            }
            return isPass;
        }


        /// <summary>
        /// 再次尝试注入目标实例的依赖项
        /// </summary>
        //private void TryInstantiateWaitingDependencies()
        //{
        //    foreach (var waitingType in _waitingForInstantiation.ToList())
        //    {
        //        if (_typeMappings.TryGetValue(waitingType.FullName, out var implementationType))
        //        {
        //            var instance = Instantiate(implementationType);
        //            if (instance != null)
        //            {

        //                _dependencies[waitingType.FullName] = instance;

        //                _waitingForInstantiation.Remove(waitingType);
        //            }
        //        }
        //    }
        //} 
        #endregion

        #region run()
        public ISereinIOC Run<T>(Action<T> action)
        {
            var service = GetOrRegisterInstantiate<T>();
            if (service != null)
            {
                action(service);
            }
            return this;
        }

        public ISereinIOC Run<T1, T2>(Action<T1, T2> action)
        {
            var service1 = GetOrRegisterInstantiate<T1>();
            var service2 = GetOrRegisterInstantiate<T2>();

            action(service1, service2);
            return this;
        }

        public ISereinIOC Run<T1, T2, T3>(Action<T1, T2, T3> action)
        {
            var service1 = GetOrRegisterInstantiate<T1>();
            var service2 = GetOrRegisterInstantiate<T2>();
            var service3 = GetOrRegisterInstantiate<T3>();
            action(service1, service2, service3);
            return this;
        }

        public ISereinIOC Run<T1, T2, T3, T4>(Action<T1, T2, T3, T4> action)
        {
            var service1 = GetOrRegisterInstantiate<T1>();
            var service2 = GetOrRegisterInstantiate<T2>();
            var service3 = GetOrRegisterInstantiate<T3>();
            var service4 = GetOrRegisterInstantiate<T4>();
            action(service1, service2, service3, service4);
            return this;
        }

        public ISereinIOC Run<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> action)
        {
            var service1 = GetOrRegisterInstantiate<T1>();
            var service2 = GetOrRegisterInstantiate<T2>();
            var service3 = GetOrRegisterInstantiate<T3>();
            var service4 = GetOrRegisterInstantiate<T4>();
            var service5 = GetOrRegisterInstantiate<T5>();
            action(service1, service2, service3, service4, service5);
            return this;
        }

        public ISereinIOC Run<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> action)
        {
            var service1 = GetOrRegisterInstantiate<T1>();
            var service2 = GetOrRegisterInstantiate<T2>();
            var service3 = GetOrRegisterInstantiate<T3>();
            var service4 = GetOrRegisterInstantiate<T4>();
            var service5 = GetOrRegisterInstantiate<T5>();
            var service6 = GetOrRegisterInstantiate<T6>();
            action(service1, service2, service3, service4, service5, service6);
            return this;
        }

        public ISereinIOC Run<T1, T2, T3, T4, T5, T6, T7>(Action<T1, T2, T3, T4, T5, T6, T7> action)
        {
            var service1 = GetOrRegisterInstantiate<T1>();
            var service2 = GetOrRegisterInstantiate<T2>();
            var service3 = GetOrRegisterInstantiate<T3>();
            var service4 = GetOrRegisterInstantiate<T4>();
            var service5 = GetOrRegisterInstantiate<T5>();
            var service6 = GetOrRegisterInstantiate<T6>();
            var service7 = GetOrRegisterInstantiate<T7>();
            action(service1, service2, service3, service4, service5, service6, service7);
            return this;
        }

        public ISereinIOC Run<T1, T2, T3, T4, T5, T6, T7, T8>(Action<T1, T2, T3, T4, T5, T6, T7, T8> action)
        {
            var service1 = GetOrRegisterInstantiate<T1>();
            var service2 = GetOrRegisterInstantiate<T2>();
            var service3 = GetOrRegisterInstantiate<T3>();
            var service4 = GetOrRegisterInstantiate<T4>();
            var service5 = GetOrRegisterInstantiate<T5>();
            var service6 = GetOrRegisterInstantiate<T6>();
            var service7 = GetOrRegisterInstantiate<T7>();
            var service8 = GetOrRegisterInstantiate<T8>();
            action(service1, service2, service3, service4, service5, service6, service7, service8);
            return this;
        }


        #endregion
    }


    /* public interface IServiceContainer
     {
         ServiceContainer Register<T>(params object[] parameters);
         ServiceContainer Register<TService, TImplementation>(params object[] parameters) where TImplementation : TService;
         TService Resolve<TService>();
         void Get<T>(Action<T> action);
         object Instantiate(Type type, params object[] parameters);

     }
     public class ServiceContainer : IServiceContainer
     {
         private readonly Dictionary<Type, object> _dependencies;
         public ServiceContainer()
         {
             _dependencies = new Dictionary<Type, object>
             {
                 [typeof(IServiceContainer)] = this
             };
         }

         public void Get<T>(Action<T> action)
         {
             var service = Resolve<T>();
             action(service);
         }
         public ServiceContainer Register<T>(params object[] parameters)
         {
             var instance = Instantiate(typeof(T), parameters);
             _dependencies[typeof(T)] = instance;
             return this;
         }

         public ServiceContainer Register<TService, TImplementation>(params object[] parameters)
             where TImplementation : TService
         {

             _dependencies[typeof(TService)] = Instantiate(typeof(TImplementation), parameters);
             return this;
         }


         public TService Resolve<TService>()
         {
             return (TService)_dependencies[typeof(TService)];
         }

         public object Instantiate(Type controllerType, params object[] parameters)
         {
             var constructors = controllerType.GetConstructors(); // 获取控制器的所有构造函数

             // 查找具有最多参数的构造函数
             var constructor = constructors.OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();

             if (constructor != null)
             {
                 if (parameters.Length > 0)
                 {
                     return Activator.CreateInstance(controllerType, parameters);
                 }
                 else {
                     var tmpParameters = constructor.GetParameters();
                     var dependencyInstances = new List<object>();

                     foreach (var parameter in tmpParameters)
                     {
                         var parameterType = parameter.ParameterType;
                         _dependencies.TryGetValue(parameterType, out var dependencyInstance);
                         dependencyInstances.Add(dependencyInstance);
                         if (dependencyInstance == null)
                         {
                             return null;
                         }
                     }
                     // 用解析的依赖项实例化目标类型
                     return Activator.CreateInstance(controllerType, dependencyInstances.ToArray());
                 }
             }
             else
             {
                 return Activator.CreateInstance(controllerType);
             }
         }
     }*/



}

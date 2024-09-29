using Newtonsoft.Json.Linq;
using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.Web;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;

namespace Serein.Library.Utils
{
    /// <summary>
    /// IOC管理容器
    /// </summary>
    public class SereinIOC/* : ISereinIOC*/
    {
        /// <summary>
        /// 类型集合，暂放待实例化的类型，完成实例化之后移除
        /// </summary>
        private readonly ConcurrentDictionary<string, Type> _typeMappings;

        /// <summary>
        /// 已完成注入的实例集合
        /// </summary>
        private readonly ConcurrentDictionary<string, object> _dependencies;

        /// <summary>
        /// 未完成注入的实例集合。
        /// 键：需要的类型名称
        /// 值：元组（对象实例，对象的属性）
        /// </summary>
        private readonly ConcurrentDictionary<string, List<(object,PropertyInfo)>> _unfinishedDependencies;

        public event IOCMembersChangedHandler OnIOCMembersChanged;

        public SereinIOC()
        {
            // 首先注册自己
            _dependencies = new ConcurrentDictionary<string, object>();
            _typeMappings = new ConcurrentDictionary<string, Type>(); 
            _unfinishedDependencies = new ConcurrentDictionary<string, List<(object, PropertyInfo)>>();
        }

        /// <summary>
        /// 绑定之前进行的默认绑定
        /// </summary>
        public void InitRegister()
        {
            _dependencies[typeof(ISereinIOC).FullName] = this;
            Register<IRouter, Router>();

            //foreach (var type in _typeMappings.Values)
            //{
            //    Register(type);
            //}
            //Build();
        }

        #region 类型的注册

        /// <summary>
        /// 注册类型
        /// </summary>
        /// <param name="type">目标类型</param>
        /// <param name="parameters">参数</param>
        public bool Register(Type type, params object[] parameters)
        {
            return RegisterType(type?.FullName, type);
        }
        /// <summary>
        /// 注册类型
        /// </summary>
        /// <param name="type">目标类型</param>
        /// <param name="parameters">参数</param>
        public bool Register<T>(params object[] parameters)
        {
            var type = typeof(T);
            return RegisterType(type.FullName, type);
        }

        /// <summary>
        /// 注册接口类型
        /// </summary>
        /// <param name="type">目标类型</param>
        /// <param name="parameters">参数</param>
        public bool Register<TService, TImplementation>(params object[] parameters)
            where TImplementation : TService
        {
            return RegisterType(typeof(TService).FullName, typeof(TImplementation));
        }
        #endregion

        /// <summary>
        /// 尝试从容器中获取对象，如果不存在目标类型的对象，则将类型信息登记到容器，并实例化注入依赖项。如果依然无法注册，则返回null。
        /// </summary>
        //public T GetOrRegisterInstantiate<T>()
        //{
        //    return (T)GetOrRegisterInstantiate(typeof(T));
        //}

        ///// <summary>
        ///// 尝试从容器中获取对象，如果不存在目标类型的对象，则将类型信息登记到容器，并实例化注入依赖项。如果依然无法注册，则返回null。
        ///// </summary>
        //public object GetOrRegisterInstantiate(Type type)
        //{
        //    // 尝试从容器中获取对象
        //    if (!_dependencies.TryGetValue(type.FullName, out object value))
        //    {
        //        // 容器中不存在目标类型的对象
        //        if (type.IsInterface)
        //        {
        //            if (_typeMappings.TryGetValue(type.FullName, out Type implementationType))
        //            {
        //                // 是接口类型，存在注册信息
        //                Register(type);// 注册类型信息
        //                value = Instantiate(implementationType); // 创建实例对象，并注入依赖
        //                CustomRegisterInstance(type.FullName, value);// 登记到IOC容器中
        //                _typeMappings.TryRemove(type.FullName, out _); // 取消类型的注册信息
        //            }
        //            else
        //            {
        //                //需要获取接口类型的实例，但不存在类型注册信息
        //                Console.WriteLine("当前需要获取接口，但没有注册实现类的类型，无法创建接口实例");
        //                return  null;
        //            }
        //        }
        //        else
        //        {
        //            // 不是接口，直接注册
        //            Register(type);// 注册类型信息
        //            value = Instantiate(type); // 创建实例对象，并注入依赖
        //            CustomRegisterInstance(type.FullName, value);// 登记到IOC容器中
        //        }
        //    }
        //    return value; 
        //}

        /// <summary>
        /// 用于临时实例的创建，不登记到IOC容器中，依赖项注入失败时也不记录。
        /// </summary>
        /// <param name="controllerType"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public object Instantiate(Type type)
        {
            var constructor = type.GetConstructors().First(); // 获取第一个构造函数
            var parameters = constructor.GetParameters(); // 获取参数列表
            var parameterValues = parameters.Select(param => ResolveDependency(param.ParameterType)).ToArray();
            var instance = Activator.CreateInstance(type, parameterValues);

            //var instance =CreateInstance(controllerType, parameters); //  CreateInstance(controllerType, parameters); // 创建目标类型的实例
            if (instance != null)
            {
                InjectDependencies(instance, false); // 完成创建后注入实例需要的特性依赖项
            }
            return instance;
        }

        public T Instantiate<T>()
        {
            return (T)Instantiate(typeof(T));
        }
        #region 通过名称记录或获取一个实例

        /// <summary>
        /// 指定key值注册一个已经实例化的实例对象
        /// </summary>
        /// <param name="key"></param>
        /// <param name="instance"></param>
        /// <param name="needInjectProperty"></param>
        public void CustomRegisterInstance(string key, object instance, bool needInjectProperty = true)
        {
            // 不存在时才允许创建
            if (!_dependencies.ContainsKey(key))
            {
                _dependencies.TryAdd(key, instance);
            }

            if (needInjectProperty)
            {
                InjectDependencies(instance); // 注入实例需要的依赖项
            }
            InjectUnfinishedDependencies(key, instance); // 检查是否存在其它实例需要该类型
            OnIOCMembersChanged?.Invoke(new IOCMembersChangedEventArgs(key, instance));
        }
        public object Get(Type type)
        {
            return Get(type.FullName);
        }

        public T Get<T>()
        {
            return (T)Get(typeof(T).FullName);
        }
        public T Get<T>(string name)
        {
            return (T)Get(name);
        }
        private object Get(string name)
        {
            if (!_dependencies.TryGetValue(name, out object value))
            {
                value = null;
            }
            return value;
        }

        #endregion

        #region 容器管理（清空，绑定）

        /// <summary>
        /// 清空容器对象
        /// </summary>
        /// <returns></returns>
        public bool Reset()
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
            return true;
        }

        /// <summary>
        /// 实例化所有已注册的类型，并尝试绑定
        /// </summary>
        /// <returns></returns>
        public bool Build2()
        {
            InitRegister(); 
            // 遍历已注册类型
            foreach (var type in _typeMappings.Values.ToArray())
            {
                if (!_dependencies.ContainsKey(type.FullName))
                {
                    var value = CreateInstance(type); // 绑定时注册的类型如果没有创建实例，则创建对应的实例
                    CustomRegisterInstance(type.FullName, value);// 登记到IOC容器中
                }
                _typeMappings.TryRemove(type.FullName, out _); // 移除类型的注册记录
            }

           
            foreach (var instance in _dependencies.Values)
            {
                InjectDependencies(instance);  // 绑定时注入实例的依赖项
            }

            return true;
        }

        public bool Build()
        {
            InitRegister();
            var graph = new Dictionary<string, List<Type>>();
            //var graph = new Dictionary<string, List<string>>();

            // 构建依赖关系图
            foreach (var type in _typeMappings.Values)
            {
                var constructor = type.GetConstructors()
                    .OrderByDescending(c => c.GetParameters().Length)
                    .FirstOrDefault();

                if (constructor != null)
                {
                    var parameters = constructor.GetParameters();
                    foreach (var param in parameters)
                    {
                        var paramTypeName = param.ParameterType.FullName;
                        if (!graph.ContainsKey(paramTypeName))
                        {
                            graph[paramTypeName] = new List<Type>();
                        }
                        graph[paramTypeName].Add(type); // 使用 Type 而不是字符串
                    }
                }
            }

            // 执行拓扑排序
            var sortedTypes = TopologicalSort(graph);

            // 创建实例并注册
            foreach (var type in sortedTypes)
            {
                var typeName = type.FullName;
                if (!_dependencies.ContainsKey(typeName))
                {
                    
                    var value = CreateInstance(type);
                    CustomRegisterInstance(typeName, value);
                    //if (graph.ContainsKey(typeName))
                    //{
                        
                        
                    //}
                    //else
                    //{
                    //    Console.WriteLine("error:"+typeName);
                    //}
                }
                else
                {
                    Console.WriteLine("not create:" + type);
                }
            }

            return true;
        }

        /// <summary>
        /// 执行拓扑排序
        /// </summary>
        /// <param name="graph"></param>
        /// <returns></returns>
        private List<Type> TopologicalSort(Dictionary<string, List<Type>> graph)
        {
            var sorted = new List<Type>();
            var visited = new HashSet<string>();

            void Visit(Type node)
            {
                var nodeName = node.FullName;
                if (visited.Contains(nodeName)) return;
                visited.Add(nodeName);
                if (graph.TryGetValue(nodeName, out var neighbors))
                {
                    foreach (var neighbor in neighbors)
                    {
                        Visit(neighbor);
                    }
                }
                sorted.Add(node);
            }

            foreach (var node in graph.Keys)
            {
                if (!_dependencies.ContainsKey(node))
                {
                    var type = _typeMappings[node]; // 获取对应的 Type
                    Visit(type);
                }
                
            }

            sorted.Reverse(); // 反转以得到正确顺序
            return sorted;
        }
        #endregion

        #region 私有方法


        /// <summary>
        /// 注册类型
        /// </summary>
        /// <param name="typeFull"></param>
        /// <param name="type"></param>
        private bool RegisterType(string typeFull, Type type)
        {
            if (!_typeMappings.ContainsKey(typeFull))
            {
                _typeMappings[typeFull] = type;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 创建实例时，尝试注入到由ioc容器管理、并需要此实例的对象。
        /// </summary>
        private object CreateInstance(Type type)
        {
            var constructor = type.GetConstructors().First(); // 获取第一个构造函数
            var parameters = constructor.GetParameters(); // 获取参数列表
            var parameterValues = parameters.Select(param => ResolveDependency(param.ParameterType)).ToArray();
            var instance = Activator.CreateInstance(type, parameterValues);
            InjectUnfinishedDependencies(type.FullName, instance);
            return instance;
        }

        private object ResolveDependency(Type parameterType)
        {
            var obj = Get(parameterType);
            if (obj is null)
            {
                throw new InvalidOperationException($"构造函数注入时类型[{parameterType}]不存在实例");
            }
            return obj;
        }

        /// <summary>
        /// 如果其它实例想要该对象时，注入过去
        /// </summary>
        private void InjectUnfinishedDependencies(string key,object instance)
        {
            if (_unfinishedDependencies.TryGetValue(key, out var unfinishedPropertyList))
            {
                foreach ((object obj, PropertyInfo property) in unfinishedPropertyList)
                {
                    property.SetValue(obj, instance); //注入依赖项
                }

                if (_unfinishedDependencies.TryRemove(key, out unfinishedPropertyList))
                {
                    unfinishedPropertyList.Clear();
                }
            }
        }


        /// <summary>
        /// 注入目标实例的依赖项
        /// </summary>
        /// <param name="instance">实例</param>
        /// <param name="isRecord">未完成依赖项注入时是否记录</param>
        private bool InjectDependencies(object instance,bool isRecord = true)
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
                else if(isRecord)
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
        //public bool Run<T>(string name, Action<T> action)
        //{
        //    var obj  = Get(name);
        //    if (obj != null)
        //    {
        //        if(obj is T service)
        //        {
        //            try
        //            {
        //                action(service);
        //            }
        //            catch (Exception ex)
        //            {
        //                Console.WriteLine(ex.Message);
        //            }
        //        }
        //    }
        //    return this;
        //}


        public void Run<T>(Action<T> action)
        {
            var service = Get<T>();
            if (service != null)
            {
                action(service);
            }
        }

        public void Run<T1, T2>(Action<T1, T2> action)
        {
            var service1 = Get<T1>();
            var service2 = Get<T2>();

            action(service1, service2);
        }

        public void Run<T1, T2, T3>(Action<T1, T2, T3> action)
        {
            var service1 = Get<T1>();
            var service2 = Get<T2>();
            var service3 = Get<T3>();
            action(service1, service2, service3);
        }

        public void Run<T1, T2, T3, T4>(Action<T1, T2, T3, T4> action)  
        {
            var service1 = Get<T1>();
            var service2 = Get<T2>();
            var service3 = Get<T3>();
            var service4 = Get<T4>();
            action(service1, service2, service3, service4);
        }

        public void Run<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> action)
        {
            var service1 = Get<T1>();
            var service2 = Get<T2>();
            var service3 = Get<T3>();
            var service4 = Get<T4>();
            var service5 = Get<T5>();
            action(service1, service2, service3, service4, service5);
        }

        public void Run<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> action)
        {
            var service1 = Get<T1>();
            var service2 = Get<T2>();
            var service3 = Get<T3>();
            var service4 = Get<T4>();
            var service5 = Get<T5>();
            var service6 = Get<T6>();
            action(service1, service2, service3, service4, service5, service6);
        }

        public void Run<T1, T2, T3, T4, T5, T6, T7>(Action<T1, T2, T3, T4, T5, T6, T7> action)
        {
            var service1 = Get<T1>();
            var service2 = Get<T2>();
            var service3 = Get<T3>();
            var service4 = Get<T4>();
            var service5 = Get<T5>();
            var service6 = Get<T6>();
            var service7 = Get<T7>();
            action(service1, service2, service3, service4, service5, service6, service7);
        }

        public void Run<T1, T2, T3, T4, T5, T6, T7, T8>(Action<T1, T2, T3, T4, T5, T6, T7, T8> action)
        {
            var service1 = Get<T1>();
            var service2 = Get<T2>();
            var service3 = Get<T3>();
            var service4 = Get<T4>();
            var service5 = Get<T5>();
            var service6 = Get<T6>();
            var service7 = Get<T7>();
            var service8 = Get<T8>();
            action(service1, service2, service3, service4, service5, service6, service7, service8);
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

using Serein.Library.Api;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

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
        private readonly ConcurrentDictionary<string, object[]> _registerParameterss;

        /// <summary>
        /// 未完成注入的实例集合。
        /// 键：需要的类型名称
        /// 值：元组（对象实例，对象的属性）
        /// </summary>
        private readonly ConcurrentDictionary<string, List<(object,PropertyInfo)>> _unfinishedDependencies;

        public event IOCMembersChangedHandler OnIOCMembersChanged;

        public SereinIOC()
        {
            _dependencies = new ConcurrentDictionary<string, object>();
            _registerParameterss = new ConcurrentDictionary<string, object[]>();
            _typeMappings = new ConcurrentDictionary<string, Type>(); 

            _unfinishedDependencies = new ConcurrentDictionary<string, List<(object, PropertyInfo)>>();
        }



        #region 类型的注册

        /// <summary>
        /// 注册类型
        /// </summary>
        /// <param name="type">目标类型</param>
        /// <param name="parameters">参数</param>
        public bool Register(Type type, params object[] parameters)
        {
            return RegisterType(type?.FullName, type, parameters);
        }
        /// <summary>
        /// 注册类型
        /// </summary>
        /// <param name="type">目标类型</param>
        /// <param name="parameters">参数</param>
        public bool Register<T>(params object[] parameters)
        {
            var type = typeof(T);
            return RegisterType(type.FullName, type, parameters);
        }

        /// <summary>
        /// 注册接口类型
        /// </summary>
        /// <param name="type">目标类型</param>
        /// <param name="parameters">参数</param>
        public bool Register<TService, TImplementation>(params object[] parameters)
            where TImplementation : TService
        {
            return RegisterType(typeof(TService).FullName, typeof(TImplementation), parameters);
        }
        #endregion

        /// <summary>
        /// 用于临时实例的创建，不登记到IOC容器中，依赖项注入失败时也不记录。
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public object Instantiate(Type type)
        {
            var constructor = type.GetConstructors().First(); // 获取第一个构造函数
            var parameters = constructor.GetParameters(); // 获取参数列表
            var parameterValues = parameters.Select(param => ResolveDependency(param.ParameterType)).ToArray(); // 生成创建类型的入参参数
            var instance = Activator.CreateInstance(type, parameterValues); // 创建实例
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
        public bool CustomRegisterInstance(string key, object instance, bool needInjectProperty = true)
        {
            // 不存在时才允许创建
            if (_dependencies.ContainsKey(key))
            {
                return false;
            }
            _dependencies.TryAdd(key, instance);
            if (needInjectProperty)
            {
                InjectDependencies(instance); // 注入实例需要的依赖项
            }
            InjectUnfinishedDependencies(key, instance); // 检查是否存在其它实例需要该类型
            OnIOCMembersChanged?.Invoke(new IOCMembersChangedEventArgs(key, instance));
            return true;
        }
        public object Get(Type type)
        {
            var instance = Get(type.FullName);
            if(instance is null)
            {
                SereinEnv.WriteLine(InfoType.INFO, "类型没有注册：" + type.FullName);
            }
            
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
            _registerParameterss?.Clear();
            _unfinishedDependencies?.Clear();
            _typeMappings?.Clear();
            _dependencies?.Clear();
            return true;
        }
        public class TypeKeyValue
        {
            public TypeKeyValue(string name, Type type)
            {
                this.Type = type;
                this.Name = name;
            }
            public string Name { get; set; }
            public Type Type { get; set; }
        }
        private const string FlowBaseClassName = "<>$FlowBaseClass!@#";


        public Dictionary<string, List<string>> BuildDependencyTree()
        {
            var dependencyMap = new Dictionary<string, HashSet<string>>();
            dependencyMap[FlowBaseClassName] = new HashSet<string>();
            foreach (var typeMapping in _typeMappings)
            {
                //var constructor = GetConstructorWithMostParameters(typeMapping.Value); // 获取参数最多的构造函数

                var constructors = GetConstructor(typeMapping.Value); // 获取参数最多的构造函数

                foreach (var constructor in constructors)
                {
                    if (constructor != null)
                    {
                        var parameters = constructor.GetParameters()
                            .Select(p => p.ParameterType)
                            .ToList();
                        if (parameters.Count == 0) // 无参的构造函数
                        {
                            var type = typeMapping.Value;
                            if (!dependencyMap[FlowBaseClassName].Contains(type.FullName))
                            {
                                dependencyMap[FlowBaseClassName].Add(type.FullName);
                            }
                        }
                        else
                        {
                            // 从类型的有参构造函数中提取类型
                            foreach (var param in parameters)
                            {
                                if (!dependencyMap.TryGetValue(param.FullName, out var hashSet))
                                {
                                    hashSet = new HashSet<string>();
                                    hashSet.Add(typeMapping.Key);
                                    dependencyMap.Add(param.FullName, hashSet);
                                }
                                else
                                {
                                    if (!hashSet.Contains(typeMapping.Key))
                                    {
                                        hashSet.Add(typeMapping.Key);
                                    }
                                }

                            }
                        }
                    }
                }

            }
            var tmp = dependencyMap.ToDictionary(key => key.Key, value => value.Value.ToList());
            return tmp;
        }
        // 获取参数最多的构造函数
        private ConstructorInfo GetConstructorWithMostParameters(Type type)
        {
            return type.GetConstructors()
                       .OrderByDescending(c => c.GetParameters().Length)
                       .FirstOrDefault();
        }
        // 获取所有构造函数
        private ConstructorInfo[] GetConstructor(Type type)
        {
            return type.GetConstructors()
                       //.OrderByDescending(c => c.GetParameters().Length)
                       .OrderByDescending(ctor => ctor.GetParameters().Length).ToArray();
        }

        // 生成顺序
        public List<string> GetCreationOrder(Dictionary<string, List<string>> dependencyMap)
        {
            var graph = new Dictionary<string, List<string>>();
            var indegree = new Dictionary<string, int>();

            foreach (var entry in dependencyMap)
            {
                var key = entry.Key;
                if (!graph.ContainsKey(key))
                {
                    graph[key] = new List<string>();
                }

                foreach (var dependent in entry.Value)
                {
                    if (!graph.ContainsKey(dependent))
                    {
                        graph[dependent] = new List<string>();
                    }
                    graph[key].Add(dependent);

                    // 更新入度
                    if (!indegree.ContainsKey(dependent))
                    {
                        indegree[dependent] = 0;
                    }
                    indegree[dependent]++;
                }

                if (!indegree.ContainsKey(key))
                {
                    indegree[key] = 0;
                }
            }

            // 拓扑排序
            var creationOrder = new List<string>();
            var queue = new Queue<string>(indegree.Where(x => x.Value == 0).Select(x => x.Key));

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                creationOrder.Add(current);
                foreach (var neighbor in graph[current])
                {
                    indegree[neighbor]--;
                    if (indegree[neighbor] == 0)
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }
           
            var tmpList = indegree.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
            if (tmpList.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("以下类型可能产生循环依赖，请避免循环依赖，如果确实需要循环引用，请使用 [AutoInjection] 特性注入属性");
                foreach (var kv in tmpList)
                {
                    sb.AppendLine($"Class Name : {kv}");
                }
                SereinEnv.WriteLine(InfoType.ERROR, sb.ToString());
            }
            
            return creationOrder;
        }

        public object CreateInstance(string typeName)
        {
            if (!_typeMappings.TryGetValue(typeName, out var type))
            {
                return null;
            }
            if (_dependencies.TryGetValue(typeName, out var instance))
            {
                return instance;
            }
            if (_registerParameterss.TryGetValue(typeName,out var @params))
            {
                instance = Activator.CreateInstance(type, @params);
            }

            // 字符串、值类型，抽象类型，暂时不支持自动创建
            if (type == typeof(string) || type.IsValueType || type.IsAbstract)
            {
                return null;
            }
            
            else
            {
                // 没有显示指定构造函数入参，选择参数最多的构造函数
                //var constructor = GetConstructorWithMostParameters(type);
                var constructors = GetConstructor(type); // 获取参数最多的构造函数

                foreach(var constructor in constructors)
                {
                    var parameters = constructor.GetParameters();
                    var args = new object[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var argType = parameters[i].ParameterType;
                        var fullName = parameters[i].ParameterType.FullName;
                        if (!_dependencies.TryGetValue(fullName, out var argObj))
                        {
                            if (!_typeMappings.ContainsKey(fullName))
                            {
                                _typeMappings.TryAdd(fullName, argType);
                            }
                            argObj = CreateInstance(fullName);
                            if (argObj is null)
                            {
                                SereinEnv.WriteLine(InfoType.WARN, "构造参数创建失败"); 
                                continue;
                            }
                        }
                        args[i] = argObj;
                    }
                    try
                    {
                        instance = Activator.CreateInstance(type, args);
                        if(instance != null)
                        {
                            break;
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }

              
            }

            InjectDependencies(instance); // 完成创建后注入实例需要的特性依赖项
            _dependencies[typeName] = instance;
            return instance;
        }


        public bool Build()
        {
            var dependencyTree = BuildDependencyTree();
            var creationOrder = GetCreationOrder(dependencyTree);

            // 输出创建顺序
            Debug.WriteLine("创建顺序: " + string.Join(" → ", creationOrder));

            // 创建对象
            foreach (var typeName in creationOrder)
            {
                if (_dependencies.ContainsKey(typeName))
                {
                    continue;
                }
                var value = CreateInstance(typeName);
                if(value is null)
                {
                    continue;
                }
                _dependencies[typeName] = value;
                OnIOCMembersChanged.Invoke(new IOCMembersChangedEventArgs(typeName, value));
            }
            _typeMappings.Clear();
            return true;

        }

        
        #endregion

        #region 私有方法


        /// <summary>
        /// 注册类型
        /// </summary>
        /// <param name="typeFull"></param>
        /// <param name="type"></param>
        private bool RegisterType(string typeFull, Type type, params object[] parameters)
        {
            if (!_typeMappings.ContainsKey(typeFull))
            {
                _typeMappings[typeFull] = type;
                if(parameters.Length > 0)
                {
                    _registerParameterss[typeFull] = parameters;
                }
                return true;
            }
            else
            {
                return false;
            }
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
                else if( isRecord )
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

       
        public void Run<T>(Action<T> action)
        {
            var service = Get<T>();
            action(service);
           
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

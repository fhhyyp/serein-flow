using Serein.Library.Api;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Serein.Library.Utils
{
    /// <summary>
    /// 指示IOC扫描构造函数时的行为
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor)]
    public sealed class SereinIOCCtorAttribute : Attribute
    {
        /// <summary>
        /// 忽略该构造函数
        /// </summary>
        public bool IsIgnore = false;
    }
    
    /// <summary>
    /// 一个轻量级的单例IOC容器
    /// </summary>
    public class SereinIOC : ISereinIOC
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
        /// 能够获取类型实例的闭包
        /// </summary>
        private readonly ConcurrentDictionary<string, Func<object>> _registerCallback;


        /// <summary>
        /// 未完成注入的实例集合。
        /// 键：需要的类型名称
        /// 值：元组（对象实例，对象的属性）
        /// </summary>
        private readonly ConcurrentDictionary<string, List<(object,PropertyInfo)>> _unfinishedDependencies;

        /// <summary>
        /// IOC容器成功创建了类型
        /// </summary>
        public event IOCMembersChangedHandler OnIOCMembersChanged;

        /// <summary>
        /// 一个轻量级的D单例IOC容器
        /// </summary>
        public SereinIOC()
        {
            _dependencies = new ConcurrentDictionary<string, object>();
            _registerCallback = new ConcurrentDictionary<string, Func<object>>();
            _typeMappings = new ConcurrentDictionary<string, Type>(); 
            _unfinishedDependencies = new ConcurrentDictionary<string, List<(object, PropertyInfo)>>();
        }

        #region 类型的注册
        /// <summary>
        /// 向容器注册类型
        /// </summary>
        /// <param name="type">需要注册的类型</param>
        /// <returns></returns>
        public ISereinIOC Register(Type type)
        {
            RegisterType(type.FullName, type);
            return this;
        }

        /// <summary>
        /// 向容器注册类型，并指定其实例成员
        /// </summary>
        /// <param name="type">需要注册的类型</param>
        /// <param name="getInstance">获取实例的回调函数</param>
        /// <returns></returns>
        public ISereinIOC Register(Type type, Func<object> getInstance)
        {
            RegisterType(type.FullName, type, getInstance);
            return this;
        }


        /// <summary>
        /// 向容器注册类型，并指定其实例成员
        /// </summary>
        /// <typeparam name="T">需要注册的类型</typeparam>
        /// <returns></returns>
        public ISereinIOC Register<T>()
        {
            var type = typeof(T);
            RegisterType(type.FullName, type);
            return this;
        }

        
        /// <summary>
        /// 向容器注册类型，并指定其实例成员
        /// </summary>
        /// <typeparam name="T">需要注册的类型</typeparam>
        /// <param name="getInstance">获取实例的回调函数</param>
        /// <returns></returns>
        public ISereinIOC Register<T>(Func<T> getInstance)
        {
            var type = typeof(T);
            RegisterType(type.FullName, type, () => getInstance.Invoke());
            return this;
        }


        /// <summary>
        /// 向容器注册接口类型，并指定其实例成员
        /// </summary>
        /// <typeparam name="TService">接口类型</typeparam>
        /// <typeparam name="TImplementation">实现类类型</typeparam>
        /// <param name="getInstance">获取实例的方法</param>
        /// <returns></returns>
        public ISereinIOC Register<TService, TImplementation>(Func<TService> getInstance)
            where TImplementation : TService
        {
           
            RegisterType(typeof(TService).FullName, typeof(TImplementation), () => getInstance.Invoke());
            return this;
        }

        /// <summary>
        /// 向容器注册接口类型，其实例成员由容器自动创建
        /// </summary>
        /// <typeparam name="TService">接口类型</typeparam>
        /// <typeparam name="TImplementation">实现类类型</typeparam>
        /// <returns></returns>
        public ISereinIOC Register<TService, TImplementation>()
            where TImplementation : TService
        {
            
            RegisterType(typeof(TService).FullName, typeof(TImplementation));
            return this;
        }


        #endregion

        #region 实例的创建
        /// <summary>
        /// 用于临时实例的创建，不登记到IOC容器中，依赖项注入失败时也不记录。
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public object CreateObject(Type type)
        {
            var ctors = GetConstructor(type); // 获取构造函数
            object instance = null;
            // 从入参最多的构造函数开始构建对象
            foreach (var ctor in ctors)
            {

                var parameters = ctor.GetParameters(); // 获取构造函数参数列表
                var parametersNames = parameters.Select(p => $"{p.ParameterType} {p.Name}");
                var parametersName = string.Join(", ", parametersNames);
                try
                {
                    var parameterValues = parameters.Select(param => Get(param.ParameterType)).ToArray(); // 生成创建类型的入参参数
                    instance = Activator.CreateInstance(type, parameterValues); // 创建实例
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    SereinEnv.WriteLine(InfoType.INFO, $"在【{type}】类型上使用ctor({parametersName})构造函数时创建对象失败。错误信息：{ex.Message}");
                    continue;
                }
                InjectDependencies(instance, false); // 完成创建后注入实例需要的特性依赖项
                break;
            }
            if (instance == null)
            {
                throw new Exception($"无法为【{type}】类型创建实例");
            }
            return instance;
        }

        /// <summary>
        /// 用于临时实例的创建，不登记到IOC容器中，依赖项注入失败时也不记录。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T CreateObject<T>()
        {
            return (T)CreateObject(typeof(T));
        }


        /// <summary>
        /// 给定一个实例，尽可能地在该实例中具有[AutoInjection]特性的属性/字段上，设置为IOC容器中已有的对应类型的对象。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance"></param>
        /// <returns></returns>
        public T InjectDependenciesProperty<T>(T instance)
        {   
            var type = instance.GetType();
            var propertys = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                .Where(p => p.CanWrite // 可写属性
                                      && p.GetCustomAttribute<AutoInjectionAttribute>() is not null // 有特性标注需要注入
                                      && p.GetValue(instance) is null); // 属性为空
            
            // 属性注入
            foreach (var property in propertys)
            {
                var propertyType = property.PropertyType;
                if (_dependencies.TryGetValue(propertyType.FullName, out var dependencyInstance))
                {
                    property.SetValue(instance, dependencyInstance); // 尝试写入到目标实例的属性中
                }
            }


            // 字段注入
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic )
                             .Where(f =>  f.GetCustomAttribute<AutoInjectionAttribute>() != null
                                      && f.GetValue(instance) == null);

            foreach (var field in fields)
            {
                var fieldType = field.FieldType;
                if (_dependencies.TryGetValue(fieldType.FullName, out var dependencyInstance))
                {
                    field.SetValue(instance, dependencyInstance);
                }
            }

            return instance;
        }

        #endregion

        #region 通过名称记录或获取一个实例

        /// <summary>
        /// 尝试获取指定类型的示例
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public object Get(Type type)
        {
            var instance = Get(type.FullName);
            if(instance is null)
            {
                SereinEnv.WriteLine(InfoType.INFO, "类型没有注册：" + type.FullName);
            }
            
            return Get(type.FullName);
        }

        /// <summary>
        /// 尝试获取指定类型的示例
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T Get<T>()
        {
            return (T)Get(typeof(T).FullName);
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
            _registerCallback?.Clear();
            _unfinishedDependencies?.Clear();
            _typeMappings?.Clear();
            _dependencies?.Clear();
            return this;
        }

        class TypeKeyValue
        {
            public TypeKeyValue(string name, Type type)
            {
                this.Type = type;
                this.Name = name;
            }
            public string Name { get; set; }
            public Type Type { get; set; }
        }


        private const string IOC_MAIN = "*Priority Instantiation*";

        /// <summary>
        /// <para>遍历所有需要注册的类型，获取到它们所有构造函数，并统计每个构造函数的入参类型，构建依赖关系树</para>
        /// </summary>
        /// <returns>“ID-PID”关系的树形结构</returns>
        private Dictionary<string, List<string>> BuildDependencyTree()
        {
            var dependencyMap = new Dictionary<string, HashSet<string>>();
            dependencyMap[IOC_MAIN] = new HashSet<string>(); // 优先实例化
            foreach (var typeMapping in _typeMappings)
            {
                var typeFullName = typeMapping.Key; // 注册的类型 FullName
                var type = typeMapping.Value; // 对应的Type。如果是以接口形式注册，typeFullName将是接口类的FullName，而type将是接口实现类。
                var constructors = GetConstructor(type); // 获取构造函数
                if(constructors .Length == 0)
                {
                    if (!dependencyMap[IOC_MAIN].Contains(type.FullName))
                    {
                        //dependencyMap[IOC_MAIN].Add(type.FullName);
                        dependencyMap[IOC_MAIN].Add(typeFullName);
                    }
                }
                foreach (var constructor in constructors)
                {
                    if (constructor is null)
                    {
                        continue;
                    }
                    var parameters = constructor.GetParameters().Select(p => p.ParameterType);
                    var ctorCount = constructors.Length;
                    var ctorParamCount = parameters.Count();

                    // 类型仅有一个构造函数，并且无参，将优先实例化
                    if (ctorCount == 1 && ctorParamCount == 0)
                    {
                        if (!dependencyMap[IOC_MAIN].Contains(type.FullName))
                        {
                            //dependencyMap[IOC_MAIN].Add(type.FullName);
                            dependencyMap[IOC_MAIN].Add(typeFullName);
                        }
                        continue;
                    }

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
            var tmp = dependencyMap.ToDictionary(key => key.Key, value => value.Value.ToList());
            return tmp;
        }

        /// <summary>
        ///  获取类型的所有构造函数，根据入参数量，由多到少排列
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private ConstructorInfo[] GetConstructor(Type type)
        {
            return type.GetConstructors().Where(ctor =>
                {
                    var attribute = ctor.GetCustomAttribute<SereinIOCCtorAttribute>();
                    if (attribute is null)
                    {
                        return true;
                    }
                    else
                    {
                        return !attribute.IsIgnore;
                    }
                })
                .OrderByDescending(ctor => ctor.GetParameters().Length).ToArray();
        }

        /// <summary>
        /// 创建实例的生成顺序
        /// </summary>
        /// <param name="dependencyMap">依赖关系树</param>
        /// <returns></returns>
        public List<string> GetCreationOrder(Dictionary<string, List<string>> dependencyMap)
        {
            var graph = new Dictionary<string, List<string>>(); // 另一种依赖关系树
            var indegree = new Dictionary<string, int>(); // 表示出现次数

            foreach (var entry in dependencyMap)
            {

                // “rootNode”是注册类的类型FullName属性
                var rootNode = entry.Key; // 根节点

                if (!graph.ContainsKey(rootNode))
                {
                    graph[rootNode] = new List<string>();
                }

                // “childNode”是注册类构造函数中出现过的参数的类型FullName属性
                foreach (var childNode in entry.Value)
                {
                    if (!graph.ContainsKey(childNode))
                    {
                        graph[childNode] = new List<string>();
                    }
                    graph[rootNode].Add(childNode);

                    // 更新入度
                    if (!indegree.ContainsKey(childNode))
                    {
                        indegree[childNode] = 0;
                    }
                    indegree[childNode]++;
                }

                if (!indegree.ContainsKey(rootNode))
                {
                    indegree[rootNode] = 0;
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
                sb.Append("以下类型存在循环依赖，请避免循环依赖，如果确实需要循环引用，请使用 [AutoInjection] 特性注入属性");
                foreach (var kv in tmpList)
                {
                    sb.AppendLine($"类名 : {kv}");
                }
                SereinEnv.WriteLine(InfoType.ERROR, sb.ToString());
            }
            
            return creationOrder;
        }

        /// <summary>
        /// 创建实例对象
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        private object CreateInstance(string typeName)
        {

            if (!_typeMappings.TryGetValue(typeName, out var type)) // 获取类型
            {
                return null;
            }
            if (_dependencies.TryGetValue(typeName, out var instance)) // 获取实例
            {
                return instance;
            }
            if (_registerCallback.TryGetValue(typeName,out var getInstance))
            {
                return getInstance.Invoke();
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
                var constructors = GetConstructor(type); // 获取构造函数
                if(constructors.Length == 0)
                {
                    return null;
                }
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

                                argObj = CreateInstance(fullName);
                                throw new Exception("构造参数创建失败");
                            }
                        }
                        args[i] = argObj;
                    }
                    try
                    {
                        instance = Activator.CreateInstance(type, args);
                        if(instance != null)
                        {
                            break; // 构建完成退出
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

        /// <summary>
        /// 绑定所有类型，生成示例
        /// </summary>
        /// <returns></returns>
        public ISereinIOC Build()
        {
            Dictionary<string, List<string>> dependencyTree = BuildDependencyTree(); // 生成类型依赖关系
            List<string> creationOrder = GetCreationOrder(dependencyTree); // 生成创建顺序

            // 输出创建顺序
            Debug.WriteLine("创建顺序: " + string.Join($"{Environment.NewLine}↓ {Environment.NewLine}", creationOrder));

            // 创建对象
            foreach (var typeName in creationOrder)
            {
                if (typeName.Equals("Serein.Library.LightweightFlowEnvironment"))
                {

                }

                if (_registerCallback.TryGetValue(typeName, out var registerCallback))
                {
                    
                }
                if (_dependencies.ContainsKey(typeName))
                {
                    continue;
                }
                if(typeName == IOC_MAIN)
                {
                    continue;
                }
                var value = CreateInstance(typeName);

                if (value is null)
                {
                    SereinEnv.WriteLine(InfoType.ERROR, $"IOC容器无法创建对象：{typeName}");
                    continue;
                }
                _dependencies[typeName] = value;
                OnIOCMembersChanged?.Invoke(new IOCMembersChangedEventArgs(typeName, value));

            }
            _typeMappings.Clear();
            return this;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 注册类型
        /// </summary>
        /// <param name="typeFull">类型名称</param>
        /// <param name="type">要注册的类型</param>
        /// <param name="getInstance">获取实例的闭包</param>
        private bool RegisterType(string typeFull, Type type, Func<object> getInstance = null)
        {
            if (!_typeMappings.ContainsKey(typeFull))
            {
                _typeMappings[typeFull] = type;
                if(getInstance != null)
                {
                    _registerCallback[typeFull] = getInstance;
                }
                return true;
            }
            else
            {
                return false;
            }
        }


        /// <summary>
        /// 如果其它实例想要该对象时，注入过去
        /// </summary>
        private void InjectUnfinishedDependencies(string key, object instance)
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
        private bool InjectDependencies(object instance, bool isRecord = true)
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

        #endregion

        #region 运行

        /// <summary>
        /// 运行一个方法，方法的参数类型由IOC容器提供
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="action"></param>
        /// <returns></returns>
        public ISereinIOC Run<T>(Action<T> action)
        {
            var service = Get<T>();
            action(service);
            return this;
        }

        /// <summary>
        /// 运行一个方法，方法的参数类型由IOC容器提供
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="action"></param>
        /// <returns></returns>
        public ISereinIOC Run<T1, T2>(Action<T1, T2> action)
        {
            var service1 = Get<T1>();
            var service2 = Get<T2>();

            action(service1, service2); 
            return this;
        }

        /// <summary>
        /// 运行一个方法，方法的参数类型由IOC容器提供
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <param name="action"></param>
        /// <returns></returns>
        public ISereinIOC Run<T1, T2, T3>(Action<T1, T2, T3> action)
        {
            var service1 = Get<T1>();
            var service2 = Get<T2>();
            var service3 = Get<T3>();
            action(service1, service2, service3);
            return this;
        }

        /// <summary>
        ///  运行一个方法，方法的参数类型由IOC容器提供
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <typeparam name="T4"></typeparam>
        /// <param name="action"></param>
        /// <returns></returns>
        public ISereinIOC Run<T1, T2, T3, T4>(Action<T1, T2, T3, T4> action)  
        {
            var service1 = Get<T1>();
            var service2 = Get<T2>();
            var service3 = Get<T3>();
            var service4 = Get<T4>();
            action(service1, service2, service3, service4);
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

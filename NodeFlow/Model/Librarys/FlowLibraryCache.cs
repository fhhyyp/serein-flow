using Serein.Library;
using Serein.NodeFlow.Tool;
using System.Globalization;
using System.Reflection;

namespace Serein.NodeFlow.Model.Library
{


    /// <summary>
    /// 加载在流程中的程序集依赖
    /// </summary>
    public class FlowLibraryCache
    {
        /// <summary>
        /// 通过程序集创建一个流程库实例
        /// </summary>
        /// <param name="assembly"></param>
        public FlowLibraryCache(Assembly assembly)
        {
            Assembly = assembly;
            FullName = Path.GetFileName(Assembly.Location);
            FilePath = Assembly.Location;
        }

        /// <summary>
        /// 通过动态程序集和文件路径创建一个流程库实例
        /// </summary>
        /// <param name="dynamicAssembly"></param>
        /// <param name="filePath"></param>
        public FlowLibraryCache(Assembly dynamicAssembly,
                          string filePath)
        {
            Assembly = dynamicAssembly;
            FullName = Path.GetFileName(filePath); ;
            FilePath = filePath;
        }

        /// <summary>
        /// 程序集本身
        /// </summary>
        public Assembly Assembly { get; private set; }

        /// <summary>
        /// 程序集全名
        /// </summary>
        public string FullName { get; private set; }

        /// <summary>
        /// 程序集文件路径
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// 加载程序集时创建的方法描述
        /// Key   ： 方法名称
        /// Value ：方法详情
        /// </summary>
        public Dictionary<string, MethodDetails> MethodDetailss { get; } = new Dictionary<string, MethodDetails>();

        /// <summary>
        /// 加载程序集时创建的方法信息
        /// </summary>
        public Dictionary<string, MethodInfo> MethodInfos { get; } = new Dictionary<string, MethodInfo>();

        /// <summary>
        /// <para>缓存节点方法通Emit委托</para>
        /// <para>Key   ：方法名称</para>
        /// <para>Value ：方法详情</para>
        /// </summary>
        public Dictionary<string, DelegateDetails> DelegateDetailss { get; } = new Dictionary<string, DelegateDetails>();

        /// <summary>
        /// 用于流程启动时,在不同阶段(Init_Loading_Loaded)需要创建实例的类型信息
        /// </summary>
        public Dictionary<RegisterSequence, List<Type>> RegisterTypes { get; } = new Dictionary<RegisterSequence, List<Type>>();


        /// <summary>
        /// 动态加载程序集
        /// </summary>
        /// <returns></returns>
        public bool LoadFlowMethod()
        {
            Assembly assembly = Assembly;

            #region 检查入参

            // 加载DLL，创建 MethodDetails、实例作用对象、委托方法
            var assemblyName = assembly.GetName().Name;
            if (string.IsNullOrEmpty(assemblyName)) // 防止动态程序集没有定义程序集名称 
            {
                return false;
            }
            List<Type> types;
            try
            {
                types = assembly.GetTypes().ToList(); // 获取程序集中的所有类型
                if (types.Count <= 0) 
                {
                    return false;
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                // 获取加载失败的类型
                var loaderExceptions = ex.LoaderExceptions;
                foreach (var loaderException in loaderExceptions)
                {
                    SereinEnv.WriteLine(InfoType.ERROR, "加载失败 : " + loaderException?.Message);
                }
                return false;
            }


            #endregion
                
            #region 获取 DynamicFlow 特性的流程控制器，如果没有退出
            // Type   ： 具有 DynamicFlowAttribute 标记的类型
            // string ： 类型元数据 DynamicFlowAttribute 特性中的 Name 属性 （用于生成方法描述时，添加在方法别名中提高可读性）
            List<(Type Type, string Name)> scanTypes = new List<(Type Type, string Name)>();

            // (Type, string)
            // Type   ： 具有 DynamicFlowAttribute 标记的类型
            // string ： 类型元数据 DynamicFlowAttribute 特性中的 Name 属性

            types = types.Where(type => type.GetCustomAttribute<DynamicFlowAttribute>() is DynamicFlowAttribute df && df.Scan).ToList();

            foreach (var type in types)
            {
                if (type.GetCustomAttribute<DynamicFlowAttribute>() is DynamicFlowAttribute dynamicFlowAttribute)
                {
                    scanTypes.Add((type, dynamicFlowAttribute.Name));
                }
            }
            if (scanTypes.Count == 0)
            {
                // 类型没有流程控制器
                return false;
            }
            #endregion

            #region 创建对应的方法元数据
            // 从 scanTypes.Type 创建的方法信息
            // Md : 方法描述
            // Dd ：方法对应的Emit委托
            List<LibraryMthodInfo> detailss = new List<LibraryMthodInfo>();

            // 遍历扫描的类型
            foreach ((var type, var flowName) in scanTypes)
            {
                var methodInfos = NodeMethodDetailsHelper.GetMethodsToProcess(type);
                foreach (var methodInfo in methodInfos) // 遍历流程控制器类型中的方法信息
                {
                    // 尝试创建
                    if (!NodeMethodDetailsHelper.TryCreateDetails(type, methodInfo, assemblyName,
                                                                   out var mi, out var md, out var dd)) // 返回的描述
                    {
                        SereinEnv.WriteLine(InfoType.ERROR, $"无法加载方法信息：{assemblyName}-{type}-{methodInfo}");
                        continue;
                    }
                    md.MethodAnotherName = flowName + md.MethodAnotherName; // 方法别名
                    detailss.Add(new LibraryMthodInfo(mi, md, dd));
                }
            }

            #endregion

            #region 检查是否成功加载，如果成功，则真正写入到缓存的集合中
            if (detailss.Count == 0)
            {
                return false;
            }

            // 简单排序一下
            detailss.Sort((a, b) => string.Compare(a.MethodDetails.MethodName, b.MethodDetails.MethodName, StringComparison.OrdinalIgnoreCase));

            foreach (var item in detailss)
            {
                SereinEnv.WriteLine(InfoType.INFO, "加载方法 : " + item.MethodDetails.MethodName);

            }
            
            #region 加载成功，缓存所有方法、委托的信息
            foreach (var item in detailss)
            {
                var key = item.MethodDetails.MethodName;
                MethodDetailss.TryAdd(key, item.MethodDetails);
                MethodInfos.TryAdd(key, item.MethodInfo);
                DelegateDetailss.TryAdd(key, item.DelegateDetails);
            }

            #endregion
            #region 加载成功，开始获取并记录所有需要自动实例化的类型（在流程启动时）
            foreach (Type type in types)
            {
                if (type.GetCustomAttribute<AutoRegisterAttribute>() is AutoRegisterAttribute attribute)
                {
                    if (!RegisterTypes.TryGetValue(attribute.Class, out var valus))
                    {
                        valus = new List<Type>();
                        RegisterTypes.TryAdd(attribute.Class, valus);
                    }
                    valus.Add(type);
                }
            }
            #endregion

            #endregion

            return true;
        }


        /// <summary>
        /// 卸载当前程序集以及附带的所有信息
        /// </summary>
        public void Unload()
        {
            DelegateDetailss.Clear();
            MethodInfos.Clear();
            RegisterTypes.Clear();
            MethodDetailss.Clear();
        }

        /// <summary>
        /// 转为依赖信息
        /// </summary>
        /// <returns></returns>
        public FlowLibraryInfo ToInfo()
        {
            var assemblyName = Assembly.GetName().Name;
            var mdInfos = MethodDetailss.Values.Select(x => x.ToInfo())
                                        .OrderBy(d => d.AssemblyName)
                                        .ThenBy(s => s.MethodAnotherName, StringComparer.Create(CultureInfo.GetCultureInfo("zh-cn"), true))
                                        .ToList();

            return new FlowLibraryInfo
            {
                AssemblyName = assemblyName,
                FileName = FullName,
                FilePath = FilePath,
                MethodInfos = mdInfos,
            };
        }


    }

}

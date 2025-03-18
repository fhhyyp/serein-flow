using Serein.Library;
using Serein.Library.Utils;
using Serein.NodeFlow.Tool;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Serein.NodeFlow
{
    public class LibraryMdDd (MethodDetails methodDetails,DelegateDetails delegateDetails)
    {
        public MethodDetails MethodDetails { get;  } = methodDetails;
        public DelegateDetails DelegateDetails { get; } = delegateDetails;
    }


    /// <summary>
    /// 加载在流程中的程序集依赖
    /// </summary>
    public class FlowLibrary
    {
        private readonly Assembly _assembly;



        //private readonly Action actionOfUnloadAssmbly;
        /*, Action actionOfUnloadAssmbly*/
        //this.actionOfUnloadAssmbly = actionOfUnloadAssmbly;

        public FlowLibrary(Assembly assembly)
        {
            this._assembly = assembly;
            this.FullName  = Path.GetFileName(_assembly.Location);

            this.FilePath = _assembly.Location;
        }

        public FlowLibrary(Assembly assembly,
                          string filePath)
        {
            this._assembly = assembly;
            this.FullName = Path.GetFileName(filePath); ;
            this.FilePath = filePath;
        }

        public string FullName { get; private set; }

        public string FilePath { get; private set; }

        /// <summary>
        /// 加载程序集时创建的方法描述
        /// Key   ： 方法名称
        /// Value ：方法详情
        /// </summary>
        public ConcurrentDictionary<string, MethodDetails> MethodDetailss { get; } = new ConcurrentDictionary<string, MethodDetails>();

        /// <summary>
        /// 管理通过Emit动态构建的委托
        /// Key   ：方法名称
        /// Value ：方法详情
        /// </summary>
        public ConcurrentDictionary<string, DelegateDetails> DelegateDetailss { get; } = new ConcurrentDictionary<string, DelegateDetails>();

        /// <summary>
        /// 记录不同的注册时机需要自动创建全局唯一实例的类型信息
        /// </summary>
        public ConcurrentDictionary<RegisterSequence, List<Type>> RegisterTypes { get; } = new ConcurrentDictionary<RegisterSequence, List<Type>>();


        /// <summary>
        /// 卸载当前程序集以及附带的所有信息
        /// </summary>
        public void Upload()
        {
            DelegateDetailss.Clear();
            RegisterTypes.Clear();
            MethodDetailss.Clear();
            //actionOfUnloadAssmbly?.Invoke();
            
        }

        /// <summary>
        /// 转为依赖信息
        /// </summary>
        /// <returns></returns>
        public NodeLibraryInfo ToInfo()
        {
            var assemblyName = _assembly.GetName().Name;    
            return new NodeLibraryInfo
            {
                AssemblyName = assemblyName,
                FileName = this.FullName,
                FilePath = this.FilePath,
            };

            
        }


        /// <summary>
        /// 动态加载程序集
        /// </summary>
        /// <param name="assembly">程序集本身</param>
        /// <returns></returns>
        public bool LoadAssembly()
        {
            Assembly assembly = this._assembly;
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
                if (types.Count < 0) // 防止动态程序集中没有类型信息？
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
                    SereinEnv.WriteLine(InfoType.ERROR, loaderException?.Message);
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

            types = types.Where(type => type.GetCustomAttribute<DynamicFlowAttribute>() is DynamicFlowAttribute dynamicFlowAttribute
            && dynamicFlowAttribute.Scan).ToList();

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
            List<LibraryMdDd> detailss = new List<LibraryMdDd>();

            // 遍历扫描的类型
            foreach ((var type, var flowName) in scanTypes)
            {
                var methodInfos = NodeMethodDetailsHelper.GetMethodsToProcess(type);
                foreach (var methodInfo in methodInfos) // 遍历流程控制器类型中的方法信息
                {
                    // 尝试创建
                    if (!NodeMethodDetailsHelper.TryCreateDetails(type, methodInfo, assemblyName,
                                                                   out var md, out var dd)) // 返回的描述
                    {
                        SereinEnv.WriteLine(InfoType.ERROR, $"无法加载方法信息：{assemblyName}-{type}-{methodInfo}");
                        continue;
                    }
                    md.MethodAnotherName = flowName + md.MethodAnotherName; // 方法别名
                    detailss.Add(new LibraryMdDd(md, dd));
                }
            }

            #endregion

            #region 检查是否成功加载，如果成功，则真正写入到缓存的集合中
            if (detailss.Count == 0)
            {
                return false;
            }
            // 简单排序一下
            //detailss = detailss.OrderBy(k => k.MethodDetails.MethodName,).ToList();

           
            
            detailss.Sort((a, b) => string.Compare(a.MethodDetails.MethodName, b.MethodDetails.MethodName, StringComparison.OrdinalIgnoreCase));
            foreach (var item in detailss)
            {
                SereinEnv.WriteLine(InfoType.INFO, "loading method : " + item.MethodDetails.MethodName);

            }

            //detailss.Sort((a, b) => string.Compare());

            #region 加载成功，缓存所有方法、委托的信息
            foreach (var item in detailss)
            {
                var key = item.MethodDetails.MethodName;
                MethodDetailss.TryAdd(key, item.MethodDetails);
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


    }

}

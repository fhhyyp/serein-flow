using Serein.Library;
using Serein.Library.Api;
using Serein.Library.FlowNode;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Tool
{
    /// <summary>
    /// 管理加载在运行环境中的外部程序集
    /// </summary>
    public class FlowLibraryManagement
    {
        public FlowLibraryManagement(IFlowEnvironment flowEnvironment)
        {
            this.flowEnvironment = flowEnvironment;
        }

        private readonly IFlowEnvironment flowEnvironment;

        /// <summary>
        /// 缓存所有加载了的程序集
        /// </summary>
        private ConcurrentDictionary<string, FlowLibrary> _myFlowLibrarys = new ConcurrentDictionary<string, FlowLibrary>();

        public (NodeLibraryInfo,List<MethodDetailsInfo>) LoadLibrary(string libraryfilePath)
        {
            return LoadDllNodeInfo(libraryfilePath);
        }

        public bool UnloadLibrary(string libraryName)
        {
            if (_myFlowLibrarys.TryGetValue(libraryName, out var flowLibrary))
            {
                try
                {
                    flowLibrary.Upload();
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"尝试卸载程序集[{libraryName}]发生错误：{ex}");
                    return false;
                }
                
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 获取方法描述
        /// </summary>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="methodName">方法名称</param>
        /// <param name="md">返回的方法描述</param>
        /// <returns>是否获取成功</returns>
        public bool TryGetMethodDetails(string assemblyName, string methodName, [MaybeNullWhen(false)]  out MethodDetails md) 
        {
            if(_myFlowLibrarys.TryGetValue(assemblyName, out var flowLibrary)
                && flowLibrary.MethodDetailss.TryGetValue(methodName,out md))
            {
                return true;
            }
            else
            {
                md = null;
                return false;
            }
        }
       
        /// <summary>
        /// 获取方法调用的委托
        /// </summary>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="methodName">方法名称</param>
        /// <param name="dd">返回的委托调用封装类</param>
        /// <returns>是否获取成功</returns>
        public bool TryGetDelegateDetails(string assemblyName, string methodName, [MaybeNullWhen(false)]  out DelegateDetails dd) 
        {
            if (_myFlowLibrarys.TryGetValue(assemblyName, out var flowLibrary)
               && flowLibrary.DelegateDetailss.TryGetValue(methodName, out dd))
            {
                return true;
            }
            else
            {
                dd = null;
                return false;
            }
        }


        /// <summary>
        /// 获取(初始化/加载时/退出后)相应的节点方法
        /// </summary>
        /// <param name="nodeType"></param>
        /// <returns></returns>
        public List<MethodDetails> GetMdsOnFlowStart(NodeType nodeType)
        {
            List<MethodDetails> mds = [];

            foreach (var library in _myFlowLibrarys.Values) 
            {
               var t_mds = library.MethodDetailss.Values.Where(it => it.MethodDynamicType == nodeType).ToList();
                mds.AddRange(t_mds);
            }
            return mds;
        }

        /// <summary>
        /// 获取流程启动时在不同时间点需要自动实例化的类型
        /// </summary>
        /// <returns></returns>
        public Dictionary<RegisterSequence, List<Type>> GetaAutoRegisterType()
        {
            Dictionary<RegisterSequence, List<Type>> rsTypes = new Dictionary<RegisterSequence, List<Type>>();
            foreach(var library in _myFlowLibrarys.Values)
            {
                foreach(var kv in library.RegisterTypes)
                {
                    var @class = kv.Key;
                    var type = kv.Value;
                    if(!rsTypes.TryGetValue(@class, out var tmpTypes))
                    {
                        tmpTypes = new List<Type>();
                        rsTypes.Add(@class, tmpTypes);
                    }
                    tmpTypes.AddRange(type);
                }
            }
            return rsTypes;
        }


        /// <summary>
        /// 获取所有方法信息，用于保存项目时调用
        /// </summary>
        /// <returns></returns>
        public List<LibraryMds> GetAllLibraryMds()
        {
            List<LibraryMds> mds = new List<LibraryMds>();
            foreach (FlowLibrary library  in _myFlowLibrarys.Values)
            {
                var tmp = new LibraryMds { 
                    AssemblyName = library.FullName, 
                    Mds = library.MethodDetailss.Values.Select(md => md.ToInfo()).ToArray()
                };
                mds.Add(tmp);
            }
            return mds;
        }





        /// <summary>
        /// 序列化当前项目的依赖信息、节点信息，用于远程登录的场景，需要将依赖信息从本地（受控端）发送到远程（主控端）
        /// </summary>
        /// <returns></returns>
        public List<NodeLibraryInfo> GetAllLibraryInfo()
        {
            return _myFlowLibrarys.Values.Select(library => library.ToInfo()).ToList();
        }


        #region 功能性方法

        /// <summary>
        /// 从文件路径中加载程序集，返回相应的信息
        /// </summary>
        /// <param name="dllPath"></param>
        /// <returns></returns>
        private (NodeLibraryInfo, List<MethodDetailsInfo>) LoadDllNodeInfo(string dllPath)
        {

      
            var fileName = Path.GetFileName(dllPath); // 获取文件名
            Assembly assembly = Assembly.LoadFrom(dllPath); // 加载程序集
            FlowLibrary flowLibrary = new FlowLibrary(dllPath, assembly, () =>
            {
                Console.WriteLine("暂未实现卸载程序集");
                //flowAlc.Unload(); // 卸载程序集
                //flowAlc = null;
                //GC.Collect(); // 强制触发GC确保卸载成功
                //GC.WaitForPendingFinalizers();
            });
            
            _myFlowLibrarys.TryAdd(assembly.GetName().Name, flowLibrary);

            (NodeLibraryInfo, List<MethodDetailsInfo>) result = (flowLibrary.ToInfo(),
                                                            flowLibrary.MethodDetailss.Values.Select(md => md.ToInfo()).ToList());
            return result;


#if false
            var fileName = Path.GetFileName(dllPath); // 获取文件名
            var flowAlc = new AssemblyLoadContext(fileName, true);
            flowAlc.LoadFromAssemblyPath(dllPath); // 加载指定路径的程序集
            flowAlc.LoadFromAssemblyPath(@"F:\临时\project\yolo flow\OpenCvSharp.dll"); // 加载指定路径的程序集

            var assemblt = flowAlc.Assemblies.ToArray()[0]; // 目前只会加载一个程序集，所以这样获取
            FlowLibrary flowLibrary = new FlowLibrary(dllPath, assemblt, () =>
            {
                flowAlc.Unload(); // 卸载程序集
                flowAlc = null;
                GC.Collect(); // 强制触发GC确保卸载成功
                GC.WaitForPendingFinalizers();
            });
            _myFlowLibrarys.TryAdd(assemblt.GetName().Name, flowLibrary);
            return flowLibrary.ToInfo(); 


            //foreach (var assemblt in flowAlc.Assemblies)
            //{
            //    FlowLibrary flowLibrary = new FlowLibrary(dllPath, assemblt, () =>
            //    {
            //        flowAlc.Unload(); // 卸载程序集
            //        flowAlc = null; 
            //        GC.Collect(); // 强制触发GC确保卸载成功
            //        GC.WaitForPendingFinalizers();
            //    });
            //}

#endif
            //if (OperatingSystem.IsWindows())
            //{
            //    UIContextOperation?.Invoke(() => OnDllLoad?.Invoke(new LoadDllEventArgs(nodeLibraryInfo, mdInfos))); // 通知UI创建dll面板显示

            //}
        }



        #endregion



        ///// <summary>
        ///// 是否对程序集的引用
        ///// </summary>
        //public void UnloadPlugin()
        //{
        //    _pluginAssembly = null; // 释放对程序集的引用
        //    Unload(); // 触发卸载
        //    // 强制进行垃圾回收，以便完成卸载
        //    GC.Collect();
        //    GC.WaitForPendingFinalizers();
        //}
    }



}

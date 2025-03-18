using Serein.Library;
using Serein.Library.Api;
using Serein.Library.FlowNode;
using Serein.Library.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

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
        private readonly ConcurrentDictionary<string, FlowLibrary> _myFlowLibrarys = new ConcurrentDictionary<string, FlowLibrary>();

        /// <summary>
        /// 加载类库
        /// </summary>
        /// <param name="libraryfilePath"></param>
        /// <returns></returns>
        public (NodeLibraryInfo, List<MethodDetailsInfo>) LoadLibraryOfPath(string libraryfilePath)
        {

            var dir = Path.GetDirectoryName(libraryfilePath); // 获取目录路径
            var sereinFlowBaseLibraryPath = Path.Combine(dir, SereinBaseLibrary);// 每个类库下面至少需要有“Serein.Library.dll”类库依赖
            if (!Path.Exists(sereinFlowBaseLibraryPath))
            {
                throw new Exception($"从文件加载DLL失败，目标文件夹不存在{SereinBaseLibrary}文件" );
            }

            var flowAlc = new FlowLibraryAssemblyContext(sereinFlowBaseLibraryPath, Path.GetFileName(libraryfilePath));
            var assembly = flowAlc.LoadFromAssemblyPath(libraryfilePath); // 加载指定路径的程序集

            var flowLibrary = new FlowLibrary(assembly);
            try
            {
                var reulst = LoadFlowLibrary(flowLibrary);
                return reulst;
            }
            catch (Exception)
            {
                flowAlc?.Unload(); // 卸载程序集
                flowAlc = null;
                GC.Collect(); // 强制触发GC确保卸载成功
                GC.WaitForPendingFinalizers();
                throw;
            }
        }

        /// <summary>
        /// 加载类库
        /// </summary>
        /// <param name="flowLibrary"></param>
        /// <returns></returns>
        public (NodeLibraryInfo, List<MethodDetailsInfo>) LoadLibraryOfPath(FlowLibrary flowLibrary)
        {
            return LoadFlowLibrary(flowLibrary);
        }

        /// <summary>
        /// 卸载类库
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        public bool UnloadLibrary(string assemblyName)
        {
            if (_myFlowLibrarys.Remove(assemblyName, out var flowLibrary))
            {
                try
                {
                    flowLibrary.Upload(); // 尝试卸载
                    flowLibrary = null;
                    return true;
                }
                catch (Exception ex)
                {
                    SereinEnv.WriteLine(InfoType.ERROR, $"尝试卸载程序集[{assemblyName}]发生错误：{ex}");
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
        public bool TryGetMethodDetails(string assemblyName, string methodName, [MaybeNullWhen(false)] out MethodDetails md)
        {
            if (_myFlowLibrarys.TryGetValue(assemblyName, out var flowLibrary)
                && flowLibrary.MethodDetailss.TryGetValue(methodName, out md))
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
        public bool TryGetDelegateDetails(string assemblyName, string methodName, [MaybeNullWhen(false)] out DelegateDetails dd)
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
            foreach (var library in _myFlowLibrarys.Values)
            {
                foreach (var kv in library.RegisterTypes)
                {
                    var @class = kv.Key;
                    var type = kv.Value;
                    if (!rsTypes.TryGetValue(@class, out var tmpTypes))
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
        /// 获取某个程序集下的所有方法信息，用于保存项目时调用
        /// </summary>
        /// <returns></returns>
        public List<MethodDetails> GetLibraryMdsOfAssmbly(string assemblyName)
        {
            if (_myFlowLibrarys.TryGetValue(assemblyName, out var flowLibrary))
            {
                return flowLibrary.MethodDetailss.Values.ToList();
            }
            return [];
        }



        /// <summary>
        /// 获取所有方法信息，用于保存项目时调用
        /// </summary>
        /// <returns></returns>
        public List<LibraryMds> GetAllLibraryMds()
        {
            List<LibraryMds> mds = new List<LibraryMds>();
            foreach (FlowLibrary library in _myFlowLibrarys.Values)
            {
                var tmp = new LibraryMds
                {
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
        /// 基础依赖
        /// </summary>
        public readonly static string SereinBaseLibrary = $"{nameof(Serein)}.{nameof(Serein.Library)}.dll";

        //private (NodeLibraryInfo, List<MethodDetailsInfo>) LoadDllNodeInfo(Assembly assembly)
        //{
            
        //    if (assembly.FullName?.ToString().Equals(typeof(IFlowEnvironment).Assembly.FullName?.ToString()) == true)
        //    {

        //        // 加载基础依赖
        //        return LoadAssembly(typeof(IFlowEnvironment).Assembly);
        //    }
        //    else
        //    {
        //        try
        //        {
        //            var assembly_result = LoadAssembly(assembly);
        //            return assembly_result;
        //        }
        //        catch (Exception)
        //        {
        //            return (null,[]);
        //        }
                
        //    }

        //}



        private (NodeLibraryInfo, List<MethodDetailsInfo>) LoadFlowLibrary(FlowLibrary flowLibrary)
        {
            var assembly = flowLibrary.Assembly;
            if (assembly.FullName?.ToString().Equals(typeof(IFlowEnvironment).Assembly.FullName?.ToString()) == true)
            {
                // 加载基础依赖
                flowLibrary = new FlowLibrary(typeof(IFlowEnvironment).Assembly);
            }

            var assmblyName = assembly.GetName().Name;
           if (!string.IsNullOrEmpty(assmblyName) && _myFlowLibrarys.ContainsKey(assmblyName))
           {
               throw new Exception($"程序集[{assembly.GetName().FullName}]已经加载过!");
           }

            var loadResult = flowLibrary.LoadAssembly(); // 加载程序集
            if (loadResult)
            {
                var assemblyName = assembly.GetName().Name;
                if (string.IsNullOrEmpty(assemblyName))
                {
                    throw new Exception($"程序集[{assembly.GetName().FullName}]加载失败，没有程序集名称");
                }
                _myFlowLibrarys.TryAdd(assemblyName, flowLibrary);

                List<MethodDetailsInfo> mdInfos = flowLibrary.MethodDetailss.Values.Select(md => md.ToInfo()).ToList();
                mdInfos.Sort((a, b) => string.Compare(a.MethodName, b.MethodName, StringComparison.OrdinalIgnoreCase));

                (NodeLibraryInfo, List<MethodDetailsInfo>) result = (flowLibrary.ToInfo(), mdInfos);
                return result;
            }
            else
            {
                throw new Exception($"程序集[{assembly.GetName().FullName}]加载失败");
            }
        }



        #endregion
    }

    /// <summary>
    /// 流程依赖加载
    /// </summary>
    public class FlowLibraryAssemblyContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        /// <summary>
        ///  创建新的加载上下文
        /// </summary>
        /// <param name="sereinFlowLibraryPath">类库路径</param>
        /// <param name="name"></param>
        public FlowLibraryAssemblyContext(string sereinFlowLibraryPath, string name) : base(name, isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(sereinFlowLibraryPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (!string.IsNullOrEmpty(assemblyPath))
            {
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
                //var assembly = LoadFromAssemblyPath(assemblyPath);
                return assembly;
            }
            else
            {
                return Default.Assemblies.FirstOrDefault(x => x.FullName == assemblyName.FullName);
            }

            // return null;

            // 构建依赖项的路径
            //string assemblyPath = Path.Combine(AppContext.BaseDirectory, assemblyName.Name + ".dll");
            //if (File.Exists(assemblyPath))
            //{
            //    return LoadFromAssemblyPath(assemblyPath);
            //}
            //assemblyPath = Path.Combine(filePath, assemblyName.Name + ".dll");
            //if (File.Exists(assemblyPath))
            //{
            //    return LoadFromAssemblyPath(assemblyPath);
            //}

            //return null; // 如果没有找到，返回 null
        }
    }
    //public static class PluginAssemblyContextExtensions
    //{

    //    public static Assembly FromAssemblyPath(this AssemblyLoadContext context, string path)
    //    {

    //        return context.LoadFromAssemblyPath(path);

    //    }

    //}
}

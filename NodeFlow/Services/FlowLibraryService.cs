using Serein.Library;
using Serein.Library.Api;
using Serein.NodeFlow.Model.Library;
using Serein.NodeFlow.Tool;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Xml.Linq;

namespace Serein.NodeFlow.Services
{

    /// <summary>
    /// 管理加载在运行环境中的外部程序集
    /// </summary>
    public class FlowLibraryService : IFlowLibraryService
    {
        /// <summary>
        /// 是否加载过基础依赖
        /// </summary>
        public bool IsLoadedBaseLibrary { get; private set; } = false;
        
        /// <summary>
        /// 构造函数，初始化流程依赖
        /// </summary>
        /// <param name="flowEnvironment"></param>
        public FlowLibraryService(IFlowEnvironment flowEnvironment)
        {
            this.flowEnvironment = flowEnvironment;
        }

        private readonly IFlowEnvironment flowEnvironment;

        /// <summary>
        /// 缓存流程依赖
        /// </summary>
        private readonly ConcurrentDictionary<string, FlowLibraryCache> _flowLibraryCaches = new ConcurrentDictionary<string, FlowLibraryCache>();


        private readonly ConcurrentDictionary<string, FlowLibraryAssemblyContext> _flowLibraryAssemblyContexts 
            = new ConcurrentDictionary<string, FlowLibraryAssemblyContext>();


        /// <summary>
        ///  每个类库下面至少需要有“Serein.Library.dll”类库依赖
        /// </summary>
        /// <param name="libraryfilePath"></param>
        /// <param name="baseLibraryPath"></param>
        /// <returns></returns>
        private bool CheckBaseLibrary(string libraryfilePath, out string baseLibraryPath)
        {
            var dir = Path.GetDirectoryName(libraryfilePath); // 获取目录路径
            ArgumentException.ThrowIfNullOrWhiteSpace(dir);
            var sereinFlowBaseLibraryPath = Path.Combine(dir, SereinBaseLibrary);
            if (!Path.Exists(sereinFlowBaseLibraryPath))
            {
                baseLibraryPath = string.Empty;
                return false;
            }
            baseLibraryPath = sereinFlowBaseLibraryPath;
            return true;
        }

        /// <summary>
        /// 加载基础依赖
        /// </summary>
        public FlowLibraryInfo LoadBaseLibrary()
        {
            var baseAssmbly = typeof(FlowBaseLibrary).Assembly;
            var flowLibrary = new FlowLibraryCache(baseAssmbly);
            flowLibrary.LoadFlowMethod();
            var assemblyName = baseAssmbly.GetName().Name;
            if (string.IsNullOrEmpty(assemblyName))
            {
                throw new Exception($"程序集\"{baseAssmbly}\"返回 Name 为 null");
            }
            _flowLibraryCaches.TryAdd(assemblyName, flowLibrary);
            var infos = flowLibrary.ToInfo();
            IsLoadedBaseLibrary = true;
            return infos; 
        }

        /// <summary>
        /// 加载流程依赖
        /// </summary>
        /// <param name="libraryfilePath"></param>
        /// <exception cref="Exception"></exception>
        public FlowLibraryInfo? LoadFlowLibrary(string libraryfilePath)
        {
            if (!CheckBaseLibrary(libraryfilePath, out var baseLibraryPath))
            {
                throw new Exception($"从文件加载DLL失败，目标文件夹不存在{SereinBaseLibrary}文件");
            }
            FlowLibraryAssemblyContext flowAlc = new FlowLibraryAssemblyContext(baseLibraryPath, Path.GetFileName(libraryfilePath));
            var flowAssembly = flowAlc.LoadFromAssemblyPath(libraryfilePath);
            if(flowAssembly is null)
            {
                throw new Exception($"从文件加载DLL失败，FlowLibraryAssemblyContext 加载的程序集为 null \"{libraryfilePath}\"");
            }

            var flowLibrary = new FlowLibraryCache(flowAssembly);
            var isSuccess = flowLibrary.LoadFlowMethod();
            if (!isSuccess)
            {
                flowAlc?.Unload(); // 卸载程序集
                GC.Collect(); // 强制触发GC确保卸载成功
                GC.WaitForPendingFinalizers();
                return null;
            }
            else
            {
                var assemblyName = flowAssembly.GetName().Name;
                if (string.IsNullOrEmpty(assemblyName))
                {
                    flowLibrary.Unload();
                    flowAlc?.Unload(); // 卸载程序集
                    GC.Collect(); // 强制触发GC确保卸载成功
                    GC.WaitForPendingFinalizers();
                    return null;
                    throw new Exception($"程序集\"{flowAssembly}\"返回 Name 为 null");
                }
                _flowLibraryCaches.TryAdd(assemblyName, flowLibrary);
                return flowLibrary.ToInfo();
            }
        }


        /// <summary>
        /// 卸载类库
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        public bool UnloadLibrary(string assemblyName)
        {
            if (_flowLibraryCaches.Remove(assemblyName, out var flowLibrary))
            {
                try
                {
                    flowLibrary.Unload(); // 尝试卸载
                    flowLibrary = null;
                    return true;
                }
                catch (Exception ex)
                {
                    SereinEnv.WriteLine(InfoType.ERROR, $"尝试卸载程序集[{assemblyName}]发生错误：{ex.Message}");
                    return false;
                }

            }
            else
            {
                return false;
            }
        }

        #region 获取流程依赖的相关方法

        /// <summary>
        /// 搜索类型
        /// </summary>
        /// <param name="fullName"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool TryGetType(string fullName,[NotNullWhen(true)] out Type? type)
        {
            var assemblys = _flowLibraryCaches.Values.Select(key => key.Assembly).ToArray();
            foreach(var assembly in assemblys)
            {
                type = assembly.GetType(fullName);
                if(type is not null)
                {
                    return true;
                }
            }
            type = null;
            return false;
        }
        /// <summary>
        /// 搜索类型
        /// </summary>
        /// <param name="fullName"></param>
        /// <returns></returns>
        public Type? GetType(string fullName)
        {
            var assemblys = _flowLibraryCaches.Values.Select(key => key.Assembly).ToArray();
            Type? type;
            foreach (var assembly in assemblys)
            {
                type = assembly.GetType(fullName);
                if(type is not null)
                {
                    return type;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取方法描述
        /// </summary>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="methodName">方法名称</param>
        /// <param name="methodInfo">返回的方法描述</param>
        /// <returns>是否获取成功</returns>
        public bool TryGetMethodInfo(string assemblyName, string methodName, [MaybeNullWhen(false)] out MethodInfo methodInfo)
        {
            if (string.IsNullOrEmpty(assemblyName) || string.IsNullOrEmpty(methodName))
            {
                methodInfo = null;
                return false;
            }
            if (_flowLibraryCaches.TryGetValue(assemblyName, out var flowLibrary)
                && flowLibrary.MethodInfos.TryGetValue(methodName, out methodInfo))
            {
                return true;
            }
            else
            {
                methodInfo = null;
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
            if (_flowLibraryCaches.TryGetValue(assemblyName, out var flowLibrary)
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
            if (_flowLibraryCaches.TryGetValue(assemblyName, out var flowLibrary)
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

            foreach (var library in _flowLibraryCaches.Values)
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
            foreach (var library in _flowLibraryCaches.Values)
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
            if (_flowLibraryCaches.TryGetValue(assemblyName, out var flowLibrary))
            {
                return flowLibrary.MethodDetailss.Values.ToList();
            }
            return [];
        }

        /// <summary>
        /// 获取流程方法信息，用于保存项目时调用
        /// </summary>
        /// <returns></returns>
        public List<FlowLibraryInfo> GetAllLibraryMds()
        {
            List<FlowLibraryInfo> mds = new List<FlowLibraryInfo>();
            foreach (FlowLibraryCache library in _flowLibraryCaches.Values)
            {
                var tmp = new FlowLibraryInfo
                {
                    AssemblyName = library.FullName,
                    MethodInfos = library.MethodDetailss.Values.Select(md => md.ToInfo()).ToList()
                };
                mds.Add(tmp);
            }
            return mds;
        }


        /// <summary>
        /// 序列化当前项目的依赖信息、节点信息，用于远程登录的场景，需要将依赖信息从本地（受控端）发送到远程（主控端）
        /// </summary>
        /// <returns></returns>
        public List<FlowLibraryInfo> GetAllLibraryInfo()
        {
            return _flowLibraryCaches.Values.Where(lib => lib.FullName != "Serein.Library.dll").Select(library => library.ToInfo()).ToList();
        }
        #endregion

        #region 功能性方法

        /// <summary>
        /// 基础依赖
        /// </summary>
        public readonly static string SereinBaseLibrary = $"{nameof(Serein)}.{nameof(Library)}.dll";


        #endregion
    }
}

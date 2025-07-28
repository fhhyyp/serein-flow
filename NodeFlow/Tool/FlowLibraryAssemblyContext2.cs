using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Model.Library
{

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
                var assembly = Default.LoadFromAssemblyPath(assemblyPath);
                //var assembly = LoadFromAssemblyPath(assemblyPath);
                return assembly;
            }
            else
            {
                return Default.Assemblies.FirstOrDefault(x => x.FullName == assemblyName.FullName);
            }
        }
    }
}

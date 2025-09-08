using System.Reflection;
using System.Runtime.Loader;

namespace Serein.NodeFlow.Tool
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
        /// <param name="baseLibraryPath">流程基础依赖类库路径</param>
        /// <param name="name"></param>
        public FlowLibraryAssemblyContext(string baseLibraryPath, string name) : base(name, isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(baseLibraryPath);
        }

        /// <summary>
        /// 加载指定的程序集
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName); // 加载程序集
            if (!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath))
            {
                try
                {
                    var assembly = Default.LoadFromAssemblyPath(assemblyPath); // 通过默认方式进行加载程序集及相关依赖
                    return assembly;
                }
                catch (Exception ex)
                {
                    var assembly = LoadFromAssemblyPath(assemblyPath);
                    return assembly;
                }
            }
            else
            {
                var assembly = Default.Assemblies.FirstOrDefault(x => x.FullName == assemblyName.FullName);
                return assembly;
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
}

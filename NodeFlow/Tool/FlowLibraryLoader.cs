using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Tool
{
    /// <summary>
    /// 管理加载在流程的程序集
    /// </summary>
    public class FlowLibraryLoader : AssemblyLoadContext
    {
        private Assembly _pluginAssembly;

        public string FullName => _pluginAssembly.FullName;

        /// <summary>
        /// 加载程序集
        /// </summary>
        /// <param name="pluginPath"></param>
        public FlowLibraryLoader(string pluginPath) : base(isCollectible: true)
        {
            _pluginAssembly = LoadFromAssemblyPath(pluginPath);
        }

        /// <summary>
        /// 保持默认加载行为
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>

        protected override Assembly Load(AssemblyName assemblyName)
        {
            return null; // 保持默认加载行为
        }


        public List<Type> LoadFlowTypes()
        {
            return _pluginAssembly.GetTypes().ToList();
        }

        /// <summary>
        /// 是否对程序集的引用
        /// </summary>
        public void UnloadPlugin()
        {
            _pluginAssembly = null; // 释放对程序集的引用
            Unload(); // 触发卸载
            // 强制进行垃圾回收，以便完成卸载
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }



}

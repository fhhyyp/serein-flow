using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace Serein.Library
{
    /// <summary>
    /// 节点DLL依赖类，如果一个项目中引入了多个DLL，需要放置在同一个文件夹中
    /// </summary>
    public class NodeLibraryInfo
    {
        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// 路径
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 所属的程序集名称
        /// </summary>
        public string AssemblyName{ get; set; }
    }



    /// <summary>
    /// 
    /// </summary>
    public class FlowLibrary
    {
        public FlowLibrary(string assemblyName, 
                           Action actionOfUnloadAssmbly)
        {
            this.AssemblyName = assemblyName;
            this.actionOfUnloadAssmbly = actionOfUnloadAssmbly;
        }

        public string AssemblyName { get; }

        //public string AssemblyVersion { get; }

        /// <summary>
        /// 加载程序集时创建的方法描述
        /// </summary>
        public ConcurrentDictionary<string, MethodDetails> MethodDetailss { get; } = new ConcurrentDictionary<string, MethodDetails>();

        /// <summary>
        /// 管理通过Emit动态构建的委托
        /// </summary>
        public ConcurrentDictionary<string, DelegateDetails> DelegateDetailss { get; } = new ConcurrentDictionary<string, DelegateDetails>();

        /// <summary>
        /// 记录不同的注册时机需要自动创建全局唯一实例的类型信息
        /// </summary>
        public ConcurrentDictionary<RegisterSequence, Type[]> RegisterTypes { get; } = new ConcurrentDictionary<RegisterSequence, Type[]>();


        private readonly Action actionOfUnloadAssmbly;

        /// <summary>
        /// 卸载当前程序集以及附带的所有信息
        /// </summary>
        public void Upload()
        {
            actionOfUnloadAssmbly?.Invoke();
        }


        /// <summary>
        /// 通过方法名称获取对应的Emit委托（元数据），用于动态调用节点对应的方法
        /// </summary>
        /// <param name="methodName">方法名称</param>
        /// <param name="dd">Emit委托</param>
        /// <returns></returns>
        public bool GetDelegateDetails(string methodName, out DelegateDetails dd)
        {
            return DelegateDetailss.TryGetValue(methodName, out dd);
        }

        /// <summary>
        /// 通过方法名称获取对应的方法描述（元数据），用于创建节点时，节点实例需要的方法描述
        /// </summary>
        /// <param name="methodName">方法名称</param>
        /// <param name="md">方法描述</param>
        /// <returns></returns>
        public bool GetMethodDetails(string methodName, out MethodDetails md)
        {
            return MethodDetailss.TryGetValue(methodName, out md);
        }

        public NodeLibraryInfo ToInfo()
        {
            return new NodeLibraryInfo
            {
                
            }
        }

    }

}

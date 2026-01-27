using Serein.Library;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Serein.Library.Api
{
    /// <summary>
    /// 流程依赖程序集管理
    /// </summary>
    public interface IFlowLibraryService
    {
        /// <summary>
        /// 是否加载了基础依赖
        /// </summary>
        bool IsLoadedBaseLibrary { get; }
        /// <summary>
        /// 加载基础依赖
        /// </summary>
        /// <returns></returns>
        FlowLibraryInfo LoadBaseLibrary();
        /// <summary>
        /// 获取已加载的方法信息
        /// </summary>
        /// <returns></returns>
        List<FlowLibraryInfo> GetAllLibraryInfo();
        /// <summary>
        /// 加载指定依赖
        /// </summary>
        /// <param name="libraryfilePath"></param>
        /// <returns></returns>
        FlowLibraryInfo? LoadFlowLibrary(string libraryfilePath);
        /// <summary>
        /// 卸载程序集
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        bool UnloadLibrary(string assemblyName);
        /// <summary>
        /// 获取委托
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <param name="methodName"></param>
        /// <param name="dd"></param>
        /// <returns></returns>
        bool TryGetDelegateDetails(string assemblyName, string methodName, out DelegateDetails dd);
        /// <summary>
        /// 获取方法描述
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <param name="methodName"></param>
        /// <param name="md"></param>
        /// <returns></returns>
        bool TryGetMethodDetails(string assemblyName, string methodName, out MethodDetails md);
        /// <summary>
        /// 获取反射方法信息
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <param name="methodName"></param>
        /// <param name="methodInfo"></param>
        /// <returns></returns>
        bool TryGetMethodInfo(string assemblyName, string methodName, out MethodInfo methodInfo);
        /// <summary>
        /// 获取依赖程序集中的类型
        /// </summary>
        /// <param name="fullName"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        bool TryGetType(string fullName, out Type? type);
        /// <summary>
        /// 获取依赖程序集中的类型
        /// </summary>
        /// <param name="fullName"></param>
        /// <returns></returns>
        Type? GetType(string fullName);

        /// <summary>
        /// 获取某个节点类型对应的方法描述
        /// </summary>
        /// <param name="nodeType"></param>
        /// <returns></returns>
        List<MethodDetails> GetMdsOnFlowStart(NodeType nodeType);

    }
}

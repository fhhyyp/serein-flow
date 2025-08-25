using Microsoft.VisualBasic;
using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Concurrency;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Serein.Library
{

    /// <summary>
    /// 动态流程上下文
    /// </summary>
    public class FlowContext : IFlowContext
    {
        /// <summary>
        /// 动态流程上下文
        /// </summary>
        /// <param name="flowEnvironment">脚本运行时的IOC</param>
        public FlowContext(IFlowEnvironment flowEnvironment)
        {
            Env = flowEnvironment;
            RunState = RunState.Running;
        }

        /// <summary>
        /// 是否记录流程调用信息
        /// </summary>
        public bool IsRecordInvokeInfo { get; set; } = true;

        /// <summary>
        /// 标识流程的Guid
        /// </summary>
        public string Guid { get; private set;  } = global::System.Guid.NewGuid().ToString();

        /// <summary>
        /// 运行环境
        /// </summary>
        public IFlowEnvironment Env { get; }

        /// <summary>
        /// 运行状态
        /// </summary>
        public RunState RunState { get; set; } = RunState.NoStart;

        /// <summary>
        /// 当前节点执行完成后，设置该属性，让运行环境判断接下来要执行哪个分支的节点。
        /// </summary>
        public ConnectionInvokeType NextOrientation { get; set; }

        /// <summary>
        /// 运行时异常信息
        /// </summary>
        public Exception ExceptionOfRuning { get; set; }

        /// <summary>
        /// 每个流程上下文分别存放节点的当前数据
        /// </summary>
        private readonly ConcurrentDictionary<string, FlowResult> dictNodeFlowData = new ConcurrentDictionary<string, FlowResult>();

        /// <summary>
        /// 每个流程上下文存储运行时节点的调用关系
        /// </summary>
        private readonly ConcurrentDictionary<string, string> dictPreviousNodes = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// 记录忽略处理的流程
        /// </summary>
        private readonly ConcurrentDictionary<string, bool> dictIgnoreNodeFlow = new ConcurrentDictionary<string, bool>();

        /// <summary>
        /// 记录节点的运行时参数数据
        /// </summary>
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, object>> dictNodeParams = new ConcurrentDictionary<string, ConcurrentDictionary<int, object>>();



        /// <summary>
        /// 记录流程调用信息
        /// </summary>
        //private Dictionary<long, IFlowNode> flowInvokeNodes = new Dictionary<long, IFlowNode>();
        private Dictionary<long, FlowInvokeInfo> flowInvokeInfos = new Dictionary<long, FlowInvokeInfo>();
        private static long _idCounter = 0;

        /// <summary>
        /// 在执行方法之前，获取新的调用信息
        /// </summary>
        /// <param name="previousNode">上一个节点</param>
        /// <param name="theNode">执行节点</param>
        public FlowInvokeInfo NewInvokeInfo(IFlowNode previousNode, IFlowNode theNode,  FlowInvokeInfo.InvokeType invokeType)
        {
            //Interlocked
            var id = Interlocked.Increment(ref _idCounter);

            FlowInvokeInfo flowInvokeInfo = new FlowInvokeInfo
            {
                ContextGuid = this.Guid,
                Id = id,
                PreviousNodeGuid = previousNode?.Guid,
                Method = theNode.MethodDetails?.MethodName,
                NodeGuid = theNode.Guid,
                Type = invokeType,
                State = FlowInvokeInfo.RunState.None,
            };
            flowInvokeInfos.Add(id, flowInvokeInfo);
            return flowInvokeInfo;
        }

        /// <summary>
        /// 获取当前流程上下文的所有节点调用信息，包含每个节点的执行时间、调用类型、执行状态等。
        /// </summary>
        /// <returns></returns>
        public List<FlowInvokeInfo> GetAllInvokeInfos() => [.. flowInvokeInfos.Values];
     
        /// <summary>
        /// 设置节点的运行时参数数据
        /// </summary>
        /// <param name="nodeModel">节点</param>
        /// <param name="index">第几个参数</param>
        /// <param name="data">数据</param>
        public void SetParamsTempData(string nodeModel, int index, object data)
        {
            if(!dictNodeParams.TryGetValue(nodeModel,out var dict))
            {
                dict = new ConcurrentDictionary<int, object>();
                dictNodeParams[nodeModel] = dict;
            }
            if (dict.TryGetValue(index, out var oldData))
            {
                dict[index] = data; // 更新数据
            }
            else
            {
                dict.TryAdd(index, data); // 添加新数据
            }
        }

        /// <summary>
        /// 获取节点的运行时参数数据
        /// </summary>
        /// <param name="nodeModel">节点</param>
        /// <param name="index">第几个参数</param>
        public bool TryGetParamsTempData(string nodeModel, int index, out object  data )
        {
            if (dictNodeParams.TryGetValue(nodeModel, out var dict))
            {
                if (dict.TryGetValue(index, out  data))
                {
                    return true; // 返回数据
                }
                else
                {
                    //throw new KeyNotFoundException($"节点 {nodeModel.Guid} 的参数索引 {index} 不存在。");
                    data = null; // 返回空数据
                    return false; // 返回未找到
                }
            }
            else
            {
                //throw new KeyNotFoundException($"节点 {nodeModel.Guid} 的参数数据不存在。");
                data = null; // 返回空数据
                return false; // 返回未找到
            }
        }

        /// <summary>
        /// 设置运行时上一节点
        /// </summary>
        /// <param name="currentNodeModel">当前节点</param>
        /// <param name="PreviousNode">上一节点</param>
        public void SetPreviousNode(string currentNodeModel, string PreviousNode)
        {
            dictPreviousNodes.AddOrUpdate(currentNodeModel, (_) => PreviousNode, (o, n) => PreviousNode);
        }

        /// <summary>
        /// 获取当前节点的运行时上一节点
        /// </summary>
        /// <param name="currentNodeModel"></param>
        /// <returns></returns>
        public string GetPreviousNode(string currentNodeModel)
        {
            if (dictPreviousNodes.TryGetValue(currentNodeModel, out var node))
            {
                return node;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 获取节点当前数据
        /// </summary>
        /// <param name="nodeGuid">节点</param>
        /// <returns></returns>
        public FlowResult GetFlowData(string nodeGuid)
        {
            if (dictNodeFlowData.TryGetValue(nodeGuid, out var data))
            {
                return data;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 添加或更新当前节点数据
        /// </summary>
        /// <param name="nodeModel">节点</param>
        /// <param name="flowData">新的数据</param>
        public void AddOrUpdateFlowData(string nodeModel, FlowResult flowData)
        {
            dictNodeFlowData.AddOrUpdate(nodeModel, _ => flowData, (o,n ) => flowData);
        }

        /// <summary>
        /// 添加或更新当前节点的数据
        /// </summary>
        /// <param name="nodeModel"></param>
        /// <param name="data"></param>
        public void AddOrUpdate(string nodeModel, object data)
        {
            var flowData = FlowResult.OK(nodeModel, this,  data);
            dictNodeFlowData.AddOrUpdate(nodeModel, _ => flowData, (o, n) => flowData);
        }

        /// <summary>
        /// 上一节点数据透传到下一节点
        /// </summary>
        /// <param name="nodeModel"></param>
        public FlowResult TransmissionData(string nodeModel)
        {
            if (dictPreviousNodes.TryGetValue(nodeModel, out var previousNode)) // 首先获取当前节点的上一节点
            {
                if (dictNodeFlowData.TryGetValue(previousNode, out var data)) // 其次获取上一节点的数据
                {
                    return data;
                    //AddOrUpdate(nodeModel.Guid, data); // 然后作为当前节点的数据记录在上下文中
                }
            }
            throw new InvalidOperationException($"透传{nodeModel}节点数据时发生异常：上一节点不存在数据");
        }

        private ConcurrentDictionary<Type, object?> tags = new ConcurrentDictionary<Type, object?>();

        /// <summary>
        /// <para>用于同一个流程上下文中共享、存储任意数据，每一个数据通过类型区分</para>
        /// <para>流程完毕时，如果存储的对象实现了 IDisposable 接口，将会自动调用</para>
        /// <para>请谨慎使用，请注意数据的生命周期和内存管理</para>
        /// </summary>
        /// <param name="tag"></param>
        public void SetTag<T>(T tag)
        {
            var key = typeof(T);
            tags.AddOrUpdate(key, (_) => tag, (o, n) => tag);
        }

        /// <summary>
        /// 指定泛型获取共享对象（在同一个上下文中保持一致）
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T? GetTag<T>()
        {
            TryGetTag(out T? tag);
            return tag;
        }


#if NET6_0_OR_GREATER
        /// <summary>
        /// 指定泛型尝试获取共享对象（在同一个上下文中保持一致）
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tag"></param>
        public bool TryGetTag<T>([NotNullWhen(true)] out T? tag)
        {
            if (tags.TryGetValue(typeof(T),out var temp) && temp is T t)
            {
                tag = t;
                return true;
            }
            tag = default;
            return false;
        }

#else
        /// <summary>
        /// 指定泛型尝试获取共享对象（在同一个上下文中保持一致）
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tag"></param>
        public bool TryGetTag<T>(out T? tag)
        {
                if (tags.TryGetValue(typeof(T),out var temp) && temp is T t)
                {
                    tag = t;
                    return true;
                }
                tag = default;
                return false;
            
        }

#endif


        /// <summary>
        /// 重置
        /// </summary>
        public void Reset()
        {
            var tagObjs = tags.Values.ToArray();
            tags.Clear();
            foreach (var tag in tagObjs) 
            {
                if (tag is IDisposable disposable)
                {
                    disposable.Dispose(); // 释放 Tag 中的资源
                }
            }
           
            this.dictNodeFlowData?.Clear();
            ExceptionOfRuning = null;
            flowInvokeInfos.Clear();
            NextOrientation = ConnectionInvokeType.None;
            RunState = RunState.Running;
            Guid = global::System.Guid.NewGuid().ToString();
        }

        /// <summary>
        /// 结束当前流程上下文
        /// </summary>
        public void Exit()
        {
            this.dictNodeFlowData?.Clear();
            ExceptionOfRuning = null;
            NextOrientation = ConnectionInvokeType.None;
            RunState = RunState.Completion;
        }

        /// <summary>
        /// 释放当前上下文中的所有资源
        /// </summary>
        /// <param name="keyValuePairs"></param>
        private void Dispose(ref IDictionary<string, object>  keyValuePairs)
        {
            foreach (var nodeObj in keyValuePairs.Values)
            {
                if (nodeObj is null)
                {
                    continue;
                }

                if (nodeObj is IDisposable disposable) /* typeof(IDisposable).IsAssignableFrom(nodeObj?.GetType()) &&*/
                {
                    disposable?.Dispose();
                }
                else if (nodeObj is IDictionary<string, object> tmpDict)
                {
                    Dispose(ref tmpDict);
                }
                else if (nodeObj is ICollection<object> tmpList)
                {
                    Dispose(ref tmpList);
                }
                else if (nodeObj is IList<object> tmpList2)
                {
                    Dispose(ref tmpList2);
                }
            }
            keyValuePairs.Clear();
        }
        private void Dispose(ref ICollection<object> list)
        {
            foreach (var nodeObj in list)
            {
                if (nodeObj is null)
                {
                    continue;
                }

                if (nodeObj is IDisposable disposable) /* typeof(IDisposable).IsAssignableFrom(nodeObj?.GetType()) &&*/
                {
                    disposable?.Dispose();
                }
                else if (nodeObj is IDictionary<string, object> tmpDict)
                {
                    Dispose(ref tmpDict);
                }
                else if (nodeObj is ICollection<object> tmpList)
                {
                    Dispose(ref tmpList);
                }
                else if (nodeObj is IList<object> tmpList2)
                {
                    Dispose(ref tmpList2);
                }
            }

            list.Clear();
        }
        private void Dispose(ref IList<object> list)
        {
            foreach (var nodeObj in list)
            {
                if (nodeObj is null)
                {
                    continue;
                }

                if (nodeObj is IDisposable disposable) /* typeof(IDisposable).IsAssignableFrom(nodeObj?.GetType()) &&*/
                {
                    disposable?.Dispose();
                }
                else if (nodeObj is IDictionary<string, object> tmpDict)
                {
                    Dispose(ref tmpDict);
                }
                else if (nodeObj is ICollection<object> tmpList)
                {
                    Dispose(ref tmpList);
                }
                else if (nodeObj is IList<object> tmpList2)
                {
                    Dispose(ref tmpList2);
                }
            }

            list.Clear();
        }
    }
}

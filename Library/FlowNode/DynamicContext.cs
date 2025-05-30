using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Serein.Library
{

    /// <summary>
    /// 动态流程上下文
    /// </summary>
    public class DynamicContext : IDynamicContext
    {
        /// <summary>
        /// 动态流程上下文
        /// </summary>
        /// <param name="flowEnvironment"></param>
        public DynamicContext(IFlowEnvironment flowEnvironment)
        {
            Env = flowEnvironment;
            RunState = RunState.Running;
        }

        private string _guid = global::System.Guid.NewGuid().ToString();
        string IDynamicContext.Guid => _guid;

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
        private readonly ConcurrentDictionary<NodeModelBase, FlowResult> dictNodeFlowData = new ConcurrentDictionary<NodeModelBase, FlowResult>();

        /// <summary>
        /// 每个流程上下文存储运行时节点的调用关系
        /// </summary>
        private readonly ConcurrentDictionary<NodeModelBase, NodeModelBase> dictPreviousNodes = new ConcurrentDictionary<NodeModelBase, NodeModelBase>();

        /// <summary>
        /// 记录忽略处理的流程
        /// </summary>
        private readonly ConcurrentDictionary<NodeModelBase, bool> dictIgnoreNodeFlow = new ConcurrentDictionary<NodeModelBase, bool>();

        /// <summary>
        /// 设置运行时上一节点
        /// </summary>
        /// <param name="currentNodeModel">当前节点</param>
        /// <param name="PreviousNode">上一节点</param>
        public void SetPreviousNode(NodeModelBase currentNodeModel, NodeModelBase PreviousNode)
        {
            dictPreviousNodes.AddOrUpdate(currentNodeModel, (_) => PreviousNode, (o, n) => PreviousNode);
        }

        /// <summary>
        /// 忽略处理该节点流程
        /// </summary>
        /// <param name="node"></param>
        public void IgnoreFlowHandle(NodeModelBase node)
        {
            dictIgnoreNodeFlow.AddOrUpdate(node, (o) => true, (o, n) => true); 
        }

        /// <summary>
        /// 获取此次流程处理状态
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public bool GetIgnodeFlowStateUpload(NodeModelBase node)
        {
            return dictIgnoreNodeFlow.TryGetValue(node, out var state) ? state : false;
        }
        /// <summary>
        /// 恢复流程处理状态
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public void RecoverIgnodeFlowStateUpload(NodeModelBase node)
        {
            dictIgnoreNodeFlow.AddOrUpdate(node, (o) => false, (o, n) => false);
        }


        /// <summary>
        /// 获取当前节点的运行时上一节点
        /// </summary>
        /// <param name="currentNodeModel"></param>
        /// <returns></returns>
        public NodeModelBase GetPreviousNode(NodeModelBase currentNodeModel)
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
        public FlowResult GetFlowData(NodeModelBase nodeGuid)
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
        public void AddOrUpdate(NodeModelBase nodeModel, FlowResult flowData)
        {
            // this.dictNodeFlowData.TryGetValue(nodeGuid, out var oldFlowData);
            dictNodeFlowData.AddOrUpdate(nodeModel, _ => flowData, (o,n ) => flowData);
        }

        /// <summary>
        /// 上一节点数据透传到下一节点
        /// </summary>
        /// <param name="nodeModel"></param>
        public FlowResult TransmissionData(NodeModelBase nodeModel)
        {
            if (dictPreviousNodes.TryGetValue(nodeModel, out var previousNode)) // 首先获取当前节点的上一节点
            {
                if (dictNodeFlowData.TryGetValue(previousNode, out var data)) // 其次获取上一节点的数据
                {
                    return data;
                    //AddOrUpdate(nodeModel.Guid, data); // 然后作为当前节点的数据记录在上下文中
                }
            }
            throw new InvalidOperationException($"透传{nodeModel.Guid}节点数据时发生异常：上一节点不存在数据");
        }

        /// <summary>
        /// 重置
        /// </summary>
        public void Reset()
        {
            //foreach (var nodeObj in dictNodeFlowData.Values)
            //{
            //    if (nodeObj is null)
            //    {

            //    }
            //    else 
            //    {
            //        if (typeof(IDisposable).IsAssignableFrom(nodeObj?.GetType()) && nodeObj is IDisposable disposable)
            //        {
            //            disposable?.Dispose();
            //        }
            //    }
            //}
            //if (Tag != null && typeof(IDisposable).IsAssignableFrom(Tag?.GetType()) && Tag is IDisposable tagDisposable)
            //{
            //    tagDisposable?.Dispose();
            //}
            this.dictNodeFlowData?.Clear();
            ExceptionOfRuning = null;
            NextOrientation = ConnectionInvokeType.None;
            RunState = RunState.Running;
            _guid = global::System.Guid.NewGuid().ToString();
        }


        /// <summary>
        /// 结束当前流程上下文
        /// </summary>
        public void Exit()
        {
            //foreach (var nodeObj in dictNodeFlowData.Values)
            //{
            //    if (nodeObj is null)
            //    {
                    
            //    }
            //    else 
            //    {
            //        if (typeof(IDisposable).IsAssignableFrom(nodeObj?.GetType()) && nodeObj is IDisposable disposable)
            //        {
            //            disposable?.Dispose();
            //        }
            //    }
            //}
            //if (Tag != null && typeof(IDisposable).IsAssignableFrom(Tag?.GetType()) && Tag is IDisposable tagDisposable)
            //{
            //    tagDisposable?.Dispose();
            //}
            this.dictNodeFlowData?.Clear();
            ExceptionOfRuning = null;
            NextOrientation = ConnectionInvokeType.None;
            RunState = RunState.Completion;
            _guid = global::System.Guid.NewGuid().ToString();
        }

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

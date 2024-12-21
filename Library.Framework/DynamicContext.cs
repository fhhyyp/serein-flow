using Serein.Library.Api;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Serein.Library.Framework.NodeFlow
{


    /// <summary>
    /// 动态流程上下文
    /// </summary>
    public class DynamicContext : IDynamicContext
    {
        public DynamicContext(/*ISereinIOC sereinIoc,*/ IFlowEnvironment flowEnvironment)
        {
            // SereinIoc = sereinIoc;
            Env = flowEnvironment;
            RunState = RunState.Running;
        }

        private readonly string _guid = global::System.Guid.NewGuid().ToString();
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
        /// 用来在当前流程上下文间传递数据
        /// </summary>
        //public Dictionary<string, object> ContextShareData { get; } = new Dictionary<string, object>();
        public object Tag { get; set; }

        /// <summary>
        /// 当前节点执行完成后，设置该属性，让运行环境判断接下来要执行哪个分支的节点。
        /// </summary>
        public ConnectionInvokeType NextOrientation { get; set; }

        /// <summary>
        /// 运行时异常信息
        /// </summary>
        public Exception ExceptionOfRuning { get; set; }

        /// <summary>
        /// 每个上下文分别存放节点的当前数据
        /// </summary>
        private readonly ConcurrentDictionary<string, object> dictNodeFlowData = new ConcurrentDictionary<string, object>();

        private readonly ConcurrentDictionary<NodeModelBase, NodeModelBase> dictPreviousNodes = new ConcurrentDictionary<NodeModelBase, NodeModelBase>();

        /// <summary>
        /// 设置运行时上一节点
        /// </summary>
        /// <param name="currentNodeModel">当前节点</param>
        /// <param name="PreviousNode">上一节点</param>
        public void SetPreviousNode(NodeModelBase currentNodeModel, NodeModelBase PreviousNode)
        {
            dictPreviousNodes.AddOrUpdate(currentNodeModel, (n1) => PreviousNode, (n1, n2) => PreviousNode);
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
        /// <returns></returns>
        public object GetFlowData(string nodeGuid)
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
        /// <param name="nodeGuid">节点</param>
        /// <param name="flowData">新的数据</param>
        public void AddOrUpdate(string nodeGuid, object flowData)
        {
            // this.dictNodeFlowData.TryGetValue(nodeGuid, out var oldFlowData);
            this.dictNodeFlowData.AddOrUpdate(nodeGuid, n1 => flowData, (n1, n2)=> flowData);
        }

        /// <summary>
        /// 上一节点数据透传到下一节点
        /// </summary>
        /// <param name="nodeModel"></param>
        public object TransmissionData(NodeModelBase nodeModel)
        {
            if (dictPreviousNodes.TryGetValue(nodeModel, out var previousNode)) // 首先获取当前节点的上一节点
            {
                if (dictNodeFlowData.TryGetValue(previousNode.Guid, out var data)) // 其次获取上一节点的数据
                {
                    return data;
                    //AddOrUpdate(nodeModel.Guid, data); // 然后作为当前节点的数据记录在上下文中
                }
            }
            return null;
        }


        /// <summary>
        /// 结束流程
        /// </summary>
        public void Exit()
        {
            foreach (var nodeObj in dictNodeFlowData.Values)
            {
                if (nodeObj is null)
                {
                    continue;
                }
                else 
                {
                    if (typeof(IDisposable).IsAssignableFrom(nodeObj?.GetType()) && nodeObj is IDisposable disposable)
                    {
                        disposable?.Dispose();
                    }
                }
            }

            if (Tag != null && typeof(IDisposable).IsAssignableFrom(Tag?.GetType()) && Tag is IDisposable tagDisposable)
            {
                tagDisposable?.Dispose();
            }
            this.Tag = null;
            this.dictNodeFlowData?.Clear();
            RunState = RunState.Completion;
        }
        // public NodeRunCts NodeRunCts { get; set; }
        // public ISereinIOC SereinIoc { get; }
        //public Task CreateTimingTask(Action action, int time = 100, int count = -1)
        //{
        //    if(NodeRunCts == null)
        //    {
        //        NodeRunCts = Env.IOC.Get<NodeRunCts>();
        //    }
        //    // 使用局部变量，避免捕获外部的 `action`
        //    Action localAction = action;

        //    return Task.Run(async () =>
        //    {
        //        for (int i = 0; i < count && !NodeRunCts.IsCancellationRequested; i++)
        //        {
        //            await Task.Delay(time);
        //            if (NodeRunCts.IsCancellationRequested) { break; }
        //            //if (FlowEnvironment.IsGlobalInterrupt)
        //            //{
        //            //    await FlowEnvironment.GetOrCreateGlobalInterruptAsync();
        //            //}
        //            // 确保对局部变量的引用
        //            localAction?.Invoke();
        //        }

        //        // 清理引用，避免闭包导致的内存泄漏
        //        localAction = null;
        //    });
        //}
    }
}

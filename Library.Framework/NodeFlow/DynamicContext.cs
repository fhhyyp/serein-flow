using Serein.Library.Api;
using System;
using System.Collections.Concurrent;

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
        /// 每个上下文分别存放节点的当前数据
        /// </summary>
        private readonly ConcurrentDictionary<string, object> dictNodeFlowData = new ConcurrentDictionary<string, object>();

        /// <summary>
        /// 获取节点当前数据
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <returns></returns>
        public object GetFlowData(string nodeGuid)
        {
            if (dictNodeFlowData.TryGetValue(nodeGuid, out var data))
            {
                return data;
            }
            {
                return null;
            }
        }

        /// <summary>
        /// 添加或更新当前节点数据
        /// </summary>
        /// <param name="nodeGuid">节点Guid</param>
        /// <param name="flowData">新的数据</param>
        public void AddOrUpdate(string nodeGuid, object flowData)
        {
            // this.dictNodeFlowData.TryGetValue(nodeGuid, out var oldFlowData);
            this.dictNodeFlowData[nodeGuid] = flowData;
        }

        /// <summary>
        /// 结束流程
        /// </summary>
        public void Exit()
        {
            foreach (var nodeObj in dictNodeFlowData.Values)
            {
                if (nodeObj != null)
                {
                    if (typeof(IDisposable).IsAssignableFrom(nodeObj?.GetType()) && nodeObj is IDisposable disposable)
                    {
                        disposable?.Dispose();
                    }
                }
            }
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

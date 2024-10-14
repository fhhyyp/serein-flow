using Serein.Library.Api;
using Serein.Library.Enums;
using Serein.Library.Utils;
using System.Collections.Concurrent;

namespace Serein.Library.Core.NodeFlow
{

    /// <summary>
    /// 动态流程上下文
    /// </summary>
    public class DynamicContext: IDynamicContext
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

        /// <summary>
        /// 运行环境
        /// </summary>
        public IFlowEnvironment Env { get; }

        /// <summary>
        /// 运行状态
        /// </summary>
        public RunState RunState { get; set; } = RunState.NoStart;

        /// <summary>
        /// 每个上下文分别存放节点的当前数据
        /// </summary>
        private readonly ConcurrentDictionary<string,object?> dictNodeFlowData = new ConcurrentDictionary<string, object?>();

        /// <summary>
        /// 获取节点当前数据
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <returns></returns>
        public object? GetFlowData(string nodeGuid)
        {
            if(dictNodeFlowData.TryGetValue(nodeGuid,out var data))
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
        public void AddOrUpdate(string nodeGuid,object? flowData)
        {
            // this.dictNodeFlowData.TryGetValue(nodeGuid, out var oldFlowData);
            this.dictNodeFlowData.AddOrUpdate(nodeGuid, _ => flowData, (_, _) => flowData);
        }

        /// <summary>
        /// 结束流程
        /// </summary>
        public void EndCurrentBranch()
        {
            this.dictNodeFlowData?.Clear();
            RunState = RunState.Completion;
        }

    }
}

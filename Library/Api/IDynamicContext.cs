using Serein.Library.Enums;
using Serein.Library.Utils;
using System;
using System.Threading.Tasks;

namespace Serein.Library.Api
{
    /// <summary>
    /// 流程上下文，包含运行环境接口，可以通过注册环境事件或调用环境接口，实现在流程运行时更改流程行为。
    /// </summary>
    public interface IDynamicContext
    {
        /// <summary>
        /// 运行环境，包含IOC容器。
        /// </summary>
        IFlowEnvironment Env { get; }

        RunState RunState { get; }

        /// <summary>
        /// 获取节点的数据（当前节点需要获取上一节点数据时，需要从 运行时上一节点 的Guid 通过这个方法进行获取
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <returns></returns>
        object GetFlowData(string nodeGuid);

        /// <summary>
        /// 添加或更新当前节点的数据
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="flowData"></param>
        void AddOrUpdate(string nodeGuid, object flowData);

        /// <summary>
        /// 用以提前结束分支运行
        /// </summary>
        void EndCurrentBranch();

        /*/// <summary>
        /// 定时循环触发
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="time"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        // Task CreateTimingTask(Action callback, int time = 100, int count = -1);*/
    } 
}

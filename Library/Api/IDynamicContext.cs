using Serein.Library;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Serein.Library.Api
{
    /// <summary>
    /// 流程上下文，包含运行环境接口，可以通过注册环境事件或调用环境接口，实现在流程运行时更改流程行为。
    /// </summary>
    public interface IDynamicContext
    {
        /// <summary>
        /// 标识流程
        /// </summary>
        string Guid {get; }

        /// <summary>
        /// 运行环境，包含IOC容器。
        /// </summary>
        IFlowEnvironment Env { get; }

        /// <summary>
        /// 是否正在运行
        /// </summary>
        RunState RunState { get; }

        /// <summary>
        /// 用来在当前流程上下文间传递数据
        /// </summary>
        //Dictionary<string, object> ContextShareData { get; }

        object Tag { get; set; }

        /// <summary>
        /// 下一个要执行的节点类别
        /// </summary>
        ConnectionInvokeType NextOrientation { get; set; }

        /// <summary>
        /// 运行时异常信息
        /// </summary>
        Exception ExceptionOfRuning { get; set; }

        /// <summary>
        /// 设置节点的运行时上一节点，用以多线程中隔开不同流程的数据
        /// </summary>
        /// <param name="currentNodeModel">当前节点</param>
        /// <param name="PreviousNode">运行时上一节点</param>
        void SetPreviousNode(NodeModelBase currentNodeModel, NodeModelBase PreviousNode);

        /// <summary>
        /// 获取当前节点的运行时上一节点，用以流程中获取数据
        /// </summary>
        /// <param name="currentNodeModel"></param>
        /// <returns></returns>
        NodeModelBase GetPreviousNode(NodeModelBase currentNodeModel);

        /// <summary>
        /// 获取节点的数据（当前节点需要获取上一节点数据时，需要从 运行时上一节点 的Guid 通过这个方法进行获取
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <returns></returns>
        object GetFlowData(string nodeGuid);

        /// <summary>
        /// 上一节点数据透传到下一节点
        /// </summary>
        /// <param name="nodeModel"></param>
        object TransmissionData(NodeModelBase nodeModel);

        /// <summary>
        /// 添加或更新当前节点的数据
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="flowData"></param>
        void AddOrUpdate(string nodeGuid, object flowData);

        /// <summary>
        /// 用以提前结束当前上下文流程的运行
        /// </summary>
        void Exit();

        


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

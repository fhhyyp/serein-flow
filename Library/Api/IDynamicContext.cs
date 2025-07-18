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
        /// 运行环境
        /// </summary>
        IFlowEnvironment Env { get; }


        /// <summary>
        /// 是否正在运行
        /// </summary>
        RunState RunState { get; }


        /// <summary>
        /// 下一个要执行的节点类别
        /// </summary>
        ConnectionInvokeType NextOrientation { get; set; }

        /// <summary>
        /// 运行时异常信息
        /// </summary>
        Exception ExceptionOfRuning { get; set; }


        /// <summary>
        /// 获取节点的运行时参数数据
        /// </summary>
        /// <param name="nodeModel">节点</param>
        /// <param name="index">第几个参数</param>
        /// <param name="data">数据</param>
        void SetParamsTempData(string nodeModel, int index, object data);

        /// <summary>
        /// 获取节点的运行时参数数据
        /// </summary>
        /// <param name="nodeModel">节点</param>
        /// <param name="index">第几个参数</param>
        /// <param name="data">获取到的参数</param>
        bool TryGetParamsTempData(string nodeModel, int index, out object data);



        /// <summary>
        /// 设置节点的运行时上一节点，用以多线程中隔开不同流程的数据
        /// </summary>
        /// <param name="currentNodeModel">当前节点</param>
        /// <param name="PreviousNode">运行时上一节点</param>
        void SetPreviousNode(string currentNodeModel, string PreviousNode);

        /// <summary>
        /// 获取当前节点的运行时上一节点，用以流程中获取数据
        /// </summary>
        /// <param name="currentNodeModel"></param>
        /// <returns></returns>
        string GetPreviousNode(string currentNodeModel);

        /// <summary>
        /// 获取节点的数据（当前节点需要获取上一节点数据时，需要从 运行时上一节点 的Guid 通过这个方法进行获取
        /// </summary>
        /// <param name="nodeModel"></param>
        /// <returns></returns>
        FlowResult GetFlowData(string nodeModel);
        /// <summary>
        /// 上一节点数据透传到下一节点
        /// </summary>
        /// <param name="nodeModel"></param>
        FlowResult TransmissionData(string nodeModel);

        /// <summary>
        /// 添加或更新当前节点的数据
        /// </summary>
        /// <param name="nodeModel"></param>
        /// <param name="flowData"></param>
        void AddOrUpdateFlowData(string nodeModel, FlowResult flowData);

        /// <summary>
        /// 添加或更新当前节点的数据
        /// </summary>
        /// <param name="nodeModel"></param>
        /// <param name="data"></param>
        void AddOrUpdate(string nodeModel, object data);


        /// <summary>
        /// 重置流程状态（用于对象池回收）
        /// </summary>
        void Reset();
        
        /// <summary>
        /// 用以提前结束当前上下文流程的运行
        /// </summary>
        void Exit();
    } 
}

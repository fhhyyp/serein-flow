using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Serein.Library.Api
{
#nullable enable
    /// <summary>
    /// 流程运行接口
    /// </summary>
    public interface IFlowControl
    {

        /// <summary>
        /// <para>单例模式IOC容器，内部维护了一个实例字典，默认使用类型的FullName作为Key，如果以“接口-实现类”的方式注册，那么将使用接口类型的FullName作为Key。</para>
        /// <para>当某个类型注册绑定成功后，将不会因为其它地方尝试注册相同类型的行为导致类型被重新创建。</para>
        /// </summary>
        ISereinIOC IOC { get; }

        /// <summary>
        /// <para>需要你提供一个由你实现的ISereinIOC接口实现类</para>
        /// <para>当你将流程运行环境集成在你的项目时，并希望流程运行时使用你提供的对象，而非自动创建</para>
        /// <para>就需要你调用这个方法，用来替换运行环境的IOC容器</para>
        /// <para>注意，是流程运行时，而非运行环境</para>
        /// </summary>
        /// <param name="ioc"></param>
        /// <param name="setDefultMemberOnReset">用于每次启动时，重置IOC后默认注册某些类型</param>
        void UseExternalIOC(ISereinIOC ioc, Action<ISereinIOC>? setDefultMemberOnReset = null);

        /// <summary>
        /// 开始运行流程
        /// </summary>
        /// <param name="canvasGuids">需要运行的流程Guid</param>
        /// <returns></returns>
        Task<bool> StartFlowAsync(string[] canvasGuids);

        /// <summary>
        /// 从选定的节点开始运行
        /// </summary>
        /// <param name="startNodeGuid"></param>
        /// <returns></returns>
        Task<TResult> StartFlowAsync<TResult>(string startNodeGuid);
        
        /// <summary>
        /// 从选定的节点开始运行
        /// </summary>
        /// <param name="startNodeGuid"></param>
        /// <returns></returns>
        Task StartFlowAsync(string startNodeGuid);

        /// <summary>
        /// 结束运行
        /// </summary>
        Task<bool> ExitFlowAsync();

        /// <summary>
        /// 激活未启动的全局触发器
        /// </summary>
        /// <param name="nodeGuid"></param>
        void ActivateFlipflopNode(string nodeGuid);

        /// <summary>
        /// 终结一个全局触发器，在它触发后将不会再次监听消息（表现为已经启动的触发器至少会再次处理一次消息，后面版本再修正这个非预期行为）
        /// </summary>
        /// <param name="nodeGuid"></param>
        void TerminateFlipflopNode(string nodeGuid);

        /// <summary>
        /// 流程启动器调用，监视数据更新通知
        /// </summary>
        /// <param name="nodeGuid">更新了数据的节点Guid</param>
        /// <param name="monitorData">更新的数据</param>
        /// <param name="sourceType">更新的数据</param>
        void MonitorObjectNotification(string nodeGuid, object monitorData, MonitorObjectEventArgs.ObjSourceType sourceType);

        /// <summary>
        /// 流程启动器调用，节点触发了中断
        /// </summary>
        /// <param name="nodeGuid">被中断的节点Guid</param>
        /// <param name="expression">被触发的表达式</param>
        /// <param name="type">中断类型。0主动监视，1表达式</param>
        void TriggerInterrupt(string nodeGuid, string expression, InterruptTriggerEventArgs.InterruptTriggerType type);


        /// <summary>
        /// 调用流程接口，将返回 FlowResult.Value。如果需要 FlowResult 对象，请使用该方法的泛型版本。
        /// </summary>
        /// <param name="apiGuid">流程接口节点Guid</param>
        /// <param name="dict">调用时入参参数</param>
        /// <returns></returns>
        Task<object> InvokeAsync(string apiGuid, Dictionary<string, object> dict);


        
        /// <summary>
        /// 调用流程接口，将返回 FlowResult.Value。如果需要 FlowResult 对象，请使用该方法的泛型版本。
        /// </summary>
        /// <param name="apiGuid">流程接口节点Guid</param>
        /// <param name="dict">调用时入参参数</param>
        /// <returns></returns>
        Task<TResult> InvokeAsync<TResult>(string apiGuid, Dictionary<string, object> dict);


    }


}

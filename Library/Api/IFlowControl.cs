using System.Threading.Tasks;

namespace Serein.Library.Api
{
    /// <summary>
    /// 流程运行接口
    /// </summary>
    public interface IFlowControl
    {
        /// <summary>
        /// <para>需要你提供一个由你实现的ISereinIOC接口实现类</para>
        /// <para>当你将流程运行环境集成在你的项目时，并希望流程运行时使用你提供的对象，而非自动创建</para>
        /// <para>就需要你调用这个方法，用来替换运行环境的IOC容器</para>
        /// <para>注意，是流程运行时，而非运行环境</para>
        /// </summary>
        /// <param name="ioc"></param>
        void UseExternalIOC(ISereinIOC ioc);

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
        Task<bool> StartFlowFromSelectNodeAsync(string startNodeGuid);

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
    }


}

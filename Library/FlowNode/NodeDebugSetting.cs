using Newtonsoft.Json.Linq;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library
{
    /// <summary>
    /// 节点调试设置，用于中断节点的运行
    /// </summary>
    [NodeProperty(ValuePath = NodeValuePath.DebugSetting)]
    public partial class NodeDebugSetting
    {
        /// <summary>
        /// 创建属于某个节点的调试设置
        /// </summary>
        /// <param name="nodeModel"></param>
        public NodeDebugSetting(NodeModelBase nodeModel)
        {
            NodeModel = nodeModel;
        }

        /// <summary>
        /// 对应的节点
        /// </summary>
        [PropertyInfo(IsProtection = true)]
        private NodeModelBase _nodeModel;

        /// <summary>
        /// 是否使能
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private bool _isEnable = true;

        /// <summary>
        ///  是否中断节点。
        /// </summary>
        [PropertyInfo(IsNotification = true, CustomCodeAtEnd = "ChangeInterruptState(value);")] // CustomCode = "NodeModel?.Env?.SetNodeInterruptAsync(NodeModel?.Guid, value);"
        private bool _isInterrupt = false;

    }

    /// <summary>
    /// 节点中断
    /// </summary>
    public partial class NodeDebugSetting
    {
        /// <summary>
        /// 取消中断的回调函数
        /// </summary>
        private Action _cancelInterrupt { get; set; }
        /// <summary>
        /// 取消中断
        /// </summary>
        public Action CancelInterrupt => _cancelInterrupt;
        /// <summary>
        /// 中断节点
        /// </summary>
        public Func<Task> _getInterruptTask;

        /// <summary>
        /// 获取中断的Task
        /// </summary>
        public Func<Task> GetInterruptTask => _getInterruptTask;


        /// <summary>
        /// 改变中断状态
        /// </summary>
        public void ChangeInterruptState(bool state)
        {
            if (state && _getInterruptTask is null)
            {
                // 设置获取中断的委托
                _getInterruptTask = () => NodeModel.Env.IOC.Get<FlowInterruptTool>().WaitTriggerAsync(NodeModel.Guid);
                
            }
            else if (!state)
            {
                if (_getInterruptTask is null)
                {

                }
                else
                {
                    // 设置解除中断的委托
                    _cancelInterrupt = () => NodeModel.Env.IOC.Get<FlowInterruptTool>().InvokeTrigger(NodeModel.Guid);
                    _cancelInterrupt.Invoke();
                    _getInterruptTask = null;
                }
               
            }
        }


    }
}




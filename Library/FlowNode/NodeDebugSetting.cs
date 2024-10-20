﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static Serein.Library.Utils.ChannelFlowInterrupt;

namespace Serein.Library
{
    /// <summary>
    /// 节点调试设置，用于中断节点的运行
    /// </summary>
    [NodeProperty(ValuePath = NodeValuePath.DebugSetting)]
    public partial class NodeDebugSetting
    {
        private readonly NodeModelBase nodeModel;
        /// <summary>
        /// 创建属于某个节点的调试设置
        /// </summary>
        /// <param name="nodeModel"></param>
        public NodeDebugSetting(NodeModelBase nodeModel)
        {
            this.nodeModel = nodeModel;
        }
        /// <summary>
        /// 是否使能
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private bool _isEnable = true;

        /// <summary>
        ///  中断级别，暂时停止继续执行后继分支。
        /// </summary>
        [PropertyInfo]
        private InterruptClass _interruptClass = InterruptClass.None;

        /// <summary>
        ///  中断级别，暂时停止继续执行后继分支。
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private bool _isInterrupt = false;


        /// <summary>
        /// 取消中断的回调函数
        /// </summary>
        [PropertyInfo]
        private Action _cancelInterruptCallback;

        /// <summary>
        /// 中断Task（用来中断）
        /// </summary>
        [PropertyInfo]
        private Func<Task<CancelType>> _getInterruptTask;
    }



    /// <summary>
    /// 中断级别，暂时停止继续执行后继分支。
    /// </summary>
    public enum InterruptClass
    {
        /// <summary>
        /// 不中断
        /// </summary>
        None,
        /// <summary>
        /// 分支中断，中断进入当前节点的分支。
        /// </summary>
        Branch,
        /// <summary>
        /// 全局中断，中断全局所有节点的运行。（暂未实现相关）
        /// </summary>
        Global,
    }
}




using System;
using System.Collections.Generic;
using System.Text;

namespace Serein.Library.Entity
{
    public class NodeDebugSetting
    {
        /// <summary>
        /// 是否使能（调试中断功能）
        /// </summary>
        public bool IsEnable { get; set; } = true;

        /// <summary>
        /// 是否监视数据改变
        /// </summary>
        public bool IsMonitorFlowData { get; set; } = false;

        /// <summary>
        ///  中断级别，暂时停止继续执行后继分支。
        /// </summary>
        public InterruptClass InterruptClass { get; set; } = InterruptClass.None;


        public List<string> InterruptExpression {  get; } = new List<string>();
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
        /// 分组中断，中断进入指定节点分组的分支。（暂未实现相关）
        /// </summary>
        Group,
        /// <summary>
        /// 全局中断，中断全局所有节点的运行。（暂未实现相关）
        /// </summary>
        Global,
    }
}

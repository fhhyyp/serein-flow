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
        /// 是否中断（调试中断功能）
        /// </summary>
        public bool IsInterrupt { get; set; } = false;

        /// <summary>
        ///  中断级别，暂时停止继续执行后继分支。
        /// </summary>
        public InterruptClass InterruptClass { get; set; } = InterruptClass.None;
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
        /// 分支中断，当前节点。
        /// </summary>
        Branch,
        /// <summary>
        /// 分组中断，相同中断分组的节点。
        /// </summary>
        Group,
        /// <summary>
        /// 全局中断，其它所有节点。
        /// </summary>
        Global,
    }
}

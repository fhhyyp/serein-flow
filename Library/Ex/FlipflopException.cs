using System;
using System.CodeDom;

namespace Serein.Library
{
    /// <summary>
    /// 触发器异常
    /// </summary>
    public class FlipflopException: Exception
    {
        public enum CancelClass
        {
            /// <summary>
            /// 取消触发器当前所在分支的继续执行
            /// </summary>
            CancelBranch,
            /// <summary>
            /// 取消整个触发器流程的再次执行（用于停止全局触发器）
            /// </summary>
            CancelFlow,
        }
        /// <summary>
        /// 是否已取消
        /// </summary>
        public bool IsCancel { get; }
        /// <summary>
        /// 取消类型
        /// </summary>
        public CancelClass Type { get; }
        public FlipflopException(string message, bool isCancel = true,CancelClass clsss = CancelClass.CancelBranch) :base(message) 
        {
            IsCancel = isCancel;
            Type = clsss;
        }
    }
}

using System;
using System.CodeDom;

namespace Serein.Library.Ex
{
    /// <summary>
    /// 触发器
    /// </summary>
    public class FlipflopException: Exception
    {
        public enum CancelClass
        {
            // 取消当前分支的继续执行
            Branch,
            // 取消整个触发器流程的再次执行
            Flow,
        }
        public bool IsCancel { get; }
        public CancelClass Clsss { get; }
        public FlipflopException(string message, bool isCancel = true,CancelClass clsss = CancelClass.Branch) :base(message) 
        {
            IsCancel = isCancel;
            Clsss = clsss;
        }
    }
}

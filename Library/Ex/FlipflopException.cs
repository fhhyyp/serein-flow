using System;

namespace Serein.Library.Ex
{
    /// <summary>
    /// 触发器
    /// </summary>
    public class FlipflopException: Exception
    {
        public bool IsCancel { get; }
        public FlipflopException(string message, bool isCancel = true) :base(message) 
        {
            IsCancel = isCancel;
        }
    }
}

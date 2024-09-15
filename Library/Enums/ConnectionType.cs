using System;
using System.Collections.Generic;
using System.Text;

namespace Serein.Library.Enums
{
    public enum ConnectionType
    {
        /// <summary>
        /// 不执行分支
        /// </summary>
        None,
        /// <summary>
        /// 真分支
        /// </summary>
        IsSucceed,
        /// <summary>
        /// 假分支
        /// </summary>
        IsFail,
        /// <summary>
        /// 异常发生分支
        /// </summary>
        IsError,
        /// <summary>
        /// 上游分支（执行当前节点前会执行一次上游分支）
        /// </summary>
        Upstream,
    }

    
}

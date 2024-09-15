using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow
{
    public enum ConnectionType
    {
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

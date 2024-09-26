using System;
using System.Collections.Generic;
using System.Text;

namespace Serein.Library.Enums
{
    public enum ConnectionType
    {
        /// <summary>
        /// 默认属性
        /// </summary>
        None,
        /// <summary>
        /// 上游分支（执行当前节点前会执行一次上游分支），默认执行。
        /// </summary>
        Upstream,
        /// <summary>
        /// 真分支（表示当前节点顺利完成）
        /// </summary>
        IsSucceed,
        /// <summary>
        /// 假分支（一般用于条件控件，条件为假时才会触发该类型的分支）
        /// </summary>
        IsFail,
        /// <summary>
        /// 异常发生分支（当前节点对应的方法执行时出现非预期的异常）
        /// </summary>
        IsError,

    }

    
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Enums
{
    /// <summary>
    /// 触发器说明
    /// </summary>
    public enum FlipflopStateType
    {
        /// <summary>
        /// 成功（方法成功执行）
        /// </summary>
        Succeed,
        /// <summary>
        /// 失败（方法没有成功执行，不过执行时没有发生非预期的错误）
        /// </summary>
        Fail,
        /// <summary>
        /// 异常（节点没有成功执行，执行时发生非预期的错误）
        /// </summary>
        Error,
        /// <summary>
        /// 取消（将不会执行触发器的后继节点）
        /// </summary>
        Cancel,
    }


}

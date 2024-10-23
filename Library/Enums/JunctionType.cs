using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library
{
    /// <summary>
    /// 连接点类型
    /// </summary>
    public enum JunctionType
    {
        /// <summary>
        /// 当前执行
        /// </summary>
        Execute,
        /// <summary>
        /// 入参
        /// </summary>
        ArgData,
        /// <summary>
        /// 返回值
        /// </summary>
        ReturnData,
        /// <summary>
        /// 下一步要执行的节点
        /// </summary>
        NextStep,
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library
{
    /// <summary>
    /// 连接的控制点类型枚举
    /// </summary>
    public enum JunctionOfConnectionType
    {
        /// <summary>
        /// 没有关系，用于处理非预期连接的情况需要的返回值
        /// </summary>
        None,
        /// <summary>
        /// 表示方法执行顺序关系
        /// </summary>
        Invoke,
        /// <summary>
        /// 表示参数获取来源关系
        /// </summary>
        Arg
    }
}

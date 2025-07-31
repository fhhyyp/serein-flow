using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library
{
    /// <summary>
    /// 信息输出等级
    /// </summary>
    public enum InfoClass
    {
        /// <summary>
        /// 调试
        /// </summary>
        Debug,
        /// <summary>
        /// 一般的
        /// </summary>
        General,
        /// <summary>
        /// 重要的
        /// </summary>
        Important,
    }


    /// <summary>
    /// 信息类别
    /// </summary>
    public enum InfoType
    {
        /// <summary>
        /// 普通信息
        /// </summary>
        INFO,
        /// <summary>
        /// 错误信息（但不影响运行）
        /// </summary>
        WARN,
        /// <summary>
        /// 异常信息（影响了运行）
        /// </summary>
        ERROR,
    }
}

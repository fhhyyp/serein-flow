using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Enums
{
    /// <summary>
    /// 流程运行状态
    /// </summary>
    public enum RunState
    {
        /// <summary>
        /// 初始化值，等待开始。只有初始化时才会存在该值，后续每次重新开始都是从 Completion 变成 Running）
        /// </summary>
        NoStart,
        /// <summary>
        /// 正在运行
        /// </summary>
        Running,
        /// <summary>
        /// 运行完成
        /// </summary>
        Completion,
    }
}

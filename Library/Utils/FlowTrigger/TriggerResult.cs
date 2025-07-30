using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{
    /// <summary>
    /// 触发器结果类，用于存储触发器的类型和返回值。
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    public class TriggerResult<TResult>
    {
        /// <summary>
        /// 触发类型
        /// </summary>
        public TriggerDescription Type { get; set; }

        /// <summary>
        /// 触发结果值
        /// </summary>
        public TResult Value { get; set; }
    }
}

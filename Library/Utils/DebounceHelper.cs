using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{
    /// <summary>
    /// 消息防抖
    /// </summary>
    public static class DebounceHelper
    {
        private static readonly ConcurrentDictionary<string, DateTime> _lastExecutionTimes = new ConcurrentDictionary<string, DateTime>();
        private static readonly object _lockObject = new object();

        /// <summary>
        /// 检查是否可以执行操作，根据传入的 key 和 debounceTime 来决定是否允许执行
        /// </summary>
        /// <param name="key">操作的唯一标识</param>
        /// <param name="debounceTimeInMs">防抖时间，单位为毫秒</param>
        /// <returns>如果可以执行操作，返回 true；否则返回 false</returns>
        public static bool CanExecute(string key, int debounceTimeInMs)
        {
            lock (_lockObject)
            {
                var currentTime = DateTime.Now;

                if (_lastExecutionTimes.TryGetValue(key, out DateTime lastExecutionTime))
                {
                    var timeSinceLastExecution = (currentTime - lastExecutionTime).TotalMilliseconds;

                    if (timeSinceLastExecution < debounceTimeInMs)
                    {
                        // 如果距离上次执行时间小于防抖时间，不允许执行
                        return false;
                    }
                }

                // 更新上次执行时间
                _lastExecutionTimes[key] = currentTime;
                return true;
            }
        }
    }

}

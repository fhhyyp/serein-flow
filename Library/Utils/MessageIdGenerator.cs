using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{
    /// <summary>
    /// 消息ID生成工具
    /// </summary>
    public class MessageIdGenerator
    {
        private static readonly object _lock = new object();
        private static int _counter = 0;

        /// <summary>
        /// 生成一个不重复的标识
        /// </summary>
        /// <param name="theme"></param>
        /// <returns></returns>
        public static string GenerateMessageId(string theme)
        {
            lock (_lock)
            {
                // 时间戳
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // 机器标识（可以替换成更加独特的标识，如机器的MAC地址等）
                string machineId = GetMachineId();

                // 进程ID
                int processId = Process.GetCurrentProcess().Id;

                // 递增计数器，确保在同一毫秒内的多次生成也不重复
                int count = _counter++;

                // 随机数
                byte[] randomBytes = new byte[8];
                using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(randomBytes);
                }
                string randomPart = BitConverter.ToString(randomBytes).Replace("-", "");

                // 将所有部分组合起来
                return $"{timestamp}-{machineId}-{processId}-{count}-{randomPart}-{theme}";
            }
        }

        private static string GetMachineId()
        {
            // 这里使用 GUID 模拟机器标识
            // 可以替换为更具体的机器信息
            return Guid.NewGuid().ToString("N");
        }
    }

}

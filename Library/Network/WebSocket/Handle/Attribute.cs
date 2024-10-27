using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serein.Library.Network.WebSocketCommunication.Handle
{
    /// <summary>
    /// 表示参数可以为空(Net462不能使用NutNull的情况）
    /// </summary>
    public sealed class NeedfulAttribute : Attribute
    {
    }

    /// <summary>
    /// 消息ID生成器
    /// </summary>
    public class MsgIdHelper
    {
        private static readonly long _epoch = new DateTime(2023, 1, 1).Ticks; // 自定义起始时间
        private static long _lastTimestamp = -1L; // 上一次生成 ID 的时间戳
        private static long _sequence = 0L; // 序列号

        /// <summary>
        /// 获取新的ID
        /// </summary>
        public static long NewId => GenerateId();


        /// <summary>
        /// 生成消息ID
        /// </summary>
        /// <returns></returns>
        public static long GenerateId()
        {
            long timestamp = DateTime.UtcNow.Ticks;

            // 如果时间戳是一样的，递增序列号
            if (timestamp == _lastTimestamp)
            {
                // 使用原子操作增加序列号
                _sequence = Interlocked.Increment(ref _sequence);
                if (_sequence > 999999) // 序列号最大值，6位
                {
                    // 等待下一毫秒
                    while (timestamp <= _lastTimestamp)
                    {
                        timestamp = DateTime.UtcNow.Ticks;
                    }
                }
            }
            else
            {
                _sequence = 0; // 重置序列号
            }

            _lastTimestamp = timestamp;

            // 生成 ID：时间戳和序列号拼接
            return (timestamp - _epoch) * 1000 + _sequence; // 返回 ID
        }
    }




}

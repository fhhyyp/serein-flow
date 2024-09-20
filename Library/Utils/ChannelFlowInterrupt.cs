using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{
    public class ChannelFlowInterrupt
    {
        /// <summary>
        /// 中断取消类型
        /// </summary>
        public enum CancelType
        {
            Manual,
            Overtime
        }

        // 使用并发字典管理每个信号对应的 Channel
        private readonly ConcurrentDictionary<string, Channel<CancelType>> _channels = new ConcurrentDictionary<string, Channel<CancelType>>();

        /// <summary>
        /// 创建信号并指定超时时间，到期后自动触发（异步方法）
        /// </summary>
        /// <param name="signal">信号标识符</param>
        /// <param name="outTime">超时时间</param>
        /// <returns>等待任务</returns>
        public async Task<CancelType> CreateChannelWithTimeoutAsync(string signal, TimeSpan outTime)
        {
            var channel = GetOrCreateChannel(signal);
            var cts = new CancellationTokenSource();

            // 异步任务：超时后自动触发信号
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(outTime, cts.Token);
                    await channel.Writer.WriteAsync(CancelType.Overtime);
                }
                catch (OperationCanceledException)
                {
                    // 超时任务被取消
                }
            }, cts.Token);

            // 等待信号传入（超时或手动触发）
            var result = await channel.Reader.ReadAsync();
            return result;
        }

        /// <summary>
        /// 创建信号并指定超时时间，到期后自动触发（同步阻塞方法）
        /// </summary>
        /// <param name="signal">信号标识符</param>
        /// <param name="timeout">超时时间</param>
        public CancelType CreateChannelWithTimeoutSync(string signal, TimeSpan timeout)
        {
            var channel = GetOrCreateChannel(signal);
            var cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;

            // 异步任务：超时后自动触发信号
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(timeout, token);
                    await channel.Writer.WriteAsync(CancelType.Overtime);
                }
                catch (OperationCanceledException)
                {
                    // 任务被取消
                }
            });

            // 同步阻塞直到信号触发或超时
            var result = channel.Reader.ReadAsync().AsTask().GetAwaiter().GetResult();
            return result;
        }

        /// <summary>
        /// 触发信号
        /// </summary>
        /// <param name="signal">信号字符串</param>
        /// <returns>是否成功触发</returns>
        public bool TriggerSignal(string signal)
        {
            if (_channels.TryGetValue(signal, out var channel))
            {
                // 手动触发信号
                channel.Writer.TryWrite(CancelType.Manual);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 取消所有任务
        /// </summary>
        public void CancelAllTasks()
        {
            foreach (var channel in _channels.Values)
            {
                channel.Writer.Complete();
            }
            _channels.Clear();
        }

        /// <summary>
        /// 获取或创建指定信号的 Channel
        /// </summary>
        /// <param name="signal">信号字符串</param>
        /// <returns>对应的 Channel</returns>
        private Channel<CancelType> GetOrCreateChannel(string signal)
        {
            return _channels.GetOrAdd(signal, _ => Channel.CreateUnbounded<CancelType>());
        }
    }
}


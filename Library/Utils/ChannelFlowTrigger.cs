

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;


namespace Serein.Library.Utils
{



    public class ChannelFlowTrigger<TSignal> 
    {
        // 使用并发字典管理每个枚举信号对应的 Channel
        private readonly ConcurrentDictionary<TSignal, Channel<(TriggerType,object)>> _channels = new ConcurrentDictionary<TSignal, Channel<(TriggerType, object)>>();

        /// <summary>
        /// 创建信号并指定超时时间，到期后自动触发（异步方法）
        /// </summary>
        /// <param name="signal">枚举信号标识符</param>
        /// <param name="outTime">超时时间</param>
        /// <returns>等待任务</returns>
        public async Task<(TriggerType, TResult)> WaitDataWithTimeoutAsync<TResult>(TSignal signal, TimeSpan outTime)
        {
            var channel = GetOrCreateChannel(signal);
            var cts = new CancellationTokenSource();

            // 异步任务：超时后自动触发信号
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(outTime, cts.Token);
                    await channel.Writer.WriteAsync((TriggerType.Overtime, null));
                }
                catch (OperationCanceledException)
                {
                    // 超时任务被取消
                }
            }, cts.Token);

            // 等待信号传入（超时或手动触发）
            (var type, var result) = await channel.Reader.ReadAsync();

            return (type, result.ToConvert<TResult>());
        }

        /// <summary>
        /// 创建信号，直到触发
        /// </summary>
        /// <param name="signal">枚举信号标识符</param>
        /// <returns>等待任务</returns>
        public async Task<TResult> WaitData<TResult>(TSignal signal)
        {
            var channel = GetOrCreateChannel(signal);
            // 等待信号传入（超时或手动触发）
            (var type, var result) = await channel.Reader.ReadAsync();
            return result.ToConvert<TResult>();
        }


        /// <summary>
        /// 触发信号
        /// </summary>
        /// <param name="signal">枚举信号标识符</param>
        /// <returns>是否成功触发</returns>
        public bool TriggerSignal(TSignal signal, object value)
        {
            if (_channels.TryGetValue(signal, out var channel))
            {
                // 手动触发信号
                channel.Writer.TryWrite((TriggerType.External,value));
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
        /// <param name="signal">枚举信号标识符</param>
        /// <returns>对应的 Channel</returns>
        private Channel<(TriggerType, object)> GetOrCreateChannel(TSignal signal)
        {
            return _channels.GetOrAdd(signal, _ => Channel.CreateUnbounded<(TriggerType, object)>());
        }
    }
}

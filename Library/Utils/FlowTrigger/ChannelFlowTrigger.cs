using Serein.Library.Api;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;


namespace Serein.Library.Utils
{

    /// <summary>
    /// 基于 Channel 的触发器实现
    /// </summary>
    /// <typeparam name="TSignal"></typeparam>
    public class ChannelFlowTrigger<TSignal> : IFlowTrigger<TSignal>
    {
        // 使用并发字典管理每个枚举信号对应的 Channel
        private readonly ConcurrentDictionary<TSignal, Channel<TriggerResult<object>>> _channels = new ConcurrentDictionary<TSignal, Channel<TriggerResult<object>>>();

        /// <summary>
        /// 获取或创建指定信号的 Channel
        /// </summary>
        /// <param name="signal">枚举信号标识符</param>
        /// <returns>对应的 Channel</returns>
        private Channel<TriggerResult<object>> GetOrCreateChannel(TSignal signal)
        {
            return _channels.GetOrAdd(signal, _ => Channel.CreateUnbounded<TriggerResult<object>>());
        }

        /// <summary>
        /// 等待信号触发并指定超时时间
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="signal"></param>
        /// <param name="outTime"></param>
        /// <returns></returns>
        public async Task<TriggerResult<TResult>> WaitTriggerWithTimeoutAsync<TResult>(TSignal signal, TimeSpan outTime)
        {
            var channel = GetOrCreateChannel(signal);
            var cts = new CancellationTokenSource();

            // 异步任务：超时后自动触发信号
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(outTime, cts.Token);
                    var outResult = new TriggerResult<object>()
                    {
                        Type = TriggerDescription.Overtime
                    };
                    await channel.Writer.WriteAsync(outResult);
                }
                catch (OperationCanceledException)
                {
                    // 超时任务被取消
                }
            }, cts.Token);

            // 等待信号传入（超时或手动触发）
            var result = await WaitTriggerAsync<TResult>(signal); // 返回一个可以超时触发的等待任务
            return result;


        }

        /// <summary>
        /// 等待信号触发
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="signal"></param>
        /// <returns></returns>
        public async Task<TriggerResult<TResult>> WaitTriggerAsync<TResult>(TSignal signal)
        {
            var channel = GetOrCreateChannel(signal);
            // 等待信号传入（超时或手动触发）
            var result = await channel.Reader.ReadAsync();
            if (result.Value is TResult data)
            {
                return new TriggerResult<TResult>()
                {
                    Value = data,
                    Type = TriggerDescription.External,
                };
            }
            else
            {
                return new TriggerResult<TResult>()
                {
                    Type = TriggerDescription.TypeInconsistency,
                };
            }
        }

        /// <summary>
        /// 调用触发器
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="signal"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<bool> InvokeTriggerAsync<TResult>(TSignal signal, TResult value)
        {
            if (_channels.TryGetValue(signal, out var channel))
            {
                // 手动触发信号
                var result = new TriggerResult<object>()
                {
                    Type = TriggerDescription.External,
                    Value = value
                };
                await channel.Writer.WriteAsync(result);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 取消所有触发器
        /// </summary>
        public void CancelAllTrigger()
        {
            foreach (var channel in _channels.Values)
            {
                channel.Writer.Complete();
            }
            _channels.Clear();
        }
    }










}

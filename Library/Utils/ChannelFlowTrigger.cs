
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;


namespace Serein.Library.NodeFlow.Tool
{
    /// <summary>
    /// 触发类型
    /// </summary>
    public enum TriggerType
    {
        /// <summary>
        /// 外部触发
        /// </summary>
        External,
        /// <summary>
        /// 超时触发
        /// </summary>
        Overtime
    }
    public class TriggerData
    {
        public TriggerType Type { get; set; }
        public object Value { get; set; }
    }


    public class ChannelFlowTrigger<TSignal> where TSignal : struct, Enum
    {
        // 使用并发字典管理每个枚举信号对应的 Channel
        private readonly ConcurrentDictionary<TSignal, Channel<TriggerData>> _channels = new ConcurrentDictionary<TSignal, Channel<TriggerData>>();


        // 到期后自动触发。短时间内触发频率过高的情况下，请将 outTime 设置位短一些的时间，因为如果超时等待时间过长，会导致非预期的“托管内存泄露”。

        /// <summary>
        /// 创建信号并指定超时时间的Channel.
        /// </summary>
        /// <param name="signal">枚举信号标识符</param>
        /// <param name="outTime">超时时间</param>
        /// <returns>等待任务</returns>
        public async Task<TriggerData> CreateChannelWithTimeoutAsync<TResult>(TSignal signal, TimeSpan outTime, TResult outValue)
        {
            var channel = GetOrCreateChannel(signal);
            //var cts = new CancellationTokenSource();
            //// 异步任务：超时后自动触发信号
            //_ = Task.Run(async () =>
            //{
            //    try
            //    {
            //        await Task.Delay(outTime, cts.Token);
            //        if(!cts.IsCancellationRequested) // 如果还没有被取消
            //        {
            //            TriggerData triggerData = new TriggerData()
            //            {
            //                Value = outValue,
            //                Type = TriggerType.Overtime,
            //            };
            //            await channel.Writer.WriteAsync(triggerData);
            //        }
            //    }
            //    catch (OperationCanceledException)
            //    {
            //        // 超时任务被取消
            //    }
            //    finally
            //    {
            //        cts?.Cancel();
            //        cts?.Dispose();  // 确保 cts 被释放
            //    }
            //}, cts.Token);
            

            // 等待信号传入（超时或手动触发）
            var result = await channel.Reader.ReadAsync();
            //cts?.Cancel();
            //cts?.Dispose();
            return result;
        }

        /// <summary>
        /// 触发信号
        /// </summary>
        /// <param name="signal">枚举信号标识符</param>
        /// <returns>是否成功触发</returns>
        public bool TriggerSignal<TResult>(TSignal signal, TResult value)
        {
            if (_channels.TryGetValue(signal, out var channel))
            {
                TriggerData triggerData = new TriggerData()
                {
                    Value = value,
                    Type = TriggerType.External,
                };
                // 手动触发信号
                channel.Writer.TryWrite(triggerData);
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
        private Channel<TriggerData> GetOrCreateChannel(TSignal signal)
        {
            return _channels.GetOrAdd(signal, _ => Channel.CreateUnbounded<TriggerData>());
        }
    }
}

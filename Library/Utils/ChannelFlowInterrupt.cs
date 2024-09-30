#region plan 2
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{
    /// <summary>
    /// 流程中断管理
    /// </summary>
    public class ChannelFlowInterrupt
    {
        /// <summary>
        /// 中断取消类型
        /// </summary>
        public enum CancelType
        {
            Manual,
            Error,
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
        public async Task<CancelType> GetCreateChannelWithTimeoutAsync(string signal, TimeSpan outTime)
        {
            var channel = GetOrCreateChannel(signal);
            var cts = new CancellationTokenSource();

            // 异步任务：超时后自动触发信号
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(outTime, cts.Token);
                    if (!cts.Token.IsCancellationRequested)
                    {
                        await channel.Writer.WriteAsync(CancelType.Overtime);
                    }
                }
                catch (OperationCanceledException)
                {
                    // 超时任务被取消
                }
                finally
                {
                    cts?.Dispose();
                }
            }, cts.Token);

            // 等待信号传入（超时或手动触发）
            try
            {
                var result = await channel.Reader.ReadAsync();
                return result;
            }
            catch
            {
                return CancelType.Error;
            }

        }


        /// <summary>
        /// 创建信号，直到手动触发（异步方法）
        /// </summary>
        /// <param name="signal">信号标识符</param>
        /// <param name="outTime">超时时间</param>
        /// <returns>等待任务</returns>
        public async Task<CancelType> GetOrCreateChannelAsync(string signal)
        {
            try
            {
                var channel = GetOrCreateChannel(signal);
                // 等待信号传入（超时或手动触发）
                var result = await channel.Reader.ReadAsync();
                return result;
            }
            catch
            {
                return CancelType.Manual;
            }
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
                catch (OperationCanceledException ex)
                {
                    // 任务被取消
                    await Console.Out.WriteLineAsync(ex.Message);
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
            //if (_channels.TryGetValue(signal, out var channel))
            //{
            //    // 手动触发信号
            //    channel.Writer.TryWrite(CancelType.Manual);
            //    return true;
            //}
            //return false;


            try
            {
                if (_channels.TryGetValue(signal, out var channel))
                {
                    // 手动触发信号
                    channel.Writer.TryWrite(CancelType.Manual);

                    // 完成写入，标记该信号通道不再接受新写入
                    channel.Writer.Complete();

                    // 触发后移除信号
                    _channels.TryRemove(signal, out _);

                    return true;
                }
                return false;
            }
            catch
            {

                return false;
            }

        }

        /// <summary>
        /// 取消所有任务
        /// </summary>
        public void CancelAllTasks()
        {
            foreach (var channel in _channels.Values)
            {
                try
                {
                    channel.Writer.Complete();
                }
                finally
                {

                }
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

#endregion

#region plan 3

//using System;
//using System.Collections.Concurrent;
//using System.Threading;
//using System.Threading.Channels;
//using System.Threading.Tasks;

//namespace Serein.Library.Utils
//{
//    /// <summary>
//    /// 流程中断管理类，提供了基于 Channel 的异步中断机制
//    /// </summary>
//    public class ChannelFlowInterrupt
//    {
//        /// <summary>
//        /// 中断取消类型
//        /// </summary>
//        public enum CancelType
//        {
//            Manual,   // 手动触发
//            Overtime, // 超时触发
//            Discard   // 丢弃触发
//        }

//        // 使用并发字典管理每个信号对应的 Channel 和状态
//        private readonly ConcurrentDictionary<string, (Channel<CancelType> Channel, bool IsCancelled, bool IsDiscardMode)> _channels
//            = new ConcurrentDictionary<string, (Channel<CancelType>, bool, bool)>();

//        // 锁对象，用于保护并发访问
//        private readonly object _lock = new object();

//        /// <summary>
//        /// 创建带有超时功能的信号，超时后自动触发
//        /// </summary>
//        public async Task<CancelType> GetCreateChannelWithTimeoutAsync(string signal, TimeSpan outTime)
//        {
//            var (channel, isCancelled, isDiscardMode) = GetOrCreateChannel(signal);

//            // 如果信号已取消或在丢弃模式下，立即返回丢弃类型
//            if (isCancelled || isDiscardMode) return CancelType.Discard;

//            var cts = new CancellationTokenSource();

//            _ = Task.Run(async () =>
//            {
//                try
//                {
//                    await Task.Delay(outTime, cts.Token);
//                    if (!cts.Token.IsCancellationRequested && !isCancelled)
//                    {
//                        await channel.Writer.WriteAsync(CancelType.Overtime);
//                    }
//                }
//                catch (OperationCanceledException)
//                {
//                    // 处理任务取消的情况
//                }
//                finally
//                {
//                    cts.Dispose();
//                }
//            }, cts.Token);

//            return await channel.Reader.ReadAsync();
//        }

//        /// <summary>
//        /// 创建或获取现有信号，等待手动触发
//        /// </summary>
//        public async Task<CancelType> GetOrCreateChannelAsync(string signal)
//        {
//            var (channel, isCancelled, isDiscardMode) = GetOrCreateChannel(signal);

//            // 如果信号已取消或在丢弃模式下，立即返回丢弃类型
//            if (isCancelled || isDiscardMode) return CancelType.Discard;

//            return await channel.Reader.ReadAsync();
//        }

//        /// <summary>
//        /// 触发信号并将其移除
//        /// </summary>
//        public bool TriggerSignal(string signal)
//        {
//            lock (_lock)
//            {
//                if (_channels.TryGetValue(signal, out var channelInfo))
//                {
//                    var (channel, isCancelled, isDiscardMode) = channelInfo;

//                    // 如果信号未被取消，则触发并标记为已取消
//                    if (!isCancelled)
//                    {
//                        channel.Writer.TryWrite(CancelType.Manual);
//                        _channels[signal] = (channel, true, false); // 标记为已取消
//                        _channels.TryRemove(signal, out _); // 从字典中移除信号
//                        return true;
//                    }
//                }
//            }
//            return false;
//        }

//        /// <summary>
//        /// 启用丢弃模式，所有后续获取的信号将直接返回丢弃类型
//        /// </summary>
//        /// <param name="signal">信号标识符</param>
//        public void EnableDiscardMode(string signal,bool state = true)
//        {
//            lock (_lock)
//            {
//                if (_channels.TryGetValue(signal, out var channelInfo))
//                {
//                    var (channel, isCancelled, _) = channelInfo;
//                    _channels[signal] = (channel, isCancelled, state); // 标记为丢弃模式
//                }
//            }
//        }

//        /// <summary>
//        /// 取消所有任务
//        /// </summary>
//        public void CancelAllTasks()
//        {
//            foreach (var (channel, _, _) in _channels.Values)
//            {
//                try
//                {
//                    channel.Writer.Complete();
//                }
//                catch
//                {
//                    // 忽略完成时的异常
//                }
//            }
//            _channels.Clear();
//        }

//        /// <summary>
//        /// 获取或创建指定信号的 Channel 通道
//        /// </summary>
//        private (Channel<CancelType>, bool, bool) GetOrCreateChannel(string signal)
//        {
//            lock (_lock)
//            {
//                return _channels.GetOrAdd(signal, _ => (Channel.CreateUnbounded<CancelType>(), false, false));
//            }
//        }
//    }
//}


#endregion
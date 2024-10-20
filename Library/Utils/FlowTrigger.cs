using Serein.Library.Utils;
using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Serein.Library
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

    public class TriggerResult<T> 
    {
        public TriggerType Type { get; set; }
        public T Value { get; set; }
    }

    /// <summary>
    /// 信号触发器类，带有消息广播功能。
    /// 使用枚举作为标记，创建
    /// </summary>
    public class FlowTrigger<TSignal> where TSignal : struct, Enum
    {
        // 使用并发字典管理每个信号对应的广播列表
        private readonly ConcurrentDictionary<TSignal, Subject<(TriggerType,object)>> _subscribers = new ConcurrentDictionary<TSignal, Subject<(TriggerType, object)>>();

        /// <summary>
        /// 获取或创建指定信号的 Subject（消息广播者）
        /// </summary>
        /// <param name="signal">枚举信号标识符</param>
        /// <returns>对应的 Subject</returns>
        private Subject<(TriggerType, object)> GetOrCreateSubject(TSignal signal)
        {
            return _subscribers.GetOrAdd(signal, _ => new Subject<(TriggerType, object)>());
        }

        /// <summary>
        /// 订阅指定信号的消息
        /// </summary>
        /// <param name="signal">枚举信号标识符</param>
        /// <param name="observer">订阅者</param>
        /// <returns>取消订阅的句柄</returns>
        public IDisposable Subscribe(TSignal signal, IObserver<(TriggerType, object)> observer)
        {
            var subject = GetOrCreateSubject(signal);
            // (IObserver<(TriggerType, object)>)
            return subject.Subscribe(observer); // 返回取消订阅的句柄
        }

        /// <summary>
        /// 创建信号并指定超时时间，触发时通知所有订阅者
        /// </summary>
        /// <param name="signal">枚举信号标识符</param>
        /// <param name="outTime">超时时间</param>
        /// <returns>等待任务，返回值为：状态（超时触发，手动触发），数据（超时触发时会使用设置好的数据）</returns>
        public async Task<(TriggerType, TResult)> CreateTaskWithTimeoutAsync<TResult>(TSignal signal, TimeSpan outTime, TResult outValue)
        {
            var subject = GetOrCreateSubject(signal);
            var cts = new CancellationTokenSource();

            // 异步任务：超时后自动触发信号
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(outTime, cts.Token);
                    if (!cts.IsCancellationRequested) // 如果还没有被取消
                    {
                        subject.OnNext((TriggerType.Overtime, outValue)); // 广播给所有订阅者
                        subject.OnCompleted(); // 通知订阅结束
                    }
                }
                catch (OperationCanceledException)
                {
                    // 超时任务被取消
                }
                finally
                {
                    cts?.Dispose();  // 确保 cts 被释放
                }
            }, cts.Token);

            var result = await WaitSignalAsync<TResult>(signal);// 返回一个可以超时触发的等待任务
            return result;
        }



        /// <summary>
        /// 创建等待任务，触发时通知所有订阅者
        /// </summary>
        /// <param name="signal">枚举信号标识符</param>
        /// <param name="outTime">超时时间</param>
        /// <returns>等待任务</returns>
        public async Task<TResult> CreateTaskAsync<TResult>(TSignal signal)
        {
            var subject = GetOrCreateSubject(signal);
            (_,var result) = await WaitSignalAsync<TResult>(signal);

            return result;// 返回一个等待的任务
        }


        /// <summary>
        /// 等待指定信号的触发
        /// </summary>
        /// <param name="signal">枚举信号标识符</param>
        /// <returns>等待任务</returns>
        public async Task<(TriggerType, TResult)> WaitSignalAsync<TResult>(TSignal signal)
        {
            var taskCompletionSource = new TaskCompletionSource<(TriggerType, object)>();
            var subscription = Subscribe(signal, new Observer<(TriggerType, object)>(taskCompletionSource.SetResult));
            (var type,var result) = await taskCompletionSource.Task;
            subscription.Dispose(); // 取消订阅
            
            return (type, result.ToConvert<TResult>());
        }

      

        /// <summary>
        /// 手动触发信号，并广播给所有订阅者
        /// </summary>
        /// <param name="signal">枚举信号标识符</param>
        /// <returns>是否成功触发</returns>
        public bool Trigger<TResult>(TSignal signal, TResult value)
        {
            if (_subscribers.TryGetValue(signal, out var subject))
            {
                subject.OnNext((TriggerType.External, value)); // 广播给所有订阅者
                //subject.OnCompleted(); // 通知订阅结束
                return true;
            }
            return false;
        }

        /// <summary>
        /// 取消所有任务
        /// </summary>
        public void CancelAllTasks()
        {
            foreach (var subject in _subscribers.Values)
            {
                subject.OnCompleted(); // 通知所有订阅者结束
            }
            _subscribers.Clear();
        }

    }

    /// <summary>
    /// 观察者类，用于包装 Action
    /// </summary>
    public class Observer<T> : IObserver<T>
    {
        private readonly Action<T> _onNext;

        public Observer(Action<T> onNext)
        {
            _onNext = onNext;
        }

        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(T value)
        {
            _onNext?.Invoke(value);
        }
    }
}

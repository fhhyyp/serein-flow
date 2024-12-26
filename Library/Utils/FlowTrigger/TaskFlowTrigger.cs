using Microsoft.Extensions.ObjectPool;
using Newtonsoft.Json.Linq;
using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Transactions;

namespace Serein.Library.Utils
{

    /// <summary>
    /// 信号触发器类，带有消息广播功能。
    /// 使用枚举作为标记，创建
    /// </summary>
    public class TaskFlowTrigger<TSignal> : IFlowTrigger<TSignal>
    {
        // 使用并发字典管理每个信号对应的广播列表
        private readonly ConcurrentDictionary<TSignal, Subject<TriggerResult<object>>> _subscribers = new ConcurrentDictionary<TSignal, Subject<TriggerResult<object>>>();
        private readonly TriggerResultPool _triggerResultPool = new TriggerResultPool();
        /// <summary>
        /// 获取或创建指定信号的 Subject（消息广播者）
        /// </summary>
        /// <param name="signal">枚举信号标识符</param>
        /// <returns>对应的 Subject</returns>
        private Subject<TriggerResult<object>> GetOrCreateSubject(TSignal signal)
        {
            return _subscribers.GetOrAdd(signal, _ => new Subject<TriggerResult<object>>());
        }

        /// <summary>
        /// 订阅指定信号的消息
        /// </summary>
        /// <param name="signal">枚举信号标识符</param>
        /// <param name="action">订阅者</param>
        /// <returns>取消订阅的句柄</returns>
        private IDisposable Subscribe<TResult>(TSignal signal, Action<TriggerResult<object>> action)
        {
            IObserver<TriggerResult<object>> observer = new Observer<TriggerResult<object>>(action);
            var subject = GetOrCreateSubject(signal);
            return subject.Subscribe(observer); // 返回取消订阅的句柄
        }



        /// <summary>
        /// 等待触发器并指定超时的时间
        /// </summary>
        /// <typeparam name="TResult">返回值类型</typeparam>
        /// <param name="signal">等待信号</param>
        /// <param name="outTime">超时时间</param>
        /// <returns></returns>
        public async Task<TriggerResult<TResult>> WaitTriggerWithTimeoutAsync<TResult>(TSignal signal, TimeSpan outTime)
        {
            var subject = GetOrCreateSubject(signal);
            var cts = new CancellationTokenSource();

            // 超时任务：延迟后触发超时信号
            var timeoutTask = Task.Delay(outTime, cts.Token).ContinueWith(t =>
            {
                if (!cts.Token.IsCancellationRequested)
                {
                    var outResult = _triggerResultPool.Get();
                    outResult.Type = TriggerDescription.Overtime;
                    subject.OnNext(outResult);
                    subject.OnCompleted();
                }
            }, cts.Token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);

            var result = await WaitTriggerAsync<TResult>(signal); // 获取触发的结果
            cts.Cancel();  // 取消超时任务
            await timeoutTask; // 确保超时任务完成
            cts.Dispose();
            return result;

        }

        /// <summary>
        /// 等待触发
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="signal"></param>
        /// <returns></returns>
        public async Task<TriggerResult<TResult>> WaitTriggerAsync<TResult>(TSignal signal)
        {
            var taskCompletionSource = new TaskCompletionSource<TriggerResult<object>>();
            var subscription = Subscribe<TResult>(signal, taskCompletionSource.SetResult);
            var result = await taskCompletionSource.Task;
            subscription.Dispose(); // 取消订阅
            var result2 = result.Value is TResult data
                ? new TriggerResult<TResult> { Value = data, Type = TriggerDescription.External }
                : new TriggerResult<TResult> { Type = TriggerDescription.TypeInconsistency };
            _triggerResultPool.Return(result); // 将结果归还池中
            return result2;
        }


        /// <summary>
        /// 手动触发信号，并广播给所有订阅者
        /// </summary>
        /// <typeparam name="TResult">触发类型</typeparam>
        /// <param name="signal">枚举信号标识符</param>
        /// <param name="value">传递的数据</param>
        /// <returns>是否成功触发</returns>
        public Task<bool> InvokeTriggerAsync<TResult>(TSignal signal, TResult value)
        {
            if (_subscribers.TryGetValue(signal, out var subject))
            {
                var result = _triggerResultPool.Get();
                result.Type = TriggerDescription.External;
                result.Value = value;
                subject.OnNext(result); // 广播给所有订阅者
                subject.OnCompleted(); // 通知订阅结束
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
        /// <summary>
        /// 取消所有任务
        /// </summary>

        public void CancelAllTrigger()
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

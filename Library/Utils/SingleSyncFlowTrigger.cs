using Newtonsoft.Json.Linq;
using Serein.Library.Api;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{
    /// <summary>
    /// 同步的单体消息触发器
    /// </summary>
    /// <typeparam name="TSingle"></typeparam>
    public class SingleSyncFlowTrigger<TSingle> : IFlowTrigger<TSingle>
    {
        private readonly ConcurrentDictionary<TSingle, Queue<TaskCompletionSource<TriggerResult<object>>>> _syncChannel
            = new ConcurrentDictionary<TSingle, Queue<TaskCompletionSource<TriggerResult<object>>>>();

        public void CancelAllTrigger()
        {
            foreach (var triggers in _syncChannel.Values) 
            {
                foreach (var trigger in triggers) 
                {
                    trigger.SetCanceled();
                }
            }

        }

        public Task<bool> InvokeTriggerAsync<TResult>(TSingle signal, TResult value)
        {
            if(_syncChannel.TryGetValue(signal, out var tcss))
            {
                var tcs = tcss.Dequeue();
                var result = new TriggerResult<object>
                {
                    Type = TriggerDescription.External,
                    Value = value,
                };
                tcs.SetResult(result);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public async Task<TriggerResult<TResult>> WaitTriggerAsync<TResult>(TSingle signal)
        {
            if (!_syncChannel.TryGetValue(signal,out var tcss))
            {
                tcss = new Queue<TaskCompletionSource<TriggerResult<object>>>();
                _syncChannel.TryAdd(signal, tcss);
            }
            var taskCompletionSource = new TaskCompletionSource<TriggerResult<object>>();
            tcss.Enqueue(taskCompletionSource);
            var result = await taskCompletionSource.Task;
            if (result.Value is TResult result2)
            {
                return new TriggerResult<TResult>
                {
                    Type = TriggerDescription.External,
                    Value = result2,
                };
            }
            else
            {
                return new TriggerResult<TResult>
                {
                    Type = TriggerDescription.TypeInconsistency,
                };
            }
        }

        public async Task<TriggerResult<TResult>> WaitTriggerWithTimeoutAsync<TResult>(TSingle signal, TimeSpan outTime)
        {
            if (!_syncChannel.TryGetValue(signal, out var tcss))
            {
                tcss = new Queue<TaskCompletionSource<TriggerResult<object>>>();
                _syncChannel.TryAdd(signal, tcss);
            }


            var taskCompletionSource = new TaskCompletionSource<TriggerResult<object>>();
            tcss.Enqueue(taskCompletionSource);

            var cts = new CancellationTokenSource();

            // 异步任务：超时后自动触发信号
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(outTime, cts.Token);
                    if (!cts.IsCancellationRequested) // 如果还没有被取消
                    {
                        var outResult = new TriggerResult<object>()
                        {
                            Type = TriggerDescription.Overtime
                        };
                        taskCompletionSource.SetResult(outResult); // 超时触发
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
            var result = await taskCompletionSource.Task;
            cts?.Cancel();
            if (result.Value is TResult result2)
            {
                return new TriggerResult<TResult>
                {
                    Type = result.Type,
                    Value = result2,
                };
            }
            else
            {
                return new TriggerResult<TResult>
                {
                    Type = result.Type,
                };
            }
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{
    /// <summary>
    /// 流程运行中断工具
    /// </summary>
    public class FlowInterruptTool
    {
        // 使用并发字典管理每个信号对应的广播列表
        private readonly ConcurrentDictionary<string, Subject<bool>> _subscribers = new ConcurrentDictionary<string, Subject<bool>>();

        /// <summary>
        /// 获取或创建指定信号的 Subject（消息广播者）
        /// </summary>
        /// <param name="signal">枚举信号标识符</param>
        /// <returns>对应的 Subject</returns>
        private Subject<bool> GetOrCreateSubject(string signal)
        {
            return _subscribers.GetOrAdd(signal, _ => new Subject<bool>());
        }

        /// <summary>
        /// 订阅指定信号的消息
        /// </summary>
        /// <param name="signal">枚举信号标识符</param>
        /// <param name="action">订阅者</param>
        /// <returns>取消订阅的句柄</returns>
        private IDisposable Subscribe(string signal, Action<bool> action)
        {
            IObserver<bool> observer = new Observer<bool>(action);
            var subject = GetOrCreateSubject(signal);
            return subject.Subscribe(observer); // 返回取消订阅的句柄
        }

        /// <summary>
        /// 等待触发
        /// </summary>
        /// <param name="signal"></param>
        /// <returns></returns>
        public async Task<bool> WaitTriggerAsync(string signal)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            var subscription = Subscribe(signal, taskCompletionSource.SetResult);
            var result = await taskCompletionSource.Task;
            subscription.Dispose(); // 取消订阅
            return result;
        }


        /// <summary>
        /// 手动触发信号，并广播给所有订阅者
        /// </summary>
        /// <param name="signal">枚举信号标识符</param>
        /// <returns>是否成功触发</returns>
        public bool InvokeTrigger(string signal)
        {
            if (_subscribers.TryGetValue(signal, out var subject))
            {
                subject.OnNext(true); // 广播给所有订阅者
                subject.OnCompleted(); // 通知订阅结束
                return true;
            }
            return false;
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
}

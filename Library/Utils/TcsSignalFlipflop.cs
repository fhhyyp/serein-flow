using Serein.Library.Ex;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Serein.Library.Core.NodeFlow.Tool
{
    //public class TcsSignalException : Exception
    //{
    //    public FlowStateType FsState { get; set; }
    //    public TcsSignalException(string? message) : base(message)
    //    {
    //        FsState = FlowStateType.Error;
    //    }
    //}

    public class TcsSignalFlipflop<TSignal> where TSignal : struct, Enum
    {
        public ConcurrentDictionary<TSignal, TaskCompletionSource<object>> TcsEvent { get; } = new ConcurrentDictionary<TSignal, TaskCompletionSource<object>>();

        public ConcurrentDictionary<TSignal, object> TcsLock { get; } = new ConcurrentDictionary<TSignal, object>();

        /// <summary>
        /// 触发信号
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="signal">信号</param>
        /// <param name="value">传递的参数</param>
        /// <returns>是否成功触发</returns>
        public bool TriggerSignal<T>(TSignal signal, T value)
        {
            var tcsLock = TcsLock.GetOrAdd(signal, new object());
            lock (tcsLock)
            {
                if (TcsEvent.TryRemove(signal, out var waitTcs))
                {
                    waitTcs.SetResult(value);
                    return true;
                }
                return false;
            }
        }

        public TaskCompletionSource<object> CreateTcs(TSignal signal)
        {
            var tcsLock = TcsLock.GetOrAdd(signal, new object());
            lock (tcsLock)
            {
                var tcs = TcsEvent.GetOrAdd(signal, new TaskCompletionSource<object>());
                return tcs;
            }
            
        }

        public void CancelTask()
        {
            foreach (var tcs in TcsEvent.Values)
            {
                tcs.SetException(new FlipflopException("任务取消"));
            }
            TcsEvent.Clear();
        }
    }
}

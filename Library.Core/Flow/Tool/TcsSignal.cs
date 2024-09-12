using System.Collections.Concurrent;

namespace Serein.Flow.Tool
{
    public class TcsSignalException : Exception
    {
        public FfState FfState { get; set; }
        public TcsSignalException(string? message) : base(message)
        {
            FfState = FfState.Cancel;
        }
    }

    public class TcsSignal<TSignal> where TSignal : struct, Enum
    {

        public ConcurrentDictionary<TSignal, Stack<TaskCompletionSource<object>>> TcsEvent { get; } = new();

        // public object tcsObj = new object();

        public bool TriggerSignal<T>(TSignal signal, T state)
        {
            if (TcsEvent.TryRemove(signal, out var waitTcss))
            {
                while (waitTcss.Count > 0)
                {

                    waitTcss.Pop().SetResult(state);

                }
                return true;
            }
            return false;


        }
        
        public TaskCompletionSource<object> CreateTcs(TSignal signal)
        {
            
            var tcs = new TaskCompletionSource<object>();
            TcsEvent.GetOrAdd(signal, _ => new Stack<TaskCompletionSource<object>>()).Push(tcs);
            return tcs;



        }
        //public TaskCompletionSource<object> GetOrCreateTcs(TSignal signal)
        //{
        //    lock (tcsObj)
        //    {
        //        var tcs = TcsEvent.GetOrAdd(signal, _ => new TaskCompletionSource<object>());
        //        if (tcs.Task.IsCompleted)
        //        {
        //            TcsEvent.TryRemove(signal, out _);
        //            tcs = new TaskCompletionSource<object>();
        //            TcsEvent[signal] = tcs;
        //        }
        //        return tcs;
        //    }
        //}

        public void CancelTask()
        {
            lock(TcsEvent)
            {

                foreach (var tcss in TcsEvent.Values)
                {
                    while (tcss.Count > 0)
                    {
                        tcss.Pop().SetException(new TcsSignalException("Task Cancel"));
                    }
                }
                TcsEvent.Clear();
            }
        }

    }
}

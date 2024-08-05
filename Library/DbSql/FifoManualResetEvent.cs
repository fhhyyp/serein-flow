namespace Serein.DbSql
{
    /// <summary>
    /// 线程阻塞
    /// </summary>
    public class FifoManualResetEvent
    {
        private readonly object lockObj = new object();
        /// <summary>
        /// 让线程按进入时间顺序调用
        /// </summary>
        private readonly Queue<Thread> waitQueue = new Queue<Thread>();
        private bool isSet;

        public bool IsSet { get => isSet; set => isSet = value; }

        public FifoManualResetEvent(bool initialState = false)
        {
            IsSet = initialState;
        }

        /// <summary>
        /// 等待解锁
        /// </summary>
        public void Wait()
        {
            lock (lockObj)
            {
                if (IsSet)
                {
                    // 获取到了发送的信号，线程开始重新执行
                    return;
                }

                var currentThread = Thread.CurrentThread;
                waitQueue.Enqueue(currentThread);

                while (!IsSet || waitQueue.Peek() != currentThread)
                {
                    Monitor.Wait(lockObj);
                }

                waitQueue.Dequeue();
            }
        }

        /// <summary>
        /// 发送信号
        /// </summary>
        public void Set()
        {
            lock (lockObj)
            {
                IsSet = true;
                Monitor.PulseAll(lockObj);
            }
        }

        /// <summary>
        /// 锁定当前线程
        /// </summary>
        public void Reset()
        {
            lock (lockObj)
            {
                IsSet = false;
            }
        }
    }

}

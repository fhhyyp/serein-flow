using Serein;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Flow.Tool
{

    #region 锁、tsk工具 (已注释）
    /*public class LockManager
    {
        private readonly ConcurrentDictionary<string, LockQueue> _locks = new ConcurrentDictionary<string, LockQueue>();

        public void CreateLock(string name)
        {
            _locks.TryAdd(name, new LockQueue());
        }

        public async Task AcquireLockAsync(string name, CancellationToken cancellationToken = default)
        {
            if (!_locks.ContainsKey(name))
            {
                throw new ArgumentException($"Lock with name '{name}' does not exist.");
            }

            var lockQueue = _locks[name];
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (lockQueue.Queue)
            {
                lockQueue.Queue.Enqueue(tcs);
                if (lockQueue.Queue.Count == 1)
                {
                    tcs.SetResult(true);
                }
            }

            await tcs.Task.ConfigureAwait(false);

            // 处理取消操作
            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    lock (lockQueue.Queue)
                    {
                        if (lockQueue.Queue.Contains(tcs))
                        {
                            tcs.TrySetCanceled();
                        }
                    }
                });
            }
        }

        public void ReleaseLock(string name)
        {
            if (!_locks.ContainsKey(name))
            {
                throw new ArgumentException($"Lock with name '{name}' does not exist.");
            }

            var lockQueue = _locks[name];

            lock (lockQueue.Queue)
            {
                if (lockQueue.Queue.Count > 0)
                {
                    lockQueue.Queue.Dequeue();

                    if (lockQueue.Queue.Count > 0)
                    {
                        var next = lockQueue.Queue.Peek();
                        next.SetResult(true);
                    }
                }
            }
        }

        private class LockQueue
        {
            public Queue<TaskCompletionSource<bool>> Queue { get; } = new Queue<TaskCompletionSource<bool>>();
        }
    }


    public interface ITaskResult
    {
        object Result { get; }
    }

    public class TaskResult<T> : ITaskResult
    {
        public TaskResult(T result)
        {
            Result = result;
        }

        public T Result { get; }

        object ITaskResult.Result => Result;
    }

    public class DynamicTasks
    {
        private static readonly ConcurrentDictionary<string, Task<ITaskResult>> TaskGuidPairs = new();
        public static Task<ITaskResult> GetTask(string Guid)
        {
            TaskGuidPairs.TryGetValue(Guid, out Task<ITaskResult> task);
            return task;
        }

        public static bool AddTask<T>(string Guid, T result)
        {
            var task = Task.FromResult<ITaskResult>(new TaskResult<T>(result));

            return TaskGuidPairs.TryAdd(Guid, task);
        }
    }
    public class TaskNodeManager
    {
        private readonly ConcurrentDictionary<string, TaskQueue> _taskQueues = new ConcurrentDictionary<string, TaskQueue>();

        public void CreateTaskNode(string name)
        {
            _taskQueues.TryAdd(name, new TaskQueue());
        }

        public async Task WaitForTaskNodeAsync(string name, CancellationToken cancellationToken = default)
        {
            if (!_taskQueues.ContainsKey(name))
            {
                throw new ArgumentException($"Task node with name '{name}' does not exist.");
            }

            var taskQueue = _taskQueues[name];
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (taskQueue.Queue)
            {
                taskQueue.Queue.Enqueue(tcs);
                if (taskQueue.Queue.Count == 1)
                {
                    tcs.SetResult(true);
                }
            }

            await tcs.Task.ConfigureAwait(false);

            // 处理取消操作
            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    lock (taskQueue.Queue)
                    {
                        if (taskQueue.Queue.Contains(tcs))
                        {
                            tcs.TrySetCanceled();
                        }
                    }
                });
            }
        }

        public void CompleteTaskNode(string name)
        {
            if (!_taskQueues.ContainsKey(name))
            {
                throw new ArgumentException($"Task node with name '{name}' does not exist.");
            }

            var taskQueue = _taskQueues[name];

            lock (taskQueue.Queue)
            {
                if (taskQueue.Queue.Count > 0)
                {
                    taskQueue.Queue.Dequeue();

                    if (taskQueue.Queue.Count > 0)
                    {
                        var next = taskQueue.Queue.Peek();
                        next.SetResult(true);
                    }
                }
            }
        }

        private class TaskQueue
        {
            public Queue<TaskCompletionSource<bool>> Queue { get; } = new Queue<TaskCompletionSource<bool>>();
        }
    }*/
    #endregion



}

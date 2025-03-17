using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{
    public class TriggerResult<TResult>
    {
        public TriggerDescription Type { get; set; }
        public TResult Value { get; set; }
    }

    /// <summary>
    /// 对象池队列
    /// </summary>
    public class ConcurrentExpandingObjectPool<T> where T : class, new()
    {
        private readonly ConcurrentQueue<T> _pool; // 存储池中对象的队列

        public ConcurrentExpandingObjectPool(int initialCapacity)
        {
            // 初始化对象池，初始容量为 initialCapacity
            _pool = new ConcurrentQueue<T>();

            // 填充初始对象
            for (int i = 0; i < initialCapacity; i++)
            {
                _pool.Enqueue(new T());
            }
        }

        /// <summary>
        /// 获取一个对象，如果池中没有对象，则动态创建新的对象
        /// </summary>
        /// <returns>池中的一个对象</returns>
        public T Get()
        {
            // 尝试从池中获取一个对象
            if (!_pool.TryDequeue(out var item))
            {
                // 如果池为空，则创建一个新的对象
                item = new T();
            }

            return item;
        }

        /// <summary>
        /// 将一个对象归还到池中
        /// </summary>
        /// <param name="item">需要归还的对象</param>
        public void Return(T item)
        {
            // 将对象归还到池中
            _pool.Enqueue(item);
        }

        /// <summary>
        /// 获取当前池中的对象数
        /// </summary>
        public int CurrentSize => _pool.Count;

        /// <summary>
        /// 清空池中的所有对象
        /// </summary>
        public void Clear()
        {
            while (_pool.TryDequeue(out _)) { } // 清空队列
        }
    }

    /// <summary>
    /// 使用 ObjectPool 来复用 TriggerResult 对象
    /// </summary>
    public class TriggerResultPool<TResult>
    {
        private readonly ConcurrentExpandingObjectPool<TriggerResult<TResult>> _objectPool;

        public TriggerResultPool(int defaultCapacity = 30)
        {
            _objectPool = new ConcurrentExpandingObjectPool<TriggerResult<TResult>>(defaultCapacity);
        }

        public TriggerResult<TResult> Get() => _objectPool.Get();

        public void Return(TriggerResult<TResult> result) => _objectPool.Return(result);
    }


    /// <summary>
    /// 触发类型
    /// </summary>
    public enum TriggerDescription
    {
        /// <summary>
        /// 外部触发
        /// </summary>
        External,
        /// <summary>
        /// 超时触发
        /// </summary>
        Overtime,
        /// <summary>
        /// 触发了，但类型不一致
        /// </summary>
        TypeInconsistency
    }
}

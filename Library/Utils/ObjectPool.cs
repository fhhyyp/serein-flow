using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{
    /// <summary>
    ///
    /// <para>具有预定义池大小限制的对象池模式的通用实现。其主要目的是将有限数量的经常使用的对象保留在池中，以便进一步回收。  </para>
    /// <para>                                                                                                                </para>
    /// <para>注:                                                                                                             </para>
    /// <para>1)目标不是保留所有返回的对象。池不是用来存储的。如果池中没有空间，则会丢弃额外返回的对象。                      </para>
    /// <para>                                                                                                                </para>
    /// <para>2)这意味着如果对象是从池中获得的，调用者将在相对较短的时间内返回它                                              </para>
    /// <para>时间。长时间保持检出对象是可以的，但是会降低池的有用性。你只需要重新开始。                                      </para>
    /// <para>                                                                                                                </para>
    /// <para>不将对象返回给池并不会损害池的工作，但这是一种不好的做法。                                                      </para>
    /// <para>基本原理：如果没有重用对象的意图，就不要使用pool——只使用“new”                                               </para>
    /// </summary>
    public class ObjectPool<T> where T : class
    {
        [DebuggerDisplay("{Value,nq}")]
        private struct Element
        {
            internal T Value;
        }

    
        /// <summary>
        ///  池对象的存储。第一个项存储在专用字段中，因为我们希望能够满足来自它的大多数请求。
        /// </summary>
        private T _firstItem;

        private readonly Element[] _items;

        /// <summary>
        /// 工厂在池的生命周期内被存储。只有当池需要扩展时，我们才调用它。
        /// 与“new T（）”相比，Func为实现者提供了更多的灵活性，并且比“new T（）”更快。
        /// </summary>
        private readonly Func<T> _factory;

        /// <summary>
        /// 指定了 T 对象释放时的行为。
        /// </summary>
        private readonly Action<T>? _free;

        /// <summary>
        /// 创建一个新的对象池实例，使用指定的工厂函数和默认大小（处理器核心数的两倍）。
        /// </summary>
        /// <param name="factory"></param>
        public ObjectPool(Func<T> factory, Action<T>? free = null) : this(factory, Environment.ProcessorCount * 2, free)
        {
        }

        /// <summary>
        /// 创建一个新的对象池实例，使用指定的工厂函数和指定的大小。
        /// </summary>
        /// <param name="factory"></param>
        /// <param name="size"></param>
        public ObjectPool(Func<T> factory, int size, Action<T>? free = null)
        {
            Debug.Assert(size >= 1);
            _factory = factory;
            _free = free;
            _items = new Element[size - 1];
        }

        /// <summary>
        /// 创建一个新的实例。
        /// </summary>
        /// <returns></returns>
        private T CreateInstance()
        {
            T inst = _factory();
            return inst;
        }

        /// <summary>
        /// 生成实例。
        /// </summary>
        /// <remarks>
        /// 搜索策略是一种简单的线性探测，选择它是为了缓存友好。
        /// 请注意，Free会尝试将回收的对象存储在靠近起点的地方，从而在统计上减少我们通常搜索的距离。
        /// </remarks>
        public T Allocate()
        {
            /*
             * PERF：检查第一个元素。如果失败，AllocateSlow将查看剩余的元素。
             * 注意，初始读是乐观地不同步的。
             * 这是有意为之。只有了待使用对象，我们才会返回。
             * 在最坏的情况下，我们可能会错过一些最近返回的对象。没什么大不了的。
             */
            T inst = _firstItem;
            if (inst == null || inst != Interlocked.CompareExchange(ref _firstItem, null, inst))
            {
                inst = AllocateSlow();
            }

            return inst;
        }

        /// <summary>
        /// 慢速分配方法，当第一个元素不可用时调用。
        /// </summary>
        /// <returns></returns>
        private T AllocateSlow()
        {
            Element[] items = _items;

            for (int i = 0; i < items.Length; i++)
            {
                // 注意，初始读是乐观地不同步的。这是有意为之。只有有了候选人，我们才会联系。在最坏的情况下，我们可能会错过一些最近返回的对象。没什么大不了的。
                T inst = items[i].Value;
                if (inst != null)
                {
                    if (inst == Interlocked.CompareExchange(ref items[i].Value, null, inst))
                    {
                        return inst;
                    }
                }
            }

            return CreateInstance();
        }

        /// <summary>
        ///返回对象到池。
        /// </summary>
        /// <remarks>
        /// 搜索策略是一种简单的线性探测，选择它是因为它具有缓存友好性。
        /// 请注意Free会尝试将回收的对象存储在靠近起点的地方，从而在统计上减少我们通常在Allocate中搜索的距离。
        /// </remarks>
        public void Free(T obj)
        {
            Validate(obj);
            _free?.Invoke(obj);
            if (_firstItem == null)
            {
                // 这里故意不使用联锁。在最坏的情况下，两个对象可能存储在同一个槽中。这是不太可能发生的，只意味着其中一个对象将被收集。
                _firstItem = obj;
            }
            else
            {
                FreeSlow(obj);
            }
        }

        private void FreeSlow(T obj)
        {
            Element[] items = _items;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].Value == null)
                {
                    // 这里故意不使用联锁。在最坏的情况下，两个对象可能存储在同一个槽中。这是不太可能发生的，只意味着其中一个对象将被收集。
                    items[i].Value = obj;
                    break;
                }
            }
        }

        [Conditional("DEBUG")]
        private void Validate(object obj)
        {
            Debug.Assert(obj != null, "freeing null?");

            Debug.Assert(_firstItem != obj, "freeing twice?");

            var items = _items;
            for (int i = 0; i < items.Length; i++)
            {
                var value = items[i].Value;
                if (value == null)
                {
                    return;
                }

                Debug.Assert(value != obj, "freeing twice?");
            }
        }
    }
}

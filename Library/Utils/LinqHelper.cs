using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{
    /// <summary>
    /// 对于 linq 的异步扩展方法
    /// </summary>
    public static class LinqHelper
    {
        /// <summary>
        /// 根据条件筛选，只保留第一个满足条件的元素，其余的不包含。
        /// </summary>
        public static IEnumerable<T> DistinctByCondition<T>(
            this IEnumerable<T> source,
            Func<T, bool> predicate)
        {
            var seenKeys = new HashSet<T>();

            foreach (var item in source)
            {
                if (!predicate(item))
                    continue;

                /*var key = keySelector(item);
                if (seenKeys.Add(key)) // 如果是新键*/
                yield return item;
            }
        }

        /// <summary>
        /// 异步选择器，返回一个新的集合，其中每个元素都是通过异步方法转换的结果。
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="source"></param>
        /// <param name="method"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<TResult>> SelectAsync<TSource, TResult>(this IEnumerable<TSource> source,
                                                                                          Func<TSource, Task<TResult>> method)
        {
            return await Task.WhenAll(source.Select(async s => await method(s)));
        }


        /// <summary>
        /// 异步选择器，返回一个新的集合，其中每个元素都是通过异步方法转换的结果。
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="source"></param>
        /// <param name="method"></param>
        /// <param name="concurrency"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<TResult>> SelectAsync<TSource, TResult>(this IEnumerable<TSource> source, 
                                                                                          Func<TSource, Task<TResult>> method,
                                                                                          int concurrency = int.MaxValue)
        {
            var semaphore = new SemaphoreSlim(concurrency);
            try
            {
                return await Task.WhenAll(source.Select(async s =>
                {
                    try
                    {
                        await semaphore.WaitAsync();
                        return await method(s);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }
            finally
            {
                semaphore.Dispose();
            }
        }
    }
}

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
    public static class LinqAsyncHelper
    {
        public static async Task<IEnumerable<TResult>> SelectAsync<TSource, TResult>(this IEnumerable<TSource> source,
                                                                                          Func<TSource, Task<TResult>> method)
        {
            return await Task.WhenAll(source.Select(async s => await method(s)));
        }

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

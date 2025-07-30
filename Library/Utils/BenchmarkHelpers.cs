using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{
    /// <summary>
    /// 代码计时工具类
    /// </summary>
    public static class BenchmarkHelpers
    {
        /// <summary>
        /// 运行指定异步方法多次并输出耗时的最大、最小和平均值。
        /// </summary>
        /// <param name="action">需要执行的方法</param>
        /// <param name="count">执行次数，默认10000</param>
        public static void Benchmark(Action action, int count = 10000)
        {
            double max = double.MinValue;
            double min = double.MaxValue;
            double total = 0;

            for (int i = 0; i < count; i++)
            {
                var sw = Stopwatch.StartNew();
                action();
                sw.Stop();

                double ms = sw.Elapsed.TotalMilliseconds;
                if (ms > max) max = ms;
                if (ms < min) min = ms;
                total += ms;
            }

            double avg = total / count;

           SereinEnv.WriteLine(InfoType.INFO, $"运行 {count} 次：");
           SereinEnv.WriteLine(InfoType.INFO, $"总耗时  :{total} 毫秒：");
           SereinEnv.WriteLine(InfoType.INFO, $"最大耗时：{max} 毫秒");
           SereinEnv.WriteLine(InfoType.INFO, $"最小耗时：{min} 毫秒");
           SereinEnv.WriteLine(InfoType.INFO, $"平均耗时：{avg} 毫秒");
        }

        /// <summary>
        /// 运行指定异步方法多次并输出耗时的最大、最小和平均值。
        /// </summary>
        /// <param name="func">需要执行的异步方法</param>
        /// <param name="count">执行次数，默认10000</param>
        public static async Task BenchmarkAsync(Func<Task> func, int count = 10000)
        {
            double max = double.MinValue;
            double min = double.MaxValue;
            double total = 0;

            for (int i = 0; i < count; i++)
            {
                var sw = Stopwatch.StartNew();
                await func();
                sw.Stop();

                double ms = sw.Elapsed.TotalMilliseconds;
                if (ms > max) max = ms;
                if (ms < min) min = ms;
                total += ms;
            }

            double avg = total / count;
            SereinEnv.WriteLine(InfoType.INFO, $"运行 {count} 次：");
            SereinEnv.WriteLine(InfoType.INFO, $"总耗时  :{total} 毫秒：");
            SereinEnv.WriteLine(InfoType.INFO, $"最大耗时：{max} 毫秒");
            SereinEnv.WriteLine(InfoType.INFO, $"最小耗时：{min} 毫秒");
            SereinEnv.WriteLine(InfoType.INFO, $"平均耗时：{avg} 毫秒");
        }
        /// <summary>
        /// 运行指定异步方法多次并输出耗时的最大、最小和平均值。
        /// </summary>
        /// <param name="task">需要执行的异步方法</param>
        /// <param name="count">执行次数，默认10000</param>
        public static async Task BenchmarkAsync(Task task, int count = 10000)
        {
            double max = double.MinValue;
            double min = double.MaxValue;
            double total = 0;

            for (int i = 0; i < count; i++)
            {
                var sw = Stopwatch.StartNew();
                await task;
                sw.Stop();

                double ms = sw.Elapsed.TotalMilliseconds;
                if (ms > max) max = ms;
                if (ms < min) min = ms;
                total += ms;
            }

            double avg = total / count;
            SereinEnv.WriteLine(InfoType.INFO, $"运行 {count} 次：");
            SereinEnv.WriteLine(InfoType.INFO, $"总耗时  :{total} 毫秒：");
            SereinEnv.WriteLine(InfoType.INFO, $"最大耗时：{max} 毫秒");
            SereinEnv.WriteLine(InfoType.INFO, $"最小耗时：{min} 毫秒");
            SereinEnv.WriteLine(InfoType.INFO, $"平均耗时：{avg} 毫秒");
        }

        /// <summary>
        /// 运行指定异步方法多次并输出耗时的最大、最小和平均值。
        /// </summary>
        /// <param name="func">需要执行的异步方法</param>
        /// <param name="count">执行次数，默认10000</param>
        public static async Task<TReult> BenchmarkAsync<TReult>(Func<Task<TReult>> func, int count = 10000)
        {
            double max = double.MinValue;
            double min = double.MaxValue;
            double total = 0;
            TReult result = default;
            for (int i = 0; i < count; i++)
            {
                var sw = Stopwatch.StartNew();
                result = await func();
                sw.Stop();

                double ms = sw.Elapsed.TotalMilliseconds;
                if (ms > max) max = ms;
                if (ms < min) min = ms;
                total += ms;
                //Console.WriteLine($"第{count}次： 耗时 {ms} ms");
            }

            double avg = total / count;
            SereinEnv.WriteLine(InfoType.INFO, $"运行 {count} 次：");
            SereinEnv.WriteLine(InfoType.INFO, $"总耗时  :{total} 毫秒：");
            SereinEnv.WriteLine(InfoType.INFO, $"最大耗时：{max} 毫秒");
            SereinEnv.WriteLine(InfoType.INFO, $"最小耗时：{min} 毫秒");
            SereinEnv.WriteLine(InfoType.INFO, $"平均耗时：{avg} 毫秒");
            return result;
        }
        /// <summary>
        /// 运行指定异步方法多次并输出耗时的最大、最小和平均值。
        /// </summary>
        /// <param name="task">需要执行的异步方法</param>
        /// <param name="count">执行次数，默认10000</param>
        public static async Task<TReult> BenchmarkAsync<TReult>(Task<TReult> task, int count = 10000)
        {
            double max = double.MinValue;
            double min = double.MaxValue;
            double total = 0;
            TReult result = default;
            for (int i = 0; i < count; i++)
            {
                var sw = Stopwatch.StartNew();
                result = await task;
                sw.Stop();

                double ms = sw.Elapsed.TotalMilliseconds;
                if (ms > max) max = ms;
                if (ms < min) min = ms;
                total += ms;
                //Console.WriteLine($"第{count}次： 耗时 {ms} ms");
            }

            double avg = total / count;
            SereinEnv.WriteLine(InfoType.INFO, $"运行 {count} 次：");
            SereinEnv.WriteLine(InfoType.INFO, $"总耗时  :{total} 毫秒：");
            SereinEnv.WriteLine(InfoType.INFO, $"最大耗时：{max} 毫秒");
            SereinEnv.WriteLine(InfoType.INFO, $"最小耗时：{min} 毫秒");
            SereinEnv.WriteLine(InfoType.INFO, $"平均耗时：{avg} 毫秒");
            return result;
        } 
        
        /// <summary>
        /// 运行指定异步方法多次并输出耗时的最大、最小和平均值。
        /// </summary>
        /// <param name="func">需要执行的异步方法</param>
        public static async Task<TReult> BenchmarkAsync<TReult>(Func<Task<TReult>> func)
        {
            double max = double.MinValue;
            double min = double.MaxValue;
            double total = 0;
            TReult result = default;
            var sw = Stopwatch.StartNew();
            result = await func();
            sw.Stop();

            double ms = sw.Elapsed.TotalMilliseconds;
            if (ms > max) max = ms;
            if (ms < min) min = ms;
            total += ms;

            var tips = $"运行耗时 :{total} 毫秒：";
            SereinEnv.WriteLine(InfoType.INFO, tips);
            return result;
        }
    }
}

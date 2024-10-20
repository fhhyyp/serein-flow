using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Channels;

namespace Serein.NodeFlow.Tool
{
    /// <summary>
    /// 捕获Console输出
    /// </summary>
    public class LogTextWriter : TextWriter
    {
        private readonly Action<string> logAction; // 更新日志UI的委托
        private readonly StringWriter stringWriter = new(); // 缓存日志内容
        private readonly Channel<string> logChannel = Channel.CreateUnbounded<string>(); // 日志管道
        //private int writeCount = 0; // 写入计数器
        //private const int maxWrites = 500; // 写入最大计数

        /// <summary>
        /// 定义输出委托与清除输出内容委托
        /// </summary>
        /// <param name="logAction"></param>
        public LogTextWriter(Action<string> logAction)
        {
            this.logAction = logAction;

            // 异步启动日志处理任务，不阻塞主线程
            Task.Run(ProcessLogQueueAsync);
        }

        /// <summary>
        /// 编码类型
        /// </summary>
        public override Encoding Encoding => Encoding.UTF8;

      
        public override void Write(char value)
        {
            stringWriter.Write(value);
            if (value == '\n')
            {
                EnqueueLog();
            }
        }

        public override void Write(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            stringWriter.Write(value);
            if (value.Contains('\n'))
            {
                EnqueueLog();
            }
        }

        public override void WriteLine(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            stringWriter.WriteLine(value);
            EnqueueLog();
        }

        /// <summary>
        /// 将日志加入通道
        /// </summary>
        private void EnqueueLog()
        {
            var log = stringWriter.ToString();
            stringWriter.GetStringBuilder().Clear();
            if (!logChannel.Writer.TryWrite(log))
            {
                // 如果写入失败（不太可能），则直接丢弃日志或处理
            }
        }

        /// <summary>
        /// 异步处理日志队列
        /// </summary>
        /// <returns></returns>
        private async Task ProcessLogQueueAsync()
        {
            await foreach (var log in logChannel.Reader.ReadAllAsync()) // 异步读取日志通道
            {
                logAction?.Invoke(log); // 执行日志写入到UI的委托

                //writeCount++;
                //if (writeCount >= maxWrites)
                //{
                //    writeCount = 0; // 重置计数器
                //}
            }
        }
    }
}

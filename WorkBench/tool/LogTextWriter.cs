using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Channels;

namespace Serein.Workbench.tool
{
    /// <summary>
    /// 可以捕获类库输出的打印输出
    /// </summary>
    public class LogTextWriter : TextWriter
    {
        private readonly Action<string> logAction; // 更新日志UI的委托
        private readonly StringWriter stringWriter = new(); // 缓存日志内容
        private readonly Channel<string> logChannel = Channel.CreateUnbounded<string>(); // 日志管道
        private readonly Action clearTextBoxAction; // 清空日志UI的委托
        private int writeCount = 0; // 写入计数器
        private const int maxWrites = 500; // 写入最大计数

        public LogTextWriter(Action<string> logAction, Action clearTextBoxAction)
        {
            this.logAction = logAction;
            this.clearTextBoxAction = clearTextBoxAction;

            // 异步启动日志处理任务，不阻塞主线程
            Task.Run(ProcessLogQueueAsync);
        }

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

        // 将日志加入通道
        private void EnqueueLog()
        {
            var log = stringWriter.ToString();
            stringWriter.GetStringBuilder().Clear();

            if (!logChannel.Writer.TryWrite(log))
            {
                // 如果写入失败（不太可能），则直接丢弃日志或处理
            }
        }

        // 异步处理日志队列
        private async Task ProcessLogQueueAsync()
        {
            await foreach (var log in logChannel.Reader.ReadAllAsync()) // 异步读取日志通道
            {
                logAction?.Invoke(log); // 执行日志写入到UI的委托

                writeCount++;
                if (writeCount >= maxWrites)
                {
                    clearTextBoxAction?.Invoke(); // 清空文本框
                    writeCount = 0; // 重置计数器
                }
            }
        }
    }
}

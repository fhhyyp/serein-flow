using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace Serein.WorkBench.tool
{
    /// <summary>
    /// 可以捕获类库输出的打印输出
    /// </summary>
    public class LogTextWriter : TextWriter
    {
        private readonly Action<string> logAction;
        private readonly StringWriter stringWriter = new();
        private readonly BlockingCollection<string> logQueue = new();
        private readonly Task logTask;

        // 用于计数的字段
        private int writeCount = 0;
        private const int maxWrites = 500;
        private readonly Action clearTextBoxAction;

        public LogTextWriter(Action<string> logAction, Action clearTextBoxAction)
        {
            this.logAction = logAction;
            this.clearTextBoxAction = clearTextBoxAction;
            logTask = Task.Run(ProcessLogQueue); // 异步处理日志
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

        private void EnqueueLog()
        {
            logQueue.Add(stringWriter.ToString());
            stringWriter.GetStringBuilder().Clear();
        }

        private async Task ProcessLogQueue()
        {
            foreach (var log in logQueue.GetConsumingEnumerable())
            {
                // 异步执行日志输出操作
                await Task.Run(() =>
                {
                    logAction(log);

                    // 计数器增加
                    writeCount++;
                    if (writeCount >= maxWrites)
                    {
                        // 计数器达到50，清空文本框
                        clearTextBoxAction?.Invoke();
                        writeCount = 0; // 重置计数器
                    }
                });
            }
        }

        public new void Dispose()
        {
            logQueue.CompleteAdding();
            logTask.Wait();
            base.Dispose();
        }
    }

}

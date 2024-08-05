using System.IO;
using System.Text;

namespace Serein.WorkBench.tool
{
    /// <summary>
    /// 可以捕获类库输出的打印输出
    /// </summary>
    public class LogTextWriter(Action<string> logAction) : TextWriter
    {
        private readonly Action<string> logAction = logAction;
        private readonly StringWriter stringWriter = new();

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            stringWriter.Write(value);
            if (value == '\n')
            {
                logAction(stringWriter.ToString());
                stringWriter.GetStringBuilder().Clear();
            }
        }

        public override void Write(string? value)
        {
            if(string.IsNullOrWhiteSpace(value)) { return; }
            stringWriter.Write(value);
            if (value.Contains('\n'))
            {
                logAction(stringWriter.ToString());
                stringWriter.GetStringBuilder().Clear();
            }
        }

        public override void WriteLine(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) { return; }
            stringWriter.WriteLine(value);
            logAction(stringWriter.ToString());
            stringWriter.GetStringBuilder().Clear();
        }
    }
}

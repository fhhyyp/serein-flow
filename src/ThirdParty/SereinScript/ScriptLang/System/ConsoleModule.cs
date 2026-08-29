using System.Diagnostics;
using ScriptLang.Runtime;

namespace ScriptLang.System
{
    [PrototypeExtension(NamingFormat = NamingFormat.Js)]
    internal sealed partial class ConsoleModule : ScriptRuntimeObject<ConsoleModule>
    {
        private static readonly Dictionary<string, Stopwatch> _stopwatches = new();
        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is ConsoleModule;

        [PrototypeFunction]
        [LspDoc("输出信息到标准输出流")]
        public static void Log(Value value) => global::System.Console.WriteLine(value?.ToString() ?? "null");

        [PrototypeFunction] 
        [LspDoc("输出错误信息到标准错误流")]
        public static void Error(Value value) => global::System.Console.Error.WriteLine(value?.ToString() ?? "null");

        [PrototypeFunction]
        [LspDoc("从标准输入异步读取一行文本")]
        public static async Task<StringValue> ReadLine()
        { 
            var line = await Task.Run(() => global::System.Console.ReadLine()); 
            return StringValue.Create(line ?? "");
        }

        [PrototypeFunction] 
        [LspDoc("清空控制台所有输出")]
        public static void Clear() => global::System.Console.Clear();

        [PrototypeFunction]
        [LspDoc("启动一个带标签的计时器")]
        public static void Time(StringValue label)
        { 
            _stopwatches[label.Value] = Stopwatch.StartNew();
        }

        [PrototypeFunction] 
        [LspDoc("停止指定标签的计时器并输出耗时（毫秒）")]
        public static void TimeEnd(StringValue label)
        { 
            if (_stopwatches.TryGetValue(label.Value, out var sw))
            { 
                sw.Stop(); 
                _stopwatches.Remove(label.Value); 
                global::System.Console.WriteLine($"{label.Value}: {sw.ElapsedMilliseconds}ms");
            }
        }

        [PrototypeProperty]
        [LspDoc("ANSI 终端颜色常量：reset/red/green/yellow/blue/magenta/cyan/white")]
        private static ObjectValue Colors() => 
            new(new Dictionary<string, Value>
            { 
                ["reset"] = StringValue.Create("\x1b[0m"), 
                ["red"] = StringValue.Create("\x1b[31m"), 
                ["green"] = StringValue.Create("\x1b[32m"), 
                ["yellow"] = StringValue.Create("\x1b[33m"), 
                ["blue"] = StringValue.Create("\x1b[34m"), 
                ["magenta"] = StringValue.Create("\x1b[35m"), 
                ["cyan"] = StringValue.Create("\x1b[36m"), 
                ["white"] = StringValue.Create("\x1b[37m")
            });
    }
}

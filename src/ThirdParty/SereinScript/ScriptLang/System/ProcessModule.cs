using System.Collections;
using System.Diagnostics;
using ScriptLang.Runtime;

namespace ScriptLang.System
{
    [PrototypeExtension(NamingFormat = NamingFormat.Js)]
    internal sealed partial class ProcessModule : ScriptRuntimeObject<ProcessModule>
    {
        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is ProcessModule;

        [PrototypeProperty]
        [LspDoc("命令行参数（args: 参数数组, execPath: 可执行文件路径）")]
        private static ObjectValue Argv()
        { 
            var args = Environment.GetCommandLineArgs(); 
            return new(new() {
                ["args"] = new ArrayValue(args.Skip(1).Select(a => (Value)StringValue.Create(a)).ToList()), 
                ["execPath"] = StringValue.Create(args[0])
            });
        }

        [PrototypeProperty]
        [LspDoc("当前进程 ID")]
        private static NumberValue<int> Pid() => NumberValueFactory.Create(Environment.ProcessId);

        [PrototypeProperty]
        [LspDoc("当前工作目录")]
        private static StringValue Cwd() => StringValue.Create(Directory.GetCurrentDirectory());

        [PrototypeProperty] 
        [LspDoc("进程已运行时间（秒）")]
        private static NumberValue<double> Uptime() => NumberValueFactory.Create(Environment.TickCount64 / 1000.0);

        [PrototypeFunction]
        [LspDoc("更改当前工作目录")]
        public static void ChDir(StringValue path) => Directory.SetCurrentDirectory(path.Value);

        [PrototypeFunction]
        [LspDoc("获取所有环境变量键值对")]
        public static ObjectValue Env()
        { 
            var dict = new Dictionary<string, Value>(); 
            foreach (DictionaryEntry e in Environment.GetEnvironmentVariables()) 
                dict[e.Key.ToString()!] = StringValue.Create(e.Value?.ToString() ?? ""); 
            return new ObjectValue(dict);
        }

        [PrototypeFunction]
        [LspDoc("执行外部命令并等待完成，返回退出代码")]
        public static async Task<NumberValue<int>> Execute(StringValue fileName, ArrayValue? args)
        {
            var info = new ProcessStartInfo
            {
                FileName = fileName.Value,
                UseShellExecute = true,
                Arguments = args is null ? string.Empty : string.Join(" ", args.Elements.Select(x => x.AsString().Replace(" ", string.Empty)))
            };
            var p = Process.Start(info);
            if (p != null) { 
                await p.WaitForExitAsync();
                return NumberValueFactory.Create(p.ExitCode); 
            } 
            return NumberValueFactory.Create(-1); 
        }

        [PrototypeFunction] 
        [LspDoc("以指定退出代码终止当前进程（0=正常）")]
        public static void Exit(NumberValue<int> code) 
            => Environment.Exit(code?.Value ?? 0);
    }
}

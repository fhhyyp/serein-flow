using System.Security.Cryptography;
using System.Text;
using ScriptLang.Runtime;

namespace ScriptLang.System
{
    [PrototypeExtension(NamingFormat = NamingFormat.Js)]
    internal sealed partial class FileModule : ScriptRuntimeObject<FileModule>
    {

        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is FileModule;

        [PrototypeFunction]
        [LspDoc("同步读取文本文件全部内容（默认UTF-8）")]
        public static StringValue Read(StringValue path, StringValue? encoding = null)
        {
            var enc = encoding is null ? Encoding.UTF8 : Encoding.GetEncoding(encoding.Value);
            var content = File.ReadAllText(path.Value, enc); return StringValue.Create(content); 
        }

        [PrototypeFunction]
        [LspDoc("异步读取文本文件全部内容（UTF-8）")]
        public static async Task<StringValue> ReadAsync(StringValue path, StringValue? encoding = null)
        {
            var enc = encoding is null ? Encoding.UTF8 : Encoding.GetEncoding(encoding.Value);
            var content = await File.ReadAllTextAsync(path.Value, enc);
            return StringValue.Create(content);
        }

        [PrototypeFunction]
        [LspDoc("同步写入文本内容到文件（UTF-8）")]
        public static void Write(StringValue path, StringValue content, StringValue? encoding = null)
        {
            var enc = encoding is null ? Encoding.UTF8 : Encoding.GetEncoding(encoding.Value);
            File.WriteAllText(path.Value, content.Value, enc);
        }

        [PrototypeFunction]
        [LspDoc("异步写入文本内容到文件（UTF-8）")]
        public static async Task WriteAsync(StringValue path, StringValue content, StringValue? encoding = null)
        {
            var enc = encoding is null ? Encoding.UTF8 : Encoding.GetEncoding(encoding.Value);
            await File.WriteAllTextAsync(path.Value, content.Value, enc);
        }

        [PrototypeFunction]
        [LspDoc("列出目录下的所有文件和子目录路径")]
        public static ArrayValue ReadDir(StringValue path)
        {
            var files = Directory.GetFiles(path.Value);
            var dirs = Directory.GetDirectories(path.Value);
            var all = files.Concat(dirs)
                            .Select(f => (Value)StringValue.Create(f))
                            .ToArray();
            return new ArrayValue([.. all]);
        }

        [PrototypeFunction]
        [LspDoc("判断指定路径的文件或目录是否存在")]
        public static BoolValue Exists(StringValue path) 
            => BoolValue.Create(File.Exists(path.Value) || Directory.Exists(path.Value));

        [PrototypeFunction]
        [LspDoc("递归创建目录（包括所有中间目录）")]
        public static void MkDir(StringValue path)
        { 
            Directory.CreateDirectory(path.Value); 
        }

        [PrototypeFunction]
        [LspDoc("删除指定文件或目录（目录会递归删除）")]
        public static void Remove(StringValue path)
        { 
            if (File.Exists(path.Value))
            {
                File.Delete(path.Value); 
            }
            else if (Directory.Exists(path.Value))
            {
                Directory.Delete(path.Value, true);
            }
        }

        [PrototypeProperty]
        [LspDoc("当前工作目录的完整路径")]
        private static StringValue Cwd() => StringValue.Create(Directory.GetCurrentDirectory());

        [PrototypeFunction]
        [LspDoc("更改当前工作目录到指定路径")]
        public static void ChDir(StringValue path) => Directory.SetCurrentDirectory(path.Value);

        [PrototypeFunction]
        [LspDoc("获取文件或目录的详细信息（大小、创建/修改时间、类型）")]
        public static ObjectValue Stat(StringValue path)
        { 
            var info = new FileInfo(path.Value); 
            return new ObjectValue(new Dictionary<string, Value> 
            { 
                ["size"] = NumberValueFactory.Create(info.Length), 
                ["created"] = StringValue.Create(info.CreationTime.ToString("o")),
                ["modified"] = StringValue.Create(info.LastWriteTime.ToString("o")), 
                ["isDirectory"] = BoolValue.Create((info.Attributes & FileAttributes.Directory) != 0), 
                ["isFile"] = BoolValue.Create((info.Attributes & FileAttributes.Directory) == 0) }); 
        }
    }
}

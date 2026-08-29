using ScriptLang.Runtime;

namespace ScriptLang.System
{
    [PrototypeExtension(NamingFormat = NamingFormat.Js)]
    internal sealed partial class PathModule : ScriptRuntimeObject<PathModule>
    {
        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is PathModule;

        [PrototypeFunction]
        [LspDoc("将多个路径片段拼接为完整路径")]
        public static StringValue Join(ArrayValue paths)
        { 
            var pathList = paths.Elements.Select(p => p.AsString()).ToArray(); 
            return StringValue.Create(Path.Combine(pathList));
        }

        [PrototypeFunction] 
        [LspDoc("将相对路径解析为绝对路径")]
        public static StringValue Resolve(StringValue path) => StringValue.Create(Path.GetFullPath(path.Value));

        [PrototypeFunction]
        [LspDoc("获取路径中的目录部分")]
        public static StringValue Dirname(StringValue path) => StringValue.Create(Path.GetDirectoryName(path.Value) ?? "");

        [PrototypeFunction]
        [LspDoc("获取路径中的文件名（可选去除扩展名）")]
        public static StringValue Basename(StringValue path, StringValue? ext = null)
        {
            var fn = Path.GetFileName(path.Value); 
            if (ext is not  null && fn.EndsWith(ext.Value))
                fn = fn[..^ext.Value.Length]; 
            return StringValue.Create(fn);
        }

        [PrototypeFunction]
        [LspDoc("获取文件的扩展名（含点号）")]
        public static StringValue Extname(StringValue path) => StringValue.Create(Path.GetExtension(path.Value));

        [PrototypeFunction] 
        [LspDoc("规范化路径（解析 . 和 ..）")]
        public static StringValue Normalize(StringValue path) => StringValue.Create(Path.GetFullPath(path.Value));

        [PrototypeProperty] 
        [LspDoc("当前操作系统的目录分隔符（Windows: \\，Linux: /）")]
        private static StringValue Separator() => StringValue.Create(Path.DirectorySeparatorChar.ToString());

        [PrototypeProperty] 
        [LspDoc("当前操作系统的路径分隔符（Windows: ;，Linux: :）")]
        private static StringValue Delimiter() => StringValue.Create(Path.PathSeparator.ToString());
    }
}

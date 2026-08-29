using ScriptLang.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace ScriptLang;

public sealed class SourceManager
{
    private readonly Dictionary<string, string> _sources = [];

    public void AddSource(string filePath, string source)
    {
        _sources[filePath] = source;
    }

    public bool TryGetSource(string filePath, [NotNullWhen(true)]out string? scr)
    {
        return _sources.TryGetValue(filePath, out scr);
    }

    public string GetSlice(string filePath, int start, int length)
    {
        if (!TryGetSource(filePath, out var src))
        {
            throw new RuntimeException($"源文件未加载: {filePath}");
        }
        return src.Substring(start, length);
    }

    public void ClearCache()
    {
        _sources.Clear();
    }
}

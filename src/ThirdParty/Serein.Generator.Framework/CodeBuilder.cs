using System;
using System.Collections.Generic;
using System.Text;

namespace Serein.Generator.Framework;

/// <summary>Indented text generation with a domain-specific model and invocation context.</summary>
public sealed class CodeBuilder<T>
{
    private readonly StringBuilder builder = new StringBuilder();
    private int indentation;
    public CodeBuilder(GenerationContext context, T cache)
    {
        Context = context;
        Cache = cache;
    }
    public GenerationContext Context { get; }
    public T Cache { get; }
    public CodeBuilder<T> AppendCode(string code = "")
    {
        Context.CancellationToken.ThrowIfCancellationRequested();
        var lines = code.Replace("\r\n", "\n").Split('\n');
        var count = lines.Length > 1 && lines[lines.Length - 1].Length == 0 ? lines.Length - 1 : lines.Length;
        for (var index = 0; index < count; index++)
        {
            if (lines[index].Length != 0) builder.Append(' ', indentation * 4);
            builder.Append(lines[index]).Append('\n');
        }
        return this;
    }
    public CodeBuilder<T> AppendCode<TItem>(IEnumerable<TItem> items, Action<TItem> emit)
    {
        foreach (var item in items)
        {
            Context.CancellationToken.ThrowIfCancellationRequested();
            emit(item);
        }
        return this;
    }
    public CodeBuilder<T> AppendCodeBlock(Action emit)
    {
        AppendCode("{");
        AppendTab(emit);
        return AppendCode("}");
    }
    public CodeBuilder<T> AppendTab(Action emit)
    {
        indentation++;
        try { emit(); }
        finally { indentation--; }
        return this;
    }
    public string ToCode() => builder.ToString();
}

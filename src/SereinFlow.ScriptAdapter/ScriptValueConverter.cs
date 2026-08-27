using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using ScriptLang.Runtime;

namespace SereinFlow.ScriptAdapter;

/// <summary>
/// Converts values between the JSON/CLR boundary and ScriptLang values. The
/// audit projection is deliberately separate from in-process script values.
/// 在 JSON/CLR 边界与 ScriptLang 值之间转换。审计投影与进程内脚本值刻意分离。
/// </summary>
public static class ScriptValueConverter
{
    public static Value ToScriptValue(object? value)
    {
        if (value is JsonElement element)
            return FromJsonElement(element);

        return value switch
        {
            null => Value.Null,
            Value scriptValue => scriptValue,
            bool boolean => BoolValue.Create(boolean),
            byte number => NumberValueFactory.Create((int)number),
            short number => NumberValueFactory.Create((int)number),
            int number => NumberValueFactory.Create(number),
            uint number => NumberValueFactory.Create((long)number),
            long number => NumberValueFactory.Create(number),
            ulong number => NumberValueFactory.Create(Convert.ToDecimal(number, CultureInfo.InvariantCulture)),
            float number => NumberValueFactory.Create(number),
            double number => NumberValueFactory.Create(number),
            decimal number => NumberValueFactory.Create(number),
            string text => StringValue.Create(text),
            char character => StringValue.Create(character.ToString()),
            DateTime dateTime => new DateTimeValue(dateTime),
            TimeSpan timeSpan => new TimeSpanValue(timeSpan),
            IDictionary dictionary => FromDictionary(dictionary),
            IEnumerable sequence when value is not string => FromSequence(sequence),
            // CLR objects are meaningful only inside the current Worker run.
            // They are never allowed to pass into audit/protocol JSON directly.
            // CLR 对象只在当前 Worker Run 内有意义，绝不能直接进入审计或协议 JSON。
            _ => new ClrObjectValue(value)
        };
    }

    /// <summary>
    /// Produces a JSON-safe, diagnostic representation. CLR object/method and
    /// function values are summarized rather than serialized by reference.
    /// 生成 JSON 安全的诊断表示；CLR 对象、方法和函数仅输出摘要，不泄漏进程内引用。
    /// </summary>
    public static object? ToAuditValue(Value value, int maxDepth = 32, int maxItems = 10_000)
    {
        if (value is null)
            throw new ArgumentNullException(nameof(value), "The script value cannot be null. 脚本值不能为空。");
        if (maxDepth < 0)
            throw new InvalidOperationException("Script output exceeded the maximum nesting depth. 脚本输出超过最大嵌套深度。");

        return value switch
        {
            NullValue => null,
            BoolValue boolean => boolean.Value,
            Value { IsNumber: true } number => GetNumberValue(number),
            StringValue text => text.Value,
            ArrayValue array => ToAuditArray(array, maxDepth, maxItems),
            ObjectValue obj => ToAuditObject(obj, maxDepth, maxItems),
            DateTimeValue dateTime => dateTime.Value,
            TimeSpanValue timeSpan => timeSpan.Value,
            ClrObjectValue clrObject => CreateClrObjectSummary(clrObject),
            ClrMethodValue clrMethod => CreateClrMethodSummary(clrMethod),
            _ => CreateRuntimeValueSummary(value)
        };
    }

    /// <summary>
    /// Projects either a ScriptLang value or an arbitrary CLR value for Worker
    /// events, SQLite and live clients. Normal serializable CLR values keep
    /// their JSON shape; otherwise a controlled type summary is emitted.
    /// 将 ScriptLang 或任意 CLR 值投影到 Worker 事件、SQLite 和实时客户端。
    /// 常规可序列化 CLR 值保留 JSON 形状，否则输出受控的类型摘要。
    /// </summary>
    public static object? ToAuditValue(object? value, int maxDepth = 32, int maxItems = 10_000)
    {
        if (value is Value scriptValue)
            return ToAuditValue(scriptValue, maxDepth, maxItems);
        if (value is null)
            return null;
        if (maxDepth < 0)
            return new Dictionary<string, object?> { ["$kind"] = "DepthLimit" };
        if (value is JsonElement element)
            return ToAuditValue(FromJsonElement(element), maxDepth, maxItems);
        if (value is IDictionary dictionary)
        {
            if (dictionary.Count > maxItems)
                return new Dictionary<string, object?> { ["$kind"] = "ItemLimit" };
            var projected = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dictionary)
                projected[Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty] = ToAuditValue(entry.Value, maxDepth - 1, maxItems);
            return projected;
        }
        if (value is IEnumerable sequence and not string)
        {
            var projected = new List<object?>();
            foreach (var item in sequence)
            {
                if (projected.Count >= maxItems)
                    return new Dictionary<string, object?> { ["$kind"] = "ItemLimit" };
                projected.Add(ToAuditValue(item, maxDepth - 1, maxItems));
            }
            return projected;
        }

        try
        {
            // JsonElement is safe to serialize into the protocol and preserves
            // useful DTO results returned by ordinary library nodes.
            // JsonElement 可以安全写入协议，并保留普通类库节点 DTO 返回值。
            return JsonSerializer.SerializeToElement(value);
        }
        catch (Exception exception) when (exception is NotSupportedException or JsonException)
        {
            return new Dictionary<string, object?>
            {
                ["$kind"] = "ClrValue",
                ["clrType"] = value.GetType().FullName,
                ["serializable"] = false
            };
        }
    }

    // Kept as the source-compatible name for callers that explicitly need the
    // serializable representation. It is not used for internal flow transport.
    // 保留该兼容名称供显式需要可序列化表示的调用方使用；流程内部传递不会调用它。
    public static object? ToClrValue(Value value, int maxDepth = 32, int maxItems = 10_000)
        => ToAuditValue(value, maxDepth, maxItems);

    private static Value FromJsonElement(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => Value.Null,
            JsonValueKind.True => BoolValue.True,
            JsonValueKind.False => BoolValue.False,
            JsonValueKind.String => StringValue.Create(element.GetString()),
            JsonValueKind.Number when element.TryGetInt32(out var intValue) => NumberValueFactory.Create(intValue),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => NumberValueFactory.Create(longValue),
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => NumberValueFactory.Create(decimalValue),
            JsonValueKind.Number => NumberValueFactory.Create(element.GetDouble()),
            JsonValueKind.Array => new ArrayValue(element.EnumerateArray().Select(FromJsonElement).ToList()),
            JsonValueKind.Object => new ObjectValue(element.EnumerateObject().ToDictionary(p => p.Name, p => FromJsonElement(p.Value), StringComparer.Ordinal)),
            _ => throw new NotSupportedException($"JSON value kind '{element.ValueKind}' is not supported. 不支持 JSON 值类型“{element.ValueKind}”。")
        };

    private static ObjectValue FromDictionary(IDictionary dictionary)
    {
        var result = new Dictionary<string, Value>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is not string key || string.IsNullOrWhiteSpace(key))
                throw new NotSupportedException("Script object keys must be non-empty strings. 脚本对象键必须是非空字符串。");
            result[key] = ToScriptValue(entry.Value);
        }
        return new ObjectValue(result);
    }

    private static ArrayValue FromSequence(IEnumerable sequence)
    {
        var result = new List<Value>();
        foreach (var item in sequence)
            result.Add(ToScriptValue(item));
        return new ArrayValue(result);
    }

    private static object?[] ToAuditArray(ArrayValue array, int maxDepth, int maxItems)
    {
        if (array.Elements.Count > maxItems)
            throw new InvalidOperationException("Script output exceeded the maximum item count. 脚本输出超过最大项目数。");
        return array.Elements.Select(item => ToAuditValue(item, maxDepth - 1, maxItems)).ToArray();
    }

    private static Dictionary<string, object?> ToAuditObject(ObjectValue value, int maxDepth, int maxItems)
    {
        if (value.Properties.Count > maxItems)
            throw new InvalidOperationException("Script output exceeded the maximum property count. 脚本输出超过最大属性数。");
        return value.Properties.ToDictionary(
            pair => pair.Key,
            pair => ToAuditValue(pair.Value, maxDepth - 1, maxItems),
            StringComparer.Ordinal);
    }

    private static object? GetNumberValue(Value value)
        => value.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value)
            ?? throw new NotSupportedException($"Script number type '{value.GetType().Name}' has no numeric value. 脚本数值类型“{value.GetType().Name}”没有数值内容。");

    private static Dictionary<string, object?> CreateClrObjectSummary(ClrObjectValue value)
        => new(StringComparer.Ordinal)
        {
            ["$kind"] = "ClrObject",
            ["clrType"] = value.Value?.GetType().FullName,
            ["serializable"] = false
        };

    private static Dictionary<string, object?> CreateClrMethodSummary(ClrMethodValue value)
        => new(StringComparer.Ordinal)
        {
            ["$kind"] = "ClrMethod",
            ["declaringType"] = value.MethodInfo.DeclaringType?.FullName,
            ["method"] = value.MethodInfo.Name,
            ["returnType"] = value.ReturnType.FullName,
            ["serializable"] = false
        };

    private static Dictionary<string, object?> CreateRuntimeValueSummary(Value value)
        => new(StringComparer.Ordinal)
        {
            ["$kind"] = value.GetType().Name,
            ["serializable"] = false
        };
}

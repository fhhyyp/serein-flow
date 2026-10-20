using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using SereinFlow.ScriptModules;
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
        var toScriptValueResult = SereinFlowScriptValueConverter.ToScriptValue(value);
        return toScriptValueResult;
    }

    /// <summary>
    /// Produces a JSON-safe, diagnostic representation. CLR object/method and
    /// function values are summarized rather than serialized by reference.
    /// 生成 JSON 安全的诊断表示；CLR 对象、方法和函数仅输出摘要，不泄漏进程内引用。
    /// </summary>
    public static object? ToAuditValue(Value value, int maxDepth = 32, int maxItems = 10_000)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value), "The script value cannot be null. 脚本值不能为空。");
        }

        if (maxDepth < 0)
        {
            throw new InvalidOperationException("Script output exceeded the maximum nesting depth. 脚本输出超过最大嵌套深度。");
        }

        object? evaluatedValue = value switch
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
            _ => CreateRuntimeValueSummary(value)};
        return evaluatedValue;
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
        {
            var toAuditValueResult = ToAuditValue(scriptValue, maxDepth, maxItems);
            return toAuditValueResult;
        }

        if (value is null)
        {
            return null;
        }

        if (maxDepth < 0)
        {
            var dictionary1 = new Dictionary<string, object?>
            {
                ["$kind"] = "DepthLimit"
            };
            return dictionary1;
        }

        if (value is JsonElement element)
        {
            var toScriptValueResult2 = ToScriptValue(element);
            var toAuditValueResult3 = ToAuditValue(toScriptValueResult2, maxDepth, maxItems);
            return toAuditValueResult3;
        }

        if (value is IDictionary dictionary)
        {
            if (dictionary.Count > maxItems)
            {
                var dictionary4 = new Dictionary<string, object?>
                {
                    ["$kind"] = "ItemLimit"
                };
                return dictionary4;
            }

            var projected = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dictionary)
            {
                var indexKey = Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty;
                projected[indexKey] = ToAuditValue(entry.Value, maxDepth - 1, maxItems);
            }

            return projected;
        }

        if (value is IEnumerable sequence and not string)
        {
            var projected = new List<object?>();
            foreach (var item in sequence)
            {
                if (projected.Count >= maxItems)
                {
                    var dictionary5 = new Dictionary<string, object?>
                    {
                        ["$kind"] = "ItemLimit"
                    };
                    return dictionary5;
                }

                var toAuditValueResult6 = ToAuditValue(item, maxDepth - 1, maxItems);
                projected.Add(toAuditValueResult6);
            }

            return projected;
        }

        try
        {
            var serializeToElementResult = JsonSerializer.SerializeToElement(value);
            // JsonElement is safe to serialize into the protocol and preserves
            // useful DTO results returned by ordinary library nodes.
            // JsonElement 可以安全写入协议，并保留普通类库节点 DTO 返回值。
            return serializeToElementResult;
        }
        catch (Exception exception)when (exception is NotSupportedException or JsonException or InvalidOperationException)
        {
            var dictionary7 = new Dictionary<string, object?>
            {
                ["$kind"] = "ClrValue",
                ["clrType"] = value.GetType().FullName,
                ["serializable"] = false
            };
            return dictionary7;
        }
    }

    // Kept as the source-compatible name for callers that explicitly need the
    // serializable representation. It is not used for internal flow transport.
    // 保留该兼容名称供显式需要可序列化表示的调用方使用；流程内部传递不会调用它。
    public static object? ToClrValue(Value value, int maxDepth = 32, int maxItems = 10_000)
    {
        var toAuditValueResult8 = ToAuditValue(value, maxDepth, maxItems);
        return toAuditValueResult8;
    }

    private static object? [] ToAuditArray(ArrayValue array, int maxDepth, int maxItems)
    {
        if (array.Elements.Count > maxItems)
        {
            throw new InvalidOperationException("Script output exceeded the maximum item count. 脚本输出超过最大项目数。");
        }

        var selectResult = array.Elements.Select(item =>
        {
            var toAuditValueResult9 = ToAuditValue(item, maxDepth - 1, maxItems);
            return toAuditValueResult9;
        });
        var toArrayResult = selectResult.ToArray();
        return toArrayResult;
    }

    private static Dictionary<string, object?> ToAuditObject(ObjectValue value, int maxDepth, int maxItems)
    {
        if (value.Properties.Count > maxItems)
        {
            throw new InvalidOperationException("Script output exceeded the maximum property count. 脚本输出超过最大属性数。");
        }

        var toDictionaryResult = value.Properties.ToDictionary(pair => pair.Key, pair =>
        {
            var toAuditValueResult10 = ToAuditValue(pair.Value, maxDepth - 1, maxItems);
            return toAuditValueResult10;
        }, StringComparer.Ordinal);
        return toDictionaryResult;
    }

    private static object? GetNumberValue(Value value)
    {
        var evaluatedValue13 = resolveevaluatedValue() ?? throw resolveNotSupportedException();
        return evaluatedValue13;
        object? resolveevaluatedValue()
        {
            var getTypeResult = value.GetType();
            var optionalReceiver = getTypeResult.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
            object? optionalResult = default(object);
            if (optionalReceiver is not null)
            {
                optionalResult = optionalReceiver.GetValue(value);
            }

            var evaluatedValue11 = optionalResult;
            return evaluatedValue11;
        }

        global::System.Exception resolveNotSupportedException()
        {
            var evaluatedValue12 = $"Script number type '{value.GetType().Name}' has no numeric value. 脚本数值类型“{value.GetType().Name}”没有数值内容。";
            var notSupportedException = new NotSupportedException(evaluatedValue12);
            return notSupportedException;
        }
    }

    private static Dictionary<string, object?> CreateClrObjectSummary(ClrObjectValue value)
    {
        global::System.Collections.Generic.Dictionary<string, object> evaluatedValue14 = new(StringComparer.Ordinal)
        {
            ["$kind"] = "ClrObject",
            ["clrType"] = value.Value?.GetType().FullName,
            ["serializable"] = false
        };
        return evaluatedValue14;
    }

    private static Dictionary<string, object?> CreateClrMethodSummary(ClrMethodValue value)
    {
        global::System.Collections.Generic.Dictionary<string, object> evaluatedValue15 = new(StringComparer.Ordinal)
        {
            ["$kind"] = "ClrMethod",
            ["declaringType"] = value.MethodInfo.DeclaringType?.FullName,
            ["method"] = value.MethodInfo.Name,
            ["returnType"] = value.ReturnType.FullName,
            ["serializable"] = false
        };
        return evaluatedValue15;
    }

    private static Dictionary<string, object?> CreateRuntimeValueSummary(Value value)
    {
        global::System.Collections.Generic.Dictionary<string, object> evaluatedValue16 = new(StringComparer.Ordinal)
        {
            ["$kind"] = value.GetType().Name,
            ["serializable"] = false
        };
        return evaluatedValue16;
    }
}

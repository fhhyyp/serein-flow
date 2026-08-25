using System.Collections;
using System.Globalization;
using System.Text.Json;
using ScriptLang.Runtime;

namespace SereinFlow.ScriptAdapter;

/// <summary>
/// Converts only the JSON-compatible value boundary used by Worker DTOs.
/// 仅转换 Worker DTO 使用的 JSON 兼容值边界。
/// CLR objects, methods and delegates are deliberately rejected on output.
/// CLR 对象、方法和委托在输出时会被明确拒绝。
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
            IDictionary dictionary => FromDictionary(dictionary),
            IEnumerable sequence when value is not string => FromSequence(sequence),
            _ => throw new NotSupportedException($"Value type '{value.GetType().FullName}' is not supported by the script boundary. 脚本边界不支持值类型“{value.GetType().FullName}”。")
        };
    }

    public static object? ToClrValue(Value value, int maxDepth = 32, int maxItems = 10_000)
    {
        if (value is null)
            throw new ArgumentNullException(nameof(value), "The script value cannot be null. 脚本值不能为空。");
        if (maxDepth < 0)
            throw new InvalidOperationException("Script output exceeded the maximum nesting depth. 脚本输出超过最大嵌套深度。");

        return value switch
        {
            NullValue => null,
            BoolValue boolean => boolean.Value,
            NumberValue<int> number => number.Value,
            NumberValue<long> number => number.Value,
            NumberValue<float> number => number.Value,
            NumberValue<double> number => number.Value,
            NumberValue<decimal> number => number.Value,
            StringValue text => text.Value,
            ArrayValue array => ToClrArray(array, maxDepth, maxItems),
            ObjectValue obj => ToClrObject(obj, maxDepth, maxItems),
            DateTimeValue dateTime => dateTime.Value,
            TimeSpanValue timeSpan => timeSpan.Value,
            _ => throw new NotSupportedException($"Script output type '{value.GetType().Name}' is not serializable. 脚本输出类型“{value.GetType().Name}”无法序列化。")
        };
    }

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

    private static object?[] ToClrArray(ArrayValue array, int maxDepth, int maxItems)
    {
        if (array.Elements.Count > maxItems)
            throw new InvalidOperationException("Script output exceeded the maximum item count. 脚本输出超过最大项目数。");
        return array.Elements.Select(item => ToClrValue(item, maxDepth - 1, maxItems)).ToArray();
    }

    private static Dictionary<string, object?> ToClrObject(ObjectValue value, int maxDepth, int maxItems)
    {
        if (value.Properties.Count > maxItems)
            throw new InvalidOperationException("Script output exceeded the maximum property count. 脚本输出超过最大属性数。");
        return value.Properties.ToDictionary(
            pair => pair.Key,
            pair => ToClrValue(pair.Value, maxDepth - 1, maxItems),
            StringComparer.Ordinal);
    }
}

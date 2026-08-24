using System.Collections;
using System.Globalization;
using System.Text.Json;
using ScriptLang.Runtime;

namespace SereinFlow.ScriptAdapter;

/// <summary>
/// Converts only the JSON-compatible value boundary used by Worker DTOs.
/// CLR objects, methods and delegates are deliberately rejected on output.
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
            _ => throw new NotSupportedException($"Value type '{value.GetType().FullName}' is not supported by the script boundary.")
        };
    }

    public static object? ToClrValue(Value value, int maxDepth = 32, int maxItems = 10_000)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (maxDepth < 0)
            throw new InvalidOperationException("Script output exceeded the maximum nesting depth.");

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
            _ => throw new NotSupportedException($"Script output type '{value.GetType().Name}' is not serializable.")
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
            _ => throw new NotSupportedException($"JSON value kind '{element.ValueKind}' is not supported.")
        };

    private static ObjectValue FromDictionary(IDictionary dictionary)
    {
        var result = new Dictionary<string, Value>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is not string key || string.IsNullOrWhiteSpace(key))
                throw new NotSupportedException("Script object keys must be non-empty strings.");
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
            throw new InvalidOperationException("Script output exceeded the maximum item count.");
        return array.Elements.Select(item => ToClrValue(item, maxDepth - 1, maxItems)).ToArray();
    }

    private static Dictionary<string, object?> ToClrObject(ObjectValue value, int maxDepth, int maxItems)
    {
        if (value.Properties.Count > maxItems)
            throw new InvalidOperationException("Script output exceeded the maximum property count.");
        return value.Properties.ToDictionary(
            pair => pair.Key,
            pair => ToClrValue(pair.Value, maxDepth - 1, maxItems),
            StringComparer.Ordinal);
    }
}

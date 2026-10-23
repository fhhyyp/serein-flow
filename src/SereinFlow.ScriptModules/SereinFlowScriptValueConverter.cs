using System;
using System.Collections;
using System.Globalization;
using System.Text.Json;
using ScriptLang.Runtime;

namespace SereinFlow.ScriptModules;
/// <summary>
/// Converts in-process script and CLR values without serializing object handles.
/// 转换进程内脚本值与 CLR 值，不对对象句柄进行序列化。
/// </summary>
public static class SereinFlowScriptValueConverter
{
    private const int MaxDepth = 32;
    private const int MaxItems = 10_000;
    public static Value ToScriptValue(object? value)
    {
        return ToScriptValue(value, MaxDepth);
    }

    /// <summary>
    /// Unwraps script scalars and CLR handles for the shared variable table.
    /// Arrays and objects become CLR containers; nested handles retain identity.
    /// 为共享变量表拆箱脚本标量和 CLR 句柄；数组、对象转为 CLR 容器，嵌套句柄保留原引用。
    /// </summary>
    public static object? ToVariableValue(Value value)
    {
        return ToVariableValue(value, MaxDepth);
    }

    private static Value ToScriptValue(object? value, int depth)
    {
        CheckDepth(depth);
        if (value is JsonElement element)
        {
            return FromJsonElement(element, depth);
        }
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
            ulong number => ConvertUnsignedNumber(number),
            float number => NumberValueFactory.Create(number),
            double number => NumberValueFactory.Create(number),
            decimal number => NumberValueFactory.Create(number),
            string text => StringValue.Create(text),
            char character => ConvertCharacter(character),
            DateTime dateTime => new DateTimeValue(dateTime),
            TimeSpan timeSpan => new TimeSpanValue(timeSpan),
            IDictionary dictionary => FromDictionary(dictionary, depth),
            IEnumerable sequence => FromSequence(sequence, depth),
            _ => new ClrObjectValue(value)
        };
        Value ConvertUnsignedNumber(ulong number)
        {
            return NumberValueFactory.Create(Convert.ToDecimal(number, CultureInfo.InvariantCulture));
        }

        Value ConvertCharacter(char character)
        {
            return StringValue.Create(character.ToString());
        }
    }

    private static object? ToVariableValue(Value value, int depth)
    {
        ArgumentNullException.ThrowIfNull(value);
        CheckDepth(depth);
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
            DateTimeValue dateTime => dateTime.Value,
            TimeSpanValue timeSpan => timeSpan.Value,
            ClrObjectValue clr => clr.Value,
            ArrayValue array => ToVariableArray(array, depth),
            ObjectValue obj => ToVariableObject(obj, depth),
            _ => throw new NotSupportedException(($"Script value '{value.GetType().Name}' cannot be stored as a flow variable. 脚本值“{value.GetType().Name}”不能存入流程变量表。"))
        };
    }

    private static Value FromJsonElement(JsonElement element, int depth)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => Value.Null,
            JsonValueKind.True => BoolValue.True,
            JsonValueKind.False => BoolValue.False,
            JsonValueKind.String => StringValue.Create(element.GetString()),
            JsonValueKind.Number when element.TryGetInt32(out var number) => NumberValueFactory.Create(number),
            JsonValueKind.Number when element.TryGetInt64(out var number) => NumberValueFactory.Create(number),
            JsonValueKind.Number when element.TryGetDecimal(out var number) => NumberValueFactory.Create(number),
            JsonValueKind.Number => NumberValueFactory.Create(element.GetDouble()),
            JsonValueKind.Array => FromJsonArray(element, depth),
            JsonValueKind.Object => FromJsonObject(element, depth),
            _ => throw new NotSupportedException($"Unsupported JSON value kind '{element.ValueKind}'. 不支持 JSON 值类型“{element.ValueKind}”。")
        };
    }

    private static ArrayValue FromJsonArray(JsonElement element, int depth)
    {
        CheckItems(element.GetArrayLength());
        return new ArrayValue(element
            .EnumerateArray()
            .Select(item =>
            {
                return ToScriptValue(item, depth - 1);
            })
            .ToList());
    }

    private static ObjectValue FromJsonObject(JsonElement element, int depth)
    {
        var properties = new Dictionary<string, Value>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            CheckItems(properties.Count + 1);
            var scriptValue = ToScriptValue(property.Value, depth - 1);
            properties.Add(property.Name, scriptValue);
        }
        return new ObjectValue(properties);
    }

    private static ObjectValue FromDictionary(IDictionary dictionary, int depth)
    {
        CheckItems(dictionary.Count);
        var result = new Dictionary<string, Value>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is not string key)
            {
                throw new NotSupportedException("Script object keys must be strings. 脚本对象键必须是字符串。");
            }

            result[key] = ToScriptValue(entry.Value, depth - 1);
        }
        return new ObjectValue(result);
    }

    private static ArrayValue FromSequence(IEnumerable sequence, int depth)
    {
        var result = new List<Value>();
        foreach (var item in sequence)
        {
            CheckItems(result.Count + 1);
            result.Add(ToScriptValue(item, depth - 1));
        }
        return new ArrayValue(result);
    }

    private static object?[] ToVariableArray(ArrayValue array, int depth)
    {
        CheckItems(array.Elements.Count);
        return array.Elements.Select(item =>
        {
            return ToVariableValue(item, depth - 1);
        }).ToArray();
    }

    private static Dictionary<string, object?> ToVariableObject(ObjectValue value, int depth)
    {
        CheckItems(value.Properties.Count);
        return value.Properties.ToDictionary(pair => pair.Key, pair =>
        {
            return ToVariableValue(pair.Value, depth - 1);
        }, StringComparer.Ordinal);
    }

    private static void CheckDepth(int depth)
    {
        if (depth < 0)
        {
            throw new NotSupportedException("Script value exceeded the maximum nesting depth (32). 脚本值超过最大嵌套深度（32）。");
        }
    }

    private static void CheckItems(int count)
    {
        if (count > MaxItems)
        {
            throw new NotSupportedException("Script collection exceeded the maximum item count (10000). 脚本集合超过最大项目数（10000）。");
        }
    }
}

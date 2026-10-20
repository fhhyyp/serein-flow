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
        var toScriptValueResult = ToScriptValue(value, MaxDepth);
        return toScriptValueResult;
    }

    /// <summary>
    /// Unwraps script scalars and CLR handles for the shared variable table.
    /// Arrays and objects become CLR containers; nested handles retain identity.
    /// 为共享变量表拆箱脚本标量和 CLR 句柄；数组、对象转为 CLR 容器，嵌套句柄保留原引用。
    /// </summary>
    public static object? ToVariableValue(Value value)
    {
        var toVariableValueResult = ToVariableValue(value, MaxDepth);
        return toVariableValueResult;
    }

    private static Value ToScriptValue(object? value, int depth)
    {
        CheckDepth(depth);
        if (value is JsonElement element)
        {
            var fromJsonElementResult = FromJsonElement(element, depth);
            return fromJsonElementResult;
        }

        Value evaluatedValue = value switch
        {
            null => Value.Null,
            Value scriptValue => scriptValue,
            bool boolean => BoolValue.Create(boolean),
            byte number => NumberValueFactory.Create((int)number),
            short number => NumberValueFactory.Create((int)number),
            int number => NumberValueFactory.Create(number),
            uint number => NumberValueFactory.Create((long)number),
            long number => NumberValueFactory.Create(number),
            ulong number => resolveCreateResult(number),
            float number => NumberValueFactory.Create(number),
            double number => NumberValueFactory.Create(number),
            decimal number => NumberValueFactory.Create(number),
            string text => StringValue.Create(text),
            char character => resolveCreateResult1(character),
            DateTime dateTime => new DateTimeValue(dateTime),
            TimeSpan timeSpan => new TimeSpanValue(timeSpan),
            IDictionary dictionary => FromDictionary(dictionary, depth),
            IEnumerable sequence => FromSequence(sequence, depth),
            _ => new ClrObjectValue(value)
        };
        return evaluatedValue;
        Value resolveCreateResult(ulong number)
        {
            var toDecimalResult = Convert.ToDecimal(number, CultureInfo.InvariantCulture);
            var createResult = NumberValueFactory.Create(toDecimalResult);
            return createResult;
        }

        Value resolveCreateResult1(char character)
        {
            var toStringResult = character.ToString();
            var createResult2 = StringValue.Create(toStringResult);
            return createResult2;
        }
    }

    private static object? ToVariableValue(Value value, int depth)
    {
        ArgumentNullException.ThrowIfNull(value);
        CheckDepth(depth);
        object? evaluatedValue4 = value switch
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
            _ => throw resolveNotSupportedException()
        };
        return evaluatedValue4;
        Exception resolveNotSupportedException()
        {
            var evaluatedValue3 = $"Script value '{value.GetType().Name}' cannot be stored as a flow variable. 脚本值“{value.GetType().Name}”不能存入流程变量表。";
            var notSupportedException = new NotSupportedException(evaluatedValue3);
            return notSupportedException;
        }
    }

    private static Value FromJsonElement(JsonElement element, int depth)
    {
        Value evaluatedValue9 = element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => Value.Null,
            JsonValueKind.True => BoolValue.True,
            JsonValueKind.False => BoolValue.False,
            JsonValueKind.String => resolveCreateResult5(),
            JsonValueKind.Number when element.TryGetInt32(out var number) => NumberValueFactory.Create(number),
            JsonValueKind.Number when element.TryGetInt64(out var number) => NumberValueFactory.Create(number),
            JsonValueKind.Number when element.TryGetDecimal(out var number) => NumberValueFactory.Create(number),
            JsonValueKind.Number => resolveCreateResult7(),
            JsonValueKind.Array => FromJsonArray(element, depth),
            JsonValueKind.Object => FromJsonObject(element, depth),
            _ => throw new NotSupportedException($"Unsupported JSON value kind '{element.ValueKind}'. 不支持 JSON 值类型“{element.ValueKind}”。")
        };
        return evaluatedValue9;
        Value resolveCreateResult5()
        {
            var getStringResult = element.GetString();
            var createResult6 = StringValue.Create(getStringResult);
            return createResult6;
        }

        Value resolveCreateResult7()
        {
            var getDoubleResult = element.GetDouble();
            var createResult8 = NumberValueFactory.Create(getDoubleResult);
            return createResult8;
        }
    }

    private static ArrayValue FromJsonArray(JsonElement element, int depth)
    {
        var getArrayLengthResult = element.GetArrayLength();
        CheckItems(getArrayLengthResult);
        var toListResult = element.EnumerateArray()
            .Select(item =>
            {
                var toScriptValueResult10 = ToScriptValue(item, depth - 1);
                return toScriptValueResult10;
            })
            .ToList();
        var arrayValue = new ArrayValue(toListResult);
        return arrayValue;
    }

    private static ObjectValue FromJsonObject(JsonElement element, int depth)
    {
        var properties = new Dictionary<string, Value>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            CheckItems(properties.Count + 1);
            var toScriptValueResult11 = ToScriptValue(property.Value, depth - 1);
            properties.Add(property.Name, toScriptValueResult11);
        }

        var objectValue = new ObjectValue(properties);
        return objectValue;
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

        var objectValue12 = new ObjectValue(result);
        return objectValue12;
    }

    private static ArrayValue FromSequence(IEnumerable sequence, int depth)
    {
        var result = new List<Value>();
        foreach (var item in sequence)
        {
            CheckItems(result.Count + 1);
            var toScriptValueResult13 = ToScriptValue(item, depth - 1);
            result.Add(toScriptValueResult13);
        }

        var arrayValue14 = new ArrayValue(result);
        return arrayValue14;
    }

    private static object?[] ToVariableArray(ArrayValue array, int depth)
    {
        CheckItems(array.Elements.Count);
        var selectResult16 = array.Elements.Select(item =>
        {
            var toVariableValueResult15 = ToVariableValue(item, depth - 1);
            return toVariableValueResult15;
        });
        var toArrayResult = selectResult16.ToArray();
        return toArrayResult;
    }

    private static Dictionary<string, object?> ToVariableObject(ObjectValue value, int depth)
    {
        CheckItems(value.Properties.Count);
        var toDictionaryResult = value.Properties.ToDictionary(pair => pair.Key, pair =>
        {
            var toVariableValueResult17 = ToVariableValue(pair.Value, depth - 1);
            return toVariableValueResult17;
        }, StringComparer.Ordinal);
        return toDictionaryResult;
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

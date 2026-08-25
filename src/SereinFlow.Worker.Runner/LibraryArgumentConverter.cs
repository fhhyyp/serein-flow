using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SereinFlow.Worker.Runner;

/// <summary>
/// Converts values resolved from flow data connections to reflected DLL
/// method parameter types. Values can originate from JSON, project inputs,
/// or another node and therefore do not necessarily have the CLR type that
/// the target method declares.
/// 将数据连接、项目输入或其他节点解析出的值转换为 DLL 反射方法声明的 CLR 类型。
/// </summary>
public static class LibraryArgumentConverter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
    };

    public static object? Convert(object? value, Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        var nullableType = Nullable.GetUnderlyingType(targetType);
        var effectiveType = nullableType ?? targetType;
        if (value is JsonElement element)
            value = ToClrValue(element);

        if (value is null)
        {
            if (targetType.IsValueType && nullableType is null)
                throw new InvalidCastException($"Null cannot be converted to {targetType.FullName}. 不能将 null 转换为目标类型“{targetType.FullName}”。");
            return null;
        }

        if (effectiveType.IsInstanceOfType(value))
            return value;

        if (effectiveType == typeof(string))
            return ConvertToString(value);

        if (effectiveType == typeof(bool))
            return ConvertToBoolean(value);

        if (effectiveType.IsEnum)
            return ConvertToEnum(value, effectiveType);

        if (effectiveType == typeof(Guid))
            return value is Guid guid ? guid : Guid.Parse(System.Convert.ToString(value, CultureInfo.InvariantCulture)!);

        if (effectiveType == typeof(char))
        {
            var charText = System.Convert.ToString(value, CultureInfo.InvariantCulture)
                ?? throw new InvalidCastException("The value has no character representation. 该值没有字符表示形式。");
            if (charText.Length != 1)
                throw new FormatException("A character value must contain exactly one character. 字符值必须恰好包含一个字符。");
            return charText[0];
        }

        if (effectiveType.IsArray && value is IEnumerable enumerable && value is not string)
            return ConvertArray(enumerable, effectiveType.GetElementType()!);

        try
        {
            if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(effectiveType))
                return System.Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            throw new InvalidCastException($"Value '{value}' cannot be converted to {effectiveType.FullName}. 值“{value}”无法转换为目标类型“{effectiveType.FullName}”。", exception);
        }

        var targetConverter = TypeDescriptor.GetConverter(effectiveType);
        if (targetConverter.CanConvertFrom(value.GetType()))
            return targetConverter.ConvertFrom(null, CultureInfo.InvariantCulture, value);

        if (value is string inputText && targetConverter.CanConvertFrom(typeof(string)))
            return targetConverter.ConvertFromInvariantString(inputText);

        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            return JsonSerializer.Deserialize(json, effectiveType, JsonOptions)
                ?? throw new InvalidCastException($"Value cannot be converted to {effectiveType.FullName}. 值无法转换为目标类型“{effectiveType.FullName}”。");
        }
        catch (JsonException exception)
        {
            throw new InvalidCastException($"Value cannot be converted to {effectiveType.FullName}. 值无法转换为目标类型“{effectiveType.FullName}”。", exception);
        }
    }

    private static string ConvertToString(object value)
    {
        if (value is string text)
            return text;
        if (value is char character)
            return character.ToString();
        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;

        // Preserve useful content when a node returns an object/array and the
        // target method explicitly asks for a string.
        return value is IEnumerable and not string
            ? JsonSerializer.Serialize(value, JsonOptions)
            : value.ToString() ?? string.Empty;
    }

    private static bool ConvertToBoolean(object value)
    {
        if (value is bool boolean)
            return boolean;
        if (value is string text)
        {
            if (bool.TryParse(text, out var parsed))
                return parsed;
            if (text == "1") return true;
            if (text == "0") return false;
        }
        if (value is IConvertible)
        {
            var number = System.Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            if (number == 1) return true;
            if (number == 0) return false;
        }
        throw new FormatException($"Value '{value}' is not a Boolean. 值“{value}”不是布尔值。");
    }

    private static object ConvertToEnum(object value, Type enumType)
    {
        if (value is string text)
            return Enum.Parse(enumType, text, ignoreCase: true);
        var underlying = Enum.GetUnderlyingType(enumType);
        var numeric = System.Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
        return Enum.ToObject(enumType, numeric!);
    }

    private static Array ConvertArray(IEnumerable values, Type elementType)
    {
        var converted = values.Cast<object?>().Select(item => Convert(item, elementType)).ToArray();
        var result = Array.CreateInstance(elementType, converted.Length);
        for (var index = 0; index < converted.Length; index++)
            result.SetValue(converted[index], index);
        return result;
    }

    private static object? ToClrValue(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.Array => element.EnumerateArray().Select(ToClrValue).ToArray(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(item => item.Name, item => ToClrValue(item.Value), StringComparer.Ordinal),
            _ => null,
        };
}

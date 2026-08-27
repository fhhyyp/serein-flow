using System.Collections;
using System.Globalization;
using System.Reflection;
using ScriptLang.Runtime;

namespace SereinFlow.ScriptAdapter;

/// <summary>
/// Converts an in-process ScriptLang value only when a reflected library
/// parameter declares its target CLR type. This is intentionally separate from
/// audit projection, which must never expose CLR references.
/// 仅在反射类库参数声明了目标 CLR 类型时转换进程内 ScriptLang 值。该逻辑刻意
/// 与审计投影分离，审计投影绝不能暴露 CLR 引用。
/// </summary>
public static class ScriptValueTypeConverter
{
    public static object? Convert(Value value, Type targetType)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(targetType);

        try
        {
            return ConvertCore(value, targetType);
        }
        catch (ScriptValueConversionException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException or ArgumentException or TargetInvocationException)
        {
            throw Fail(value, targetType, exception.Message, exception);
        }
    }

    private static object? ConvertCore(Value value, Type targetType)
    {
        var nullableType = Nullable.GetUnderlyingType(targetType);
        var effectiveType = nullableType ?? targetType;

        if (value is NullValue)
        {
            if (effectiveType.IsValueType && nullableType is null)
                throw Fail(value, targetType, "A non-nullable value was required. 目标参数要求非空值。");
            return null;
        }

        if (typeof(Value).IsAssignableFrom(effectiveType))
        {
            if (effectiveType.IsInstanceOfType(value))
                return value;
            throw Fail(value, targetType, "The runtime script value kind does not match the requested Value type. 运行时脚本值类型与目标 Value 类型不匹配。");
        }

        if (value is ClrObjectValue clrObject)
        {
            if (clrObject.Value is not null && effectiveType.IsInstanceOfType(clrObject.Value))
                return clrObject.Value;
            throw Fail(value, targetType, "The wrapped CLR object is not assignable to the target type. 包装的 CLR 对象不能赋值给目标类型。");
        }

        if (value is ClrMethodValue clrMethod)
        {
            if (effectiveType.IsInstanceOfType(clrMethod.MethodInfo))
                return clrMethod.MethodInfo;
            throw Fail(value, targetType, "The CLR method is not assignable to the target type. CLR 方法不能赋值给目标类型。");
        }

        if (effectiveType == typeof(object))
            return ScriptValueConverter.ToAuditValue(value);

        if (value is StringValue text)
        {
            if (effectiveType == typeof(string))
                return text.Value;
            throw Fail(value, targetType, "String values can only be converted to String. 字符串值只能转换为 String。");
        }

        if (value is BoolValue boolean)
        {
            if (effectiveType == typeof(bool))
                return boolean.Value;
            throw Fail(value, targetType, "Boolean values can only be converted to Boolean. 布尔值只能转换为 Boolean。");
        }

        if (value is DateTimeValue dateTime)
        {
            if (effectiveType == typeof(DateTime))
                return dateTime.Value;
            throw Fail(value, targetType, "DateTime values can only be converted to DateTime. 日期时间值只能转换为 DateTime。");
        }

        if (value is TimeSpanValue timeSpan)
        {
            if (effectiveType == typeof(TimeSpan))
                return timeSpan.Value;
            throw Fail(value, targetType, "TimeSpan values can only be converted to TimeSpan. 时间间隔值只能转换为 TimeSpan。");
        }

        if (value is Value { IsNumber: true })
        {
            if (!IsNumericType(effectiveType))
                throw Fail(value, targetType, "Numeric values require a numeric target type. 数值必须转换为数值目标类型。");
            var number = GetNumberValue(value);
            return System.Convert.ChangeType(number, effectiveType, CultureInfo.InvariantCulture);
        }

        if (value is ArrayValue array)
            return ConvertArray(array, effectiveType);

        if (value is ObjectValue obj)
            return ConvertObject(obj, effectiveType);

        throw Fail(value, targetType, "The script value kind is not supported for this target type. 此脚本值类型不支持转换为目标类型。");
    }

    private static object ConvertArray(ArrayValue array, Type targetType)
    {
        if (targetType.IsArray)
        {
            var elementType = targetType.GetElementType()
                ?? throw Fail(array, targetType, "The array element type is unavailable. 数组元素类型不可用。");
            var result = Array.CreateInstance(elementType, array.Elements.Count);
            for (var index = 0; index < array.Elements.Count; index++)
                result.SetValue(Convert(array.Elements[index], elementType), index);
            return result;
        }

        if (!TryGetCollectionElementType(targetType, out var itemType))
            throw Fail(array, targetType, "Array values require an array or compatible generic collection target. 数组值需要数组或兼容的泛型集合目标类型。");

        var listType = typeof(List<>).MakeGenericType(itemType);
        var list = (IList)(Activator.CreateInstance(listType)
            ?? throw Fail(array, targetType, "The collection target cannot be created. 无法创建集合目标类型。"));
        foreach (var item in array.Elements)
            list.Add(Convert(item, itemType));
        if (!targetType.IsAssignableFrom(listType))
            throw Fail(array, targetType, "The generic collection target is not compatible with List<T>. 泛型集合目标与 List<T> 不兼容。");
        return list;
    }

    private static object ConvertObject(ObjectValue value, Type targetType)
    {
        if (TryGetDictionaryValueType(targetType, out var valueType))
        {
            var dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType);
            var dictionary = (IDictionary)(Activator.CreateInstance(dictionaryType)
                ?? throw Fail(value, targetType, "The dictionary target cannot be created. 无法创建字典目标类型。"));
            foreach (var item in value.Properties)
                dictionary.Add(item.Key, Convert(item.Value, valueType));
            if (!targetType.IsAssignableFrom(dictionaryType))
                throw Fail(value, targetType, "The dictionary target is not compatible with Dictionary<string, T>. 字典目标与 Dictionary<string, T> 不兼容。");
            return dictionary;
        }

        if (targetType.IsInterface || targetType.IsAbstract || targetType.IsValueType || targetType == typeof(string))
            throw Fail(value, targetType, "Object values require a dictionary or constructible CLR object target. 对象值需要字典或可构造的 CLR 对象目标类型。");

        var target = Activator.CreateInstance(targetType)
            ?? throw Fail(value, targetType, "The CLR object target cannot be created. 无法创建 CLR 对象目标类型。");
        foreach (var property in targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite || property.GetIndexParameters().Length != 0)
                continue;
            var source = value.Properties.FirstOrDefault(item => string.Equals(item.Key, property.Name, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(source.Key))
                continue;
            property.SetValue(target, Convert(source.Value, property.PropertyType));
        }
        return target;
    }

    private static bool TryGetCollectionElementType(Type type, out Type elementType)
    {
        if (type.IsGenericType && IsSupportedCollectionDefinition(type.GetGenericTypeDefinition()))
        {
            elementType = type.GetGenericArguments()[0];
            return true;
        }

        var enumerable = type.GetInterfaces()
            .Concat([type])
            .FirstOrDefault(candidate => candidate.IsGenericType
                && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (enumerable is not null)
        {
            elementType = enumerable.GetGenericArguments()[0];
            return true;
        }

        elementType = null!;
        return false;
    }

    private static bool IsSupportedCollectionDefinition(Type genericDefinition)
        => genericDefinition == typeof(List<>)
            || genericDefinition == typeof(IList<>)
            || genericDefinition == typeof(ICollection<>)
            || genericDefinition == typeof(IEnumerable<>)
            || genericDefinition == typeof(IReadOnlyList<>)
            || genericDefinition == typeof(IReadOnlyCollection<>);

    private static bool TryGetDictionaryValueType(Type type, out Type valueType)
    {
        var dictionary = type.GetInterfaces()
            .Concat([type])
            .FirstOrDefault(candidate => candidate.IsGenericType
                && (candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                    || candidate.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)
                    || candidate.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                && candidate.GetGenericArguments()[0] == typeof(string));
        if (dictionary is not null)
        {
            valueType = dictionary.GetGenericArguments()[1];
            return true;
        }

        if (typeof(IDictionary).IsAssignableFrom(type))
        {
            valueType = typeof(object);
            return true;
        }

        valueType = null!;
        return false;
    }

    private static object GetNumberValue(Value value)
        => value.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value)
            ?? throw Fail(value, typeof(decimal), "The numeric value is unavailable. 数值内容不可用。");

    private static bool IsNumericType(Type type)
        => type == typeof(byte) || type == typeof(sbyte)
            || type == typeof(short) || type == typeof(ushort)
            || type == typeof(int) || type == typeof(uint)
            || type == typeof(long) || type == typeof(ulong)
            || type == typeof(float) || type == typeof(double)
            || type == typeof(decimal);

    private static ScriptValueConversionException Fail(Value value, Type targetType, string detail, Exception? innerException = null)
        => new(
            value.GetType().Name,
            targetType,
            $"Script value '{value.GetType().Name}' cannot be converted to '{targetType.FullName}'. 脚本值“{value.GetType().Name}”无法转换为“{targetType.FullName}”。 {detail}",
            innerException);
}

public sealed class ScriptValueConversionException : InvalidCastException
{
    public ScriptValueConversionException(string valueKind, Type targetType, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ValueKind = valueKind;
        TargetType = targetType;
    }

    public string ValueKind { get; }

    public Type TargetType { get; }
}

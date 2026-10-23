using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace SereinFlow.Mcp.Framework;

/// <summary>
/// Runtime binding rules emitted from compile-time parameter declarations.
/// 从编译期参数声明生成的运行时绑定规则。
/// </summary>
public sealed class McpParameterOptions
{
    public bool Required { get; set; }
    public bool Trim { get; set; }
    public bool AllowEmpty { get; set; }
    public bool AllowNull { get; set; }
    public bool NonEmptyGuid { get; set; }
    public bool IgnoreWhitespace { get; set; }
    public bool AllowNumericEnum { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorPath { get; set; }
}

/// <summary>
/// Optional host-specific wire compatibility binder resolved in the invocation scope.
/// 在调用作用域中解析的可选宿主传输兼容绑定器。
/// </summary>
public interface IMcpRequestBinder<T> where T : class
{
    T Bind(JsonElement arguments, JsonSerializerOptions options);
}

public static class McpArgumentReader
{
    // Tolerant readers are for audit projection; request binding uses the strict readers below.
    // 宽容读取器仅用于审计投影；请求绑定使用下方的严格读取器。
    public static string? TryString(JsonElement arguments, string name)
        => arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public static System.Guid? TryGuid(JsonElement arguments, string name)
        => System.Guid.TryParse(TryString(arguments, name), out var parsed) ? parsed : null;

    public static long? TryInt64(JsonElement arguments, string name)
        => arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var parsed) ? parsed : null;

    public static T? TryEnum<T>(JsonElement arguments, string name) where T : struct, System.Enum
        => System.Enum.TryParse<T>(TryString(arguments, name), true, out var parsed) && System.Enum.IsDefined(parsed) ? parsed : null;

    public static System.Guid? OptionalGuid(JsonElement arguments, string name)
        => arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out var value)
            ? ReadGuid(value, name, new McpParameterOptions()) : null;
    public static McpParameterBindingException Invalid(string name, McpParameterOptions parameter, string message)
        => new(parameter.ErrorMessage ?? message,
            parameter.ErrorPath ?? (parameter.ErrorCode is null ? "$." + name : null), errorCode: parameter.ErrorCode)
        { SuppressErrorData = parameter.ErrorCode is null && parameter.ErrorPath is null };

    public static void Require(JsonElement arguments, string name, McpParameterOptions parameter)
    {
        if (!arguments.TryGetProperty(name, out _))
            throw Invalid(name, parameter, $"MCP parameter '{name}' is required. 必须提供该 MCP 参数。");
    }

    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Compatibility entry point; new callers use ReadString.")]
    public static string? String(JsonElement value, string name, McpParameterOptions parameter)
        => ReadString(value, name, parameter);

    public static string? ReadString(JsonElement value, string name, McpParameterOptions parameter)
    {
        if (value.ValueKind == JsonValueKind.Null && parameter.AllowNull) return null;
        if (value.ValueKind != JsonValueKind.String)
            throw Invalid(name, parameter, $"MCP parameter '{name}' must be a string. 该 MCP 参数必须是字符串。");
        var text = value.GetString();
        if (parameter.Required && !parameter.AllowEmpty && string.IsNullOrWhiteSpace(text))
            throw Invalid(name, parameter, $"MCP parameter '{name}' is required. 必须提供该 MCP 参数。");
        return parameter.Trim ? text?.Trim() : text;
    }

    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Compatibility entry point; new callers use ReadGuid.")]
    public static System.Guid? Guid(JsonElement value, string name, McpParameterOptions parameter)
        => ReadGuid(value, name, parameter);

    public static System.Guid? ReadGuid(JsonElement value, string name, McpParameterOptions parameter)
    {
        var text = ReadString(value, name, parameter);
        if (!parameter.Required && string.IsNullOrWhiteSpace(text)) return null;
        if (!System.Guid.TryParse(text, out var parsed) || (parameter.NonEmptyGuid && parsed == System.Guid.Empty))
            throw Invalid(name, parameter, $"MCP parameter '{name}' must be a GUID. 该 MCP 参数必须是 GUID。");
        return parsed;
    }

    public static System.Guid RequiredGuid(JsonElement value, string name, McpParameterOptions parameter)
        => ReadGuid(value, name, parameter)
            ?? throw Invalid(name, parameter, $"MCP parameter '{name}' must be a GUID. 该 MCP 参数必须是 GUID。");

    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Compatibility entry point; new callers use ReadInt32.")]
    public static int Int32(JsonElement value, string name, McpParameterOptions parameter)
        => ReadInt32(value, name, parameter);

    public static int ReadInt32(JsonElement value, string name, McpParameterOptions parameter)
        => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed) ? parsed
            : throw Invalid(name, parameter, $"MCP parameter '{name}' must be an integer. 该 MCP 参数必须是整数。");

    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Compatibility entry point; new callers use ReadInt64.")]
    public static long Int64(JsonElement value, string name, McpParameterOptions parameter)
        => ReadInt64(value, name, parameter);

    public static long ReadInt64(JsonElement value, string name, McpParameterOptions parameter)
        => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var parsed) ? parsed
            : throw Invalid(name, parameter, $"MCP parameter '{name}' must be an integer. 该 MCP 参数必须是整数。");

    public static bool Boolean(JsonElement value, string name, McpParameterOptions parameter)
        => value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean()
            : throw Invalid(name, parameter, $"MCP parameter '{name}' must be a boolean. 该 MCP 参数必须是布尔值。");

    public static T Enum<T>(JsonElement value, string name, McpParameterOptions parameter) where T : struct, System.Enum
    {
        var text = ReadString(value, name, parameter)?.Trim();
        if (!parameter.AllowNumericEnum && (text is null || !EnumValues<T>.Names.Contains(text)))
            throw Invalid(name, parameter, $"MCP parameter '{name}' contains an invalid enum value. 该 MCP 参数包含无效的枚举值。");
        return System.Enum.TryParse<T>(text, true, out var parsed) && System.Enum.IsDefined(parsed) ? parsed
            : throw Invalid(name, parameter, $"MCP parameter '{name}' contains an invalid enum value. 该 MCP 参数包含无效的枚举值。");
    }

    private static class EnumValues<T> where T : struct, System.Enum
    {
        internal static readonly HashSet<string> Names = new(System.Enum.GetNames<T>(), StringComparer.OrdinalIgnoreCase);
    }

    public static System.DateTimeOffset DateTimeOffset(JsonElement value, string name, McpParameterOptions parameter)
        => System.DateTimeOffset.TryParse(ReadString(value, name, parameter), out var parsed) ? parsed
            : throw Invalid(name, parameter, $"MCP parameter '{name}' must be an ISO date. 该 MCP 参数必须是 ISO 格式日期。");

    public static T Value<T>(JsonElement value, string name, McpParameterOptions parameter, JsonSerializerOptions options)
    {
        if (value.ValueKind == JsonValueKind.Null && !parameter.AllowNull)
            throw Invalid(name, parameter, $"MCP parameter '{name}' cannot be null. 该 MCP 参数不能为 null。");
        try { return value.Deserialize<T>(options)!; }
        catch (JsonException) { throw Invalid(name, parameter, $"MCP parameter '{name}' contains an invalid value. 该 MCP 参数包含无效的值。"); }
    }

    public static JsonElement Json(JsonElement value, string name, McpParameterOptions parameter)
        => value.ValueKind == JsonValueKind.Null && !parameter.AllowNull
            ? throw Invalid(name, parameter, $"MCP parameter '{name}' cannot be null. 该 MCP 参数不能为 null。") : value.Clone();
}

using System.Text.Json;

namespace SereinFlow.Mcp.Framework;

/// <summary>Runtime binding rules emitted from compile-time parameter declarations.</summary>
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

/// <summary>Optional host-specific wire compatibility binder resolved in the invocation scope.</summary>
public interface IMcpRequestBinder<T> where T : class
{
    T Bind(JsonElement arguments, JsonSerializerOptions options);
}

public static class McpArgumentReader
{
    // Tolerant readers are for audit projection; request binding uses the strict readers below.
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
            ? Guid(value, name, new McpParameterOptions()) : null;
    public static McpParameterBindingException Invalid(string name, McpParameterOptions parameter, string message)
        => new(parameter.ErrorMessage ?? message,
            parameter.ErrorPath ?? (parameter.ErrorCode is null ? "$." + name : null), errorCode: parameter.ErrorCode)
        { SuppressErrorData = parameter.ErrorCode is null && parameter.ErrorPath is null };

    public static void Require(JsonElement arguments, string name, McpParameterOptions parameter)
    {
        if (!arguments.TryGetProperty(name, out _))
            throw Invalid(name, parameter, $"MCP parameter '{name}' is required.");
    }

    public static string? String(JsonElement value, string name, McpParameterOptions parameter)
    {
        if (value.ValueKind == JsonValueKind.Null && parameter.AllowNull) return null;
        if (value.ValueKind != JsonValueKind.String)
            throw Invalid(name, parameter, $"MCP parameter '{name}' must be a string.");
        var text = value.GetString();
        if (parameter.Required && !parameter.AllowEmpty && string.IsNullOrWhiteSpace(text))
            throw Invalid(name, parameter, $"MCP parameter '{name}' is required.");
        return parameter.Trim ? text?.Trim() : text;
    }

    public static Guid? Guid(JsonElement value, string name, McpParameterOptions parameter)
    {
        var text = String(value, name, parameter);
        if (!parameter.Required && string.IsNullOrWhiteSpace(text)) return null;
        if (!System.Guid.TryParse(text, out var parsed) || (parameter.NonEmptyGuid && parsed == System.Guid.Empty))
            throw Invalid(name, parameter, $"MCP parameter '{name}' must be a GUID.");
        return parsed;
    }

    public static int Int32(JsonElement value, string name, McpParameterOptions parameter)
        => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed) ? parsed
            : throw Invalid(name, parameter, $"MCP parameter '{name}' must be an integer.");

    public static long Int64(JsonElement value, string name, McpParameterOptions parameter)
        => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var parsed) ? parsed
            : throw Invalid(name, parameter, $"MCP parameter '{name}' must be an integer.");

    public static bool Boolean(JsonElement value, string name, McpParameterOptions parameter)
        => value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean()
            : throw Invalid(name, parameter, $"MCP parameter '{name}' must be a boolean.");

    public static T Enum<T>(JsonElement value, string name, McpParameterOptions parameter) where T : struct, System.Enum
    {
        var text = String(value, name, parameter)?.Trim();
        if (!parameter.AllowNumericEnum && (text is null || !EnumValues<T>.Names.Contains(text)))
            throw Invalid(name, parameter, $"MCP parameter '{name}' contains an invalid enum value.");
        return System.Enum.TryParse<T>(text, true, out var parsed) && System.Enum.IsDefined(parsed) ? parsed
            : throw Invalid(name, parameter, $"MCP parameter '{name}' contains an invalid enum value.");
    }

    private static class EnumValues<T> where T : struct, System.Enum
    {
        internal static readonly HashSet<string> Names = new(System.Enum.GetNames<T>(), StringComparer.OrdinalIgnoreCase);
    }

    public static System.DateTimeOffset DateTimeOffset(JsonElement value, string name, McpParameterOptions parameter)
        => System.DateTimeOffset.TryParse(String(value, name, parameter), out var parsed) ? parsed
            : throw Invalid(name, parameter, $"MCP parameter '{name}' must be an ISO date.");

    public static T Value<T>(JsonElement value, string name, McpParameterOptions parameter, JsonSerializerOptions options)
    {
        if (value.ValueKind == JsonValueKind.Null && !parameter.AllowNull)
            throw Invalid(name, parameter, $"MCP parameter '{name}' cannot be null.");
        try { return value.Deserialize<T>(options)!; }
        catch (JsonException) { throw Invalid(name, parameter, $"MCP parameter '{name}' contains an invalid value."); }
    }

    public static JsonElement Json(JsonElement value, string name, McpParameterOptions parameter)
        => value.ValueKind == JsonValueKind.Null && !parameter.AllowNull
            ? throw Invalid(name, parameter, $"MCP parameter '{name}' cannot be null.") : value.Clone();
}

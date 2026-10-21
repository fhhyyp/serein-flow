using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace SereinFlow.Mcp.Framework;

/// <summary>Shared JSON and validation policy used by generated strongly typed delegates.</summary>
public static class McpRequestBinder
{
    public static JsonElement Normalize(JsonElement arguments) => arguments.ValueKind == JsonValueKind.Undefined
        ? JsonSerializer.SerializeToElement(new { }) : arguments;

    public static T Bind<T>(McpToolCallContext context, JsonElement arguments, JsonSerializerOptions jsonOptions,
        Action<T, string, HashSet<object>> validate) where T : class
    {
        if (arguments.ValueKind == JsonValueKind.Undefined)
        {
            arguments = JsonSerializer.SerializeToElement(new { });
        }

        T request;
        try
        {
            var binder = context.Services.GetService<IMcpRequestBinder<T>>();
            request = (binder is null ? arguments.Deserialize<T>(jsonOptions) : binder.Bind(arguments, jsonOptions))
                ?? throw new McpParameterBindingException("MCP arguments cannot be null.", "$");
        }
        catch (JsonException exception)
        {
            var path = string.IsNullOrWhiteSpace(exception.Path) ? null : exception.Path;
            var location = path is null ? string.Empty : $" at '{path}'";
            throw new McpParameterBindingException($"MCP arguments are invalid{location}.", path);
        }
        catch (NotSupportedException)
        {
            throw new McpParameterBindingException("MCP arguments contain an unsupported contract value.");
        }

        validate(request, "$", new HashSet<object>(ReferenceEqualityComparer.Instance));
        return request;
    }

    public static bool ValidateObject(object value, string path, HashSet<object> visited)
    {
        if (!visited.Add(value))
        {
            return false;
        }

        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(value, new ValidationContext(value), errors, validateAllProperties: true))
        {
            throw new McpParameterBindingException("MCP arguments failed validation.", path,
                errors.Select(error => error.ErrorMessage).ToArray());
        }

        return true;
    }

}

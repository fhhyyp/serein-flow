using System.Text.Json;

namespace SereinFlow.Mcp.Framework;

/// <summary>
/// Represents MCP tool descriptor data used by SereinFlow.
/// 表示 SereinFlow 使用的MCPToolDescriptor数据。
/// </summary>
public sealed record McpToolDescriptor(string Name, string Description, JsonElement InputSchema);

/// <summary>Host JSON configuration, snapshotted when the generated registry is loaded.</summary>
public sealed class McpToolDiscoveryOptions
{
    public JsonSerializerOptions JsonOptions { get; init; } = new(JsonSerializerDefaults.Web) { RespectRequiredConstructorParameters = true };
}

/// <summary>The host owns this service scope and any supplied invocation context values.</summary>
public sealed class McpToolCallContext(IServiceProvider services, IReadOnlyDictionary<Type, object>? values = null)
{
    private readonly Dictionary<Type, object> _values = values is null ? [] : new(values);
    public IServiceProvider Services { get; } = services;

    public T GetValue<T>() => _values.TryGetValue(typeof(T), out var value)
        ? (T)value
        : throw new InvalidOperationException($"No invocation context was provided for '{typeof(T)}'.");

    public void SetValue<T>(T value) where T : notnull => _values[typeof(T)] = value;

    public bool TryGetValue<T>(out T? value) where T : class
    {
        value = _values.TryGetValue(typeof(T), out var stored) ? (T)stored : null;
        return value is not null;
    }
}

public sealed class McpParameterBindingException(string message, string? path = null, IReadOnlyList<string?>? errors = null, string? errorCode = null)
    : Exception(message)
{
    public string? Path { get; } = path;
    public IReadOnlyList<string?>? Errors { get; } = errors;
    public string? ErrorCode { get; } = errorCode;
    public bool SuppressErrorData { get; init; }
}

public sealed class McpToolDefinition
{
    private readonly Func<McpToolCallContext, JsonElement, CancellationToken, Task<object?>> _execute;

    public McpToolDefinition(McpToolDescriptor descriptor, IMcpToolMetadata metadata,
        Func<McpToolCallContext, JsonElement, CancellationToken, Task<object?>> execute)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        if (metadata.RequiresIdempotencyKey && metadata.ExecutionKind != McpToolExecutionKind.Mutation)
            throw new ArgumentException("Only mutation tools can require an idempotency key.", nameof(metadata));
    }

    public McpToolDescriptor Descriptor { get; }
    public IMcpToolMetadata Metadata { get; }

    public Task<object?> ExecuteAsync(McpToolCallContext context, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (arguments.ValueKind is not (JsonValueKind.Object or JsonValueKind.Undefined))
        {
            throw new McpParameterBindingException("MCP tool arguments must be a JSON object.", "$");
        }
        return _execute(context, arguments, cancellationToken);
    }
}

public sealed class McpToolCatalog
{
    private readonly Dictionary<string, McpToolDefinition> _byName;
    public McpToolCatalog(IEnumerable<McpToolDefinition> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        var entries = tools.ToArray();
        _byName = new Dictionary<string, McpToolDefinition>(StringComparer.Ordinal);
        foreach (var tool in entries)
        {
            if (tool is null || string.IsNullOrWhiteSpace(tool.Descriptor.Name) || !_byName.TryAdd(tool.Descriptor.Name, tool))
            {
                throw new ArgumentException("Tool names must be nonempty and unique.", nameof(tools));
            }
        }
        Tools = Array.AsReadOnly(entries);
        Descriptors = Array.AsReadOnly(entries.Select(tool => tool.Descriptor).ToArray());
    }

    public IReadOnlyList<McpToolDefinition> Tools { get; }
    public IReadOnlyList<McpToolDescriptor> Descriptors { get; }
    public bool TryGet(string name, out McpToolDefinition? tool)
    {
        tool = null;
        return !string.IsNullOrWhiteSpace(name) && _byName.TryGetValue(name, out tool);
    }
}

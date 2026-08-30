using System.Text.Json;

namespace SereinFlow.Mcp;

public static class McpProtocolConstants
{
    public const string JsonRpcVersion = "2.0";
    public const string ProtocolVersion = "2025-06-18";
    public const string ServerName = "sereinflow";
    public const string ServerVersion = "0.1.0";
}

public sealed record McpResourceDescriptor(
    string Uri,
    string Name,
    string? Description,
    string MimeType = "application/json");

public sealed record McpResourceTemplateDescriptor(
    string UriTemplate,
    string Name,
    string? Description,
    string MimeType = "application/json");

public sealed record McpPromptArgumentDescriptor(
    string Name,
    string? Description,
    bool Required = false);

public sealed record McpPromptDescriptor(
    string Name,
    string? Description,
    IReadOnlyList<McpPromptArgumentDescriptor> Arguments);

public sealed record McpPromptContent(
    string Type,
    string Text);

public sealed record McpPromptMessage(
    string Role,
    McpPromptContent Content);

public sealed record McpPromptResult(
    string? Description,
    IReadOnlyList<McpPromptMessage> Messages);

public sealed record McpToolDescriptor(
    string Name,
    string Description,
    JsonElement InputSchema);

public sealed record McpResourceReadResult(
    string Uri,
    object Value,
    string MimeType = "application/json");

public sealed record McpToolCallResult(object? Value, bool IsError = false);

public interface ISereinFlowMcpBackend
{
    Task<IReadOnlyList<McpResourceDescriptor>> ListResourcesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<McpResourceTemplateDescriptor>> ListResourceTemplatesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<McpPromptDescriptor>> ListPromptsAsync(CancellationToken cancellationToken);

    Task<McpPromptResult> GetPromptAsync(string name, JsonElement arguments, CancellationToken cancellationToken);

    Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken);

    Task<McpResourceReadResult> ReadResourceAsync(string uri, CancellationToken cancellationToken);

    Task<McpToolCallResult> CallToolAsync(string name, JsonElement arguments, CancellationToken cancellationToken);
}

public sealed class McpProtocolException : Exception
{
    public McpProtocolException(int code, string message, object? data = null)
        : base(message)
    {
        Code = code;
        ErrorData = data;
    }

    public int Code { get; }

    public object? ErrorData { get; }
}

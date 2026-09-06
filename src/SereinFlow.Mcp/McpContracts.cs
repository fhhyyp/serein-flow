using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace SereinFlow.Mcp;

public static class McpProtocolConstants
{
    public const string JsonRpcVersion = "2.0";
    public const string ProtocolVersion = "2025-06-18";
    public const string ServerName = "sereinflow";
    public const string ServerVersion = "0.1.0";
}

/// <summary>
/// JSON-RPC error codes emitted by the SereinFlow MCP server.
/// The wire format remains numeric, but call sites use named constants instead
/// of repeating protocol magic numbers.
/// </summary>
public static class McpProtocolErrorCodes
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
    public const int GenericServerError = -32000;
    public const int Unauthenticated = -32001;
    public const int PermissionDenied = -32003;
    public const int ResourceNotFound = -32004;
    public const int TransientFailure = -32005;
    public const int Conflict = -32010;
    public const int OperationRejected = -32011;
    public const int RequestTooLarge = -32012;
    public const int ResponseTooLarge = -32013;
    public const int LibraryInspectionUnavailable = -32020;

    public static int FromHttpStatus(int statusCode)
        => statusCode switch
        {
            StatusCodes.Status400BadRequest => InvalidParams,
            StatusCodes.Status401Unauthorized => Unauthenticated,
            StatusCodes.Status403Forbidden => PermissionDenied,
            StatusCodes.Status404NotFound => ResourceNotFound,
            StatusCodes.Status409Conflict => Conflict,
            StatusCodes.Status422UnprocessableEntity => OperationRejected,
            StatusCodes.Status429TooManyRequests or StatusCodes.Status503ServiceUnavailable or StatusCodes.Status504GatewayTimeout => TransientFailure,
            _ => GenericServerError
        };
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
    public McpProtocolException(int protocolCode, string message, object? data = null)
        : base(message)
    {
        Code = protocolCode;
        ErrorData = data;
    }

    public int Code { get; }

    public object? ErrorData { get; }
}

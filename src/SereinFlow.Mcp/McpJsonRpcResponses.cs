using System.Text.Json;
using System.Text.Json.Serialization;

namespace SereinFlow.Mcp;

// Separate envelopes guarantee result/error exclusivity, including result: null.
// 独立响应类型保证 result/error 互斥，并保留显式的 result: null。
internal sealed record McpJsonRpcSuccessResponse(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] JsonElement? Id,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] object? Result)
{
    [JsonPropertyName("jsonrpc")]
    [JsonPropertyOrder(-1)]
    public string JsonRpc => McpProtocolConstants.JsonRpcVersion;
}

/// <summary>
/// Represents MCP json rpc error response data used by SereinFlow.
/// 表示 SereinFlow 使用的MCPJsonRpc错误响应数据。
/// </summary>
internal sealed record McpJsonRpcErrorResponse<TError>(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] JsonElement? Id,
    TError Error)
{
    [JsonPropertyName("jsonrpc")]
    [JsonPropertyOrder(-1)]
    public string JsonRpc => McpProtocolConstants.JsonRpcVersion;
}

/// <summary>
/// Represents MCP json rpc error data used by SereinFlow.
/// 表示 SereinFlow 使用的MCPJsonRpc错误数据。
/// </summary>
internal sealed record McpJsonRpcError(int Code, string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] object? Data);

// The size-limit fallback historically omits data; ordinary errors include it.
// 超限回退响应省略 data，普通错误则保留该字段，包括 null。
internal sealed record McpJsonRpcErrorSummary(int Code, string Message);

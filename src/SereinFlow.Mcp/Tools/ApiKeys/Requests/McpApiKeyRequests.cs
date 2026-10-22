using SereinFlow.Contracts;

namespace SereinFlow.Mcp;

[McpBindProperties]
internal class McpCreateApiKeyRequest
{
    public Guid? ProjectId { get; set; }

    [McpParameter(Required = true, Trim = true)]
    public string Name { get; set; } = string.Empty;

    [McpParameter(Required = true)]
    public IReadOnlyList<McpPermissionDto> Permissions { get; set; } = Array.Empty<McpPermissionDto>();

    [McpParameter(IgnoreWhitespace = true, ErrorMessage = "MCP parameter 'expiresAt' must be an ISO date. MCP 参数 expiresAt 必须是 ISO 格式日期。")]
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsAdministrator { get; set; } = false;

    [McpParameter(Required = true, Trim = true)]
    public string IdempotencyKey { get; set; } = string.Empty;
}

[McpBindProperties]
internal class McpRotateApiKeyRequest
{
    [McpParameter(Required = true, Trim = true)]
    public string KeyId { get; set; } = string.Empty;

    [McpParameter(Required = true, Trim = true)]
    public string IdempotencyKey { get; set; } = string.Empty;
}

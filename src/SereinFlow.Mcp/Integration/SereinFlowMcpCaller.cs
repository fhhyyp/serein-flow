using Microsoft.AspNetCore.Http;
using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Mcp.Integration;

/// <summary>Caller information exposed to tools without exposing the DI container.</summary>
[Framework.McpContext]
public class SereinFlowMcpCaller(McpPrincipal? principal)
{
    public McpPrincipal? Principal { get; } = principal;

    public McpPrincipal RequirePrincipal() => Principal
        ?? throw new McpSecurityException(McpErrorCodes.Unauthenticated, "MCP authentication is required.", StatusCodes.Status401Unauthorized);
}

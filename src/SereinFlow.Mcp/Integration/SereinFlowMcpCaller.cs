using Microsoft.AspNetCore.Http;
using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Mcp.Integration;

/// <summary>
/// Caller information exposed to tools without exposing the DI container.
/// 向工具公开调用方信息，不公开依赖注入容器。
/// </summary>
[Framework.McpContext]
public sealed class SereinFlowMcpCaller(SereinFlowActor? principal)
{
    public SereinFlowActor? Principal { get; } = principal;

    public SereinFlowActor RequirePrincipal() => Principal
        ?? throw new SereinFlowAccessException(McpErrorCodes.Unauthenticated, "MCP authentication is required. 需要进行 MCP 身份验证。", StatusCodes.Status401Unauthorized);
}

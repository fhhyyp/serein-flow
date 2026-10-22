using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;

namespace SereinFlow.Mcp.Integration;

/// <summary>
/// Compatibility context for business handlers that still need scoped host services.
/// 供仍需作用域内宿主服务的业务处理器使用的兼容上下文。
/// </summary>
[Framework.McpContext]
public sealed class SereinFlowMcpToolContext(IServiceScope scope, McpSecurityService security, McpPrincipal? principal)
    : SereinFlowMcpCaller(principal)
{
    public IServiceScope Scope { get; } = scope ?? throw new ArgumentNullException(nameof(scope));
    public IServiceProvider Services => Scope.ServiceProvider;
    public McpSecurityService Security { get; } = security ?? throw new ArgumentNullException(nameof(security));
}

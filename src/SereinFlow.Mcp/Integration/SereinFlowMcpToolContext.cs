using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;

namespace SereinFlow.Mcp.Integration;

/// <summary>Compatibility context for business handlers that still need scoped host services.</summary>
[Framework.McpContext]
public sealed class SereinFlowMcpToolContext(IServiceScope scope, McpSecurityService security, McpPrincipal? principal)
    : SereinFlowMcpCaller(principal)
{
    public IServiceScope Scope { get; } = scope ?? throw new ArgumentNullException(nameof(scope));
    public IServiceProvider Services => Scope.ServiceProvider;
    public McpSecurityService Security { get; } = security ?? throw new ArgumentNullException(nameof(security));
}

using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Mcp;

/// <summary>
/// Resolves SereinFlow resource URIs through the same scoped read handlers
/// used by MCP tools. URI syntax belongs here, not in the backend facade.
/// </summary>
public sealed class McpResourceReader
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMcpPrincipalAccessor _principalAccessor;
    private readonly McpAiGuidanceProvider _aiGuidanceProvider;

    public McpResourceReader(
        IServiceScopeFactory scopeFactory,
        IMcpPrincipalAccessor principalAccessor,
        McpAiGuidanceProvider aiGuidanceProvider)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _principalAccessor = principalAccessor ?? throw new ArgumentNullException(nameof(principalAccessor));
        _aiGuidanceProvider = aiGuidanceProvider ?? throw new ArgumentNullException(nameof(aiGuidanceProvider));
    }

    public async Task<McpResourceReadResult> ReadAsync(string uri, CancellationToken cancellationToken)
    {
        if (McpAiGuidance.IsGuidanceUri(uri))
            return await _aiGuidanceProvider.ReadAsync(uri, cancellationToken);

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
            || !string.Equals(parsed.Scheme, "sereinflow", StringComparison.OrdinalIgnoreCase))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The SereinFlow resource URI is invalid.");
        }

        var collection = parsed.Host.ToLowerInvariant();
        var segments = parsed.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();

        using var scope = _scopeFactory.CreateScope();
        var context = new McpToolContext(
            scope,
            scope.ServiceProvider.GetRequiredService<McpSecurityService>(),
            _principalAccessor.Current);
        object? value = collection switch
        {
            "projects" when segments.Length == 0
                => await McpReadModelToolHandlers.ReadProjectsResourceAsync(context, cancellationToken),
            "archived-projects" when segments.Length == 0
                => await McpReadModelToolHandlers.ReadArchivedProjectsResourceAsync(context, cancellationToken),
            "projects" when segments.Length == 1 && Guid.TryParse(segments[0], out var projectId)
                => await McpReadModelToolHandlers.ReadProjectResourceAsync(context, projectId, cancellationToken),
            "projects" when segments.Length == 4
                && Guid.TryParse(segments[0], out var topologyProjectId)
                && string.Equals(segments[1], "flows", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(segments[2], out var topologyFlowId)
                && string.Equals(segments[3], "topology", StringComparison.OrdinalIgnoreCase)
                => await McpReadModelToolHandlers.ReadTopologyResourceAsync(context, topologyProjectId, topologyFlowId, cancellationToken),
            "libraries" when segments.Length == 0
                => await McpReadModelToolHandlers.ReadLibrariesResourceAsync(context, cancellationToken),
            "archived-libraries" when segments.Length == 0
                => await McpReadModelToolHandlers.ReadArchivedLibrariesResourceAsync(context, cancellationToken),
            "libraries" when segments.Length == 1
                => await McpReadModelToolHandlers.ReadLibraryResourceAsync(context, segments[0], cancellationToken),
            "library-families" when segments.Length == 0
                => await McpReadModelToolHandlers.ReadLibraryFamiliesResourceAsync(context, cancellationToken),
            "library-families" when segments.Length == 1
                => await McpReadModelToolHandlers.ReadLibraryFamilyResourceAsync(context, segments[0], cancellationToken),
            "projects" when segments.Length == 2
                && Guid.TryParse(segments[0], out var librariesProjectId)
                && string.Equals(segments[1], "libraries", StringComparison.OrdinalIgnoreCase)
                => await McpReadModelToolHandlers.ReadProjectLibrariesResourceAsync(context, librariesProjectId, cancellationToken),
            "projects" when segments.Length == 3
                && Guid.TryParse(segments[0], out var libraryUpgradeProjectId)
                && string.Equals(segments[1], "library-upgrades", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(segments[2], out var upgradeId)
                => await McpReadModelToolHandlers.ReadLibraryUpgradeResourceAsync(
                    context, libraryUpgradeProjectId, upgradeId, cancellationToken),
            "runs" when segments.Length == 0
                => await McpDebugToolHandlers.ReadRunsResourceAsync(context, cancellationToken),
            "runs" when segments.Length == 1 && Guid.TryParse(segments[0], out var runId)
                => await McpReadModelToolHandlers.ReadRunResourceAsync(context, runId, cancellationToken),
            "runs" when segments.Length == 2
                && Guid.TryParse(segments[0], out var workpiecesRunId)
                && string.Equals(segments[1], "workpieces", StringComparison.OrdinalIgnoreCase)
                => await McpReadModelToolHandlers.ReadRunWorkpiecesResourceAsync(context, workpiecesRunId, cancellationToken),
            "runs" when segments.Length == 3
                && Guid.TryParse(segments[0], out var workpieceRunId)
                && string.Equals(segments[1], "workpieces", StringComparison.OrdinalIgnoreCase)
                => await McpReadModelToolHandlers.ReadRunWorkpieceResourceAsync(context, workpieceRunId, segments[2], cancellationToken),
            "debug-sessions" when segments.Length == 0
                => await McpDebugToolHandlers.ReadDebugSessionsResourceAsync(context, cancellationToken),
            "debug-sessions" when segments.Length == 1 && Guid.TryParse(segments[0], out var sessionId)
                => await McpReadModelToolHandlers.ReadDebugResourceAsync(context, sessionId, cancellationToken),
            "projects" when segments.Length == 5
                && Guid.TryParse(segments[0], out var versionsProjectId)
                && string.Equals(segments[1], "flows", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(segments[2], out var versionsFlowId)
                && string.Equals(segments[3], "versions", StringComparison.OrdinalIgnoreCase)
                => await McpReadModelToolHandlers.ReadVersionsResourceAsync(
                    context, versionsProjectId, versionsFlowId, segments[4], cancellationToken),
            "projects" when segments.Length == 6
                && Guid.TryParse(segments[0], out var versionProjectId)
                && string.Equals(segments[1], "flows", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(segments[2], out var versionFlowId)
                && string.Equals(segments[3], "versions", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(segments[5], out var version)
                => await McpReadModelToolHandlers.ReadVersionResourceAsync(
                    context, versionProjectId, versionFlowId, segments[4], version, cancellationToken),
            "mcp-previews" when segments.Length == 1 && Guid.TryParse(segments[0], out var previewId)
                => await McpReadModelToolHandlers.ReadPreviewResourceAsync(context, previewId, cancellationToken),
            _ => throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The SereinFlow resource URI is not supported.")
        };

        if (value is null)
            throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The requested SereinFlow resource was not found.");

        return new McpResourceReadResult(uri, value);
    }
}

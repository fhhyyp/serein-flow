using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
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
        if (_aiGuidanceProvider.IsGuidanceUri(uri))
            return await _aiGuidanceProvider.ReadAsync(uri, cancellationToken);

        if (!McpResourceUriParser.TryParse(uri, out var match) || match is null)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The SereinFlow resource URI is invalid.");
        }

        using var scope = _scopeFactory.CreateScope();
        var context = new McpToolContext(
            scope,
            scope.ServiceProvider.GetRequiredService<McpSecurityService>(),
            _principalAccessor.Current);
        var parameters = match.Parameters;
        object? value = match.Id switch
        {
            McpResourceId.Projects
                => await McpReadModelToolHandlers.ReadProjectsResourceAsync(context, cancellationToken),
            McpResourceId.ArchivedProjects
                => await McpReadModelToolHandlers.ReadArchivedProjectsResourceAsync(context, cancellationToken),
            McpResourceId.Project
                => await McpReadModelToolHandlers.ReadProjectResourceAsync(context, Guid.Parse(parameters["projectId"]), cancellationToken),
            McpResourceId.FlowTopology
                => await McpReadModelToolHandlers.ReadTopologyResourceAsync(context, Guid.Parse(parameters["projectId"]), Guid.Parse(parameters["flowId"]), cancellationToken),
            McpResourceId.Libraries
                => await McpReadModelToolHandlers.ReadLibrariesResourceAsync(context, cancellationToken),
            McpResourceId.ArchivedLibraries
                => await McpReadModelToolHandlers.ReadArchivedLibrariesResourceAsync(context, cancellationToken),
            McpResourceId.Library
                => await McpReadModelToolHandlers.ReadLibraryResourceAsync(context, parameters["libraryId"], cancellationToken),
            McpResourceId.LibraryFamilies
                => await McpReadModelToolHandlers.ReadLibraryFamiliesResourceAsync(context, cancellationToken),
            McpResourceId.LibraryFamily
                => await McpReadModelToolHandlers.ReadLibraryFamilyResourceAsync(context, parameters["familyId"], cancellationToken),
            McpResourceId.ProjectLibraries
                => await McpReadModelToolHandlers.ReadProjectLibrariesResourceAsync(context, Guid.Parse(parameters["projectId"]), cancellationToken),
            McpResourceId.LibraryUpgrade
                => await McpReadModelToolHandlers.ReadLibraryUpgradeResourceAsync(context, Guid.Parse(parameters["projectId"]), Guid.Parse(parameters["upgradeId"]), cancellationToken),
            McpResourceId.Runs
                => await McpDebugToolHandlers.ReadRunsResourceAsync(context, cancellationToken),
            McpResourceId.Run
                => await McpReadModelToolHandlers.ReadRunResourceAsync(context, Guid.Parse(parameters["runId"]), cancellationToken),
            McpResourceId.RunWorkpieces
                => await McpReadModelToolHandlers.ReadRunWorkpiecesResourceAsync(context, Guid.Parse(parameters["runId"]), cancellationToken),
            McpResourceId.RunWorkpiece
                => await McpReadModelToolHandlers.ReadRunWorkpieceResourceAsync(context, Guid.Parse(parameters["runId"]), parameters["workpieceId"], cancellationToken),
            McpResourceId.DebugSessions
                => await McpDebugToolHandlers.ReadDebugSessionsResourceAsync(context, cancellationToken),
            McpResourceId.DebugSession
                => await McpReadModelToolHandlers.ReadDebugResourceAsync(context, Guid.Parse(parameters["sessionId"]), cancellationToken),
            McpResourceId.FlowVersions
                => await McpReadModelToolHandlers.ReadVersionsResourceAsync(context, Guid.Parse(parameters["projectId"]), Guid.Parse(parameters["flowId"]), parameters["track"], cancellationToken),
            McpResourceId.FlowVersion
                => await McpReadModelToolHandlers.ReadVersionResourceAsync(context, Guid.Parse(parameters["projectId"]), Guid.Parse(parameters["flowId"]), parameters["track"], long.Parse(parameters["version"], CultureInfo.InvariantCulture), cancellationToken),
            McpResourceId.Preview
                => await McpReadModelToolHandlers.ReadPreviewResourceAsync(context, Guid.Parse(parameters["previewId"]), cancellationToken),
            _ => throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The SereinFlow resource URI is not supported.")
        };

        if (value is null)
            throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The requested SereinFlow resource was not found.");

        return new McpResourceReadResult(uri, value);
    }
}

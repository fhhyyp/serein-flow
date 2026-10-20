using System.Threading.Tasks;
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
    public McpResourceReader(IServiceScopeFactory scopeFactory, IMcpPrincipalAccessor principalAccessor, McpAiGuidanceProvider aiGuidanceProvider)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _principalAccessor = principalAccessor ?? throw new ArgumentNullException(nameof(principalAccessor));
        _aiGuidanceProvider = aiGuidanceProvider ?? throw new ArgumentNullException(nameof(aiGuidanceProvider));
    }

    public async Task<McpResourceReadResult> ReadAsync(string uri, CancellationToken cancellationToken)
    {
        if (_aiGuidanceProvider.IsGuidanceUri(uri))
        {
            var readAsyncResult = await _aiGuidanceProvider.ReadAsync(uri, cancellationToken);
            return readAsyncResult;
        }

        if (!McpResourceUriParser.TryParse(uri, out var match) || match is null)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The SereinFlow resource URI is invalid.");
        }

        using var scope = _scopeFactory.CreateScope();
        var getRequiredServiceResult = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        var context = new McpToolContext(scope, getRequiredServiceResult, _principalAccessor.Current);
        var parameters = match.Parameters;
        object? value = match.Id switch
        {
            McpResourceId.Projects => await McpReadModelToolHandlers.ReadProjectsResourceAsync(context, cancellationToken),
            McpResourceId.ArchivedProjects => await McpReadModelToolHandlers.ReadArchivedProjectsResourceAsync(context, cancellationToken),
            McpResourceId.Project => await resolveReadProjectResourceAsyncResult(),
            McpResourceId.FlowTopology => await resolveReadTopologyResourceAsyncResult(),
            McpResourceId.Libraries => await McpReadModelToolHandlers.ReadLibrariesResourceAsync(context, cancellationToken),
            McpResourceId.ArchivedLibraries => await McpReadModelToolHandlers.ReadArchivedLibrariesResourceAsync(context, cancellationToken),
            McpResourceId.Library => await McpReadModelToolHandlers.ReadLibraryResourceAsync(context, parameters["libraryId"], cancellationToken),
            McpResourceId.LibraryFamilies => await McpReadModelToolHandlers.ReadLibraryFamiliesResourceAsync(context, cancellationToken),
            McpResourceId.LibraryFamily => await McpReadModelToolHandlers.ReadLibraryFamilyResourceAsync(context, parameters["familyId"], cancellationToken),
            McpResourceId.ProjectLibraries => await resolveReadProjectLibrariesResourceAsyncResult(),
            McpResourceId.LibraryUpgrade => await resolveReadLibraryUpgradeResourceAsyncResult(),
            McpResourceId.Runs => await McpDebugToolHandlers.ReadRunsResourceAsync(context, cancellationToken),
            McpResourceId.Run => await resolveReadRunResourceAsyncResult(),
            McpResourceId.RunWorkpieces => await resolveReadRunWorkpiecesResourceAsyncResult(),
            McpResourceId.RunWorkpiece => await resolveReadRunWorkpieceResourceAsyncResult(),
            McpResourceId.DebugSessions => await McpDebugToolHandlers.ReadDebugSessionsResourceAsync(context, cancellationToken),
            McpResourceId.DebugSession => await resolveReadDebugResourceAsyncResult(),
            McpResourceId.FlowVersions => await resolveReadVersionsResourceAsyncResult(),
            McpResourceId.FlowVersion => await resolveReadVersionResourceAsyncResult(),
            McpResourceId.Preview => await resolveReadPreviewResourceAsyncResult(),
            _ => throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The SereinFlow resource URI is not supported.")};
        if (value is null)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The requested SereinFlow resource was not found.");
        }

        var mcpResourceReadResult = new McpResourceReadResult(uri, value);
        return mcpResourceReadResult;
        Task<object?> resolveReadProjectResourceAsyncResult()
        {
            var parseResult = Guid.Parse(parameters["projectId"]);
            var readProjectResourceAsyncResult = McpReadModelToolHandlers.ReadProjectResourceAsync(context, parseResult, cancellationToken);
            return readProjectResourceAsyncResult;
        }

        Task<object?> resolveReadTopologyResourceAsyncResult()
        {
            var parseResult1 = Guid.Parse(parameters["projectId"]);
            var parseResult2 = Guid.Parse(parameters["flowId"]);
            var readTopologyResourceAsyncResult = McpReadModelToolHandlers.ReadTopologyResourceAsync(context, parseResult1, parseResult2, cancellationToken);
            return readTopologyResourceAsyncResult;
        }

        Task<object> resolveReadProjectLibrariesResourceAsyncResult()
        {
            var parseResult3 = Guid.Parse(parameters["projectId"]);
            var readProjectLibrariesResourceAsyncResult = McpReadModelToolHandlers.ReadProjectLibrariesResourceAsync(context, parseResult3, cancellationToken);
            return readProjectLibrariesResourceAsyncResult;
        }

        Task<object> resolveReadLibraryUpgradeResourceAsyncResult()
        {
            var parseResult4 = Guid.Parse(parameters["projectId"]);
            var parseResult5 = Guid.Parse(parameters["upgradeId"]);
            var readLibraryUpgradeResourceAsyncResult = McpReadModelToolHandlers.ReadLibraryUpgradeResourceAsync(context, parseResult4, parseResult5, cancellationToken);
            return readLibraryUpgradeResourceAsyncResult;
        }

        Task<object?> resolveReadRunResourceAsyncResult()
        {
            var parseResult6 = Guid.Parse(parameters["runId"]);
            var readRunResourceAsyncResult = McpReadModelToolHandlers.ReadRunResourceAsync(context, parseResult6, cancellationToken);
            return readRunResourceAsyncResult;
        }

        Task<object?> resolveReadRunWorkpiecesResourceAsyncResult()
        {
            var parseResult7 = Guid.Parse(parameters["runId"]);
            var readRunWorkpiecesResourceAsyncResult = McpReadModelToolHandlers.ReadRunWorkpiecesResourceAsync(context, parseResult7, cancellationToken);
            return readRunWorkpiecesResourceAsyncResult;
        }

        Task<object?> resolveReadRunWorkpieceResourceAsyncResult()
        {
            var parseResult8 = Guid.Parse(parameters["runId"]);
            var readRunWorkpieceResourceAsyncResult = McpReadModelToolHandlers.ReadRunWorkpieceResourceAsync(context, parseResult8, parameters["workpieceId"], cancellationToken);
            return readRunWorkpieceResourceAsyncResult;
        }

        Task<object?> resolveReadDebugResourceAsyncResult()
        {
            var parseResult9 = Guid.Parse(parameters["sessionId"]);
            var readDebugResourceAsyncResult = McpReadModelToolHandlers.ReadDebugResourceAsync(context, parseResult9, cancellationToken);
            return readDebugResourceAsyncResult;
        }

        Task<object> resolveReadVersionsResourceAsyncResult()
        {
            var parseResult10 = Guid.Parse(parameters["projectId"]);
            var parseResult11 = Guid.Parse(parameters["flowId"]);
            var readVersionsResourceAsyncResult = McpReadModelToolHandlers.ReadVersionsResourceAsync(context, parseResult10, parseResult11, parameters["track"], cancellationToken);
            return readVersionsResourceAsyncResult;
        }

        Task<object?> resolveReadVersionResourceAsyncResult()
        {
            var parseResult12 = Guid.Parse(parameters["projectId"]);
            var parseResult13 = Guid.Parse(parameters["flowId"]);
            var parseResult14 = long.Parse(parameters["version"], CultureInfo.InvariantCulture);
            var readVersionResourceAsyncResult = McpReadModelToolHandlers.ReadVersionResourceAsync(context, parseResult12, parseResult13, parameters["track"], parseResult14, cancellationToken);
            return readVersionResourceAsyncResult;
        }

        Task<object> resolveReadPreviewResourceAsyncResult()
        {
            var parseResult15 = Guid.Parse(parameters["previewId"]);
            var readPreviewResourceAsyncResult = McpReadModelToolHandlers.ReadPreviewResourceAsync(context, parseResult15, cancellationToken);
            return readPreviewResourceAsyncResult;
        }
    }
}

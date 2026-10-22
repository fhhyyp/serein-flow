using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Mcp.Integration;
using SereinFlow.Mcp.Queries;
using SereinFlow.Mcp.Previews;

namespace SereinFlow.Mcp;
/// <summary>
/// Resolves SereinFlow resource URIs through the same scoped read handlers
/// used by MCP tools. URI syntax belongs here, not in the backend facade.
/// 通过 MCP 工具共用的作用域内读取处理器解析 SereinFlow 资源 URI；URI 语法由此处负责，不放入后端门面。
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
        var guidance = await _aiGuidanceProvider.TryReadAsync(uri, cancellationToken);
        if (guidance is not null)
        {
            return guidance;
        }

        if (!McpResourceUriParser.TryParse(uri, out var match) || match is null)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The SereinFlow resource URI is invalid. SereinFlow 资源 URI 无效。");
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var principal = _principalAccessor.Current;
        var caller = new SereinFlowMcpCaller(principal);
        var parameters = match.Parameters;
        object? value = match.Id switch
        {
            McpResourceId.Projects => await scope.ServiceProvider.GetRequiredService<McpProjectQueries>().ReadProjectsAsync(principal, false, cancellationToken),
            McpResourceId.ArchivedProjects => await scope.ServiceProvider.GetRequiredService<McpProjectQueries>().ReadProjectsAsync(principal, true, cancellationToken),
            McpResourceId.Project => await scope.ServiceProvider.GetRequiredService<McpProjectQueries>().ReadProjectAsync(principal, GetGuid("projectId"), cancellationToken),
            McpResourceId.FlowTopology => await scope.ServiceProvider.GetRequiredService<McpFlowQueries>().ReadFlowTopologyAsync(principal, GetGuid("projectId"), GetGuid("flowId"), FlowVersionTrackDto.Development, null, null, cancellationToken),
            McpResourceId.Libraries => await scope.ServiceProvider.GetRequiredService<McpLibraryQueries>().ReadLibrariesAsync(principal, cancellationToken),
            McpResourceId.ArchivedLibraries => await scope.ServiceProvider.GetRequiredService<McpLibraryQueries>().ReadLibrariesAsync(principal, cancellationToken, McpLibraryQueries.LibraryListScope.Archived),
            McpResourceId.Library => await scope.ServiceProvider.GetRequiredService<McpLibraryQueries>().ReadLibraryAsync(principal, parameters["libraryId"], cancellationToken),
            McpResourceId.LibraryFamilies => await scope.ServiceProvider.GetRequiredService<McpLibraryQueries>().ReadLibraryFamiliesAsync(principal, true, new AiReadModelOptions(), cancellationToken),
            McpResourceId.LibraryFamily => await scope.ServiceProvider.GetRequiredService<McpLibraryQueries>().ReadLibraryFamilyAsync(principal, parameters["familyId"], cancellationToken),
            McpResourceId.ProjectLibraries => await scope.ServiceProvider.GetRequiredService<McpLibraryQueries>().ReadProjectLibrariesAsync(principal, GetGuid("projectId"), new AiReadModelOptions(), cancellationToken),
            McpResourceId.LibraryUpgrade => await scope.ServiceProvider.GetRequiredService<McpLibraryQueries>().ReadLibraryUpgradeAsync(principal, GetGuid("projectId"), GetGuid("upgradeId"), cancellationToken),
            McpResourceId.Runs => await scope.ServiceProvider.GetRequiredService<McpRunQueries>().ListRunsAsync(caller, new McpListRunsRequest(), cancellationToken),
            McpResourceId.Run => await scope.ServiceProvider.GetRequiredService<McpRunQueries>().ReadRunAsync(principal, GetGuid("runId"), cancellationToken),
            McpResourceId.RunWorkpieces => await scope.ServiceProvider.GetRequiredService<McpRunQueries>().ListRunWorkpiecesAsync(principal, GetGuid("runId"), cancellationToken),
            McpResourceId.RunWorkpiece => await scope.ServiceProvider.GetRequiredService<McpRunQueries>().GetRunWorkpieceAsync(principal, GetGuid("runId"), parameters["workpieceId"].Trim(), cancellationToken),
            McpResourceId.DebugSessions => await scope.ServiceProvider.GetRequiredService<McpDebugQueries>().ListDebugSessionsAsync(caller, new McpListDebugSessionsRequest(), cancellationToken),
            McpResourceId.DebugSession => await scope.ServiceProvider.GetRequiredService<McpDebugQueries>().ReadDebugAsync(principal, GetGuid("sessionId"), cancellationToken),
            McpResourceId.FlowVersions => await scope.ServiceProvider.GetRequiredService<McpFlowQueries>().ReadVersionsAsync(principal, GetGuid("projectId"), GetGuid("flowId"), parameters["track"], cancellationToken),
            McpResourceId.FlowVersion => await scope.ServiceProvider.GetRequiredService<McpFlowQueries>().ReadVersionAsync(principal, GetGuid("projectId"), GetGuid("flowId"), parameters["track"], long.Parse(parameters["version"], CultureInfo.InvariantCulture), cancellationToken),
            McpResourceId.Preview => await scope.ServiceProvider.GetRequiredService<McpPreviewReader>().ReadPreviewAsync(principal, GetGuid("previewId"), cancellationToken),
            _ => throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The SereinFlow resource URI is not supported. 不支持此 SereinFlow 资源 URI。")
        };
        if (value is null)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The requested SereinFlow resource was not found. 未找到请求的 SereinFlow 资源。");
        }

        return new McpResourceReadResult(uri, value);

        Guid GetGuid(string name) => Guid.Parse(parameters[name]);
    }
}

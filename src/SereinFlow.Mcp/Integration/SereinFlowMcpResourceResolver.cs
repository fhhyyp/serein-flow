using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Mcp.Integration;

/// <summary>Reads resource keys from validated DTOs and resolves persisted ownership.</summary>
internal static class SereinFlowMcpResourceResolver
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, PropertyInfo>> Properties = new();

    internal static async ValueTask<SereinFlowResource> ResolveAsync(IServiceProvider services, SereinFlowActor actor, object? request, CancellationToken cancellationToken)
    {
        if (request is null) return SereinFlowResource.Workspace;
        // Reflection reads DTO metadata only; tool dispatch remains generated and strongly typed.
        var properties = Properties.GetOrAdd(request.GetType(), type => type.GetProperties().ToDictionary(property => property.Name, StringComparer.Ordinal));
        Guid? Key(string name) => properties.TryGetValue(name, out var property) && property.GetValue(request) is Guid id && id != Guid.Empty ? id : null;
        var projectId = Key("ProjectId");
        var flowId = Key("FlowId");
        if (Key("RunId") is { } runId)
        {
            var run = await services.GetRequiredService<IFlowRunStore>().FindAsync(runId, cancellationToken) ?? throw Missing(RunErrorCodes.NotFound);
            return SereinFlowResource.Run(runId, run.ProjectId);
        }
        if (Key("SessionId") is { } sessionId)
        {
            var session = await services.GetRequiredService<IFlowDebugSessionService>().FindAsync(sessionId, cancellationToken) ?? throw Missing(DebugErrorCodes.SessionNotFound);
            return new SereinFlowResource("run", session.RunId.ToString("D"), session.ProjectId);
        }
        if (Key("PreviewId") is { } previewId)
        {
            var preview = await services.GetRequiredService<IMcpPreviewStore>().FindAsync(previewId, cancellationToken) ?? throw Missing(McpErrorCodes.PreviewNotFound);
            if (!string.Equals(preview.PrincipalId, actor.Id, StringComparison.Ordinal))
                throw new SereinFlowAccessException(McpErrorCodes.PreviewOwnerMismatch, "The preview belongs to another caller.", 403);
            projectId = preview.ProjectId;
            flowId = preview.FlowId;
        }
        if (projectId is { } project && flowId is { } flow)
        {
            // Flow repositories use the bound (project, flow) pair as a composite key.
            // The handler checks existence through that same pair before any business change.
            return SereinFlowResource.Flow(flow, project);
        }
        if (properties.TryGetValue("LibraryId", out var libraryProperty) && libraryProperty.GetValue(request) is string library)
            return SereinFlowResource.Library(library, projectId);
        return projectId is { } owner ? SereinFlowResource.Project(owner) : SereinFlowResource.Workspace;
    }

    private static McpProtocolException Missing(string code) => new(McpProtocolErrorCodes.ResourceNotFound, "The requested resource was not found.", new { code });
}

using System.Text.Json;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Application;

public enum FlowDefinitionWriteStatus
{
    Saved,
    NoChange,
    NotFound,
    Archived,
    Conflict,
    Invalid,
}

public sealed record FlowDefinitionPreparationResult(
    FlowDefinitionDto Current,
    FlowDefinitionDto Candidate,
    FlowValidationResultDto Validation);

public sealed record FlowDefinitionWriteResult(
    FlowDefinitionWriteStatus Status,
    FlowDefinitionPreparationResult? Preparation = null,
    FlowDefinitionDto? Saved = null,
    long? CurrentVersion = null)
{
    public bool IsSaved => Status == FlowDefinitionWriteStatus.Saved && Saved is not null;
}

/// <summary>
/// Shared application boundary for persistence validation, normalization and
/// optimistic flow-definition writes. API and MCP must use the same boundary
/// so a flow cannot be accepted by one entry point and rejected by another.
/// 统一流程持久化校验、规范化和乐观并发写入边界。API 与 MCP 必须复用，避免
/// 同一流程在不同入口产生不一致的规则。
/// </summary>
public sealed class FlowDefinitionWriteService
{
    private readonly IProjectRepository _projects;
    private readonly IFlowDefinitionRepository _flows;
    private readonly ProjectLibraryService _projectLibraries;
    private readonly IWorkspaceChangePublisher? _changePublisher;

    public FlowDefinitionWriteService(
        IProjectRepository projects,
        IFlowDefinitionRepository flows,
        ProjectLibraryService projectLibraries,
        IWorkspaceChangePublisher? changePublisher = null)
    {
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _flows = flows ?? throw new ArgumentNullException(nameof(flows));
        _projectLibraries = projectLibraries ?? throw new ArgumentNullException(nameof(projectLibraries));
        _changePublisher = changePublisher;
    }

    public async Task<FlowDefinitionPreparationResult?> PrepareAsync(
        Guid projectId,
        Guid flowId,
        FlowDefinitionDto candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (await _projects.FindAsync(projectId, cancellationToken) is null)
            return null;
        var current = await _flows.FindAsync(projectId, flowId, cancellationToken);
        if (current is null)
            return null;

        var diagnostics = FlowDefinitionContractValidator.ValidateForPersistence(candidate)
            .Diagnostics
            .ToList();
        var libraryValidation = await _projectLibraries.ValidateFlowLibrariesAsync(
            projectId,
            candidate,
            cancellationToken);
        diagnostics.AddRange(libraryValidation.Diagnostics);
        var validation = new FlowValidationResultDto(diagnostics.Count == 0, diagnostics);
        var normalized = validation.IsValid
            ? FlowDefinitionContractNormalizer.NormalizeForPersistence(candidate)
            : candidate;
        return new(current, normalized, validation);
    }

    public async Task<FlowDefinitionWriteResult> WriteAsync(
        Guid projectId,
        Guid flowId,
        FlowDefinitionDto candidate,
        long expectedVersion,
        CancellationToken cancellationToken = default,
        string origin = "web")
    {
        if (candidate.Id != flowId || expectedVersion < 1)
        {
            return new(
                FlowDefinitionWriteStatus.Invalid,
                CurrentVersion: expectedVersion);
        }

        var preparation = await PrepareAsync(projectId, flowId, candidate, cancellationToken);
        if (preparation is null)
            return new(FlowDefinitionWriteStatus.NotFound);
        if (await _projects.FindAsync(projectId, cancellationToken) is { Status: SereinFlow.Domain.ProjectStatus.Archived })
            return new(FlowDefinitionWriteStatus.Archived, preparation);
        if (preparation.Current.Version != expectedVersion)
        {
            return new(
                FlowDefinitionWriteStatus.Conflict,
                preparation,
                CurrentVersion: preparation.Current.Version);
        }
        if (!preparation.Validation.IsValid)
            return new(FlowDefinitionWriteStatus.Invalid, preparation);

        if (new FlowDiffService().Compare(preparation.Current, preparation.Candidate).Changes.Count == 0)
            return new(FlowDefinitionWriteStatus.NoChange, preparation, preparation.Current, expectedVersion);

        var saved = await _flows.TryUpdateAsync(
            projectId,
            preparation.Candidate,
            expectedVersion,
            cancellationToken);
        if (saved is null)
        {
            return new(
                FlowDefinitionWriteStatus.Conflict,
                preparation,
                CurrentVersion: (await _flows.FindAsync(projectId, flowId, cancellationToken))?.Version);
        }

        if (_changePublisher is not null)
        {
            var scope = GetChangeScope(preparation.Current, preparation.Candidate);
            await _changePublisher.PublishAsync(
                new WorkspaceChangeEventDto(
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    "flow.changed",
                    projectId,
                    flowId,
                    saved.Version,
                    saved.Checksum,
                    origin,
                    "flow.save",
                    scope.CanvasIds,
                    scope.NodeIds,
                    scope.ConnectionIds,
                    scope.ParameterIds,
                    LibraryIds: ProjectLibraryService.GetLibraryIds(saved)),
                CancellationToken.None);
        }

        return new(FlowDefinitionWriteStatus.Saved, preparation, saved, saved.Version);
    }

    private static ChangeScope GetChangeScope(FlowDefinitionDto before, FlowDefinitionDto after)
    {
        var canvasIds = new HashSet<string>(StringComparer.Ordinal);
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        var connectionIds = new HashSet<string>(StringComparer.Ordinal);
        var parameterIds = new HashSet<string>(StringComparer.Ordinal);
        var beforeCanvases = before.Canvases.ToDictionary(static canvas => canvas.Id, StringComparer.Ordinal);
        var afterCanvases = after.Canvases.ToDictionary(static canvas => canvas.Id, StringComparer.Ordinal);

        foreach (var canvasId in beforeCanvases.Keys.Union(afterCanvases.Keys).Order(StringComparer.Ordinal))
        {
            if (!beforeCanvases.TryGetValue(canvasId, out var oldCanvas))
            {
                AddCanvasScope(afterCanvases[canvasId], canvasIds, nodeIds, connectionIds, parameterIds);
                continue;
            }

            if (!afterCanvases.TryGetValue(canvasId, out var newCanvas))
            {
                AddCanvasScope(oldCanvas, canvasIds, nodeIds, connectionIds, parameterIds);
                continue;
            }

            if (oldCanvas.Lifecycle != newCanvas.Lifecycle
                || !string.Equals(oldCanvas.Name, newCanvas.Name, StringComparison.Ordinal))
            {
                canvasIds.Add(canvasId);
            }

            var oldNodes = oldCanvas.Nodes.ToDictionary(static node => node.Id, StringComparer.Ordinal);
            var newNodes = newCanvas.Nodes.ToDictionary(static node => node.Id, StringComparer.Ordinal);
            foreach (var nodeId in oldNodes.Keys.Union(newNodes.Keys).Order(StringComparer.Ordinal))
            {
                if (!oldNodes.TryGetValue(nodeId, out var oldNode))
                {
                    AddNodeScope(newNodes[nodeId], canvasIds, nodeIds, parameterIds, canvasId);
                    continue;
                }

                if (!newNodes.TryGetValue(nodeId, out var newNode))
                {
                    AddNodeScope(oldNode, canvasIds, nodeIds, parameterIds, canvasId);
                    continue;
                }

                if (!string.Equals(Serialize(oldNode), Serialize(newNode), StringComparison.Ordinal))
                {
                    canvasIds.Add(canvasId);
                    nodeIds.Add(nodeId);
                }

                var oldParameters = oldNode.Parameters.ToDictionary(ParameterKey, StringComparer.Ordinal);
                var newParameters = newNode.Parameters.ToDictionary(ParameterKey, StringComparer.Ordinal);
                foreach (var parameterId in oldParameters.Keys.Union(newParameters.Keys).Order(StringComparer.Ordinal))
                {
                    if (!newParameters.TryGetValue(parameterId, out var newParameter)
                        || !oldParameters.TryGetValue(parameterId, out var oldParameter)
                        || !string.Equals(Serialize(oldParameter), Serialize(newParameter), StringComparison.Ordinal))
                    {
                        canvasIds.Add(canvasId);
                        nodeIds.Add(nodeId);
                        parameterIds.Add(parameterId);
                    }
                }
            }

            var oldConnections = oldCanvas.Connections.ToDictionary(static connection => connection.Id, StringComparer.Ordinal);
            var newConnections = newCanvas.Connections.ToDictionary(static connection => connection.Id, StringComparer.Ordinal);
            foreach (var connectionId in oldConnections.Keys.Union(newConnections.Keys).Order(StringComparer.Ordinal))
            {
                if (!newConnections.TryGetValue(connectionId, out var newConnection)
                    || !oldConnections.TryGetValue(connectionId, out var oldConnection)
                    || !string.Equals(Serialize(oldConnection), Serialize(newConnection), StringComparison.Ordinal))
                {
                    canvasIds.Add(canvasId);
                    connectionIds.Add(connectionId);
                }
            }
        }

        return new(
            canvasIds.Order(StringComparer.Ordinal).ToArray(),
            nodeIds.Order(StringComparer.Ordinal).ToArray(),
            connectionIds.Order(StringComparer.Ordinal).ToArray(),
            parameterIds.Order(StringComparer.Ordinal).ToArray());
    }

    private static void AddCanvasScope(
        CanvasDto canvas,
        HashSet<string> canvasIds,
        HashSet<string> nodeIds,
        HashSet<string> connectionIds,
        HashSet<string> parameterIds)
    {
        canvasIds.Add(canvas.Id);
        foreach (var node in canvas.Nodes)
        {
            nodeIds.Add(node.Id);
            foreach (var parameter in node.Parameters)
                parameterIds.Add(ParameterKey(parameter));
        }
        foreach (var connection in canvas.Connections)
            connectionIds.Add(connection.Id);
    }

    private static void AddNodeScope(
        NodeDto node,
        HashSet<string> canvasIds,
        HashSet<string> nodeIds,
        HashSet<string> parameterIds,
        string canvasId)
    {
        canvasIds.Add(canvasId);
        nodeIds.Add(node.Id);
        foreach (var parameter in node.Parameters)
            parameterIds.Add(ParameterKey(parameter));
    }

    private static string ParameterKey(NodeParameterDto parameter)
        => parameter.Ui?.Id ?? parameter.Name;

    private static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value);

    private sealed record ChangeScope(
        IReadOnlyList<string> CanvasIds,
        IReadOnlyList<string> NodeIds,
        IReadOnlyList<string> ConnectionIds,
        IReadOnlyList<string> ParameterIds);
}

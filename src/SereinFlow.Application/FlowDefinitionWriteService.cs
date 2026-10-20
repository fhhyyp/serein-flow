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

public sealed record FlowDefinitionPreparationResult(FlowDefinitionDto Current, FlowDefinitionDto Candidate, FlowValidationResultDto Validation);
public sealed record FlowDefinitionWriteResult(FlowDefinitionWriteStatus Status, FlowDefinitionPreparationResult? Preparation = null, FlowDefinitionDto? Saved = null, long? CurrentVersion = null)
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
    public FlowDefinitionWriteService(IProjectRepository projects, IFlowDefinitionRepository flows, ProjectLibraryService projectLibraries, IWorkspaceChangePublisher? changePublisher = null)
    {
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _flows = flows ?? throw new ArgumentNullException(nameof(flows));
        _projectLibraries = projectLibraries ?? throw new ArgumentNullException(nameof(projectLibraries));
        _changePublisher = changePublisher;
    }

    public async Task<FlowDefinitionPreparationResult?> PrepareAsync(Guid projectId, Guid flowId, FlowDefinitionDto candidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (await _projects.FindAsync(projectId, cancellationToken)is null)
        {
            return null;
        }

        var current = await _flows.FindAsync(projectId, flowId, cancellationToken);
        if (current is null)
        {
            return null;
        }

        var evaluatedValue = FlowDefinitionContractValidator.ValidateForPersistence(candidate).Diagnostics;
        var diagnostics = evaluatedValue.ToList();
        var libraryValidation = await _projectLibraries.ValidateFlowLibrariesAsync(projectId, candidate, cancellationToken);
        diagnostics.AddRange(libraryValidation.Diagnostics);
        var validation = new FlowValidationResultDto(diagnostics.Count == 0, diagnostics);
        var normalized = validation.IsValid ? FlowDefinitionContractNormalizer.NormalizeForPersistence(candidate) : candidate;
        FlowDefinitionPreparationResult evaluatedValue1 = new(current, normalized, validation);
        return evaluatedValue1;
    }

    public async Task<FlowDefinitionWriteResult> WriteAsync(Guid projectId, Guid flowId, FlowDefinitionDto candidate, long expectedVersion, CancellationToken cancellationToken = default, string origin = "web")
    {
        if (candidate.Id != flowId || expectedVersion < 1)
        {
            FlowDefinitionWriteResult evaluatedValue2 = new(FlowDefinitionWriteStatus.Invalid, CurrentVersion: expectedVersion);
            return evaluatedValue2;
        }

        var preparation = await PrepareAsync(projectId, flowId, candidate, cancellationToken);
        if (preparation is null)
        {
            FlowDefinitionWriteResult evaluatedValue3 = new(FlowDefinitionWriteStatus.NotFound);
            return evaluatedValue3;
        }

        if (await _projects.FindAsync(projectId, cancellationToken)is { Status: SereinFlow.Domain.ProjectStatus.Archived })
        {
            FlowDefinitionWriteResult evaluatedValue4 = new(FlowDefinitionWriteStatus.Archived, preparation);
            return evaluatedValue4;
        }

        if (preparation.Current.Version != expectedVersion)
        {
            FlowDefinitionWriteResult evaluatedValue5 = new(FlowDefinitionWriteStatus.Conflict, preparation, CurrentVersion: preparation.Current.Version);
            return evaluatedValue5;
        }

        if (!preparation.Validation.IsValid)
        {
            FlowDefinitionWriteResult evaluatedValue6 = new(FlowDefinitionWriteStatus.Invalid, preparation);
            return evaluatedValue6;
        }

        if (resolveCompareResult().Changes.Count == 0)
        {
            FlowDefinitionWriteResult evaluatedValue7 = new(FlowDefinitionWriteStatus.NoChange, preparation, preparation.Current, expectedVersion);
            return evaluatedValue7;
        }

        var saved = await _flows.TryUpdateAsync(projectId, preparation.Candidate, expectedVersion, cancellationToken);
        if (saved is null)
        {
            var evaluatedValue8 = (await _flows.FindAsync(projectId, flowId, cancellationToken))?.Version;
            FlowDefinitionWriteResult evaluatedValue9 = new(FlowDefinitionWriteStatus.Conflict, preparation, CurrentVersion: evaluatedValue8);
            return evaluatedValue9;
        }

        if (_changePublisher is not null)
        {
            var scope = GetChangeScope(preparation.Current, preparation.Candidate);
            var newGuidResult = Guid.NewGuid();
            var getLibraryIdsResult = ProjectLibraryService.GetLibraryIds(saved);
            var workspaceChangeEventDto = new WorkspaceChangeEventDto(newGuidResult, DateTimeOffset.UtcNow, "flow.changed", projectId, flowId, saved.Version, saved.Checksum, origin, "flow.save", scope.CanvasIds, scope.NodeIds, scope.ConnectionIds, scope.ParameterIds, LibraryIds: getLibraryIdsResult);
            await _changePublisher.PublishAsync(workspaceChangeEventDto, CancellationToken.None);
        }

        FlowDefinitionWriteResult evaluatedValue10 = new(FlowDefinitionWriteStatus.Saved, preparation, saved, saved.Version);
        return evaluatedValue10;
        FlowDiffDto resolveCompareResult()
        {
            var flowDiffService = new FlowDiffService();
            var compareResult = flowDiffService.Compare(preparation.Current, preparation.Candidate);
            return compareResult;
        }
    }

    private static ChangeScope GetChangeScope(FlowDefinitionDto before, FlowDefinitionDto after)
    {
        var canvasIds = new HashSet<string>(StringComparer.Ordinal);
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        var connectionIds = new HashSet<string>(StringComparer.Ordinal);
        var parameterIds = new HashSet<string>(StringComparer.Ordinal);
        var beforeCanvases = before.Canvases.ToDictionary(static canvas => canvas.Id, StringComparer.Ordinal);
        var afterCanvases = after.Canvases.ToDictionary(static canvas => canvas.Id, StringComparer.Ordinal);
        var unionResult22 = beforeCanvases.Keys.Union(afterCanvases.Keys);
        foreach (var canvasId in unionResult22.Order(StringComparer.Ordinal))
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

            if (oldCanvas.Lifecycle != newCanvas.Lifecycle || !string.Equals(oldCanvas.Name, newCanvas.Name, StringComparison.Ordinal))
            {
                canvasIds.Add(canvasId);
            }

            var oldNodes = oldCanvas.Nodes.ToDictionary(static node => node.Id, StringComparer.Ordinal);
            var newNodes = newCanvas.Nodes.ToDictionary(static node => node.Id, StringComparer.Ordinal);
            var unionResult20 = oldNodes.Keys.Union(newNodes.Keys);
            foreach (var nodeId in unionResult20.Order(StringComparer.Ordinal))
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

                if (!resolveEqualsResult12())
                {
                    canvasIds.Add(canvasId);
                    nodeIds.Add(nodeId);
                }

                var oldParameters = oldNode.Parameters.ToDictionary(ParameterKey, StringComparer.Ordinal);
                var newParameters = newNode.Parameters.ToDictionary(ParameterKey, StringComparer.Ordinal);
                var unionResult = oldParameters.Keys.Union(newParameters.Keys);
                foreach (var parameterId in unionResult.Order(StringComparer.Ordinal))
                {
                    if (!newParameters.TryGetValue(parameterId, out var newParameter) || !oldParameters.TryGetValue(parameterId, out var oldParameter) || !resolveEqualsResult())
                    {
                        canvasIds.Add(canvasId);
                        nodeIds.Add(nodeId);
                        parameterIds.Add(parameterId);
                    }

                    bool resolveEqualsResult()
                    {
                        var serializeResult = Serialize(oldParameter);
                        var serializeResult11 = Serialize(newParameter);
                        var equalsResult = string.Equals(serializeResult, serializeResult11, StringComparison.Ordinal);
                        return equalsResult;
                    }
                }

                bool resolveEqualsResult12()
                {
                    var serializeResult13 = Serialize(oldNode);
                    var serializeResult14 = Serialize(newNode);
                    var equalsResult15 = string.Equals(serializeResult13, serializeResult14, StringComparison.Ordinal);
                    return equalsResult15;
                }
            }

            var oldConnections = oldCanvas.Connections.ToDictionary(static connection => connection.Id, StringComparer.Ordinal);
            var newConnections = newCanvas.Connections.ToDictionary(static connection => connection.Id, StringComparer.Ordinal);
            var unionResult21 = oldConnections.Keys.Union(newConnections.Keys);
            foreach (var connectionId in unionResult21.Order(StringComparer.Ordinal))
            {
                if (!newConnections.TryGetValue(connectionId, out var newConnection) || !oldConnections.TryGetValue(connectionId, out var oldConnection) || !resolveEqualsResult16())
                {
                    canvasIds.Add(canvasId);
                    connectionIds.Add(connectionId);
                }

                bool resolveEqualsResult16()
                {
                    var serializeResult17 = Serialize(oldConnection);
                    var serializeResult18 = Serialize(newConnection);
                    var equalsResult19 = string.Equals(serializeResult17, serializeResult18, StringComparison.Ordinal);
                    return equalsResult19;
                }
            }
        }

        var orderResult = canvasIds.Order(StringComparer.Ordinal);
        var toArrayResult = orderResult.ToArray();
        var orderResult23 = nodeIds.Order(StringComparer.Ordinal);
        var toArrayResult24 = orderResult23.ToArray();
        var orderResult25 = connectionIds.Order(StringComparer.Ordinal);
        var toArrayResult26 = orderResult25.ToArray();
        var orderResult27 = parameterIds.Order(StringComparer.Ordinal);
        var toArrayResult28 = orderResult27.ToArray();
        FlowDefinitionWriteService.ChangeScope evaluatedValue29 = new(toArrayResult, toArrayResult24, toArrayResult26, toArrayResult28);
        return evaluatedValue29;
    }

    private static void AddCanvasScope(CanvasDto canvas, HashSet<string> canvasIds, HashSet<string> nodeIds, HashSet<string> connectionIds, HashSet<string> parameterIds)
    {
        canvasIds.Add(canvas.Id);
        foreach (var node in canvas.Nodes)
        {
            nodeIds.Add(node.Id);
            foreach (var parameter in node.Parameters)
            {
                var parameterKeyResult = ParameterKey(parameter);
                parameterIds.Add(parameterKeyResult);
            }
        }

        foreach (var connection in canvas.Connections)
        {
            connectionIds.Add(connection.Id);
        }
    }

    private static void AddNodeScope(NodeDto node, HashSet<string> canvasIds, HashSet<string> nodeIds, HashSet<string> parameterIds, string canvasId)
    {
        canvasIds.Add(canvasId);
        nodeIds.Add(node.Id);
        foreach (var parameter in node.Parameters)
        {
            var parameterKeyResult30 = ParameterKey(parameter);
            parameterIds.Add(parameterKeyResult30);
        }
    }

    private static string ParameterKey(NodeParameterDto parameter) => parameter.Ui?.Id ?? parameter.Name;
    private static string Serialize<T>(T value)
    {
        var serializeResult31 = JsonSerializer.Serialize(value);
        return serializeResult31;
    }

    private sealed record ChangeScope(IReadOnlyList<string> CanvasIds, IReadOnlyList<string> NodeIds, IReadOnlyList<string> ConnectionIds, IReadOnlyList<string> ParameterIds);
}

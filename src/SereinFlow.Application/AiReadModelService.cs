using System;
using System.Collections.Generic;
using System.Text.Json;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application;
/// <summary>
/// Builds stable, bounded read models for MCP Resources and AI analysis.
/// This service intentionally depends only on application persistence ports and
/// never exposes database records or editor-only UI metadata.
/// 为 MCP Resource 和 AI 分析构建稳定且有界的只读模型。该服务只依赖应用层持久化端口，
/// 不暴露数据库记录或仅供编辑器使用的 UI 元数据。
/// </summary>
public sealed class AiReadModelService
{
    private readonly IProjectRepository _projects;
    private readonly IFlowDefinitionRepository _flows;
    private readonly IFlowVersionRepository _versions;
    private readonly ILibraryCatalogService _libraries;
    private readonly IFlowRunStore _runs;
    private readonly IFlowRunEventStore _events;
    private readonly IFlowRunOutputStore _outputs;
    private readonly IFlowDebugSessionStore _debugSessions;
    private readonly IProjectLibraryReferenceRepository? _projectLibraryReferences;
    public AiReadModelService(IProjectRepository projects, IFlowDefinitionRepository flows, IFlowVersionRepository versions, ILibraryCatalogService libraries, IFlowRunStore runs, IFlowRunEventStore events, IFlowRunOutputStore outputs, IFlowDebugSessionStore debugSessions, IProjectLibraryReferenceRepository? projectLibraryReferences = null)
    {
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _flows = flows ?? throw new ArgumentNullException(nameof(flows));
        _versions = versions ?? throw new ArgumentNullException(nameof(versions));
        _libraries = libraries ?? throw new ArgumentNullException(nameof(libraries));
        _runs = runs ?? throw new ArgumentNullException(nameof(runs));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
        _debugSessions = debugSessions ?? throw new ArgumentNullException(nameof(debugSessions));
        _projectLibraryReferences = projectLibraryReferences;
    }

    public async Task<AiPageDto<AiProjectSummaryDto>> ListProjectsAsync(AiReadModelOptions? options = null, CancellationToken cancellationToken = default)
    {
        var listProjectsAsyncResult = await ListProjectsAsync(static project => project.Status != ProjectStatus.Archived, options, cancellationToken);
        return listProjectsAsyncResult;
    }

    public async Task<AiPageDto<AiProjectSummaryDto>> ListArchivedProjectsAsync(AiReadModelOptions? options = null, CancellationToken cancellationToken = default)
    {
        var listProjectsAsyncResult1 = await ListProjectsAsync(static project => project.Status == ProjectStatus.Archived, options, cancellationToken);
        return listProjectsAsyncResult1;
    }

    private async Task<AiPageDto<AiProjectSummaryDto>> ListProjectsAsync(Func<Project, bool> predicate, AiReadModelOptions? options, CancellationToken cancellationToken)
    {
        var evaluatedValue4 = (options ?? new());
        var normalized = evaluatedValue4.Normalize();
        var projects = await _projects.ListAsync(cancellationToken);
        var minResult = Math.Min(normalized.MaxItems, projects.Count);
        var items = new List<AiProjectSummaryDto>(minResult);
        var whereResult = projects.Where(predicate);
        var orderByResult = whereResult.OrderBy(static item => item.Id);
        foreach (var project in orderByResult.Take(normalized.MaxItems + 1))
        {
            var flows = await _flows.ListByProjectAsync(project.Id, cancellationToken);
            var summaries = new List<AiFlowSummaryDto>(flows.Count);
            foreach (var flow in flows.OrderBy(static item => item.Id))
            {
                var findProductionVersionAsyncResult = await _versions.FindProductionVersionAsync(project.Id, flow.Id, cancellationToken);
                var sumResult = flow.Canvases.Sum(static canvas => canvas.Nodes.Count);
                AiFlowSummaryDto evaluatedValue = new(flow.Id, flow.Version, findProductionVersionAsyncResult, flow.EntryNodeId, flow.Canvases.Count, sumResult);
                summaries.Add(evaluatedValue);
            }

            var toStringResult = project.Status.ToString();
            AiProjectSummaryDto evaluatedValue2 = new(project.Id, project.Name, project.Version, toStringResult, project.CreatedAt, project.UpdatedAt, summaries);
            items.Add(evaluatedValue2);
        }

        var createPageResult = CreatePage(items, normalized.MaxItems, static item =>
        {
            var toStringResult3 = item.Id.ToString("D");
            return toStringResult3;
        });
        return createPageResult;
    }

    public async Task<AiProjectSummaryDto?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await _projects.FindAsync(projectId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var flows = await _flows.ListByProjectAsync(projectId, cancellationToken);
        var summaries = new List<AiFlowSummaryDto>(flows.Count);
        foreach (var flow in flows.OrderBy(static item => item.Id))
        {
            var findProductionVersionAsyncResult5 = await _versions.FindProductionVersionAsync(projectId, flow.Id, cancellationToken);
            var sumResult6 = flow.Canvases.Sum(static canvas => canvas.Nodes.Count);
            AiFlowSummaryDto evaluatedValue7 = new(flow.Id, flow.Version, findProductionVersionAsyncResult5, flow.EntryNodeId, flow.Canvases.Count, sumResult6);
            summaries.Add(evaluatedValue7);
        }

        var toStringResult8 = project.Status.ToString();
        AiProjectSummaryDto evaluatedValue9 = new(project.Id, project.Name, project.Version, toStringResult8, project.CreatedAt, project.UpdatedAt, summaries);
        return evaluatedValue9;
    }

    public async Task<AiFlowTopologyDto?> GetFlowTopologyAsync(Guid projectId, Guid flowId, FlowVersionTrackDto track = FlowVersionTrackDto.Development, long? version = null, AiReadModelOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (await _projects.FindAsync(projectId, cancellationToken)is null)
        {
            return null;
        }

        FlowDefinitionDto? definition;
        if (version is not null)
        {
            var detail = await _versions.FindVersionAsync(projectId, flowId, version.Value, cancellationToken);
            if (detail is null || detail.Version.Track != track)
            {
                return null;
            }

            definition = detail.Definition;
        }
        else
        {
            definition = track == FlowVersionTrackDto.Production ? await _versions.FindProductionDefinitionAsync(projectId, flowId, cancellationToken) : await _flows.FindAsync(projectId, flowId, cancellationToken);
        }

        AiFlowTopologyDto? evaluatedValue11 = definition is null ? null : resolveMapTopologyResult();
        return evaluatedValue11;
        AiFlowTopologyDto resolveMapTopologyResult()
        {
            var evaluatedValue10 = (options ?? new());
            var normalizeResult = evaluatedValue10.Normalize();
            var mapTopologyResult = MapTopology(projectId, definition, track, normalizeResult);
            return mapTopologyResult;
        }
    }

    public Task<IReadOnlyList<FlowVersionSummaryDto>> GetFlowVersionHistoryAsync(Guid projectId, Guid flowId, FlowVersionTrackDto track, CancellationToken cancellationToken = default)
    {
        var listVersionsAsyncResult = _versions.ListVersionsAsync(projectId, flowId, track, cancellationToken);
        return listVersionsAsyncResult;
    }

    public async Task<AiFlowEditModelDto?> GetFlowEditModelAsync(Guid projectId, Guid flowId, BuiltinNodeCatalogDto builtinNodes, AiReadModelOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builtinNodes);
        var flow = await GetFlowTopologyAsync(projectId, flowId, FlowVersionTrackDto.Development, null, options, cancellationToken);
        if (flow is null)
        {
            return null;
        }

        var libraries = await _libraries.ListAsync(false, cancellationToken);
        if (_projectLibraryReferences is not null)
        {
            var references = await _projectLibraryReferences.ListByProjectAsync(projectId, cancellationToken);
            var selectResult = references.Select(static item => item.LibraryId);
            var referencedIds = selectResult.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var whereResult12 = libraries.Where(item =>
            {
                var containsResult = referencedIds.Contains(item.Id);
                return containsResult;
            });
            libraries = whereResult12.ToArray();
        }

        var selectResult13 = libraries.Select(static item =>
        {
            var mapLibraryResult = MapLibrary(item, includeNodes: true);
            return mapLibraryResult;
        });
        var toArrayResult = selectResult13.ToArray();
        AiFlowEditModelDto evaluatedValue14 = new(projectId, flowId, flow, builtinNodes.Nodes, toArrayResult);
        return evaluatedValue14;
    }

    public Task<FlowVersionDetailDto?> GetFlowVersionAsync(Guid projectId, Guid flowId, long version, CancellationToken cancellationToken = default)
    {
        var findVersionAsyncResult = _versions.FindVersionAsync(projectId, flowId, version, cancellationToken);
        return findVersionAsyncResult;
    }

    public async Task<AiPageDto<AiLibrarySummaryDto>> ListLibrariesAsync(bool includeArchived = false, AiReadModelOptions? options = null, CancellationToken cancellationToken = default)
    {
        var listLibrariesAsyncResult = await ListLibrariesAsync(includeArchived ? null : LibraryLifecycleDto.Available, includeArchived, options, cancellationToken);
        return listLibrariesAsyncResult;
    }

    public async Task<AiPageDto<AiLibrarySummaryDto>> ListArchivedLibrariesAsync(AiReadModelOptions? options = null, CancellationToken cancellationToken = default)
    {
        var listLibrariesAsyncResult15 = await ListLibrariesAsync(LibraryLifecycleDto.Archived, includeArchived: true, options, cancellationToken);
        return listLibrariesAsyncResult15;
    }

    private async Task<AiPageDto<AiLibrarySummaryDto>> ListLibrariesAsync(LibraryLifecycleDto? lifecycle, bool includeArchived, AiReadModelOptions? options, CancellationToken cancellationToken)
    {
        var evaluatedValue17 = (options ?? new());
        var normalized = evaluatedValue17.Normalize();
        var libraries = await _libraries.ListAsync(includeArchived, cancellationToken);
        var whereResult18 = libraries.Where(item => lifecycle is null || item.Lifecycle == lifecycle);
        var orderByResult19 = whereResult18.OrderBy(static item => item.Id, StringComparer.Ordinal);
        var takeResult = orderByResult19.Take(normalized.MaxItems + 1);
        var selectResult20 = takeResult.Select(static item =>
        {
            var mapLibraryResult16 = MapLibrary(item, includeNodes: false);
            return mapLibraryResult16;
        });
        var items = selectResult20.ToArray();
        var createPageResult21 = CreatePage(items, normalized.MaxItems, static item => item.Id);
        return createPageResult21;
    }

    public async Task<AiLibrarySummaryDto?> GetLibraryAsync(string libraryId, CancellationToken cancellationToken = default)
    {
        var library = await _libraries.FindAsync(libraryId, cancellationToken);
        AiLibrarySummaryDto? evaluatedValue22 = library is null ? null : MapLibrary(library, includeNodes: true);
        return evaluatedValue22;
    }

    public async Task<AiPageDto<AiRunSummaryDto>> ListRunsAsync(Guid? projectId = null, FlowRunStatusDto? status = null, AiReadModelOptions? options = null, CancellationToken cancellationToken = default)
    {
        var evaluatedValue24 = (options ?? new());
        var normalized = evaluatedValue24.Normalize();
        var statuses = status is null ? null : new[]
        {
            (FlowRunStatus)status.Value
        };
        var flowRunQuery = new FlowRunQuery(statuses, projectId, normalized.MaxItems + 1);
        var runs = await _runs.ListAsync(flowRunQuery, cancellationToken);
        var orderByDescendingResult = runs.OrderByDescending(static item => item.CreatedAt);
        var takeResult25 = orderByDescendingResult.Take(normalized.MaxItems + 1);
        var selectResult26 = takeResult25.Select(MapRun);
        var items = selectResult26.ToArray();
        var createPageResult27 = CreatePage(items, normalized.MaxItems, static item =>
        {
            var toStringResult23 = item.Id.ToString("D");
            return toStringResult23;
        });
        return createPageResult27;
    }

    public async Task<AiPageDto<AiDebugSessionSummaryDto>> ListActiveDebugSessionsAsync(Guid? projectId = null, AiReadModelOptions? options = null, CancellationToken cancellationToken = default)
    {
        var evaluatedValue29 = (options ?? new());
        var normalized = evaluatedValue29.Normalize();
        var sessions = await _debugSessions.ListActiveAsync(cancellationToken);
        var whereResult30 = sessions.Where(session => projectId is null || session.ProjectId == projectId.Value);
        var orderByDescendingResult31 = whereResult30.OrderByDescending(static session => session.UpdatedAt);
        var thenByResult = orderByDescendingResult31.ThenBy(static session => session.Id);
        var takeResult32 = thenByResult.Take(normalized.MaxItems + 1);
        var selectResult33 = takeResult32.Select(MapDebugSessionSummary);
        var items = selectResult33.ToArray();
        var createPageResult34 = CreatePage(items, normalized.MaxItems, static item =>
        {
            var toStringResult28 = item.Id.ToString("D");
            return toStringResult28;
        });
        return createPageResult34;
    }

    public async Task<AiRunInspectionDto?> GetRunInspectionAsync(Guid runId, FlowVersionTrackDto? definitionTrack = null, AiReadModelOptions? options = null, long afterEventSequence = 0, CancellationToken cancellationToken = default)
    {
        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        var evaluatedValue35 = (options ?? new());
        var normalized = evaluatedValue35.Normalize();
        var snapshot = await _runs.GetSnapshotAsync(runId, cancellationToken);
        var definition = snapshot is null ? null : MapTopology(run.ProjectId, snapshot, definitionTrack, normalized);
        var events = await GetRunEventsAsync(runId, afterEventSequence, normalized, cancellationToken) ?? EmptyPage<AiRunEventDto>();
        var outputs = await GetRunOutputsAsync(runId, normalized, cancellationToken) ?? EmptyPage<AiNodeExecutionRecordDto>();
        var debug = run.DebugSessionId is null ? null : await GetDebugStateAsync(run.DebugSessionId.Value, cancellationToken);
        var mapRunResult = MapRun(run);
        AiRunInspectionDto evaluatedValue36 = new(mapRunResult, definition, events, outputs, debug);
        return evaluatedValue36;
    }

    public async Task<AiPageDto<AiRunEventDto>?> GetRunEventsAsync(Guid runId, long afterSequence = 0, AiReadModelOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (await _runs.FindAsync(runId, cancellationToken)is null)
        {
            return null;
        }

        var evaluatedValue38 = (options ?? new());
        var normalized = evaluatedValue38.Normalize();
        var evaluatedValue39 = (await _events.GetAfterAsync(runId, afterSequence, cancellationToken));
        var orderByResult40 = evaluatedValue39.OrderBy(static item => item.Sequence);
        var takeResult41 = orderByResult40.Take(normalized.MaxItems + 1);
        var selectResult42 = takeResult41.Select(item =>
        {
            var mapEventResult = MapEvent(item, normalized.MaxJsonBytes);
            return mapEventResult;
        });
        var events = selectResult42.ToArray();
        var createPageResult43 = CreatePage(events, normalized.MaxItems, static item =>
        {
            var toStringResult37 = item.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return toStringResult37;
        });
        return createPageResult43;
    }

    public async Task<AiPageDto<AiNodeExecutionRecordDto>?> GetRunOutputsAsync(Guid runId, AiReadModelOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (await _runs.FindAsync(runId, cancellationToken)is null)
        {
            return null;
        }

        var evaluatedValue45 = (options ?? new());
        var normalized = evaluatedValue45.Normalize();
        var evaluatedValue46 = (await _outputs.ListAsync(runId, cancellationToken));
        var orderByResult47 = evaluatedValue46.OrderBy(static item => item.Sequence);
        var takeResult48 = orderByResult47.Take(normalized.MaxItems + 1);
        var selectResult49 = takeResult48.Select(item =>
        {
            var mapOutputResult = MapOutput(item, normalized.MaxJsonBytes);
            return mapOutputResult;
        });
        var outputs = selectResult49.ToArray();
        var createPageResult50 = CreatePage(outputs, normalized.MaxItems, static item =>
        {
            var toStringResult44 = item.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return toStringResult44;
        });
        return createPageResult50;
    }

    public async Task<AiDebugStateDto?> GetDebugStateAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _debugSessions.FindAsync(sessionId, cancellationToken);
        AiDebugStateDto? evaluatedValue51 = session is null ? null : MapDebugState(session);
        return evaluatedValue51;
    }

    public async Task<AiDebugStateWaitResultDto?> WaitForDebugStateChangeAsync(Guid sessionId, long afterRevision, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var result = await FlowDebugSessionWaiter.WaitAsync(_debugSessions, sessionId, afterRevision, timeout, cancellationToken);
        AiDebugStateWaitResultDto? evaluatedValue53 = result.Session is null ? null : resolveevaluatedValue();
        return evaluatedValue53;
        AiDebugStateWaitResultDto resolveevaluatedValue()
        {
            var mapDebugStateResult = MapDebugState(result.Session);
            AiDebugStateWaitResultDto evaluatedValue52 = new(result.HasChanged, result.TimedOut, mapDebugStateResult);
            return evaluatedValue52;
        }
    }

    private static AiFlowTopologyDto MapTopology(Guid projectId, FlowDefinitionDto definition, FlowVersionTrackDto? track, AiReadModelOptions options)
    {
        var evaluatedValue64 = definition.RunPolicy?.ConcurrencyMode.ToString() ?? "Unknown";
        var selectResult65 = definition.Canvases.Select(canvas =>
        {
            var takeResult54 = canvas.Nodes.Take(options.MaxItems + 1);
            var nodes = takeResult54.ToArray();
            var takeResult55 = canvas.Connections.Take(options.MaxItems + 1);
            var connections = takeResult55.ToArray();
            var whereResult56 = canvas.Connections.Where(static connection => connection.Kind == ConnectionKindDto.Data);
            var dataConnections = whereResult56.ToArray();
            var truncated = nodes.Length > options.MaxItems || connections.Length > options.MaxItems;
            var toStringResult57 = canvas.Lifecycle.ToString();
            var takeResult58 = nodes.Take(options.MaxItems);
            var selectResult59 = takeResult58.Select(node =>
            {
                var mapNodeResult = MapNode(canvas.Id, node, dataConnections, options);
                return mapNodeResult;
            });
            var toArrayResult60 = selectResult59.ToArray();
            var takeResult61 = connections.Take(options.MaxItems);
            var selectResult62 = takeResult61.Select(MapConnection);
            var toArrayResult63 = selectResult62.ToArray();
            var aiCanvasDto = new AiCanvasDto(canvas.Id, toStringResult57, canvas.Name, toArrayResult60, toArrayResult63, truncated);
            return aiCanvasDto;
        });
        var toArrayResult66 = selectResult65.ToArray();
        AiFlowTopologyDto evaluatedValue67 = new(projectId, definition.Id, track, definition.Version, definition.SchemaVersion, definition.EntryNodeId, definition.Checksum, evaluatedValue64, toArrayResult66);
        return evaluatedValue67;
    }

    private static AiNodeContractDto MapNode(string canvasId, NodeDto node, IReadOnlyList<ConnectionDto> dataConnections, AiReadModelOptions options)
    {
        var toStringResult68 = node.Type.ToString();
        var selectResult69 = node.Ports.Select(port =>
        {
            var aiPortContractDto = new AiPortContractDto(port.Id, port.Name, port.Direction, port.Required);
            return aiPortContractDto;
        });
        var toArrayResult70 = selectResult69.ToArray();
        var selectResult71 = node.Parameters.Select(parameter =>
        {
            var firstOrDefaultResult = dataConnections.FirstOrDefault(connection => connection.ToNodeId == node.Id && connection.ToPortId == (parameter.Ui?.Id ?? parameter.Name));
            var mapParameterResult = MapParameter(parameter, options, firstOrDefaultResult);
            return mapParameterResult;
        });
        var toArrayResult72 = selectResult71.ToArray();
        AiNodeRuntimeDto conditionalValue;
        if (node.Ui is null)
        {
            conditionalValue = null;
        }
        else
        {
            Guid? evaluatedValue1 = Guid.TryParse(node.Ui.TargetFlowId, out var targetFlowId) ? targetFlowId : null;
            conditionalValue = new AiNodeRuntimeDto(node.Ui.LibraryId, node.Ui.LibraryNodeContractId, node.Ui.FlowLibraryName, node.Ui.ClassName, node.Ui.MethodName, node.Ui.DllName, node.Ui.DllVersion, node.Ui.ReturnType, node.Ui.IsAwaitable ?? false, evaluatedValue1, node.Ui.TargetNodeId, node.Ui.TargetCanvasId, node.Ui.IsPublic ?? false);
        }

        AiNodeRuntimeDto? evaluatedValue73 = conditionalValue;
        AiScriptContractDto? evaluatedValue79 = node.Script is null ? null : resolveAiScriptContractDto();
        AiNodeContractDto evaluatedValue80 = new(node.Id, toStringResult68, node.DisplayName, canvasId, toArrayResult70, toArrayResult72, evaluatedValue73, evaluatedValue79, node.X, node.Y);
        return evaluatedValue80;
        AiScriptContractDto resolveAiScriptContractDto()
        {
            string? evaluatedValue74 = options.IncludeScriptSource ? LimitText(node.Script.Source, options.MaxJsonBytes) : null;
            var selectResult75 = node.Script.Inputs.Select(MapScriptValue);
            var toArrayResult76 = selectResult75.ToArray();
            var selectResult77 = node.Script.Outputs.Select(MapScriptValue);
            var toArrayResult78 = selectResult77.ToArray();
            var aiScriptContractDto = new AiScriptContractDto(node.Script.NodeId, node.Script.LanguageVersion, node.Script.SourceHash, evaluatedValue74, toArrayResult76, toArrayResult78);
            return aiScriptContractDto;
        }
    }

    private static AiParameterContractDto MapParameter(NodeParameterDto parameter, AiReadModelOptions options, ConnectionDto? dataConnection = null)
    {
        // Runtime data connections are authoritative. This fallback keeps
        // topology and run-snapshot diagnostics accurate for older flows that
        // persisted the connection but left the parameter source as literal.
        // 运行时数据连接优先；兼容旧流程中“连接已保存但参数来源仍为字面量”的诊断展示。
        var source = dataConnection is null ? parameter.Source : DataSourceDto.PreviousNode;
        var sourceNodeId = dataConnection?.FromNodeId ?? parameter.Ui?.SourceNodeId;
        var sourcePortId = dataConnection?.FromPortId ?? parameter.Ui?.SourcePortId;
        var toStringResult81 = source.ToString();
        var evaluatedValue82 = !string.IsNullOrWhiteSpace(parameter.ValueJson) || parameter.Ui?.ProjectInputKey is not null || parameter.Ui?.Expression is not null || sourceNodeId is not null;
        string? evaluatedValue83 = options.IncludeFlowLiteralValues && source == DataSourceDto.Literal ? LimitText(parameter.ValueJson, options.MaxJsonBytes) : null;
        AiParameterContractDto evaluatedValue84 = new(parameter.Ui?.Id ?? parameter.Name, parameter.Name, parameter.Ui?.Type ?? parameter.Ui?.ValueKind, toStringResult81, parameter.Required, evaluatedValue82, evaluatedValue83, options.IncludeFlowLiteralValues ? parameter.Ui?.ProjectInputKey : null, options.IncludeFlowLiteralValues ? parameter.Ui?.Expression : null, sourceNodeId, sourcePortId, parameter.Ui?.IsVariadic ?? false, parameter.Ui?.VariadicGroupId, parameter.Ui?.ElementType, parameter.Ui?.Description, parameter.Ui?.EnumMetadata);
        return evaluatedValue84;
    }

    private static AiScriptValueContractDto MapScriptValue(ScriptValueContractDto value)
    {
        AiScriptValueContractDto evaluatedValue85 = new(value.Id ?? value.Name, value.Name, value.ValueKind, value.Required, value.Description);
        return evaluatedValue85;
    }

    private static AiConnectionDto MapConnection(ConnectionDto connection)
    {
        var toStringResult86 = connection.Kind.ToString();
        var evaluatedValue87 = connection.Branch?.ToString();
        string? evaluatedValue88 = connection.Kind == ConnectionKindDto.Data ? (connection.DataSource ?? DataSourceDto.PreviousNode).ToString() : connection.DataSource?.ToString();
        AiConnectionDto evaluatedValue89 = new(connection.Id, connection.FromNodeId, connection.FromPortId, connection.ToNodeId, connection.ToPortId, toStringResult86, evaluatedValue87, evaluatedValue88, connection.Priority);
        return evaluatedValue89;
    }

    private static AiLibrarySummaryDto MapLibrary(LibraryDto library, bool includeNodes)
    {
        var toStringResult93 = library.Lifecycle.ToString();
        IReadOnlyList<AiLibraryNodeContractDto>? evaluatedValue96 = includeNodes ? resolveToArrayResult() : null;
        AiLibrarySummaryDto evaluatedValue97 = new(library.Id, library.Name, library.Version, library.SemanticVersion, library.Sha256, toStringResult93, library.FamilyId, library.FamilyName, library.Nodes.Count, evaluatedValue96);
        return evaluatedValue97;
        AiLibraryNodeContractDto[] resolveToArrayResult()
        {
            var selectResult94 = library.Nodes.Select(node =>
            {
                var toStringResult90 = node.Type.ToString();
                var selectResult91 = node.Parameters.Select(parameter =>
                {
                    var aiLibraryParameterContractDto = new AiLibraryParameterContractDto(parameter.Id, parameter.Name, parameter.Type, parameter.Description, parameter.Required, parameter.IsVariadic, parameter.ElementType, parameter.DefaultValue, parameter.Aliases, parameter.IdentityConfidence, parameter.EnumMetadata);
                    return aiLibraryParameterContractDto;
                });
                var toArrayResult92 = selectResult91.ToArray();
                var aiLibraryNodeContractDto = new AiLibraryNodeContractDto(node.Id, node.ContractId ?? node.Id, toStringResult90, node.DisplayName, node.Description, node.LibraryId, node.ClassName, node.MethodName, node.DllName, node.DllVersion, node.ReturnType, node.IsAwaitable, node.FlowLibraryName, toArrayResult92, node.IdentityConfidence);
                return aiLibraryNodeContractDto;
            });
            var toArrayResult95 = selectResult94.ToArray();
            return toArrayResult95;
        }
    }

    private static AiRunSummaryDto MapRun(FlowRun run)
    {
        var toStringResult98 = run.Status.ToString();
        var toStringResult99 = run.ExecutionKind.ToString();
        AiRunSummaryDto evaluatedValue100 = new(run.Id, run.ProjectId, run.FlowId, run.FlowVersion, toStringResult98, toStringResult99, run.IsListenerRun, run.CreatedAt, run.StartedAt, run.EndedAt, run.ErrorSummary, run.CancellationReason, run.DebugSessionId, run.DefinitionChecksum);
        return evaluatedValue100;
    }

    private static AiRunEventDto MapEvent(FlowRunEvent item, int maxJsonBytes)
    {
        var deconstructedValues = ParsePayload(item.PayloadJson, maxJsonBytes);
        var payload = deconstructedValues.Item1;
        var malformed = deconstructedValues.Item2;
        var truncated = deconstructedValues.Item3;
        AiRunEventDto evaluatedValue101 = new(item.RunId, item.Sequence, item.Timestamp, item.Type, item.NodeId, payload, malformed, truncated);
        return evaluatedValue101;
    }

    private static AiNodeExecutionRecordDto MapOutput(FlowRunOutput item, int maxJsonBytes)
    {
        var deconstructedValues102 = ParsePayload(item.InputsJson, maxJsonBytes);
        var inputs = deconstructedValues102.Item1;
        var inputsMalformed = deconstructedValues102.Item2;
        var inputsTruncated = deconstructedValues102.Item3;
        var deconstructedValues103 = ParsePayload(item.OutputsJson, maxJsonBytes);
        var outputs = deconstructedValues103.Item1;
        var outputsMalformed = deconstructedValues103.Item2;
        var outputsTruncated = deconstructedValues103.Item3;
        AiNodeExecutionRecordDto evaluatedValue104 = new(item.RunId, item.Sequence, item.Timestamp, item.NodeId, item.Outcome, item.Branch, inputs, outputs, item.ErrorCode, item.ErrorMessage, inputsMalformed, outputsMalformed, inputsTruncated, outputsTruncated);
        return evaluatedValue104;
    }

    private static AiDebugStateDto MapDebugState(FlowDebugSession session)
    {
        var toStringResult105 = session.Status.ToString();
        AiDebugPauseStateDto? evaluatedValue107 = session.PauseState is null ? null : resolveAiDebugPauseStateDto();
        AiDebugNodeResultDto? evaluatedValue110 = session.LastNodeResult is null ? null : resolveAiDebugNodeResultDto();
        AiDebugStateDto evaluatedValue111 = new(session.Id, session.RunId, session.ProjectId, session.FlowId, toStringResult105, session.BreakpointNodeIds, session.CurrentNodeId, session.ActiveInvocationId, session.ActiveFlipflopNodeId, session.QueuedTriggerCount, session.LastCommandSequence, session.FailureMessage, session.CreatedAt, session.UpdatedAt, session.StateRevision, evaluatedValue107, evaluatedValue110);
        return evaluatedValue111;
        AiDebugPauseStateDto resolveAiDebugPauseStateDto()
        {
            var evaluatedValue106 = ParsePayload(session.PauseState.InputsJson, 64 * 1024).Payload;
            var aiDebugPauseStateDto = new AiDebugPauseStateDto(session.PauseState.NodeId, session.PauseState.NodeType, session.PauseState.Step, session.PauseState.FrameDepth, session.PauseState.InvocationId, session.PauseState.BoundarySequence, evaluatedValue106, session.PauseState.PausedAt);
            return aiDebugPauseStateDto;
        }

        AiDebugNodeResultDto resolveAiDebugNodeResultDto()
        {
            var evaluatedValue108 = ParsePayload(session.LastNodeResult.InputsJson, 64 * 1024).Payload;
            var evaluatedValue109 = ParsePayload(session.LastNodeResult.OutputsJson, 64 * 1024).Payload;
            var aiDebugNodeResultDto = new AiDebugNodeResultDto(session.LastNodeResult.NodeId, session.LastNodeResult.Sequence, session.LastNodeResult.CompletedAt, session.LastNodeResult.Outcome, session.LastNodeResult.Branch, evaluatedValue108, evaluatedValue109, session.LastNodeResult.ErrorCode, session.LastNodeResult.ErrorMessage);
            return aiDebugNodeResultDto;
        }
    }

    private static AiDebugSessionSummaryDto MapDebugSessionSummary(FlowDebugSession session)
    {
        var toStringResult112 = session.Status.ToString();
        AiDebugSessionSummaryDto evaluatedValue113 = new(session.Id, session.RunId, session.ProjectId, session.FlowId, toStringResult112, session.BreakpointNodeIds, session.CurrentNodeId, session.ActiveInvocationId, session.ActiveFlipflopNodeId, session.QueuedTriggerCount, session.LastCommandSequence, session.StateRevision, session.CreatedAt, session.UpdatedAt);
        return evaluatedValue113;
    }

    private static (JsonElement Payload, bool Malformed, bool Truncated) ParsePayload(string? json, int maxJsonBytes)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            var evaluatedValue114 = (JsonSerializer.SerializeToElement(new { }), false, false);
            return evaluatedValue114;
        }

        var bounded = LimitText(json, maxJsonBytes);
        if (!string.Equals(bounded, json, StringComparison.Ordinal))
        {
            var evaluatedValue115 = (JsonSerializer.SerializeToElement(new { truncated = true, rawLength = json.Length, value = bounded }), false, true);
            return evaluatedValue115;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var evaluatedValue116 = (document.RootElement.Clone(), false, false);
            return evaluatedValue116;
        }
        catch (JsonException)
        {
            var evaluatedValue117 = (JsonSerializer.SerializeToElement(new { malformed = true, raw = bounded }), true, false);
            return evaluatedValue117;
        }
    }

    private static string? LimitText(string? value, int maxBytes)
    {
        if (value is null)
        {
            return null;
        }

        if (System.Text.Encoding.UTF8.GetByteCount(value) <= maxBytes)
        {
            return value;
        }

        var length = Math.Min(value.Length, maxBytes);
        while (true)
        {
            if (!(length > 0 && resolveGetByteCountResult() > maxBytes))
            {
                break;
            }

            length--;
            int resolveGetByteCountResult()
            {
                var asSpanResult = value.AsSpan(0, length);
                var getByteCountResult = System.Text.Encoding.UTF8.GetByteCount(asSpanResult);
                return getByteCountResult;
            }
        }

        return value[..length];
    }

    private static AiPageDto<T> CreatePage<T>(IReadOnlyList<T> items, int take, Func<T, string> cursor)
    {
        var hasMore = items.Count > take;
        var visible = hasMore ? resolveToArrayResult118() : items.ToArray();
        string? evaluatedValue121 = hasMore && visible.Length > 0 ? cursor(visible[^1]) : null;
        AiPageDto<T> evaluatedValue122 = new(AiReadModelContract.SchemaVersion, visible, hasMore, evaluatedValue121);
        return evaluatedValue122;
        T[] resolveToArrayResult118()
        {
            var takeResult119 = items.Take(take);
            var toArrayResult120 = takeResult119.ToArray();
            return toArrayResult120;
        }
    }

    private static AiPageDto<T> EmptyPage<T>()
    {
        AiPageDto<T> evaluatedValue123 = new(AiReadModelContract.SchemaVersion, [], false, null);
        return evaluatedValue123;
    }
}

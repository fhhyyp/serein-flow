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

    public async Task<AiPageDto<AiProjectSummaryDto>> ListProjectsAsync(AiReadModelOptions? options = null, CancellationToken cancellationToken = default, ProjectAccessScope? accessScope = null)
    {
        return await ListProjectsAsync(project =>
        {
            return project.Status != ProjectStatus.Archived && (accessScope == null || accessScope.Contains(project.Id));
        }, options, cancellationToken);
    }

    public async Task<AiPageDto<AiProjectSummaryDto>> ListArchivedProjectsAsync(AiReadModelOptions? options = null, CancellationToken cancellationToken = default, ProjectAccessScope? accessScope = null)
    {
        return await ListProjectsAsync(project =>
        {
            return project.Status == ProjectStatus.Archived && (accessScope == null || accessScope.Contains(project.Id));
        }, options, cancellationToken);
    }

    private async Task<AiPageDto<AiProjectSummaryDto>> ListProjectsAsync(Func<Project, bool> predicate, AiReadModelOptions? options, CancellationToken cancellationToken)
    {
        var normalized = ((options ?? new())).Normalize();
        var projects = await _projects.ListAsync(cancellationToken);
        var items = new List<AiProjectSummaryDto>(Math.Min(normalized.MaxItems, projects.Count));
        foreach (var project in projects
            .Where(predicate)
            .OrderBy(static item => item.Id)
            .Take(normalized.MaxItems + 1))
        {
            var flows = await _flows.ListByProjectAsync(project.Id, cancellationToken);
            var summaries = new List<AiFlowSummaryDto>(flows.Count);
            foreach (var flow in flows.OrderBy(static item => item.Id))
            {
                var productionVersion = await _versions.FindProductionVersionAsync(project.Id, flow.Id, cancellationToken);
                var nodeCount = flow.Canvases.Sum(static canvas => canvas.Nodes.Count);
                AiFlowSummaryDto aiFlowSummaryDto = new(flow.Id, flow.Version, productionVersion, flow.EntryNodeId, flow.Canvases.Count, nodeCount);
                summaries.Add(aiFlowSummaryDto);
            }

            var status = project.Status.ToString();
            AiProjectSummaryDto aiProjectSummaryDto = new(project.Id, project.Name, project.Version, status, project.CreatedAt, project.UpdatedAt, summaries);
            items.Add(aiProjectSummaryDto);
        }
        return CreatePage(items, normalized.MaxItems, static item =>
        {
            return item.Id.ToString("D");
        });
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
            var productionVersion = await _versions.FindProductionVersionAsync(projectId, flow.Id, cancellationToken);
            var nodeCount = flow.Canvases.Sum(static canvas => canvas.Nodes.Count);
            AiFlowSummaryDto aiFlowSummaryDto = new(flow.Id, flow.Version, productionVersion, flow.EntryNodeId, flow.Canvases.Count, nodeCount);
            summaries.Add(aiFlowSummaryDto);
        }

        var status = project.Status.ToString();
        return new(project.Id, project.Name, project.Version, status, project.CreatedAt, project.UpdatedAt, summaries);
    }

    public async Task<AiFlowTopologyDto?> GetFlowTopologyAsync(Guid projectId, Guid flowId, FlowVersionTrackDto track = FlowVersionTrackDto.Development, long? version = null, AiReadModelOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (await _projects.FindAsync(projectId, cancellationToken) is null)
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
        return definition is null ? null : MapRequestedTopology();
        AiFlowTopologyDto MapRequestedTopology()
        {
            var normalizedOptions = ((options ?? new())).Normalize();
            return MapTopology(projectId, definition, track, normalizedOptions);
        }
    }

    public Task<IReadOnlyList<FlowVersionSummaryDto>> GetFlowVersionHistoryAsync(Guid projectId, Guid flowId, FlowVersionTrackDto track, CancellationToken cancellationToken = default)
    {
        return _versions.ListVersionsAsync(projectId, flowId, track, cancellationToken);
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
            var referencedIds = (await _projectLibraryReferences.ListByProjectAsync(projectId, cancellationToken)).Select(static item => item.LibraryId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            libraries = libraries.Where(item =>
            {
                return referencedIds.Contains(item.Id);
            }).ToArray();
        }

        var resolvedLibraries = libraries.Select(static item =>
        {
            return MapLibrary(item, includeNodes: true);
        }).ToArray();
        return new(projectId, flowId, flow, builtinNodes.Nodes, resolvedLibraries);
    }

    public Task<FlowVersionDetailDto?> GetFlowVersionAsync(Guid projectId, Guid flowId, long version, CancellationToken cancellationToken = default)
    {
        return _versions.FindVersionAsync(projectId, flowId, version, cancellationToken);
    }

    public async Task<AiPageDto<AiLibrarySummaryDto>> ListLibrariesAsync(bool includeArchived = false, AiReadModelOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await ListLibrariesAsync(includeArchived ? null : LibraryLifecycleDto.Available, includeArchived, options, cancellationToken);
    }

    public async Task<AiPageDto<AiLibrarySummaryDto>> ListArchivedLibrariesAsync(AiReadModelOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await ListLibrariesAsync(LibraryLifecycleDto.Archived, includeArchived: true, options, cancellationToken);
    }

    private async Task<AiPageDto<AiLibrarySummaryDto>> ListLibrariesAsync(LibraryLifecycleDto? lifecycle, bool includeArchived, AiReadModelOptions? options, CancellationToken cancellationToken)
    {
        var normalized = ((options ?? new())).Normalize();
        var items = (await _libraries.ListAsync(includeArchived, cancellationToken))
            .Where(item => lifecycle is null || item.Lifecycle == lifecycle)
            .OrderBy(static item => item.Id, StringComparer.Ordinal)
            .Take(normalized.MaxItems + 1)
            .Select(static item =>
        {
            return MapLibrary(item, includeNodes: false);
        })
            .ToArray();
        return CreatePage(items, normalized.MaxItems, static item => item.Id);
    }

    public async Task<AiLibrarySummaryDto?> GetLibraryAsync(string libraryId, CancellationToken cancellationToken = default)
    {
        var library = await _libraries.FindAsync(libraryId, cancellationToken);
        return library is null ? null : MapLibrary(library, includeNodes: true);
    }

    public async Task<AiPageDto<AiRunSummaryDto>> ListRunsAsync(Guid? projectId = null, FlowRunStatusDto? status = null, AiReadModelOptions? options = null, CancellationToken cancellationToken = default, ProjectAccessScope? accessScope = null)
    {
        var normalized = ((options ?? new())).Normalize();
        var statuses = status is null ? null : new[]
        {
            (FlowRunStatus)status.Value
        };
        var projectIds = accessScope is { AllProjects: false } ? accessScope.ProjectIds.ToArray() : null;
        var flowRunQuery = new FlowRunQuery(statuses, projectId, normalized.MaxItems + 1, projectIds);
        var items = (await _runs.ListAsync(flowRunQuery, cancellationToken))
            .OrderByDescending(static item => item.CreatedAt)
            .Take(normalized.MaxItems + 1)
            .Select(MapRun)
            .ToArray();
        return CreatePage(items, normalized.MaxItems, static item =>
        {
            return item.Id.ToString("D");
        });
    }

    public async Task<AiPageDto<AiDebugSessionSummaryDto>> ListActiveDebugSessionsAsync(Guid? projectId = null, AiReadModelOptions? options = null, CancellationToken cancellationToken = default, ProjectAccessScope? accessScope = null)
    {
        var normalized = ((options ?? new())).Normalize();
        var items = (await _debugSessions.ListActiveAsync(cancellationToken))
            .Where(session =>
        {
            return (projectId is null || session.ProjectId == projectId.Value) && (accessScope == null || accessScope.Contains(session.ProjectId));
        })
            .OrderByDescending(static session => session.UpdatedAt)
            .ThenBy(static session => session.Id)
            .Take(normalized.MaxItems + 1)
            .Select(MapDebugSessionSummary)
            .ToArray();
        return CreatePage(items, normalized.MaxItems, static item =>
        {
            return item.Id.ToString("D");
        });
    }

    public async Task<AiRunInspectionDto?> GetRunInspectionAsync(Guid runId, FlowVersionTrackDto? definitionTrack = null, AiReadModelOptions? options = null, long afterEventSequence = 0, CancellationToken cancellationToken = default)
    {
        var run = await _runs.FindAsync(runId, cancellationToken);
        if (run is null)
        {
            return null;
        }
        var normalized = ((options ?? new())).Normalize();
        var snapshot = await _runs.GetSnapshotAsync(runId, cancellationToken);
        var definition = snapshot is null ? null : MapTopology(run.ProjectId, snapshot, definitionTrack, normalized);
        var events = await GetRunEventsAsync(runId, afterEventSequence, normalized, cancellationToken) ?? EmptyPage<AiRunEventDto>();
        var outputs = await GetRunOutputsAsync(runId, normalized, cancellationToken) ?? EmptyPage<AiNodeExecutionRecordDto>();
        var debug = run.DebugSessionId is null ? null : await GetDebugStateAsync(run.DebugSessionId.Value, cancellationToken);
        var aiRunSummaryDto = MapRun(run);
        return new(aiRunSummaryDto, definition, events, outputs, debug);
    }

    public async Task<AiPageDto<AiRunEventDto>?> GetRunEventsAsync(Guid runId, long afterSequence = 0, AiReadModelOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (await _runs.FindAsync(runId, cancellationToken) is null)
        {
            return null;
        }
        var normalized = ((options ?? new())).Normalize();
        var events = (await _events.GetAfterAsync(runId, afterSequence, cancellationToken))
            .OrderBy(static item => item.Sequence)
            .Take(normalized.MaxItems + 1)
            .Select(item =>
        {
            return MapEvent(item, normalized.MaxJsonBytes);
        })
            .ToArray();
        return CreatePage(events, normalized.MaxItems, static item =>
        {
            return item.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
        });
    }

    public async Task<AiPageDto<AiNodeExecutionRecordDto>?> GetRunOutputsAsync(Guid runId, AiReadModelOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (await _runs.FindAsync(runId, cancellationToken) is null)
        {
            return null;
        }
        var normalized = ((options ?? new())).Normalize();
        var outputs = (await _outputs.ListAsync(runId, cancellationToken))
            .OrderBy(static item => item.Sequence)
            .Take(normalized.MaxItems + 1)
            .Select(item =>
        {
            return MapOutput(item, normalized.MaxJsonBytes);
        })
            .ToArray();
        return CreatePage(outputs, normalized.MaxItems, static item =>
        {
            return item.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
        });
    }

    public async Task<AiDebugStateDto?> GetDebugStateAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _debugSessions.FindAsync(sessionId, cancellationToken);
        return session is null ? null : MapDebugState(session);
    }

    public async Task<AiDebugStateWaitResultDto?> WaitForDebugStateChangeAsync(Guid sessionId, long afterRevision, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var result = await FlowDebugSessionWaiter.WaitAsync(_debugSessions, sessionId, afterRevision, timeout, waitForPauseOrCompletion: true, cancellationToken: cancellationToken);
        return result.Session is null ? null : MapWaitResult();
        AiDebugStateWaitResultDto MapWaitResult()
        {
            var state = MapDebugState(result.Session);
            return new(result.HasChanged, result.TimedOut, state);
        }
    }

    private static AiFlowTopologyDto MapTopology(Guid projectId, FlowDefinitionDto definition, FlowVersionTrackDto? track, AiReadModelOptions options)
    {
        var concurrencyMode = definition.RunPolicy?.ConcurrencyMode.ToString() ?? "Unknown";
        var canvases = definition.Canvases.Select(canvas =>
        {
            var nodes = canvas.Nodes.Take(options.MaxItems + 1).ToArray();
            var connections = canvas.Connections.Take(options.MaxItems + 1).ToArray();
            var dataConnections = canvas.Connections.Where(static connection => connection.Kind == ConnectionKindDto.Data).ToArray();
            var truncated = nodes.Length > options.MaxItems || connections.Length > options.MaxItems;
            var lifecycle = canvas.Lifecycle.ToString();
            var take = nodes
                .Take(options.MaxItems)
                .Select(node =>
            {
                return MapNode(canvas.Id, node, dataConnections, options);
            })
                .ToArray();
            var mappedConnections = connections
                .Take(options.MaxItems)
                .Select(MapConnection)
                .ToArray();
            return new AiCanvasDto(canvas.Id, lifecycle, canvas.Name, take, mappedConnections, truncated);
        }).ToArray();
        return new(projectId, definition.Id, track, definition.Version, definition.SchemaVersion, definition.EntryNodeId, definition.Checksum, concurrencyMode, canvases);
    }

    private static AiNodeContractDto MapNode(string canvasId, NodeDto node, IReadOnlyList<ConnectionDto> dataConnections, AiReadModelOptions options)
    {
        var type = node.Type.ToString();
        var ports = node.Ports.Select(port =>
        {
            return new AiPortContractDto(port.Id, port.Name, port.Direction, port.Required);
        }).ToArray();
        var parameters = node.Parameters.Select(parameter =>
        {
            var dataConnection = dataConnections.FirstOrDefault(connection => connection.ToNodeId == node.Id && connection.ToPortId == (parameter.Ui?.Id ?? parameter.Name));
            return MapParameter(parameter, options, dataConnection);
        }).ToArray();
        AiNodeRuntimeDto? runtime;
        if (node.Ui is null)
        {
            runtime = null;
        }
        else
        {
            Guid? parsedTargetFlowId = Guid.TryParse(node.Ui.TargetFlowId, out var targetFlowId) ? targetFlowId : null;
            runtime = new AiNodeRuntimeDto(node.Ui.LibraryId, node.Ui.LibraryNodeContractId, node.Ui.FlowLibraryName, node.Ui.ClassName, node.Ui.MethodName, node.Ui.DllName, node.Ui.DllVersion, node.Ui.ReturnType, node.Ui.IsAwaitable ?? false, parsedTargetFlowId, node.Ui.TargetNodeId, node.Ui.TargetCanvasId, node.Ui.IsPublic ?? false);
        }

        AiNodeRuntimeDto? aiNodeRuntimeDto = runtime;
        AiScriptContractDto? script = node.Script is null ? null : MapScriptContract();
        return new(node.Id, type, node.DisplayName, canvasId, ports, parameters, aiNodeRuntimeDto, script, node.X, node.Y);
        AiScriptContractDto MapScriptContract()
        {
            string? limitText = options.IncludeScriptSource ? LimitText(node.Script.Source, options.MaxJsonBytes) : null;
            var inputs = node.Script.Inputs.Select(MapScriptValue).ToArray();
            var outputs = node.Script.Outputs.Select(MapScriptValue).ToArray();
            return new AiScriptContractDto(node.Script.NodeId, node.Script.LanguageVersion, node.Script.SourceHash, limitText, inputs, outputs);
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
        var sourceText = source.ToString();
        var isConfigured = !string.IsNullOrWhiteSpace(parameter.ValueJson) || parameter.Ui?.ProjectInputKey is not null || parameter.Ui?.Expression is not null || sourceNodeId is not null;
        string? valueJson = options.IncludeFlowLiteralValues && source == DataSourceDto.Literal ? LimitText(parameter.ValueJson, options.MaxJsonBytes) : null;
        return new(parameter.Ui?.Id ?? parameter.Name, parameter.Name, parameter.Ui?.Type ?? parameter.Ui?.ValueKind, sourceText, parameter.Required, isConfigured, valueJson, options.IncludeFlowLiteralValues ? parameter.Ui?.ProjectInputKey : null, options.IncludeFlowLiteralValues ? parameter.Ui?.Expression : null, sourceNodeId, sourcePortId, parameter.Ui?.IsVariadic ?? false, parameter.Ui?.VariadicGroupId, parameter.Ui?.ElementType, parameter.Ui?.Description, parameter.Ui?.EnumMetadata);
    }

    private static AiScriptValueContractDto MapScriptValue(ScriptValueContractDto value)
    {
        return new(value.Id ?? value.Name, value.Name, value.ValueKind, value.Required, value.Description);
    }

    private static AiConnectionDto MapConnection(ConnectionDto connection)
    {
        var kind = connection.Kind.ToString();
        var branch = connection.Branch?.ToString();
        string? dataSource = connection.Kind == ConnectionKindDto.Data ? (connection.DataSource ?? DataSourceDto.PreviousNode).ToString() : connection.DataSource?.ToString();
        return new(connection.Id, connection.FromNodeId, connection.FromPortId, connection.ToNodeId, connection.ToPortId, kind, branch, dataSource, connection.Priority);
    }

    private static AiLibrarySummaryDto MapLibrary(LibraryDto library, bool includeNodes)
    {
        var lifecycle = library.Lifecycle.ToString();
        IReadOnlyList<AiLibraryNodeContractDto>? nodes = includeNodes ? library.Nodes.Select(node =>
            {
                var type = node.Type.ToString();
                var parameters = node.Parameters.Select(parameter =>
                {
                    return new AiLibraryParameterContractDto(parameter.Id, parameter.Name, parameter.Type, parameter.Description, parameter.Required, parameter.IsVariadic, parameter.ElementType, parameter.DefaultValue, parameter.Aliases, parameter.IdentityConfidence, parameter.EnumMetadata);
                }).ToArray();
                return new AiLibraryNodeContractDto(node.Id, node.ContractId ?? node.Id, type, node.DisplayName, node.Description, node.LibraryId, node.ClassName, node.MethodName, node.DllName, node.DllVersion, node.ReturnType, node.IsAwaitable, node.FlowLibraryName, parameters, node.IdentityConfidence);
            }).ToArray() : null;
        return new(library.Id, library.Name, library.Version, library.SemanticVersion, library.Sha256, lifecycle, library.FamilyId, library.FamilyName, library.Nodes.Count, nodes);
    }

    private static AiRunSummaryDto MapRun(FlowRun run)
    {
        var status = run.Status.ToString();
        var executionKind = run.ExecutionKind.ToString();
        return new(run.Id, run.ProjectId, run.FlowId, run.FlowVersion, status, executionKind, run.IsListenerRun, run.CreatedAt, run.StartedAt, run.EndedAt, run.ErrorSummary, run.CancellationReason, run.DebugSessionId, run.DefinitionChecksum);
    }

    private static AiRunEventDto MapEvent(FlowRunEvent item, int maxJsonBytes)
    {
        var (payload, malformed, truncated) = ParsePayload(item.PayloadJson, maxJsonBytes);
        return new(item.RunId, item.Sequence, item.Timestamp, item.Type, item.NodeId, payload, malformed, truncated);
    }

    private static AiNodeExecutionRecordDto MapOutput(FlowRunOutput item, int maxJsonBytes)
    {
        var (inputs, inputsMalformed, inputsTruncated) = ParsePayload(item.InputsJson, maxJsonBytes);
        var (outputs, outputsMalformed, outputsTruncated) = ParsePayload(item.OutputsJson, maxJsonBytes);
        return new(item.RunId, item.Sequence, item.Timestamp, item.NodeId, item.Outcome, item.Branch, inputs, outputs, item.ErrorCode, item.ErrorMessage, inputsMalformed, outputsMalformed, inputsTruncated, outputsTruncated);
    }

    private static AiDebugStateDto MapDebugState(FlowDebugSession session)
    {
        var status = session.Status.ToString();
        AiDebugPauseStateDto? pauseState = session.PauseState is null ? null : MapPauseState();
        AiDebugNodeResultDto? aiDebugNodeResultDto = session.LastNodeResult is null ? null : MapLastNodeResult();
        return new(session.Id, session.RunId, session.ProjectId, session.FlowId, status, session.BreakpointNodeIds, session.CurrentNodeId, session.ActiveInvocationId, session.ActiveFlipflopNodeId, session.QueuedTriggerCount, session.LastCommandSequence, session.FailureMessage, session.CreatedAt, session.UpdatedAt, session.StateRevision, pauseState, aiDebugNodeResultDto);
        AiDebugPauseStateDto MapPauseState()
        {
            var inputs = ParsePayload(session.PauseState.InputsJson, 64 * 1024).Payload;
            return new AiDebugPauseStateDto(session.PauseState.NodeId, session.PauseState.NodeType, session.PauseState.Step, session.PauseState.FrameDepth, session.PauseState.InvocationId, session.PauseState.BoundarySequence, inputs, session.PauseState.PausedAt);
        }

        AiDebugNodeResultDto MapLastNodeResult()
        {
            var payload = ParsePayload(session.LastNodeResult.InputsJson, 64 * 1024).Payload;
            var outputs = ParsePayload(session.LastNodeResult.OutputsJson, 64 * 1024).Payload;
            return new AiDebugNodeResultDto(session.LastNodeResult.NodeId, session.LastNodeResult.Sequence, session.LastNodeResult.CompletedAt, session.LastNodeResult.Outcome, session.LastNodeResult.Branch, payload, outputs, session.LastNodeResult.ErrorCode, session.LastNodeResult.ErrorMessage);
        }
    }

    private static AiDebugSessionSummaryDto MapDebugSessionSummary(FlowDebugSession session)
    {
        var status = session.Status.ToString();
        return new(session.Id, session.RunId, session.ProjectId, session.FlowId, status, session.BreakpointNodeIds, session.CurrentNodeId, session.ActiveInvocationId, session.ActiveFlipflopNodeId, session.QueuedTriggerCount, session.LastCommandSequence, session.StateRevision, session.CreatedAt, session.UpdatedAt);
    }

    private static (JsonElement Payload, bool Malformed, bool Truncated) ParsePayload(string? json, int maxJsonBytes)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (JsonSerializer.SerializeToElement(new { }), false, false);
        }

        var bounded = LimitText(json, maxJsonBytes);
        if (!string.Equals(bounded, json, StringComparison.Ordinal))
        {
            return (JsonSerializer.SerializeToElement(new { truncated = true, rawLength = json.Length, value = bounded }), false, true);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return (document.RootElement.Clone(), false, false);
        }
        catch (JsonException)
        {
            return (JsonSerializer.SerializeToElement(new { malformed = true, raw = bounded }), true, false);
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
            if (!(length > 0 && GetPrefixByteCount() > maxBytes))
            {
                break;
            }

            length--;
            int GetPrefixByteCount()
            {
                var asSpan = value.AsSpan(0, length);
                return System.Text.Encoding.UTF8.GetByteCount(asSpan);
            }
        }

        return value[..length];
    }

    private static AiPageDto<T> CreatePage<T>(IReadOnlyList<T> items, int take, Func<T, string> cursor)
    {
        var hasMore = items.Count > take;
        var visible = hasMore ? items.Take(take).ToArray() : items.ToArray();
        string? nextCursor = hasMore && visible.Length > 0 ? cursor(visible[^1]) : null;
        return new(AiReadModelContract.SchemaVersion, visible, hasMore, nextCursor);
    }

    private static AiPageDto<T> EmptyPage<T>()
    {
        return new(AiReadModelContract.SchemaVersion, [], false, null);
    }
}

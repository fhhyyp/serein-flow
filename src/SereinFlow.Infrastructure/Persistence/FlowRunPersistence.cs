using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SqlSugar;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Infrastructure.Persistence;
public sealed class SqlSugarFlowRunStore : IFlowRunStore
{
    private const int MinimumRunPageSize = 1;
    private const int MaximumRunPageSize = 10_000;
    private const int DefaultRunTimeoutSeconds = 300;
    private const int DefaultMaximumSteps = 10_000;
    private const int DefaultMaximumNodeVisits = 1_000;
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateWebOptions(options =>
    {
        var jsonStringEnumConverter = new JsonStringEnumConverter(JsonNamingPolicy.CamelCase);
        options.Converters.Add(jsonStringEnumConverter);
    });
    private readonly IRepository<FlowRunRecord> _runs;
    private readonly IRepository<FlowRunDefinitionRecord> _definitions;
    private readonly IRepository<LibraryRecord>? _libraries;
    private readonly IRepository<RunLibraryBindingRecord>? _libraryBindings;
    private readonly IUnitOfWork _unitOfWork;
    public SqlSugarFlowRunStore(IRepository<FlowRunRecord> runs, IRepository<FlowRunDefinitionRecord> definitions, IRepository<LibraryRecord>? libraries, IRepository<RunLibraryBindingRecord>? libraryBindings, IUnitOfWork unitOfWork)
    {
        _runs = runs;
        _definitions = definitions;
        _libraries = libraries;
        _libraryBindings = libraryBindings;
        _unitOfWork = unitOfWork;
    }

    public SqlSugarFlowRunStore(SqliteDatabase database)
    {
        var runs = new SqlSugarRepository<FlowRunRecord>(database.Client);
        var definitions = new SqlSugarRepository<FlowRunDefinitionRecord>(database.Client);
        var libraries = new SqlSugarRepository<LibraryRecord>(database.Client);
        var libraryBindings = new SqlSugarRepository<RunLibraryBindingRecord>(database.Client);
        var unitOfWork = new SqlSugarUnitOfWork(database.Client);
        _runs = runs;
        _definitions = definitions;
        _libraries = libraries;
        _libraryBindings = libraryBindings;
        _unitOfWork = unitOfWork;
    }

    public Task<FlowRun> CreateWithSnapshotAsync(FlowRun run, FlowDefinitionDto definition, CancellationToken cancellationToken = default)
    {
        var flowRunExecutionOptions = new FlowRunExecutionOptions(null, DefaultRunTimeoutSeconds, DefaultMaximumSteps, DefaultMaximumNodeVisits);
        var createWithSnapshotAsyncResult = CreateWithSnapshotAsync(run, definition, flowRunExecutionOptions, cancellationToken);
        return createWithSnapshotAsyncResult;
    }

    public Task<FlowRun> CreateWithSnapshotAsync(FlowRun run, FlowDefinitionDto definition, FlowRunExecutionOptions options, CancellationToken cancellationToken = default)
    {
        var createWithSnapshotCoreAsyncResult = CreateWithSnapshotCoreAsync(run, definition, options, cancellationToken);
        return createWithSnapshotCoreAsyncResult;
    }

    public async Task<FlowRunAdmissionResult> TryCreateWithSnapshotAsync(FlowRun run, FlowDefinitionDto definition, FlowRunExecutionOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            var saved = await CreateWithSnapshotCoreAsync(run, definition, options, cancellationToken);
            var flowRunAdmissionResult = new FlowRunAdmissionResult(saved);
            return flowRunAdmissionResult;
        }
        catch (SqlSugarException exception)when (IsExclusiveConflict(exception))
        {
            var flowRunQuery = new FlowRunQuery([FlowRunStatus.Pending, FlowRunStatus.Running]);
            var evaluatedValue = (await ListAsync(flowRunQuery, cancellationToken));
            var active = evaluatedValue.FirstOrDefault(item => item.FlowId == run.FlowId && item.ConcurrencyMode == FlowConcurrencyMode.ExclusiveReject);
            var flowRunAdmissionResult1 = new FlowRunAdmissionResult(null, active?.Id);
            return flowRunAdmissionResult1;
        }
    }

    private Task<FlowRun> CreateWithSnapshotCoreAsync(FlowRun run, FlowDefinitionDto definition, FlowRunExecutionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (run.FlowId != definition.Id || run.FlowVersion != definition.Version)
        {
            throw new InvalidOperationException("The run identity does not match its immutable flow snapshot. 运行实例身份与不可变流程快照不匹配。");
        }

        var definitionChecksum = definition.Checksum?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(definitionChecksum))
        {
            throw new InvalidOperationException("An immutable flow snapshot requires a checksum. 不可变流程快照必须包含校验和。");
        }

        if (!string.IsNullOrWhiteSpace(run.DefinitionChecksum) && !string.Equals(run.DefinitionChecksum, definitionChecksum, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The run checksum does not match its immutable flow snapshot. 运行实例校验和与不可变流程快照不匹配。");
        }

        var serialized = JsonSerializer.Serialize(definition, JsonOptions);
        var projectInputsJson = options.ProjectInputs is null ? null : JsonSerializer.Serialize(options.ProjectInputs, JsonOptions);
        var executeAsyncResult = _unitOfWork.ExecuteAsync(async token =>
        {
            var flowRunRecord = new FlowRunRecord
            {
                Id = run.Id.ToString("D"),
                ProjectId = run.ProjectId == Guid.Empty ? null : run.ProjectId.ToString("D"),
                FlowId = run.FlowId.ToString("D"),
                FlowVersion = run.FlowVersion,
                DefinitionChecksum = definitionChecksum,
                Status = run.Status.ToString(),
                CreatedAt = run.CreatedAt.ToString("O"),
                Deadline = run.Deadline?.ToString("O"),
                TimeoutSeconds = options.TimeoutSeconds,
                MaxSteps = options.MaxSteps,
                ProjectInputsJson = projectInputsJson,
                MaxNodeVisits = options.MaxNodeVisits,
                ConcurrencyMode = run.ConcurrencyMode.ToString(),
                ExclusivityKey = run.ExclusivityKey,
                IsListenerRun = run.IsListenerRun,
                QueuedAt = run.QueuedAt.ToString("O"),
                ExecutionKind = run.ExecutionKind.ToString(),
                DebugSessionId = run.DebugSessionId?.ToString("D")
            };
            await _runs.AddAsync(flowRunRecord, token);
            var flowRunDefinitionRecord = new FlowRunDefinitionRecord
            {
                RunId = run.Id.ToString("D"),
                FlowId = definition.Id.ToString("D"),
                FlowVersion = definition.Version,
                SchemaVersion = definition.SchemaVersion,
                Checksum = definition.Checksum,
                DefinitionJson = serialized,
                CreatedAt = run.CreatedAt.ToString("O")
            };
            await _definitions.AddAsync(flowRunDefinitionRecord, token);
            await WriteLibraryBindingsAsync(run, definition, token);
            return run;
        }, cancellationToken);
        return executeAsyncResult;
    }

    public async Task<IReadOnlyList<PendingFlowRun>> ListPendingAsync(CancellationToken cancellationToken = default)
    {
        var pendingStatus = FlowRunStatus.Pending.ToString();
        var productionKind = FlowRunExecutionKind.Production.ToString();
        var rows = await _runs.ListAsync(row => row.Status == pendingStatus && (row.ExecutionKind == null || row.ExecutionKind == productionKind), cancellationToken);
        var pending = new List<PendingFlowRun>(rows.Count);
        foreach (var row in rows)
        {
            var run = Map(row);
            var definition = await GetSnapshotAsync(run.Id, cancellationToken);
            if (definition is null || string.IsNullOrWhiteSpace(run.DefinitionChecksum) || !string.Equals(run.DefinitionChecksum, definition.Checksum, StringComparison.Ordinal) || run.FlowId != definition.Id || run.FlowVersion != definition.Version)
            {
                continue;
            }

            IReadOnlyDictionary<string, JsonElement>? inputs = null;
            if (!string.IsNullOrWhiteSpace(row.ProjectInputsJson))
            {
                inputs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.ProjectInputsJson!, JsonOptions);
            }

            var timeoutSeconds = row.TimeoutSeconds > 0 ? row.TimeoutSeconds : 300;
            var maxSteps = row.MaxSteps > 0 ? row.MaxSteps : 10_000;
            var maxNodeVisits = row.MaxNodeVisits > 0 ? row.MaxNodeVisits : 1_000;
            var flowRunExecutionOptions2 = new FlowRunExecutionOptions(inputs, timeoutSeconds, maxSteps, maxNodeVisits);
            var pendingFlowRun = new PendingFlowRun(run, definition, flowRunExecutionOptions2);
            pending.Add(pendingFlowRun);
        }

        var orderByResult = pending.OrderBy(static item => item.Run.QueuedAt);
        var thenByResult = orderByResult.ThenBy(static item => item.Run.Id);
        var toArrayResult = thenByResult.ToArray();
        return toArrayResult;
    }

    public async Task<IReadOnlyList<FlowRun>> ListAsync(FlowRunQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var optionalReceiver = query.Statuses;
        HashSet<string>? optionalResult = default(HashSet<string>);
        if (optionalReceiver is not null)
        {
            var selectResult = optionalReceiver.Select(static item =>
            {
                var toStringResult = item.ToString();
                return toStringResult;
            });
            optionalResult = selectResult.ToHashSet(StringComparer.Ordinal);
        }

        var statuses = optionalResult;
        var projectId = query.ProjectId?.ToString("D");
        var rows = await _runs.ListAsync(projectId is not null ? row => row.ProjectId == projectId : null, cancellationToken);
        var whereResult = rows.Where(row =>
        {
            var evaluatedValue3 = statuses is null || statuses.Contains(row.Status);
            return evaluatedValue3;
        });
        var selectResult4 = whereResult.Select(Map);
        var orderByDescendingResult = selectResult4.OrderByDescending(static item => item.CreatedAt);
        var clampResult = Math.Clamp(query.Take, MinimumRunPageSize, MaximumRunPageSize);
        var takeResult = orderByDescendingResult.Take(clampResult);
        var toArrayResult5 = takeResult.ToArray();
        return toArrayResult5;
    }

    public async Task<FlowRun?> FindAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var toStringResult6 = runId.ToString("D");
        var row = await _runs.GetByIdAsync(toStringResult6, cancellationToken);
        FlowRun? evaluatedValue7 = row is null ? null : Map(row);
        return evaluatedValue7;
    }

    public async Task<FlowDefinitionDto?> GetSnapshotAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var runKey = runId.ToString("D");
        var rows = await _definitions.ListAsync(cancellationToken: cancellationToken);
        var row = rows.SingleOrDefault(item =>
        {
            var equalsResult = string.Equals(item.RunId, runKey, StringComparison.OrdinalIgnoreCase);
            return equalsResult;
        });
        FlowDefinitionDto? evaluatedValue8 = row is null ? null : JsonSerializer.Deserialize<FlowDefinitionDto>(row.DefinitionJson, JsonOptions);
        return evaluatedValue8;
    }

    public async Task<bool> SaveAsync(FlowRun run, CancellationToken cancellationToken = default)
    {
        var toStringResult9 = run.Id.ToString("D");
        var record = await _runs.GetByIdAsync(toStringResult9, cancellationToken);
        if (record is null)
        {
            return false;
        }

        record.Status = run.Status.ToString();
        record.StartedAt = run.StartedAt?.ToString("O");
        record.EndedAt = run.EndedAt?.ToString("O");
        record.CancellationReason = run.CancellationReason;
        record.ErrorSummary = run.ErrorSummary;
        record.Deadline = run.Deadline?.ToString("O");
        record.CreatedAt = run.CreatedAt.ToString("O");
        record.ConcurrencyMode = run.ConcurrencyMode.ToString();
        record.ExclusivityKey = run.ExclusivityKey;
        record.IsListenerRun = run.IsListenerRun;
        record.QueuedAt = run.QueuedAt.ToString("O");
        record.ExecutionKind = run.ExecutionKind.ToString();
        record.DebugSessionId = run.DebugSessionId?.ToString("D");
        var updateAsyncResult = await _runs.UpdateAsync(record, cancellationToken);
        return updateAsyncResult;
    }

    public FlowRun CreateWithSnapshot(FlowRun run, FlowDefinitionDto definition)
    {
        if (run.FlowId != definition.Id || run.FlowVersion != definition.Version)
        {
            throw new InvalidOperationException("The run identity does not match its immutable flow snapshot. 运行实例身份与不可变流程快照不匹配。");
        }

        if (string.IsNullOrWhiteSpace(definition.Checksum))
        {
            throw new InvalidOperationException("An immutable flow snapshot requires a checksum. 不可变流程快照必须包含校验和。");
        }

        if (!string.IsNullOrWhiteSpace(run.DefinitionChecksum) && !string.Equals(run.DefinitionChecksum, definition.Checksum, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The run checksum does not match its immutable flow snapshot. 运行实例校验和与不可变流程快照不匹配。");
        }

        var serialized = JsonSerializer.Serialize(definition, JsonOptions);
        var executeAsyncResult12 = _unitOfWork.ExecuteAsync(async cancellationToken =>
        {
            var flowRunRecord10 = new FlowRunRecord
            {
                Id = run.Id.ToString("D"),
                ProjectId = run.ProjectId == Guid.Empty ? null : run.ProjectId.ToString("D"),
                FlowId = run.FlowId.ToString("D"),
                FlowVersion = run.FlowVersion,
                DefinitionChecksum = definition.Checksum,
                Status = run.Status.ToString(),
                CreatedAt = run.CreatedAt.ToString("O"),
                ConcurrencyMode = run.ConcurrencyMode.ToString(),
                ExclusivityKey = run.ExclusivityKey,
                IsListenerRun = run.IsListenerRun,
                QueuedAt = run.QueuedAt.ToString("O"),
                ExecutionKind = run.ExecutionKind.ToString(),
                DebugSessionId = run.DebugSessionId?.ToString("D")
            };
            await _runs.AddAsync(flowRunRecord10, cancellationToken);
            var flowRunDefinitionRecord11 = new FlowRunDefinitionRecord
            {
                RunId = run.Id.ToString("D"),
                FlowId = definition.Id.ToString("D"),
                FlowVersion = definition.Version,
                SchemaVersion = definition.SchemaVersion,
                Checksum = definition.Checksum,
                DefinitionJson = serialized,
                CreatedAt = run.CreatedAt.ToString("O")
            };
            await _definitions.AddAsync(flowRunDefinitionRecord11, cancellationToken);
            await WriteLibraryBindingsAsync(run, definition, cancellationToken);
            return run;
        });
        var getAwaiterResult = executeAsyncResult12.GetAwaiter();
        var getResultResult = getAwaiterResult.GetResult();
        return getResultResult;
    }

    public FlowRun? Find(Guid runId)
    {
        var toStringResult13 = runId.ToString("D");
        var getByIdAsyncResult = _runs.GetByIdAsync(toStringResult13);
        var getAwaiterResult14 = getByIdAsyncResult.GetAwaiter();
        var row = getAwaiterResult14.GetResult();
        FlowRun? evaluatedValue15 = row is null ? null : Map(row);
        return evaluatedValue15;
    }

    public FlowDefinitionDto? GetSnapshot(Guid runId)
    {
        // Keep the lookup independent of the concrete query provider.  The
        // application-facing repository intentionally exposes expressions,
        // but snapshot retrieval must remain reliable for providers that do
        // not translate every expression shape identically.  Filtering the
        // small, run-scoped table after materialization also keeps this
        // adapter compatible with the existing SQLite schema.
        var runKey = runId.ToString("D");
        var listAsyncResult = _definitions.ListAsync();
        var getAwaiterResult17 = listAsyncResult.GetAwaiter();
        var getResultResult18 = getAwaiterResult17.GetResult();
        var row = getResultResult18.SingleOrDefault(item =>
        {
            var equalsResult16 = string.Equals(item.RunId, runKey, StringComparison.OrdinalIgnoreCase);
            return equalsResult16;
        });
        FlowDefinitionDto? evaluatedValue19 = row is null ? null : JsonSerializer.Deserialize<FlowDefinitionDto>(row.DefinitionJson, JsonOptions);
        return evaluatedValue19;
    }

    public bool Save(FlowRun run)
    {
        var toStringResult20 = run.Id.ToString("D");
        var getByIdAsyncResult21 = _runs.GetByIdAsync(toStringResult20);
        var getAwaiterResult22 = getByIdAsyncResult21.GetAwaiter();
        var record = getAwaiterResult22.GetResult();
        if (record is null)
        {
            return false;
        }

        record.Status = run.Status.ToString();
        record.StartedAt = run.StartedAt?.ToString("O");
        record.EndedAt = run.EndedAt?.ToString("O");
        record.CancellationReason = run.CancellationReason;
        record.ErrorSummary = run.ErrorSummary;
        record.Deadline = run.Deadline?.ToString("O");
        record.CreatedAt = run.CreatedAt.ToString("O");
        record.ConcurrencyMode = run.ConcurrencyMode.ToString();
        record.ExclusivityKey = run.ExclusivityKey;
        record.IsListenerRun = run.IsListenerRun;
        record.QueuedAt = run.QueuedAt.ToString("O");
        record.ExecutionKind = run.ExecutionKind.ToString();
        record.DebugSessionId = run.DebugSessionId?.ToString("D");
        var updateAsyncResult23 = _runs.UpdateAsync(record);
        var getAwaiterResult24 = updateAsyncResult23.GetAwaiter();
        var getResultResult25 = getAwaiterResult24.GetResult();
        return getResultResult25;
    }

    private static FlowRun Map(FlowRunRecord row)
    {
        var parseResult = Guid.Parse(row.Id);
        Guid evaluatedValue26 = Guid.TryParse(row.ProjectId, out var projectId) ? projectId : Guid.Empty;
        var parseResult27 = Guid.Parse(row.FlowId);
        var parseResult28 = Enum.Parse<FlowRunStatus>(row.Status);
        var evaluatedValue29 = ParseNullable(row.CreatedAt) ?? ParseNullable(row.StartedAt) ?? DateTimeOffset.UtcNow;
        var parseNullableResult = ParseNullable(row.StartedAt);
        var parseNullableResult30 = ParseNullable(row.EndedAt);
        FlowConcurrencyMode evaluatedValue31 = Enum.TryParse<FlowConcurrencyMode>(row.ConcurrencyMode, out var concurrencyMode) ? concurrencyMode : FlowConcurrencyMode.Parallel;
        var parseNullableResult32 = ParseNullable(row.QueuedAt);
        var parseNullableResult33 = ParseNullable(row.Deadline);
        FlowRunExecutionKind evaluatedValue34 = Enum.TryParse<FlowRunExecutionKind>(row.ExecutionKind, out var executionKind) ? executionKind : FlowRunExecutionKind.Production;
        Guid? evaluatedValue35 = Guid.TryParse(row.DebugSessionId, out var debugSessionId) ? debugSessionId : null;
        var rehydrateResult = FlowRun.Rehydrate(parseResult, evaluatedValue26, parseResult27, row.FlowVersion, parseResult28, evaluatedValue29, parseNullableResult, parseNullableResult30, row.CancellationReason, row.ErrorSummary, evaluatedValue31, row.IsListenerRun, parseNullableResult32, parseNullableResult33, evaluatedValue34, evaluatedValue35, row.DefinitionChecksum);
        return rehydrateResult;
    }

    private static bool IsExclusiveConflict(SqlSugarException exception)
    {
        var evaluatedValue36 = exception.Message.Contains("UX_FlowRuns_ActiveExclusiveFlow", StringComparison.OrdinalIgnoreCase) || exception.Message.Contains("FlowRuns.ExclusivityKey", StringComparison.OrdinalIgnoreCase);
        return evaluatedValue36;
    }

    private async Task WriteLibraryBindingsAsync(FlowRun run, FlowDefinitionDto definition, CancellationToken cancellationToken)
    {
        if (_libraryBindings is null || _libraries is null)
        {
            return;
        }

        foreach (var artifactId in LibraryBindingIndex.Extract(definition))
        {
            // Run snapshots can contain older, no-longer-catalogued bindings.
            // The index is optional audit metadata and must not prevent the
            // immutable run snapshot itself from being retained.
            // 运行快照可能包含目录中已不存在的旧绑定。该索引只是可选的审计
            // 元数据，不能妨碍不可变运行快照本身被保留。
            if (await _libraries.GetByIdAsync(artifactId, cancellationToken)is null)
            {
                continue;
            }

            var runLibraryBindingRecord = new RunLibraryBindingRecord
            {
                Id = resolveToStringResult(),
                RunId = run.Id.ToString("D"),
                LibraryArtifactId = artifactId,
                CreatedAt = run.CreatedAt.ToString("O")
            };
            await _libraryBindings.AddAsync(runLibraryBindingRecord, cancellationToken);
            string resolveToStringResult()
            {
                var newGuidResult = Guid.NewGuid();
                var toStringResult37 = newGuidResult.ToString("N");
                return toStringResult37;
            }
        }
    }

    private static DateTimeOffset Parse(string value)
    {
        var parseResult38 = DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return parseResult38;
    }

    private static DateTimeOffset? ParseNullable(string? value)
    {
        DateTimeOffset? evaluatedValue39 = string.IsNullOrWhiteSpace(value) ? null : Parse(value);
        return evaluatedValue39;
    }
}

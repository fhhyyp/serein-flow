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
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateWebOptions(options =>
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

    private readonly IRepository<FlowRunRecord> _runs;
    private readonly IRepository<FlowRunDefinitionRecord> _definitions;
    private readonly IRepository<LibraryRecord>? _libraries;
    private readonly IRepository<RunLibraryBindingRecord>? _libraryBindings;
    private readonly IUnitOfWork _unitOfWork;

    public SqlSugarFlowRunStore(
        IRepository<FlowRunRecord> runs,
        IRepository<FlowRunDefinitionRecord> definitions,
        IRepository<LibraryRecord>? libraries,
        IRepository<RunLibraryBindingRecord>? libraryBindings,
        IUnitOfWork unitOfWork)
    {
        _runs = runs;
        _definitions = definitions;
        _libraries = libraries;
        _libraryBindings = libraryBindings;
        _unitOfWork = unitOfWork;
    }

    public SqlSugarFlowRunStore(SqliteDatabase database)
        : this(
            new SqlSugarRepository<FlowRunRecord>(database.Client),
            new SqlSugarRepository<FlowRunDefinitionRecord>(database.Client),
            new SqlSugarRepository<LibraryRecord>(database.Client),
            new SqlSugarRepository<RunLibraryBindingRecord>(database.Client),
            new SqlSugarUnitOfWork(database.Client))
    {
    }

    public Task<FlowRun> CreateWithSnapshotAsync(FlowRun run, FlowDefinitionDto definition, CancellationToken cancellationToken = default)
        => CreateWithSnapshotAsync(
            run,
            definition,
            new FlowRunExecutionOptions(null, 300, 10_000, 1_000),
            cancellationToken);

    public Task<FlowRun> CreateWithSnapshotAsync(
        FlowRun run,
        FlowDefinitionDto definition,
        FlowRunExecutionOptions options,
        CancellationToken cancellationToken = default)
        => CreateWithSnapshotCoreAsync(run, definition, options, cancellationToken);

    public async Task<FlowRunAdmissionResult> TryCreateWithSnapshotAsync(
        FlowRun run,
        FlowDefinitionDto definition,
        FlowRunExecutionOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var saved = await CreateWithSnapshotCoreAsync(run, definition, options, cancellationToken);
            return new FlowRunAdmissionResult(saved);
        }
        catch (SqlSugarException exception) when (IsExclusiveConflict(exception))
        {
            var active = (await ListAsync(
                    new FlowRunQuery([FlowRunStatus.Pending, FlowRunStatus.Running]),
                    cancellationToken))
                .FirstOrDefault(item => item.FlowId == run.FlowId && item.ConcurrencyMode == FlowConcurrencyMode.ExclusiveReject);
            return new FlowRunAdmissionResult(null, active?.Id);
        }
    }

    private Task<FlowRun> CreateWithSnapshotCoreAsync(
        FlowRun run,
        FlowDefinitionDto definition,
        FlowRunExecutionOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (run.FlowId != definition.Id || run.FlowVersion != definition.Version)
        {
            throw new InvalidOperationException(
                "The run identity does not match its immutable flow snapshot. 运行实例身份与不可变流程快照不匹配。");
        }

        var definitionChecksum = definition.Checksum?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(definitionChecksum))
        {
            throw new InvalidOperationException(
                "An immutable flow snapshot requires a checksum. 不可变流程快照必须包含校验和。");
        }

        if (!string.IsNullOrWhiteSpace(run.DefinitionChecksum)
            && !string.Equals(run.DefinitionChecksum, definitionChecksum, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The run checksum does not match its immutable flow snapshot. 运行实例校验和与不可变流程快照不匹配。");
        }

        var serialized = JsonSerializer.Serialize(definition, JsonOptions);
        var projectInputsJson = options.ProjectInputs is null ? null : JsonSerializer.Serialize(options.ProjectInputs, JsonOptions);
        return _unitOfWork.ExecuteAsync(async token =>
        {
            await _runs.AddAsync(new FlowRunRecord
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
            }, token);
            await _definitions.AddAsync(new FlowRunDefinitionRecord
            {
                RunId = run.Id.ToString("D"),
                FlowId = definition.Id.ToString("D"),
                FlowVersion = definition.Version,
                SchemaVersion = definition.SchemaVersion,
                Checksum = definition.Checksum,
                DefinitionJson = serialized,
                CreatedAt = run.CreatedAt.ToString("O")
            }, token);
            await WriteLibraryBindingsAsync(run, definition, token);
            return run;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<PendingFlowRun>> ListPendingAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _runs.ListAsync(
            row => row.Status == FlowRunStatus.Pending.ToString()
                && (row.ExecutionKind == null || row.ExecutionKind == FlowRunExecutionKind.Production.ToString()),
            cancellationToken);
        var pending = new List<PendingFlowRun>(rows.Count);
        foreach (var row in rows)
        {
            var run = Map(row);
            var definition = await GetSnapshotAsync(run.Id, cancellationToken);
            if (definition is null
                || string.IsNullOrWhiteSpace(run.DefinitionChecksum)
                || !string.Equals(run.DefinitionChecksum, definition.Checksum, StringComparison.Ordinal)
                || run.FlowId != definition.Id
                || run.FlowVersion != definition.Version)
                continue;

            IReadOnlyDictionary<string, JsonElement>? inputs = null;
            if (!string.IsNullOrWhiteSpace(row.ProjectInputsJson))
            {
                inputs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.ProjectInputsJson!, JsonOptions);
            }

            var timeoutSeconds = row.TimeoutSeconds > 0 ? row.TimeoutSeconds : 300;
            var maxSteps = row.MaxSteps > 0 ? row.MaxSteps : 10_000;
            var maxNodeVisits = row.MaxNodeVisits > 0 ? row.MaxNodeVisits : 1_000;
            pending.Add(new PendingFlowRun(run, definition, new FlowRunExecutionOptions(inputs, timeoutSeconds, maxSteps, maxNodeVisits)));
        }
        return pending
            .OrderBy(static item => item.Run.QueuedAt)
            .ThenBy(static item => item.Run.Id)
            .ToArray();
    }

    public async Task<IReadOnlyList<FlowRun>> ListAsync(FlowRunQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var statuses = query.Statuses?.Select(static item => item.ToString()).ToHashSet(StringComparer.Ordinal);
        var projectId = query.ProjectId?.ToString("D");
        var rows = await _runs.ListAsync(
            projectId is not null
                ? row => row.ProjectId == projectId
                : null,
            cancellationToken);
        return rows
            .Where(row => statuses is null || statuses.Contains(row.Status))
            .Select(Map)
            .OrderByDescending(static item => item.CreatedAt)
            .Take(Math.Clamp(query.Take, 1, 10_000))
            .ToArray();
    }

    public async Task<FlowRun?> FindAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var row = await _runs.GetByIdAsync(runId.ToString("D"), cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<FlowDefinitionDto?> GetSnapshotAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var runKey = runId.ToString("D");
        var rows = await _definitions.ListAsync(cancellationToken: cancellationToken);
        var row = rows.SingleOrDefault(item => string.Equals(item.RunId, runKey, StringComparison.OrdinalIgnoreCase));
        return row is null ? null : JsonSerializer.Deserialize<FlowDefinitionDto>(row.DefinitionJson, JsonOptions);
    }

    public async Task<bool> SaveAsync(FlowRun run, CancellationToken cancellationToken = default)
    {
        var record = await _runs.GetByIdAsync(run.Id.ToString("D"), cancellationToken);
        if (record is null)
            return false;
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
        return await _runs.UpdateAsync(record, cancellationToken);
    }

    public FlowRun CreateWithSnapshot(FlowRun run, FlowDefinitionDto definition)
    {
        if (run.FlowId != definition.Id || run.FlowVersion != definition.Version)
            throw new InvalidOperationException("The run identity does not match its immutable flow snapshot. 运行实例身份与不可变流程快照不匹配。");
        if (string.IsNullOrWhiteSpace(definition.Checksum))
            throw new InvalidOperationException("An immutable flow snapshot requires a checksum. 不可变流程快照必须包含校验和。");
        if (!string.IsNullOrWhiteSpace(run.DefinitionChecksum)
            && !string.Equals(run.DefinitionChecksum, definition.Checksum, StringComparison.Ordinal))
            throw new InvalidOperationException("The run checksum does not match its immutable flow snapshot. 运行实例校验和与不可变流程快照不匹配。");

        var serialized = JsonSerializer.Serialize(definition, JsonOptions);
        return _unitOfWork.ExecuteAsync(async cancellationToken =>
        {
            await _runs.AddAsync(new FlowRunRecord
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
            }, cancellationToken);
            await _definitions.AddAsync(new FlowRunDefinitionRecord
            {
                RunId = run.Id.ToString("D"),
                FlowId = definition.Id.ToString("D"),
                FlowVersion = definition.Version,
                SchemaVersion = definition.SchemaVersion,
                Checksum = definition.Checksum,
                DefinitionJson = serialized,
                CreatedAt = run.CreatedAt.ToString("O")
            }, cancellationToken);
            await WriteLibraryBindingsAsync(run, definition, cancellationToken);
            return run;
        }).GetAwaiter().GetResult();
    }

    public FlowRun? Find(Guid runId)
    {
        var row = _runs.GetByIdAsync(runId.ToString("D")).GetAwaiter().GetResult();
        return row is null ? null : Map(row);
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
        var row = _definitions.ListAsync()
            .GetAwaiter().GetResult()
            .SingleOrDefault(item => string.Equals(item.RunId, runKey, StringComparison.OrdinalIgnoreCase));
        return row is null ? null : JsonSerializer.Deserialize<FlowDefinitionDto>(row.DefinitionJson, JsonOptions);
    }

    public bool Save(FlowRun run)
    {
        var record = _runs.GetByIdAsync(run.Id.ToString("D")).GetAwaiter().GetResult();
        if (record is null)
            return false;
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
        return _runs.UpdateAsync(record).GetAwaiter().GetResult();
    }

    private static FlowRun Map(FlowRunRecord row)
        => FlowRun.Rehydrate(
            Guid.Parse(row.Id),
            Guid.TryParse(row.ProjectId, out var projectId) ? projectId : Guid.Empty,
            Guid.Parse(row.FlowId),
            row.FlowVersion,
            Enum.Parse<FlowRunStatus>(row.Status),
            ParseNullable(row.CreatedAt) ?? ParseNullable(row.StartedAt) ?? DateTimeOffset.UtcNow,
            ParseNullable(row.StartedAt),
            ParseNullable(row.EndedAt),
            row.CancellationReason,
            row.ErrorSummary,
            Enum.TryParse<FlowConcurrencyMode>(row.ConcurrencyMode, out var concurrencyMode)
                ? concurrencyMode
                : FlowConcurrencyMode.Parallel,
            row.IsListenerRun,
            ParseNullable(row.QueuedAt),
            ParseNullable(row.Deadline),
            Enum.TryParse<FlowRunExecutionKind>(row.ExecutionKind, out var executionKind)
                ? executionKind
                : FlowRunExecutionKind.Production,
            Guid.TryParse(row.DebugSessionId, out var debugSessionId) ? debugSessionId : null,
            row.DefinitionChecksum);

    private static bool IsExclusiveConflict(SqlSugarException exception)
        => exception.Message.Contains("UX_FlowRuns_ActiveExclusiveFlow", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("FlowRuns.ExclusivityKey", StringComparison.OrdinalIgnoreCase);

    private async Task WriteLibraryBindingsAsync(
        FlowRun run,
        FlowDefinitionDto definition,
        CancellationToken cancellationToken)
    {
        if (_libraryBindings is null || _libraries is null)
            return;

        foreach (var artifactId in LibraryBindingIndex.Extract(definition))
        {
            // Run snapshots can contain older, no-longer-catalogued bindings.
            // The index is optional audit metadata and must not prevent the
            // immutable run snapshot itself from being retained.
            // 运行快照可能包含目录中已不存在的旧绑定。该索引只是可选的审计
            // 元数据，不能妨碍不可变运行快照本身被保留。
            if (await _libraries.GetByIdAsync(artifactId, cancellationToken) is null)
                continue;

            await _libraryBindings.AddAsync(new RunLibraryBindingRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                RunId = run.Id.ToString("D"),
                LibraryArtifactId = artifactId,
                CreatedAt = run.CreatedAt.ToString("O")
            }, cancellationToken);
        }
    }

    private static DateTimeOffset Parse(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    private static DateTimeOffset? ParseNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : Parse(value);

}

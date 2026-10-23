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
        return CreateWithSnapshotAsync(run, definition, flowRunExecutionOptions, cancellationToken);
    }

    public Task<FlowRun> CreateWithSnapshotAsync(FlowRun run, FlowDefinitionDto definition, FlowRunExecutionOptions options, CancellationToken cancellationToken = default)
    {
        return CreateWithSnapshotCoreAsync(run, definition, options, cancellationToken);
    }

    public async Task<FlowRunAdmissionResult> TryCreateWithSnapshotAsync(FlowRun run, FlowDefinitionDto definition, FlowRunExecutionOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            var saved = await CreateWithSnapshotCoreAsync(run, definition, options, cancellationToken);
            return new FlowRunAdmissionResult(saved);
        }
        catch (SqlSugarException exception) when (IsExclusiveConflict(exception))
        {
            var flowRunQuery = new FlowRunQuery([FlowRunStatus.Pending, FlowRunStatus.Running]);
            var active = (await ListAsync(flowRunQuery, cancellationToken)).FirstOrDefault(item => item.FlowId == run.FlowId && item.ConcurrencyMode == FlowConcurrencyMode.ExclusiveReject);
            return new FlowRunAdmissionResult(null, active?.Id);
        }
    }

    private Task<FlowRun> CreateWithSnapshotCoreAsync(FlowRun run, FlowDefinitionDto definition, FlowRunExecutionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (run.FlowId != definition.Id || run.FlowVersion != definition.Version)
        {
            throw new InvalidOperationException("The run identity does not match its immutable flow snapshot. 运行实例身份与不可变流程快照不匹配。");
        }

        var snapshotChecksum = definition.Checksum;
        var definitionChecksum = snapshotChecksum?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(snapshotChecksum))
        {
            throw new InvalidOperationException("An immutable flow snapshot requires a checksum. 不可变流程快照必须包含校验和。");
        }

        if (!string.IsNullOrWhiteSpace(run.DefinitionChecksum) && !string.Equals(run.DefinitionChecksum, definitionChecksum, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The run checksum does not match its immutable flow snapshot. 运行实例校验和与不可变流程快照不匹配。");
        }

        var definitionJson = JsonSerializer.Serialize(definition, JsonOptions);
        var projectInputsJson = options.ProjectInputs is null ? null : JsonSerializer.Serialize(options.ProjectInputs, JsonOptions);
        return _unitOfWork.ExecuteAsync(async token =>
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
                Checksum = snapshotChecksum,
                DefinitionJson = definitionJson,
                CreatedAt = run.CreatedAt.ToString("O")
            };
            await _definitions.AddAsync(flowRunDefinitionRecord, token);
            await WriteLibraryBindingsAsync(run, definition, token);
            return run;
        }, cancellationToken);
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
            var pendingFlowRun = new PendingFlowRun(run, definition, new FlowRunExecutionOptions(inputs, timeoutSeconds, maxSteps, maxNodeVisits));
            pending.Add(pendingFlowRun);
        }
        return pending
            .OrderBy(static item => item.Run.QueuedAt)
            .ThenBy(static item => item.Run.Id)
            .ToArray();
    }

    public async Task<IReadOnlyList<FlowRun>> ListAsync(FlowRunQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var mappedStatuses = query.Statuses;
        HashSet<string>? hashSet = default(HashSet<string>);
        if (mappedStatuses is not null)
        {
            hashSet = mappedStatuses.Select(static item =>
            {
                return item.ToString();
            }).ToHashSet(StringComparer.Ordinal);
        }

        var statuses = hashSet;
        if (query.ProjectIds is { Count: 0 })
        {
            return [];
        }

        var allowedProjects = query.ProjectIds?.Select(id =>
        {
            return id.ToString("D");
        }).ToArray();
        var projectId = query.ProjectId?.ToString("D");
        var orderByDescending = (await _runs.ListAsync(projectId is not null ? row => row.ProjectId == projectId : null, cancellationToken))
            .Where(row =>
        {
            return allowedProjects is null || allowedProjects.Contains(row.ProjectId);
        })
            .Where(row =>
        {
            return statuses is null || statuses.Contains(row.Status);
        })
            .Select(Map)
            .OrderByDescending(static item => item.CreatedAt);
        var clamp = Math.Clamp(query.Take, MinimumRunPageSize, MaximumRunPageSize);
        return orderByDescending.Take(clamp).ToArray();
    }

    public async Task<FlowRun?> FindAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var id = runId.ToString("D");
        var row = await _runs.GetByIdAsync(id, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<FlowDefinitionDto?> GetSnapshotAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var runKey = runId.ToString("D");
        var row = (await _definitions.ListAsync(cancellationToken: cancellationToken)).SingleOrDefault(item =>
        {
            return string.Equals(item.RunId, runKey, StringComparison.OrdinalIgnoreCase);
        });
        return row is null ? null : JsonSerializer.Deserialize<FlowDefinitionDto>(row.DefinitionJson, JsonOptions);
    }

    public async Task<bool> SaveAsync(FlowRun run, CancellationToken cancellationToken = default)
    {
        var id = run.Id.ToString("D");
        var record = await _runs.GetByIdAsync(id, cancellationToken);
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
        return await _runs.UpdateAsync(record, cancellationToken);
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

        var definitionJson = JsonSerializer.Serialize(definition, JsonOptions);
        var task = _unitOfWork.ExecuteAsync(async cancellationToken =>
        {
            var entity = new FlowRunRecord
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
            await _runs.AddAsync(entity, cancellationToken);
            var flowRunDefinitionRecord = new FlowRunDefinitionRecord
            {
                RunId = run.Id.ToString("D"),
                FlowId = definition.Id.ToString("D"),
                FlowVersion = definition.Version,
                SchemaVersion = definition.SchemaVersion,
                Checksum = definition.Checksum,
                DefinitionJson = definitionJson,
                CreatedAt = run.CreatedAt.ToString("O")
            };
            await _definitions.AddAsync(flowRunDefinitionRecord, cancellationToken);
            await WriteLibraryBindingsAsync(run, definition, cancellationToken);
            return run;
        });
        return task.GetAwaiter().GetResult();
    }

    public FlowRun? Find(Guid runId)
    {
        var id = runId.ToString("D");
        var row = _runs
            .GetByIdAsync(id)
            .GetAwaiter()
            .GetResult();
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
        // 查询不依赖具体提供程序的表达式翻译能力；对运行级小表先物化再筛选，保证快照读取可靠，并兼容现有 SQLite 架构。
        var runKey = runId.ToString("D");
        var row = _definitions
            .ListAsync()
            .GetAwaiter()
            .GetResult()
            .SingleOrDefault(item =>
        {
            return string.Equals(item.RunId, runKey, StringComparison.OrdinalIgnoreCase);
        });
        return row is null ? null : JsonSerializer.Deserialize<FlowDefinitionDto>(row.DefinitionJson, JsonOptions);
    }

    public bool Save(FlowRun run)
    {
        var id = run.Id.ToString("D");
        var record = _runs
            .GetByIdAsync(id)
            .GetAwaiter()
            .GetResult();
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
        return _runs
            .UpdateAsync(record)
            .GetAwaiter()
            .GetResult();
    }

    private static FlowRun Map(FlowRunRecord row)
    {
        var id = Guid.Parse(row.Id);
        Guid empty = Guid.TryParse(row.ProjectId, out var projectId) ? projectId : Guid.Empty;
        var flowId = Guid.Parse(row.FlowId);
        var status = Enum.Parse<FlowRunStatus>(row.Status);
        var createdAt = ParseNullable(row.CreatedAt) ?? ParseNullable(row.StartedAt) ?? DateTimeOffset.UtcNow;
        var startedAt = ParseNullable(row.StartedAt);
        var endedAt = ParseNullable(row.EndedAt);
        FlowConcurrencyMode parallel = Enum.TryParse<FlowConcurrencyMode>(row.ConcurrencyMode, out var concurrencyMode) ? concurrencyMode : FlowConcurrencyMode.Parallel;
        var queuedAt = ParseNullable(row.QueuedAt);
        var deadline = ParseNullable(row.Deadline);
        FlowRunExecutionKind production = Enum.TryParse<FlowRunExecutionKind>(row.ExecutionKind, out var executionKind) ? executionKind : FlowRunExecutionKind.Production;
        Guid? parsedDebugSessionId = Guid.TryParse(row.DebugSessionId, out var debugSessionId) ? debugSessionId : null;
        return FlowRun.Rehydrate(id, empty, flowId, row.FlowVersion, status, createdAt, startedAt, endedAt, row.CancellationReason, row.ErrorSummary, parallel, row.IsListenerRun, queuedAt, deadline, production, parsedDebugSessionId, row.DefinitionChecksum);
    }

    private static bool IsExclusiveConflict(SqlSugarException exception)
    {
        return exception.Message.Contains("UX_FlowRuns_ActiveExclusiveFlow", StringComparison.OrdinalIgnoreCase) || exception.Message.Contains("FlowRuns.ExclusivityKey", StringComparison.OrdinalIgnoreCase);
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
            if (await _libraries.GetByIdAsync(artifactId, cancellationToken) is null)
            {
                continue;
            }

            var runLibraryBindingRecord = new RunLibraryBindingRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                RunId = run.Id.ToString("D"),
                LibraryArtifactId = artifactId,
                CreatedAt = run.CreatedAt.ToString("O")
            };
            await _libraryBindings.AddAsync(runLibraryBindingRecord, cancellationToken);
        }
    }

    private static DateTimeOffset Parse(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    private static DateTimeOffset? ParseNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : Parse(value);
    }
}

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private readonly IUnitOfWork _unitOfWork;

    public SqlSugarFlowRunStore(
        IRepository<FlowRunRecord> runs,
        IRepository<FlowRunDefinitionRecord> definitions,
        IUnitOfWork unitOfWork)
    {
        _runs = runs;
        _definitions = definitions;
        _unitOfWork = unitOfWork;
    }

    public SqlSugarFlowRunStore(SqliteDatabase database)
        : this(
            new SqlSugarRepository<FlowRunRecord>(database.Client),
            new SqlSugarRepository<FlowRunDefinitionRecord>(database.Client),
            new SqlSugarUnitOfWork(database.Client))
    {
    }

    public Task<FlowRun> CreateWithSnapshotAsync(FlowRun run, FlowDefinitionDto definition, CancellationToken cancellationToken = default)
        => CreateWithSnapshotAsync(
            run,
            definition,
            new FlowRunExecutionOptions(null, DateTimeOffset.UtcNow.AddMinutes(5), 10_000, 1_000),
            cancellationToken);

    public Task<FlowRun> CreateWithSnapshotAsync(
        FlowRun run,
        FlowDefinitionDto definition,
        FlowRunExecutionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
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
                Status = run.Status.ToString(),
                CreatedAt = run.CreatedAt.ToString("O"),
                Deadline = options.Deadline.ToString("O"),
                MaxSteps = options.MaxSteps,
                ProjectInputsJson = projectInputsJson,
                MaxNodeVisits = options.MaxNodeVisits
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
            return run;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<PendingFlowRun>> ListPendingAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _runs.ListAsync(row => row.Status == FlowRunStatus.Pending.ToString(), cancellationToken);
        var pending = new List<PendingFlowRun>(rows.Count);
        foreach (var row in rows)
        {
            var run = Map(row);
            var definition = await GetSnapshotAsync(run.Id, cancellationToken);
            if (definition is null)
                continue;

            IReadOnlyDictionary<string, JsonElement>? inputs = null;
            if (!string.IsNullOrWhiteSpace(row.ProjectInputsJson))
            {
                inputs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.ProjectInputsJson!, JsonOptions);
            }

            var deadline = ParseNullable(row.Deadline) ?? run.CreatedAt.AddMinutes(5);
            var maxSteps = row.MaxSteps > 0 ? row.MaxSteps : 10_000;
            var maxNodeVisits = row.MaxNodeVisits > 0 ? row.MaxNodeVisits : 1_000;
            pending.Add(new PendingFlowRun(run, definition, new FlowRunExecutionOptions(inputs, deadline, maxSteps, maxNodeVisits)));
        }
        return pending;
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
        record.CreatedAt = run.CreatedAt.ToString("O");
        return await _runs.UpdateAsync(record, cancellationToken);
    }

    public FlowRun CreateWithSnapshot(FlowRun run, FlowDefinitionDto definition)
    {
        var serialized = JsonSerializer.Serialize(definition, JsonOptions);
        return _unitOfWork.ExecuteAsync(async cancellationToken =>
        {
            await _runs.AddAsync(new FlowRunRecord
            {
                Id = run.Id.ToString("D"),
                ProjectId = run.ProjectId == Guid.Empty ? null : run.ProjectId.ToString("D"),
                FlowId = run.FlowId.ToString("D"),
                FlowVersion = run.FlowVersion,
                Status = run.Status.ToString(),
                CreatedAt = run.CreatedAt.ToString("O")
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
        record.CreatedAt = run.CreatedAt.ToString("O");
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
            row.ErrorSummary);

    private static DateTimeOffset Parse(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    private static DateTimeOffset? ParseNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : Parse(value);

}

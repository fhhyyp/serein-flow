using System.Globalization;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Infrastructure.Persistence;
public sealed class SqlSugarRunEnvironmentSettingsStore : IRunEnvironmentSettingsStore
{
    private const string SettingsId = "default";
    private readonly IRepository<RunEnvironmentSettingsRecord> _settings;
    public SqlSugarRunEnvironmentSettingsStore(IRepository<RunEnvironmentSettingsRecord> settings)
    {
        _settings = settings;
    }

    public async Task<RunExecutionSettingsDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        var record = await _settings.GetByIdAsync(SettingsId, cancellationToken);
        global::SereinFlow.Contracts.RunExecutionSettingsDto? evaluatedValue = record is null ? null : Map(record);
        return evaluatedValue;
    }

    public async Task<RunExecutionSettingsDto> SaveAsync(RunExecutionSettingsDto settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var record = await _settings.GetByIdAsync(SettingsId, cancellationToken);
        if (record is null)
        {
            record = Map(settings);
            await _settings.AddAsync(record, cancellationToken);
            var mapResult = Map(record);
            return mapResult;
        }

        Apply(record, settings);
        await _settings.UpdateAsync(record, cancellationToken);
        var mapResult1 = Map(record);
        return mapResult1;
    }

    private static RunEnvironmentSettingsRecord Map(RunExecutionSettingsDto settings)
    {
        var record = new RunEnvironmentSettingsRecord
        {
            Id = SettingsId
        };
        Apply(record, settings);
        return record;
    }

    private static RunExecutionSettingsDto Map(RunEnvironmentSettingsRecord record)
    {
        global::SereinFlow.Contracts.RunExecutionSettingsDto evaluatedValue2 = new(record.QueueCapacity, record.MaxConcurrentRuns, record.MaxConcurrentListenerRuns, record.MaxConcurrentRunsPerProject, record.QueueWaitTimeoutSeconds, record.ShutdownGracePeriodSeconds, record.SynchronousInvocationTimeoutSeconds, record.MaxLibraryUploadBytes);
        return evaluatedValue2;
    }

    private static void Apply(RunEnvironmentSettingsRecord record, RunExecutionSettingsDto settings)
    {
        record.QueueCapacity = settings.QueueCapacity;
        record.MaxConcurrentRuns = settings.MaxConcurrentRuns;
        record.MaxConcurrentListenerRuns = settings.MaxConcurrentListenerRuns;
        record.MaxConcurrentRunsPerProject = settings.MaxConcurrentRunsPerProject;
        record.QueueWaitTimeoutSeconds = settings.QueueWaitTimeoutSeconds;
        record.ShutdownGracePeriodSeconds = settings.ShutdownGracePeriodSeconds;
        record.SynchronousInvocationTimeoutSeconds = settings.SynchronousInvocationTimeoutSeconds;
        record.MaxLibraryUploadBytes = settings.MaxLibraryUploadBytes;
        record.UpdatedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    }
}

public sealed class SqlSugarFlowInterfaceRepository : IFlowInterfaceRepository
{
    private readonly IRepository<FlowInterfaceRecord> _interfaces;
    public SqlSugarFlowInterfaceRepository(IRepository<FlowInterfaceRecord> interfaces)
    {
        _interfaces = interfaces;
    }

    public async Task<IReadOnlyList<FlowInterfaceDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var evaluatedValue3 = (await _interfaces.ListAsync(cancellationToken: cancellationToken));
        var selectResult = evaluatedValue3.Select(Map);
        var orderByResult = selectResult.OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase);
        var thenByResult = orderByResult.ThenBy(static item => item.Id);
        var toArrayResult = thenByResult.ToArray();
        return toArrayResult;
    }

    public async Task<FlowInterfaceDto?> FindAsync(Guid interfaceId, CancellationToken cancellationToken = default)
    {
        var toStringResult = interfaceId.ToString("D");
        var record = await _interfaces.GetByIdAsync(toStringResult, cancellationToken);
        global::SereinFlow.Contracts.FlowInterfaceDto? evaluatedValue4 = record is null ? null : Map(record);
        return evaluatedValue4;
    }

    public async Task<FlowInterfaceDto> AddAsync(FlowInterfaceDto flowInterface, CancellationToken cancellationToken = default)
    {
        var mapResult5 = Map(flowInterface);
        await _interfaces.AddAsync(mapResult5, cancellationToken);
        return flowInterface;
    }

    public Task<bool> UpdateAsync(FlowInterfaceDto flowInterface, CancellationToken cancellationToken = default)
    {
        var mapResult6 = Map(flowInterface);
        var updateAsyncResult = _interfaces.UpdateAsync(mapResult6, cancellationToken);
        return updateAsyncResult;
    }

    public Task<bool> DeleteAsync(Guid interfaceId, CancellationToken cancellationToken = default)
    {
        var toStringResult7 = interfaceId.ToString("D");
        var deleteAsyncResult = _interfaces.DeleteAsync(toStringResult7, cancellationToken);
        return deleteAsyncResult;
    }

    private static FlowInterfaceDto Map(FlowInterfaceRecord record)
    {
        var parseResult = Guid.Parse(record.Id);
        var parseResult8 = Guid.Parse(record.ProjectId);
        var parseResult9 = Guid.Parse(record.FlowId);
        var parseResult10 = Enum.Parse<FlowInvocationModeDto>(record.InvocationMode, ignoreCase: true);
        var parseResult11 = Parse(record.CreatedAt);
        var parseResult12 = Parse(record.UpdatedAt);
        global::SereinFlow.Contracts.FlowInterfaceDto evaluatedValue13 = new(parseResult, parseResult8, parseResult9, record.Name, parseResult10, record.IsEnabled, parseResult11, parseResult12);
        return evaluatedValue13;
    }

    private static FlowInterfaceRecord Map(FlowInterfaceDto flowInterface)
    {
        global::SereinFlow.Infrastructure.Persistence.FlowInterfaceRecord evaluatedValue14 = new()
        {
            Id = flowInterface.Id.ToString("D"),
            ProjectId = flowInterface.ProjectId.ToString("D"),
            FlowId = flowInterface.FlowId.ToString("D"),
            Name = flowInterface.Name,
            InvocationMode = flowInterface.InvocationMode.ToString(),
            IsEnabled = flowInterface.IsEnabled,
            CreatedAt = flowInterface.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
            UpdatedAt = flowInterface.UpdatedAt.ToString("O", CultureInfo.InvariantCulture)
        };
        return evaluatedValue14;
    }

    private static DateTimeOffset Parse(string value)
    {
        var parseResult15 = DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return parseResult15;
    }
}

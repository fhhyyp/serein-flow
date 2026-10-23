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
        return record is null ? null : Map(record);
    }

    public async Task<RunExecutionSettingsDto> SaveAsync(RunExecutionSettingsDto settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var record = await _settings.GetByIdAsync(SettingsId, cancellationToken);
        if (record is null)
        {
            record = Map(settings);
            await _settings.AddAsync(record, cancellationToken);
            return Map(record);
        }

        Apply(record, settings);
        await _settings.UpdateAsync(record, cancellationToken);
        return Map(record);
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
        return new(record.QueueCapacity, record.MaxConcurrentRuns, record.MaxConcurrentListenerRuns, record.MaxConcurrentRunsPerProject, record.QueueWaitTimeoutSeconds, record.ShutdownGracePeriodSeconds, record.SynchronousInvocationTimeoutSeconds, record.MaxLibraryUploadBytes);
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
        return (await _interfaces.ListAsync(cancellationToken: cancellationToken))
            .Select(Map)
            .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.Id)
            .ToArray();
    }

    public async Task<FlowInterfaceDto?> FindAsync(Guid interfaceId, CancellationToken cancellationToken = default)
    {
        var id = interfaceId.ToString("D");
        var record = await _interfaces.GetByIdAsync(id, cancellationToken);
        return record is null ? null : Map(record);
    }

    public async Task<FlowInterfaceDto> AddAsync(FlowInterfaceDto flowInterface, CancellationToken cancellationToken = default)
    {
        var entity = Map(flowInterface);
        await _interfaces.AddAsync(entity, cancellationToken);
        return flowInterface;
    }

    public Task<bool> UpdateAsync(FlowInterfaceDto flowInterface, CancellationToken cancellationToken = default)
    {
        var entity = Map(flowInterface);
        return _interfaces.UpdateAsync(entity, cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid interfaceId, CancellationToken cancellationToken = default)
    {
        var id = interfaceId.ToString("D");
        return _interfaces.DeleteAsync(id, cancellationToken);
    }

    private static FlowInterfaceDto Map(FlowInterfaceRecord record)
    {
        var id = Guid.Parse(record.Id);
        var projectId = Guid.Parse(record.ProjectId);
        var flowId = Guid.Parse(record.FlowId);
        var invocationMode = Enum.Parse<FlowInvocationModeDto>(record.InvocationMode, ignoreCase: true);
        var createdAt = Parse(record.CreatedAt);
        var updatedAt = Parse(record.UpdatedAt);
        return new(id, projectId, flowId, record.Name, invocationMode, record.IsEnabled, createdAt, updatedAt);
    }

    private static FlowInterfaceRecord Map(FlowInterfaceDto flowInterface)
    {
        return new()
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
    }

    private static DateTimeOffset Parse(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}

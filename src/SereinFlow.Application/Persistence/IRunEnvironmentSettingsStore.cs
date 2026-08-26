using SereinFlow.Contracts;

namespace SereinFlow.Application.Persistence;

/// <summary>
/// Persists the operator-managed scheduler limits without exposing a database
/// implementation to API services.
/// 保存由运维人员管理的调度器限制，不向 API 服务公开数据库实现。
/// </summary>
public interface IRunEnvironmentSettingsStore
{
    Task<RunExecutionSettingsDto?> GetAsync(CancellationToken cancellationToken = default);

    Task<RunExecutionSettingsDto> SaveAsync(
        RunExecutionSettingsDto settings,
        CancellationToken cancellationToken = default);
}

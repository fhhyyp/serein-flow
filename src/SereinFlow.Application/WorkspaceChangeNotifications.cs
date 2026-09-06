using SereinFlow.Contracts;

namespace SereinFlow.Application;

/// <summary>
/// Publishes committed workspace changes to live clients. Implementations are
/// transport-specific; the application layer only knows about the event
/// contract so API and MCP share the same notification boundary.
/// 发布已提交的工作区变化给实时客户端。具体实现由传输层负责，应用层只依赖事件合同，
/// 从而让 API 与 MCP 共用同一个通知边界。
/// </summary>
public interface IWorkspaceChangePublisher
{
    Task PublishAsync(
        WorkspaceChangeEventDto change,
        CancellationToken cancellationToken = default);
}

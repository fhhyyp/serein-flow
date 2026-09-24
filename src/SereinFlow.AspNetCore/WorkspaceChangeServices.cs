using Microsoft.AspNetCore.SignalR;
using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Api;

/// <summary>
/// SignalR transport for committed editor and library changes. The
/// application services publish only after persistence succeeds, so MCP and
/// REST mutations use the same live-update path.
/// 已提交编辑器和类库变化的 SignalR 传输层。应用服务只在持久化成功后发布，
/// 因此 MCP 与 REST 修改使用同一条实时更新通道。
/// </summary>
public sealed class WorkspaceChangePublisher : IWorkspaceChangePublisher
{
    private readonly IHubContext<WorkspaceEventsHub> _hub;
    private readonly ILogger<WorkspaceChangePublisher> _logger;

    public WorkspaceChangePublisher(
        IHubContext<WorkspaceEventsHub> hub,
        ILogger<WorkspaceChangePublisher> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task PublishAsync(
        WorkspaceChangeEventDto change,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        try
        {
            var group = change.ProjectId is { } projectId
                ? ProjectGroup(projectId)
                : GlobalGroup;
            await _hub.Clients.Group(group).SendAsync(
                "workspaceChanged",
                change,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The database commit has already completed. A disconnected live
            // client will reconcile from the authoritative GET on reconnect.
            // 数据库提交已经完成。断线客户端会在重连时通过权威 GET 对账。
            _logger.LogWarning(
                exception,
                "Workspace change push failed for {ChangeType} {ProjectId} {FlowId}.",
                change.ChangeType,
                change.ProjectId,
                change.FlowId);
        }
    }

    internal static string ProjectGroup(Guid projectId)
        => $"project:{projectId:D}";

    internal const string GlobalGroup = "workspace:global";
}

public sealed class WorkspaceEventsHub : Hub
{
    public Task SubscribeProject(Guid projectId)
        => Groups.AddToGroupAsync(
            Context.ConnectionId,
            WorkspaceChangePublisher.ProjectGroup(projectId));

    public Task UnsubscribeProject(Guid projectId)
        => Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            WorkspaceChangePublisher.ProjectGroup(projectId));

    public Task SubscribeGlobal()
        => Groups.AddToGroupAsync(Context.ConnectionId, WorkspaceChangePublisher.GlobalGroup);

    public Task UnsubscribeGlobal()
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, WorkspaceChangePublisher.GlobalGroup);
}

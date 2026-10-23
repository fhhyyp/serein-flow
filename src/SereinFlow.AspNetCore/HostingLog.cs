using SereinFlow.Domain;

namespace SereinFlow.Api;

internal static class HostingLog
{
    // Keep the existing event ID, templates and structured property names.
    // 保留既有事件编号、模板和结构化属性名。
    internal static readonly Action<ILogger, Guid, Exception?> RecoveredRunRejected = LoggerMessage.Define<Guid>(
        LogLevel.Warning, default, "Recovered run {RunId} could not be returned to the scheduler. 恢复的运行实例无法重新加入调度器。");

    internal static readonly Action<ILogger, int, Exception?> OrphanedRunsReconciled = LoggerMessage.Define<int>(
        LogLevel.Warning, default, "Reconciled {Count} orphaned running flow run(s) after engine startup. 引擎启动后已将这些孤儿运行实例标记为中断。");

    internal static readonly Action<ILogger, Guid, Guid, Guid, Exception?> RunStarting = LoggerMessage.Define<Guid, Guid, Guid>(
        LogLevel.Information, default, "Starting flow run {RunId} for project {ProjectId}, flow {FlowId}. 开始运行流程实例。");

    internal static readonly Action<ILogger, Guid, FlowRunStatus, Exception?> RunFinished = LoggerMessage.Define<Guid, FlowRunStatus>(
        LogLevel.Information, default, "Flow run {RunId} finished with status {Status}. 流程实例已结束。");

    internal static readonly Action<ILogger, Guid, Exception?> LiveRunBroadcastFailed = LoggerMessage.Define<Guid>(
        LogLevel.Warning, default, "Live run event broadcast failed for {RunId}. 实时事件广播失败。");

    internal static readonly Action<ILogger, Guid, Exception?> RunPushFailed = LoggerMessage.Define<Guid>(
        LogLevel.Warning, default, "SignalR run event push failed for {RunId}. SignalR 运行事件推送失败。");

    internal static readonly Action<ILogger, Guid, Exception?> TerminalRunBroadcastFailed = LoggerMessage.Define<Guid>(
        LogLevel.Warning, default, "Terminal run event broadcast failed for {RunId}. 终止事件广播失败。");

    internal static readonly Action<ILogger, Guid, Exception?> TerminalRunPushFailed = LoggerMessage.Define<Guid>(
        LogLevel.Warning, default, "Terminal SignalR event push failed for {RunId}. 终止 SignalR 事件推送失败。");

    internal static readonly Action<ILogger, Exception?> ApiRequestFailed = LoggerMessage.Define(
        LogLevel.Error, default, "Unhandled SereinFlow API request failure. SereinFlow API 请求发生未处理的错误。");

    internal static readonly Action<ILogger, string, Guid?, Guid?, Exception?> WorkspaceChangePushFailed = LoggerMessage.Define<string, Guid?, Guid?>(
        LogLevel.Warning, default, "Workspace change push failed for {ChangeType} {ProjectId} {FlowId}. 工作区变更推送失败。");
}

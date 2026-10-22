using SereinFlow.Contracts;

namespace SereinFlow.Mcp.Integration;

/// <summary>
/// SereinFlow permission and audit policy consumed by the host middleware.
/// 宿主中间件使用的 SereinFlow 权限与审计策略。
/// </summary>
public interface ISereinFlowMcpToolMetadata : IMcpToolMetadata
{
    SereinFlowPermission[] Permissions { get; }
    bool HasAuditTrack { get; }
    FlowVersionTrackDto AuditTrack { get; }
}

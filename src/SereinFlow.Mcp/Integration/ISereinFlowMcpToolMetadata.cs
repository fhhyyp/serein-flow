using SereinFlow.Contracts;

namespace SereinFlow.Mcp.Integration;

/// <summary>SereinFlow permission and audit policy consumed by the host middleware.</summary>
public interface ISereinFlowMcpToolMetadata : IMcpToolMetadata
{
    McpPermissionDto[] Permissions { get; }
    bool HasAuditTrack { get; }
    FlowVersionTrackDto AuditTrack { get; }
}

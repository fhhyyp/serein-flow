namespace SereinFlow.Contracts;
/// <summary>
/// Identifies where MCP guidance content was loaded from.
/// 标识 MCP 指南内容的加载来源。
/// </summary>
public enum McpAiGuidanceSourceDto
{
    /// <summary>
    /// Indicates the embedded value.
    /// 表示“内置”这一取值。
    /// </summary>
    Embedded,
    /// <summary>
    /// Indicates the disk value.
    /// 表示“磁盘”这一取值。
    /// </summary>
    Disk,
}

/// <summary>
/// Defines the API data contract for MCP AI guidance resource.
/// 定义MCPAI指南资源的 API 数据契约。
/// </summary>
public sealed record McpAiGuidanceResourceDto(string Key, string Uri, string Name, string Description, McpAiGuidanceSourceDto Source);
/// <summary>
/// Defines the API data contract for MCP AI guidance content.
/// 定义MCPAI指南Content的 API 数据契约。
/// </summary>
public sealed record McpAiGuidanceContentDto(McpAiGuidanceResourceDto Resource, string Content, string Revision, long MaxBytes);
/// <summary>
/// Carries request data for save MCP AI guidance operations.
/// 承载SaveMCPAI指南操作的请求数据。
/// </summary>
public sealed record SaveMcpAiGuidanceRequestDto(string Content, string ExpectedRevision);
/// <summary>
/// Carries request data for reset MCP AI guidance operations.
/// 承载重置MCPAI指南操作的请求数据。
/// </summary>
public sealed record ResetMcpAiGuidanceRequestDto(string ExpectedRevision);

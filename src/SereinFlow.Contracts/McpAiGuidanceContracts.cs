namespace SereinFlow.Contracts;
public enum McpAiGuidanceSourceDto
{
    Embedded,
    Disk,
}

public sealed record McpAiGuidanceResourceDto(string Key, string Uri, string Name, string Description, McpAiGuidanceSourceDto Source);
public sealed record McpAiGuidanceContentDto(McpAiGuidanceResourceDto Resource, string Content, string Revision, long MaxBytes);
public sealed record SaveMcpAiGuidanceRequestDto(string Content, string ExpectedRevision);
public sealed record ResetMcpAiGuidanceRequestDto(string ExpectedRevision);

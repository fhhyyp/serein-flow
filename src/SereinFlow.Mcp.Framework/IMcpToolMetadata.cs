namespace SereinFlow.Mcp.Framework;

/// <summary>Runtime policy contract implemented by compile-time generated declarations.</summary>
public interface IMcpToolMetadata
{
    string Name { get; }
    string Description { get; }
    McpToolExecutionKind ExecutionKind { get; }
    bool RequiresIdempotencyKey { get; }
}

public enum McpToolExecutionKind
{
    Read,
    Mutation,
}

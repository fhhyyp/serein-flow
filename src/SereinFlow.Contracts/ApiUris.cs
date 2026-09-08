namespace SereinFlow.Contracts;

/// <summary>
/// Canonical relative HTTP links emitted by API DTOs. This is separate from
/// MCP resource URIs because it describes the HTTP representation of data.
/// </summary>
public static class SereinFlowApiUris
{
    public static string RunWorkpiece(Guid runId, string workpieceId)
        => $"/api/runs/{runId:D}/workpieces/{Uri.EscapeDataString(workpieceId)}";
}

namespace SereinFlow.Contracts;
/// <summary>
/// Canonical relative HTTP links emitted by API DTOs. This is separate from
/// MCP resource URIs because it describes the HTTP representation of data.
/// API DTO 返回的规范相对 HTTP 链接；与 MCP 资源 URI 分开定义，用于描述数据的 HTTP 表示。
/// </summary>
public static class SereinFlowApiUris
{
    public static string RunWorkpiece(Guid runId, string workpieceId)
    {
        var evaluatedValue = $"/api/runs/{runId:D}/workpieces/{Uri.EscapeDataString(workpieceId)}";
        return evaluatedValue;
    }
}

using System.Collections.Concurrent;
using System.Text.Json;

namespace SereinFlow.Mcp.Framework;

/// <summary>
/// Serializes tools requiring persistent idempotency, without storing replay results.
/// 串行执行需要持久化幂等性的工具，不存储重放结果。
/// </summary>
public sealed class McpToolMutationGate
{
    private static readonly McpParameterOptions KeyParameter = new()
    {
        Required = true,
        Trim = true,
        ErrorMessage = "MCP parameter 'idempotencyKey' is required. 必须提供 MCP 参数 idempotencyKey。"
    };
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    public async Task<IDisposable?> AcquireAsync(McpToolDefinition tool, JsonElement arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (!tool.Metadata.RequiresIdempotencyKey) return null;
        arguments = McpRequestBinder.Normalize(arguments);
        if (arguments.ValueKind != JsonValueKind.Object)
            throw new McpParameterBindingException("MCP tool arguments must be a JSON object. MCP 工具参数必须是 JSON 对象。", "$");
        McpArgumentReader.Require(arguments, "idempotencyKey", KeyParameter);
        _ = McpArgumentReader.ReadString(arguments.GetProperty("idempotencyKey"), "idempotencyKey", KeyParameter);
        var gate = _gates.GetOrAdd(tool.Descriptor.Name, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new SemaphoreLease(gate);
    }

    private sealed class SemaphoreLease(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) semaphore.Release();
        }
    }
}

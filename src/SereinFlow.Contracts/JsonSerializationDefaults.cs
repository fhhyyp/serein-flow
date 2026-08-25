using System.Text.Encodings.Web;
using System.Text.Json;

namespace SereinFlow.Contracts;

/// <summary>
/// Shared JSON defaults for API, persistence and Worker payloads. Keeping
/// Unicode characters readable is important for diagnostics, event payloads
/// and the SQLite snapshots inspected by operators.
/// API、持久化和 Worker 载荷共用的 JSON 默认设置。保留 Unicode 字符可读，便于
/// 运维人员直接查看诊断信息、运行事件载荷和 SQLite 快照。
/// </summary>
public static class SereinJsonSerialization
{
    public static JsonSerializerOptions CreateWebOptions(Action<JsonSerializerOptions>? configure = null)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        configure?.Invoke(options);
        return options;
    }
}

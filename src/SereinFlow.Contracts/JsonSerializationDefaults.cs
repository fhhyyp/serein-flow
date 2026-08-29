using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>
    /// JSON options for public SereinFlow contracts. Enum values use stable
    /// camelCase strings on the wire while the built-in converter still accepts
    /// numeric values for backward compatibility.
    /// 面向公开 SereinFlow 合同的 JSON 选项。枚举在传输层使用稳定的 camelCase
    /// 字符串，同时保留内置转换器对旧版数值枚举的读取兼容性。
    /// </summary>
    public static JsonSerializerOptions CreateContractOptions(Action<JsonSerializerOptions>? configure = null)
        => CreateWebOptions(options =>
        {
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            configure?.Invoke(options);
        });
}

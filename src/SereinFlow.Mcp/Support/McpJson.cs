using System.Text.Json;
using SereinFlow.Contracts;

namespace SereinFlow.Mcp.Support;

internal static class McpJson
{
    internal static readonly JsonSerializerOptions ContractJsonOptions = SereinJsonSerialization.CreateContractOptions(options =>
    {
        var mcpPermissionJsonConverter = new SereinFlowPermissionJsonConverter();
        options.Converters.Insert(0, mcpPermissionJsonConverter);
    });
    internal static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, ContractJsonOptions);
    }

    internal static JsonElement DeserializeStoredResponse(string responseJson)
    {
        return JsonSerializer.Deserialize<JsonElement>(responseJson, ContractJsonOptions);
    }
}

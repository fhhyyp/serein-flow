using System.Text.Json;
using SereinFlow.Application;
using SereinFlow.Mcp.Tools;

namespace SereinFlow.Mcp.Integration;

/// <summary>Host wire compatibility normalization, before the flow business handler is invoked.</summary>
internal sealed class FlowPatchRequestBinder(FlowPatchContractNormalizer normalizer) : IMcpRequestBinder<NormalizedFlowPatchRequest>
{
    public NormalizedFlowPatchRequest Bind(JsonElement arguments, JsonSerializerOptions options)
    {
        try { return normalizer.Normalize(arguments); }
        catch (FlowPatchContractException exception) { throw McpToolSupport.InvalidFlowPatchContract(exception); }
    }
}

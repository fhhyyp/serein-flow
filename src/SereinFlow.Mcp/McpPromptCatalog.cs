using System.Collections.Generic;
using System.Text.Json;

namespace SereinFlow.Mcp;
internal static class McpPromptCatalog
{
    private const int MaxRequestLength = 4_000;
    public static IReadOnlyList<McpPromptDescriptor> Descriptors { get; } = createDescriptorsInitialValue();

    public static async Task<McpPromptResult> GetAsync(string name, JsonElement arguments, McpAiGuidanceProvider guidanceProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(guidanceProvider);
        var request = GetRequest(arguments, name);
        IReadOnlyList<McpAiGuidanceId> guidanceIds = name switch
        {
            "sereinflow.inspect" => [McpAiGuidanceId.SereinFlowProjects],
            "sereinflow.edit-flow" => [McpAiGuidanceId.SereinFlowFlows, McpAiGuidanceId.SereinFlowUiUx],
            "sereinflow.debug-run" => [McpAiGuidanceId.SereinFlowRuntime, McpAiGuidanceId.SereinFlowWorkpieces],
            "sereinflow.publish-flow" => [McpAiGuidanceId.SereinFlowRelease],
            "sereinflow.package-library" => [McpAiGuidanceId.LibraryBuild, McpAiGuidanceId.LibraryZip, McpAiGuidanceId.LibraryMetadata],
            "sereinflow.upgrade-library" => [McpAiGuidanceId.LibraryUpgrade],
            "sereinlang.compile" => [McpAiGuidanceId.SereinLangSyntax],
            _ => throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, $"The SereinFlow prompt '{name}' is not supported.")};
        var selectResult = guidanceIds.Select(id =>
        {
            var readAsyncResult = guidanceProvider.ReadAsync(id, cancellationToken);
            return readAsyncResult;
        });
        var guidance = await Task.WhenAll(selectResult);
        var selectResult1 = guidance.Select(resource =>
        {
            var evaluatedValue = resource.Value as string ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The SereinFlow AI guidance is not available.");
            return evaluatedValue;
        });
        var guidanceText = string.Join("\n\n", selectResult1);
        var description = name switch
        {
            "sereinflow.inspect" => "SereinFlow project inspection",
            "sereinflow.edit-flow" => "SereinFlow flow edit preview",
            "sereinflow.debug-run" => "SereinFlow run and debug inspection",
            "sereinflow.publish-flow" => "SereinFlow publish or rollback preview",
            "sereinflow.package-library" => "SereinFlow source-to-package library workflow",
            "sereinflow.upgrade-library" => "SereinFlow project library upgrade workflow",
            "sereinlang.compile" => "SereinLang standalone syntax compilation workflow",
            _ => throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, $"The SereinFlow prompt '{name}' is not supported.")};
        var resultResult = Result(description, $"""
            SereinFlow workflow: {name}
            User request: {request}

            Apply the selected server-provided skills below to this request.
            The skills are loaded from the SereinFlow service at prompt request
            time and are authoritative for this capability. Unrelated skills
            are intentionally omitted.

            --- server-provided guidance ---
            {guidanceText}
            --- end server-provided guidance ---
            """);
        return resultResult;
    }

    private static McpPromptResult Result(string description, string text)
    {
        IReadOnlyList<McpPromptMessage> evaluatedValue2 = [resolveMcpPromptMessage()];
        McpPromptResult evaluatedValue3 = new(description, evaluatedValue2);
        return evaluatedValue3;
        McpPromptMessage resolveMcpPromptMessage()
        {
            var trimResult = text.Trim();
            var mcpPromptContent = new McpPromptContent("text", trimResult);
            var mcpPromptMessage = new McpPromptMessage("user", mcpPromptContent);
            return mcpPromptMessage;
        }
    }

    private static string GetRequest(JsonElement arguments, string promptName)
    {
        if (arguments.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return "No additional request was supplied.";
        }

        if (arguments.ValueKind != JsonValueKind.Object)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "MCP prompt arguments must be a JSON object.");
        }

        if (!arguments.TryGetProperty("request", out var request) || request.ValueKind == JsonValueKind.Null || request.ValueKind == JsonValueKind.Undefined)
        {
            if (string.Equals(promptName, "sereinflow.inspect", StringComparison.Ordinal))
            {
                return "No additional request was supplied.";
            }

            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "MCP prompt argument 'request' is required.");
        }

        if (request.ValueKind != JsonValueKind.String || resolveIsNullOrWhiteSpaceResult())
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "MCP prompt argument 'request' must be a non-empty string.");
        }

        var evaluatedValue4 = request.GetString()!;
        var value = evaluatedValue4.Trim();
        if (value.Length > MaxRequestLength)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "MCP prompt argument 'request' exceeds the configured length limit.");
        }

        return value;
        bool resolveIsNullOrWhiteSpaceResult()
        {
            var getStringResult = request.GetString();
            var isNullOrWhiteSpaceResult = string.IsNullOrWhiteSpace(getStringResult);
            return isNullOrWhiteSpaceResult;
        }
    }

    private static IReadOnlyList<McpPromptDescriptor> createDescriptorsInitialValue()
    {
        IReadOnlyList<McpPromptDescriptor> descriptorsInitialValue = [resolveevaluatedValue(), resolveevaluatedValue6(), resolveevaluatedValue9(), resolveevaluatedValue12(), resolveevaluatedValue15(), resolveevaluatedValue18(), resolveevaluatedValue21()];
        return descriptorsInitialValue;
        McpPromptDescriptor resolveevaluatedValue()
        {
            IReadOnlyList<McpPromptArgumentDescriptor> evaluatedValue1 = [new("request", "Optional project discovery or inspection request.")];
            McpPromptDescriptor evaluatedValue5 = new("sereinflow.inspect", "Inspect project discovery and project-level read state.", evaluatedValue1);
            return evaluatedValue5;
        }

        McpPromptDescriptor resolveevaluatedValue6()
        {
            IReadOnlyList<McpPromptArgumentDescriptor> evaluatedValue7 = [new("request", "The requested flow change.", Required: true)];
            McpPromptDescriptor evaluatedValue8 = new("sereinflow.edit-flow", "Prepare and apply a validated flow edit using one task-level authorization and the preview gate.", evaluatedValue7);
            return evaluatedValue8;
        }

        McpPromptDescriptor resolveevaluatedValue9()
        {
            IReadOnlyList<McpPromptArgumentDescriptor> evaluatedValue10 = [new("request", "The runtime or debugging question.", Required: true)];
            McpPromptDescriptor evaluatedValue11 = new("sereinflow.debug-run", "Inspect a run or debug session with bounded, current state diagnostics.", evaluatedValue10);
            return evaluatedValue11;
        }

        McpPromptDescriptor resolveevaluatedValue12()
        {
            IReadOnlyList<McpPromptArgumentDescriptor> evaluatedValue13 = [new("request", "The requested release or rollback operation.", Required: true)];
            McpPromptDescriptor evaluatedValue14 = new("sereinflow.publish-flow", "Review production impact and prepare a publish or rollback preview.", evaluatedValue13);
            return evaluatedValue14;
        }

        McpPromptDescriptor resolveevaluatedValue15()
        {
            IReadOnlyList<McpPromptArgumentDescriptor> evaluatedValue16 = [new("request", "The source-to-package library request; excludes import or attachment.", Required: true)];
            McpPromptDescriptor evaluatedValue17 = new("sereinflow.package-library", "Prepare source code as a SereinFlow library package using SereinFlow.Library from NuGet.org.", evaluatedValue16);
            return evaluatedValue17;
        }

        McpPromptDescriptor resolveevaluatedValue18()
        {
            IReadOnlyList<McpPromptArgumentDescriptor> evaluatedValue19 = [new("request", "The requested project library upgrade.", Required: true)];
            McpPromptDescriptor evaluatedValue20 = new("sereinflow.upgrade-library", "Inspect a project library family and prepare a compatible per-flow library upgrade through the preview gate.", evaluatedValue19);
            return evaluatedValue20;
        }

        McpPromptDescriptor resolveevaluatedValue21()
        {
            IReadOnlyList<McpPromptArgumentDescriptor> evaluatedValue22 = [new("request", "The SereinLang syntax compilation request; excludes imports and host APIs.", Required: true)];
            McpPromptDescriptor evaluatedValue23 = new("sereinlang.compile", "Compile a standalone SereinLang lexical or expression syntax draft and inspect its structured diagnostics.", evaluatedValue22);
            return evaluatedValue23;
        }
    }
}

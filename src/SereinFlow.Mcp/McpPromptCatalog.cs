using System.Text.Json;

namespace SereinFlow.Mcp;

internal static class McpPromptCatalog
{
    private const int MaxRequestLength = 4_000;
    private static readonly IReadOnlyList<PromptDefinition> Definitions =
    [
        new("sereinflow.inspect", "Inspect project discovery and project-level read state.",
            "SereinFlow project inspection", "Optional project discovery or inspection request.", false,
            [McpAiGuidanceId.SereinFlowProjects]),
        new("sereinflow.edit-flow", "Prepare and apply a validated flow edit using one task-level authorization and the preview gate.",
            "SereinFlow flow edit preview", "The requested flow change.", true,
            [McpAiGuidanceId.SereinFlowFlows, McpAiGuidanceId.SereinFlowUiUx]),
        new("sereinflow.debug-run", "Inspect a run or debug session with bounded, current state diagnostics.",
            "SereinFlow run and debug inspection", "The runtime or debugging question.", true,
            [McpAiGuidanceId.SereinFlowRuntime, McpAiGuidanceId.SereinFlowWorkpieces]),
        new("sereinflow.publish-flow", "Review production impact and prepare a publish or rollback preview.",
            "SereinFlow publish or rollback preview", "The requested release or rollback operation.", true,
            [McpAiGuidanceId.SereinFlowRelease]),
        new("sereinflow.package-library", "Prepare source code as a SereinFlow library package using SereinFlow.Library from NuGet.org.",
            "SereinFlow source-to-package library workflow", "The source-to-package library request; excludes import or attachment.", true,
            [McpAiGuidanceId.LibraryBuild, McpAiGuidanceId.LibraryZip, McpAiGuidanceId.LibraryMetadata]),
        new("sereinflow.upgrade-library", "Inspect a project library family and prepare a compatible per-flow library upgrade through the preview gate.",
            "SereinFlow project library upgrade workflow", "The requested project library upgrade.", true,
            [McpAiGuidanceId.LibraryUpgrade]),
        new("sereinlang.compile", "Compile a standalone SereinLang lexical or expression syntax draft and inspect its structured diagnostics.",
            "SereinLang standalone syntax compilation workflow", "The SereinLang syntax compilation request; excludes imports and host APIs.", true,
            [McpAiGuidanceId.SereinLangSyntax])
    ];
    private static readonly Dictionary<string, PromptDefinition> ByName =
        Definitions.ToDictionary(static definition => definition.Name, StringComparer.Ordinal);
    public static IReadOnlyList<McpPromptDescriptor> Descriptors { get; } = Definitions
        .Select(static definition => new McpPromptDescriptor(definition.Name, definition.Description,
            [new("request", definition.RequestDescription, definition.RequestRequired)]))
        .ToArray();

    public static async Task<McpPromptResult> GetAsync(string name, JsonElement arguments, McpAiGuidanceProvider guidanceProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(guidanceProvider);
        // Keep argument validation ahead of the unsupported-name error for compatibility.
        var definition = name is not null && ByName.TryGetValue(name, out var registered) ? registered : null;
        var requestRequired = definition?.RequestRequired ?? true;
        var request = GetRequest(arguments, requestRequired);
        if (definition is null)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, $"The SereinFlow prompt '{name}' is not supported.");
        }

        var guidance = await guidanceProvider.ReadManyAsync(definition.GuidanceIds, cancellationToken);
        var guidanceText = string.Join("\n\n", guidance.Select(static resource =>
            resource.Value as string ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The SereinFlow AI guidance is not available.")));
        return Result(definition.ResultDescription, $"""
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
    }

    private static McpPromptResult Result(string description, string text)
    {
        return new McpPromptResult(description,
            [new McpPromptMessage("user", new McpPromptContent("text", text.Trim()))]);
    }

    private static string GetRequest(JsonElement arguments, bool requestRequired)
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
            if (!requestRequired)
            {
                return "No additional request was supplied.";
            }

            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "MCP prompt argument 'request' is required.");
        }

        if (request.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(request.GetString()))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "MCP prompt argument 'request' must be a non-empty string.");
        }

        var value = request.GetString()!.Trim();
        if (value.Length > MaxRequestLength)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "MCP prompt argument 'request' exceeds the configured length limit.");
        }

        return value;
    }

    private sealed record PromptDefinition(
        string Name,
        string Description,
        string ResultDescription,
        string RequestDescription,
        bool RequestRequired,
        IReadOnlyList<McpAiGuidanceId> GuidanceIds);
}

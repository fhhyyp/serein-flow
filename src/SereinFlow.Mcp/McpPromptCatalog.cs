using System.Text.Json;

namespace SereinFlow.Mcp;

internal static class McpPromptCatalog
{
    private const int MaxRequestLength = 4_000;

    public static IReadOnlyList<McpPromptDescriptor> Descriptors { get; } =
    [
        new(
            "sereinflow.inspect",
            "Inspect project discovery and project-level read state.",
            [new("request", "Optional project discovery or inspection request.")]),
        new(
            "sereinflow.edit-flow",
            "Prepare and apply a validated flow edit using one task-level authorization and the preview gate.",
            [new("request", "The requested flow change.", Required: true)]),
        new(
            "sereinflow.debug-run",
            "Inspect a run or debug session with bounded, current state diagnostics.",
            [new("request", "The runtime or debugging question.", Required: true)]),
        new(
            "sereinflow.publish-flow",
            "Review production impact and prepare a publish or rollback preview.",
            [new("request", "The requested release or rollback operation.", Required: true)]),
        new(
            "sereinflow.package-library",
            "Prepare source code as a SereinFlow library package using SereinFlow.Library from NuGet.org.",
            [new("request", "The source-to-package library request; excludes import or attachment.", Required: true)]),
        new(
            "sereinflow.upgrade-library",
            "Inspect a project library family and prepare a compatible per-flow library upgrade through the preview gate.",
            [new("request", "The requested project library upgrade.", Required: true)]),
        new(
            "sereinlang.compile",
            "Compile a standalone SereinLang lexical or expression syntax draft and inspect its structured diagnostics.",
            [new("request", "The SereinLang syntax compilation request; excludes imports and host APIs.", Required: true)])
    ];

    public static async Task<McpPromptResult> GetAsync(
        string name,
        JsonElement arguments,
        McpAiGuidanceProvider guidanceProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(guidanceProvider);
        var request = GetRequest(arguments, name);
        IReadOnlyList<string> guidanceUris = name switch
        {
            "sereinflow.inspect" => [McpAiGuidance.SereinFlowProjectsResourceUri],
            "sereinflow.edit-flow" => [McpAiGuidance.SereinFlowFlowsResourceUri],
            "sereinflow.debug-run" => [McpAiGuidance.SereinFlowRuntimeResourceUri],
            "sereinflow.publish-flow" => [McpAiGuidance.SereinFlowReleaseResourceUri],
            "sereinflow.package-library" =>
            [
                McpAiGuidance.LibraryBuildResourceUri,
                McpAiGuidance.LibraryZipResourceUri,
                McpAiGuidance.LibraryMetadataResourceUri
            ],
            "sereinflow.upgrade-library" => [McpAiGuidance.LibraryUpgradeResourceUri],
            "sereinlang.compile" => [McpAiGuidance.SereinLangSyntaxResourceUri],
            _ => throw new McpProtocolException(-32602, $"The SereinFlow prompt '{name}' is not supported.")
        };
        var guidance = await Task.WhenAll(guidanceUris.Select(uri => guidanceProvider.ReadAsync(uri, cancellationToken)));
        var guidanceText = string.Join(
            "\n\n",
            guidance.Select(resource => resource.Value as string
                ?? throw new McpProtocolException(-32004, "The SereinFlow AI guidance is not available.")));
        var description = name switch
        {
            "sereinflow.inspect" => "SereinFlow project inspection",
            "sereinflow.edit-flow" => "SereinFlow flow edit preview",
            "sereinflow.debug-run" => "SereinFlow run and debug inspection",
            "sereinflow.publish-flow" => "SereinFlow publish or rollback preview",
            "sereinflow.package-library" => "SereinFlow source-to-package library workflow",
            "sereinflow.upgrade-library" => "SereinFlow project library upgrade workflow",
            "sereinlang.compile" => "SereinLang standalone syntax compilation workflow",
            _ => throw new McpProtocolException(-32602, $"The SereinFlow prompt '{name}' is not supported.")
        };

        return Result(
            description,
            $"""
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
        => new(
            description,
            [new McpPromptMessage("user", new McpPromptContent("text", text.Trim()))]);

    private static string GetRequest(JsonElement arguments, string promptName)
    {
        if (arguments.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return "No additional request was supplied.";
        if (arguments.ValueKind != JsonValueKind.Object)
            throw new McpProtocolException(-32602, "MCP prompt arguments must be a JSON object.");
        if (!arguments.TryGetProperty("request", out var request)
            || request.ValueKind == JsonValueKind.Null
            || request.ValueKind == JsonValueKind.Undefined)
        {
            if (string.Equals(promptName, "sereinflow.inspect", StringComparison.Ordinal))
                return "No additional request was supplied.";
            throw new McpProtocolException(-32602, "MCP prompt argument 'request' is required.");
        }
        if (request.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(request.GetString()))
            throw new McpProtocolException(-32602, "MCP prompt argument 'request' must be a non-empty string.");

        var value = request.GetString()!.Trim();
        if (value.Length > MaxRequestLength)
            throw new McpProtocolException(-32602, "MCP prompt argument 'request' exceeds the configured length limit.");
        return value;
    }
}

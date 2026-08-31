using System.Text.Json;

namespace SereinFlow.Mcp;

internal static class McpPromptCatalog
{
    private const int MaxRequestLength = 4_000;

    public static IReadOnlyList<McpPromptDescriptor> Descriptors { get; } =
    [
        new(
            "sereinflow.inspect",
            "Inspect SereinFlow state and recommend the smallest safe next step.",
            [new("request", "Optional user intent to classify.")]),
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
            "Prepare a SereinFlow library package using SereinFlow.Library from NuGet.org for server-side preview inspection.",
            [new("request", "The library or packaging request.", Required: true)]),
        new(
            "sereinlang.compile",
            "Compile a SereinLang source draft and inspect only its structured diagnostics.",
            [new("request", "The SereinLang authoring or compilation request.", Required: true)])
    ];

    public static async Task<McpPromptResult> GetAsync(
        string name,
        JsonElement arguments,
        McpAiGuidanceProvider guidanceProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(guidanceProvider);
        var request = GetRequest(arguments, name);
        var guidanceUri = name switch
        {
            "sereinflow.inspect" or "sereinflow.edit-flow" or "sereinflow.debug-run" or "sereinflow.publish-flow"
                => McpAiGuidance.SereinFlowResourceUri,
            "sereinflow.package-library" => McpAiGuidance.LibraryPackageResourceUri,
            "sereinlang.compile" => McpAiGuidance.SereinLangResourceUri,
            _ => throw new McpProtocolException(-32602, $"The SereinFlow prompt '{name}' is not supported.")
        };
        var guidance = await guidanceProvider.ReadAsync(guidanceUri, cancellationToken);
        var guidanceText = guidance.Value as string
            ?? throw new McpProtocolException(-32004, "The SereinFlow AI guidance is not available.");
        var description = name switch
        {
            "sereinflow.inspect" => "SereinFlow read-only inspection",
            "sereinflow.edit-flow" => "SereinFlow flow edit preview",
            "sereinflow.debug-run" => "SereinFlow run and debug inspection",
            "sereinflow.publish-flow" => "SereinFlow publish or rollback preview",
            "sereinflow.package-library" => "SereinFlow library package workflow",
            "sereinlang.compile" => "SereinLang compilation workflow",
            _ => throw new McpProtocolException(-32602, $"The SereinFlow prompt '{name}' is not supported.")
        };

        return Result(
            description,
            $"""
            SereinFlow workflow: {name}
            User request: {request}

            Apply the selected server-provided skill below to this request.
            The skill is loaded from the SereinFlow service at prompt request
            time and is authoritative for this capability. Unrelated skills
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

using System.Text;
using System.Text.Json;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using Arguments = SereinFlow.Mcp.Framework.McpArgumentReader;

namespace SereinFlow.Mcp.Integration;

/// <summary>Projects a completed tool call into the SereinFlow audit storage contract.</summary>
public sealed class SereinFlowMcpAuditProjection(IMcpAuditStore auditStore, IMcpPreviewStore? previewStore = null)
{
    public async Task RecordAsync(McpToolCompletion completion, McpPrincipal? principal)
    {
        var context = completion.Context;
        var arguments = context.Arguments;
        var metadata = context.Tool?.Metadata as ISereinFlowMcpToolMetadata;
        var rawArguments = arguments.ValueKind == JsonValueKind.Undefined ? string.Empty : arguments.GetRawText();
        var idempotencyKey = Arguments.TryString(arguments, "idempotencyKey");
        var inputHash = McpIdempotencyService.HashKey(string.IsNullOrWhiteSpace(idempotencyKey) ? rawArguments : idempotencyKey);
        var inputBytes = Encoding.UTF8.GetByteCount(rawArguments);
        var outputBytes = completion.Value is null ? 0 : Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(completion.Value, Support.McpJson.ContractJsonOptions));
        var previewId = Arguments.TryGuid(arguments, "previewId");
        var preview = previewId is null ? null : await (previewStore
            ?? throw new InvalidOperationException("A preview store is required for preview audit projection.")).FindAsync(previewId.Value, CancellationToken.None);
        var projectId = Arguments.TryGuid(arguments, "projectId") ?? preview?.ProjectId;
        var flowId = Arguments.TryGuid(arguments, "flowId") ?? preview?.FlowId;
        var defaultTrack = metadata?.HasAuditTrack == true ? metadata.AuditTrack : (FlowVersionTrackDto?)null;
        var track = Arguments.TryEnum<FlowVersionTrackDto>(arguments, "track") ?? defaultTrack ?? GetPreviewTrack(preview);
        var flowVersion = Arguments.TryInt64(arguments, "expectedDevelopmentVersion") ?? Arguments.TryInt64(arguments, "expectedHeadVersion")
            ?? Arguments.TryInt64(arguments, "sourceVersion") ?? Arguments.TryInt64(arguments, "version") ?? preview?.ExpectedVersion;
        var entry = new McpAuditEntry(Guid.NewGuid(), principal?.Id ?? "anonymous", context.Name, projectId, flowId,
            track, flowVersion, previewId, completion.Outcome, inputHash, completion.Summary, DateTimeOffset.UtcNow,
            Math.Max(0, (long)completion.Duration.TotalMilliseconds), inputBytes, outputBytes);
        await auditStore.AddAsync(entry, CancellationToken.None);
    }

    private static FlowVersionTrackDto? GetPreviewTrack(McpPreviewEntry? preview)
    {
        if (preview is null) return null;
        if (string.Equals(preview.Operation, FlowErrorCodes.Patch, StringComparison.Ordinal)
            || string.Equals(preview.Operation, "flow.publish", StringComparison.Ordinal)) return FlowVersionTrackDto.Development;
        if (!string.Equals(preview.Operation, "flow.rollback", StringComparison.Ordinal)) return null;
        try
        {
            using var document = JsonDocument.Parse(preview.PayloadJson);
            return document.RootElement.TryGetProperty("request", out var request)
                ? Arguments.TryEnum<FlowVersionTrackDto>(request, "track") : null;
        }
        catch (JsonException) { return null; }
    }
}

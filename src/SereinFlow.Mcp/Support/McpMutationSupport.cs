using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Mcp.Support;

internal static class McpMutationSupport
{
    internal static void RequireConfirmation(McpMutationApplyRequestDto request)
    {
        if (!string.Equals(request.Confirmation, "APPLY", StringComparison.Ordinal))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The confirmation value must be APPLY.");
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The idempotency key is required.");
        }
    }

    internal static async Task MarkPreviewAppliedAsync(McpPreviewService previews, McpPreviewEntry entry, CancellationToken cancellationToken)
    {
        if (await previews.MarkAppliedAsync(entry, cancellationToken))
        {
            return;
        }

        throw new McpProtocolException(McpProtocolErrorCodes.InternalError, "The mutation was committed but its MCP preview state could not be recorded.", new { code = McpErrorCodes.PreviewStatePersistFailed });
    }

    internal static bool IsPreviewPending(McpPreviewEntry entry) => entry.Status == McpMutationPreviewStatusDto.Pending && entry.ExpiresAt > DateTimeOffset.UtcNow;
}

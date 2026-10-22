using System.Text.Json;
using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Mcp.Support;

internal static class McpErrors
{
    internal static McpProtocolException InvalidPatchValue(JsonException exception)
    {
        var path = string.IsNullOrWhiteSpace(exception.Path) ? null : exception.Path;
        var location = path is null ? string.Empty : $" at '{path}'";
        return new McpProtocolException(McpProtocolErrorCodes.InvalidParams, $"The flow patch value is invalid{location}. Use camelCase enum strings such as 'action' and 'data'; legacy numeric enum values are also accepted.", new { code = McpErrorCodes.InvalidPatchValue, path });
    }

    internal static McpProtocolException InvalidFlowPatchContract(FlowPatchContractException exception)
    {
        return new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The flow patch contract is invalid.", new { code = exception.Code, diagnosticId = exception.DiagnosticId, fieldPath = exception.FieldPath, expected = exception.Expected, schemaVersion = FlowPatchContract.CurrentSchemaVersion, remediation = exception.Remediation });
    }

    internal static McpProtocolException InvalidLibraryNodeTemplate(LibraryNodeTemplateException exception)
    {
        return new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The library node template request is invalid.", new { code = exception.Code, diagnosticId = exception.DiagnosticId, fieldPath = exception.FieldPath, expected = exception.Expected, remediation = exception.Remediation, statusCode = exception.StatusCode });
    }

    internal static FlowVersionTrackDto ParseTrack(string value)
    {
        return (FlowVersionTrackDto)(Enum.TryParse<FlowVersionTrackDto>(value, true, out var track) && Enum.IsDefined(track) ? track : throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The flow version track must be development or production."));
    }

    internal static McpProtocolException VersionConflict(long? currentVersion)
    {
        return new McpProtocolException(McpProtocolErrorCodes.Conflict, "The flow version changed before the MCP mutation was applied.", new { currentVersion });
    }

    internal static void RequireValidMutation(FlowValidationResultDto validation, string message)
    {
        if (validation.IsValid)
        {
            return;
        }

        throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, message, new { code = McpErrorCodes.ValidationFailed, diagnostics = validation.Diagnostics });
    }
}

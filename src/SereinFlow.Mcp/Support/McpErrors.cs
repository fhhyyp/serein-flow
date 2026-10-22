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
        return new McpProtocolException(McpProtocolErrorCodes.InvalidParams, $"The flow patch value is invalid{location}. Use camelCase enum strings such as 'action' and 'data'; legacy numeric enum values are also accepted. 流程补丁值无效；请使用 action、data 等 camelCase 枚举字符串，也兼容旧版枚举数值。", new { code = McpErrorCodes.InvalidPatchValue, path });
    }

    internal static McpProtocolException InvalidFlowPatchContract(FlowPatchContractException exception)
    {
        return new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The flow patch contract is invalid. 流程补丁契约无效。", new { code = exception.Code, diagnosticId = exception.DiagnosticId, fieldPath = exception.FieldPath, expected = exception.Expected, schemaVersion = FlowPatchContract.CurrentSchemaVersion, remediation = exception.Remediation });
    }

    internal static McpProtocolException InvalidLibraryNodeTemplate(LibraryNodeTemplateException exception)
    {
        return new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The library node template request is invalid. 类库节点模板请求无效。", new { code = exception.Code, diagnosticId = exception.DiagnosticId, fieldPath = exception.FieldPath, expected = exception.Expected, remediation = exception.Remediation, statusCode = exception.StatusCode });
    }

    internal static FlowVersionTrackDto ParseTrack(string value)
    {
        return (FlowVersionTrackDto)(Enum.TryParse<FlowVersionTrackDto>(value, true, out var track) && Enum.IsDefined(track) ? track : throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The flow version track must be development or production. 流程版本轨道必须为 development 或 production。"));
    }

    internal static McpProtocolException VersionConflict(long? currentVersion)
    {
        return new McpProtocolException(McpProtocolErrorCodes.Conflict, "The flow version changed before the MCP mutation was applied. 应用 MCP 修改前流程版本已发生变化。", new { currentVersion });
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

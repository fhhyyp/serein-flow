using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Mcp.Tools;
/// <summary>
/// Shared parsing, protocol conversion and mutation helpers used by MCP tool
/// handlers. This module deliberately owns no tool routing or resource URI
/// dispatch.
/// </summary>
internal static class McpToolSupport
{
    internal static readonly JsonSerializerOptions ContractJsonOptions = SereinJsonSerialization.CreateContractOptions(options =>
    {
        var mcpPermissionJsonConverter = new McpPermissionJsonConverter();
        options.Converters.Insert(0, mcpPermissionJsonConverter);
    });
    internal static McpProtocolException InvalidPatchValue(JsonException exception)
    {
        var path = string.IsNullOrWhiteSpace(exception.Path) ? null : exception.Path;
        var location = path is null ? string.Empty : $" at '{path}'";
        var mcpProtocolException2 = new McpProtocolException(McpProtocolErrorCodes.InvalidParams, $"The flow patch value is invalid{location}. Use camelCase enum strings such as 'action' and 'data'; legacy numeric enum values are also accepted.", new { code = McpErrorCodes.InvalidPatchValue, path });
        return mcpProtocolException2;
    }

    internal static McpProtocolException InvalidFlowPatchContract(FlowPatchContractException exception)
    {
        McpProtocolException evaluatedValue3 = new(McpProtocolErrorCodes.InvalidParams, "The flow patch contract is invalid.", new { code = exception.Code, diagnosticId = exception.DiagnosticId, fieldPath = exception.FieldPath, expected = exception.Expected, schemaVersion = FlowPatchContract.CurrentSchemaVersion, remediation = exception.Remediation });
        return evaluatedValue3;
    }

    internal static McpProtocolException InvalidLibraryNodeTemplate(LibraryNodeTemplateException exception)
    {
        McpProtocolException evaluatedValue4 = new(McpProtocolErrorCodes.InvalidParams, "The library node template request is invalid.", new { code = exception.Code, diagnosticId = exception.DiagnosticId, fieldPath = exception.FieldPath, expected = exception.Expected, remediation = exception.Remediation, statusCode = exception.StatusCode });
        return evaluatedValue4;
    }

    internal static FlowVersionTrackDto ParseTrack(string value)
    {
        FlowVersionTrackDto evaluatedValue7 = Enum.TryParse<FlowVersionTrackDto>(value, true, out var track) && Enum.IsDefined(track) ? track : throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The flow version track must be development or production.");
        return evaluatedValue7;
    }

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

    internal static McpProtocolException VersionConflict(long? currentVersion)
    {
        McpProtocolException evaluatedValue8 = new(McpProtocolErrorCodes.Conflict, "The flow version changed before the MCP mutation was applied.", new { currentVersion });
        return evaluatedValue8;
    }

    internal static async Task<FlowValidationResultDto> ValidateExecutableFlowAsync(IServiceScope scope, Guid projectId, FlowDefinitionDto definition, CancellationToken cancellationToken)
    {
        var validation = FlowDefinitionContractValidator.ValidateForExecution(definition);
        var getRequiredServiceResult = scope.ServiceProvider.GetRequiredService<ProjectLibraryService>();
        var libraryValidation = await getRequiredServiceResult.ValidateFlowLibrariesAsync(projectId, definition, cancellationToken);
        var concatResult = validation.Diagnostics.Concat(libraryValidation.Diagnostics);
        var toArrayResult9 = concatResult.ToArray();
        var flowValidationResultDto = new FlowValidationResultDto(validation.IsValid && libraryValidation.IsValid, toArrayResult9);
        return flowValidationResultDto;
    }

    internal static void RequireValidMutation(FlowValidationResultDto validation, string message)
    {
        if (validation.IsValid)
        {
            return;
        }

        throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, message, new { code = McpErrorCodes.ValidationFailed, diagnostics = validation.Diagnostics });
    }

    internal static async Task MarkPreviewAppliedAsync(McpPreviewService previews, McpPreviewEntry entry, CancellationToken cancellationToken)
    {
        if (await previews.MarkAppliedAsync(entry, cancellationToken))
        {
            return;
        }

        throw new McpProtocolException(McpProtocolErrorCodes.InternalError, "The mutation was committed but its MCP preview state could not be recorded.", new { code = McpErrorCodes.PreviewStatePersistFailed });
    }

    internal static async Task<Project> RequireActiveProjectAsync(IServiceScope scope, McpSecurityService security, McpPrincipal principal, Guid projectId, McpPermissionDto permission, CancellationToken cancellationToken)
    {
        security.Require(principal, permission, projectId);
        var project = await resolveFindAsyncResult() ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The project was not found.");
        if (project.Status == SereinFlow.Domain.ProjectStatus.Archived)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "Archived projects cannot be changed by this MCP operation.", new { code = ProjectErrorCodes.Archived });
        }

        return project;
        Task<Project?> resolveFindAsyncResult()
        {
            var getRequiredServiceResult10 = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            var findAsyncResult = getRequiredServiceResult10.FindAsync(projectId, cancellationToken);
            return findAsyncResult;
        }
    }

    internal static string Serialize<T>(T value)
    {
        var serializeResult = JsonSerializer.Serialize(value, ContractJsonOptions);
        return serializeResult;
    }

    internal static JsonElement DeserializeStoredResponse(string responseJson)
    {
        var deserializeResult = JsonSerializer.Deserialize<JsonElement>(responseJson, ContractJsonOptions);
        return deserializeResult;
    }

    internal static bool IsProjectScoped(McpPrincipal? principal) => principal?.ProjectId is not null && !principal.IsLocal && !principal.IsAdministrator;
    internal static bool IsPreviewPending(McpPreviewEntry entry) => entry.Status == McpMutationPreviewStatusDto.Pending && entry.ExpiresAt > DateTimeOffset.UtcNow;
    internal static McpToolDescriptor Tool(string name, string description, JsonElement inputSchema)
    {
        McpToolDescriptor evaluatedValue11 = new(name, description, inputSchema);
        return evaluatedValue11;
    }

    internal static JsonElement Schema(Dictionary<string, McpJsonSchema>? properties = null, string[]? required = null)
    {
        var schema = McpJsonSchema.Object(properties, required);
        var element = JsonSerializer.SerializeToElement(schema);
        return element;
    }

    internal static McpJsonSchema StringSchema(string? description = null)
    {
        var schema = new McpJsonSchema { Type = "string", Description = description };
        return schema;
    }

    internal static McpJsonSchema StringEnumSchema(string? description, params string[] values)
    {
        var enumValues = JsonSerializer.SerializeToElement(values);
        var schema = new McpJsonSchema { Type = "string", Enum = enumValues, Description = description };
        return schema;
    }

    internal static McpJsonSchema NumberSchema()
    {
        var schema = new McpJsonSchema { Type = "integer" };
        return schema;
    }
    internal static McpJsonSchema BooleanSchema()
    {
        var schema = new McpJsonSchema { Type = "boolean" };
        return schema;
    }
    internal static AiReadModelOptions ReadOptions(McpReadOptionsRequest arguments) => new(
        arguments.MaxItems, arguments.MaxJsonBytes, arguments.IncludeFlowLiteralValues, arguments.IncludeScriptSource);

}

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Mcp;

/// <summary>
/// Shared parsing, protocol conversion and mutation helpers used by MCP tool
/// handlers. This module deliberately owns no tool routing or resource URI
/// dispatch.
/// </summary>
internal static class McpToolSupport
{
    internal static readonly JsonSerializerOptions ContractJsonOptions = SereinJsonSerialization.CreateContractOptions(options =>
    {
        options.Converters.Insert(0, new McpPermissionJsonConverter());
    });

    internal static McpPermissionDto[] ReadPermissions(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("permissions", out var value) || value.ValueKind != JsonValueKind.Array)
            throw new McpProtocolException(-32602, "MCP parameter 'permissions' must be an array.");
        var permissions = new List<McpPermissionDto>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || !McpPermissionNames.TryParse(item.GetString(), out var permission))
                throw new McpProtocolException(-32602, "MCP parameter 'permissions' contains an invalid permission.");
            permissions.Add(permission);
        }
        return permissions.Distinct().ToArray();
    }

    internal static DateTimeOffset? ReadOptionalDate(JsonElement arguments, string name)
    {
        var value = GetOptionalString(arguments, name);
        return string.IsNullOrWhiteSpace(value)
            ? null
            : DateTimeOffset.TryParse(value, out var parsed)
                ? parsed
                : throw new McpProtocolException(-32602, $"MCP parameter '{name}' must be an ISO date.");
    }

    internal static T Deserialize<T>(JsonElement arguments)
    {
        try
        {
            return arguments.Deserialize<T>(ContractJsonOptions)
                ?? throw new McpProtocolException(-32602, "MCP arguments cannot be null.");
        }
        catch (JsonException exception)
        {
            throw InvalidArguments(exception);
        }
        catch (NotSupportedException)
        {
            throw new McpProtocolException(
                -32602,
                "MCP arguments contain an unsupported contract value.",
                new { code = "mcp.invalid_arguments" });
        }
    }

    internal static McpProtocolException InvalidArguments(JsonException exception)
    {
        var path = string.IsNullOrWhiteSpace(exception.Path) ? null : exception.Path;
        var location = path is null ? string.Empty : $" at '{path}'";
        return new McpProtocolException(
            -32602,
            $"MCP arguments are invalid{location}.",
            new { code = "mcp.invalid_arguments", path });
    }

    internal static McpProtocolException InvalidPatchValue(JsonException exception)
    {
        var path = string.IsNullOrWhiteSpace(exception.Path) ? null : exception.Path;
        var location = path is null ? string.Empty : $" at '{path}'";
        return new McpProtocolException(
            -32602,
            $"The flow patch value is invalid{location}. Use camelCase enum strings such as 'action' and 'data'; legacy numeric enum values are also accepted.",
            new { code = "mcp.invalid_patch_value", path });
    }

    internal static McpProtocolException InvalidFlowPatchContract(FlowPatchContractException exception)
        => new(
            -32602,
            "The flow patch contract is invalid.",
            new
            {
                code = exception.Code,
                diagnosticId = exception.DiagnosticId,
                fieldPath = exception.FieldPath,
                expected = exception.Expected,
                schemaVersion = FlowPatchContract.CurrentSchemaVersion,
                remediation = exception.Remediation
            });

    internal static McpProtocolException InvalidLibraryNodeTemplate(LibraryNodeTemplateException exception)
        => new(
            -32602,
            "The library node template request is invalid.",
            new
            {
                code = exception.Code,
                diagnosticId = exception.DiagnosticId,
                fieldPath = exception.FieldPath,
                expected = exception.Expected,
                remediation = exception.Remediation,
                statusCode = exception.StatusCode
            });

    internal static Guid? TryGetGuid(JsonElement arguments, string name)
    {
        var value = GetOptionalString(arguments, name);
        return string.IsNullOrWhiteSpace(value)
            ? null
            : Guid.TryParse(value, out var parsed)
                ? parsed
                : throw new McpProtocolException(-32602, $"MCP parameter '{name}' must be a GUID.");
    }

    internal static long GetLong(JsonElement arguments, string name)
        => GetOptionalLong(arguments, name)
            ?? throw new McpProtocolException(-32602, $"MCP parameter '{name}' is required.");

    internal static FlowVersionTrackDto ParseTrack(string value)
        => Enum.TryParse<FlowVersionTrackDto>(value, true, out var track) && Enum.IsDefined(track)
            ? track
            : throw new McpProtocolException(-32602, "The flow version track must be development or production.");

    internal static void RequireConfirmation(McpMutationApplyRequestDto request)
    {
        if (!string.Equals(request.Confirmation, "APPLY", StringComparison.Ordinal))
            throw new McpProtocolException(-32602, "The confirmation value must be APPLY.");
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new McpProtocolException(-32602, "The idempotency key is required.");
    }

    internal static McpProtocolException VersionConflict(long? currentVersion)
        => new(-32010, "The flow version changed before the MCP mutation was applied.", new { currentVersion });

    internal static async Task<FlowValidationResultDto> ValidateExecutableFlowAsync(
        IServiceScope scope,
        Guid projectId,
        FlowDefinitionDto definition,
        CancellationToken cancellationToken)
    {
        var validation = FlowDefinitionContractValidator.ValidateForExecution(definition);
        var libraryValidation = await scope.ServiceProvider.GetRequiredService<ProjectLibraryService>()
            .ValidateFlowLibrariesAsync(projectId, definition, cancellationToken);
        return new FlowValidationResultDto(
            validation.IsValid && libraryValidation.IsValid,
            validation.Diagnostics.Concat(libraryValidation.Diagnostics).ToArray());
    }

    internal static void RequireValidMutation(FlowValidationResultDto validation, string message)
    {
        if (validation.IsValid)
            return;

        throw new McpProtocolException(
            -32011,
            message,
            new { code = "mcp.validation_failed", diagnostics = validation.Diagnostics });
    }

    internal static async Task MarkPreviewAppliedAsync(
        McpPreviewService previews,
        McpPreviewEntry entry,
        CancellationToken cancellationToken)
    {
        if (await previews.MarkAppliedAsync(entry, cancellationToken))
            return;

        throw new McpProtocolException(
            -32603,
            "The mutation was committed but its MCP preview state could not be recorded.",
            new { code = "mcp.preview_state_persist_failed" });
    }

    internal static async Task<SereinFlow.Domain.Project> RequireActiveProjectAsync(
        IServiceScope scope,
        McpSecurityService security,
        McpPrincipal principal,
        Guid projectId,
        McpPermissionDto permission,
        CancellationToken cancellationToken)
    {
        security.Require(principal, permission, projectId);
        var project = await scope.ServiceProvider.GetRequiredService<IProjectRepository>()
            .FindAsync(projectId, cancellationToken)
            ?? throw new McpProtocolException(-32004, "The project was not found.");
        if (project.Status == SereinFlow.Domain.ProjectStatus.Archived)
        {
            throw new McpProtocolException(
                -32011,
                "Archived projects cannot be changed by this MCP operation.",
                new { code = "project.archived" });
        }
        return project;
    }

    internal static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, ContractJsonOptions);

    internal static async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken)
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
                break;
            hash.AppendData(buffer, 0, read);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    internal static JsonElement DeserializeStoredResponse(string responseJson)
        => JsonSerializer.Deserialize<JsonElement>(responseJson, ContractJsonOptions);

    internal static bool IsProjectScoped(McpPrincipal? principal)
        => principal?.ProjectId is not null && !principal.IsLocal && !principal.IsAdministrator;

    internal static bool IsPreviewPending(McpPreviewEntry entry)
        => entry.Status == McpMutationPreviewStatusDto.Pending
            && entry.ExpiresAt > DateTimeOffset.UtcNow;

    internal static McpToolDescriptor Tool(string name, string description, JsonElement inputSchema)
        => new(name, description, inputSchema);

    internal static JsonElement Schema(
        Dictionary<string, object?>? properties = null,
        string[]? required = null)
        => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = properties ?? new Dictionary<string, object?>(),
            required = required ?? [],
            additionalProperties = false
        });

    internal static object StringSchema(string? description = null)
        => description is null ? new { type = "string" } : new { type = "string", description };

    internal static object StringEnumSchema(string? description, params string[] values)
        => new { type = "string", @enum = values, description };

    internal static object NumberSchema()
        => new { type = "integer" };

    internal static object BooleanSchema()
        => new { type = "boolean" };

    internal static AiReadModelOptions ReadOptions(JsonElement arguments)
        => new(
            GetOptionalInt(arguments, "maxItems") ?? 200,
            GetOptionalInt(arguments, "maxJsonBytes") ?? 64 * 1024,
            GetOptionalBool(arguments, "includeFlowLiteralValues") ?? false,
            GetOptionalBool(arguments, "includeScriptSource") ?? false);

    internal static FlowVersionTrackDto ReadTrack(JsonElement arguments)
    {
        var value = GetOptionalString(arguments, "track");
        return string.IsNullOrWhiteSpace(value)
            ? FlowVersionTrackDto.Development
            : Enum.TryParse<FlowVersionTrackDto>(value, true, out var track) && Enum.IsDefined(track)
                ? track
                : throw new McpProtocolException(-32602, "The flow version track must be development or production.");
    }

    internal static FlowVersionTrackDto? ReadOptionalTrack(JsonElement arguments)
    {
        var value = GetOptionalString(arguments, "track");
        return string.IsNullOrWhiteSpace(value)
            ? null
            : Enum.TryParse<FlowVersionTrackDto>(value, true, out var track) && Enum.IsDefined(track)
                ? track
                : throw new McpProtocolException(-32602, "The flow version track must be development or production.");
    }

    internal static Guid GetGuid(JsonElement arguments, string name)
    {
        var value = GetRequiredString(arguments, name);
        return Guid.TryParse(value, out var parsed)
            ? parsed
            : throw new McpProtocolException(-32602, $"MCP parameter '{name}' must be a GUID.");
    }

    internal static string GetRequiredString(JsonElement arguments, string name)
    {
        var value = GetOptionalString(arguments, name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new McpProtocolException(-32602, $"MCP parameter '{name}' is required.")
            : value.Trim();
    }

    internal static string? GetOptionalString(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind != JsonValueKind.String)
            throw new McpProtocolException(-32602, $"MCP parameter '{name}' must be a string.");
        return value.GetString();
    }

    internal static int? GetOptionalInt(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var parsed))
            throw new McpProtocolException(-32602, $"MCP parameter '{name}' must be an integer.");
        return parsed;
    }

    internal static long? GetOptionalLong(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var parsed))
            throw new McpProtocolException(-32602, $"MCP parameter '{name}' must be an integer.");
        return parsed;
    }

    internal static bool? GetOptionalBool(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new McpProtocolException(-32602, $"MCP parameter '{name}' must be a boolean.");
        return value.GetBoolean();
    }
}

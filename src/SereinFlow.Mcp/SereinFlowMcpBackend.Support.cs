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

public sealed partial class SereinFlowMcpBackend
{
    private static McpPermissionDto[] ReadPermissions(JsonElement arguments)
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

    private static DateTimeOffset? ReadOptionalDate(JsonElement arguments, string name)
    {
        var value = GetOptionalString(arguments, name);
        return string.IsNullOrWhiteSpace(value)
            ? null
            : DateTimeOffset.TryParse(value, out var parsed)
                ? parsed
                : throw new McpProtocolException(-32602, $"MCP parameter '{name}' must be an ISO date.");
    }

    private static T Deserialize<T>(JsonElement arguments)
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

    private static McpProtocolException InvalidArguments(JsonException exception)
    {
        var path = string.IsNullOrWhiteSpace(exception.Path) ? null : exception.Path;
        var location = path is null ? string.Empty : $" at '{path}'";
        return new McpProtocolException(
            -32602,
            $"MCP arguments are invalid{location}.",
            new { code = "mcp.invalid_arguments", path });
    }

    private static McpProtocolException InvalidPatchValue(JsonException exception)
    {
        var path = string.IsNullOrWhiteSpace(exception.Path) ? null : exception.Path;
        var location = path is null ? string.Empty : $" at '{path}'";
        return new McpProtocolException(
            -32602,
            $"The flow patch value is invalid{location}. Use camelCase enum strings such as 'action' and 'data'; legacy numeric enum values are also accepted.",
            new { code = "mcp.invalid_patch_value", path });
    }

    private static McpProtocolException InvalidFlowPatchContract(FlowPatchContractException exception)
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

    private static McpProtocolException InvalidLibraryNodeTemplate(LibraryNodeTemplateException exception)
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

    private static Guid? TryGetGuid(JsonElement arguments, string name)
    {
        var value = GetOptionalString(arguments, name);
        return string.IsNullOrWhiteSpace(value)
            ? null
            : Guid.TryParse(value, out var parsed)
                ? parsed
                : throw new McpProtocolException(-32602, $"MCP parameter '{name}' must be a GUID.");
    }

    private static long GetLong(JsonElement arguments, string name)
        => GetOptionalLong(arguments, name)
            ?? throw new McpProtocolException(-32602, $"MCP parameter '{name}' is required.");

    private static FlowVersionTrackDto ParseTrack(string value)
        => Enum.TryParse<FlowVersionTrackDto>(value, true, out var track) && Enum.IsDefined(track)
            ? track
            : throw new McpProtocolException(-32602, "The flow version track must be development or production.");

    private static void RequireConfirmation(McpMutationApplyRequestDto request)
    {
        if (!string.Equals(request.Confirmation, "APPLY", StringComparison.Ordinal))
            throw new McpProtocolException(-32602, "The confirmation value must be APPLY.");
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new McpProtocolException(-32602, "The idempotency key is required.");
    }

    private static McpProtocolException VersionConflict(long? currentVersion)
        => new(-32010, "The flow version changed before the MCP mutation was applied.", new { currentVersion });

    private static async Task<FlowValidationResultDto> ValidateExecutableFlowAsync(
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

    private static void RequireValidMutation(FlowValidationResultDto validation, string message)
    {
        if (validation.IsValid)
            return;

        throw new McpProtocolException(
            -32011,
            message,
            new { code = "mcp.validation_failed", diagnostics = validation.Diagnostics });
    }

    private static async Task MarkPreviewAppliedAsync(
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

    private static async Task<SereinFlow.Domain.Project> RequireActiveProjectAsync(
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

    private static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, ContractJsonOptions);

    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken)
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

    private static JsonElement DeserializeStoredResponse(string responseJson)
        => JsonSerializer.Deserialize<JsonElement>(responseJson, ContractJsonOptions);

    private static bool IsProjectScoped(McpPrincipal? principal)
        => principal?.ProjectId is not null && !principal.IsLocal && !principal.IsAdministrator;

    private static bool IsPreviewPending(McpPreviewEntry entry)
        => entry.Status == McpMutationPreviewStatusDto.Pending
            && entry.ExpiresAt > DateTimeOffset.UtcNow;

    private static Guid? TryGetGuidSilently(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String)
            return null;
        return Guid.TryParse(value.GetString(), out var parsed) ? parsed : null;
    }

    private static string? TryGetStringSilently(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String)
            return null;
        return value.GetString();
    }

    private static FlowVersionTrackDto? GetAuditTrack(string operation, JsonElement arguments)
    {
        var value = GetOptionalString(arguments, "track");
        if (!string.IsNullOrWhiteSpace(value)
            && Enum.TryParse<FlowVersionTrackDto>(value, true, out var track)
            && Enum.IsDefined(track))
        {
            return track;
        }

        return operation is "sereinflow_preview_flow_patch"
            or "sereinflow_apply_flow_patch"
            or "sereinflow_preview_publish_flow"
            or "sereinflow_apply_publish_flow"
            ? FlowVersionTrackDto.Development
            : null;
    }

    private static FlowVersionTrackDto? GetPreviewAuditTrack(McpPreviewEntry entry)
    {
        if (string.Equals(entry.Operation, "flow.patch", StringComparison.Ordinal)
            || string.Equals(entry.Operation, "flow.publish", StringComparison.Ordinal))
        {
            return FlowVersionTrackDto.Development;
        }

        if (!string.Equals(entry.Operation, "flow.rollback", StringComparison.Ordinal))
            return null;

        try
        {
            using var document = JsonDocument.Parse(entry.PayloadJson);
            if (document.RootElement.TryGetProperty("request", out var request)
                && request.TryGetProperty("track", out var track)
                && track.ValueKind == JsonValueKind.String
                && Enum.TryParse<FlowVersionTrackDto>(track.GetString(), true, out var parsed)
                && Enum.IsDefined(parsed))
            {
                return parsed;
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static long? GetAuditFlowVersion(JsonElement arguments)
        => GetOptionalLong(arguments, "expectedDevelopmentVersion")
            ?? GetOptionalLong(arguments, "expectedHeadVersion")
            ?? GetOptionalLong(arguments, "sourceVersion")
            ?? GetOptionalLong(arguments, "version");

    private sealed record StoredProjectCreatePreview(
        CreateProjectMcpRequestDto Request,
        Guid ProjectId,
        Guid FlowId,
        DateTimeOffset CreatedAt,
        FlowDefinitionDto Definition,
        FlowValidationResultDto Validation);
    private sealed record StoredFlowPatchPreview(
        FlowPatchRequestDto Request,
        FlowDefinitionDto CandidateDefinition,
        FlowValidationResultDto Validation,
        FlowDiffDto Diff,
        FlowPatchCanonicalRequestDto? CanonicalRequest = null,
        IReadOnlyList<FlowPatchNormalizationWarningDto>? NormalizationWarnings = null);
    private sealed record StoredPublishPreview(
        PublishFlowPreviewRequestDto Request,
        FlowValidationResultDto Validation,
        FlowDiffDto Diff,
        bool HasProductionVersion = true,
        long? ExpectedProductionVersion = null);
    private sealed record StoredRollbackPreview(RollbackFlowPreviewRequestDto Request, FlowValidationResultDto Validation, FlowDiffDto Diff);
    private sealed record StoredLibraryPackagePreview(
        string FileName,
        Guid? ProjectId,
        string StagingPath,
        long SizeBytes,
        string PackageSha256,
        LibraryPackageInspectionDto Inspection,
        LibraryArtifactCompatibilityDto? Compatibility = null,
        LibraryPackageProjectImpactDto? ProjectImpact = null);
    private sealed record StoredProjectLibraryAttachPreview(
        ProjectLibraryAttachRequestDto Request,
        IReadOnlyList<ValidationDiagnosticDto> Diagnostics);

    private sealed class SemaphoreLease(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                semaphore.Release();
        }
    }

    private static McpToolDescriptor Tool(string name, string description, JsonElement inputSchema)
        => new(name, description, inputSchema);

    private static JsonElement Schema(
        Dictionary<string, object?>? properties = null,
        string[]? required = null)
        => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = properties ?? new Dictionary<string, object?>(),
            required = required ?? [],
            additionalProperties = false
        });

    private static object StringSchema(string? description = null)
        => description is null ? new { type = "string" } : new { type = "string", description };

    private static object NumberSchema()
        => new { type = "integer" };

    private static object BooleanSchema()
        => new { type = "boolean" };

    private static AiReadModelOptions ReadOptions(JsonElement arguments)
        => new(
            GetOptionalInt(arguments, "maxItems") ?? 200,
            GetOptionalInt(arguments, "maxJsonBytes") ?? 64 * 1024,
            GetOptionalBool(arguments, "includeFlowLiteralValues") ?? false,
            GetOptionalBool(arguments, "includeScriptSource") ?? false);

    private static FlowVersionTrackDto ReadTrack(JsonElement arguments)
    {
        var value = GetOptionalString(arguments, "track");
        return string.IsNullOrWhiteSpace(value)
            ? FlowVersionTrackDto.Development
            : Enum.TryParse<FlowVersionTrackDto>(value, true, out var track) && Enum.IsDefined(track)
                ? track
                : throw new McpProtocolException(-32602, "The flow version track must be development or production.");
    }

    private static FlowVersionTrackDto? ReadOptionalTrack(JsonElement arguments)
    {
        var value = GetOptionalString(arguments, "track");
        return string.IsNullOrWhiteSpace(value)
            ? null
            : Enum.TryParse<FlowVersionTrackDto>(value, true, out var track) && Enum.IsDefined(track)
                ? track
                : throw new McpProtocolException(-32602, "The flow version track must be development or production.");
    }

    private static Guid GetGuid(JsonElement arguments, string name)
    {
        var value = GetRequiredString(arguments, name);
        return Guid.TryParse(value, out var parsed)
            ? parsed
            : throw new McpProtocolException(-32602, $"MCP parameter '{name}' must be a GUID.");
    }

    private static string GetRequiredString(JsonElement arguments, string name)
    {
        var value = GetOptionalString(arguments, name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new McpProtocolException(-32602, $"MCP parameter '{name}' is required.")
            : value.Trim();
    }

    private static string? GetOptionalString(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind != JsonValueKind.String)
            throw new McpProtocolException(-32602, $"MCP parameter '{name}' must be a string.");
        return value.GetString();
    }

    private static int? GetOptionalInt(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var parsed))
            throw new McpProtocolException(-32602, $"MCP parameter '{name}' must be an integer.");
        return parsed;
    }

    private static long? GetOptionalLong(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var parsed))
            throw new McpProtocolException(-32602, $"MCP parameter '{name}' must be an integer.");
        return parsed;
    }

    private static bool? GetOptionalBool(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new McpProtocolException(-32602, $"MCP parameter '{name}' must be a boolean.");
        return value.GetBoolean();
    }
}

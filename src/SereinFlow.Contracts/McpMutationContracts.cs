using System.Text.Json;
using System.Text.Json.Serialization;

namespace SereinFlow.Contracts;

[JsonConverter(typeof(McpPermissionJsonConverter))]
public enum McpPermissionDto
{
    ProjectRead,
    LibraryRead,
    RunRead,
    DebugRead,
    FlowWrite,
    DebugControl,
    FlowPublish,
    FlowRollback,
    ScriptCompile,
    LibraryImport,
    LibraryManage,
    McpKeysManage,
    SensitiveRead,
}

/// <summary>
/// Stable wire names for MCP permissions. The dotted names are part of the
/// public authorization contract; enum and camelCase spellings remain accepted
/// when reading persisted keys created by earlier builds.
/// MCP 权限的稳定传输名称。点号名称属于公开授权契约；读取旧版本持久化
/// Key 时仍兼容枚举名称和 camelCase 名称。
/// </summary>
public static class McpPermissionNames
{
    private static readonly IReadOnlyDictionary<McpPermissionDto, string> Names =
        new Dictionary<McpPermissionDto, string>
        {
            [McpPermissionDto.ProjectRead] = "project.read",
            [McpPermissionDto.LibraryRead] = "library.read",
            [McpPermissionDto.RunRead] = "run.read",
            [McpPermissionDto.DebugRead] = "debug.read",
            [McpPermissionDto.FlowWrite] = "flow.write",
            [McpPermissionDto.DebugControl] = "debug.control",
            [McpPermissionDto.FlowPublish] = "flow.publish",
            [McpPermissionDto.FlowRollback] = "flow.rollback",
            [McpPermissionDto.ScriptCompile] = "script.compile",
            [McpPermissionDto.LibraryImport] = "library.import",
            [McpPermissionDto.LibraryManage] = "library.manage",
            [McpPermissionDto.McpKeysManage] = "mcp.keys.manage",
            [McpPermissionDto.SensitiveRead] = "sensitive.read",
        };

    public static string ToName(McpPermissionDto permission)
        => Names.TryGetValue(permission, out var name)
            ? name
            : throw new ArgumentOutOfRangeException(nameof(permission), permission, "The MCP permission is not defined.");

    public static bool TryParse(string? value, out McpPermissionDto permission)
    {
        permission = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        foreach (var pair in Names)
        {
            if (string.Equals(pair.Value, normalized, StringComparison.OrdinalIgnoreCase))
            {
                permission = pair.Key;
                return true;
            }
        }

        return Enum.TryParse(normalized, ignoreCase: true, out permission)
            && Enum.IsDefined(permission);
    }
}

public sealed class McpPermissionJsonConverter : JsonConverter<McpPermissionDto>
{
    public override McpPermissionDto Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String
            && McpPermissionNames.TryParse(reader.GetString(), out var permission))
        {
            return permission;
        }

        if (reader.TokenType == JsonTokenType.Number
            && reader.TryGetInt32(out var numeric)
            && Enum.IsDefined((McpPermissionDto)numeric))
        {
            return (McpPermissionDto)numeric;
        }

        throw new JsonException("The MCP permission is invalid.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        McpPermissionDto value,
        JsonSerializerOptions options)
        => writer.WriteStringValue(McpPermissionNames.ToName(value));
}

public enum McpMutationPreviewStatusDto
{
    Pending,
    Applied,
    Expired,
    Rejected,
}

public enum FlowPatchOperationKindDto
{
    AddCanvas,
    UpdateCanvas,
    RemoveCanvas,
    AddNode,
    ReplaceNode,
    RemoveNode,
    SetNodeParameter,
    AddConnection,
    ReplaceConnection,
    RemoveConnection,
    SetEntryNode,
    SetRunPolicy,
    ReplaceScriptSource,
}

public sealed record FlowPatchOperationDto(
    FlowPatchOperationKindDto Operation,
    string? CanvasId = null,
    string? NodeId = null,
    string? ConnectionId = null,
    string? ParameterId = null,
    JsonElement? Value = null);

public sealed record FlowPatchRequestDto(
    Guid ProjectId,
    Guid FlowId,
    long ExpectedDevelopmentVersion,
    IReadOnlyList<FlowPatchOperationDto> Operations,
    string? Remark = null);

public sealed record FlowDiffItemDto(
    string Kind,
    string Path,
    string? Before,
    string? After,
    bool IsSensitive = false);

public sealed record FlowDiffDto(
    Guid FlowId,
    long BaseVersion,
    long? CandidateVersion,
    string BaseChecksum,
    string CandidateChecksum,
    IReadOnlyList<FlowDiffItemDto> Changes,
    int AddedNodes,
    int RemovedNodes,
    int ChangedNodes,
    int AddedConnections,
    int RemovedConnections,
    int ChangedConnections);

public sealed record FlowPatchPreviewDto(
    Guid PreviewId,
    Guid ProjectId,
    Guid FlowId,
    long ExpectedDevelopmentVersion,
    DateTimeOffset ExpiresAt,
    string PreviewFingerprint,
    bool CanApply,
    FlowValidationResultDto Validation,
    FlowDiffDto Diff,
    FlowDefinitionDto? CandidateDefinition = null);

public sealed record FlowVersionComparisonDto(
    FlowVersionDetailDto From,
    FlowVersionDetailDto To,
    FlowDiffDto Diff);

public sealed record McpMutationApplyRequestDto(
    Guid PreviewId,
    string PreviewFingerprint,
    string Confirmation,
    string IdempotencyKey);

public sealed record McpPreviewDescriptorDto(
    Guid PreviewId,
    string Operation,
    Guid? ProjectId,
    Guid? FlowId,
    McpMutationPreviewStatusDto Status,
    DateTimeOffset ExpiresAt,
    string PreviewFingerprint);

public sealed record PublishFlowPreviewRequestDto(
    Guid ProjectId,
    Guid FlowId,
    long ExpectedDevelopmentVersion,
    string? Remark = null);

public sealed record RollbackFlowPreviewRequestDto(
    Guid ProjectId,
    Guid FlowId,
    FlowVersionTrackDto Track,
    long SourceVersion,
    long ExpectedHeadVersion);

public sealed record FlowVersionMutationDto(
    FlowVersionSummaryDto Version,
    FlowDiffDto Diff,
    FlowDefinitionDto? Definition = null);

public sealed record ScriptCompileRequestDto(
    string Source,
    string? SourceName,
    string LanguageVersion,
    IReadOnlyList<ScriptValueContractDto> Inputs);

public sealed record ScriptCompileDiagnosticDto(
    string Code,
    string Message,
    string Severity,
    string? SourceName = null,
    int? Line = null,
    int? Column = null);

public sealed record ScriptCompileResultDto(
    bool IsSuccess,
    string SourceHash,
    string LanguageVersion,
    string? SourceName,
    TimeSpan Duration,
    IReadOnlyList<ScriptCompileDiagnosticDto> Diagnostics);

public sealed record LibraryPackagePreviewDto(
    Guid PreviewId,
    string FileName,
    long SizeBytes,
    string Sha256,
    DateTimeOffset ExpiresAt,
    string PreviewFingerprint,
    bool CanApply,
    LibraryDto? Library,
    IReadOnlyList<ValidationDiagnosticDto> Diagnostics,
    bool AlreadyExists,
    string? DllSha256 = null,
    LibraryArtifactCompatibilityDto? Compatibility = null,
    LibraryPackageProjectImpactDto? ProjectImpact = null);

public sealed record ProjectLibraryAttachPreviewDto(
    Guid PreviewId,
    Guid ProjectId,
    string LibraryId,
    DateTimeOffset ExpiresAt,
    string PreviewFingerprint,
    bool CanApply,
    IReadOnlyList<ValidationDiagnosticDto> Diagnostics);

public sealed record ProjectLibraryAttachRequestDto(
    Guid ProjectId,
    string LibraryId);

public sealed record McpApiKeyDto(
    string Id,
    Guid? ProjectId,
    string Name,
    string KeyPrefix,
    IReadOnlyList<McpPermissionDto> Permissions,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    DateTimeOffset? LastUsedAt,
    bool IsAdministrator = false);

public sealed record CreateMcpApiKeyRequestDto(
    Guid? ProjectId,
    string Name,
    IReadOnlyList<McpPermissionDto> Permissions,
    DateTimeOffset? ExpiresAt = null,
    bool IsAdministrator = false);

public sealed record CreatedMcpApiKeyDto(McpApiKeyDto Key, string Secret);

public sealed record RotatedMcpApiKeyDto(
    string RevokedKeyId,
    McpApiKeyDto Key,
    string? Secret,
    bool Replayed = false);

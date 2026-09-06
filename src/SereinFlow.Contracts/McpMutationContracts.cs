using System.Text.Json;
using System.Text.Json.Serialization;

namespace SereinFlow.Contracts;

[JsonConverter(typeof(McpPermissionJsonConverter))]
public enum McpPermissionDto
{
    ProjectRead,
    ProjectWrite,
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
    RunMessagePublish,
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
    private static readonly Dictionary<McpPermissionDto, string> Names =
        new Dictionary<McpPermissionDto, string>
        {
            [McpPermissionDto.ProjectRead] = ProjectErrorCodes.Read,
            [McpPermissionDto.ProjectWrite] = ProjectErrorCodes.Write,
            [McpPermissionDto.LibraryRead] = LibraryErrorCodes.Read,
            [McpPermissionDto.RunRead] = RunErrorCodes.Read,
            [McpPermissionDto.DebugRead] = DebugErrorCodes.Read,
            [McpPermissionDto.FlowWrite] = FlowErrorCodes.Write,
            [McpPermissionDto.DebugControl] = DebugErrorCodes.Control,
            [McpPermissionDto.FlowPublish] = "flow.publish",
            [McpPermissionDto.FlowRollback] = "flow.rollback",
            [McpPermissionDto.ScriptCompile] = ScriptErrorCodes.Compile,
            [McpPermissionDto.LibraryImport] = LibraryErrorCodes.Import,
            [McpPermissionDto.LibraryManage] = LibraryErrorCodes.Manage,
            [McpPermissionDto.McpKeysManage] = McpErrorCodes.KeysManage,
            [McpPermissionDto.SensitiveRead] = "sensitive.read",
            [McpPermissionDto.RunMessagePublish] = "run.message.publish",
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
    AddNodeParameter,
    RemoveNodeParameter,
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

/// <summary>
/// Public flow-patch wire contract versions. Version 1 remains input-only
/// compatibility for previews persisted or produced before the typed v2 union.
/// </summary>
public static class FlowPatchContract
{
    public const string LegacySchemaVersion = "1.0";
    public const string CurrentSchemaVersion = "2.0";
    public const string EnumEncoding = "camelCase";
}

/// <summary>
/// Canonical v2 flow operation. The normalizer constructs only the fields
/// allowed by the selected <see cref="Op"/> discriminator.
/// </summary>
public sealed record FlowPatchCanonicalOperationDto(
    string Op,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CanvasId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? NodeId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ConnectionId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ParameterId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CanvasDto? Canvas = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NodeDto? Node = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NodeParameterDto? Parameter = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ConnectionDto? Connection = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EntryNodeId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] FlowRunPolicyDto? RunPolicy = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Source = null);

public sealed record FlowPatchCanonicalRequestDto(
    Guid ProjectId,
    Guid FlowId,
    long ExpectedDevelopmentVersion,
    string SchemaVersion,
    IReadOnlyList<FlowPatchCanonicalOperationDto> Operations,
    string? Remark = null);

public sealed record FlowPatchNormalizationWarningDto(
    string Code,
    string FieldPath,
    string Message);

public sealed record NodeTemplatePositionDto(double X, double Y);

public sealed record LibraryNodeTemplateRequestDto(
    Guid ProjectId,
    string LibraryId,
    string LibraryNodeContractId,
    NodeTemplatePositionDto Position);

public sealed record LibraryNodeTemplateDto(
    NodeDto Node,
    string TemplateSource,
    string LibraryId,
    string LibraryVersion,
    string LibrarySha256,
    string ContractRevision);

public sealed record CreateProjectMcpRequestDto(
    string Name,
    string? FlowName = null);

public sealed record ProjectCreatePreviewDto(
    Guid PreviewId,
    Guid ProjectId,
    Guid FlowId,
    string Name,
    string FlowName,
    DateTimeOffset ExpiresAt,
    string PreviewFingerprint,
    bool CanApply,
    FlowValidationResultDto Validation,
    ProjectWorkspaceDto Workspace,
    bool IsPreviewOnly = true);

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
    FlowDefinitionDto? CandidateDefinition = null,
    bool IsPreviewOnly = true,
    string SchemaVersion = FlowPatchContract.CurrentSchemaVersion,
    string EnumEncoding = FlowPatchContract.EnumEncoding,
    IReadOnlyList<FlowPatchCanonicalOperationDto>? NormalizedOperations = null,
    IReadOnlyList<FlowPatchNormalizationWarningDto>? NormalizationWarnings = null);

public sealed record FlowVersionComparisonDto(
    FlowVersionDetailDto From,
    FlowVersionDetailDto To,
    FlowDiffDto Diff);

public sealed record McpMutationApplyRequestDto(
    Guid PreviewId,
    string PreviewFingerprint,
    string Confirmation,
    string IdempotencyKey);

/// <summary>
/// Best-effort notification that an authoritative project or environment
/// state changed. The event is an invalidation hint; clients must reread the
/// affected resource instead of treating the event as the source of truth.
/// 权威项目或环境状态发生变化时发送的尽力通知。事件只是失效提示，客户端仍应
/// 重新读取受影响资源，不能把事件本身当作最终数据源。
/// </summary>
public sealed record WorkspaceChangeEventDto(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string ChangeType,
    Guid? ProjectId,
    Guid? FlowId,
    long? Version,
    string? Checksum,
    string Origin,
    string Operation,
    IReadOnlyList<string>? CanvasIds = null,
    IReadOnlyList<string>? NodeIds = null,
    IReadOnlyList<string>? ConnectionIds = null,
    IReadOnlyList<string>? ParameterIds = null,
    IReadOnlyList<string>? LibraryIds = null);

public sealed record McpPreviewDescriptorDto(
    Guid PreviewId,
    string Operation,
    Guid? ProjectId,
    Guid? FlowId,
    McpMutationPreviewStatusDto Status,
    DateTimeOffset ExpiresAt,
    string PreviewFingerprint,
    bool IsPreviewOnly = true);

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
    LibraryPackageProjectImpactDto? ProjectImpact = null,
    bool IsPreviewOnly = true);

public sealed record ProjectLibraryAttachPreviewDto(
    Guid PreviewId,
    Guid ProjectId,
    string LibraryId,
    DateTimeOffset ExpiresAt,
    string PreviewFingerprint,
    bool CanApply,
    IReadOnlyList<ValidationDiagnosticDto> Diagnostics,
    bool IsPreviewOnly = true);

public sealed record ProjectLibraryAttachRequestDto(
    Guid ProjectId,
    string LibraryId);

public sealed record LibraryFamilyAssignmentMcpRequestDto(
    string LibraryId,
    string? FamilyId,
    string? Name,
    string? Description = null);

public sealed record LibraryFamilyAssignmentMcpPreviewDto(
    Guid PreviewId,
    LibraryFamilyAssignmentMcpRequestDto Request,
    DateTimeOffset ExpiresAt,
    string PreviewFingerprint,
    bool CanApply,
    IReadOnlyList<ValidationDiagnosticDto> Diagnostics,
    LibraryFamilyDto? CurrentFamily,
    LibraryFamilyDto? TargetFamily,
    bool IsPreviewOnly = true);

public sealed record McpLibraryUpgradePreviewRequestDto(
    Guid ProjectId,
    string SourceArtifactId,
    string TargetArtifactId,
    IReadOnlyList<Guid> FlowIds);

public sealed record McpLibraryUpgradeApplyRequestDto(
    Guid PreviewId,
    string PreviewFingerprint,
    string Confirmation,
    string IdempotencyKey,
    IReadOnlyList<ApplyLibraryUpgradeRequestDto> Flows);

public sealed record McpLibraryUpgradePreviewDto(
    Guid PreviewId,
    DateTimeOffset ExpiresAt,
    string PreviewFingerprint,
    bool CanApply,
    LibraryUpgradePlanDto Plan,
    bool IsPreviewOnly = true);

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

/// <summary>
/// MCP-only start contract. The HTTP API keeps using
/// <see cref="StartFlowDebugSessionRequestDto"/> without an idempotency key.
/// </summary>
public sealed record McpStartFlowDebugSessionRequestDto(
    Guid ProjectId,
    Guid FlowId,
    IReadOnlyList<string>? BreakpointNodeIds,
    IReadOnlyDictionary<string, JsonElement>? ProjectInputs = null,
    int? TimeoutSeconds = null,
    int? MaxSteps = null,
    int? MaxNodeVisits = null,
    long? ExpectedFlowVersion = null,
    int? MaxQueuedFlipflopTriggers = null,
    string? IdempotencyKey = null);

public sealed record McpDebugSessionStartedDto(
    Guid SessionId,
    Guid RunId,
    Guid ProjectId,
    Guid FlowId,
    FlowDebugSessionStatusDto Status,
    long StateRevision);

public sealed record McpDebugCommandAcceptedDto(
    Guid SessionId,
    Guid RunId,
    string Command,
    long CommandSequence,
    FlowDebugSessionStatusDto Status,
    long StateRevision,
    bool Accepted = true);

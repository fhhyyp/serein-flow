using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SereinFlow.Contracts;

[JsonConverter(typeof(SereinFlowPermissionJsonConverter))]
/// <summary>
/// Defines transport-independent SereinFlow operations.
/// 定义与传输方式无关的 SereinFlow 操作。
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "This public permission enum is shared by authorization, generated tools and host integrations; its name describes the domain contract.")]
public enum SereinFlowPermission
{
    /// <summary>
    /// Indicates the project read value.
    /// 表示“项目读取”这一取值。
    /// </summary>
    ProjectRead,
    /// <summary>
    /// Indicates the project write value.
    /// 表示“项目写入”这一取值。
    /// </summary>
    ProjectWrite,
    /// <summary>
    /// Indicates the library read value.
    /// 表示“库读取”这一取值。
    /// </summary>
    LibraryRead,
    /// <summary>
    /// Indicates the run read value.
    /// 表示“运行读取”这一取值。
    /// </summary>
    RunRead,
    /// <summary>
    /// Indicates the debug read value.
    /// 表示“调试读取”这一取值。
    /// </summary>
    DebugRead,
    /// <summary>
    /// Indicates the flow write value.
    /// 表示“流程写入”这一取值。
    /// </summary>
    FlowWrite,
    /// <summary>
    /// Indicates the debug control value.
    /// 表示“调试控制”这一取值。
    /// </summary>
    DebugControl,
    /// <summary>
    /// Indicates the flow publish value.
    /// 表示“流程发布”这一取值。
    /// </summary>
    FlowPublish,
    /// <summary>
    /// Indicates the flow rollback value.
    /// 表示“流程回滚”这一取值。
    /// </summary>
    FlowRollback,
    /// <summary>
    /// Indicates the script compile value.
    /// 表示“脚本Compile”这一取值。
    /// </summary>
    ScriptCompile,
    /// <summary>
    /// Indicates the library import value.
    /// 表示“库导入”这一取值。
    /// </summary>
    LibraryImport,
    /// <summary>
    /// Indicates the library manage value.
    /// 表示“库管理”这一取值。
    /// </summary>
    LibraryManage,
    /// <summary>
    /// Indicates the security management operation.
    /// 表示安全管理操作。
    /// </summary>
    SecurityManage,
    /// <summary>
    /// Indicates the sensitive read value.
    /// 表示“敏感数据读取”这一取值。
    /// </summary>
    SensitiveRead,
    /// <summary>
    /// Indicates the run message publish value.
    /// 表示“运行消息发布”这一取值。
    /// </summary>
    RunMessagePublish,
    /// <summary>
    /// Indicates the run execute value.
    /// 表示“运行Execute”这一取值。
    /// </summary>
    RunExecute,
    /// <summary>
    /// Indicates the run variable write value.
    /// 表示“运行变量写入”这一取值。
    /// </summary>
    RunVariableWrite,
    WorkspaceManage,
}

/// <summary>
/// Stable dotted operation names used by the public authorization contract.
/// 公开授权契约使用的稳定点号操作名称。
/// </summary>
public static class SereinFlowOperationNames
{
    private static readonly Dictionary<SereinFlowPermission, string> Names = new Dictionary<SereinFlowPermission, string>
    {
        [SereinFlowPermission.ProjectRead] = ProjectErrorCodes.Read,
        [SereinFlowPermission.ProjectWrite] = ProjectErrorCodes.Write,
        [SereinFlowPermission.LibraryRead] = LibraryErrorCodes.Read,
        [SereinFlowPermission.RunRead] = RunErrorCodes.Read,
        [SereinFlowPermission.DebugRead] = DebugErrorCodes.Read,
        [SereinFlowPermission.FlowWrite] = FlowErrorCodes.Write,
        [SereinFlowPermission.DebugControl] = DebugErrorCodes.Control,
        [SereinFlowPermission.FlowPublish] = "flow.publish",
        [SereinFlowPermission.FlowRollback] = "flow.rollback",
        [SereinFlowPermission.ScriptCompile] = ScriptErrorCodes.Compile,
        [SereinFlowPermission.LibraryImport] = LibraryErrorCodes.Import,
        [SereinFlowPermission.LibraryManage] = LibraryErrorCodes.Manage,
        [SereinFlowPermission.SecurityManage] = "security.manage",
        [SereinFlowPermission.SensitiveRead] = "sensitive.read",
        [SereinFlowPermission.RunMessagePublish] = "run.message.publish",
        [SereinFlowPermission.RunExecute] = "run.execute",
        [SereinFlowPermission.RunVariableWrite] = "run.variable.write",
        [SereinFlowPermission.WorkspaceManage] = "workspace.manage",
    };
    public static string ToName(SereinFlowPermission permission)
    {
        return Names.TryGetValue(permission, out var name) ? name : throw new ArgumentOutOfRangeException(nameof(permission), permission, "The MCP permission is not defined. 未定义该 MCP 权限。");
    }

    public static bool TryParse(string? value, out SereinFlowPermission permission)
    {
        permission = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        foreach (var pair in Names)
        {
            if (string.Equals(pair.Value, normalized, StringComparison.OrdinalIgnoreCase))
            {
                permission = pair.Key;
                return true;
            }
        }

        return false;
    }
}

public sealed class SereinFlowPermissionJsonConverter : JsonConverter<SereinFlowPermission>
{
    public override SereinFlowPermission Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var permissionName = reader.GetString();
            if (SereinFlowOperationNames.TryParse(permissionName, out var permission))
            {
                return permission;
            }
        }

        throw new JsonException("A dotted SereinFlow operation name is required. 必须使用点号分隔的 SereinFlow 操作名称。");
    }

    public override void Write(Utf8JsonWriter writer, SereinFlowPermission value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(SereinFlowOperationNames.ToName(value));
    }
}

/// <summary>
/// Identifies the status of an MCP mutation preview.
/// 标识 MCP 变更预览的状态。
/// </summary>
public enum McpMutationPreviewStatusDto
{
    /// <summary>
    /// Indicates the pending value.
    /// 表示“等待中”这一取值。
    /// </summary>
    Pending,
    /// <summary>
    /// Indicates the applied value.
    /// 表示“已应用”这一取值。
    /// </summary>
    Applied,
    /// <summary>
    /// Indicates the expired value.
    /// 表示“已过期”这一取值。
    /// </summary>
    Expired,
    /// <summary>
    /// Indicates the rejected value.
    /// 表示“已拒绝”这一取值。
    /// </summary>
    Rejected,
}

/// <summary>
/// Identifies an operation in a flow patch request.
/// 标识流程补丁请求中的操作类型。
/// </summary>
public enum FlowPatchOperationKindDto
{
    /// <summary>
    /// Indicates the add canvas value.
    /// 表示“添加画布”这一取值。
    /// </summary>
    AddCanvas,
    /// <summary>
    /// Indicates the update canvas value.
    /// 表示“更新画布”这一取值。
    /// </summary>
    UpdateCanvas,
    /// <summary>
    /// Indicates the remove canvas value.
    /// 表示“移除画布”这一取值。
    /// </summary>
    RemoveCanvas,
    /// <summary>
    /// Indicates the add node value.
    /// 表示“添加节点”这一取值。
    /// </summary>
    AddNode,
    /// <summary>
    /// Indicates the replace node value.
    /// 表示“替换节点”这一取值。
    /// </summary>
    ReplaceNode,
    /// <summary>
    /// Indicates the remove node value.
    /// 表示“移除节点”这一取值。
    /// </summary>
    RemoveNode,
    /// <summary>
    /// Indicates the set node parameter value.
    /// 表示“设置节点参数”这一取值。
    /// </summary>
    SetNodeParameter,
    /// <summary>
    /// Indicates the add connection value.
    /// 表示“添加连线”这一取值。
    /// </summary>
    AddConnection,
    /// <summary>
    /// Indicates the replace connection value.
    /// 表示“替换连线”这一取值。
    /// </summary>
    ReplaceConnection,
    /// <summary>
    /// Indicates the remove connection value.
    /// 表示“移除连线”这一取值。
    /// </summary>
    RemoveConnection,
    /// <summary>
    /// Indicates the set entry node value.
    /// 表示“设置入口节点”这一取值。
    /// </summary>
    SetEntryNode,
    /// <summary>
    /// Indicates the set run policy value.
    /// 表示“设置运行Policy”这一取值。
    /// </summary>
    SetRunPolicy,
    /// <summary>
    /// Indicates the replace script source value.
    /// 表示“替换脚本源码”这一取值。
    /// </summary>
    ReplaceScriptSource,
    /// <summary>
    /// Indicates the add node parameter value.
    /// 表示“添加节点参数”这一取值。
    /// </summary>
    AddNodeParameter,
    /// <summary>
    /// Indicates the remove node parameter value.
    /// 表示“移除节点参数”这一取值。
    /// </summary>
    RemoveNodeParameter,
}

/// <summary>
/// Defines the API data contract for flow patch operation.
/// 定义流程Patch操作的 API 数据契约。
/// </summary>
public sealed record FlowPatchOperationDto(FlowPatchOperationKindDto Operation, string? CanvasId = null, string? NodeId = null, string? ConnectionId = null, string? ParameterId = null, JsonElement? Value = null);
/// <summary>
/// Carries request data for flow patch operations.
/// 承载流程Patch操作的请求数据。
/// </summary>
public sealed record FlowPatchRequestDto(Guid ProjectId, Guid FlowId, long ExpectedDevelopmentVersion, IReadOnlyList<FlowPatchOperationDto> Operations, string? Remark = null);
/// <summary>
/// Public flow-patch wire contract versions. Version 1 remains input-only
/// compatibility for previews persisted or produced before the typed v2 union.
/// 公开的流程补丁传输契约版本；v1 仅兼容强类型 v2 联合模型出现前生成或持久化的预览输入。
/// </summary>
public static class FlowPatchContract
{
    public const string LegacySchemaVersion = "1.0";
    public const string CurrentSchemaVersion = "2.0";
    public const string EnumEncoding = "camelCase";
}

/// <summary>
/// Canonical v2 flow operation. The normalizer constructs only the fields
/// allowed by the selected <see cref = "Op"/> discriminator.
/// 规范的 v2 流程操作；规范化器只构造所选 Op 判别值允许的字段。
/// </summary>
public sealed record FlowPatchCanonicalOperationDto(string Op, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CanvasId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? NodeId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ConnectionId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ParameterId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CanvasDto? Canvas = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NodeDto? Node = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NodeParameterDto? Parameter = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ConnectionDto? Connection = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EntryNodeId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] FlowRunPolicyDto? RunPolicy = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Source = null);
/// <summary>
/// Carries request data for flow patch canonical operations.
/// 承载流程Patch规范化操作的请求数据。
/// </summary>
public sealed record FlowPatchCanonicalRequestDto(Guid ProjectId, Guid FlowId, long ExpectedDevelopmentVersion, string SchemaVersion, IReadOnlyList<FlowPatchCanonicalOperationDto> Operations, string? Remark = null);
/// <summary>
/// Defines the API data contract for flow patch normalization warning.
/// 定义流程Patch规范化警告的 API 数据契约。
/// </summary>
public sealed record FlowPatchNormalizationWarningDto(string Code, string FieldPath, string Message);
/// <summary>
/// Defines the API data contract for node template position.
/// 定义节点TemplatePosition的 API 数据契约。
/// </summary>
public sealed record NodeTemplatePositionDto(double X, double Y);
/// <summary>
/// Carries request data for library node template operations.
/// 承载库节点Template操作的请求数据。
/// </summary>
public sealed record LibraryNodeTemplateRequestDto(Guid ProjectId, string LibraryId, string LibraryNodeContractId, NodeTemplatePositionDto Position);
/// <summary>
/// Defines the API data contract for library node template.
/// 定义库节点Template的 API 数据契约。
/// </summary>
public sealed record LibraryNodeTemplateDto(NodeDto Node, string TemplateSource, string LibraryId, string LibraryVersion, string LibrarySha256, string ContractRevision);
/// <summary>
/// Carries request data for builtin node template operations.
/// 承载Builtin节点Template操作的请求数据。
/// </summary>
public sealed record BuiltinNodeTemplateRequestDto(Guid ProjectId, string BuiltinNodeId, NodeTemplatePositionDto Position);
/// <summary>
/// Defines the API data contract for builtin node template.
/// 定义Builtin节点Template的 API 数据契约。
/// </summary>
public sealed record BuiltinNodeTemplateDto(NodeDto Node, string TemplateSource, string BuiltinNodeId);
/// <summary>
/// Carries request data for create project MCP operations.
/// 承载创建项目MCP操作的请求数据。
/// </summary>
public sealed record CreateProjectMcpRequestDto(string Name, string? FlowName = null);
/// <summary>
/// Defines the API data contract for project create preview.
/// 定义项目创建预览的 API 数据契约。
/// </summary>
public sealed record ProjectCreatePreviewDto(Guid PreviewId, Guid ProjectId, Guid FlowId, string Name, string FlowName, DateTimeOffset ExpiresAt, string PreviewFingerprint, bool CanApply, FlowValidationResultDto Validation, ProjectWorkspaceDto Workspace, bool IsPreviewOnly = true);
/// <summary>
/// Defines the API data contract for flow diff item.
/// 定义流程差异Item的 API 数据契约。
/// </summary>
public sealed record FlowDiffItemDto(string Kind, string Path, string? Before, string? After, bool IsSensitive = false);
/// <summary>
/// Defines the API data contract for flow diff.
/// 定义流程差异的 API 数据契约。
/// </summary>
public sealed record FlowDiffDto(Guid FlowId, long BaseVersion, long? CandidateVersion, string BaseChecksum, string CandidateChecksum, IReadOnlyList<FlowDiffItemDto> Changes, int AddedNodes, int RemovedNodes, int ChangedNodes, int AddedConnections, int RemovedConnections, int ChangedConnections);
/// <summary>
/// Defines the API data contract for flow patch preview.
/// 定义流程Patch预览的 API 数据契约。
/// </summary>
public sealed record FlowPatchPreviewDto(Guid PreviewId, Guid ProjectId, Guid FlowId, long ExpectedDevelopmentVersion, DateTimeOffset ExpiresAt, string PreviewFingerprint, bool CanApply, FlowValidationResultDto Validation, FlowDiffDto Diff, FlowDefinitionDto? CandidateDefinition = null, bool IsPreviewOnly = true, string SchemaVersion = FlowPatchContract.CurrentSchemaVersion, string EnumEncoding = FlowPatchContract.EnumEncoding, IReadOnlyList<FlowPatchCanonicalOperationDto>? NormalizedOperations = null, IReadOnlyList<FlowPatchNormalizationWarningDto>? NormalizationWarnings = null);
/// <summary>
/// Defines the API data contract for flow version comparison.
/// 定义流程版本Comparison的 API 数据契约。
/// </summary>
public sealed record FlowVersionComparisonDto(FlowVersionDetailDto From, FlowVersionDetailDto To, FlowDiffDto Diff);
/// <summary>
/// Carries request data for MCP mutation apply operations.
/// 承载MCP变更应用操作的请求数据。
/// </summary>
public sealed record McpMutationApplyRequestDto(Guid PreviewId, string PreviewFingerprint, string Confirmation, string IdempotencyKey);
/// <summary>
/// Best-effort notification that an authoritative project or environment
/// state changed. The event is an invalidation hint; clients must reread the
/// affected resource instead of treating the event as the source of truth.
/// 权威项目或环境状态发生变化时发送的尽力通知。事件只是失效提示，客户端仍应
/// 重新读取受影响资源，不能把事件本身当作最终数据源。
/// </summary>
public sealed record WorkspaceChangeEventDto(Guid EventId, DateTimeOffset OccurredAt, string ChangeType, Guid? ProjectId, Guid? FlowId, long? Version, string? Checksum, string Origin, string Operation, IReadOnlyList<string>? CanvasIds = null, IReadOnlyList<string>? NodeIds = null, IReadOnlyList<string>? ConnectionIds = null, IReadOnlyList<string>? ParameterIds = null, IReadOnlyList<string>? LibraryIds = null);
/// <summary>
/// Defines the API data contract for MCP preview descriptor.
/// 定义MCP预览Descriptor的 API 数据契约。
/// </summary>
public sealed record McpPreviewDescriptorDto(Guid PreviewId, string Operation, Guid? ProjectId, Guid? FlowId, McpMutationPreviewStatusDto Status, DateTimeOffset ExpiresAt, string PreviewFingerprint, bool IsPreviewOnly = true);
/// <summary>
/// Carries request data for publish flow preview operations.
/// 承载发布流程预览操作的请求数据。
/// </summary>
public sealed record PublishFlowPreviewRequestDto(Guid ProjectId, Guid FlowId, long ExpectedDevelopmentVersion, string? Remark = null);
/// <summary>
/// Carries request data for rollback flow preview operations.
/// 承载回滚流程预览操作的请求数据。
/// </summary>
public sealed record RollbackFlowPreviewRequestDto(Guid ProjectId, Guid FlowId, FlowVersionTrackDto Track, long SourceVersion, long ExpectedHeadVersion);
/// <summary>
/// Defines the API data contract for flow version mutation.
/// 定义流程版本变更的 API 数据契约。
/// </summary>
public sealed record FlowVersionMutationDto(FlowVersionSummaryDto Version, FlowDiffDto Diff, FlowDefinitionDto? Definition = null);
/// <summary>
/// Carries request data for script compile operations.
/// 承载脚本编译操作的请求数据。
/// </summary>
public sealed record ScriptCompileRequestDto(string Source, string? SourceName, string LanguageVersion, IReadOnlyList<ScriptValueContractDto> Inputs);
/// <summary>
/// Defines the API data contract for script compile diagnostic.
/// 定义脚本编译诊断的 API 数据契约。
/// </summary>
public sealed record ScriptCompileDiagnosticDto(string Code, string Message, string Severity, string? SourceName = null, int? Line = null, int? Column = null);
/// <summary>
/// Captures the outcome of script compile operations.
/// 记录脚本编译操作的结果。
/// </summary>
public sealed record ScriptCompileResultDto(bool IsSuccess, string SourceHash, string LanguageVersion, string? SourceName, TimeSpan Duration, IReadOnlyList<ScriptCompileDiagnosticDto> Diagnostics);
/// <summary>
/// Defines the API data contract for library package preview.
/// 定义库包预览的 API 数据契约。
/// </summary>
public sealed record LibraryPackagePreviewDto(Guid PreviewId, string FileName, long SizeBytes, string Sha256, DateTimeOffset ExpiresAt, string PreviewFingerprint, bool CanApply, LibraryDto? Library, IReadOnlyList<ValidationDiagnosticDto> Diagnostics, bool AlreadyExists, string? DllSha256 = null, LibraryArtifactCompatibilityDto? Compatibility = null, LibraryPackageProjectImpactDto? ProjectImpact = null, bool IsPreviewOnly = true);
/// <summary>
/// Defines the API data contract for project library attach preview.
/// 定义项目库关联预览的 API 数据契约。
/// </summary>
public sealed record ProjectLibraryAttachPreviewDto(Guid PreviewId, Guid ProjectId, string LibraryId, DateTimeOffset ExpiresAt, string PreviewFingerprint, bool CanApply, IReadOnlyList<ValidationDiagnosticDto> Diagnostics, bool IsPreviewOnly = true);
/// <summary>
/// Carries request data for project library attach operations.
/// 承载项目库关联操作的请求数据。
/// </summary>
public sealed record ProjectLibraryAttachRequestDto(Guid ProjectId, string LibraryId);
/// <summary>
/// Carries request data for library family assignment MCP operations.
/// 承载库系列AssignmentMCP操作的请求数据。
/// </summary>
public sealed record LibraryFamilyAssignmentMcpRequestDto(string LibraryId, string? FamilyId, string? Name, string? Description = null);
/// <summary>
/// Defines the API data contract for library family assignment MCP preview.
/// 定义库系列AssignmentMCP预览的 API 数据契约。
/// </summary>
public sealed record LibraryFamilyAssignmentMcpPreviewDto(Guid PreviewId, LibraryFamilyAssignmentMcpRequestDto Request, DateTimeOffset ExpiresAt, string PreviewFingerprint, bool CanApply, IReadOnlyList<ValidationDiagnosticDto> Diagnostics, LibraryFamilyDto? CurrentFamily, LibraryFamilyDto? TargetFamily, bool IsPreviewOnly = true);
/// <summary>
/// Carries request data for MCP library upgrade preview operations.
/// 承载MCP库升级预览操作的请求数据。
/// </summary>
public sealed record McpLibraryUpgradePreviewRequestDto(Guid ProjectId, string SourceArtifactId, string TargetArtifactId, IReadOnlyList<Guid> FlowIds);
/// <summary>
/// Carries request data for MCP library upgrade apply operations.
/// 承载MCP库升级应用操作的请求数据。
/// </summary>
public sealed record McpLibraryUpgradeApplyRequestDto(Guid PreviewId, string PreviewFingerprint, string Confirmation, string IdempotencyKey, IReadOnlyList<ApplyLibraryUpgradeRequestDto> Flows);
/// <summary>
/// Defines the API data contract for MCP library upgrade preview.
/// 定义MCP库升级预览的 API 数据契约。
/// </summary>
public sealed record McpLibraryUpgradePreviewDto(Guid PreviewId, DateTimeOffset ExpiresAt, string PreviewFingerprint, bool CanApply, LibraryUpgradePlanDto Plan, bool IsPreviewOnly = true);
/// <summary>
/// Defines the API data contract for MCP API key.
/// 定义MCPAPI密钥的 API 数据契约。
/// </summary>

/// <summary>
/// MCP-only start contract. The HTTP API keeps using
/// <see cref = "StartFlowDebugSessionRequestDto"/> without an idempotency key.
/// 仅用于 MCP 的启动契约；HTTP API 继续使用不含幂等键的 StartFlowDebugSessionRequestDto。
/// </summary>
public sealed record McpStartFlowDebugSessionRequestDto(Guid ProjectId, Guid FlowId, IReadOnlyList<string>? BreakpointNodeIds, IReadOnlyDictionary<string, JsonElement>? ProjectInputs = null, int? TimeoutSeconds = null, int? MaxSteps = null, int? MaxNodeVisits = null, long? ExpectedFlowVersion = null, int? MaxQueuedFlipflopTriggers = null, string? IdempotencyKey = null);
/// <summary>
/// Defines the API data contract for MCP debug session started.
/// 定义MCP调试会话Started的 API 数据契约。
/// </summary>
public sealed record McpDebugSessionStartedDto(Guid SessionId, Guid RunId, Guid ProjectId, Guid FlowId, FlowDebugSessionStatusDto Status, long StateRevision);
/// <summary>
/// Defines the API data contract for MCP debug command accepted.
/// 定义MCP调试命令已接收的 API 数据契约。
/// </summary>
public sealed record McpDebugCommandAcceptedDto(Guid SessionId, Guid RunId, string Command, long CommandSequence, FlowDebugSessionStatusDto Status, long StateRevision, bool Accepted = true);

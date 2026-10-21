using System.Text.Json;

namespace SereinFlow.Contracts;
public static class WorkerProtocol
{
    public const int Version = 2;
}

/// <summary>
/// Identifies a node kind in API contracts.
/// 标识 API 契约中的节点类型。
/// </summary>
public enum NodeTypeDto
{
    /// <summary>
    /// Indicates the action value.
    /// 表示“动作”这一取值。
    /// </summary>
    Action = 0,
    /// <summary>
    /// Indicates the flipflop value.
    /// 表示“触发器”这一取值。
    /// </summary>
    Flipflop = 1,
    /// <summary>
    /// Indicates the script value.
    /// 表示“脚本”这一取值。
    /// </summary>
    Script = 2,
    // 3 is intentionally reserved for the removed Condition node.
    // 3 专门保留给已移除的 Condition 节点，不能复用。
    FlowCall = 4,
}

/// <summary>
/// Identifies a canvas lifecycle stage in API contracts.
/// 标识 API 契约中的画布生命周期阶段。
/// </summary>
public enum CanvasLifecycleDto
{
    /// <summary>
    /// Indicates the main value.
    /// 表示“主画布”这一取值。
    /// </summary>
    Main,
    /// <summary>
    /// Indicates the init value.
    /// 表示“初始化”这一取值。
    /// </summary>
    Init,
    /// <summary>
    /// Indicates the loading value.
    /// 表示“加载”这一取值。
    /// </summary>
    Loading,
    /// <summary>
    /// Indicates the exit value.
    /// 表示“退出”这一取值。
    /// </summary>
    Exit,
    /// <summary>
    /// Indicates the custom value.
    /// 表示“自定义”这一取值。
    /// </summary>
    Custom
}

/// <summary>
/// Identifies a connection kind in API contracts.
/// 标识 API 契约中的连线类型。
/// </summary>
public enum ConnectionKindDto
{
    /// <summary>
    /// Indicates the execution value.
    /// 表示“执行”这一取值。
    /// </summary>
    Execution,
    /// <summary>
    /// Indicates the data value.
    /// 表示“数据”这一取值。
    /// </summary>
    Data
}

/// <summary>
/// Identifies an execution branch in API contracts.
/// 标识 API 契约中的执行分支。
/// </summary>
public enum ExecutionBranchDto
{
    /// <summary>
    /// Indicates the success value.
    /// 表示“成功”这一取值。
    /// </summary>
    Success,
    /// <summary>
    /// Indicates the failure value.
    /// 表示“失败”这一取值。
    /// </summary>
    Failure,
    /// <summary>
    /// Indicates the error value.
    /// 表示“错误”这一取值。
    /// </summary>
    Error
}

/// <summary>
/// Identifies a parameter value source in API contracts.
/// 标识 API 契约中的参数取值来源。
/// </summary>
public enum DataSourceDto
{
    /// <summary>
    /// Indicates the literal value.
    /// 表示“字面量”这一取值。
    /// </summary>
    Literal,
    /// <summary>
    /// Indicates the previous node value.
    /// 表示“上游节点”这一取值。
    /// </summary>
    PreviousNode,
    /// <summary>
    /// Indicates the project input value.
    /// 表示“项目输入”这一取值。
    /// </summary>
    ProjectInput,
    /// <summary>
    /// Indicates the expression value.
    /// 表示“表达式”这一取值。
    /// </summary>
    Expression
}

/// <summary>
/// Identifies a run lifecycle state in API contracts.
/// 标识 API 契约中的运行生命周期状态。
/// </summary>
public enum FlowRunStatusDto
{
    /// <summary>
    /// Indicates the pending value.
    /// 表示“等待中”这一取值。
    /// </summary>
    Pending,
    /// <summary>
    /// Indicates the running value.
    /// 表示“运行中”这一取值。
    /// </summary>
    Running,
    /// <summary>
    /// Indicates the succeeded value.
    /// 表示“成功”这一取值。
    /// </summary>
    Succeeded,
    /// <summary>
    /// Indicates the failed value.
    /// 表示“失败”这一取值。
    /// </summary>
    Failed,
    /// <summary>
    /// Indicates the cancelled value.
    /// 表示“已取消”这一取值。
    /// </summary>
    Cancelled,
    /// <summary>
    /// Indicates the timed out value.
    /// 表示“TimedOut”这一取值。
    /// </summary>
    TimedOut,
    /// <summary>
    /// Indicates the interrupted value.
    /// 表示“已中断”这一取值。
    /// </summary>
    Interrupted
}

/// <summary>
/// Identifies the run execution kind in API contracts.
/// 标识 API 契约中的运行执行类型。
/// </summary>
public enum FlowRunExecutionKindDto
{
    /// <summary>
    /// Indicates the production value.
    /// 表示“生产”这一取值。
    /// </summary>
    Production,
    /// <summary>
    /// Indicates the debug value.
    /// 表示“调试”这一取值。
    /// </summary>
    Debug
}

/// <summary>
/// Identifies a flow version track.
/// 标识流程版本轨道。
/// </summary>
public enum FlowVersionTrackDto
{
    /// <summary>
    /// Indicates the development value.
    /// 表示“开发”这一取值。
    /// </summary>
    Development,
    /// <summary>
    /// Indicates the production value.
    /// 表示“生产”这一取值。
    /// </summary>
    Production
}

/// <summary>
/// Identifies the operation that produced a flow version.
/// 标识生成流程版本的操作。
/// </summary>
public enum FlowVersionOperationDto
{
    /// <summary>
    /// Indicates the created value.
    /// 表示“创建”这一取值。
    /// </summary>
    Created,
    /// <summary>
    /// Indicates the saved value.
    /// 表示“已保存”这一取值。
    /// </summary>
    Saved,
    /// <summary>
    /// Indicates the published value.
    /// 表示“发布”这一取值。
    /// </summary>
    Published,
    /// <summary>
    /// Indicates the rolled back value.
    /// 表示“RolledBack”这一取值。
    /// </summary>
    RolledBack,
    /// <summary>
    /// Indicates the library upgraded value.
    /// 表示“库Upgraded”这一取值。
    /// </summary>
    LibraryUpgraded,
    /// <summary>
    /// Indicates the imported value.
    /// 表示“导入”这一取值。
    /// </summary>
    Imported
}

/// <summary>
/// Identifies a debug session state in API contracts.
/// 标识 API 契约中的调试会话状态。
/// </summary>
public enum FlowDebugSessionStatusDto
{
    /// <summary>
    /// Indicates the pending value.
    /// 表示“等待中”这一取值。
    /// </summary>
    Pending,
    /// <summary>
    /// Indicates the running value.
    /// 表示“运行中”这一取值。
    /// </summary>
    Running,
    /// <summary>
    /// Indicates the paused value.
    /// 表示“已暂停”这一取值。
    /// </summary>
    Paused,
    /// <summary>
    /// Indicates the completed value.
    /// 表示“Completed”这一取值。
    /// </summary>
    Completed,
    /// <summary>
    /// Indicates the cancelled value.
    /// 表示“已取消”这一取值。
    /// </summary>
    Cancelled,
    /// <summary>
    /// Indicates the failed value.
    /// 表示“失败”这一取值。
    /// </summary>
    Failed
}

/// <summary>
/// Identifies a concurrency policy in API contracts.
/// 标识 API 契约中的并发策略。
/// </summary>
public enum FlowConcurrencyModeDto
{
    /// <summary>
    /// Indicates the parallel value.
    /// 表示“并行”这一取值。
    /// </summary>
    Parallel,
    /// <summary>
    /// Indicates the exclusive reject value.
    /// 表示“Exclusive拒绝”这一取值。
    /// </summary>
    ExclusiveReject
}

/// <summary>
/// Defines whether flow invocation is synchronous or asynchronous.
/// 定义流程调用采用同步还是异步方式。
/// </summary>
public enum FlowInvocationModeDto
{
    /// <summary>
    /// Indicates the asynchronous value.
    /// 表示“异步”这一取值。
    /// </summary>
    Asynchronous,
    /// <summary>
    /// Indicates the synchronous value.
    /// 表示“同步”这一取值。
    /// </summary>
    Synchronous
}

/// <summary>
/// Identifies a library lifecycle state in API contracts.
/// 标识 API 契约中的库生命周期状态。
/// </summary>
public enum LibraryLifecycleDto
{
    /// <summary>
    /// Indicates the available value.
    /// 表示“可用”这一取值。
    /// </summary>
    Available,
    /// <summary>
    /// Indicates the archived value.
    /// 表示“已归档”这一取值。
    /// </summary>
    Archived
}

/// <summary>
/// States whether a node/parameter identity is stable under the current
/// attribute rules or was derived from legacy metadata. Stable identities may
/// be explicitly declared or deterministically inferred by the PE scanner.
/// 标明节点/参数身份是当前特性规则下稳定的，还是从旧元数据推导而来。稳定身份
/// 可以由类库作者显式声明，也可以由 PE 扫描器确定性推导。
/// </summary>
public enum LibraryContractIdentityConfidenceDto
{
    /// <summary>
    /// Indicates the explicit value.
    /// 表示“明确”这一取值。
    /// </summary>
    Explicit,
    /// <summary>
    /// Indicates the legacy value.
    /// 表示“旧版”这一取值。
    /// </summary>
    Legacy
}

/// <summary>
/// Deterministic compatibility classification used by the library upgrade
/// preview. Values are ordered by neither severity nor UI color.
/// 类库升级预览使用的确定性兼容性分类。这些值不表示严重程度排序，也不等同于 UI 颜色。
/// </summary>
public enum LibraryCompatibilityClassificationDto
{
    /// <summary>
    /// Indicates the exact value.
    /// 表示“完全匹配”这一取值。
    /// </summary>
    Exact,
    /// <summary>
    /// Indicates the compatible value.
    /// 表示“兼容”这一取值。
    /// </summary>
    Compatible,
    /// <summary>
    /// Indicates the requires mapping value.
    /// 表示“RequiresMapping”这一取值。
    /// </summary>
    RequiresMapping,
    /// <summary>
    /// Indicates the requires rewire value.
    /// 表示“RequiresRewire”这一取值。
    /// </summary>
    RequiresRewire,
    /// <summary>
    /// Indicates the breaking value.
    /// 表示“不兼容”这一取值。
    /// </summary>
    Breaking,
    /// <summary>
    /// Indicates the unknown value.
    /// 表示“未知”这一取值。
    /// </summary>
    Unknown
}

/// <summary>
/// Identifies the status of a library upgrade plan.
/// 标识库升级计划的状态。
/// </summary>
public enum LibraryUpgradePlanStatusDto
{
    /// <summary>
    /// Indicates the analyzed value.
    /// 表示“已分析”这一取值。
    /// </summary>
    Analyzed,
    /// <summary>
    /// Indicates the applied value.
    /// 表示“已应用”这一取值。
    /// </summary>
    Applied,
    /// <summary>
    /// Indicates the failed value.
    /// 表示“失败”这一取值。
    /// </summary>
    Failed,
    /// <summary>
    /// Indicates the superseded value.
    /// 表示“已替代”这一取值。
    /// </summary>
    Superseded
}

/// <summary>
/// Identifies an event emitted by a worker.
/// 标识 Worker 发出的事件。
/// </summary>
public enum WorkerEventType
{
    /// <summary>
    /// Indicates the run started value.
    /// 表示“运行Started”这一取值。
    /// </summary>
    RunStarted,
    /// <summary>
    /// Indicates the node started value.
    /// 表示“节点Started”这一取值。
    /// </summary>
    NodeStarted,
    /// <summary>
    /// Indicates the node completed value.
    /// 表示“节点Completed”这一取值。
    /// </summary>
    NodeCompleted,
    /// <summary>
    /// Indicates the node failed value.
    /// 表示“节点失败”这一取值。
    /// </summary>
    NodeFailed,
    /// <summary>
    /// Indicates the node errored value.
    /// 表示“节点Errored”这一取值。
    /// </summary>
    NodeErrored,
    /// <summary>
    /// Indicates the log value.
    /// 表示“日志”这一取值。
    /// </summary>
    Log,
    /// <summary>
    /// Indicates the run completed value.
    /// 表示“运行Completed”这一取值。
    /// </summary>
    RunCompleted,
    /// <summary>
    /// Indicates the run cancelled value.
    /// 表示“运行已取消”这一取值。
    /// </summary>
    RunCancelled,
    /// <summary>
    /// Indicates the debug paused value.
    /// 表示“调试已暂停”这一取值。
    /// </summary>
    DebugPaused,
    /// <summary>
    /// Indicates the debug trigger received value.
    /// 表示“调试TriggerReceived”这一取值。
    /// </summary>
    DebugTriggerReceived,
    /// <summary>
    /// Indicates the debug trigger queued value.
    /// 表示“调试TriggerQueued”这一取值。
    /// </summary>
    DebugTriggerQueued,
    /// <summary>
    /// Indicates the debug trigger admitted value.
    /// 表示“调试TriggerAdmitted”这一取值。
    /// </summary>
    DebugTriggerAdmitted,
    /// <summary>
    /// Indicates the debug trigger rejected value.
    /// 表示“调试Trigger已拒绝”这一取值。
    /// </summary>
    DebugTriggerRejected,
    /// <summary>
    /// Indicates the debug trigger completed value.
    /// 表示“调试TriggerCompleted”这一取值。
    /// </summary>
    DebugTriggerCompleted,
    /// <summary>
    /// Indicates the debug trigger failed value.
    /// 表示“调试Trigger失败”这一取值。
    /// </summary>
    DebugTriggerFailed
}

/// <summary>
/// Defines the API data contract for project.
/// 定义项目的 API 数据契约。
/// </summary>
public sealed record ProjectDto(Guid Id, string Name, long Version, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
/// <summary>
/// Defines the API data contract for flow definition summary.
/// 定义流程定义摘要的 API 数据契约。
/// </summary>
public sealed record FlowDefinitionSummaryDto(Guid Id, long Version, string EntryNodeId, int CanvasCount = 0, int NodeCount = 0, long? ProductionVersion = null);
/// <summary>
/// Defines the API data contract for project workspace.
/// 定义项目工作区的 API 数据契约。
/// </summary>
public sealed record ProjectWorkspaceDto(ProjectDto Project, IReadOnlyList<FlowDefinitionSummaryDto> Flows);
/// <summary>
/// Carries request data for create project operations.
/// 承载创建项目操作的请求数据。
/// </summary>
public sealed record CreateProjectRequestDto(string Name, FlowDefinitionDto Definition);
/// <summary>
/// Carries request data for rename project operations.
/// 承载重命名项目操作的请求数据。
/// </summary>
public sealed record RenameProjectRequestDto(string Name, long ExpectedVersion);
/// <summary>
/// Carries request data for update flow definition operations.
/// 承载更新流程定义操作的请求数据。
/// </summary>
public sealed record UpdateFlowDefinitionRequestDto(long ExpectedVersion, FlowDefinitionDto Definition);
/// <summary>
/// Defines the API data contract for flow version summary.
/// 定义流程版本摘要的 API 数据契约。
/// </summary>
public sealed record FlowVersionSummaryDto(Guid FlowId, long Version, FlowVersionTrackDto Track, FlowVersionOperationDto Operation, long? ParentVersion, long? SourceVersion, string Remark, DateTimeOffset? CreatedAt, bool IsCurrent);
/// <summary>
/// Defines the API data contract for flow version detail.
/// 定义流程版本详情的 API 数据契约。
/// </summary>
public sealed record FlowVersionDetailDto(FlowVersionSummaryDto Version, FlowDefinitionDto Definition);
/// <summary>
/// Carries request data for publish flow version operations.
/// 承载发布流程版本操作的请求数据。
/// </summary>
public sealed record PublishFlowVersionRequestDto(long ExpectedDevelopmentVersion, string? Remark = null);
/// <summary>
/// Carries request data for rollback flow version operations.
/// 承载回滚流程版本操作的请求数据。
/// </summary>
public sealed record RollbackFlowVersionRequestDto(FlowVersionTrackDto Track, long ExpectedHeadVersion);
/// <summary>
/// Carries request data for run flow operations.
/// 承载运行流程操作的请求数据。
/// </summary>
public sealed record RunFlowRequestDto(long? ExpectedFlowVersion, IReadOnlyDictionary<string, JsonElement>? ProjectInputs, int? TimeoutSeconds, int? MaxSteps, int? MaxNodeVisits = null);
/// <summary>
/// Defines the API data contract for canvas.
/// 定义画布的 API 数据契约。
/// </summary>
public sealed record CanvasDto(string Id, CanvasLifecycleDto Lifecycle, IReadOnlyList<NodeDto> Nodes, IReadOnlyList<ConnectionDto> Connections, string? Name = null);
/// <summary>
/// Defines the API data contract for node.
/// 定义节点的 API 数据契约。
/// </summary>
public sealed record NodeDto(string Id, NodeTypeDto Type, string DisplayName, double X, double Y, IReadOnlyList<NodePortDto> Ports, IReadOnlyList<NodeParameterDto> Parameters, ScriptNodeDataDto? Script, NodeUiMetadataDto? Ui = null);
/// <summary>
/// Defines the API data contract for node ui metadata.
/// 定义节点UiMetadata的 API 数据契约。
/// </summary>
public sealed record NodeUiMetadataDto(string Kind, string TitleKey, string SubtitleKey, string? Description, string Status, bool HasDataOutput, double? Width, string? Category = null, string? LibraryId = null, string? ClassName = null, string? MethodName = null, string? DllName = null, string? DllVersion = null, string? ReturnType = null, string? TargetNodeId = null, string? TargetFlowId = null, bool? IsAwaitable = null, string? StaticReturnType = null, bool? IsDynamicReturnType = null, string? TargetCanvasId = null, bool? IsPublic = null, IReadOnlyList<FlowCallParameterBindingDto>? FlowCallParameterBindings = null, string? LibraryNodeContractId = null, string? FlowLibraryName = null);
/// <summary>
/// Defines the API data contract for node port.
/// 定义节点端口的 API 数据契约。
/// </summary>
public sealed record NodePortDto(string Id, string Name, string Direction, bool Required);
/// <summary>
/// Defines the API data contract for node parameter.
/// 定义节点参数的 API 数据契约。
/// </summary>
public sealed record NodeParameterDto(string Name, string? ValueJson, DataSourceDto Source, bool Required, NodeParameterUiMetadataDto? Ui = null);
/// <summary>
/// Defines the API data contract for node parameter ui metadata.
/// 定义节点参数UiMetadata的 API 数据契约。
/// </summary>
public sealed record NodeParameterUiMetadataDto(string Id, string NameKey, string ValueKind, string? LiteralValue, string? ProjectInputKey, string? Expression, string? SourceNodeId, string? SourcePortId, string? Type = null, string? Description = null, string? InputMode = null, bool? IsVariadic = null, string? VariadicGroupId = null, string? ElementType = null, string? VariadicMode = null, EnumParameterMetadataDto? EnumMetadata = null);
/// <summary>
/// A display-safe enum member extracted from PE metadata. Numeric values are
/// strings so unsigned 64-bit enum values never lose precision in JSON/JS.
/// 从 PE 元数据提取的安全枚举成员。数值以字符串保存，避免无符号 64 位枚举值在 JSON/JS 中丢失精度。
/// </summary>
public sealed record EnumValueOptionDto(string Name, string NumericValue);
/// <summary>
/// Enum metadata travels with a parameter definition rather than resolving a
/// potentially newer library catalog at run/snapshot read time.
/// 枚举元数据随参数定义保存，不会在读取运行快照时改用可能已更新的类库目录。
/// </summary>
public sealed record EnumParameterMetadataDto(string TypeName, bool IsFlags, string UnderlyingType, IReadOnlyList<EnumValueOptionDto> Options);
/// <summary>
/// Defines the API data contract for connection.
/// 定义连线的 API 数据契约。
/// </summary>
public sealed record ConnectionDto(string Id, string FromNodeId, string FromPortId, string ToNodeId, string ToPortId, ConnectionKindDto Kind, ExecutionBranchDto? Branch, DataSourceDto? DataSource, int Priority);
/// <summary>
/// Defines the API data contract for script node data.
/// 定义脚本节点Data的 API 数据契约。
/// </summary>
public sealed record ScriptNodeDataDto(string NodeId, string Source, string LanguageVersion, string SourceHash, IReadOnlyList<ScriptValueContractDto> Inputs, IReadOnlyList<ScriptValueContractDto> Outputs);
/// <summary>
/// Defines the API data contract for script value contract.
/// 定义脚本值契约的 API 数据契约。
/// </summary>
public sealed record ScriptValueContractDto(string Name, string ValueKind, bool Required, string? Id = null, string? Description = null);
/// <summary>
/// Defines the API data contract for flow call parameter binding.
/// 定义流程Call参数Binding的 API 数据契约。
/// </summary>
public sealed record FlowCallParameterBindingDto(string CallParameterId, string TargetParameterId);
/// <summary>
/// Defines the API data contract for builtin node catalog.
/// 定义Builtin节点Catalog的 API 数据契约。
/// </summary>
public sealed record BuiltinNodeCatalogDto(IReadOnlyList<NodeCreationDescriptorDto> Nodes);
/// <summary>
/// Defines the API data contract for node creation descriptor.
/// 定义节点创建Descriptor的 API 数据契约。
/// </summary>
public sealed record NodeCreationDescriptorDto(string Id, NodeTypeDto Type, string DisplayName, string? Description, NodeUiMetadataDto Ui, ScriptNodeDataDto? Script = null, IReadOnlyList<NodeParameterDto>? Parameters = null);
/// <summary>
/// Defines the API data contract for flow definition.
/// 定义流程定义的 API 数据契约。
/// </summary>
public sealed record FlowDefinitionDto(Guid Id, int SchemaVersion, long Version, IReadOnlyList<CanvasDto> Canvases, string EntryNodeId, string Checksum, FlowUiMetadataDto? Ui = null, FlowRunPolicyDto? RunPolicy = null);
/// <summary>
/// Defines the API data contract for flow run policy.
/// 定义流程运行策略的 API 数据契约。
/// </summary>
public sealed record FlowRunPolicyDto(FlowConcurrencyModeDto ConcurrencyMode);
/// <summary>
/// Defines the API data contract for flow ui metadata.
/// 定义流程UiMetadata的 API 数据契约。
/// </summary>
public sealed record FlowUiMetadataDto(FlowConnectionLineTypesDto? ConnectionLineTypes = null, FlowCanvasFocusSettingsDto? CanvasFocusSettings = null);
/// <summary>
/// Defines the API data contract for flow connection line types.
/// 定义流程连线LineTypes的 API 数据契约。
/// </summary>
public sealed record FlowConnectionLineTypesDto(string? Execution = null, string? Data = null);
/// <summary>
/// Defines the API data contract for flow canvas focus settings.
/// 定义流程画布Focus设置的 API 数据契约。
/// </summary>
public sealed record FlowCanvasFocusSettingsDto(bool? Enabled = null, bool? ParameterSources = null, bool? ParameterConsumers = null, bool? Callers = null, bool? Callees = null);
/// <summary>
/// Defines the API data contract for plugin manifest.
/// 定义PluginManifest的 API 数据契约。
/// </summary>
public sealed record PluginManifestDto(string AssemblyName, string AssemblyVersion, string Sha256, IReadOnlyList<string> TargetRids, string ApiVersion, IReadOnlyList<string> NodeTypes);
/// <summary>
/// A server-owned class library package. The package path is intentionally not
/// exposed to clients; only immutable metadata and the safe node catalog cross
/// the API boundary.
/// 服务端管理的类库包。包路径不会暴露给客户端，只有不可变元数据和安全节点目录会跨越 API 边界。
/// </summary>
public sealed record LibraryDto(string Id, string Name, string Version, string FileName, long SizeBytes, string Sha256, DateTimeOffset UploadedAt, IReadOnlyList<LibraryNodeDto> Nodes, LibraryLifecycleDto Lifecycle = LibraryLifecycleDto.Available, string? FamilyId = null, string? SemanticVersion = null, LibraryArtifactManifestDto? CompatibilityManifest = null, string? FamilyName = null, string? DllSha256 = null);
/// <summary>
/// Defines the API data contract for library node.
/// 定义库节点的 API 数据契约。
/// </summary>
public sealed record LibraryNodeDto(string Id, NodeTypeDto Type, string DisplayName, string? Description, string LibraryId, string ClassName, string MethodName, string DllName, string DllVersion, string ReturnType, IReadOnlyList<LibraryParameterDto> Parameters, bool IsAwaitable = false, string? ContractId = null, string? OverloadSignature = null, LibraryContractIdentityConfidenceDto IdentityConfidence = LibraryContractIdentityConfidenceDto.Legacy, string? FlowLibraryName = null, bool HasResultConverter = false);
/// <summary>
/// Defines the API data contract for library parameter.
/// 定义库参数的 API 数据契约。
/// </summary>
public sealed record LibraryParameterDto(string Id, string Name, string Type, string? Description, bool Required, bool IsVariadic = false, string? VariadicGroupId = null, string? ElementType = null, EnumParameterMetadataDto? EnumMetadata = null, string? DefaultValue = null, IReadOnlyList<string>? Aliases = null, LibraryContractIdentityConfidenceDto IdentityConfidence = LibraryContractIdentityConfidenceDto.Legacy);
/// <summary>
/// Immutable compatibility snapshot generated from PE metadata at upload time.
/// It is separate from the presentation catalog so later UI changes cannot
/// alter the ABI used by a class-library upgrade analysis.
/// 上传时由 PE 元数据生成的不可变兼容性快照。它与展示目录分离，后续 UI
/// 调整不会改变类库升级分析所依据的 ABI。
/// </summary>
public sealed record LibraryArtifactManifestDto(string ArtifactId, string AssemblyName, string AssemblyVersion, string SemanticVersion, IReadOnlyList<LibraryManifestNodeDto> Nodes);
/// <summary>
/// Contract-level compatibility information produced while a completed ZIP
/// package is being previewed. It does not imply that the target artifact has
/// been imported or attached to a project.
/// 类库 ZIP 预览期间生成的契约级兼容性信息。不表示目标工件已经导入或接入项目。
/// </summary>
public sealed record LibraryArtifactCompatibilityDto(string BaselineArtifactId, string BaselineVersion, string TargetArtifactId, string TargetVersion, bool IsCompatible, IReadOnlyList<LibraryArtifactCompatibilityIssueDto> Issues);
/// <summary>
/// Defines the API data contract for library artifact compatibility issue.
/// 定义库制品兼容性Issue的 API 数据契约。
/// </summary>
public sealed record LibraryArtifactCompatibilityIssueDto(string Id, LibraryCompatibilityClassificationDto Classification, string Code, string Message, string? SourceNodeContractId = null, string? TargetNodeContractId = null, string? SourceParameterId = null, string? TargetParameterId = null, bool BlocksApplication = false);
/// <summary>
/// Bounded impact counts for a package replacement candidate. The package is
/// not persisted when this information is produced.
/// 候选类库替换包的有界影响统计。生成该信息时不会持久化类库包。
/// </summary>
public sealed record LibraryPackageProjectImpactDto(Guid ProjectId, string? BaselineArtifactId, int AffectedFlowCount, int AffectedNodeCount, IReadOnlyList<Guid> AffectedFlowIds);
/// <summary>
/// A logical product line containing immutable library artifacts. The family
/// never replaces an artifact identity; flows and runs continue to bind SHA
/// artifact IDs directly.
/// 逻辑上的类库产品线，包含多个不可变类库工件。类库族不会替代工件身份；
/// 流程和运行仍直接绑定 SHA 工件 ID。
/// </summary>
public sealed record LibraryFamilyDto(string Id, string Name, string? Description, string? LatestArtifactId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<LibraryDto>? Artifacts = null);
/// <summary>
/// Read-only impact counts for one immutable library artifact. Current flows
/// are counted from the latest saved definition of each flow; version and run
/// counts retain the historical audit footprint.
/// 单个不可变类库工件的只读影响统计。当前流程按每个流程最新保存的定义统计；
/// 流程版本和运行次数则保留历史审计范围。
/// </summary>
public sealed record LibraryArtifactUsageDto(string LibraryArtifactId, int CurrentFlowCount, int FlowVersionCount, int RunSnapshotCount);
/// <summary>
/// Assigns an immutable artifact to an existing family, or creates a new
/// family from Name when FamilyId is absent. Neither option changes the ZIP.
/// 当提供 FamilyId 时把不可变工件归入现有类库族；缺少 FamilyId 时按 Name
/// 创建新类库族。两种操作都不会更改 ZIP 工件。
/// </summary>
public sealed record AssignLibraryFamilyRequestDto(string? FamilyId, string? Name, string? Description = null);
/// <summary>
/// Carries request data for update library lifecycle operations.
/// 承载更新库Lifecycle操作的请求数据。
/// </summary>
public sealed record UpdateLibraryLifecycleRequestDto(LibraryLifecycleDto Lifecycle);
/// <summary>
/// Carries request data for library upgrade preview operations.
/// 承载库升级预览操作的请求数据。
/// </summary>
public sealed record LibraryUpgradePreviewRequestDto(string SourceArtifactId, string TargetArtifactId, IReadOnlyList<Guid> FlowIds);
/// <summary>
/// Defines the API data contract for library upgrade issue.
/// 定义库升级Issue的 API 数据契约。
/// </summary>
public sealed record LibraryUpgradeIssueDto(string Id, LibraryCompatibilityClassificationDto Classification, string Code, string Message, string? CanvasId = null, string? NodeId = null, string? SourceNodeContractId = null, string? TargetNodeContractId = null, string? SourceParameterId = null, string? TargetParameterId = null, string? ConnectionId = null, bool RequiresAcknowledgement = false, bool BlocksApplication = false);
/// <summary>
/// Defines the API data contract for flow library upgrade preview.
/// 定义流程库升级预览的 API 数据契约。
/// </summary>
public sealed record FlowLibraryUpgradePreviewDto(Guid FlowId, long FlowVersion, bool CanApply, int AffectedNodeCount, IReadOnlyList<LibraryUpgradeIssueDto> Issues);
/// <summary>
/// Defines the API data contract for library upgrade plan.
/// 定义库升级计划的 API 数据契约。
/// </summary>
public sealed record LibraryUpgradePlanDto(Guid Id, Guid ProjectId, string SourceArtifactId, string TargetArtifactId, LibraryUpgradePlanStatusDto Status, IReadOnlyList<FlowLibraryUpgradePreviewDto> Flows, DateTimeOffset CreatedAt, DateTimeOffset? AppliedAt = null, string? FailureMessage = null, IReadOnlyList<LibraryUpgradePlanFlowResultDto>? AppliedFlows = null);
/// <summary>
/// Immutable audit entry for one successfully applied flow within a library
/// upgrade plan. A plan may apply several flows independently.
/// 类库升级计划中单个流程成功应用的不可变审计记录。一个计划可以独立应用多个流程。
/// </summary>
public sealed record LibraryUpgradePlanFlowResultDto(Guid FlowId, long PreviousVersion, long NewVersion, DateTimeOffset AppliedAt);
/// <summary>
/// Phase 3 applies one selected flow at a time. The expected version prevents
/// a preview from overwriting an editor's newer definition.
/// 第三阶段一次只应用一个选定流程。期望版本可防止旧预览覆盖编辑器中的新定义。
/// </summary>
public sealed record ApplyLibraryUpgradeRequestDto(Guid FlowId, long ExpectedFlowVersion, IReadOnlyList<string>? AcknowledgedItemIds = null);
/// <summary>
/// Captures the outcome of library upgrade apply operations.
/// 记录库升级应用操作的结果。
/// </summary>
public sealed record LibraryUpgradeApplyResultDto(Guid FlowId, long PreviousVersion, long NewVersion, string SourceArtifactId, string TargetArtifactId, int MigratedNodeCount, IReadOnlyList<LibraryUpgradeIssueDto> Issues);
/// <summary>
/// A batch keeps every flow result explicit: success on one flow never hides
/// a conflict or validation failure on another flow.
/// 批量操作会明确保留每个流程的结果：一个流程成功不会掩盖其它流程的冲突或校验失败。
/// </summary>
public sealed record ApplyLibraryUpgradeBatchRequestDto(IReadOnlyList<ApplyLibraryUpgradeRequestDto> Flows);
/// <summary>
/// Defines the API data contract for library upgrade apply failure.
/// 定义库升级应用失败的 API 数据契约。
/// </summary>
public sealed record LibraryUpgradeApplyFailureDto(Guid FlowId, int StatusCode, string? Code, string? Message, long? CurrentVersion = null);
/// <summary>
/// Captures the outcome of library upgrade batch apply operations.
/// 记录库升级Batch应用操作的结果。
/// </summary>
public sealed record LibraryUpgradeBatchApplyResultDto(Guid PlanId, IReadOnlyList<LibraryUpgradeApplyResultDto> Succeeded, IReadOnlyList<LibraryUpgradeApplyFailureDto> Failed);
/// <summary>
/// Defines the API data contract for library manifest node.
/// 定义库Manifest节点的 API 数据契约。
/// </summary>
public sealed record LibraryManifestNodeDto(string ContractId, LibraryContractIdentityConfidenceDto IdentityConfidence, NodeTypeDto Type, string DeclaringType, string MethodName, string OverloadSignature, string ReturnType, bool IsAwaitable, IReadOnlyList<LibraryManifestParameterDto> Parameters, bool HasResultConverter = false);
/// <summary>
/// Defines the API data contract for library manifest parameter.
/// 定义库Manifest参数的 API 数据契约。
/// </summary>
public sealed record LibraryManifestParameterDto(string ContractId, LibraryContractIdentityConfidenceDto IdentityConfidence, IReadOnlyList<string> Aliases, string ClrName, string DisplayName, string Type, bool Required, string? DefaultValue, bool IsVariadic, string? ElementType, EnumParameterMetadataDto? EnumMetadata, bool IsInjectedFlowContext = false);
/// <summary>
/// Captures the outcome of library upload operations.
/// 记录库上传操作的结果。
/// </summary>
public sealed record LibraryUploadResultDto(LibraryDto Library, bool AlreadyExists);
/// <summary>
/// Defines the API data contract for project library reference.
/// 定义项目库Reference的 API 数据契约。
/// </summary>
public sealed record ProjectLibraryReferenceDto(Guid ProjectId, string LibraryId, DateTimeOffset ReferencedAt, LibraryDto Library);
/// <summary>
/// Captures the outcome of flow validation operations.
/// 记录流程校验操作的结果。
/// </summary>
public sealed record FlowValidationResultDto(bool IsValid, IReadOnlyList<ValidationDiagnosticDto> Diagnostics);
/// <summary>
/// Defines the API data contract for validation diagnostic.
/// 定义校验诊断的 API 数据契约。
/// </summary>
public sealed record ValidationDiagnosticDto(string Code, string Message, string? Path);
/// <summary>
/// Defines the API data contract for flow run.
/// 定义流程运行的 API 数据契约。
/// </summary>
public sealed record FlowRunDto(Guid Id, Guid FlowId, long FlowVersion, FlowRunStatusDto Status, DateTimeOffset? StartedAt, DateTimeOffset? EndedAt, string? ErrorSummary, Guid? ProjectId = null, DateTimeOffset? CreatedAt = null, string? CancellationReason = null, FlowConcurrencyModeDto? ConcurrencyMode = null, bool IsListenerRun = false, DateTimeOffset? QueuedAt = null, FlowRunExecutionKindDto ExecutionKind = FlowRunExecutionKindDto.Production, Guid? DebugSessionId = null, string? DefinitionChecksum = null);
/// <summary>
/// Carries request data for start flow debug session operations.
/// 承载启动流程调试会话操作的请求数据。
/// </summary>
public sealed record StartFlowDebugSessionRequestDto(IReadOnlyList<string>? BreakpointNodeIds, IReadOnlyDictionary<string, JsonElement>? ProjectInputs = null, int? TimeoutSeconds = null, int? MaxSteps = null, int? MaxNodeVisits = null, long? ExpectedFlowVersion = null, int? MaxQueuedFlipflopTriggers = null, FlowDefinitionDto? Definition = null);
/// <summary>
/// Defines the API data contract for flow debug session.
/// 定义流程调试会话的 API 数据契约。
/// </summary>
public sealed record FlowDebugSessionDto(Guid Id, Guid RunId, Guid ProjectId, Guid FlowId, FlowDebugSessionStatusDto Status, IReadOnlyList<string> BreakpointNodeIds, string? CurrentNodeId, Guid? ActiveInvocationId, string? ActiveFlipflopNodeId, int QueuedTriggerCount, long LastCommandSequence, string? FailureMessage, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long StateRevision = 0, FlowDebugPauseStateDto? PauseState = null, FlowDebugNodeResultDto? LastNodeResult = null);
/// <summary>
/// Defines the API data contract for flow debug pause state.
/// 定义流程调试暂停状态的 API 数据契约。
/// </summary>
public sealed record FlowDebugPauseStateDto(string NodeId, string NodeType, int Step, int FrameDepth, Guid? InvocationId, long BoundarySequence, JsonElement Inputs, DateTimeOffset PausedAt, Guid? ExecutionId = null);
/// <summary>
/// Captures the outcome of flow debug node operations.
/// 记录流程调试节点操作的结果。
/// </summary>
public sealed record FlowDebugNodeResultDto(string NodeId, long Sequence, DateTimeOffset CompletedAt, string Outcome, string? Branch, JsonElement Inputs, JsonElement Outputs, string? ErrorCode, string? ErrorMessage);
/// <summary>
/// Captures the outcome of flow debug wait operations.
/// 记录流程调试等待操作的结果。
/// </summary>
public sealed record FlowDebugWaitResultDto(bool HasChanged, bool TimedOut, FlowDebugSessionDto Session);
/// <summary>
/// Carries request data for flow debug command operations.
/// 承载流程调试命令操作的请求数据。
/// </summary>
public sealed record FlowDebugCommandRequestDto(long CommandSequence);
/// <summary>
/// Defines the API data contract for flow run overview.
/// 定义流程运行Overview的 API 数据契约。
/// </summary>
public sealed record FlowRunOverviewDto(int QueueCapacity, int QueuedCount, int ActiveRunCount, int ActiveListenerRunCount, int MaxConcurrentRuns, int MaxConcurrentListenerRuns, int MaxConcurrentRunsPerProject, IReadOnlyList<FlowRunDto> QueuedRuns, IReadOnlyList<FlowRunDto> ActiveRuns, IReadOnlyList<FlowRunDto> RecentRuns);
/// <summary>
/// Defines the API data contract for run execution settings.
/// 定义运行执行设置的 API 数据契约。
/// </summary>
public sealed record RunExecutionSettingsDto(int QueueCapacity, int MaxConcurrentRuns, int MaxConcurrentListenerRuns, int MaxConcurrentRunsPerProject, int QueueWaitTimeoutSeconds = 60, int ShutdownGracePeriodSeconds = 10, int SynchronousInvocationTimeoutSeconds = 30, long MaxLibraryUploadBytes = 100 * 1024 * 1024);
/// <summary>
/// Defines the API data contract for flow interface.
/// 定义流程Interface的 API 数据契约。
/// </summary>
public sealed record FlowInterfaceDto(Guid Id, Guid ProjectId, Guid FlowId, string Name, FlowInvocationModeDto InvocationMode, bool IsEnabled, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long? ProductionVersion = null);
/// <summary>
/// Carries request data for create flow interface operations.
/// 承载创建流程Interface操作的请求数据。
/// </summary>
public sealed record CreateFlowInterfaceRequestDto(Guid ProjectId, Guid FlowId, string Name, FlowInvocationModeDto InvocationMode, bool IsEnabled = true);
/// <summary>
/// Carries request data for update flow interface operations.
/// 承载更新流程Interface操作的请求数据。
/// </summary>
public sealed record UpdateFlowInterfaceRequestDto(string Name, FlowInvocationModeDto InvocationMode, bool IsEnabled);
/// <summary>
/// Carries request data for public flow invocation operations.
/// 承载Public流程Invocation操作的请求数据。
/// </summary>
public sealed record PublicFlowInvocationRequestDto(IReadOnlyDictionary<string, JsonElement>? ProjectInputs = null, int? TimeoutSeconds = null, int? MaxSteps = null, int? MaxNodeVisits = null);
/// <summary>
/// Defines the API data contract for flow node data.
/// 定义流程节点Data的 API 数据契约。
/// </summary>
public sealed record FlowNodeDataDto(string NodeId, JsonElement Outputs);
/// <summary>
/// Defines the API data contract for public flow invocation response.
/// 定义Public流程Invocation响应的 API 数据契约。
/// </summary>
public sealed record PublicFlowInvocationResponseDto(Guid TaskId, FlowRunStatusDto Status, bool IsCompleted, IReadOnlyList<FlowNodeDataDto> FlowData);
/// <summary>
/// Describes a flow run event.
/// 描述流程运行事件。
/// </summary>
public sealed record FlowRunEventDto(Guid RunId, long Sequence, DateTimeOffset Timestamp, string Type, string? NodeId, string PayloadJson);
/// <summary>
/// Defines the API data contract for flow run output.
/// 定义流程运行输出的 API 数据契约。
/// </summary>
public sealed record FlowRunOutputDto(Guid RunId, long Sequence, DateTimeOffset Timestamp, string NodeId, string Outcome, string? Branch, JsonElement Inputs, JsonElement Outputs, string? ErrorCode, string? ErrorMessage);
/// <summary>
/// Defines the API data contract for worker event envelope.
/// 定义Worker事件Envelope的 API 数据契约。
/// </summary>
public sealed record WorkerEventEnvelopeDto(int ProtocolVersion, Guid RunId, long Sequence, DateTimeOffset Timestamp, WorkerEventType EventType, string? NodeId, string PayloadJson);
/// <summary>
/// Carries request data for worker run operations.
/// 承载Worker运行操作的请求数据。
/// </summary>
public sealed record WorkerRunRequestDto(int ProtocolVersion, Guid RunId, Guid FlowId, long FlowVersion, string DefinitionJson, DateTimeOffset Deadline, string? ProjectId = null, IReadOnlyDictionary<string, JsonElement>? ProjectInputs = null, int MaxSteps = 10_000, string? ScriptArtifactRootPath = null, string? LibraryPackageRootPath = null, int MaxNodeVisits = 1_000, IReadOnlyList<string>? AllowedLibraryIds = null, WorkerDebugOptionsDto? Debug = null, string? WorkpieceRootPath = null);
/// <summary>
/// Metadata for one run-local workpiece. The API exposes the download URL but never exposes the
/// server-side storage path.
/// 单个运行级工件的元数据。API 只公开下载地址，不公开服务端存储路径。
/// </summary>
public sealed record FlowWorkpieceDto(Guid RunId, string Id, string Kind, string Name, string ContentType, long Length, DateTimeOffset CreatedAt, string DownloadUrl, string? NodeId = null, Guid? ExecutionId = null);
/// <summary>
/// Immutable debug settings captured when a worker run starts. Breakpoints are
/// node IDs from the submitted flow snapshot, never CLR code locations.
/// Worker 运行启动时捕获的不可变调试设置。断点是已提交流程快照中的节点 ID，
/// 绝不是 CLR 代码位置。
/// </summary>
public sealed record WorkerDebugOptionsDto(Guid DebugSessionId, IReadOnlyList<string> BreakpointNodeIds, int MaxQueuedFlipflopTriggers = 64);
/// <summary>
/// Identifies the run-local message channel used by a Worker message endpoint.
/// 标识 Worker 消息端点使用的运行内消息通道。
/// </summary>
public enum WorkerMessageChannelKindDto
{
    /// <summary>
    /// Indicates the queue value.
    /// 表示“队列”这一取值。
    /// </summary>
    Queue = 0,
    /// <summary>
    /// Indicates the event bus value.
    /// 表示“EventBus”这一取值。
    /// </summary>
    EventBus = 1,
}

/// <summary>
/// Identifies the payload representation used at the Worker boundary.
/// 标识 Worker 边界使用的载荷表示方式。
/// </summary>
public enum WorkerMessageSerializationModeDto
{
    /// <summary>
    /// Indicates the direct object value.
    /// 表示“DirectObject”这一取值。
    /// </summary>
    DirectObject = 0,
    /// <summary>
    /// Indicates the json value.
    /// 表示“JSON”这一取值。
    /// </summary>
    Json = 1,
}

/// <summary>
/// An external message sent to an active Worker run.
/// 发送给活动 Worker 运行的外部消息。
/// </summary>
public sealed record WorkerMessageDeliveryDto(int ProtocolVersion, Guid RunId, Guid MessageId, string Topic, WorkerMessageChannelKindDto ChannelKind, WorkerMessageSerializationModeDto SerializationMode, string? ContractId, string PayloadJson, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt = null);
/// <summary>
/// Acknowledges that an external message entered the Worker-local broker.
/// 确认外部消息已进入 Worker 本地 Broker。
/// </summary>
public sealed record WorkerMessageAcceptedDto(int ProtocolVersion, Guid RunId, Guid MessageId, string Topic, bool Duplicate = false);
/// <summary>
/// Describes the result of an API-to-Worker message delivery request.
/// 描述 API 向 Worker 投递消息的结果。
/// </summary>
public enum WorkerMessageDeliveryStatusDto
{
    /// <summary>
    /// Indicates the accepted value.
    /// 表示“已接收”这一取值。
    /// </summary>
    Accepted = 0,
    /// <summary>
    /// Indicates the rejected value.
    /// 表示“已拒绝”这一取值。
    /// </summary>
    Rejected = 1,
    /// <summary>
    /// Indicates the not found value.
    /// 表示“NotFound”这一取值。
    /// </summary>
    NotFound = 2,
    /// <summary>
    /// Indicates the not ready value.
    /// 表示“Not就绪”这一取值。
    /// </summary>
    NotReady = 3,
    /// <summary>
    /// Indicates the timed out value.
    /// 表示“TimedOut”这一取值。
    /// </summary>
    TimedOut = 4,
}

/// <summary>
/// The bounded response returned by the Supervisor for message delivery.
/// Supervisor 返回的有界消息投递结果。
/// </summary>
public sealed record WorkerMessageDeliveryResponseDto(WorkerMessageDeliveryStatusDto Status, int ProtocolVersion, Guid RunId, Guid MessageId, string Topic, string? Code, string? Message, bool Duplicate = false);
/// <summary>
/// Reports why an external message was rejected by the Worker boundary.
/// 报告 Worker 边界拒绝外部消息的原因。
/// </summary>
public sealed record WorkerMessageRejectedDto(int ProtocolVersion, Guid RunId, Guid MessageId, string Topic, string Code, string Message);
/// <summary>
/// Announces a message endpoint explicitly exposed by a running Worker.
/// 宣布运行中 Worker 显式开放的消息端点。
/// </summary>
public sealed record WorkerMessageEndpointDto(int ProtocolVersion, Guid RunId, string Topic, WorkerMessageChannelKindDto ChannelKind, WorkerMessageSerializationModeDto SerializationMode, string? ContractId, bool ExternalIngress);
/// <summary>
/// JSON body accepted by the active-run message ingress endpoint.
/// 活动运行消息入口接受的 JSON 请求体。
/// </summary>
public sealed record RunMessageIngressRequestDto(JsonElement Payload, string? MessageId = null, string? ContractId = null, WorkerMessageChannelKindDto ChannelKind = WorkerMessageChannelKindDto.Queue);
/// <summary>
/// A strictly increasing control command scoped to one debug session.
/// 严格递增且仅作用于一个调试会话的控制命令。
/// </summary>
public sealed record WorkerDebugCommandDto(int ProtocolVersion, Guid RunId, Guid DebugSessionId, long CommandSequence);
/// <summary>
/// Safe snapshot emitted before the paused node enters its executor.
/// 节点进入执行器前发出的安全快照。
/// </summary>
public sealed record WorkerDebugPauseDto(Guid DebugSessionId, Guid RunId, string NodeId, string NodeType, int Step, object? Inputs, int FrameDepth, Guid? TriggerInvocationId = null, Guid? ExecutionId = null);
/// <summary>
/// Carries request data for worker cancel operations.
/// 承载WorkerCancel操作的请求数据。
/// </summary>
public sealed record WorkerCancelRequestDto(int ProtocolVersion, Guid RunId, string Reason);
/// <summary>
/// Captures the outcome of worker run operations.
/// 记录Worker运行操作的结果。
/// </summary>
public sealed record WorkerRunResultDto(int ProtocolVersion, Guid RunId, FlowRunStatusDto Status, string? ErrorCode, string? ErrorMessage, RunVariableSnapshotDto? FinalVariables = null);
/// <summary>
/// Defines the API data contract for problem details.
/// 定义ProblemDetails的 API 数据契约。
/// </summary>
public sealed record ProblemDetailsDto(string Type, string Title, int Status, string Detail, string? Instance, IReadOnlyDictionary<string, string[]>? Errors);

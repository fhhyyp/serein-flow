using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Mcp.Previews;
// Persisted payloads are shared by preview-producing tools and preview reads.
// They intentionally remain internal to avoid extending the public MCP model.
// 预览生成工具和预览读取共用持久化载荷；保持内部可见，避免扩大公开 MCP 模型。
internal sealed record StoredProjectCreatePreview(CreateProjectMcpRequestDto Request, Guid ProjectId, Guid FlowId, DateTimeOffset CreatedAt, FlowDefinitionDto Definition, FlowValidationResultDto Validation);
/// <summary>
/// Represents stored flow patch preview data used by SereinFlow.
/// 表示 SereinFlow 使用的Stored流程Patch预览数据。
/// </summary>
internal sealed record StoredFlowPatchPreview(FlowPatchRequestDto Request, FlowDefinitionDto CandidateDefinition, FlowValidationResultDto Validation, FlowDiffDto Diff, FlowPatchCanonicalRequestDto? CanonicalRequest = null, IReadOnlyList<FlowPatchNormalizationWarningDto>? NormalizationWarnings = null);
/// <summary>
/// Represents stored publish preview data used by SereinFlow.
/// 表示 SereinFlow 使用的Stored发布预览数据。
/// </summary>
internal sealed record StoredPublishPreview(PublishFlowPreviewRequestDto Request, FlowValidationResultDto Validation, FlowDiffDto Diff, bool HasProductionVersion = true, long? ExpectedProductionVersion = null);
/// <summary>
/// Represents stored rollback preview data used by SereinFlow.
/// 表示 SereinFlow 使用的Stored回滚预览数据。
/// </summary>
internal sealed record StoredRollbackPreview(RollbackFlowPreviewRequestDto Request, FlowValidationResultDto Validation, FlowDiffDto Diff);
/// <summary>
/// Represents stored library package preview data used by SereinFlow.
/// 表示 SereinFlow 使用的Stored库包预览数据。
/// </summary>
internal sealed record StoredLibraryPackagePreview(string FileName, Guid? ProjectId, string StagingPath, long SizeBytes, string PackageSha256, LibraryPackageInspectionDto Inspection, LibraryArtifactCompatibilityDto? Compatibility = null, LibraryPackageProjectImpactDto? ProjectImpact = null);
/// <summary>
/// Represents stored project library attach preview data used by SereinFlow.
/// 表示 SereinFlow 使用的Stored项目库关联预览数据。
/// </summary>
internal sealed record StoredProjectLibraryAttachPreview(ProjectLibraryAttachRequestDto Request, IReadOnlyList<ValidationDiagnosticDto> Diagnostics);
/// <summary>
/// Represents stored library family assignment preview data used by SereinFlow.
/// 表示 SereinFlow 使用的Stored库系列Assignment预览数据。
/// </summary>
internal sealed record StoredLibraryFamilyAssignmentPreview(LibraryFamilyAssignmentMcpRequestDto Request, IReadOnlyList<ValidationDiagnosticDto> Diagnostics, LibraryFamilyDto? CurrentFamily, LibraryFamilyDto? TargetFamily);
/// <summary>
/// Represents stored library upgrade preview data used by SereinFlow.
/// 表示 SereinFlow 使用的Stored库升级预览数据。
/// </summary>
internal sealed record StoredLibraryUpgradePreview(LibraryUpgradePreviewRequestDto Request, LibraryUpgradePlanDto Plan);

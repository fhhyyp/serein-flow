using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Mcp;

// Persisted payloads are shared by preview-producing tools and preview reads.
// They intentionally remain internal to avoid extending the public MCP model.
internal sealed record StoredProjectCreatePreview(
    CreateProjectMcpRequestDto Request,
    Guid ProjectId,
    Guid FlowId,
    DateTimeOffset CreatedAt,
    FlowDefinitionDto Definition,
    FlowValidationResultDto Validation);

internal sealed record StoredFlowPatchPreview(
    FlowPatchRequestDto Request,
    FlowDefinitionDto CandidateDefinition,
    FlowValidationResultDto Validation,
    FlowDiffDto Diff,
    FlowPatchCanonicalRequestDto? CanonicalRequest = null,
    IReadOnlyList<FlowPatchNormalizationWarningDto>? NormalizationWarnings = null);

internal sealed record StoredPublishPreview(
    PublishFlowPreviewRequestDto Request,
    FlowValidationResultDto Validation,
    FlowDiffDto Diff,
    bool HasProductionVersion = true,
    long? ExpectedProductionVersion = null);

internal sealed record StoredRollbackPreview(
    RollbackFlowPreviewRequestDto Request,
    FlowValidationResultDto Validation,
    FlowDiffDto Diff);

internal sealed record StoredLibraryPackagePreview(
    string FileName,
    Guid? ProjectId,
    string StagingPath,
    long SizeBytes,
    string PackageSha256,
    LibraryPackageInspectionDto Inspection,
    LibraryArtifactCompatibilityDto? Compatibility = null,
    LibraryPackageProjectImpactDto? ProjectImpact = null);

internal sealed record StoredProjectLibraryAttachPreview(
    ProjectLibraryAttachRequestDto Request,
    IReadOnlyList<ValidationDiagnosticDto> Diagnostics);

internal sealed record StoredLibraryFamilyAssignmentPreview(
    LibraryFamilyAssignmentMcpRequestDto Request,
    IReadOnlyList<ValidationDiagnosticDto> Diagnostics,
    LibraryFamilyDto? CurrentFamily,
    LibraryFamilyDto? TargetFamily);

internal sealed record StoredLibraryUpgradePreview(
    LibraryUpgradePreviewRequestDto Request,
    LibraryUpgradePlanDto Plan);

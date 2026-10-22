using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using static SereinFlow.Mcp.Support.McpMutationSupport;

namespace SereinFlow.Mcp.Previews;

internal static class McpPreviewProjection
{
    internal static FlowPatchPreviewDto ReadFlowPatchPreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredFlowPatchPreview>(entry);
        return ToFlowPatchPreview(entry, descriptor, stored);
    }

    internal static FlowPatchPreviewDto ToFlowPatchPreview(McpPreviewEntry preview, StoredFlowPatchPreview stored)
    {
        var isPreviewPending = IsPreviewPending(preview);
        return ToFlowPatchPreview(preview.Id, preview.ExpiresAt, preview.PreviewFingerprint, isPreviewPending, stored);
    }

    private static FlowPatchPreviewDto ToFlowPatchPreview(
        McpPreviewEntry entry,
        McpPreviewDescriptorDto descriptor,
        StoredFlowPatchPreview stored)
    {
        var isPreviewPending = IsPreviewPending(entry);
        return ToFlowPatchPreview(descriptor.PreviewId, descriptor.ExpiresAt, descriptor.PreviewFingerprint, isPreviewPending, stored);
    }

    private static FlowPatchPreviewDto ToFlowPatchPreview(
        Guid previewId,
        DateTimeOffset expiresAt,
        string previewFingerprint,
        bool isPending,
        StoredFlowPatchPreview stored)
    {
        var normalized = stored.CanonicalRequest is null
            ? new FlowPatchContractNormalizer().NormalizeLegacyRequest(stored.Request)
            : new NormalizedFlowPatchRequest(stored.CanonicalRequest, stored.Request, stored.NormalizationWarnings ?? []);
        var redactedDiff = FlowDiffService.RedactSensitive(stored.Diff);
        var redactedCandidateDefinition = FlowDiffService.RedactSensitive(stored.CandidateDefinition);
        return new FlowPatchPreviewDto(previewId, stored.Request.ProjectId, stored.Request.FlowId, stored.Request.ExpectedDevelopmentVersion, expiresAt, previewFingerprint, isPending && stored.Validation.IsValid && stored.Diff.Changes.Count > 0, stored.Validation, redactedDiff, redactedCandidateDefinition, SchemaVersion: FlowPatchContract.CurrentSchemaVersion, EnumEncoding: FlowPatchContract.EnumEncoding, NormalizedOperations: normalized.Request.Operations, NormalizationWarnings: normalized.Warnings);
    }

    internal static object ReadPublishPreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredPublishPreview>(entry);
        var canApply = IsPreviewPending(entry) && stored.Validation.IsValid && (!stored.HasProductionVersion || stored.Diff.Changes.Count > 0);
        var redactedDiff = FlowDiffService.RedactSensitive(stored.Diff);
        return new
        {
            previewId = descriptor.PreviewId,
            descriptor.ExpiresAt,
            descriptor.PreviewFingerprint,
            expectedProductionVersion = stored.ExpectedProductionVersion,
            canApply,
            validation = stored.Validation,
            diff = redactedDiff
        };
    }

    internal static object ReadRollbackPreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredRollbackPreview>(entry);
        var canApply = IsPreviewPending(entry) && stored.Validation.IsValid && stored.Diff.Changes.Count > 0;
        var redactedDiff = FlowDiffService.RedactSensitive(stored.Diff);
        return new
        {
            previewId = descriptor.PreviewId,
            descriptor.ExpiresAt,
            descriptor.PreviewFingerprint,
            canApply,
            validation = stored.Validation,
            diff = redactedDiff
        };
    }

    internal static LibraryPackagePreviewDto ReadLibraryPackagePreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredLibraryPackagePreview>(entry);
        var isPreviewPending = IsPreviewPending(entry);
        return new LibraryPackagePreviewDto(descriptor.PreviewId, stored.FileName, stored.SizeBytes, stored.PackageSha256, descriptor.ExpiresAt, descriptor.PreviewFingerprint, isPreviewPending, stored.Inspection.Library, [], stored.Inspection.AlreadyExists, stored.Inspection.Library.DllSha256, stored.Compatibility, stored.ProjectImpact);
    }

    internal static ProjectLibraryAttachPreviewDto ReadProjectLibraryAttachPreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredProjectLibraryAttachPreview>(entry);
        var canApply = IsPreviewPending(entry) && stored.Diagnostics.Count == 0;
        return new ProjectLibraryAttachPreviewDto(descriptor.PreviewId, stored.Request.ProjectId, stored.Request.LibraryId, descriptor.ExpiresAt, descriptor.PreviewFingerprint, canApply, stored.Diagnostics);
    }

    internal static LibraryFamilyAssignmentMcpPreviewDto ReadLibraryFamilyAssignmentPreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredLibraryFamilyAssignmentPreview>(entry);
        var canApply = IsPreviewPending(entry) && stored.Diagnostics.Count == 0;
        return new LibraryFamilyAssignmentMcpPreviewDto(descriptor.PreviewId, stored.Request, descriptor.ExpiresAt, descriptor.PreviewFingerprint, canApply, stored.Diagnostics, stored.CurrentFamily, stored.TargetFamily);
    }

    internal static McpLibraryUpgradePreviewDto ReadLibraryUpgradePreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredLibraryUpgradePreview>(entry);
        var canApply = IsPreviewPending(entry) && stored.Plan.Flows.Any(static flow => flow.CanApply);
        return new McpLibraryUpgradePreviewDto(descriptor.PreviewId, descriptor.ExpiresAt, descriptor.PreviewFingerprint, canApply, stored.Plan);
    }

    internal static ProjectCreatePreviewDto ReadProjectCreatePreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredProjectCreatePreview>(entry);
        var project = RehydrateProject(stored);
        var candidate = new ProjectCreationCandidate(project, stored.Definition, stored.Validation);
        return ToProjectCreatePreview(descriptor, candidate);
    }

    internal static Project RehydrateProject(StoredProjectCreatePreview stored)
    {
        return Project.Rehydrate(stored.ProjectId, stored.Request.Name, version: 1, SereinFlow.Domain.ProjectStatus.Draft, stored.CreatedAt, stored.CreatedAt);
    }

    internal static ProjectCreatePreviewDto ToProjectCreatePreview(McpPreviewDescriptorDto descriptor, ProjectCreationCandidate candidate)
    {
        var getFlowName = GetFlowName(candidate.Definition);
        var createWorkspace = CreateWorkspace(candidate);
        return new ProjectCreatePreviewDto(descriptor.PreviewId, candidate.Project.Id, candidate.Definition.Id, candidate.Project.Name, getFlowName, descriptor.ExpiresAt, descriptor.PreviewFingerprint, descriptor.Status == McpMutationPreviewStatusDto.Pending && descriptor.ExpiresAt > DateTimeOffset.UtcNow && candidate.Validation.IsValid, candidate.Validation, createWorkspace);
    }

    internal static ProjectWorkspaceDto CreateWorkspace(ProjectCreationCandidate candidate)
    {
        var toProjectStatusValue = ToProjectStatusValue(candidate.Project.Status);
        var projectDto = new ProjectDto(candidate.Project.Id, candidate.Project.Name, candidate.Project.Version, toProjectStatusValue, candidate.Project.CreatedAt, candidate.Project.UpdatedAt);
        var definition = candidate.Definition;
        var summary = new FlowDefinitionSummaryDto(
            definition.Id, definition.Version, definition.EntryNodeId, definition.Canvases.Count,
            definition.Canvases.Sum(static canvas => canvas.Nodes.Count));
        return new ProjectWorkspaceDto(projectDto, [summary]);
    }

    internal static string GetFlowName(FlowDefinitionDto definition)
    {
        return definition.Canvases.FirstOrDefault()?.Name ?? "Main";
    }

    private static string ToProjectStatusValue(ProjectStatus status)
    {
        return status switch
        {
            SereinFlow.Domain.ProjectStatus.Draft => "draft",
            SereinFlow.Domain.ProjectStatus.Ready => "ready",
            SereinFlow.Domain.ProjectStatus.ScriptInvalid => "scriptInvalid",
            SereinFlow.Domain.ProjectStatus.Archived => "archived",
            _ => status.ToString()
        };
    }
}

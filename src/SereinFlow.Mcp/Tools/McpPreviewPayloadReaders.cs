using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using static SereinFlow.Mcp.McpToolSupport;

namespace SereinFlow.Mcp;
internal static class McpPreviewPayloadReaders
{
    internal static object ReadPublishPreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredPublishPreview>(entry);
        var evaluatedValue = IsPreviewPending(entry) && stored.Validation.IsValid && (!stored.HasProductionVersion || stored.Diff.Changes.Count > 0);
        var redactSensitiveResult = FlowDiffService.RedactSensitive(stored.Diff);
        return new
        {
            previewId = descriptor.PreviewId,
            descriptor.ExpiresAt,
            descriptor.PreviewFingerprint,
            expectedProductionVersion = stored.ExpectedProductionVersion,
            canApply = evaluatedValue,
            validation = stored.Validation,
            diff = redactSensitiveResult
        };
    }

    internal static object ReadRollbackPreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredRollbackPreview>(entry);
        var evaluatedValue1 = IsPreviewPending(entry) && stored.Validation.IsValid && stored.Diff.Changes.Count > 0;
        var redactSensitiveResult2 = FlowDiffService.RedactSensitive(stored.Diff);
        return new
        {
            previewId = descriptor.PreviewId,
            descriptor.ExpiresAt,
            descriptor.PreviewFingerprint,
            canApply = evaluatedValue1,
            validation = stored.Validation,
            diff = redactSensitiveResult2
        };
    }

    internal static LibraryPackagePreviewDto ReadLibraryPackagePreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredLibraryPackagePreview>(entry);
        var isPreviewPendingResult = IsPreviewPending(entry);
        var libraryPackagePreviewDto = new LibraryPackagePreviewDto(descriptor.PreviewId, stored.FileName, stored.SizeBytes, stored.PackageSha256, descriptor.ExpiresAt, descriptor.PreviewFingerprint, isPreviewPendingResult, stored.Inspection.Library, [], stored.Inspection.AlreadyExists, stored.Inspection.Library.DllSha256, stored.Compatibility, stored.ProjectImpact);
        return libraryPackagePreviewDto;
    }

    internal static ProjectLibraryAttachPreviewDto ReadProjectLibraryAttachPreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredProjectLibraryAttachPreview>(entry);
        var evaluatedValue3 = IsPreviewPending(entry) && stored.Diagnostics.Count == 0;
        var projectLibraryAttachPreviewDto = new ProjectLibraryAttachPreviewDto(descriptor.PreviewId, stored.Request.ProjectId, stored.Request.LibraryId, descriptor.ExpiresAt, descriptor.PreviewFingerprint, evaluatedValue3, stored.Diagnostics);
        return projectLibraryAttachPreviewDto;
    }

    internal static LibraryFamilyAssignmentMcpPreviewDto ReadLibraryFamilyAssignmentPreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredLibraryFamilyAssignmentPreview>(entry);
        var evaluatedValue4 = IsPreviewPending(entry) && stored.Diagnostics.Count == 0;
        var libraryFamilyAssignmentMcpPreviewDto = new LibraryFamilyAssignmentMcpPreviewDto(descriptor.PreviewId, stored.Request, descriptor.ExpiresAt, descriptor.PreviewFingerprint, evaluatedValue4, stored.Diagnostics, stored.CurrentFamily, stored.TargetFamily);
        return libraryFamilyAssignmentMcpPreviewDto;
    }

    internal static McpLibraryUpgradePreviewDto ReadLibraryUpgradePreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredLibraryUpgradePreview>(entry);
        var evaluatedValue5 = IsPreviewPending(entry) && stored.Plan.Flows.Any(static flow => flow.CanApply);
        var mcpLibraryUpgradePreviewDto = new McpLibraryUpgradePreviewDto(descriptor.PreviewId, descriptor.ExpiresAt, descriptor.PreviewFingerprint, evaluatedValue5, stored.Plan);
        return mcpLibraryUpgradePreviewDto;
    }
}

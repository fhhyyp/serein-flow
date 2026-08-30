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
    private static object ReadPublishPreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredPublishPreview>(entry);
        return new
        {
            previewId = descriptor.PreviewId,
            descriptor.ExpiresAt,
            descriptor.PreviewFingerprint,
            expectedProductionVersion = stored.ExpectedProductionVersion,
            canApply = IsPreviewPending(entry)
                && stored.Validation.IsValid
                && (!stored.HasProductionVersion || stored.Diff.Changes.Count > 0),
            validation = stored.Validation,
            diff = FlowDiffService.RedactSensitive(stored.Diff),
        };
    }

    private static object ReadRollbackPreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredRollbackPreview>(entry);
        return new
        {
            previewId = descriptor.PreviewId,
            descriptor.ExpiresAt,
            descriptor.PreviewFingerprint,
            canApply = IsPreviewPending(entry)
                && stored.Validation.IsValid
                && stored.Diff.Changes.Count > 0,
            validation = stored.Validation,
            diff = FlowDiffService.RedactSensitive(stored.Diff),
        };
    }

    private static LibraryPackagePreviewDto ReadLibraryPackagePreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredLibraryPackagePreview>(entry);
        return new LibraryPackagePreviewDto(
            descriptor.PreviewId,
            stored.FileName,
            stored.SizeBytes,
            stored.PackageSha256,
            descriptor.ExpiresAt,
            descriptor.PreviewFingerprint,
            IsPreviewPending(entry),
            stored.Inspection.Library,
            [],
            stored.Inspection.AlreadyExists,
            stored.Inspection.Library.DllSha256,
            stored.Compatibility,
            stored.ProjectImpact);
    }

    private static ProjectLibraryAttachPreviewDto ReadProjectLibraryAttachPreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredProjectLibraryAttachPreview>(entry);
        return new ProjectLibraryAttachPreviewDto(
            descriptor.PreviewId,
            stored.Request.ProjectId,
            stored.Request.LibraryId,
            descriptor.ExpiresAt,
            descriptor.PreviewFingerprint,
            IsPreviewPending(entry) && stored.Diagnostics.Count == 0,
            stored.Diagnostics);
    }

}


namespace SereinFlow.Mcp;

[McpBindProperties]
internal class McpListLibrariesRequest : McpReadOptionsRequest
{
    public bool IncludeArchived { get; set; } = false;
}

[McpBindProperties]
internal class McpListLibraryFamiliesRequest : McpReadOptionsRequest
{
    public bool IncludeArchivedArtifacts { get; set; } = true;
}

[McpBindProperties]
internal class McpLibraryQueryRequest
{
    [McpParameter(Required = true, Trim = true)]
    public string LibraryId { get; set; } = string.Empty;
}

[McpBindProperties]
internal class McpLibraryFamilyQueryRequest
{
    [McpParameter(Required = true, Trim = true)]
    public string FamilyId { get; set; } = string.Empty;
}

[McpBindProperties]
internal class McpProjectLibrariesRequest : McpReadOptionsRequest
{
    [McpParameter(Required = true)]
    public Guid ProjectId { get; set; }
}

[McpBindProperties]
internal class McpLibraryUpgradeQueryRequest
{
    [McpParameter(Required = true)]
    public Guid ProjectId { get; set; }

    [McpParameter(Required = true)]
    public Guid UpgradeId { get; set; }
}

[McpBindProperties]
internal class McpLibraryPackagePreviewRequest
{
    [McpParameter(Required = true, Trim = true)]
    public string FileName { get; set; } = string.Empty;

    [McpParameter(Required = true, Trim = true)]
    public string PackageBase64 { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
    public string? FamilyId { get; set; }
    public string? BaselineArtifactId { get; set; }
}

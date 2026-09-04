using SereinFlow.Application;

namespace SereinFlow.Application.Tests;

public sealed class FileUploadLimitsTests
{
    [Fact]
    public void NormalizeClampsOperatorValueToSupportedRange()
    {
        Assert.Equal(FileUploadLimits.MinimumMaxFileSizeBytes, FileUploadLimits.Normalize(1));
        Assert.Equal(FileUploadLimits.MaximumMaxFileSizeBytes, FileUploadLimits.Normalize(long.MaxValue));
        Assert.Equal(128 * 1024 * 1024, FileUploadLimits.Normalize(128 * 1024 * 1024));
    }

    [Fact]
    public void McpRequestLimitIncludesBase64ExpansionAndOverhead()
    {
        var fileSize = 128 * 1024 * 1024;
        var expectedBase64Size = ((fileSize + 2) / 3) * 4;

        Assert.Equal(
            expectedBase64Size + FileUploadLimits.TransportOverheadBytes,
            FileUploadLimits.GetMcpRequestBodyLimit(fileSize, 16 * 1024 * 1024));
    }
}

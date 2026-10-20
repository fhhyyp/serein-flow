using SereinFlow.Application;

namespace SereinFlow.Application.Tests;
public sealed class FileUploadLimitsTests
{
    [Fact]
    public void NormalizeClampsOperatorValueToSupportedRange()
    {
        var normalizeResult = FileUploadLimits.Normalize(1);
        Assert.Equal(FileUploadLimits.MinimumMaxFileSizeBytes, normalizeResult);
        var normalizeResult1 = FileUploadLimits.Normalize(long.MaxValue);
        Assert.Equal(FileUploadLimits.MaximumMaxFileSizeBytes, normalizeResult1);
        var normalizeResult2 = FileUploadLimits.Normalize(128 * 1024 * 1024);
        Assert.Equal(128 * 1024 * 1024, normalizeResult2);
    }

    [Fact]
    public void McpRequestLimitIncludesBase64ExpansionAndOverhead()
    {
        var fileSize = 128 * 1024 * 1024;
        var expectedBase64Size = ((fileSize + 2) / 3) * 4;
        var getMcpRequestBodyLimitResult = FileUploadLimits.GetMcpRequestBodyLimit(fileSize, 16 * 1024 * 1024);
        Assert.Equal(expectedBase64Size + FileUploadLimits.TransportOverheadBytes, getMcpRequestBodyLimitResult);
    }
}

namespace SereinFlow.Application;

/// <summary>
/// Shared limits for library packages uploaded through the API or MCP.
/// The value represents decoded package bytes; transport overhead is handled
/// separately for multipart and base64 encoded requests.
/// </summary>
public static class FileUploadLimits
{
    public const long DefaultMaxFileSizeBytes = 100 * 1024 * 1024;
    public const long MinimumMaxFileSizeBytes = 1 * 1024 * 1024;
    public const long MaximumMaxFileSizeBytes = 512 * 1024 * 1024;
    public const long TransportOverheadBytes = 1 * 1024 * 1024;

    public static long Normalize(long value)
        => Math.Clamp(value, MinimumMaxFileSizeBytes, MaximumMaxFileSizeBytes);

    public static long GetApiRequestBodyLimit(long maxFileSizeBytes)
        => checked(Normalize(maxFileSizeBytes) + TransportOverheadBytes);

    public static long GetMcpRequestBodyLimit(long maxFileSizeBytes, long protocolRequestLimitBytes)
    {
        var fileSize = Normalize(maxFileSizeBytes);
        var base64Bytes = checked(((fileSize + 2) / 3) * 4);
        return Math.Max(protocolRequestLimitBytes, checked(base64Bytes + TransportOverheadBytes));
    }

    public static long GetBase64EncodedLimit(long maxFileSizeBytes)
        => checked(((Normalize(maxFileSizeBytes) + 2) / 3) * 4);
}

/// <summary>
/// Process-local live value shared by API, MCP and library package validation.
/// </summary>
public interface IFileUploadSettings
{
    long MaxLibraryUploadBytes { get; }

    long Configure(long maxLibraryUploadBytes);
}

public sealed class FileUploadSettings : IFileUploadSettings
{
    private long _maxLibraryUploadBytes = FileUploadLimits.DefaultMaxFileSizeBytes;

    public long MaxLibraryUploadBytes
        => Interlocked.Read(ref _maxLibraryUploadBytes);

    public long Configure(long maxLibraryUploadBytes)
    {
        var normalized = FileUploadLimits.Normalize(maxLibraryUploadBytes);
        Interlocked.Exchange(ref _maxLibraryUploadBytes, normalized);
        return normalized;
    }
}

namespace SereinFlow.Application;
/// <summary>
/// Shared limits for library packages uploaded through the API or MCP.
/// The value represents decoded package bytes; transport overhead is handled
/// separately for multipart and base64 encoded requests.
/// API 和 MCP 共用的类库包上传限制，以解码后的包字节数计；multipart 和 base64 传输开销另行处理。
/// </summary>
public static class FileUploadLimits
{
    private const long BytesPerMebibyte = 1024 * 1024;
    private const long Base64DecodedGroupBytes = 3;
    private const long Base64EncodedGroupCharacters = 4;
    private const long Base64GroupRoundingBytes = Base64DecodedGroupBytes - 1;
    public const long DefaultMaxFileSizeBytes = 100 * BytesPerMebibyte;
    public const long MinimumMaxFileSizeBytes = BytesPerMebibyte;
    public const long MaximumMaxFileSizeBytes = 512 * BytesPerMebibyte;
    public const long TransportOverheadBytes = BytesPerMebibyte;
    public static long Normalize(long value)
    {
        var clampResult = Math.Clamp(value, MinimumMaxFileSizeBytes, MaximumMaxFileSizeBytes);
        return clampResult;
    }

    public static long GetApiRequestBodyLimit(long maxFileSizeBytes)
    {
        var normalizedFileSize = Normalize(maxFileSizeBytes);
        var requestBodyLimit = checked(normalizedFileSize + TransportOverheadBytes);
        return requestBodyLimit;
    }

    public static long GetMcpRequestBodyLimit(long maxFileSizeBytes, long protocolRequestLimitBytes)
    {
        var fileSize = Normalize(maxFileSizeBytes);
        var base64Bytes = checked(((fileSize + Base64GroupRoundingBytes) / Base64DecodedGroupBytes) * Base64EncodedGroupCharacters);
        var maxResult = Math.Max(protocolRequestLimitBytes, checked(base64Bytes + TransportOverheadBytes));
        return maxResult;
    }

    public static long GetBase64EncodedLimit(long maxFileSizeBytes)
    {
        var normalizedFileSize = Normalize(maxFileSizeBytes);
        var encodedLimit = checked(((normalizedFileSize + Base64GroupRoundingBytes) / Base64DecodedGroupBytes) * Base64EncodedGroupCharacters);
        return encodedLimit;
    }
}

/// <summary>
/// Process-local live value shared by API, MCP and library package validation.
/// API、MCP 与类库包校验共用的进程内实时配置值。
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
    {
        get
        {
            var readResult = Interlocked.Read(ref _maxLibraryUploadBytes);
            return readResult;
        }
    }

    public long Configure(long maxLibraryUploadBytes)
    {
        var normalized = FileUploadLimits.Normalize(maxLibraryUploadBytes);
        Interlocked.Exchange(ref _maxLibraryUploadBytes, normalized);
        return normalized;
    }
}

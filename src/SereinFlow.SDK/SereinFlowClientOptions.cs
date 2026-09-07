using System.Text.Json;
using SereinFlow.Contracts;

namespace SereinFlow.Client;

public sealed class SereinFlowClientOptions
{
    public Uri? BaseAddress { get; set; }

    public string? ApiKey { get; set; }

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(100);

    public int MaxRetries { get; set; } = 3;

    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(250);

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan DefaultWaitTimeout { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan DefaultPollInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    public JsonSerializerOptions? JsonSerializerOptions { get; set; }

    internal SereinFlowClientOptions Validate(Uri? fallbackBaseAddress = null)
    {
        var baseAddress = BaseAddress ?? fallbackBaseAddress;
        if (baseAddress is null
            || !baseAddress.IsAbsoluteUri
            || (baseAddress.Scheme != Uri.UriSchemeHttp && baseAddress.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("BaseAddress must be an absolute HTTP or HTTPS URI.");
        }

        if (RequestTimeout <= TimeSpan.Zero)
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(RequestTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegative(MaxRetries);
        if (RetryBaseDelay < TimeSpan.Zero)
            ArgumentOutOfRangeException.ThrowIfNegative(RetryBaseDelay.Ticks);
        if (MaxRetryDelay < TimeSpan.Zero)
            ArgumentOutOfRangeException.ThrowIfNegative(MaxRetryDelay.Ticks);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(DefaultWaitTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(DefaultPollInterval, TimeSpan.Zero);

        BaseAddress = EnsureTrailingSlash(baseAddress);
        JsonSerializerOptions ??= SereinJsonSerialization.CreateContractOptions();
        return this;
    }

    private static Uri EnsureTrailingSlash(Uri value)
    {
        var text = value.ToString();
        return text.EndsWith('/')
            ? value
            : new Uri(text + "/", UriKind.Absolute);
    }
}

using System;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SereinFlow.Contracts;

namespace SereinFlow.Client;

internal sealed class SereinFlowHttpClient
{
    private const int MaximumRetryBackoffExponent = 10;
    private static readonly HashSet<HttpStatusCode> TransientStatusCodes = [HttpStatusCode.RequestTimeout, HttpStatusCode.TooManyRequests, HttpStatusCode.BadGateway, HttpStatusCode.ServiceUnavailable, HttpStatusCode.GatewayTimeout,];
    private readonly HttpClient _httpClient;
    private readonly SereinFlowClientOptions _options;
    public SereinFlowHttpClient(HttpClient httpClient, SereinFlowClientOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        var sereinFlowClientOptions = (options ?? throw new ArgumentNullException(nameof(options)));
        _options = sereinFlowClientOptions.Validate(httpClient.BaseAddress);
        _httpClient.BaseAddress = _options.BaseAddress;
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }

    public JsonSerializerOptions JsonOptions => _options.JsonSerializerOptions!;
    public TimeSpan DefaultWaitTimeout => _options.DefaultWaitTimeout;
    public TimeSpan DefaultPollInterval => _options.DefaultPollInterval;

    public async Task<T> SendJsonAsync<T>(HttpMethod method, string relativePath, object? body, bool allowRetry, IReadOnlyDictionary<string, string?>? headers, CancellationToken cancellationToken)
    {
        var payload = body is null ? null : JsonSerializer.Serialize(body, JsonOptions);
        using var response = await SendAsync(method, relativePath, payload, allowRetry, headers, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(content, JsonOptions) ?? throw new JsonException($"The SereinFlow response at '{response.RequestMessage?.RequestUri}' was empty. SereinFlow 响应为空，地址见上述信息。");
    }

    public async Task SendNoContentAsync(HttpMethod method, string relativePath, object? body, bool allowRetry, IReadOnlyDictionary<string, string?>? headers, CancellationToken cancellationToken)
    {
        var payload = body is null ? null : JsonSerializer.Serialize(body, JsonOptions);
        using var response = await SendAsync(method, relativePath, payload, allowRetry, headers, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string relativePath, bool allowRetry, IReadOnlyDictionary<string, string?>? headers, HttpCompletionOption completionOption, CancellationToken cancellationToken)
    {
        var response = await SendAsync(method, relativePath, payload: null, allowRetry, headers, completionOption, cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    public async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw CreateException(response, body);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativePath, string? payload, bool allowRetry, IReadOnlyDictionary<string, string?>? headers, HttpCompletionOption completionOption, CancellationToken cancellationToken)
    {
        var attempts = allowRetry ? _options.MaxRetries + 1 : 1;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            using var request = CreateRequest(method, relativePath, payload, headers);
            if (_options.BearerTokenProvider is { } tokenProvider)
            {
                var token = await tokenProvider(cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }

            using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            requestTimeout.CancelAfter(_options.RequestTimeout);
            HttpResponseMessage? response = null;
            try
            {
                response = await _httpClient.SendAsync(request, completionOption, requestTimeout.Token).ConfigureAwait(false);
            }
            catch (HttpRequestException) when (allowRetry && attempt + 1 < attempts)
            {
                await DelayBeforeRetryAsync(null, attempt, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (response is not null && allowRetry && TransientStatusCodes.Contains(response.StatusCode) && attempt + 1 < attempts)
            {
                var retryAfter = response.Headers.RetryAfter;
                response.Dispose();
                await DelayBeforeRetryAsync(retryAfter, attempt, cancellationToken).ConfigureAwait(false);
                continue;
            }

            return response!;
        }

        throw new InvalidOperationException("The SereinFlow HTTP retry loop ended without a response. SereinFlow HTTP 重试循环结束时仍未获得响应。");
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath, string? payload, IReadOnlyDictionary<string, string?>? headers)
    {
        var request = new HttpRequestMessage(method, relativePath);
        var hasAcceptHeader = headers?.Keys.Any(static key =>
        {
            return string.Equals(key, "Accept", StringComparison.OrdinalIgnoreCase);
        }) == true;
        if (!hasAcceptHeader)
        {
            var mediaTypeWithQualityHeaderValue = new MediaTypeWithQualityHeaderValue("application/json");
            request.Headers.Accept.Add(mediaTypeWithQualityHeaderValue);
        }

        if (headers is not null)
        {
            foreach (var header in headers)
            {
                if (header.Value is not null)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }

        if (payload is not null)
        {
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private async Task DelayBeforeRetryAsync(RetryConditionHeaderValue? retryAfter, int attempt, CancellationToken cancellationToken)
    {
        var delay = retryAfter?.Delta ?? (retryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : TimeSpan.FromTicks((_options.RetryBaseDelay.Ticks * (1L << Math.Min(attempt, MaximumRetryBackoffExponent)))));
        delay = delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
        if (delay > _options.MaxRetryDelay)
        {
            delay = _options.MaxRetryDelay;
        }
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
    }

    private SereinFlowClientException CreateException(HttpResponseMessage response, string body)
    {
        var error = ParseError(response.StatusCode, body);
        return new SereinFlowClientException((int)response.StatusCode, response.RequestMessage?.RequestUri ?? _httpClient.BaseAddress!, error, body);
    }

    private static SereinFlowApiError ParseError(HttpStatusCode statusCode, string body)
    {
        var extensions = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        string? code = null;
        string? title = null;
        string? detail = null;
        string? instance = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                title = ReadString(root, "title");
                detail = ReadString(root, "detail") ?? ReadString(root, "message");
                instance = ReadString(root, "instance");
                code = ReadString(root, "code");
                if (root.TryGetProperty("extensions", out var extensionObject) && extensionObject.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in extensionObject.EnumerateObject())
                    {
                        extensions[property.Name] = property.Value.Clone();
                    }
                }

                foreach (var property in root.EnumerateObject())
                {
                    if (property.Name is not ("type" or "title" or "status" or "detail" or "instance" or "code" or "extensions"))
                    {
                        extensions[property.Name] = property.Value.Clone();
                    }
                }
            }
        }
        catch (JsonException)
        {
            detail = string.IsNullOrWhiteSpace(body) ? null : body;
        }

        if (code is null && extensions.TryGetValue("code", out var codeValue) && codeValue.ValueKind == JsonValueKind.String)
        {
            code = codeValue.GetString();
        }
        return new SereinFlowApiError((int)statusCode, code, title, detail, instance, extensions);
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}

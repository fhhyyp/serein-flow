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
    private static readonly HashSet<HttpStatusCode> TransientStatusCodes = [HttpStatusCode.RequestTimeout, HttpStatusCode.TooManyRequests, HttpStatusCode.BadGateway, HttpStatusCode.ServiceUnavailable, HttpStatusCode.GatewayTimeout, ];
    private readonly HttpClient _httpClient;
    private readonly SereinFlowClientOptions _options;
    public SereinFlowHttpClient(HttpClient httpClient, SereinFlowClientOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        var evaluatedValue = (options ?? throw new ArgumentNullException(nameof(options)));
        _options = evaluatedValue.Validate(httpClient.BaseAddress);
        _httpClient.BaseAddress = _options.BaseAddress;
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }

    public JsonSerializerOptions JsonOptions => _options.JsonSerializerOptions!;
    public TimeSpan DefaultWaitTimeout => _options.DefaultWaitTimeout;
    public TimeSpan DefaultPollInterval => _options.DefaultPollInterval;

    public async Task<T> SendJsonAsync<T>(HttpMethod method, string relativePath, object? body, bool allowRetry, IReadOnlyDictionary<string, string?>? headers, CancellationToken cancellationToken)
    {
        var payload = body is null ? null : JsonSerializer.Serialize(body, JsonOptions);
        var sendAsyncResult = SendAsync(method, relativePath, payload, allowRetry, headers, HttpCompletionOption.ResponseContentRead, cancellationToken);
        using var response = await sendAsyncResult.ConfigureAwait(false);
        var ensureSuccessAsyncResult = EnsureSuccessAsync(response, cancellationToken);
        await ensureSuccessAsyncResult.ConfigureAwait(false);
        var readAsStringAsyncResult = response.Content.ReadAsStringAsync(cancellationToken);
        var content = await readAsStringAsyncResult.ConfigureAwait(false);
        var evaluatedValue1 = JsonSerializer.Deserialize<T>(content, JsonOptions) ?? throw new JsonException($"The SereinFlow response at '{response.RequestMessage?.RequestUri}' was empty. SereinFlow 响应为空，地址见上述信息。");
        return evaluatedValue1;
    }

    public async Task SendNoContentAsync(HttpMethod method, string relativePath, object? body, bool allowRetry, IReadOnlyDictionary<string, string?>? headers, CancellationToken cancellationToken)
    {
        var payload = body is null ? null : JsonSerializer.Serialize(body, JsonOptions);
        var sendAsyncResult2 = SendAsync(method, relativePath, payload, allowRetry, headers, HttpCompletionOption.ResponseContentRead, cancellationToken);
        using var response = await sendAsyncResult2.ConfigureAwait(false);
        var ensureSuccessAsyncResult3 = EnsureSuccessAsync(response, cancellationToken);
        await ensureSuccessAsyncResult3.ConfigureAwait(false);
    }

    public async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string relativePath, bool allowRetry, IReadOnlyDictionary<string, string?>? headers, HttpCompletionOption completionOption, CancellationToken cancellationToken)
    {
        var sendAsyncResult5 = SendAsync(method, relativePath, payload: null, allowRetry, headers, completionOption, cancellationToken);
        var response = await sendAsyncResult5.ConfigureAwait(false);
        try
        {
            var ensureSuccessAsyncResult4 = EnsureSuccessAsync(response, cancellationToken);
            await ensureSuccessAsyncResult4.ConfigureAwait(false);
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

        var readAsStringAsyncResult6 = response.Content.ReadAsStringAsync(cancellationToken);
        var body = await readAsStringAsyncResult6.ConfigureAwait(false);
        throw CreateException(response, body);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativePath, string? payload, bool allowRetry, IReadOnlyDictionary<string, string?>? headers, HttpCompletionOption completionOption, CancellationToken cancellationToken)
    {
        var attempts = allowRetry ? _options.MaxRetries + 1 : 1;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            using var request = CreateRequest(method, relativePath, payload, headers);
            using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            requestTimeout.CancelAfter(_options.RequestTimeout);
            HttpResponseMessage? response = null;
            try
            {
                var sendAsyncResult7 = _httpClient.SendAsync(request, completionOption, requestTimeout.Token);
                response = await sendAsyncResult7.ConfigureAwait(false);
            }
            catch (HttpRequestException)when (allowRetry && attempt + 1 < attempts)
            {
                var delayBeforeRetryAsyncResult = DelayBeforeRetryAsync(null, attempt, cancellationToken);
                await delayBeforeRetryAsyncResult.ConfigureAwait(false);
                continue;
            }

            if (response is not null && allowRetry && TransientStatusCodes.Contains(response.StatusCode) && attempt + 1 < attempts)
            {
                var retryAfter = response.Headers.RetryAfter;
                response.Dispose();
                var delayBeforeRetryAsyncResult8 = DelayBeforeRetryAsync(retryAfter, attempt, cancellationToken);
                await delayBeforeRetryAsyncResult8.ConfigureAwait(false);
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
            var equalsResult = string.Equals(key, "Accept", StringComparison.OrdinalIgnoreCase);
            return equalsResult;
        }) == true;
        if (!hasAcceptHeader)
        {
            var mediaTypeWithQualityHeaderValue = new MediaTypeWithQualityHeaderValue("application/json");
            request.Headers.Accept.Add(mediaTypeWithQualityHeaderValue);
        }

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
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
        var delay = retryAfter?.Delta ?? (retryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : resolveFromTicksResult());
        delay = delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
        if (delay > _options.MaxRetryDelay)
        {
            delay = _options.MaxRetryDelay;
        }

        var delayResult = Task.Delay(delay, cancellationToken);
        await delayResult.ConfigureAwait(false);
        TimeSpan resolveFromTicksResult()
        {
            var evaluatedValue9 = _options.RetryBaseDelay.Ticks * (1L << Math.Min(attempt, MaximumRetryBackoffExponent));
            var fromTicksResult = TimeSpan.FromTicks(evaluatedValue9);
            return fromTicksResult;
        }
    }

    private SereinFlowClientException CreateException(HttpResponseMessage response, string body)
    {
        var error = ParseError(response.StatusCode, body);
        var sereinFlowClientException = new SereinFlowClientException((int)response.StatusCode, response.RequestMessage?.RequestUri ?? _httpClient.BaseAddress!, error, body);
        return sereinFlowClientException;
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

        var sereinFlowApiError = new SereinFlowApiError((int)statusCode, code, title, detail, instance, extensions);
        return sereinFlowApiError;
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        string? evaluatedValue10 = root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return evaluatedValue10;
    }
}

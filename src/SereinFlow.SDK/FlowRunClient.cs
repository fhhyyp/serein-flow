using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using SereinFlow.Contracts;

namespace SereinFlow.Client;

public sealed class FlowRunClient : IFlowRunClient
{
    private readonly SereinFlowHttpClient _transport;

    internal FlowRunClient(SereinFlowHttpClient transport)
        => _transport = transport;

    public Task<FlowRunDto> GetAsync(Guid runId, CancellationToken cancellationToken = default)
        => _transport.SendJsonAsync<FlowRunDto>(
            HttpMethod.Get,
            $"api/runs/{runId:D}",
            body: null,
            allowRetry: true,
            headers: null,
            cancellationToken);

    public Task<FlowDefinitionDto> GetSnapshotAsync(Guid runId, CancellationToken cancellationToken = default)
        => _transport.SendJsonAsync<FlowDefinitionDto>(
            HttpMethod.Get,
            $"api/runs/{runId:D}/snapshot",
            body: null,
            allowRetry: true,
            headers: null,
            cancellationToken);

    public async Task<IReadOnlyList<FlowRunOutputDto>> GetOutputsAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
        => await _transport.SendJsonAsync<FlowRunOutputDto[]>(
                HttpMethod.Get,
                $"api/runs/{runId:D}/outputs",
                body: null,
                allowRetry: true,
                headers: null,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task<FlowRunOutputDto?> GetLatestNodeOutputAsync(
        Guid runId,
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        var outputs = await GetOutputsAsync(runId, cancellationToken).ConfigureAwait(false);
        return outputs
            .Where(item => string.Equals(item.NodeId, nodeId, StringComparison.Ordinal))
            .OrderByDescending(static item => item.Sequence)
            .FirstOrDefault();
    }

    public async Task<T?> GetLatestNodeOutputAsync<T>(
        Guid runId,
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        var output = await GetLatestNodeOutputAsync(runId, nodeId, cancellationToken).ConfigureAwait(false);
        if (output is null || output.Outputs.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return default;
        return output.Outputs.Deserialize<T>(_transport.JsonOptions);
    }

    public async Task<IReadOnlyList<FlowRunEventDto>> GetEventsAsync(
        Guid runId,
        long afterSequence = 0,
        CancellationToken cancellationToken = default)
    {
        ValidateSequence(afterSequence);
        return await _transport.SendJsonAsync<FlowRunEventDto[]>(
                HttpMethod.Get,
                $"api/runs/{runId:D}/events?afterSequence={afterSequence.ToString(CultureInfo.InvariantCulture)}",
                body: null,
                allowRetry: true,
                headers: null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async IAsyncEnumerable<FlowRunEventDto> StreamEventsAsync(
        Guid runId,
        long afterSequence = 0,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ValidateSequence(afterSequence);
        var headers = new Dictionary<string, string?>
        {
            ["Accept"] = "text/event-stream",
            ["Last-Event-ID"] = afterSequence.ToString(CultureInfo.InvariantCulture),
        };
        using var response = await _transport.SendRawAsync(
                HttpMethod.Get,
                $"api/runs/{runId:D}/events/stream",
                allowRetry: false,
                headers,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        await foreach (var item in SseEventParser.ReadAsync(stream, _transport.JsonOptions, cancellationToken))
            yield return item;
    }

    public async Task<FlowRunResult> WaitForCompletionAsync(
        Guid runId,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        var maximumWait = timeout ?? _transport.DefaultWaitTimeout;
        var interval = pollInterval ?? _transport.DefaultPollInterval;
        ValidateWaitValues(maximumWait, interval);

        var deadline = DateTimeOffset.UtcNow + maximumWait;
        var latest = await GetAsync(runId, cancellationToken).ConfigureAwait(false);
        while (!IsTerminal(latest.Status) && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            latest = await GetAsync(runId, cancellationToken).ConfigureAwait(false);
        }

        if (!IsTerminal(latest.Status))
        {
            throw new SereinFlowClientTimeoutException(runId, maximumWait, latest);
        }

        return new FlowRunResult(latest, await GetOutputsAsync(runId, cancellationToken).ConfigureAwait(false));
    }

    public Task CancelAsync(Guid runId, CancellationToken cancellationToken = default)
        => _transport.SendNoContentAsync(
            HttpMethod.Post,
            $"api/runs/{runId:D}/cancel",
            body: null,
            allowRetry: false,
            headers: null,
            cancellationToken);

    internal static bool IsTerminal(FlowRunStatusDto status)
        => status is FlowRunStatusDto.Succeeded
            or FlowRunStatusDto.Failed
            or FlowRunStatusDto.Cancelled
            or FlowRunStatusDto.TimedOut
            or FlowRunStatusDto.Interrupted;

    private static void ValidateSequence(long sequence)
    {
        if (sequence < 0)
            ArgumentOutOfRangeException.ThrowIfNegative(sequence);
    }

    private static void ValidateWaitValues(TimeSpan timeout, TimeSpan pollInterval)
    {
        if (timeout <= TimeSpan.Zero)
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);
    }
}

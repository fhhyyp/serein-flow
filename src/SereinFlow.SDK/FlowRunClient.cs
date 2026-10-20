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
    {
        _transport = transport;
    }

    public Task<FlowRunDto> GetAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var sendJsonAsyncResult = _transport.SendJsonAsync<FlowRunDto>(HttpMethod.Get, $"api/runs/{runId:D}", body: null, allowRetry: true, headers: null, cancellationToken);
        return sendJsonAsyncResult;
    }

    public Task<FlowDefinitionDto> GetSnapshotAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var sendJsonAsyncResult1 = _transport.SendJsonAsync<FlowDefinitionDto>(HttpMethod.Get, $"api/runs/{runId:D}/snapshot", body: null, allowRetry: true, headers: null, cancellationToken);
        return sendJsonAsyncResult1;
    }

    public async Task<IReadOnlyList<FlowRunOutputDto>> GetOutputsAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var sendJsonAsyncResult2 = _transport.SendJsonAsync<FlowRunOutputDto[]>(HttpMethod.Get, $"api/runs/{runId:D}/outputs", body: null, allowRetry: true, headers: null, cancellationToken);
        var configureAwaitResult = await sendJsonAsyncResult2.ConfigureAwait(false);
        return configureAwaitResult;
    }

    public async Task<FlowRunOutputDto?> GetLatestNodeOutputAsync(Guid runId, string nodeId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        var getOutputsAsyncResult = GetOutputsAsync(runId, cancellationToken);
        var outputs = await getOutputsAsyncResult.ConfigureAwait(false);
        var whereResult = outputs.Where(item =>
        {
            var equalsResult = string.Equals(item.NodeId, nodeId, StringComparison.Ordinal);
            return equalsResult;
        });
        var orderByDescendingResult = whereResult.OrderByDescending(static item => item.Sequence);
        var firstOrDefaultResult = orderByDescendingResult.FirstOrDefault();
        return firstOrDefaultResult;
    }

    public async Task<T?> GetLatestNodeOutputAsync<T>(Guid runId, string nodeId, CancellationToken cancellationToken = default)
    {
        var getLatestNodeOutputAsyncResult = GetLatestNodeOutputAsync(runId, nodeId, cancellationToken);
        var output = await getLatestNodeOutputAsyncResult.ConfigureAwait(false);
        if (output is null || output.Outputs.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return default;
        }

        var deserializeResult = output.Outputs.Deserialize<T>(_transport.JsonOptions);
        return deserializeResult;
    }

    public async Task<IReadOnlyList<FlowRunEventDto>> GetEventsAsync(Guid runId, long afterSequence = 0, CancellationToken cancellationToken = default)
    {
        ValidateSequence(afterSequence);
        var evaluatedValue = $"api/runs/{runId:D}/events?afterSequence={afterSequence.ToString(CultureInfo.InvariantCulture)}";
        var sendJsonAsyncResult3 = _transport.SendJsonAsync<FlowRunEventDto[]>(HttpMethod.Get, evaluatedValue, body: null, allowRetry: true, headers: null, cancellationToken);
        var configureAwaitResult4 = await sendJsonAsyncResult3.ConfigureAwait(false);
        return configureAwaitResult4;
    }

    public async IAsyncEnumerable<FlowRunEventDto> StreamEventsAsync(Guid runId, long afterSequence = 0, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ValidateSequence(afterSequence);
        var headers = new Dictionary<string, string?>
        {
            ["Accept"] = "text/event-stream",
            ["Last-Event-ID"] = afterSequence.ToString(CultureInfo.InvariantCulture),
        };
        var sendRawAsyncResult = _transport.SendRawAsync(HttpMethod.Get, $"api/runs/{runId:D}/events/stream", allowRetry: false, headers, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        using var response = await sendRawAsyncResult.ConfigureAwait(false);
        var readAsStreamAsyncResult = response.Content.ReadAsStreamAsync(cancellationToken);
        await using var stream = await readAsStreamAsyncResult.ConfigureAwait(false);
        await foreach (var item in SseEventParser.ReadAsync(stream, _transport.JsonOptions, cancellationToken))
        {
            yield return item;
        }
    }

    public async Task<FlowRunResult> WaitForCompletionAsync(Guid runId, TimeSpan? timeout = null, TimeSpan? pollInterval = null, CancellationToken cancellationToken = default)
    {
        var maximumWait = timeout ?? _transport.DefaultWaitTimeout;
        var interval = pollInterval ?? _transport.DefaultPollInterval;
        ValidateWaitValues(maximumWait, interval);
        var deadline = DateTimeOffset.UtcNow + maximumWait;
        var getAsyncResult5 = GetAsync(runId, cancellationToken);
        var latest = await getAsyncResult5.ConfigureAwait(false);
        while (!IsTerminal(latest.Status) && DateTimeOffset.UtcNow < deadline)
        {
            var delayResult = Task.Delay(interval, cancellationToken);
            await delayResult.ConfigureAwait(false);
            var getAsyncResult = GetAsync(runId, cancellationToken);
            latest = await getAsyncResult.ConfigureAwait(false);
        }

        if (!IsTerminal(latest.Status))
        {
            throw new SereinFlowClientTimeoutException(runId, maximumWait, latest);
        }

        var getOutputsAsyncResult6 = GetOutputsAsync(runId, cancellationToken);
        var configureAwaitResult7 = await getOutputsAsyncResult6.ConfigureAwait(false);
        var flowRunResult = new FlowRunResult(latest, configureAwaitResult7);
        return flowRunResult;
    }

    public Task CancelAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var sendNoContentAsyncResult = _transport.SendNoContentAsync(HttpMethod.Post, $"api/runs/{runId:D}/cancel", body: null, allowRetry: false, headers: null, cancellationToken);
        return sendNoContentAsyncResult;
    }

    internal static bool IsTerminal(FlowRunStatusDto status) => status is FlowRunStatusDto.Succeeded or FlowRunStatusDto.Failed or FlowRunStatusDto.Cancelled or FlowRunStatusDto.TimedOut or FlowRunStatusDto.Interrupted;
    private static void ValidateSequence(long sequence)
    {
        if (sequence < 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        }
    }

    private static void ValidateWaitValues(TimeSpan timeout, TimeSpan pollInterval)
    {
        if (timeout <= TimeSpan.Zero)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);
    }
}

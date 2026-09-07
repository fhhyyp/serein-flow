using System.Net.Http;
using SereinFlow.Contracts;

namespace SereinFlow.Client;

public sealed class FlowInvocationClient : IFlowInvocationClient
{
    private readonly SereinFlowHttpClient _transport;

    internal FlowInvocationClient(SereinFlowHttpClient transport)
        => _transport = transport;

    public Task<FlowRunDto> StartRunAsync(
        Guid projectId,
        Guid flowId,
        RunFlowRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _transport.SendJsonAsync<FlowRunDto>(
            HttpMethod.Post,
            $"api/projects/{projectId:D}/flows/{flowId:D}/runs",
            request,
            allowRetry: false,
            headers: null,
            cancellationToken);
    }

    public Task<PublicFlowInvocationResponseDto> InvokeAsync(
        Guid interfaceId,
        PublicFlowInvocationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _transport.SendJsonAsync<PublicFlowInvocationResponseDto>(
            HttpMethod.Post,
            $"api/public/flows/{interfaceId:D}/invoke",
            request,
            allowRetry: false,
            headers: null,
            cancellationToken);
    }

    public Task<PublicFlowInvocationResponseDto> GetTaskAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
        => _transport.SendJsonAsync<PublicFlowInvocationResponseDto>(
            HttpMethod.Get,
            $"api/public/tasks/{taskId:D}",
            body: null,
            allowRetry: true,
            headers: null,
            cancellationToken);

    public async Task<PublicFlowInvocationResponseDto> WaitForPublicTaskAsync(
        Guid taskId,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        var maximumWait = timeout ?? _transport.DefaultWaitTimeout;
        var interval = pollInterval ?? _transport.DefaultPollInterval;
        ValidateWaitValues(maximumWait, interval);

        var deadline = DateTimeOffset.UtcNow + maximumWait;
        var latest = await GetTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
        while (!latest.IsCompleted && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            latest = await GetTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
        }

        if (!latest.IsCompleted)
            throw new SereinFlowClientTimeoutException(taskId, maximumWait, latest);
        return latest;
    }

    private static void ValidateWaitValues(TimeSpan timeout, TimeSpan pollInterval)
    {
        if (timeout <= TimeSpan.Zero)
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);
    }
}

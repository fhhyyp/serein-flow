using System.Net.Http;
using SereinFlow.Contracts;

namespace SereinFlow.Client;
public sealed class FlowInvocationClient : IFlowInvocationClient
{
    private readonly SereinFlowHttpClient _transport;
    internal FlowInvocationClient(SereinFlowHttpClient transport)
    {
        _transport = transport;
    }

    public Task<FlowRunDto> StartRunAsync(Guid projectId, Guid flowId, RunFlowRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sendJsonAsyncResult = _transport.SendJsonAsync<FlowRunDto>(HttpMethod.Post, $"api/projects/{projectId:D}/flows/{flowId:D}/runs", request, allowRetry: false, headers: null, cancellationToken);
        return sendJsonAsyncResult;
    }

    public Task<PublicFlowInvocationResponseDto> InvokeAsync(Guid interfaceId, PublicFlowInvocationRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sendJsonAsyncResult1 = _transport.SendJsonAsync<PublicFlowInvocationResponseDto>(HttpMethod.Post, $"api/public/flows/{interfaceId:D}/invoke", request, allowRetry: false, headers: null, cancellationToken);
        return sendJsonAsyncResult1;
    }

    public Task<PublicFlowInvocationResponseDto> GetTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var sendJsonAsyncResult2 = _transport.SendJsonAsync<PublicFlowInvocationResponseDto>(HttpMethod.Get, $"api/public/tasks/{taskId:D}", body: null, allowRetry: true, headers: null, cancellationToken);
        return sendJsonAsyncResult2;
    }

    public async Task<PublicFlowInvocationResponseDto> WaitForPublicTaskAsync(Guid taskId, TimeSpan? timeout = null, TimeSpan? pollInterval = null, CancellationToken cancellationToken = default)
    {
        var maximumWait = timeout ?? _transport.DefaultWaitTimeout;
        var interval = pollInterval ?? _transport.DefaultPollInterval;
        ValidateWaitValues(maximumWait, interval);
        var deadline = DateTimeOffset.UtcNow + maximumWait;
        var getTaskAsyncResult3 = GetTaskAsync(taskId, cancellationToken);
        var latest = await getTaskAsyncResult3.ConfigureAwait(false);
        while (!latest.IsCompleted && DateTimeOffset.UtcNow < deadline)
        {
            var delayResult = Task.Delay(interval, cancellationToken);
            await delayResult.ConfigureAwait(false);
            var getTaskAsyncResult = GetTaskAsync(taskId, cancellationToken);
            latest = await getTaskAsyncResult.ConfigureAwait(false);
        }

        if (!latest.IsCompleted)
        {
            throw new SereinFlowClientTimeoutException(taskId, maximumWait, latest);
        }

        return latest;
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

using System.Collections.Concurrent;
using SereinFlow.Contracts;
using SereinFlow.Worker.Protocol;

namespace SereinFlow.Worker.Supervisor;

public sealed partial class WorkerSupervisor
{
    public Task<WorkerVariableResponseDto> AccessVariablesAsync(
        WorkerVariableRequestDto request, CancellationToken cancellationToken = default)
        => _messageSessions.TryGetValue(request.RunId, out var session)
            ? session.AccessVariablesAsync(request, cancellationToken)
            : Task.FromResult(new WorkerVariableResponseDto(WorkerProtocolConstants.Version, request.RunId, 409,
                Code: WorkerErrorCodes.NotActive, Message: "The Worker run is not active. Worker 运行当前不活动。"));
}

internal sealed partial class WorkerMessageSession
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<WorkerVariableResponseDto>> _variableRequests = new();

    internal async Task<WorkerVariableResponseDto> AccessVariablesAsync(
        WorkerVariableRequestDto request, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _completed) == 1) return VariablesInactive();
        if (request.RunId != _request.RunId || request.ProtocolVersion != WorkerProtocolConstants.Version)
            return new(WorkerProtocolConstants.Version, request.RunId, 400, Code: VariableErrorCodes.InvalidRequest);
        var requestId = Guid.NewGuid().ToString("N");
        var pending = new TaskCompletionSource<WorkerVariableResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        _variableRequests[requestId] = pending;
        try
        {
            // Completion may race registration; avoid waiting for a response from an exited Worker.
            if (Volatile.Read(ref _completed) == 1) return VariablesInactive();
            await _transport.SendAsync(WorkerMessage.Create(WorkerProtocolConstants.VariableRequestKind,
                WorkerProtocolCodec.SerializePayload(request), request.RunId, requestId: requestId), cancellationToken);
            return await pending.Task.WaitAsync(_deliveryTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            return new(WorkerProtocolConstants.Version, request.RunId, 504,
                Code: VariableErrorCodes.Timeout, Message: "The Worker variable request timed out. Worker 变量请求超时。");
        }
        catch (Exception exception) when (exception is IOException or WorkerTransportException or ObjectDisposedException)
        {
            return VariablesInactive();
        }
        finally { _variableRequests.TryRemove(requestId, out _); }
    }

    private bool HandleVariableResponse(WorkerMessage message)
    {
        var response = WorkerProtocolCodec.DeserializePayload<WorkerVariableResponseDto>(message);
        if (response.RunId != _request.RunId || response.ProtocolVersion != WorkerProtocolConstants.Version)
            throw new WorkerProtocolException(VariableErrorCodes.InvalidRequest, "Invalid variable response identity.");
        if (_variableRequests.TryGetValue(message.RequestId, out var pending)) pending.TrySetResult(response);
        return true;
    }

    private WorkerVariableResponseDto VariablesInactive()
        => new(WorkerProtocolConstants.Version, _request.RunId, 409,
            Code: WorkerErrorCodes.NotActive, Message: "The Worker run is not active. Worker 运行当前不活动。");

    private void CompleteVariables()
    {
        foreach (var pending in _variableRequests.Values) pending.TrySetResult(VariablesInactive());
        _variableRequests.Clear();
    }
}

using System.Collections.Concurrent;
using SereinFlow.Contracts;
using SereinFlow.Worker.Protocol;

namespace SereinFlow.Worker.Supervisor;
public sealed partial class WorkerSupervisor
{
    public Task<WorkerVariableResponseDto> AccessVariablesAsync(WorkerVariableRequestDto request, CancellationToken cancellationToken = default)
    {
        global::System.Threading.Tasks.Task<global::SereinFlow.Contracts.WorkerVariableResponseDto> evaluatedValue = _messageSessions.TryGetValue(request.RunId, out var session) ? session.AccessVariablesAsync(request, cancellationToken) : resolveFromResultResult();
        return evaluatedValue;
        global::System.Threading.Tasks.Task<global::SereinFlow.Contracts.WorkerVariableResponseDto> resolveFromResultResult()
        {
            var workerVariableResponseDto = new WorkerVariableResponseDto(WorkerProtocolConstants.Version, request.RunId, 409, Code: WorkerErrorCodes.NotActive, Message: "The Worker run is not active. Worker 运行当前不活动。");
            var fromResultResult = Task.FromResult(workerVariableResponseDto);
            return fromResultResult;
        }
    }
}

internal sealed partial class WorkerMessageSession
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<WorkerVariableResponseDto>> _variableRequests = new();
    internal async Task<WorkerVariableResponseDto> AccessVariablesAsync(WorkerVariableRequestDto request, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _completed) == 1)
        {
            var variablesInactiveResult = VariablesInactive();
            return variablesInactiveResult;
        }

        if (request.RunId != _request.RunId || request.ProtocolVersion != WorkerProtocolConstants.Version)
        {
            global::SereinFlow.Contracts.WorkerVariableResponseDto evaluatedValue1 = new(WorkerProtocolConstants.Version, request.RunId, 400, Code: VariableErrorCodes.InvalidRequest);
            return evaluatedValue1;
        }

        var newGuidResult = Guid.NewGuid();
        var requestId = newGuidResult.ToString("N");
        var pending = new TaskCompletionSource<WorkerVariableResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        _variableRequests[requestId] = pending;
        try
        {
            // Completion may race registration; avoid waiting for a response from an exited Worker.
            if (Volatile.Read(ref _completed) == 1)
            {
                var variablesInactiveResult2 = VariablesInactive();
                return variablesInactiveResult2;
            }

            var serializePayloadResult = WorkerProtocolCodec.SerializePayload(request);
            var createResult = WorkerMessage.Create(WorkerProtocolConstants.VariableRequestKind, serializePayloadResult, request.RunId, requestId: requestId);
            await _transport.SendAsync(createResult, cancellationToken);
            var waitAsyncResult = await pending.Task.WaitAsync(_deliveryTimeout, cancellationToken);
            return waitAsyncResult;
        }
        catch (TimeoutException)
        {
            global::SereinFlow.Contracts.WorkerVariableResponseDto evaluatedValue3 = new(WorkerProtocolConstants.Version, request.RunId, 504, Code: VariableErrorCodes.Timeout, Message: "The Worker variable request timed out. Worker 变量请求超时。");
            return evaluatedValue3;
        }
        catch (Exception exception)when (exception is IOException or WorkerTransportException or ObjectDisposedException)
        {
            var variablesInactiveResult4 = VariablesInactive();
            return variablesInactiveResult4;
        }
        finally
        {
            _variableRequests.TryRemove(requestId, out _);
        }
    }

    private bool HandleVariableResponse(WorkerMessage message)
    {
        var response = WorkerProtocolCodec.DeserializePayload<WorkerVariableResponseDto>(message);
        if (response.RunId != _request.RunId || response.ProtocolVersion != WorkerProtocolConstants.Version)
        {
            throw new WorkerProtocolException(VariableErrorCodes.InvalidRequest, "Invalid variable response identity.");
        }

        if (_variableRequests.TryGetValue(message.RequestId, out var pending))
        {
            pending.TrySetResult(response);
        }

        return true;
    }

    private WorkerVariableResponseDto VariablesInactive()
    {
        global::SereinFlow.Contracts.WorkerVariableResponseDto evaluatedValue5 = new(WorkerProtocolConstants.Version, _request.RunId, 409, Code: WorkerErrorCodes.NotActive, Message: "The Worker run is not active. Worker 运行当前不活动。");
        return evaluatedValue5;
    }

    private void CompleteVariables()
    {
        foreach (var pending in _variableRequests.Values)
        {
            var variablesInactiveResult6 = VariablesInactive();
            pending.TrySetResult(variablesInactiveResult6);
        }

        _variableRequests.Clear();
    }
}

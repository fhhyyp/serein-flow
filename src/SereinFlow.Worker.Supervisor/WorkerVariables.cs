using System.Threading.Tasks;
using System.Collections.Concurrent;
using SereinFlow.Contracts;
using SereinFlow.Worker.Protocol;

namespace SereinFlow.Worker.Supervisor;

public sealed partial class WorkerSupervisor
{
    public Task<WorkerVariableResponseDto> AccessVariablesAsync(WorkerVariableRequestDto request, CancellationToken cancellationToken = default)
    {
        return _messageSessions.TryGetValue(request.RunId, out var session) ? session.AccessVariablesAsync(request, cancellationToken) : InactiveRunResponse();
        Task<WorkerVariableResponseDto> InactiveRunResponse()
        {
            var workerVariableResponseDto = new WorkerVariableResponseDto(WorkerProtocolConstants.Version, request.RunId, 409, Code: WorkerErrorCodes.NotActive, Message: "The Worker run is not active. Worker 运行当前不活动。");
            return Task.FromResult(workerVariableResponseDto);
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
            return VariablesInactive();
        }

        if (request.RunId != _request.RunId || request.ProtocolVersion != WorkerProtocolConstants.Version)
        {
            return new(WorkerProtocolConstants.Version, request.RunId, 400, Code: VariableErrorCodes.InvalidRequest);
        }
        var requestId = Guid.NewGuid().ToString("N");
        var pending = new TaskCompletionSource<WorkerVariableResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        _variableRequests[requestId] = pending;
        try
        {
            // Completion may race registration; avoid waiting for a response from an exited Worker.
            // 运行完成可能与注册并发发生；避免等待已经退出的 Worker 返回响应。
            if (Volatile.Read(ref _completed) == 1)
            {
                return VariablesInactive();
            }
            var message = WorkerMessage.Create(WorkerProtocolConstants.VariableRequestKind, WorkerProtocolCodec.SerializePayload(request), request.RunId, requestId: requestId);
            await _transport.SendAsync(message, cancellationToken);
            return await pending.Task.WaitAsync(_deliveryTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            return new(WorkerProtocolConstants.Version, request.RunId, 504, Code: VariableErrorCodes.Timeout, Message: "The Worker variable request timed out. Worker 变量请求超时。");
        }
        catch (Exception exception) when (exception is IOException or WorkerTransportException or ObjectDisposedException)
        {
            return VariablesInactive();
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
            throw new WorkerProtocolException(VariableErrorCodes.InvalidRequest, "Invalid variable response identity. 变量响应标识无效。");
        }

        if (_variableRequests.TryGetValue(message.RequestId, out var pending))
        {
            pending.TrySetResult(response);
        }

        return true;
    }

    private WorkerVariableResponseDto VariablesInactive()
    {
        return new(WorkerProtocolConstants.Version, _request.RunId, 409, Code: WorkerErrorCodes.NotActive, Message: "The Worker run is not active. Worker 运行当前不活动。");
    }

    private void CompleteVariables()
    {
        foreach (var pending in _variableRequests.Values)
        {
            pending.TrySetResult(VariablesInactive());
        }

        _variableRequests.Clear();
    }
}

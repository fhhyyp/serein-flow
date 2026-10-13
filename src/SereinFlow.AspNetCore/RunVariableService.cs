using System.Text;
using System.Text.Json;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Worker.Client;

namespace SereinFlow.Api;

public sealed class RunVariableService(IFlowRunStore runs, IWorkerVariableRunClient client, IFlowRunEventStore events) : IRunVariableService
{
    public async Task<WorkerVariableResponseDto> AccessAsync(
        WorkerVariableRequestDto request, CancellationToken cancellationToken = default)
    {
        var run = await runs.FindAsync(request.RunId, cancellationToken);
        if (run is null)
            return Failure(request.RunId, 404, RunErrorCodes.NotFound, "Run not found. 未找到运行实例。");
        if (request.ProtocolVersion != WorkerProtocol.Version || request.Operation is not ("list" or "get" or "set")
            || request.Offset < 0 || request.Take is < 1 or > 200
            || (request.Operation != "list" && (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 256)))
            return Failure(request.RunId, 400, VariableErrorCodes.InvalidRequest, "Invalid variable operation, name or page.");
        if (request.Operation == "set")
        {
            if (run.IsTerminal || run.Status != FlowRunStatus.Running)
                return Failure(request.RunId, 409, WorkerErrorCodes.NotActive, "Only an active Worker can change variables. 仅活动 Worker 可修改变量。");
            var value = request.Value;
            if (value.ValueKind == JsonValueKind.Undefined)
                return Failure(request.RunId, 400, VariableErrorCodes.ValueInvalid, "An explicit JSON value is required.");
            if (request.ValueType is not ("null" or "boolean" or "string" or "int32" or "int64" or "double" or "decimal" or "json"))
                return Failure(request.RunId, 400, VariableErrorCodes.TypeInvalid, "Unsupported variable valueType.");
            if (Encoding.UTF8.GetByteCount(value.GetRawText()) > 16_384)
                return Failure(request.RunId, 413, VariableErrorCodes.TooLarge, "Variable values are limited to 16384 JSON bytes.");
        }
        if (run.IsTerminal)
            return await ReadFinalAsync(request, cancellationToken);
        if (run.Status != FlowRunStatus.Running)
            return Failure(request.RunId, 409, WorkerErrorCodes.NotActive, "The Worker is not active. Worker 尚未活动。");
        var response = await client.AccessVariablesAsync(request, cancellationToken);
        // Completion can be persisted between the status read and the Worker request.
        // 状态读取与 Worker 请求之间可能恰好完成运行，回退到已保存的最终快照。
        return request.Operation != "set" && response.Code == WorkerErrorCodes.NotActive
            ? await ReadFinalAsync(request, cancellationToken)
            : response;
    }

    private async Task<WorkerVariableResponseDto> ReadFinalAsync(WorkerVariableRequestDto request, CancellationToken cancellationToken)
    {
        var lastSequence = await events.GetLastSequenceAsync(request.RunId, cancellationToken);
        var tail = await events.GetAfterAsync(request.RunId, Math.Max(0, lastSequence - 8), cancellationToken);
        foreach (var item in tail.OrderByDescending(static item => item.Sequence))
        {
            if (item.Type is not (RunErrorCodes.Completed or RunErrorCodes.Failed or RunErrorCodes.Cancelled or RunErrorCodes.TimedOut))
                continue;
            try
            {
                using var payload = JsonDocument.Parse(item.PayloadJson);
                if (payload.RootElement.ValueKind != JsonValueKind.Object
                    || !payload.RootElement.TryGetProperty("variables", out var value) || value.ValueKind != JsonValueKind.Object)
                    continue;
                var snapshot = value.Deserialize<RunVariableSnapshotDto>(SereinJsonSerialization.CreateWebOptions());
                if (snapshot is null || snapshot.RunId != request.RunId || !snapshot.IsFinal || snapshot.Variables is null)
                    continue;
                if (request.Operation == "get")
                {
                    var variable = snapshot.Variables.FirstOrDefault(entry => entry.Name == request.Name);
                    return variable is not null
                        ? new(WorkerProtocol.Version, request.RunId, 200, Variable: variable)
                        : Failure(request.RunId, 404, VariableErrorCodes.NotFound, "Variable not found in the saved snapshot. 保存的快照中未找到变量。");
                }
                var page = snapshot.Variables.Skip(request.Offset).Take(request.Take).ToArray();
                return new(WorkerProtocol.Version, request.RunId, 200, Snapshot: snapshot with
                {
                    Variables = page, Offset = request.Offset,
                    HasMore = request.Offset + page.Length < snapshot.Variables.Count,
                });
            }
            catch (JsonException)
            {
                // Legacy or malformed terminal events do not contain a recoverable snapshot.
            }
        }
        return Failure(request.RunId, 409, VariableErrorCodes.SnapshotUnavailable,
            "This run has no saved variable snapshot. 此运行未保存流程变量快照。");
    }

    private static WorkerVariableResponseDto Failure(Guid runId, int status, string code, string message)
        => new(WorkerProtocol.Version, runId, status, Code: code, Message: message);
}

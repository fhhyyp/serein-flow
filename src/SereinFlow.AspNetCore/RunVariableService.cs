using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Worker.Client;

namespace SereinFlow.Api;

public sealed class RunVariableService(IFlowRunStore runs, IWorkerVariableRunClient client, IFlowRunEventStore events) : IRunVariableService
{
    private const int MaximumVariablePageSize = 200;
    private const int MaximumVariableNameLength = 256;
    private const int FinalSnapshotEventLookback = 8;
    private const string ListOperation = "list";
    private const string GetOperation = "get";
    private const string SetOperation = "set";
    public async Task<WorkerVariableResponseDto> AccessAsync(WorkerVariableRequestDto request, CancellationToken cancellationToken = default)
    {
        var run = await runs.FindAsync(request.RunId, cancellationToken);
        if (run is null)
        {
            var failureResult = Failure(request.RunId, StatusCodes.Status404NotFound, RunErrorCodes.NotFound, "Run not found. 未找到运行实例。");
            return failureResult;
        }

        if (request.ProtocolVersion != WorkerProtocol.Version || request.Operation is not (ListOperation or GetOperation or SetOperation) || request.Offset < 0 || request.Take is < 1 or > MaximumVariablePageSize || (request.Operation != ListOperation && (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > MaximumVariableNameLength)))
        {
            var failureResult1 = Failure(request.RunId, StatusCodes.Status400BadRequest, VariableErrorCodes.InvalidRequest, "Invalid variable operation, name or page.");
            return failureResult1;
        }

        if (request.Operation == SetOperation)
        {
            if (run.IsTerminal || run.Status != FlowRunStatus.Running)
            {
                var failureResult2 = Failure(request.RunId, StatusCodes.Status409Conflict, WorkerErrorCodes.NotActive, "Only an active Worker can change variables. 仅活动 Worker 可修改变量。");
                return failureResult2;
            }

            var value = request.Value;
            if (value.ValueKind == JsonValueKind.Undefined)
            {
                var failureResult3 = Failure(request.RunId, StatusCodes.Status400BadRequest, VariableErrorCodes.ValueInvalid, "An explicit JSON value is required.");
                return failureResult3;
            }

            if (request.ValueType is not ("null" or "boolean" or "string" or "int32" or "int64" or "double" or "decimal" or "json"))
            {
                var failureResult4 = Failure(request.RunId, StatusCodes.Status400BadRequest, VariableErrorCodes.TypeInvalid, "Unsupported variable valueType.");
                return failureResult4;
            }

            if (resolveGetByteCountResult() > 16_384)
            {
                var failureResult5 = Failure(request.RunId, 413, VariableErrorCodes.TooLarge, "Variable values are limited to 16384 JSON bytes.");
                return failureResult5;
            }

            int resolveGetByteCountResult()
            {
                var getRawTextResult = value.GetRawText();
                var getByteCountResult = Encoding.UTF8.GetByteCount(getRawTextResult);
                return getByteCountResult;
            }
        }

        if (run.IsTerminal)
        {
            var readFinalAsyncResult = await ReadFinalAsync(request, cancellationToken);
            return readFinalAsyncResult;
        }

        if (run.Status != FlowRunStatus.Running)
        {
            var failureResult6 = Failure(request.RunId, StatusCodes.Status409Conflict, WorkerErrorCodes.NotActive, "The Worker is not active. Worker 尚未活动。");
            return failureResult6;
        }

        var response = await client.AccessVariablesAsync(request, cancellationToken);
        WorkerVariableResponseDto variableResponse = request.Operation != SetOperation && response.Code == WorkerErrorCodes.NotActive ? await ReadFinalAsync(request, cancellationToken) : response;
        // Completion can be persisted between the status read and the Worker request.
        // 状态读取与 Worker 请求之间可能恰好完成运行，回退到已保存的最终快照。
        return variableResponse;
    }

    private async Task<WorkerVariableResponseDto> ReadFinalAsync(WorkerVariableRequestDto request, CancellationToken cancellationToken)
    {
        var lastSequence = await events.GetLastSequenceAsync(request.RunId, cancellationToken);
        var firstTailSequence = Math.Max(0, lastSequence - FinalSnapshotEventLookback);
        var tail = await events.GetAfterAsync(request.RunId, firstTailSequence, cancellationToken);
        foreach (var item in tail.OrderByDescending(static item => item.Sequence))
        {
            if (item.Type is not (RunErrorCodes.Completed or RunErrorCodes.Failed or RunErrorCodes.Cancelled or RunErrorCodes.TimedOut))
            {
                continue;
            }

            try
            {
                using var payload = JsonDocument.Parse(item.PayloadJson);
                if (payload.RootElement.ValueKind != JsonValueKind.Object || !payload.RootElement.TryGetProperty("variables", out var value) || value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var createWebOptionsResult = SereinJsonSerialization.CreateWebOptions();
                var snapshot = value.Deserialize<RunVariableSnapshotDto>(createWebOptionsResult);
                if (snapshot is null || snapshot.RunId != request.RunId || !snapshot.IsFinal || snapshot.Variables is null)
                {
                    continue;
                }

                if (request.Operation == GetOperation)
                {
                    var variable = snapshot.Variables.FirstOrDefault(entry => entry.Name == request.Name);
                    WorkerVariableResponseDto evaluatedValue7 = variable is not null ? new(WorkerProtocol.Version, request.RunId, StatusCodes.Status200OK, Variable: variable) : Failure(request.RunId, StatusCodes.Status404NotFound, VariableErrorCodes.NotFound, "Variable not found in the saved snapshot. 保存的快照中未找到变量。");
                    return evaluatedValue7;
                }

                var page = snapshot.Variables
                    .Skip(request.Offset)
                    .Take(request.Take)
                    .ToArray();
                WorkerVariableResponseDto evaluatedValue8 = new(WorkerProtocol.Version, request.RunId, StatusCodes.Status200OK, Snapshot: snapshot with { Variables = page, Offset = request.Offset, HasMore = request.Offset + page.Length < snapshot.Variables.Count, });
                return evaluatedValue8;
            }
            catch (JsonException)
            {
                // Legacy or malformed terminal events do not contain a recoverable snapshot.
            }
        }

        var failureResult9 = Failure(request.RunId, StatusCodes.Status409Conflict, VariableErrorCodes.SnapshotUnavailable, "This run has no saved variable snapshot. 此运行未保存流程变量快照。");
        return failureResult9;
    }

    private static WorkerVariableResponseDto Failure(Guid runId, int status, string code, string message)
    {
        WorkerVariableResponseDto evaluatedValue10 = new(WorkerProtocol.Version, runId, status, Code: code, Message: message);
        return evaluatedValue10;
    }
}

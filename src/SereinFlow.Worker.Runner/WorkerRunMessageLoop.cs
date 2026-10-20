using SereinFlow.Contracts;
using SereinFlow.Library;
using SereinFlow.Worker.Protocol;

namespace SereinFlow.Worker.Runner;
internal static class WorkerRunMessageLoop
{
    public static async Task RunAsync(WorkerRunRequestDto request, IWorkerTransport transport, WorkerMessageService messageService, LibraryVariableService variables, Task runTask, DebugRunController? debugController, CancellationTokenSource runCancellation, CancellationToken cancellationToken)
    {
        var receiveAsyncResult3 = transport.ReceiveAsync(cancellationToken);
        var readTask = receiveAsyncResult3.AsTask();
        while (!runTask.IsCompleted)
        {
            var completed = await Task.WhenAny(runTask, readTask);
            if (completed == runTask)
            {
                break;
            }

            var message = await readTask;
            if (message is null)
            {
                runCancellation.Cancel();
                break;
            }

            if (message.Kind == WorkerProtocolConstants.CancelKind && message.RunId == request.RunId)
            {
                runCancellation.Cancel();
                var createResult = WorkerMessage.Create(WorkerProtocolConstants.CancelAcknowledgedKind, runId: request.RunId);
                await transport.SendAsync(createResult, cancellationToken);
            }
            else if (debugController is not null && message.RunId == request.RunId && message.Kind is WorkerProtocolConstants.DebugContinueKind or WorkerProtocolConstants.DebugStepKind or WorkerProtocolConstants.DebugStopKind)
            {
                var command = WorkerProtocolCodec.DeserializePayload<WorkerDebugCommandDto>(message);
                var accepted = message.Kind switch
                {
                    WorkerProtocolConstants.DebugContinueKind => debugController.TryContinue(command),
                    WorkerProtocolConstants.DebugStepKind => debugController.TryStep(command),
                    WorkerProtocolConstants.DebugStopKind => debugController.TryStop(command),
                    _ => false
                };
                if (accepted && message.Kind == WorkerProtocolConstants.DebugStopKind)
                {
                    runCancellation.Cancel();
                }
            }
            else if (message.Kind == WorkerProtocolConstants.HeartbeatKind)
            {
                var createResult1 = WorkerMessage.Create(WorkerProtocolConstants.HeartbeatAcknowledgedKind, runId: request.RunId);
                await transport.SendAsync(createResult1, cancellationToken);
            }
            else if (message.Kind == WorkerProtocolConstants.MessageDeliverKind && message.RunId == request.RunId)
            {
                await SendDeliveryResultAsync(request, transport, messageService, message, cancellationToken);
            }
            else if (message.Kind == WorkerProtocolConstants.VariableRequestKind && message.RunId == request.RunId)
            {
                WorkerVariableResponseDto response;
                try
                {
                    var command = WorkerProtocolCodec.DeserializePayload<WorkerVariableRequestDto>(message);
                    response = WorkerVariableService.Execute(request.RunId, variables, command);
                }
                catch (WorkerProtocolException)
                {
                    response = new(WorkerProtocolConstants.Version, request.RunId, 400, Code: VariableErrorCodes.InvalidRequest, Message: "Invalid variable request. 变量请求无效。");
                }

                var serializePayloadResult = WorkerProtocolCodec.SerializePayload(response);
                var createResult2 = WorkerMessage.Create(WorkerProtocolConstants.VariableResponseKind, serializePayloadResult, request.RunId, requestId: message.RequestId);
                await transport.SendAsync(createResult2, cancellationToken);
            }

            var receiveAsyncResult = transport.ReceiveAsync(cancellationToken);
            readTask = receiveAsyncResult.AsTask();
        }

        await runTask;
    }

    private static async Task SendDeliveryResultAsync(WorkerRunRequestDto request, IWorkerTransport transport, WorkerMessageService messageService, WorkerMessage message, CancellationToken cancellationToken)
    {
        var delivery = WorkerProtocolCodec.DeserializePayload<WorkerMessageDeliveryDto>(message);
        var outcome = messageService.TryDeliver(delivery);
        var responseKind = outcome.Accepted ? WorkerProtocolConstants.MessageAcceptedKind : WorkerProtocolConstants.MessageRejectedKind;
        var responsePayload = outcome.Accepted ? resolveSerializePayloadResult() : resolveSerializePayloadResult5();
        var createResult7 = WorkerMessage.Create(responseKind, responsePayload, request.RunId, requestId: message.RequestId);
        await transport.SendAsync(createResult7, cancellationToken);
        string resolveSerializePayloadResult()
        {
            var workerMessageAcceptedDto = new WorkerMessageAcceptedDto(WorkerProtocolConstants.Version, request.RunId, outcome.MessageId, outcome.Topic, outcome.Duplicate);
            var serializePayloadResult4 = WorkerProtocolCodec.SerializePayload(workerMessageAcceptedDto);
            return serializePayloadResult4;
        }

        string resolveSerializePayloadResult5()
        {
            var workerMessageRejectedDto = new WorkerMessageRejectedDto(WorkerProtocolConstants.Version, request.RunId, outcome.MessageId, outcome.Topic, outcome.Code ?? MessageErrorCodes.Rejected, outcome.Message);
            var serializePayloadResult6 = WorkerProtocolCodec.SerializePayload(workerMessageRejectedDto);
            return serializePayloadResult6;
        }
    }
}

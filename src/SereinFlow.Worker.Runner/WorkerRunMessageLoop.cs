using SereinFlow.Contracts;
using SereinFlow.Worker.Protocol;

namespace SereinFlow.Worker.Runner;

internal static class WorkerRunMessageLoop
{
    public static async Task RunAsync(
        WorkerRunRequestDto request,
        IWorkerTransport transport,
        WorkerMessageService messageService,
        Task runTask,
        DebugRunController? debugController,
        CancellationTokenSource runCancellation,
        CancellationToken cancellationToken)
    {
        var readTask = transport.ReceiveAsync(cancellationToken).AsTask();
        while (!runTask.IsCompleted)
        {
            var completed = await Task.WhenAny(runTask, readTask);
            if (completed == runTask)
                break;

            var message = await readTask;
            if (message is null)
            {
                runCancellation.Cancel();
                break;
            }

            if (message.Kind == WorkerProtocolConstants.CancelKind && message.RunId == request.RunId)
            {
                runCancellation.Cancel();
                await transport.SendAsync(
                    WorkerMessage.Create(WorkerProtocolConstants.CancelAcknowledgedKind, runId: request.RunId),
                    cancellationToken);
            }
            else if (debugController is not null
                && message.RunId == request.RunId
                && message.Kind is WorkerProtocolConstants.DebugContinueKind
                    or WorkerProtocolConstants.DebugStepKind
                    or WorkerProtocolConstants.DebugStopKind)
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
                    runCancellation.Cancel();
            }
            else if (message.Kind == WorkerProtocolConstants.HeartbeatKind)
            {
                await transport.SendAsync(
                    WorkerMessage.Create(WorkerProtocolConstants.HeartbeatAcknowledgedKind, runId: request.RunId),
                    cancellationToken);
            }
            else if (message.Kind == WorkerProtocolConstants.MessageDeliverKind
                && message.RunId == request.RunId)
            {
                await SendDeliveryResultAsync(request, transport, messageService, message, cancellationToken);
            }

            readTask = transport.ReceiveAsync(cancellationToken).AsTask();
        }

        await runTask;
    }

    private static async Task SendDeliveryResultAsync(
        WorkerRunRequestDto request,
        IWorkerTransport transport,
        WorkerMessageService messageService,
        WorkerMessage message,
        CancellationToken cancellationToken)
    {
        var delivery = WorkerProtocolCodec.DeserializePayload<WorkerMessageDeliveryDto>(message);
        var outcome = messageService.TryDeliver(delivery);
        var responseKind = outcome.Accepted
            ? WorkerProtocolConstants.MessageAcceptedKind
            : WorkerProtocolConstants.MessageRejectedKind;
        var responsePayload = outcome.Accepted
            ? WorkerProtocolCodec.SerializePayload(new WorkerMessageAcceptedDto(
                WorkerProtocolConstants.Version,
                request.RunId,
                outcome.MessageId,
                outcome.Topic,
                outcome.Duplicate))
            : WorkerProtocolCodec.SerializePayload(new WorkerMessageRejectedDto(
                WorkerProtocolConstants.Version,
                request.RunId,
                outcome.MessageId,
                outcome.Topic,
                outcome.Code ?? MessageErrorCodes.Rejected,
                outcome.Message));

        await transport.SendAsync(
            WorkerMessage.Create(responseKind, responsePayload, request.RunId, requestId: message.RequestId),
            cancellationToken);
    }
}

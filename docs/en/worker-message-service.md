# Worker Message Service

Each Worker run creates an isolated in-memory message service and injects the
same `IMessageService` singleton into the node libraries loaded for that run.
The message service is not persisted across runs and does not retain messages
after the Worker exits.

## SDK Semantics

The SDK contract is defined in `SereinFlow.Library`. Node libraries depend only
on the interfaces, not on the Runner or transport implementation:

```csharp
public interface IMessageService
{
    IMessageQueue CreateMessageQueue(MessageChannelOptions? options = null);
    IEventBus CreateEventBus(MessageChannelOptions? options = null);
}
```

`MessageQueue` is a topic-based competing-consumer queue: multiple consumers
share one FIFO stream and each message is delivered to only one consumer.
`EventBus` provides an independent buffer for each subscriber; subscribers
that already exist when a message is published each receive one copy, while
late subscribers do not receive historical messages.

Channels use JSON serialization by default. `DirectObject` is only for object
references inside the same Worker process and requires the received object to
be assignable to the target type. Messages crossing an isolated load context
or coming from an external API must use JSON. Immutable DTOs are recommended.

Every topic is bounded. Its options include:

- `Capacity`: buffer capacity; must be positive;
- `OverflowStrategy`: `Reject`, `DropOldest`, or `DropNewest`;
- `MessageTtl`: optional message lifetime;
- `MaxPayloadBytes`: maximum JSON payload size;
- `ContractId`: optional logical contract identifier;
- `ExternalIngress`: whether Supervisor/API external delivery is allowed;
  disabled by default.

All send, receive, and subscription waits accept a `CancellationToken`. Run
cancellation, timeouts, or Worker shutdown wake the wait and release the
underlying subscription.

## Declaring a Node Endpoint

Only endpoints with `ExternalIngress = true` are registered with the
Supervisor. The following FlipFlop endpoint receives a JSON string for the
`example.text` contract:

```csharp
public sealed class MessageNodes(IMessageService messageService)
{
    public async Task<string> ReceiveExternal(IFlowContext context)
    {
        var queue = messageService.CreateMessageQueue(new MessageChannelOptions
        {
            SerializationMode = MessageSerializationMode.Json,
            ExternalIngress = true,
            ContractId = "example.text",
            Capacity = 8
        });

        return await queue.ReceiveAsync<string>("example.inbox", context.CancellationToken);
    }
}
```

The registration includes the channel kind, serialization mode, and
`contractId`. Internal topics are not exposed automatically, and an endpoint
declaration cannot allow an external request to specify an assembly-qualified
type.

## External API

Both active ordinary runs and debug runs support:

```http
POST /api/runs/{runId}/messages/{topic}
Idempotency-Key: optional-client-key
Content-Type: application/json
```

Request body:

```json
{
  "payload": { "value": "hello" },
  "messageId": "optional-guid",
  "contractId": "example.text",
  "channelKind": "Queue"
}
```

The API accepts JSON payloads only and validates the run state, topic,
`messageId`/`Idempotency-Key`, size, endpoint registration,
`ExternalIngress`, and `contractId`. `payload` may be an object, array, string,
number, Boolean, or JSON `null`.

When supplied, `messageId` must be a non-empty GUID. If it is omitted, a
GUID-formatted `Idempotency-Key` is used directly as the message ID; an
ordinary string produces a stable GUID from `runId + topic + key`; if neither
is present, a new GUID is generated. `channelKind` defaults to `Queue` and may
also be `EventBus`.

`202 Accepted` means that the message entered the local Worker Broker. It does
not mean that the downstream flow has completed: it confirms only the Worker
receipt and not the Flipflop successor, the complete flow, or any business side
effect. Observe run events, outputs, and the final run state for execution
results. The message is always delivered to the active Worker for its `runId`
and the flow snapshot taken when that Worker started; the system does not
recreate a Worker or resume a run after it ends.

Common results:

| HTTP | Meaning |
| --- | --- |
| `202` | Accepted; a repeated message ID is still accepted and marked duplicate |
| `404` | The run does not exist or Supervisor has no matching active Worker session |
| `409` | The run ended, the endpoint is not registered, or the message session is unavailable |
| `403` | The endpoint does not allow external delivery |
| `429` | The bounded channel is full |
| `504` | The Worker did not respond within the delivery timeout |

The protocol bridge continues to use the Worker's single receive loop and
serialized send path. The message service does not operate
`StdioWorkerTransport` directly, so replacing it with another
`IWorkerTransport` does not require changes to the node SDK or Broker semantics.

## MCP Publish Tool

Authenticated HTTP MCP or stdio MCP with an explicitly configured API key can
use `sereinflow_publish_run_message` to publish to an active ordinary or debug
run:

```json
{
  "runId": "8e0f2b1e-7e9c-4e8e-b5d0-2f4d8d6f21a8",
  "topic": "order.created",
  "payload": { "orderId": "A10001", "amount": 99.5 },
  "channelKind": "queue",
  "contractId": "order.created.v1",
  "idempotencyKey": "agent-call-20260902-001"
}
```

The tool requires the `run.message.publish` permission and a non-empty
`idempotencyKey`. It persists the result by MCP principal and request content.
Retrying with the same principal, tool, and idempotency key returns the original
structured result without calling the Worker again. The Worker also deduplicates
messages in the run-level Broker by `messageId`. MCP's `accepted` result likewise
means only that the Broker received the message, not that downstream business
work has completed.

## Version 1 Scope

The current implementation does not provide a cross-Worker Broker,
persistence/replay, consumer acknowledgements, recovery after a run ends,
in-run flow or assembly replacement, or automatic forwarding of every internal
event to the API.

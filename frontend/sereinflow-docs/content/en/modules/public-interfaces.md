# Public flow interfaces

Create a **public flow interface** when another program needs to invoke a flow. Callers use the interface ID without manipulating the internal canvas or versions.

## From flow to external call

Save the flow, [publish a production version](/en/modules/versions), then create and enable the interface. A flow without a production version cannot have a public interface.

An interface can be **synchronous** or **asynchronous**. Synchronous calls wait briefly and return the result if it finishes in time; otherwise they return a task ID. Asynchronous calls return a task ID immediately. Either way, use that ID to query later.

## Caller input

Invoke with `POST /api/public/flows/{interfaceId}/invoke`. The `projectInputs` body field is a JSON object whose keys depend on your flow. If REST authentication is enabled, invocation requires `run.execute`; querying requires `run.read`.

A disabled or deleted interface, or an unavailable production version, cannot start normally. See [Public interfaces API](/en/api/public-interfaces).

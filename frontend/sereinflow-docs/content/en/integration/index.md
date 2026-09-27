# Integration

“Service injection” can mean three different things. Choose the task first:

| Goal | Entry point | Read |
| --- | --- | --- |
| Add nodes and share an object between them | `[FlowService]` in a node library | [Register services](/en/library/services) |
| Run SereinFlow inside your ASP.NET Core application | `AddSereinFlow`, `UseSereinFlow`, `MapSereinFlow` | [Host integration](/en/integration/hosting) |
| Call a running SereinFlow instance from .NET | `AddSereinFlowClient`, `ISereinFlowClient` | [.NET client](/en/integration/client-di) |

See [Internal service registration](/en/integration/service-registration) for SereinFlow's storage, execution, HTTP, and MCP registrations. Uploaded node libraries run in a separate Worker; host services are not automatically passed to node constructors.

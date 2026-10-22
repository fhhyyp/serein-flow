# SereinFlow.Mcp.Framework

Independent .NET 10 tool hosting primitives: generated tool registries, JSON request binding,
schema generation, DataAnnotations validation, direct invocation and scoped DI activation.
There are no references to SereinFlow business assemblies or ASP.NET Core.

Sources are grouped into `Hosting` (execution, policies and mutation leases),
`Binding` (request binding and validation), `Metadata` (registries, catalogs and discovery),
and `Schema`. Public types keep the `SereinFlow.Mcp.Framework` namespace.
Tool dependencies can be declared through the single accessible constructor;
generated factories register instance handlers as scoped services. Host caller context
is a separate method parameter and should not be used as a service locator.

Add `SereinFlow.Mcp.Generator` as an analyzer (`OutputItemType="Analyzer"`,
`ReferenceOutputAssembly="false"`) to each assembly declaring tools. The generator emits
`<AssemblyName>.Generated.McpToolRegistry`. The example below assumes assembly name `ExampleHost`.
Declaration attributes are emitted as internal types by the generator's post-initialization output.
Runtime APIs depend on `IMcpToolMetadata` and `McpParameterOptions`, rather than generated attribute identities.

```csharp
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Mcp.Framework;

var services = new ServiceCollection();
var registry = new ExampleHost.Generated.McpToolRegistry();
services.AddMcpToolHandlers(registry);
await using var provider = services.BuildServiceProvider();
var catalog = McpToolDiscovery.CreateCatalog(registry);

await using var scope = provider.CreateAsyncScope();
if (catalog.TryGet("echo", out var tool))
{
    var arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { text = "hello" });
    var result = await tool!.ExecuteAsync(new McpToolCallContext(scope.ServiceProvider), arguments);
}

public sealed record EchoRequest(string Text);

public static class EchoTools
{
    [McpTool("echo", Description = "Echo text.")]
    public static Task<string> EchoAsync(EchoRequest request) => Task.FromResult(request.Text);
}
```

`AddMcpToolHandlers` also registers `McpToolExecutor`. Use it to own the call scope,
look up the tool and run registered `IMcpToolMiddleware` in registration order.
Middleware can provide host context using `context.CallContext.SetValue(...)` and wrap `next`.
The direct invocation example above leaves scope ownership with its caller.

Host responsibilities: authorization, transport, error mapping, audit and persistent idempotency. Derive an internal `McpToolAttribute`
to attach host-specific policies. Mark context types or parameters with `[McpContext]`
and supply values using `McpToolCallContext`.
Pass serializer settings through `McpToolDiscoveryOptions.JsonOptions`.
Explicit descriptors can preserve existing published schemas and tool ordering.
Use the framework's `McpJsonSchema` vocabulary for hand-authored schemas.

Methods return `Task<T>`, accept at most one object request, and may accept a
`CancellationToken` plus marked context parameters. Binding and validation errors
use `McpParameterBindingException`; business exceptions and cancellation keep their types.

Raw `JsonElement` request parameters are rejected by the generator. Arbitrary JSON
values may still be properties of a typed request. Use `[McpBindProperties]` on a
parameterless request with writable properties to generate JSON field binding;
`[McpParameter]` controls required fields, trimming, null acceptance and error metadata.
Absent optional fields retain property defaults, and required fields are included in the
default schema. Ordinary DTOs use the configured serializer options unchanged.
Hosts may register scoped `IMcpRequestBinder<T>` implementations for legacy wire normalization.

Use `[McpHost("AddExampleTools")]` on a top-level partial static class to generate
registration and catalog overloads. Optional partial hooks `ConfigureServices`,
`ConfigureJsonOptions` and `ConfigureCatalog` supply business configuration.

Derive `McpToolPolicy` to provide authorization, result semantics, failure mapping
and audit projection. It owns mutation leases, timing, completion records and an
independent asynchronous scope for recording. Recording errors cannot replace a
tool result or exception. `McpToolMutationGate` serializes calls requiring an
idempotency key by tool name; persistent replay storage remains a host concern.
The policy runs through `McpToolExecutor`; direct tool delegates bypass middleware.

# Development: ScriptAdapter

`src/SereinFlow.ScriptAdapter` connects flow node contracts to ScriptLang. Start here to change input/output behavior, diagnostics, caching, or CLR conversion. To add script-callable business methods, see [ScriptModules](/en/script/script-modules).

## Entry points and execution order

| Type | Responsibility |
| --- | --- |
| `IScriptNodeExecutor` / `SereinScriptNodeExecutor` | Implements `INodeExecutor` for `NodeType.Script`. |
| `ISereinLangCompiler` / `SereinLangCompiler` | Compiles without execution; returns `ScriptCompileResultDto` and diagnostics. |
| `ScriptArtifactStore` | Rebuilds and loads per-project `.ssc` bytecode and `manifest.json`. |
| `ScriptValueConverter` | Converts CLR/JSON inputs to script values and creates audit projections. |
| `ScriptValueTypeConverter` | Converts script values to the target CLR type at downstream .NET parameter boundaries. |

`ExecuteAsync` validates inputs and cancellation, creates a `ScriptEngine` for this invocation, and registers `sereinflow`. It tries a matching cached artifact and compiles the source when none is usable. After creating the task, it registers every declared input name, injects script values, runs the task, and maps its result to output contracts.

`src/SereinFlow.Worker.Runner/Program.cs` adds the executor to `NodeExecutorRegistry`. MCP registers the diagnostic compiler through `SereinFlowMcpRegistration`. Replace an executor at the Worker's registration point; registering it only in the ASP.NET Core container does not replace the Worker's instance.

## Input and output contracts

`ScriptNodeDefinition.Create` holds `nodeId`, `source`, `languageVersion`, and input/output `ScriptValueContract` entries. A contract contains `Name`, `ValueKind`, `Required`, optional `Id`, and `Description`.

- Request input keys and result output keys use `Id ?? Name`; script variables use `Name`.
- Required validation checks key presence, so null counts as supplied. Omitted optional inputs receive `Value.Null`.
- One output receives the whole `Value`. Multiple outputs require `ObjectValue` and read properties by output `Name`; missing properties currently map to CLR null.
- The executor does not coerce results according to `ValueKind`. Reflected library calls use `ScriptValueTypeConverter` to convert to the target parameter type.

Put this complete example in a .NET 10 console project referencing `SereinFlow.ScriptAdapter`:

```csharp
using SereinFlow.Domain;
using LibraryVariableService = SereinFlow.Library.LibraryVariableService;
using SereinFlow.Runtime.Abstractions;
using SereinFlow.ScriptAdapter;
using ScriptLang.Runtime;

var script = ScriptNodeDefinition.Create(
    "script-node", "return amount + 1", "0.1",
    inputs: [new("amount", "System.Int32", Required: true, Id: "amount-id")],
    outputs: [new("result", "System.Int32", Id: "result-id")]);
var node = NodeDefinition.Create(
    "script-node", NodeType.Script, "Add one", script: script);
var context = new MemoryExecutionContext();
context.Write("projectId", "demo");
var variables = new LibraryVariableService(); // Reuse this instance throughout one run
var runtime = new NodeExecutionRuntime(
    new NodeExecutionEnvironment(
        Guid.NewGuid(), "demo", Guid.NewGuid(), "main", node.Id, 0),
    variables: variables);

var result = await new SereinScriptNodeExecutor().ExecuteAsync(
    new NodeExecutionRequest(
        node, context, new Dictionary<string, object?> { ["amount-id"] = 41 }, runtime),
    CancellationToken.None);
if (!result.IsSuccess)
    throw new InvalidOperationException($"{result.ErrorCode}: {result.ErrorMessage}");
var value = (Value)result.Outputs["result-id"]!;
Console.WriteLine(ScriptValueTypeConverter.Convert(value, typeof(int))); // 42

sealed class MemoryExecutionContext : IExecutionContext
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);
    public object? Read(string key) => _values.GetValueOrDefault(key);
    public void Write(string key, object? value) => _values[key] = value;
    public IReadOnlyDictionary<string, object?> Snapshot() => _values;
}
```

Direct adapter calls are useful for local development. Production hosting executes flows through the Worker. Supply an `INodeExecutionEventSink` to collect logs, and cancellation tokens and a cancellation probe to respond to host cancellation.

## Diagnostic compilation

```csharp
using SereinFlow.Contracts;
using SereinFlow.ScriptAdapter;

var compiler = new SereinLangCompiler();
var diagnostic = await compiler.CompileAsync(new ScriptCompileRequestDto(
    "return amount + 1", "demo.serein",
    SereinLangCompiler.SupportedLanguageVersion,
    [new ScriptValueContractDto("amount", "System.Int32", true)]));
Console.WriteLine(diagnostic.IsSuccess);
```

Compilation creates no execution task or `.ssc` artifact and does not execute imports. Success establishes compilability, not the existence of runtime modules or validity of actual inputs. The result includes source SHA-256, language version, source name, duration, and diagnostics, with line/column positions when extractable.

The current entry point accepts version `0.1`, at most 256 KiB of UTF-8 source and 128 inputs, and a 10-second compilation wait. The underlying compiler is synchronous; work already started may continue after response timeout or cancellation. These limits belong to diagnostic compilation; the direct executor does not repeat all of this validation.

## Bytecode caching

`ScriptArtifactStore.RebuildProject(projectId, definitions)` creates `{nodeId}.ssc` files and a project manifest. When configured with a cache root, the Worker calls it at run startup. Normal rebuilds remove the old project cache, compile into staging, and publish after all scripts succeed.

`TryLoad` validates `ArtifactFingerprint`, which covers source, language version, input IDs/names/types/required flags, and `ScriptNodeDefinition.CompilerCompatibilityVersion`. Input-only changes invalidate old artifacts. Output contracts are not in the fingerprint because mapping happens after execution. Missing, corrupt, or mismatched artifacts cause source compilation.

Update `CompilerCompatibilityVersion` when changing compiler global-variable conventions or bytecode compatibility. Register every declared input after loading an artifact, even if the source does not reference it, so unused but valid connectors can receive values.

## Conversion and execution branches

Internal flow transport retains `ScriptLang.Runtime.Value`. `ScriptValueTypeConverter.Convert(value, targetType)` handles scalars, dates, durations, arrays/lists, dictionaries, and mappable objects. Compatible CLR handles retain identity. Strings convert only to strings, Booleans only to Booleans, and null cannot target a non-nullable value type.

`ScriptValueConverter.ToAuditValue` projects values for Worker events, persistence, and clients; CLR objects, methods, and functions become summaries. The compatibility method `ToClrValue` is also an audit projection. Do not use it for internal transport or run variable storage.

Normal results select `Success`. Input validation, script errors, and unsupported values select `Error`. Cancellation returns `Failure` with `script.cancelled`. There is currently no convention that selects a business Failure branch from a returned script object.

## Validate a change

From the repository root, run the existing adapter tests covering connector IDs, caches, multiple outputs, cancellation, logs, environment, and variable conversion:

```powershell
dotnet test tests/SereinFlow.ScriptAdapter.Tests/SereinFlow.ScriptAdapter.Tests.csproj
```

Check both source and `.ssc` execution when adding capabilities, downstream .NET parameters and audit projection when changing conversion, and [diagnostic and execution registration](/en/script/script-modules#make-flow-nodes-use-the-module) when changing modules.

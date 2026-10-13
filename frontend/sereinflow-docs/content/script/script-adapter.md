# 二次开发：ScriptAdapter

`src/SereinFlow.ScriptAdapter` 将流程节点契约接到 ScriptLang 引擎。修改输入输出、编译诊断、缓存或 CLR 转换时，从这里入手。给脚本增加业务方法则阅读[ScriptModules](/script/script-modules)。

## 关键入口与执行顺序

| 类型 | 职责 |
| --- | --- |
| `IScriptNodeExecutor` / `SereinScriptNodeExecutor` | 实现 `INodeExecutor`，处理 `NodeType.Script`。 |
| `ISereinLangCompiler` / `SereinLangCompiler` | 只编译源码，返回 `ScriptCompileResultDto` 与诊断。 |
| `ScriptArtifactStore` | 按项目重建与加载 `.ssc` 字节码及 `manifest.json`。 |
| `ScriptValueConverter` | CLR/JSON 输入转脚本值，以及对外审计投影。 |
| `ScriptValueTypeConverter` | 在下游 .NET 参数边界，按目标 CLR 类型转换脚本值。 |

`ExecuteAsync` 先校验输入和取消状态，为本次调用创建 `ScriptEngine` 并注册 `sereinflow` 模块，然后尝试加载匹配的缓存。缓存不可用时直接编译源码。创建任务后，逐个注册输入名称并注入脚本值，运行任务，最后按输出契约映射返回值。

Worker 在 `src/SereinFlow.Worker.Runner/Program.cs` 中把脚本执行器加入 `NodeExecutorRegistry`。MCP 通过 `SereinFlowMcpRegistration` 注册诊断编译器。更换执行器需接入 Worker 的注册位置；仅在 ASP.NET Core 容器中注册执行器不会替换 Worker 内部的实例。

## 输入输出契约

`ScriptNodeDefinition.Create` 保存 `nodeId`、`source`、`languageVersion` 和输入输出 `ScriptValueContract`。契约包含 `Name`、`ValueKind`、`Required`、可选的 `Id`、`Description`。

- 请求中的输入键与结果中的输出键使用 `Id ?? Name`；脚本读取输入使用 `Name`。
- 必需输入校验检查键是否存在，null 仍属于已提供的值；缺失的可选输入注入 `Value.Null`。
- 一个输出接收完整的 `Value`；多个输出要求 `ObjectValue`，按各输出的 `Name` 读取属性，缺失属性当前映射为 CLR null。
- 执行器不按 `ValueKind` 强制转换返回值。传给反射调用的类库方法时，由 `ScriptValueTypeConverter` 按目标参数类型转换。

以下完整示例可以放入引用 `SereinFlow.ScriptAdapter` 的 .NET 10 控制台项目中：

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
var variables = new LibraryVariableService(); // 同一运行的所有调用复用此实例
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

这个示例直接调用适配器，适合本地开发验证。正式宿主通过 Worker 执行流程。收集日志时为 `NodeExecutionRuntime` 提供 `INodeExecutionEventSink`，响应宿主取消时传入取消令牌及取消探针。

## 诊断编译

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

诊断编译不创建执行任务、不持久化 `.ssc`，也不执行导入模块。成功只说明源码可以编译，不能证明运行时模块存在或输入值合法。返回值包括源码 SHA-256、语言版本、源名称、耗时与诊断；能解析位置信息时包含行列。

当前接受版本 `0.1`，源码上限为 256 KiB（UTF-8），输入上限为 128 个，编译等待超时为 10 秒。底层编译为同步方法；响应超时或取消后，已开始的后台编译仍可能继续。上述限制属于诊断编译入口，直接执行器并不重复这一整套校验。

## 字节码缓存

`ScriptArtifactStore.RebuildProject(projectId, definitions)` 生成每个节点的 `{nodeId}.ssc` 和项目清单；Worker 配置缓存根目录时，会在运行开始阶段调用它。通常的重建先清理旧项目缓存，编译到暂存目录，全部成功后再发布。

`TryLoad` 按 `ArtifactFingerprint` 校验缓存。指纹包含源码、语言版本、输入 ID/名称/类型/必需标记及 `ScriptNodeDefinition.CompilerCompatibilityVersion`。只改输入契约也会使旧缓存失效；输出契约不参与指纹，输出在执行结束时映射。缓存缺失、损坏或指纹不符时，执行器回退到源码编译。

修改编译器全局变量约定或影响旧字节码兼容性的行为时，应更新 `CompilerCompatibilityVersion`。即使源码暂未引用某个输入，加载缓存后也必须注册全部声明输入，避免给合法但未使用的连接器赋值时失败。

## 值转换与错误分支

流程内部保留 `ScriptLang.Runtime.Value`。`ScriptValueTypeConverter.Convert(value, targetType)` 支持标量、日期时间、时间间隔、数组/列表、字典及可映射的对象；CLR 对象句柄在目标类型匹配时保留引用。字符串仅转字符串，布尔值仅转布尔值，null 不能传给不可空的值类型。

`ScriptValueConverter.ToAuditValue` 用于 Worker 事件、持久化与客户端展示；CLR 对象、方法和函数生成摘要。兼容方法 `ToClrValue` 也是审计投影，不能用它替代内部值传递或运行变量存储。

正常返回结果走 `Success`。输入校验、脚本异常和不支持的值走 `Error`；取消返回 `Failure`，错误码为 `script.cancelled`。当前没有通过脚本返回对象自动选择业务 Failure 分支的约定。

## 修改后怎样验证

在仓库根目录运行现有适配器测试，覆盖输入 ID、缓存、多个输出、取消、日志、环境和变量转换：

```powershell
dotnet test tests/SereinFlow.ScriptAdapter.Tests/SereinFlow.ScriptAdapter.Tests.csproj
```

增加能力时应同时检查源码直接执行与 `.ssc` 执行；改动值转换时检查下游 .NET 参数与对外投影；改动模块时检查[诊断入口与运行入口](/script/script-modules#让流程节点使用新模块)。

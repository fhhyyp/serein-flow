# 二次开发：ScriptModules

`src/SereinFlow.ScriptModules` 把 C# 对象的方法与属性暴露给脚本。内置 `sereinflow` 模块导出 `log`、`env`、`variables`，通过 `SereinFlowScriptModuleRegistration.Register(engine, context)` 为每次脚本调用注册。

## 原型与模块导出是两步

1. **注册原型**：`engine.PrototypeManager.Register(instance)` 告诉引擎某类脚本值有哪些方法和属性。
2. **注册模块**：`engine.ImportResolver.RegisterBuiltinModule(name, exports)` 决定 `import { member } from "name"` 能取得什么。

导出对象使用 `ObjectValue`，C# 实例包装为 `ClrObjectValue`。只把对象放入导出表不会自动暴露任意 C# 成员，需要用原型明确声明。

## 给脚本增加一个方法和属性

在 `SereinFlow.ScriptModules` 项目中新增以下文件。项目已引用 ScriptLang 运行时，并将 `ScriptLang.Generator` 作为 Analyzer；新建独立模块项目时也需要这两项引用。

```csharp
using ScriptLang;
using ScriptLang.Runtime;

namespace SereinFlow.ScriptModules
{

    [PrototypeExtension(PushThis = true, NamingFormat = NamingFormat.Js)]
    internal sealed partial class ExampleTextModule
    {
        private readonly string _tag;
        public ExampleTextModule(string tag) => _tag = tag;

        public partial bool IsTarget(Value value)
            => value is ClrObjectValue clr && clr.Value is ExampleTextModule;

        [PrototypeFunction]
        public static StringValue AddTag(ExampleTextModule target, StringValue text)
            => StringValue.Create(target._tag + ":" + text.Value);

        [PrototypeProperty]
        public static StringValue Tag(ExampleTextModule target)
            => StringValue.Create(target._tag);
    }

    public static class ExampleModuleRegistration
    {
        public static void Register(ScriptEngine engine)
        {
            var text = new ExampleTextModule("batch");
            engine.PrototypeManager.Register(text);
            engine.ImportResolver.RegisterBuiltinModule(
                "example",
                new ObjectValue(new Dictionary<string, Value>(StringComparer.Ordinal)
                {
                    ["text"] = new ClrObjectValue(text)
                }));
        }
    }
}
```

`partial` 类与 `partial IsTarget` 由原型生成器补齐接口实现。`IsTarget` 限定该原型服务的对象类型；`PushThis = true` 把脚本接收者作为静态方法的第一个参数传入。`NamingFormat.Js` 将 `AddTag`、`Tag` 映射为脚本中的 `addTag`、`tag`。

先注册，再创建并运行任务：

```csharp
using SereinFlow.ScriptModules;
using ScriptLang;
using ScriptLang.Runtime;

var engine = new ScriptEngine();
ExampleModuleRegistration.Register(engine);
var chunk = engine.CompileSource("""
    import { text } from "example"
    return text.addTag(text.tag)
    """, "example.serein");
var result = await engine.CreateTask(chunk).RunAsync(CancellationToken.None);
Console.WriteLine(((StringValue)result).Value); // batch:batch
```

新增项目的引用形式可参考现有项目：

```xml
<ItemGroup>
  <ProjectReference Include="..\ThirdParty\SereinScript\ScriptLang\ScriptLang.csproj" />
  <ProjectReference Include="..\ThirdParty\SereinScript\ScriptLang.Generator\ScriptLang.Generator.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

路径按新项目位置调整。当前原型生成器要求使用示例中的块作用域命名空间（`namespace ... { }`）。生成器在构建时生成原型实现；若出现未实现的 partial 方法或接口，先检查命名空间形式、Analyzer 引用、`partial` 声明和标记。

## 让流程节点使用新模块

上面的独立示例只注册到它自己的引擎。让画布脚本也能导入 `example`，可以在 `SereinFlowScriptModuleRegistration.Register` 中调用 `ExampleModuleRegistration.Register(engine)`，再重新构建使用这些项目的宿主与 Worker。

当前 `SereinScriptNodeExecutor.ExecuteAsync` 与 `SereinLangCompiler` 的诊断编译都会调用该注册入口。每次执行都会创建新的引擎，加载 `.ssc` 后仍需注册模块；字节码并不保存 C# 模块实例。

`ScriptArtifactStore.RebuildProject` 使用自己的引擎。当前 `import` 被编译成运行时导入指令，不会在重建阶段执行或校验模块导出。若扩展涉及额外全局变量或编译期约定，应同时调整诊断编译、缓存重建和执行注入，并检查是否需要更新缓存兼容版本。

上传 `[FlowNode]` 或 `[FlowService]` 类库不是脚本模块注册入口。新增可导入能力需要接入引擎注册；宿主 DI 服务不会自动传入 Worker 中的脚本模块。

## 运行上下文与值转换

`SereinFlowScriptModuleContext` 接收本次调用的 `NodeExecutionRuntime` 与 `CancellationToken`。日志与环境模块实例按调用创建；变量模块则通过该上下文访问同一运行共用的 `LibraryVariableService`。

| 能力 | 扩展时应遵循的约定 |
| --- | --- |
| 日志 | 使用 `PublishLogAsync` 或运行事件出口，让运行时关联运行与节点身份。 |
| 环境 | 从 `Environment` 读取调用信息，按需查询取消状态。 |
| 变量 | 通过运行级变量表共享值；自定义宿主必须提供 `NodeExecutionRuntime.Variables`。 |
| 异步调用 | 原型可返回 `Task<Value>` 等受支持的任务类型，参考内置 `log`；宿主操作应响应取消令牌。 |
| 转换 | 使用 `SereinFlowScriptValueConverter.ToScriptValue` 与 `ToVariableValue` 处理变量值。 |

诊断注册没有活动运行，此时 `Environment` 为 null；内置环境属性返回空字符串或默认值，实际变量操作会因缺少运行变量表而失败。注册阶段应只声明能力，等脚本调用方法时再访问运行资源。

数组转换为 `object?[]`，对象转换为 `Dictionary<string, object?>`，容器在转换时复制；CLR 句柄保留引用。存储不支持函数、方法、循环引用，最多嵌套 32 层，每个容器最多 10,000 项。不要用审计摘要保存变量，也不要在静态模块实例上存放某次运行的上下文。

新增导出时保持上下文范围明确，避免通过公开属性直接暴露整个流程会话或宿主配置。对照内置模块，验证实际调用、缓存执行、无活动运行的诊断注册、取消与变量隔离；现有验证命令见[ScriptAdapter](/script/script-adapter#修改后怎样验证)。

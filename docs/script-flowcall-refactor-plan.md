# Script、FlowCall 与可变参数重构执行方案

**状态：** 待实施  
**编制日期：** 2026-08-27  
**适用范围：** `SereinFlow.Domain`、`Contracts`、`Application`、`Runtime`、`ScriptAdapter`、`Worker.Runner`、`Infrastructure`、Vue 前端及自动化测试。

## 1. 结论与边界

当前项目可以实现本方案全部需求。已有运行队列、Worker 隔离、流程快照、数据连接、`Success` / `Failure` / `Error` 分支、脚本缓存、同流程 `FlowCall` 静态环检测以及单个 Worker Run 的类库缓存，可作为基础复用。

但以下能力目前只是部分存在，不能视为完成：

| 需求 | 当前状态 | 结论 |
| --- | --- | --- |
| 移除 `Condition` | 领域枚举、DTO、前端节点目录和 Worker 注册仍包含该类型 | 可移除；属于 Schema 破坏性变更 |
| Script 节点 | 领域模型和 Worker 执行器已存在 | 缺少基础节点面板、脚本编辑器、输入行编辑、稳定参数 ID、原始 `Value` 保留及目标类型转换 |
| FlowCall 节点 | 已支持同流程目标节点、调用图环检测和静态返回类型分析 | 缺少公开节点、画布/节点选择器、调用参数契约与隔离子上下文 |
| ScriptLang 来源 | 通过仓库内 `src/ThirdParty/SereinScript` 的相对 `ProjectReference` 引用 | 需要记录并验证随仓库交付的源码修订与生成器输入 |
| `IFlowContext` 注入 | 只有内部 `IExecutionContext`；反射调用把每个 CLR 参数当作普通节点输入 | 需要新增受限的公开契约和 Worker 注入逻辑 |
| 可变参数 | 没有 `ParamArrayAttribute` 扫描结果、DTO、编辑器状态或运行时打包 | 需要从扫描、持久化、编辑器到执行器完整打通 |

本方案遵循下列已确认的边界：

- 不兼容旧流程。流程 Schema 升级后，旧 Schema 和已移除的节点类型均拒绝保存或运行。
- 普通执行连接图继续要求静态 DAG；只存在 `Success`、`Failure`、`Error` 三种分支。
- Script 内部异常统一进入 `Error` 分支。
- FlowCall 第一阶段只调用**当前流程快照**中的公开节点；可跨当前流程的不同画布，不支持跨 Flow 调用。
- 全局 Flipflop 的重复监听不构成静态执行环，运行时仍受最大步数和单节点访问次数限制。

## 2. 当前实现依据

### 2.1 节点与运行时

- `NodeType` 和 `NodeTypeDto` 目前为 `Action`、`Flipflop`、`Script`、`Condition`、`FlowCall` 五项，且使用隐式枚举值。
- `ConditionNodeExecutor` 仍在 `SereinFlow.Runtime`，Worker Runner 仍会注册它。
- `FlowRunner` 已按连接的 `ExecutionBranch` 调度 `Success`、`Failure`、`Error`；不存在 `Upstream` 调度。
- `FlowRunner` 已在每个节点开始、完成、失败和错误时记录输入、输出事件。
- `FlowExecutionSession.CreateChild()` 当前会复制全部节点值；`FlowCall` 当前甚至直接复用调用方 Session。两者都会让被调流程看到或覆盖调用方节点数据，不能满足上下文隔离要求。

### 2.2 Script

- `ScriptNodeDefinition` 已有源代码、语言版本、输入和输出列表，输入数量在存储形态上可变；但输入项没有稳定 ID 和备注。
- `SereinScriptNodeExecutor` 已编译/加载 `.ssc`、注入全局变量、捕获脚本异常进入 `Error`。
- 执行器当前通过 `ScriptValueConverter.ToClrValue` 立即把结果转换为 JSON 兼容 CLR 值；`ClrObjectValue`、`ClrMethodValue` 与委托会被拒绝。
- `ScriptArtifactStore` 的缓存指纹只使用脚本源代码哈希。仅修改输入名称或数量时可能复用由旧全局变量集合编译出的 `.ssc`，这是必须修复的正确性问题。
- 目前 `SereinFlow.ScriptAdapter.csproj` 只引用 NuGet 包。指定源码仓库 `D:\Project\C#\SereinScript\SereinScript\ScriptLang` 存在，审计时 HEAD 为 `37d8429b93eb8ab1c9c60b3390269fba32158344`，但该关联没有被当前构建验证。

### 2.3 编辑器与参数

- `NodeLibraryPanel.vue` 仅显示项目类库的扫描结果，项目没有类库时会显示为空；基础节点不会出现。
- `useNodeDrop.ts` 的拖拽机制可复用，但其创建载荷目前限定为 `LibraryNodeDto`。
- `useFlowGraph.ts` 已能够以任意 `NodeKind` 创建节点，但默认参数模型不适合 Script 或可变参数。
- `InspectorPanel.vue` 仅支持一般名称、描述和参数来源/值；没有脚本编辑、输入管理、公开节点开关、FlowCall 目标选择或 `params` 模式选择。
- 数据连接目标端口已经使用参数 ID，运行时 `DataConnectionResolver` 却主要以参数名称传递输入。后续重命名 Script 输入时必须以参数 ID 为运行时绑定主键，名称仅作为显示名、脚本变量名和反射方法名。

### 2.4 FlowCall、类库与上下文

- `ExecutionPlanBuilder` 已验证 `TargetNodeId` 存在、第一阶段目标 Flow 为当前 Flow，并检测直接或间接 FlowCall 环。
- `FlowCallReturnTypeAnalyzer` 已能对目标节点及后续路径做保守静态返回类型分析。
- 当前没有 `IsPublic` 字段，也没有画布与公开节点选择器。
- `LibraryNodeExecutor` 已在单个 Worker Run 内复用程序集、类型和方法元数据，但对 `IFlowContext` 没有特殊参数注入。
- `LibraryCatalogService` 使用 PE Metadata 扫描，不会在 API 进程加载 DLL；它尚不识别 `ParamArrayAttribute`。

## 3. 核心架构决策

### 3.1 节点模型与 Schema v5

新的运行时节点类型严格为：

```text
Action
Flipflop
Script
FlowCall
```

`Condition` 完全移除。为防止旧的数值 `Condition` 被错误解释为 `FlowCall`，两个枚举均改用显式稳定值：保留既有 `Action = 0`、`Flipflop = 1`、`Script = 2`、`FlowCall = 4`，不复用值 `3`。

将 `FlowDefinition.CurrentSchemaVersion` 从 `4` 升为 `5`。API 的节点类型 JSON 转换器要把字符串 `"Condition"` 和数值 `3` 还原为一个未定义枚举值，由领域校验返回：

```text
node.type_removed
Node type has been removed and cannot execute. 节点类型已移除，不能执行。
```

不能把它静默映射到任意剩余节点类型。由于不存在兼容承诺，旧 Schema 也统一返回 `node.type_removed` / Schema 不受支持诊断。

### 3.2 统一参数身份

参数 ID 作为持久化、连接、运行时解析和审计的唯一主键；名称不再承担身份职责。

```text
NodeParameterDefinition
  Id                  stable identifier
  Name                display name / Script global name / reflected parameter name
  Description         parameter remark
  ValueKind           declared display and conversion hint
  Source + source configuration
  Required
  Variadic metadata   only for C# params groups
```

`DataConnectionResolver` 返回以参数 ID 为键的解析结果。节点执行器依据本节点参数定义映射到脚本变量名或 CLR 方法参数名。运行事件保存 `{ id, name, value }` 形式的可读审计投影，避免名称重命名破坏关联，也避免审计失去可读性。

### 3.3 Script 值采用双层表示

Script 输出**不能**在 Worker 内部立即丢弃原始 `ScriptLang.Runtime.Value`。采用下列边界：

```text
同一 Worker Run 的流程内存
  保留 Runtime Value
  - 后续 DLL 参数按目标 CLR 类型延迟转换
  - target type 为 Value 时可原样注入
  - ClrObjectValue / ClrMethodValue 保留其进程内语义

Worker 协议、SQLite、日志、SignalR、SSE、API、前端
  只写入 Serializable/Audit Value
  - JSON/CLR 安全扁平化投影
  - 非可序列化 CLR 值写入受控摘要，绝不传出对象引用
```

这样既能兼容前端展示、审计和跨进程通信，也能满足脚本到 DLL 的 `Value`、`MethodInfo` 和 CLR 可赋值对象场景。

### 3.4 FlowCall 使用隔离调用帧

FlowCall 执行时：

```text
调用方数据连接解析 FlowCall 自身参数
  -> 创建共享 RunId、取消令牌、事件序列和执行预算的子调用帧
  -> 仅复制 project.* 项目输入
  -> 注入明确的 FlowCall 参数映射
  -> 从公开 TargetNodeId 运行目标节点及其后继
  -> 仅将调用终止结果作为 FlowCall 输出返回调用方
```

子调用帧不得复制调用方任意 `nodeId.portId` 输出。目标流程内的节点值只能存在于该帧中，因此多个 FlowCall、普通入口流和全局 Flipflop 触发实例不会相互覆盖。

### 3.5 受限 `IFlowContext`

不复制旧版暴露环境、任意数据和 `NextOrientation` 可写属性的宽泛接口。通过独立的 `SereinFlow.Library` SDK 提供供类库作者引用的 `SereinFlow.Runtime.Abstractions.IFlowContext`：

```csharp
public interface IFlowContext
{
    Guid RunId { get; }
    string NodeId { get; }
    CancellationToken CancellationToken { get; }
    void SelectSuccess();
    void SelectFailure(string? code = null, string? message = null);
    void SelectError(string? code = null, string? message = null);
}
```

它按**一次节点调用**创建。反射发现该参数时自动注入，不出现在节点参数面板或数据连接器中。方法正常返回后，执行器读取分支请求并覆盖默认 `Success`；方法抛出异常时仍固定进入 `Error`，不会被事先设置的成功/失败请求掩盖。

## 4. 分阶段执行计划

### Phase 1：移除 Condition 并建立 Schema v5

1. 修改 `SereinFlow.Domain.NodeType`、`SereinFlow.Contracts.NodeTypeDto`、前端 `ApiNodeType`、`NodeKind` 和节点目录为四种类型，并使用显式枚举值。
2. 升级 Schema 至 v5；添加可识别已删除类型的 JSON 转换/验证路径，明确返回 `node.type_removed` 双语诊断。
3. 删除 `ConditionNodeExecutor`、Worker 注册、前端图标、i18n、拖拽入口、默认工厂与测试样例。
4. 更新流程保存、运行前校验和 Worker 映射，保证 `condition` 无法创建、持久化或执行。

### Phase 2：完善跨层参数契约

1. 扩展 Domain、Contracts、DTO、`flowDtoMapper.ts` 和前端 `MethodParameter`，引入 `Description`、稳定参数 ID 和可变参数元数据。
2. 将 `DataConnectionResolver`、`LibraryNodeExecutor`、Script 执行器和事件审计改为 ID 驱动的参数解析；连接始终使用 `Connection.ToPortId = Parameter.Id`。
3. 保存时验证参数 ID 的唯一性、Script 全局变量名合法且唯一、数据连接端口存在，以及同一 FlowCall 参数映射的完整性。
4. 统一参数追溯记录：每个节点事件保留实际解析前/转换后的安全输入值、来源和错误代码。

### Phase 3：建立 ScriptLang 制品溯源

1. 保持生产项目使用仓库内可移植的相对 `ProjectReference`，不加入开发机绝对路径。
2. 新增构建脚本或 CI 任务，记录 `src/ThirdParty/SereinScript` 的固定修订、生成器输入和 SHA-256 provenance；不再生成仓库本地 NuGet 源。
3. 构建/CI 校验源码修订、生成器输入和 provenance；不匹配即失败。
4. Linux 部署使用随仓库交付并已验证的源码工程，不依赖开发机盘符或额外的本地包源。更新依赖时必须同时更新固定修订和验证记录。

### Phase 4：重构 Script 定义、缓存与编辑器

1. 以 `Node.Parameters` 作为 Script 输入的唯一绑定真源，删除输入来源和值在 `ScriptValueContract` 与节点参数之间的重复语义。
2. Script 定义保留脚本源代码、语言版本和固定输出端口 `result`；该端口的运行时类型声明为 `ScriptLang.Runtime.Value`，前端显示为 `Value` / `脚本值`，不伪造一个固定 CLR 返回类型。
3. 脚本检查器提供源代码编辑器，以及输入行的新增、删除、重命名、必填、类型提示和备注编辑。重命名只改名称，不改参数 ID，因此既有数据连接不会失效。
4. 新建 `Script` 节点模板由服务端基础节点目录下发，包含空参数集合、固定 `result` 输出和最小合法脚本模板；Vue 不本地预设节点实例。
5. 将缓存标识从仅 `SourceHash` 改为 `ArtifactFingerprint`。指纹至少包含：源代码、语言版本、编译器兼容版本、按顺序的输入 ID/名称/类型/必填信息。变更任一编译相关输入后必须重建 `.ssc`。
6. `ScriptArtifactStore` 的 manifest、`TryLoad` 与项目重建流程全部使用该指纹；保留“项目加载时清理并重建缓存”的现有安全行为。

### Phase 5：Script Runtime Value 与目标类型转换

1. 将现有 `ScriptValueConverter` 分为：
   - `ToScriptValue`：将流程输入转换为脚本值；补全 `DateTime`、`TimeSpan` 和 Worker 内可用 CLR 包装支持。
   - `ToAuditValue`：用于协议、事件和持久化的安全扁平化投影。
   - `ScriptValueTypeConverter.Convert(Value value, Type targetType)`：仅供 Worker 内 DLL 参数调用。
2. Script 执行器把原始 `Value` 写入 Session 的节点输出；`FlowRunner` 与 `WorkerEventPublisher` 通过统一审计投影器序列化输入和输出，绝不直接 JSON 序列化原始 Script 值。
3. 在 `LibraryArgumentConverter` 常规 CLR/JSON 转换之前识别 `ScriptLang.Runtime.Value` 并委托 `ScriptValueTypeConverter`。
4. 实现并以结构化双语错误 `script.value_conversion_failed` 覆盖下列规则：

| Script Value | 目标类型处理 |
| --- | --- |
| `StringValue` | `string` |
| `NumberValue<T>` | 对目标数值类型执行受控、检查溢出的转换 |
| `ArrayValue` | `T[]`、`List<T>` 和兼容集合接口；逐项递归转换 |
| `ObjectValue` | `Dictionary<string, T>` 等兼容字典；目标为普通 CLR DTO 时仅对公开可写属性递归绑定 |
| `DateTimeValue` | `DateTime` |
| `TimeSpanValue` | `TimeSpan` |
| `BoolValue` | `bool` |
| `ClrObjectValue` | 仅当包装对象对目标 CLR 类型可赋值时通过；禁止以反射属性绑定作为回退 |
| `ClrMethodValue` | `MethodInfo` |
| `Value` / 明确 `Value` 子类型 | `Value` 时原样传入；具体子类型时验证或构造可兼容值，失败即抛出结构化错误 |

5. `ClrObjectValue`、`ClrMethodValue`、委托、程序集和反射对象的审计值改为安全摘要（例如 runtime kind、CLR 类型名、可序列化标志），禁止穿透 Worker 协议、数据库、SignalR、SSE 和前端状态。

### Phase 6：服务端基础节点目录与前端节点库

1. 在 Contracts/Application 增加服务端拥有的 `BuiltinNodeCatalogDto` / `NodeCreationDescriptorDto`，首批仅下发 `Script` 和 `FlowCall`。
2. 增加读取基础目录的 API；其响应只包含可创建的节点描述和服务器模板版本，不包含任何前端本地演示节点。
3. 将 `NodeLibraryPanel.vue` 改为固定的“基础节点 / Basic nodes”区段加项目类库区段。即使未上传类库，基础节点仍可拖拽创建。
4. 将 `useNodeDrop.ts` 由 `LibraryNodeDto` 专用载荷重构为通用 `NodeCreationDescriptor`；保留现有 Pointer Event 拖拽流程和画布坐标换算。
5. 保持现有 Minimalism / Swiss Style：信息密度高、清晰分隔、14px 基准正文、稳定图标和颜色语义。基础节点以简洁的类型标记区分，不增加营销式卡片或默认流程内容。

### Phase 7：公开节点与 FlowCall 编辑体验

1. 在节点定义/运行时元数据中持久化执行显著的 `IsPublic`；为 UI 辅助和后端交叉验证增加 `TargetCanvasId`，`TargetNodeId` 仍是真正调用身份。
2. 选择某节点为公开节点时，检查器提供开关；普通 Action、Flipflop、Script、FlowCall 均可被标记，后端仍负责检测调用环。
3. FlowCall 检查器按当前编辑内存中的流程定义生成两级选择器：先选画布，再选该画布的公开节点。这样未保存的画布/公开状态也可立即参与编辑；保存与运行时进行服务器最终校验。
4. 选择目标后，把目标入口的公开参数镜像为 FlowCall 自身输入，建立 `CallParameterId -> TargetParameterId` 映射。调用方可以为这些参数使用字面量、项目输入、表达式或数据连接。
5. 目标切换时显示将被移除的无效映射，并要求确认；不匹配的连接被安全移除，避免悬空连接端口。
6. `ExecutionPlanBuilder` 额外验证：目标节点存在、目标画布匹配、目标节点是公开节点、目标 Flow 为空或等于当前 Flow、调用图无环。静态返回类型分析继续复用，但对 Script 终端结果记为动态 `ScriptLang.Runtime.Value` / `System.Object`。

### Phase 8：FlowCall 隔离执行

1. 为 `FlowExecutionSession` 增加明确的 `CreateFlowCallFrame`，共享 RunId、取消、资源策略、序列和步数/访问预算，但不复制一般节点输出。
2. 子帧只注入项目输入和经调用方数据连接解析的 FlowCall 参数；`DataConnectionResolver` 支持在目标入口节点消费该调用帧参数。
3. `FlowRunner.ExecuteFlowCallAsync` 改为在子帧中运行目标节点及后继。子帧的状态、端口值和错误分支不回写调用方；只把最终输出作为 FlowCall 节点输出写回。
4. 运行事件增加调用帧/调用深度标识，使审计界面能说明节点运行来自主流程还是某个 FlowCall，但不将子帧原始 Script 值写出。

### Phase 9：`IFlowContext` 反射注入

1. 在独立的 `SereinFlow.Library` SDK 中定义并打包公开 `IFlowContext`；`SereinFlow.Runtime.Abstractions` 通过类型转发保持旧类库二进制兼容，更新测试类库和类库作者文档的引用方式。
2. PE Metadata 扫描识别此精确全名的参数，不把它生成普通 `LibraryParameterDto` 或前端连接器。
3. `WorkerLibraryRuntimeCache` 的可收集 `AssemblyLoadContext` 必须回退到默认上下文中的 Abstractions 程序集，保证 DLL 看到的接口类型与 Worker 注入实例类型一致。
4. `LibraryNodeExecutor` 为每次调用创建上下文、注入它、await 方法完成，再安全采纳分支请求。捕获到 CLR 异常时保持 `Error` 优先。

### Phase 10：C# `params` 可变参数

1. `LibraryCatalogService` 通过 PE Metadata 识别 `System.ParamArrayAttribute`，并在 `LibraryParameterDto` 中下发 `IsVariadic`、`VariadicGroupId`、`ElementType` 和集合目标类型。
2. Node/DTO/前端参数模型保留两种可变参数展示模式：
   - **展开模式：** 每个元素有独立稳定参数 ID、数据连接器和删除按钮；通过加号新增行。
   - **集合模式：** 一个参数入口和一个连接器，接收 `T[]`、`List<T>`、`IEnumerable<T>` 或单个可转换元素。
3. 模式切换使用分段控件；新增/删除使用图标按钮并提供 tooltip。切换到集合模式时将展开项转换为一个 JSON 数组或保留数据连接为唯一集合连接；无法无损转换时在检查器中显示诊断，不静默丢值。
4. `LibraryNodeExecutor` 不再依赖“节点参数数量等于 CLR 方法参数数量”。它按反射参数位置构建参数数组，把同一 `VariadicGroupId` 的展开元素逐项转换并打包为真实 `T[]`；零项传入空数组。
5. 普通 `List<T>`/数组参数不自动被当作可变参数，只有 CLR `params T[]` 才出现展开模式；普通集合参数仍可在集合模式下使用统一转换器传值。
6. Script 的“可变输入”仅指用户可新增/删除的命名输入，并不冒充 C# `params`；其逐项输入连接器始终与 Script 全局变量一一对应。

## 5. 前端交互与视觉约束

基础节点、Script 输入、FlowCall 目标和 `params` 编辑均置于现有三栏编辑器，不新增浮动页面级卡片。

```text
左侧节点库
  基础节点：Script、FlowCall
  项目类库：Action、Flipflop

中间画布
  与 DLL 节点一致的拖拽、流程端口、数据端口和连接线行为

右侧检查器
  Script：源代码 -> 输入列表 -> 通用信息
  FlowCall：目标画布 -> 公开节点 -> 调用参数 -> 通用信息
  params：展开/集合分段控件 -> 参数编辑
```

- 不在 Vue 中保留默认项目、演示节点或硬编码基础节点实例；创建描述来自 API。
- Script 输入名称改变后调用 Vue Flow 的节点内部更新机制，保证数据连接器位置和绑定 ID 同步。
- 所有图标使用现有 Lucide 图标；新增、删除、切换和公开状态均有键盘焦点与文字 tooltip。
- 继续使用当前蓝、绿、橙、红作为状态/分支语义；节点类型仅用低饱和度标记，不以颜色作为唯一信息来源。
- 动态输入行、目标选择器和代码编辑区域必须在窄屏下纵向堆叠，检查器自身保持独立滚动，不能被运行输出面板遮挡。

## 6. 文件级改动清单

| 范围 | 主要文件/新增文件 | 改动 |
| --- | --- | --- |
| Domain | `FlowEnums.cs`、`FlowDefinition.cs`、`NodeDefinition.cs`、`ScriptNodeDefinition.cs` | Schema v5、四种节点、参数/公开节点/FlowCall/Script 定义 |
| Contracts | `Dtos.cs`、节点类型 JSON 转换器、新基础目录 DTO | 契约、参数元数据、基础节点目录、已删除类型诊断 |
| Application | `FlowDefinitionValidation.cs`、目录服务 | 规范化、输入/公开目标/FlowCall 约束校验 |
| Runtime | `FlowRunner.cs`、`FlowExecutionSession.cs`、`DataConnectionResolver.cs`、`BuiltInNodeExecutors.cs` | 删除 Condition、调用帧、ID 参数解析、安全事件投影 |
| Library SDK / Runtime.Abstractions | `SereinFlow.Library/FlowContext.cs`、类型转发文件、`RuntimeContracts.cs` | 独立 NuGet 中的受限 `IFlowContext`；运行时兼容转发与节点调用分支意图 |
| ScriptAdapter | `SereinScriptNodeExecutor.cs`、`ScriptValueConverter.cs`、新 `ScriptValueTypeConverter.cs`、`ScriptArtifactStore.cs` | 原始 Value、审计投影、十类转换、缓存指纹 |
| Worker Runner | `Program.cs`、`LibraryNodeExecutor.cs`、`LibraryArgumentConverter.cs`、`WorkerLibraryRuntimeCache.cs` | Executor 注册、参数注入、params 打包、ALC 契约共享 |
| Infrastructure | `LibraryCatalogService.cs` | `ParamArrayAttribute` 扫描与目录元数据 |
| Frontend | `NodeLibraryPanel.vue`、`InspectorPanel.vue`、`ParameterEditor.vue`、`useNodeDrop.ts`、`useFlowGraph.ts`、`types.ts`、`flowDtoMapper.ts`、i18n | 基础节点、Script/FlowCall 检查器、可变参数、DTO 映射 |
| Build | `Directory.Build.props`、构建脚本、`src/ThirdParty/SereinScript` 的 provenance | ScriptLang 来源验证与可部署源码工程 |
| Tests | Domain/Application/Runtime/ScriptAdapter/Infrastructure/Worker/Frontend tests、`SereinFlow.TestLibrary` | 下文验收覆盖 |

## 7. 验收与测试矩阵

### 节点和迁移

- `NodeType` 和前端节点目录只含四种节点。
- `Condition` 的字符串或原数值输入返回 `node.type_removed`，绝不映射为 `FlowCall`。
- Schema v4 与 v5 前的流程拒绝保存/运行；新空白项目和新四类节点可正常保存。

### Script

- 基础节点面板无需 DLL 即可拖拽创建 Script。
- Script 输入可以新增、删除、改名、改备注和连数据线；改名后连接 ID 不变。
- 输入契约发生变化会让缓存失效并重新编译。
- 十种 `Value -> CLR` 规则分别覆盖成功、失败、空值、集合元素失败、数值溢出、DTO 绑定失败和安全摘要。
- `Value`、`ClrObjectValue`、`ClrMethodValue` 在同 Worker Run 内可用于支持的 DLL 参数；事件、快照、SSE、SignalR 和 SQLite 中只出现安全 JSON 值或摘要。
- 脚本异常进入 `Error` 分支。

### FlowCall

- 可选择当前 Flow 的任意画布和公开节点；非公开、缺失、画布不匹配和跨 Flow 目标被拒绝。
- 调用入口参数能接收字面量、项目输入、表达式和数据连接。
- FlowCall 子流程输出不会覆盖调用方节点输出；嵌套调用也不泄露兄弟调用数据。
- 目标节点及后继流程正确执行；返回类型分析保留同类型、动态类型、无返回和调用环的既有覆盖。

### `IFlowContext` 与可变参数

- DLL 的 `IFlowContext` 参数不显示在编辑器，Worker 能正确注入同一契约程序集的实例。
- `SelectSuccess`、`SelectFailure`、`SelectError` 覆盖正常返回的默认分支；CLR 异常始终走 `Error`。
- 上传扫描能识别 `params int[]`；展开模式的新增/删除、连接器、顺序和空数组均正确。
- 集合模式接受数组、`List<T>`、`IEnumerable<T>` 和单值；无法转换时返回带中英文消息的结构化诊断。

### 回归

- Action、Flipflop、数据连接、Worker 协议、运行事件、FlowRun 快照、SignalR/SSE 和受控队列测试全部通过。
- Worker stdout 仅保留协议帧；所有诊断继续进入 stderr/结构化运行事件。
- 前端测试覆盖基础节点拖拽、Script 输入端口位置、FlowCall 目标筛选、可变参数模式切换和窄屏检查器滚动。

## 8. 实施顺序与提交边界

建议按 Phase 1 -> 2 -> 3 -> 4/5 -> 6 -> 7/8 -> 9 -> 10 -> 测试回归执行。每个阶段独立构建并运行相关测试后再进入下一阶段；避免把 Schema、Worker 协议和前端交互的失败混在同一个调试面上。

建议将提交拆分为：

1. `refactor: remove condition node and introduce schema v5`
2. `feat: add server-owned Script and FlowCall catalog`
3. `feat: preserve Script runtime values and convert typed inputs`
4. `feat: add public FlowCall targets and isolated invocation frames`
5. `feat: inject flow context and support variadic parameters`
6. `test: cover Script conversion, FlowCall isolation and variadic inputs`

在所有阶段完成前，不变更已运行的队列、项目类库隔离、运行快照不可变和 Worker/API 隔离边界。

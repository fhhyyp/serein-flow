# 类库版本化升级与流程节点兼容实施计划

**状态：** 已实施，待用户验收
**编制日期：** 2026-08-28
**适用范围：** `SereinFlow.Contracts`、`Domain`、`Application`、`Infrastructure`、`Worker.Runner`、ASP.NET Core API、Vue 前端、数据库迁移及自动化测试。
**目标：** 支持环境类库发布新版本、评估其对现有流程节点的影响，并在不破坏既有流程定义、排队运行和历史运行快照的前提下，显式迁移到新版本。

**执行记录（2026-08-28）：** 已完成 Phase 1 至 Phase 4 的契约、工件元数据、兼容性预览、单流程/多流程独立升级、绑定索引与使用影响统计实现。已通过 `SereinFlow.Application.Tests`（22 项）、`SereinFlow.Infrastructure.Tests`（19 项）、Vue 生产构建以及 API/Worker 完整编译；桌面和移动布局已完成本地浏览器联调。旧流程版本、旧工件和运行快照保持不可变，尚待实际业务类库版本发布后的用户验收。

## 1. 结论与边界

当前工程已经具备类库“不可变工件”的关键基础：上传 ZIP 后以 SHA-256 作为 `LibraryId`，包文件按 `packages/{sha256}.zip` 保存；同一项目可以引用多个类库工件；Worker 按流程运行快照中的允许类库 ID 加载程序集；流程执行前还会创建不可变的 `FlowRunDefinitions` 快照。

因此，类库更新不应设计成“覆盖旧 DLL”或“把所有节点的 `LibraryId` 原地改成新版本”。正确的模型是：

```text
发布新版本 ZIP
  -> 生成新的不可变类库工件
  -> 扫描并保存版本兼容性清单
  -> 用户选择源版本、目标版本和受影响流程
  -> 生成只读升级预览
  -> 用户确认映射和风险项
  -> 克隆并创建新的流程定义版本
  -> 新版本流程使用新类库工件
  -> 原流程版本、旧类库工件和运行快照保持不变
```

本计划的核心不变量如下：

1. 已上传的 ZIP、`LibraryId` 和包元数据永不覆盖、永不原地修改。
2. 已保存的流程版本永不原地修改。升级只能创建 `Version + 1` 的流程定义版本。
3. `FlowRunDefinitions` 运行快照永不修改；正在排队、运行中和已结束的 Run 始终按其快照执行和审计。
4. 节点在流程定义中绑定的是明确的 `LibraryArtifactId`，Worker 不得在运行时解析“最新版本”。
5. 未完成兼容性分析、存在阻断项或用户未确认映射时，不允许执行升级。
6. 第一阶段不删除旧 ZIP。旧工件可归档以禁止新引用，但只要仍被流程版本或运行快照引用，就必须可执行、可审计。

本计划不包括以下内容：

- 在运行中的 Worker 内热替换程序集；当前运行完成或终止后才会卸载其可回收 `AssemblyLoadContext`。
- 自动把整个项目所有流程升级到最新版本；项目级操作也必须逐流程产生预览和结果。
- 根据方法名称、参数序号或 `ToString()` 猜测安全映射。
- 首期对旧工件进行物理删除或垃圾回收。

## 2. 当前实现评估

### 2.1 已具备的能力

| 能力 | 当前实现 | 对本方案的作用 |
| --- | --- | --- |
| 不可变类库包 | `LibraryCatalogService` 以 ZIP SHA-256 创建库 ID 并保存包 | 新版本天然是独立工件，不会覆盖旧 DLL |
| 项目引用隔离 | `ProjectLibraryReferences` 按 `ProjectId + LibraryId` 保存引用 | 同一项目可并存旧、新工件，逐流程迁移 |
| 运行时精确绑定 | API 根据快照构造 `AllowedLibraryIds`；Worker 校验并按 ID 加载 | 历史流程和新流程可稳定使用各自版本 |
| 单 Run 程序集缓存 | `WorkerLibraryRuntimeCache` 在同一 Worker Run 内缓存程序集、类型和方法 | 不因本计划改变当前运行性能模型 |
| 流程版本与 Run 快照 | `FlowDefinitionVersions`、`FlowRuns`、`FlowRunDefinitions` | 升级可以创建可追溯的新版本而不影响旧版本 |
| 当前引用保护 | `ProjectLibraryService` 会阻止移除仍被当前流程使用的类库引用 | 可扩展为升级完成后的引用清理保护 |

相关实现主要位于：

- `src/SereinFlow.Infrastructure/Persistence/LibraryCatalogService.cs`
- `src/SereinFlow.Application/ProjectLibraryService.cs`
- `src/SereinFlow.Infrastructure/Persistence/ProjectLibraryReferencePersistence.cs`
- `src/SereinFlow.Infrastructure/Persistence/FlowDefinitionPersistence.cs`
- `src/SereinFlow.Infrastructure/Persistence/FlowRunPersistence.cs`
- `src/SereinFlow.Api/RunExecutionServices.cs`
- `src/SereinFlow.Worker.Runner/WorkerLibraryRuntimeCache.cs`

### 2.2 当前缺口

| 缺口 | 当前风险 | 需要建立的能力 |
| --- | --- | --- |
| 没有逻辑类库族 | SHA 是工件身份，无法表达 `1.5.0 -> 1.6.0` 是同一产品线 | `LibraryFamily` 和工件版本历史 |
| 无升级工作流 | 上传新 ZIP 后只能手工重建或修改节点 | 预览、确认、应用和结果查询 API/UI |
| 节点身份随 SHA 改变 | 现有节点 ID 依赖库 ID、类名和方法名，版本更新即改变 | 稳定节点契约 ID，且完整区分重载 |
| 参数身份只依赖顺序 | `param-1`、`param-2` 在插入或重排参数后可能错误复用数据连接 | 稳定参数契约 ID 和别名映射 |
| 无 ABI/兼容清单 | 无法准确判断参数、返回值、`params`、枚举和 Flipflop 约束的变化 | 每个工件持久化兼容性 Manifest |
| 无使用索引 | 每次分析影响范围需扫描流程 JSON，无法快速展示引用和未来清理影响 | 流程与运行的类库绑定索引 |
| 无用户可见升级预览 | 用户不能确认哪些节点、参数和连线被迁移 | 只读预览和显式映射界面 |

## 3. 术语与数据模型

### 3.1 术语

| 术语 | 含义 |
| --- | --- |
| 类库族（Library Family） | 逻辑上的同一类库产品，例如“生产检测节点库” |
| 类库工件（Library Artifact） | 一个不可变 ZIP 上传结果；`Id` 为 ZIP SHA-256 |
| 语义版本（Semantic Version） | 类库作者声明的版本，例如 `1.6.0`；仅用于展示、排序和升级选择，不替代 SHA 身份 |
| 节点契约 ID | 跨同一类库族稳定的节点身份，例如 `quality.calculate-rate` |
| 参数契约 ID | 在节点契约内稳定的参数身份，例如 `qualified-count` |
| 兼容性 Manifest | 由 PE Metadata 扫描产生的、供版本比较的结构化节点契约快照 |
| 升级计划（Upgrade Plan） | 由源工件、目标工件、项目、流程选择和分析结果组成的持久化操作记录 |
| 升级预览（Upgrade Preview） | 尚未改动流程的只读分析结果，包含映射、风险和阻断诊断 |

### 3.2 推荐表结构

现有 `Libraries` 已经是不可变工件表，第一阶段应在该表补充字段，而不是复制一张内容相同的工件表。

```text
LibraryFamilies
  Id
  Name
  Description
  LatestArtifactId
  CreatedAt
  UpdatedAt

Libraries (existing, one row = one immutable artifact)
  Id                         SHA-256, existing primary key
  FamilyId                   nullable during legacy migration
  SemanticVersion            nullable / normalized string
  CompatibilityManifestJson  new
  Lifecycle                  Active | Archived
  ...existing immutable package/catalog fields

LibraryUpgradePlans
  Id
  ProjectId
  SourceArtifactId
  TargetArtifactId
  Status                     Draft | Analyzed | Applied | Failed | Superseded
  AnalysisJson
  CreatedAt
  AppliedAt

FlowLibraryBindings
  ProjectId
  FlowId
  FlowVersion
  LibraryArtifactId
  CreatedAt

RunLibraryBindings
  RunId
  LibraryArtifactId
  CreatedAt
```

`FlowLibraryBindings` 和 `RunLibraryBindings` 是查询、审计与清理影响分析的索引，不应成为 Worker 的运行时权威。Worker 的唯一权威仍是 `FlowRunDefinitions.DefinitionJson` 所代表的运行快照。

### 3.3 生命周期约束

```text
上传工件 -> Active
Active -> Archived            仅禁止新项目引用或作为默认升级目标
Archived -> Active            允许恢复
任何状态 -> 物理删除          本计划第一阶段不提供
```

归档工件仍须满足：旧流程保存、历史运行审计、排队 Run 与运行中 Run 可以按原工件执行。删除功能应在独立的生命周期计划中处理，并先证明没有任何流程版本、运行快照或保留策略引用它。

## 4. 稳定节点与参数契约

### 4.1 扩展公共特性

在独立的 `src/SereinFlow.Library/LibraryAttributes.cs` SDK 中定义稳定特性，并通过 `SereinFlow.Contracts` 类型转发保持旧类库兼容：

```csharp
public sealed class FlowNodeAttribute : Attribute
{
    public string? Id { get; set; }
    // existing: NodeType, AnotherName, Desc
}

public sealed class NodeParamAttribute : Attribute
{
    public string? Id { get; set; }
    public string[]? Aliases { get; set; }
    // existing: Name, IsExplicit
}
```

类库作者升级方法时，应保持 `FlowNodeAttribute.Id` 和未删除参数的 `NodeParamAttribute.Id` 不变。仅改显示名、描述、帮助文本或默认值时，不应改变这些 ID。

示例：

```csharp
[FlowNode(Id = "quality.calculate-rate", AnotherName = "计算合格率")]
public QualityResult Calculate(
    [NodeParam(Id = "batch-no", Name = "批次号")] string batchNo,
    [NodeParam(Id = "planned-count", Name = "计划数量")] int plannedCount,
    [NodeParam(Id = "qualified-count", Name = "合格数量", Aliases = ["pass-count"])] int qualifiedCount)
{
    // ...
}
```

### 4.2 扫描校验

上传扫描阶段必须校验以下约束，并以双语诊断拒绝无效新工件：

1. ID 为非空、规范化后唯一的受限标识符；建议格式为小写字母、数字、`.`、`-` 与 `_`。
2. 同一类库族中，节点契约 ID 不得重复。
3. 同一节点中，参数契约 ID 不得重复。
4. 同一节点中，别名不得重复，且不得等于其他活动参数 ID。
5. 节点 ID 必须稳定地对应一种节点类型；`Action` 与 `Flipflop` 间变化视为破坏性变更。
6. `Flipflop` 仍必须返回 `Task` 或 `Task<T>`；新目标工件不满足此规则时上传失败。
7. 完整 CLR 签名仍要保存，用于区分重载；不能仅以类名和方法名唯一标识节点。

对于没有显式 ID 的历史工件，扫描器可生成仅供展示和分析的 fallback identity，例如“程序集名 + 完整类型名 + 方法名 + 完整参数类型签名”。此类节点必须写入 `identityConfidence = legacy`。当源、目标版本无法唯一匹配时，分析结果为 `Unknown`，不得自动迁移。

## 5. 兼容性 Manifest 与分析规则

### 5.1 Manifest 内容

扫描器除了当前用于节点库展示的 `NodeCatalogJson` 外，应持久化面向兼容性判断的 `CompatibilityManifestJson`。建议结构如下：

```text
Artifact
  ArtifactId / SHA-256
  FamilyId
  SemanticVersion
  AssemblyIdentity
  Nodes[]
    ContractId
    IdentityConfidence             Explicit | Legacy
    NodeType                       Action | Flipflop
    DeclaringType
    MethodName
    OverloadSignature
    ReturnType
    IsAwaitable
    OutputContract
    Parameters[]
      ContractId
      Aliases[]
      CLR name / display name / description
      CLR type / nullability
      Required
      Default value (safe JSON representation)
      IsVariadic / element type / collapsed-input support
      Enum names and underlying values
      IsInjectedFlowContext
```

`IFlowContext` 注入参数不属于用户可连接参数，必须在 Manifest 中标识为 `IsInjectedFlowContext = true`，但不在普通节点参数席位、映射和连线分析中出现。

### 5.2 分析分类

升级预览的每个受影响节点、参数和连线必须落入以下一种状态：

| 分类 | 含义 | 是否可直接应用 |
| --- | --- | --- |
| `Exact` | 契约 ID、节点类型、输入输出和调用语义完全一致 | 可以 |
| `Compatible` | 仅展示信息变化、增加可选参数或明确安全的默认值变化 | 可以 |
| `RequiresMapping` | 通过 `Aliases` 识别重命名，或需用户选择单一映射 | 用户确认后可以 |
| `RequiresRewire` | 参数拆分/合并、必填参数新增或端口拓扑变化 | 不可直接；完成手工连线/赋值后才可以 |
| `Breaking` | 节点删除、节点类型变化、返回契约不兼容、Flipflop 非 awaitable、无法转换参数类型 | 不可以 |
| `Unknown` | 历史 fallback identity 无法唯一对应目标节点或重载 | 不可以 |

### 5.3 参数与返回类型规则

以下规则只用于生成预览，升级应用仍需执行完整的流程定义校验。

| 变更 | 分类 | 处理原则 |
| --- | --- | --- |
| 显示名、描述变更 | `Compatible` | 保留流程节点自定义名称和参数显式值 |
| 新增带可靠默认值的可选参数 | `Compatible` | 写入目标默认值或保持未显式设置 |
| 参数改名且目标含原 ID 别名 | `RequiresMapping` | 预填映射，用户确认后保留值和数据连线 |
| 新增必填参数 | `RequiresRewire` | 必须在预览中显式赋值或重新连线 |
| 参数删除 | `Breaking` | 若节点有显式值或入站数据连接，阻止升级；无使用时可作为受控兼容项 |
| 参数顺序变化、稳定 ID 不变 | `Exact` / `Compatible` | 以稳定 ID 映射，绝不以序号映射 |
| `int -> long`、`int -> decimal` | 可标记为受控 `Compatible` | 仅当统一运行时转换器已有无损或明确定义的规则 |
| `string -> Guid` | `RequiresMapping` | 只有用户确认且转换器支持时允许 |
| 任意对象、集合元素类型或可空性收紧 | `Breaking` 或 `RequiresRewire` | 不允许基于 `ToString()` 自动猜测 |
| 返回类型、输出端口类型或数目改变 | `Breaking` | 必须检查所有下游数据连接，首期不自动改写 |
| Flipflop 失去 `Task` / `Task<T>` 返回 | `Breaking` | 目标工件不可作为升级目标 |
| 重载身份无法唯一匹配 | `Unknown` | 不能自动升级 |

数据连接映射必须以 `Connection.ToPortId = NodeParameterDefinition.Id` 为主键。节点卡片中的显示名称、参数排序、连接器布局和描述变化不应改变连接指向。

## 6. 服务端实现计划

### Phase A：稳定契约与工件元数据准备

1. 为 `FlowNodeAttribute` 和 `NodeParamAttribute` 增加可选稳定 ID、参数别名成员，并同步更新测试类库示例。
2. 扩展 PE Metadata 扫描器：读取稳定 ID、别名、完整方法签名、返回类型、可等待性、默认值、枚举、可变参数和受限 `IFlowContext` 注入信息。
3. 生成并保存 `CompatibilityManifestJson`；现有 `NodeCatalogJson` 继续服务节点库展示，避免将兼容性内部细节直接耦合到前端。
4. 新增迁移版本，为 `Libraries` 添加 `FamilyId`、`SemanticVersion`、`CompatibilityManifestJson`、`Lifecycle`，并创建 `LibraryFamilies`。
5. 对已有库采用“未归属 / legacy family”迁移策略：不猜测不同 SHA 是否同族；管理员可在环境类库页面显式归类。没有稳定 ID 的工件保持 legacy 状态。
6. 运行迁移后为现有流程定义回填 `FlowLibraryBindings`，为既有 `FlowRunDefinitions` 回填 `RunLibraryBindings`；回填失败应产生诊断记录但不得改写快照。

### Phase B：兼容性分析服务

1. 在 Application 层新增与数据库无关的模型：`LibraryArtifactManifest`、`LibraryCompatibilityReport`、`NodeCompatibilityResult`、`ParameterCompatibilityResult`、`FlowUpgradePreview`。
2. 新增 `ILibraryCompatibilityAnalyzer`，输入源/目标 Manifest 和流程定义，输出确定性、可序列化的分析报告。
3. 节点匹配优先级为：显式 `ContractId` -> 旧 ID 的目标别名（仅参数） -> 唯一 legacy 完整签名。不得使用“同名第一个方法”匹配。
4. 分析受影响的节点运行元数据、显式参数值、数据连接、执行连接、节点输出和下游目标参数；所有无法证明安全的情况都输出阻断诊断。
5. 将报告存入 `LibraryUpgradePlans.AnalysisJson`。创建预览不修改项目引用、流程版本或节点定义。

### Phase C：升级应用与事务

升级应用的每一个 Flow 必须使用调用方提交的 `ExpectedFlowVersion` 做乐观并发校验。版本不一致时返回：

```text
flow.version_conflict
The flow version changed before the library upgrade could be applied. 类库升级应用前流程版本已发生变化。
```

对单一流程的应用顺序如下：

```text
读取已确认的升级预览
  -> 读取当前流程定义及 ExpectedFlowVersion
  -> 重做兼容性分析并验证预览未过期
  -> 拒绝 Breaking / Unknown / 未完成 RequiresRewire 项
  -> 克隆当前流程定义为 Version + 1
  -> 仅替换受影响节点的类库工件与运行时元数据
  -> 保留节点 ID、坐标、画布、样式、自定义名称、未受影响参数及执行连接
  -> 以参数稳定 ID 映射显式值与数据连接目标端口
  -> 按用户确认的映射填充改名参数或新增必填参数
  -> 运行现有流程定义、项目类库引用与 FlowCall 校验
  -> 添加目标工件项目引用、写入新流程版本和 FlowLibraryBindings
  -> 当前项目流程均不再使用源工件时，自动取消其项目引用
  -> 更新升级计划状态与结果
  -> 事务提交
```

事务失败时，必须同时回滚：新流程版本、项目新引用或旧引用清理、绑定索引与升级计划应用状态。原流程和旧工件不受影响。

升级成功后，系统只检查该项目的当前流程定义；当没有任何当前流程继续使用源工件时，自动取消其项目引用。`FlowDefinitionVersions`、`FlowLibraryBindings`、运行快照及其 `RunLibraryBindings` 保持不变，旧工件仍可用于历史审计和运行快照访问。

### Phase D：仓储接口与 Infrastructure

Application 服务继续仅依赖 `IRepository<TEntity>`、`IUnitOfWork` 和文件/目录抽象；不得向 API 或 Application 暴露 `SqlSugarClient`、`ISqlSugarClient`、原始 SQL 或 SQLite 连接对象。

新增或扩展仓储职责：

```text
ILibraryFamilyStore
ILibraryArtifactStore                 existing catalog persistence extension
ILibraryUpgradePlanStore
IFlowLibraryBindingStore
IRunLibraryBindingStore
```

Infrastructure 内使用 SqlSugar 实现这些接口和数据迁移。所有“创建流程版本 + 绑定索引 + 项目引用 + 升级计划状态”写入必须在同一个 `IUnitOfWork.ExecuteAsync` 中完成。

## 7. API 合约计划

以下路由遵循现有管理 API 风格，实际 DTO 名称可在实现时与现有 `LibraryDto`、`ProjectLibraryReferenceDto` 对齐。

```text
GET  /api/library-families
GET  /api/library-families/{familyId}/artifacts
PATCH /api/libraries/{libraryId}/family
PATCH /api/libraries/{libraryId}/lifecycle

GET  /api/libraries/{libraryId}/compatibility?targetLibraryId={targetLibraryId}
POST /api/projects/{projectId}/library-upgrades/preview
GET  /api/projects/{projectId}/library-upgrades/{upgradeId}
POST /api/projects/{projectId}/library-upgrades/{upgradeId}/apply
```

`POST /preview` 请求应至少包含：

```json
{
  "sourceArtifactId": "sha256-old",
  "targetArtifactId": "sha256-new",
  "flowIds": ["flow-a", "flow-b"],
  "expectedFlowVersions": {
    "flow-a": 12,
    "flow-b": 3
  }
}
```

应用请求只允许提交分析中可识别的确认和映射，禁止直接发送任意替换后的流程 JSON：

```json
{
  "expectedFlowVersions": {
    "flow-a": 12
  },
  "acknowledgedItems": ["flow-a.node-rate.parameter-qualified-count"],
  "parameterMappings": [
    {
      "flowId": "flow-a",
      "nodeId": "node-rate",
      "sourceParameterId": "pass-count",
      "targetParameterId": "qualified-count"
    }
  ],
  "requiredValueOverrides": []
}
```

成功响应必须返回每一个流程的独立结果：原版本、新版本、迁移节点数量、保留/重接连线数量、未处理诊断、旧/新工件 ID。项目批量升级的语义为“逐 Flow 独立结果”，而不是发生一处冲突就模糊地改写整个项目。

所有诊断、错误码和异常提示遵循现有规范：英文在前、中文在后，例如：

```text
library.upgrade_breaking_change
The target library removes a parameter used by the flow. 目标类库移除了流程正在使用的参数。
```

## 8. Worker 与运行时约束

本特性不应改变 Worker 隔离边界。

1. API 创建 Run 时继续先生成 `FlowRunDefinitions`，再从该快照提取 `AllowedLibraryIds`、脚本缓存根和包根路径。
2. Worker 只能从请求允许的工件 ID 中加载 DLL，且继续使用每个 Worker Run 独立的可回收 `AssemblyLoadContext`、程序集缓存、类型缓存和方法缓存。
3. Worker 不得查询类库族的 `LatestArtifactId`，也不得把流程节点的工件 ID 替换为同族最新版本。
4. 类库升级后，新创建的流程版本使用目标工件；旧版本以及已经创建的 Run 继续使用源工件。
5. `RunLibraryBindings` 用于恢复、审计和归档影响展示，不用于跳过 Worker 的 `AllowedLibraryIds` 验证。
6. 归档源工件不能影响已创建 Run 的重试、恢复或快照查看；只有缺失的工件文件才应明确失败并记录 `library.package_missing`。

## 9. 前端交互计划

前端使用现有控制台与编辑器的 Minimalism / Swiss Style：紧凑网格、清晰信息层级、操作靠近上下文，不引入营销式大卡片或自动变更。

### 9.1 环境类库

1. 在环境类库列表按“类库族”分组，显示名称、当前推荐工件版本、工件数量、状态与最后上传时间。
2. 展开族后显示每个工件的语义版本、SHA 短码、上传日期、节点数量、使用流程数、运行快照数和归档状态。
3. 上传新版本后，显示它是新工件而不是覆盖旧工件；对未归属工件提供显式“归入类库族”操作。
4. 归档按钮仅改变新引用可见性，界面要清晰提示“仍被旧流程和历史运行保留”。

### 9.2 项目类库引用

1. 项目类库面板显示当前引用的具体工件版本、SHA 短码和被当前流程使用的数量。
2. 对同族可用的目标工件显示“可检查升级”，不在页面加载、保存流程或上传工件时自动替换节点。
3. 默认隐藏已归档且未引用的工件；已被当前项目或历史流程绑定的归档工件必须以受控“已归档”状态显示，不能无故消失。

### 9.3 升级向导与预览

升级在一个可关闭的向导中完成：

```text
选择源工件与目标工件
  -> 选择受影响流程
  -> 查看兼容性报告
  -> 确认映射或补齐必填参数
  -> 生成新的流程版本
```

预览表每一行至少显示：流程、画布、节点、源/目标方法签名、参数映射、受影响数据连接、分类、建议操作和诊断。`Exact`、`Compatible`、`RequiresMapping`、`RequiresRewire`、`Breaking`、`Unknown` 必须有稳定的文字和颜色语义，且不能仅依赖颜色表达。

对需要重连的项，点击后进入升级草稿编辑器：只编辑即将创建的新流程版本草稿，源流程为只读。画布应高亮目标节点、失效输入连接器和需要设置的参数，保持既有节点位置与连接样式。用户未完成所有阻断项前，“创建新版本”按钮禁用。

### 9.4 节点检查器与审计

节点检查器应显示：当前绑定的类库族、工件版本、SHA 短码、稳定节点契约 ID、方法签名和“发现可升级版本”状态。该状态仅提示，不改变节点。

流程版本历史和运行快照查看页显示实际使用的工件版本及 SHA，以便审计某次运行到底执行了哪一份 DLL。快照编辑器继续只读。

前端通过现有 i18n 提供中英文文案。中文模式显示中文，英文模式显示英文；不把双语错误串直接作为普通界面标签。

## 10. 迁移与发布阶段

### Phase 1：契约基础与无副作用采集

- 增加特性稳定 ID、扫描器与 Manifest 存储。
- 创建类库族/工件元数据迁移和绑定索引。
- 仅提供内部或管理员只读 Manifest 查看能力。
- 不暴露升级按钮，不改写任何流程。

**验收：** 新上传工件可以保存完整 Manifest；历史工件被标记为 legacy；现有上传、项目引用、流程保存、Worker 执行和历史快照不回归。

### Phase 2：只读兼容性报告

- 实现 `ILibraryCompatibilityAnalyzer` 与预览 API。
- 在项目类库界面提供“检查升级”入口和只读报告。
- 对 legacy、重载歧义、输出变化、必填新增等场景正确阻断。

**验收：** 预览不会改变流程 JSON、流程版本、项目引用或节点位置；重复调用得到相同分析结果；报告可追溯到源/目标 Manifest SHA。

### Phase 3：单流程受控应用

- 实现 Expected Flow Version 校验、克隆流程版本、稳定 ID 参数迁移、事务写入和应用结果。
- 首期只允许一次应用一个流程；需要映射或重连的项必须人工确认。
- 在编辑器中提供新版本草稿并保留源版本的只读入口。

**验收：** 升级成功后只有新流程版本使用新工件，源版本仍可运行；任意校验失败都不留下半成品引用或流程版本。

### Phase 4：项目批量升级与使用影响

- 支持选择多个流程并返回逐流程结果。
- 使用 `FlowLibraryBindings`、`RunLibraryBindings` 展示工件影响范围。
- 支持升级计划历史、失败重试和已归档工件影响提示。

**验收：** 一个流程失败不会模糊覆盖其他流程结果；正在运行的旧 Run 和已完成快照始终引用原工件。

### Phase 5：后续治理（不纳入本期）

- 定义工件保留期、导出备份、包完整性巡检和经过确认的物理删除流程。
- 引入可选的发布签名、供应链签名校验和审批机制。
- 允许跨项目的工件使用报告，但不突破项目类库引用隔离。

## 11. 测试计划与验收标准

### 11.1 单元测试

1. 同名、不同 SHA 的 ZIP 上传后形成不同工件，旧包文件未被覆盖。
2. 同一类库族中稳定节点 ID、参数 ID、别名重复时上传被拒绝。
3. 同名方法的不同重载使用完整签名区分；不唯一 legacy 匹配返回 `Unknown`。
4. `Exact`、`Compatible`、`RequiresMapping`、`RequiresRewire`、`Breaking`、`Unknown` 的分析分类均有覆盖。
5. 插入、重排参数但参数契约 ID 不变时，既有显式值和数据连接仍映射至正确参数。
6. 参数别名改名后，预览正确生成待确认映射。
7. 新增必填参数、删除正在使用的参数、输出类型改变、节点删除和 Flipflop 返回类型失效均阻断应用。
8. `IFlowContext` 注入参数不会被暴露为可连线输入，也不会影响普通参数契约比较。
9. 目标工件与源工件不属于同一类库族时默认阻断，除非后续产品策略增加显式跨族确认。

### 11.2 集成与事务测试

1. 创建升级预览前后，流程定义、当前版本、项目引用和绑定索引均不变化。
2. 成功应用后产生 `Version + 1`，源版本的 JSON、节点 ID、坐标、自定义名称和引用完整保持。
3. 新版本仅替换匹配节点的运行时工件元数据；未受影响节点和执行连接字节等价或语义等价保留。
4. 连接目标端口按稳定参数 ID 保留，不受参数名称、显示顺序和节点 UI 布局变化影响。
5. `ExpectedFlowVersion` 过期时返回 `flow.version_conflict`，且不产生新版本或新项目引用。
6. 应用过程任一写入失败时，流程版本、项目引用、升级计划状态和绑定索引全部回滚。
7. 项目引用源工件与目标工件可并存；源工件仅在没有当前流程使用时才可从项目引用移除。

### 11.3 Worker 与运行快照测试

1. 新工件上传、预览或升级后，已经排队和运行中的 Run 继续从 `FlowRunDefinitions` 使用旧工件。
2. 新流程版本创建的 Run 仅允许加载目标工件，Worker 不会解析“最新版本”。
3. 同一个 Worker Run 内仍复用类库程序集、类型和方法缓存；升级逻辑不增加每节点加载次数。
4. 旧工件归档后，已绑定的旧流程和运行快照仍可执行；未引用项目中新建节点默认不可选择该归档工件。
5. 包文件确实缺失时，Worker 返回结构化 `library.package_missing` 错误并保留运行事件。

### 11.4 API 与前端验收

1. API 返回源/目标 SHA、语义版本、分析分类、映射、受影响连线及可操作诊断。
2. 前端不会因上传、刷新、打开项目或保存流程而自动更新任意节点工件。
3. 升级向导能区分可自动迁移、需要确认、需要重连和阻断项；阻断项存在时不能应用。
4. 从预览进入草稿时，仅新版本草稿可编辑，旧流程和运行快照始终只读。
5. 节点检查器、流程版本历史、运行快照都能显示实际绑定的工件版本和 SHA。
6. 中文/英文界面文案分别正确；API 异常与日志遵循“英文在前，中文在后”的双语规范。

## 12. 实施完成后的行为

```text
上传 类库 v1.6.0 ZIP
  -> 得到新的 SHA 工件，v1.5.0 工件不变
  -> 扫描稳定节点/参数契约并保存 Manifest
  -> 项目管理员选择 v1.5.0 -> v1.6.0 与目标流程
  -> 系统给出每个节点、参数和数据连接的兼容性预览
  -> 用户确认映射、补齐必要参数或处理重连
  -> 系统事务性创建新的流程版本
  -> 新版本绑定 v1.6.0，旧版本继续绑定 v1.5.0
  -> 新 Run 按各自流程快照执行指定 SHA
  -> 旧 Run、旧流程版本、历史审计和旧 DLL 均不被破坏
```

该方案的安全边界是“版本化发布、显式迁移、快照执行”，而不是“覆盖 DLL 后希望旧流程继续可用”。只有在稳定节点/参数契约可以证明兼容、且所有需要用户决策的映射已被确认后，才创建新的流程版本。

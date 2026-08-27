# 枚举参数值选项实施计划

**状态：** 已完成  
**编制日期：** 2026-08-27  
**适用范围：** 类库 ZIP 元数据扫描、Contracts、Domain、Application、Infrastructure、Vue 编辑器、运行快照与测试。  
**前置提交：** `382a962 feat: improve typed node parameter inputs`

## 1. 目标与边界

为 DLL 节点的枚举型方法参数提供安全、可追溯的选项式编辑体验。

```text
上传类库 ZIP
  -> 仅用 PE Metadata 扫描枚举定义
  -> 类库节点目录下发枚举元数据
  -> 拖入画布时复制到节点参数定义
  -> 保存至流程定义与 FlowRunDefinition 快照
  -> 编辑器显示单选下拉或 Flags 多选
  -> Worker 继续将已选名称转换为目标 CLR 枚举
```

本期覆盖：

- `Action` 和 `Flipflop` DLL 节点的枚举参数。
- ZIP 主 DLL 中声明的枚举，以及 ZIP 内依赖 DLL 中可无歧义解析的枚举。
- 普通枚举的单选输入和 `[Flags]` 枚举的多选输入。
- 已保存流程、运行快照和审计界面中的选项元数据保留。
- 字面量、数据连接、项目输入、表达式四种参数来源共存。

本期明确不做：

- 不在 API 进程反射加载外部 DLL，也不执行用户程序集代码。
- 不扫描开发机 CLR、NuGet 全局缓存或运行时目录来猜测框架/系统枚举。
- 不为无法从 ZIP 解析的枚举伪造选项；它们安全退化为现有文本输入。
- 不改变 Worker 的隔离边界、流程调度、运行协议或已有布尔参数选择器。
- 不把运行时数据连接或项目输入限制为前端可枚举值；这类值仍由 Worker 在运行时转换和校验。

## 2. 当前实现与缺口

### 已具备

- `LibraryCatalogService` 使用 `System.Reflection.Metadata` 和 `PEReader` 扫描 ZIP 主 DLL，不会加载该程序集。
- 参数目录已经携带 `Id`、名称、CLR 类型、必填和可变参数信息。
- 节点参数 ID 会保存到流程定义，运行快照保存不可变 `DefinitionJson`。
- `LibraryArgumentConverter` 已识别 `Type.IsEnum`；字符串成员名按大小写不敏感方式解析，数值可转换为枚举底层类型。
- 前端已有按参数类型提供专用编辑器的基础：`bool` 字面量已经显示真/假选项。

### 必须补齐

| 缺口 | 当前表现 | 需要的结果 |
| --- | --- | --- |
| 枚举成员扫描 | 仅保存 `System.Namespace.Mode` 类型名 | 扫描成员名、底层值、是否 Flags、底层 CLR 类型 |
| 契约 | `LibraryParameterDto` 与参数 UI 元数据没有枚举字段 | 目录、画布节点、流程定义和快照都携带同一不可变元数据 |
| 编辑器 | 所有非 bool 字面量都是文本框 | 普通枚举为下拉框，Flags 为复选框组 |
| 保存校验 | 枚举字面量可输入任意文本 | 已知枚举的字面量在 API 保存时校验成员名与 Flags 组合 |
| 已上传类库 | SHA 相同的上传直接返回已有 `NodeCatalogJson` | 新扫描器部署后可重新索引已保存 ZIP，补齐目录元数据 |
| 审计 | 快照只显示参数的类型和原始字面量 | 快照可显示枚举类型、成员名和 Flags 组合 |

## 3. 架构决策

### 3.1 元数据模型

在 Contracts 中新增下列只含 JSON 原始值的记录。所有数值以字符串传输，避免 `UInt64` 和 JavaScript `Number` 精度损失。

```csharp
public sealed record EnumValueOptionDto(
    string Name,
    string NumericValue);

public sealed record EnumParameterMetadataDto(
    string TypeName,
    bool IsFlags,
    string UnderlyingType,
    IReadOnlyList<EnumValueOptionDto> Options);
```

扩展以下字段，均放在现有记录的**末尾并提供默认 `null`**，保证已保存 JSON 可以反序列化：

```text
LibraryParameterDto.EnumMetadata
NodeParameterUiMetadataDto.EnumMetadata
前端 LibraryParameterDto.enumMetadata
前端 MethodParameter.enumMetadata
```

`NodeParameterDefinition` 也必须持有等价的领域值对象。Application 映射、持久化 JSON 与 `FlowDefinitionValidation.ToDto` 负责完整往返，不能只把该字段停留在浏览器状态。

### 3.2 字面量表示

流程定义中的 `ValueJson` / `literalValue` 继续保存为字符串：

```text
普通枚举       -> "Automatic"
Flags 单个值    -> "Read"
Flags 多个值    -> "Read, Write"
```

`NumericValue` 仅供 UI 显示、Flags 状态计算、快照查阅和服务端校验使用，不作为默认保存格式。保存成员名可读性更好，也与当前 `Enum.Parse(..., ignoreCase: true)` 行为一致。

对于 Flags：

- 包含零值成员（通常为 `None`）时，该成员是互斥选项；选择任一非零成员时自动取消零值。
- 选择零值时自动取消其它成员。
- 未选择任何成员保持空字面量，继续由现有 `Required` 校验决定是否可保存。
- 按 DLL 元数据声明顺序保存已选成员，以产生稳定的快照与 diff。

### 3.3 解析范围与安全退化

扫描器建立 ZIP 内所有 `.dll` 的只读枚举索引，不加载它们：

```text
ZIP 主 DLL
  + ZIP 内依赖 DLL
  -> TypeDefinition.BaseType == System.Enum
  -> full type name -> EnumParameterMetadataDto
```

解析规则：

1. 支持顶层和嵌套枚举，类型名与现有签名提供器一致，例如 `Company.Device+State`。
2. 支持 `byte`、`sbyte`、`short`、`ushort`、`int`、`uint`、`long`、`ulong` 的枚举常量，统一转成不丢失精度的十进制字符串。
3. 通过 `System.FlagsAttribute` 判断 `IsFlags`。
4. 对 `Nullable<TEnum>` 解包后查找 `TEnum`。
5. 同一完整类型名在 ZIP 中出现多个不一致定义时，不任选一个。该参数不下发枚举元数据，保留文本输入，并记录服务端结构化诊断。
6. 类型只存在于框架程序集、缺失依赖包或无法从 ZIP 唯一解析时，同样不下发元数据。Worker 仍可按既有逻辑执行。

这使 API 只处理 PE 元数据与 JSON，不持有外部程序集的 `Assembly`、`Type`、`FieldInfo` 或用户代码实例。

### 3.4 校验与运行时职责

| 参数来源 | 保存阶段 | 运行阶段 |
| --- | --- | --- |
| `Literal` 且有 `EnumMetadata` | 校验普通枚举成员，或校验 Flags 中逗号分隔的每一个成员 | Worker 正常转换为 CLR 枚举 |
| `Literal` 但无元数据 | 保持现有自由文本行为 | Worker 转换失败时返回 `node.input_invalid` |
| 数据连接 / 项目输入 / 表达式 | 不进行静态成员校验 | Worker 用目标反射参数类型转换并报告转换错误 |

新增保存诊断：

```text
node.enum_literal_invalid
Enum literal '{value}' is not valid for parameter '{parameter}'.
枚举字面量“{value}”不是参数“{parameter}”的有效选项。
```

该规则不禁止已有动态数据链路。Worker 的枚举转换仍是最终防线，不移除其对名称、数字和跨节点值的支持。

## 4. 分阶段实施

### Phase 1: Contracts、Domain 与持久化模型

1. 在 `SereinFlow.Contracts` 定义 `EnumValueOptionDto` 和 `EnumParameterMetadataDto`。
2. 扩展 `LibraryParameterDto`、`NodeParameterUiMetadataDto` 和任何 API 目录映射，末尾添加可空 `EnumMetadata`。
3. 在 `SereinFlow.Domain.NodeParameterDefinition` 加入领域等价模型；验证类型名、底层类型、选项名称不为空且选项名不重复。
4. 更新 `FlowDefinitionValidation` 的 DTO -> Domain 和 Domain -> DTO 映射；确保 API 保存、读取、流程运行副本与运行快照完整往返。
5. 前端扩展 `LibraryParameterDto`、`NodeParameterUiMetadataDto`、`MethodParameter`、`NodeParameterDefinition`，以及 `flowDtoMapper.ts`、`useNodeDrop.ts` 的双向映射。
6. 不需要修改 Worker 协议版本：枚举元数据已经包含在 `DefinitionJson` 和运行请求中，且仅用于可读性、编辑和保存校验。

### Phase 2: ZIP 级枚举 Metadata 扫描

1. 将 `LibraryCatalogService.InspectPackageAsync` 的 ZIP 枚举逻辑抽成受测试保护的包元数据读取器。
2. 在现有安全检查完成后，枚举 ZIP 内全部 `.dll` 条目并用 `PEReader` 读取元数据；复用大小、条目数和 Zip Slip 防护，不解压至任意路径。
3. 第一遍建立 `EnumTypeIndex`：
   - `TypeDefinition.BaseType` 为 `System.Enum` 的类型；
   - 忽略实例字段 `value__`；
   - 从静态 literal 字段提取名称与常量；
   - 提取 `FlagsAttribute` 与实际底层类型；
   - 保留 metadata 声明顺序。
4. 第二遍继续扫描主 DLL 上的 `[FlowLibrary]` / `[FlowNode]` 方法；参数签名解析后查找其枚举元数据，并填入 `LibraryParameterDto.EnumMetadata`。
5. 对同名冲突、无常量、损坏的依赖 DLL、未知底层类型生成内部诊断并安全降级；不得把无关 DLL 导致的枚举索引问题变成 API 进程崩溃。
6. 保持 Flipflop 的 `Task` / `Task<T>` 上传校验、`IFlowContext` 参数过滤和 `params` 参数扫描不变。

### Phase 3: 既有类库目录重新索引

1. 为 `LibraryRecord` 增加仅 Infrastructure 使用的 `CatalogSchemaVersion`，并新增 SQLite migration。
2. 枚举扫描升级时递增目录版本；上传新 ZIP 直接写入当前版本。
3. 建立受控 `LibraryCatalogReindexHostedService`：应用启动后，以有限并发读取版本落后的不可变 ZIP，并在每一个类库完成后用独立事务更新 `NodeCatalogJson` 和版本号。
4. 单个 ZIP 损坏或无法重新扫描时保留最后可用目录，将失败原因记录为双语结构化日志；不得阻塞 API 启动或影响其它类库。
5. 在运行环境类库管理页增加“重新扫描目录”图标按钮和状态提示，供管理员显式重试。该操作只更新扫描目录，绝不替换 ZIP、类库 ID 或现有项目引用。
6. 运行快照绝不随重新索引改变：它使用创建运行时保存的流程定义副本。

### Phase 4: 前端参数编辑器

1. 在 `parameterTypes.ts` 增加枚举识别与字面量编解码纯函数：
   - `isEnumParameter(parameter)`；
   - `parseEnumLiteral("Read, Write")`；
   - `serializeEnumLiteral(options)`；
   - Flags 零值互斥规则。
2. `ParameterEditor.vue` 在 `source === 'literal'` 且具有 `enumMetadata` 时优先渲染：
   - **普通枚举：** 原生 `select`，首项为“请选择 / Select a value”。
   - **Flags：** `fieldset` + 可滚动的复选框选项组；选项数量较多时限定稳定高度，不使检查器撑出可视区域。
3. 选项文本使用 DLL 声明的成员名；辅助文本显示枚举类型，必要时显示 `NumericValue`，不混入中文/英文翻译以避免臆造业务含义。
4. `source` 改为数据连接、项目输入或表达式时，隐藏枚举选择器并保留现有来源编辑控件；切回字面量时恢复最近的有效成员组合。
5. 枚举元数据缺失时保持文本框，并以低优先级状态文本说明“枚举选项不可用 / Enum options unavailable”，不阻断编辑。
6. 遵循现有 Minimalism / Swiss Style：使用原生下拉与复选框，不增加卡片嵌套；选项色彩不是唯一状态指示；中文模式只显示中文，英文模式只显示英文。
7. `RunSnapshotNodeInspector.vue` 增加只读枚举元数据展示：类型、普通成员或 Flags 成员组合、原始配置值。快照界面不得提供编辑控件。

### Phase 5: 保存验证与运行期回归

1. 在 Application 领域校验中对带元数据的 `Literal` 参数执行成员校验，返回 `node.enum_literal_invalid` 双语诊断及准确参数路径。
2. 正常枚举拒绝空值以外的未知成员；是否允许空值仍由 `Required` 规则控制。
3. Flags 拆分逗号成员、去除空格、检查成员存在与重复，零值和非零值并存时拒绝保存或由前端在选择时归一化。
4. 保持 `LibraryArgumentConverter.ConvertToEnum` 作为运行时最终转换器；补充异常包装，确保动态来源给出参数名、目标类型和双语 `node.input_invalid`。
5. 运行审计记录继续保存解析后的真实入参和输出；不将枚举 `Type` 或反射对象跨 Worker 传输，只传 JSON 字符串或数值投影。

## 5. 文件级改动清单

| 层 | 主要文件 | 改动 |
| --- | --- | --- |
| Contracts | `SereinFlow.Contracts/Dtos.cs` | 新增枚举 DTO；扩展类库参数和参数 UI 元数据 |
| Domain | `SereinFlow.Domain/NodeDefinition.cs` | 参数定义持久化枚举元数据 |
| Application | `FlowDefinitionValidation.cs` 及映射 | 元数据往返、字面量成员验证、双语诊断 |
| Infrastructure | `Persistence/LibraryCatalogService.cs` | ZIP 全 DLL PE Metadata 枚举索引与主 DLL 参数绑定 |
| Infrastructure | `Persistence/SqliteMigrator.cs`、Library record/repository | 目录 Schema 版本与重新索引支持 |
| API | DI/Hosted Service 注册、环境类库端点 | 后台重索引、显式重试 API |
| Frontend API | `api/libraryApi.ts`、`api/flowApi.ts` | 枚举 DTO 类型声明 |
| Frontend flow | `flow/types.ts`、`flow/flowDtoMapper.ts`、`composables/useNodeDrop.ts` | 目录 -> 节点 -> 流程定义完整映射 |
| Frontend inspector | `ParameterEditor.vue`、`RunSnapshotNodeInspector.vue`、`i18n.ts`、`parameterTypes.ts` | 普通枚举选择、Flags 多选、快照只读展示和双语文案 |
| Tests | Infrastructure/Application/Worker/frontend tests | 见下一节 |

## 6. 测试与验收

### Metadata 与持久化

- 上传测试类库可识别普通枚举、枚举类型名、底层类型、成员名和稳定数值字符串。
- `[Flags]` 可识别并保留零值、单 bit 值和组合别名。
- 枚举定义位于 ZIP 依赖 DLL 时可被唯一解析。
- 同名枚举冲突、缺失依赖 DLL、损坏 DLL 时不加载程序集、不崩溃，参数安全退化。
- 已上传类库重新索引后获得选项；失败可重试；既有项目引用和不可变运行快照不变。
- 流程定义、保存后重新加载、FlowRunDefinition 快照都保留完全相同的枚举元数据。

### 编辑器

- `System.Namespace.Mode` 字面量显示普通下拉，选择后持久化成员名。
- `[Flags]` 显示复选框组，零值互斥，多个成员以稳定顺序保存为 `A, B`。
- 未解析枚举仍是文本框；布尔值仍是独立的真/假选择器，二者不互相覆盖。
- 数据连接、项目输入、表达式不会被枚举控件干扰；切换来源不破坏连接器和现有值。
- 中文/英文模式的控制标签分别纯中文/纯英文；键盘可聚焦、可选择、可清除。
- 运行快照只读显示配置的枚举信息，不能修改。

### 验证与 Worker

- 已知普通枚举的未知字面量保存时返回 `node.enum_literal_invalid`。
- Flags 的未知成员、重复成员、零值与非零值组合得到准确参数路径诊断。
- 动态来源在运行时按现有 `LibraryArgumentConverter` 转换；无效值返回可读的 `node.input_invalid`。
- Action 与 Flipflop 的枚举入参均可正确执行；失败时仍按既有 `Success` / `Failure` / `Error` 语义处理。
- API、扫描器和重新索引服务测试证明未调用 `Assembly.Load`、未执行外部类库代码。

## 7. 实施顺序与完成定义

实施按以下顺序进行，任何阶段均需通过前一阶段的测试后再进入下一阶段：

1. Contracts / Domain / mapper 的可空元数据字段与 JSON 往返测试。
2. 只读 ZIP 枚举扫描及类库目录测试。
3. 保存校验、错误诊断和 Worker 转换回归。
4. 前端单选、Flags 多选、快照只读展示与中英文文案。
5. 已上传类库重索引、环境类库页面入口与端到端测试。

完成时，用户上传一个包含枚举入参的类库后，可以在节点检查器直接选择有效成员；Flags 可组合选择；保存、重载、运行和运行快照均保留同一组选项与已选值。任何无法安全解析的枚举都不会导致程序集加载或 API 崩溃，并会可靠退化为文本输入与运行时转换校验。

## 8. 实施验证

已于 2026-08-27 完成实施并验证：

- Vue 单元测试与生产构建通过。
- Infrastructure、Application、Worker Integration 与 Runtime 测试通过。
- API 核心编译通过；保留的分析器警告均来自既有运行队列服务，不属于本计划新增代码。
- 后台重建在单个历史 ZIP 损坏时会保留最后可用目录、记录结构化双语警告，并继续处理其它过期类库。

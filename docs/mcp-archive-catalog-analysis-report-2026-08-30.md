# MCP 项目与类库归档清单分析报告

日期：2026-08-30

## 结论

当前 MCP 的默认项目清单确实会返回已归档项目，原因是 MCP 读取模型没有按 `ProjectStatus.Archived` 过滤。项目归档状态已经持久化，无需新增数据库字段或迁移；问题仅在 MCP 读取投影与资源路由。

类库的默认清单已经只返回活动工件（`LibraryLifecycleDto.Available`），但并不存在“仅已归档类库”的 MCP 清单。现有 `includeArchived=true` 的语义是“活动和归档混合返回”，不能作为与默认活动清单对称的归档读取链路。

建议保留按 ID 的直接读取能力，使已归档对象仍可用于审计、既有引用和运行追溯；默认清单和新增归档清单则严格分离。直接读取结果已携带项目 `status` 与类库 `lifecycle`，调用方可以明确标识其只读归档状态。

## 现状证据

| 范围 | 当前调用链 | 归档行为 | 结论 |
| --- | --- | --- | --- |
| MCP 项目工具 | `sereinflow_list_projects` -> `McpReadModelToolHandlers.ListProjectsAsync` -> `AiReadModelService.ListProjectsAsync` | `IProjectRepository.ListAsync` 返回全部项目；读取模型遍历前未过滤 `Archived` | 默认工具错误地返回归档项目 |
| MCP 项目 Resource | `sereinflow://projects` -> `ReadProjectsResourceAsync` -> 同一读取模型 | 与项目工具共用同一未过滤链路 | 同样会返回归档项目 |
| HTTP 项目清单 | `GET /api/projects` | `ProjectsController.List` 在 `includeArchived != true` 时过滤 `ProjectStatus.Archived` | HTTP 默认语义已符合要求，可作为 MCP 对齐目标 |
| MCP 类库工具 | `sereinflow_list_libraries` -> `ListLibrariesAsync(includeArchived)` | 参数缺省为 `false`，类库目录按 `Available` 过滤 | 默认工具已只返回活动类库 |
| MCP 类库 Resource | `sereinflow://libraries` -> `ReadLibrariesResourceAsync` | 未传 `includeArchived`，因此默认仅活动类库 | 默认 Resource 已符合要求 |
| MCP 类库归档查询 | `includeArchived=true` | `ILibraryCatalogService.ListAsync(true)` 返回活动和归档的并集 | 缺少只返回归档类库的独立链路 |
| 直接读取 | `sereinflow_get_project`、`sereinflow_get_library` 和对应 URI 模板 | `GetProjectAsync` / `GetLibraryAsync` 均按 ID 查找，不过滤归档状态 | 可保留为显式审计读取，返回对象状态字段用于区分 |

关键实现位置：

- `src/SereinFlow.Application/AiReadModelService.cs`：`ListProjectsAsync` 在分页前未过滤项目状态；`ListLibrariesAsync(false)` 已默认读取活动类库。
- `src/SereinFlow.Mcp/Tools/McpReadModelToolHandlers.cs`：项目工具与 `sereinflow://projects` Resource 共享未过滤的项目读取；类库工具读取了可选的 `includeArchived`。
- `src/SereinFlow.Infrastructure/Persistence/ProjectPersistence.cs`：仓储的 `ListAsync` 有意返回所有项目，归档过滤不应下沉到这个通用仓储。
- `src/SereinFlow.Infrastructure/Persistence/LibraryCatalogService.cs`：目录服务默认只返回 `Available`，传入 `includeArchived=true` 时返回全部生命周期状态。
- `src/SereinFlow.Api/Controllers/ProjectsController.cs`：HTTP 默认项目清单已经在控制器层过滤归档项目。
- `src/SereinFlow.Api/Controllers/LibrariesController.cs`：标准类库路由默认活动；`/api/environment/libraries` 为控制台归档视图刻意返回全部类库，不应随 MCP 改造改变。

## 建议的 MCP 契约

将“活动/非归档”和“已归档”建模为两个明确的清单能力，而不是让调用方依赖一个含义宽泛的布尔开关。

| 对象 | 默认/活动链路 | 新增归档链路 | 返回集 |
| --- | --- | --- | --- |
| 项目工具 | `sereinflow_list_projects` | `sereinflow_list_archived_projects` | 前者为 `Status != Archived`，后者为 `Status == Archived` |
| 类库工具 | `sereinflow_list_libraries` | `sereinflow_list_archived_libraries` | 前者为 `Lifecycle == Available`，后者为 `Lifecycle == Archived` |
| 项目 Resource | `sereinflow://projects` | `sereinflow://archived-projects` | 与对应工具完全一致 |
| 类库 Resource | `sereinflow://libraries` | `sereinflow://archived-libraries` | 与对应工具完全一致 |

四个清单工具均保留现有的 `maxItems` 边界。新增工具使用现有 `project.read` 或 `library.read` 权限，不建议为了读取归档记录另设管理员权限；归档并不意味着数据不再可读，只意味着它不再参与默认工作集。

建议将 `sereinflow_list_libraries.includeArchived` 标记为兼容性保留的“返回全部生命周期状态”选项，而不要将其改名后冒充“仅归档”。新代码应使用新的 `sereinflow_list_archived_libraries`。在一个明确的弃用窗口后可移除该参数；若当前版本允许不兼容的 MCP 架构调整，也可以直接从默认类库工具的 schema 移除该参数。

Resource 使用独立主机名 `archived-projects` 和 `archived-libraries`，而不是 `sereinflow://projects/archived` 或 `sereinflow://libraries/archived`。这样不会与现有 `{projectId}`、`{libraryId}` 路由产生保留字冲突，资源列表也能直接显示两类清单。

## 建议实现

1. 在 `AiReadModelService` 抽取项目和类库分页投影的私有公共路径，并在排序、`Take(maxItems + 1)` 之前按状态过滤。

   - 保持 `ListProjectsAsync` 的公开默认语义为非归档项目。
   - 新增 `ListArchivedProjectsAsync`，只选择 `ProjectStatus.Archived`。
   - 保持 `ListLibrariesAsync` 的公开默认语义为 `LibraryLifecycleDto.Available`。
   - 新增 `ListArchivedLibrariesAsync`。在不扩张持久化接口的最小方案中，它可调用 `ILibraryCatalogService.ListAsync(includeArchived: true)` 后筛选 `Archived`；筛选必须发生在分页前。

2. 在 `McpReadModelToolHandlers` 增加 `ListArchivedProjectsAsync` 和 `ListArchivedLibrariesAsync`，并让内部 `ReadProjectsAsync`、`ReadLibrariesAsync` 接收明确的生命周期范围，而不是复用布尔的“包含归档”含义。

   - 全局/管理员调用：直接调用读取模型的对应活动或归档方法。
   - 项目范围 API Key：活动项目清单仅在绑定项目非归档时返回该项目；归档项目清单仅在绑定项目已归档时返回该项目。不得因为调用了归档清单而放大到其他项目。
   - 项目范围 API Key 的类库清单：继续先限定到该项目已关联的类库，再按 `Available` 或 `Archived` 筛选。已归档但仍被该项目引用的类库应能出现在归档清单中。

3. 在 `SereinFlowMcpToolCatalogFactory` 注册两项新增只读工具，并在 `ToolDescriptors` 中发布准确说明和仅包含 `maxItems` 的 schema。默认项目工具不应引入 `includeArchived` 参数，避免重现类库的混合状态问题。

4. 在 `SereinFlowMcpBackend` 的 `Resources` 中新增两个归档 Resource；在 `McpResourceReader` 中增加 `archived-projects` 与 `archived-libraries` 的无路径段解析，并调用新增的读取处理器。更新 `docs/mcp-readonly-server.md` 的 Resource 清单。

5. 保持 `sereinflow_get_project`、`sereinflow_get_library`、`sereinflow://projects/{projectId}` 与 `sereinflow://libraries/{libraryId}` 的当前按 ID 读取语义。调用方必须通过返回的 `status` / `lifecycle` 将其视为只读记录。若产品规则要求“归档对象只能从归档链路读取”，则这是额外的访问策略变更，需要同时阻止直接读取、流程拓扑、版本和运行关联读取；该范围明显大于本次清单改造，不建议隐式纳入。

## 分页与一致性要求

状态过滤必须位于排序与分页计数之前。错误顺序为“先排序和取 `maxItems + 1`，再丢弃归档项”，这会造成页面数量不足、`hasMore` 失真，并可能漏掉后续符合状态的记录。

推荐顺序如下：

```text
读取全部候选 -> 按目标状态过滤 -> 稳定排序 -> 取 maxItems + 1 -> 构造 page/hasMore/nextCursor
```

项目清单中的“活动”应定义为 `Status != ProjectStatus.Archived`，因为项目存在 `Draft` 等非归档状态，不能错误地假设项目状态枚举具有 `Available` 或 `Active` 成员。类库活动状态则严格是 `LibraryLifecycleDto.Available`。

归档过程与读取过程之间允许最终显示任一一致快照，无需为本次读取功能引入跨项目、流程和类库的事务。每一个单次列表响应只需保证它自己的结果集、游标和 `hasMore` 来自同一过滤规则。

## 测试与验收矩阵

| 层级 | 场景 | 预期 |
| --- | --- | --- |
| 应用读取模型 | 一个草稿项目和一个归档项目 | 默认项目页只含草稿项目；归档项目页只含归档项目 |
| 应用读取模型 | 项目 ID 排序中归档项位于活动项前，`maxItems=1` | 默认页仍返回一个活动项，且 `hasMore` 与下一游标正确 |
| 应用读取模型 | 一个 `Available` 与一个 `Archived` 类库 | 默认类库页只含 `Available`；归档类库页只含 `Archived` |
| MCP 工具 | 调用四个清单工具 | 工具目录可发现；结果的状态集合分别严格匹配其名称 |
| MCP Resource | 读取四个固定清单 URI | 与同名工具结果一致，且 `resources/list` 可发现新增 URI |
| 项目范围 Key | Key 绑定的项目在归档前后分别读取两类项目清单 | 仅能看见绑定项目，且只会出现在与其状态匹配的清单 |
| 项目范围 Key | 已关联的活动和归档类库 | 两类类库清单均不得泄露未关联工件；各自只出现匹配生命周期的关联工件 |
| 兼容性 | `sereinflow_list_libraries` 未传参数 | 行为保持为只返回活动类库 |
| 兼容性（如保留参数） | `includeArchived=true` | 明确验证为混合全集，并在工具说明中标识为兼容性路径；新归档工具不得返回活动类库 |
| 直接读取 | 通过 ID 读取已归档项目/类库 | 仍成功返回，且状态字段明确为 `Archived` |

现有 `LibraryCatalogTests.ArchiveRetainsTheImmutablePackageForExistingProjectRuns` 已验证类库归档后的默认目录隐藏和底层可查；但当前 `AiReadModelServiceTests` 与 `SereinFlow.Mcp.Tests` 没有覆盖上述 MCP 项目/类库归档分流，必须新增。

## 影响与非目标

本次无需修改项目和类库的持久化模型，也无需新增数据库迁移。`Projects.Status`、类库 `Lifecycle`、类库归档时间和不可变包保留机制已经存在。

不建议修改 HTTP `/api/projects`、`/api/libraries` 的默认语义：前者已默认排除归档项目，后者已默认排除归档类库。也不建议修改 `/api/environment/libraries`，因为前端归档视图依赖其返回全量工件后在界面中分组。

唯一需要明确的产品决策是是否保留按 ID 读取归档对象。本文建议保留，以支持审计与既有引用；若决定收紧，则应另立工作项，对全部按项目 ID 的 MCP 读取链路进行访问策略设计与回归测试。

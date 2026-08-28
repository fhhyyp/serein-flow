# MCP 第一阶段开发计划：只读 AI 基础层

## 执行记录

2026-08-28：已开始实施。已新增 `AiReadModels.cs` 和 `AiReadModelService.cs`，完成项目、流程拓扑、类库、运行、事件、节点输出和调试状态的只读映射；默认隐藏流程字面量和脚本源码，JSON 载荷支持有界解析并区分非法与截断；API 已注册该服务；新增 Application 测试覆盖上述契约。MCP 传输适配尚未接入，等待确定官方 SDK 和认证方案。

## 目标

在不改变现有流程执行、调试、版本和类库升级行为的前提下，建立面向 AI 的只读应用层。第一阶段完成后，MCP 适配器可以稳定读取项目、流程拓扑、版本、类库契约、运行实例、运行定义快照、运行事件、节点输出和调试会话状态。

## 范围

### 本阶段实现

1. 新增独立的 AI 只读契约，避免直接复用前端 UI DTO。
2. 新增项目、流程、节点、连接、类库、运行、事件、输出和调试会话的机器可读摘要。
3. 统一将 JSON 字符串载荷解析为 JSON 元素，并对非法载荷返回结构化诊断。
4. 为事件、输出和节点列表提供有界查询，防止一次响应占满模型上下文。
5. 对脚本源码、输入、输出、错误消息和类库元数据建立敏感信息脱敏与大小限制入口。
6. 新增只读 AI 查询服务，供未来 MCP Resources 和 HTTP API 共同调用。
7. 为只读查询增加 Application 层单元测试和 API 集成测试。

### 明确不在本阶段

- 不实现流程写入、发布、回滚和类库上传 Tool。
- 不实现调试 continue、step、stop Tool。
- 不实现 SereinLang 编译服务。
- 不实现 C# 类库 `dotnet build` 服务。
- 不改变现有流程版本语义。
- 不改变 Worker 协议版本。
- 不直接手写完整 MCP 协议传输层。协议接入使用独立适配器，等待官方 SDK 或确定传输版本后实现。

## 目标契约

建议在 `SereinFlow.Contracts` 中增加以下只读模型：

```text
AiProjectSummaryDto
AiFlowSummaryDto
AiFlowTopologyDto
AiNodeContractDto
AiConnectionDto
AiLibrarySummaryDto
AiLibraryNodeContractDto
AiRunSummaryDto
AiRunTimelineDto
AiNodeExecutionRecordDto
AiDebugStateDto
AiDiagnosticDto
AiPageDto<T>
```

契约要求：

- 所有模型包含 `SchemaVersion` 或由 URI 明确版本。
- 节点、参数、连接、运行和事件均使用稳定 ID。
- 不暴露服务器文件路径、Worker 进程细节、数据库结构和 Secret。
- 大型源码、输入和输出提供摘要、长度和是否截断信息。
- 原始 JSON 仅作为可选详情，不作为主要机器接口。
- 失败返回稳定 `code`、`message`、`path` 和可选 `details`。

## 查询服务

新增 `AiReadModelService`，只依赖 Application Persistence 接口和现有目录服务，不直接依赖 SqlSugar。

建议方法：

```text
GetProjectsAsync
GetProjectAsync
GetFlowTopologyAsync
GetFlowVersionAsync
ListFlowVersionsAsync
GetLibraryAsync
ListLibrariesAsync
GetRunAsync
GetRunDefinitionSnapshotAsync
GetRunTimelineAsync
GetRunOutputsAsync
GetDebugStateAsync
```

查询服务负责：

1. 校验项目、流程、运行和调试会话的归属关系。
2. 将前端 DTO 映射为 AI 只读模型。
3. 将事件 `payloadJson` 和节点输出 JSON 解析为结构化字段。
4. 应用分页、数量上限、字节上限和脱敏策略。
5. 统一处理找不到资源、非法 JSON、版本不匹配和载荷超限。

## 只读 Resource URI

```text
/mcp/v1/resources/projects
/mcp/v1/resources/projects/{projectId}
/mcp/v1/resources/projects/{projectId}/flows/{flowId}/topology
/mcp/v1/resources/projects/{projectId}/flows/{flowId}/versions/{track}
/mcp/v1/resources/projects/{projectId}/flows/{flowId}/versions/{version}
/mcp/v1/resources/libraries
/mcp/v1/resources/libraries/{libraryId}
/mcp/v1/resources/runs/{runId}
/mcp/v1/resources/runs/{runId}/definition-snapshot
/mcp/v1/resources/runs/{runId}/timeline
/mcp/v1/resources/runs/{runId}/outputs
/mcp/v1/resources/debug-sessions/{sessionId}
```

上述路径是应用层适配目标，不代表在 MCP SDK 确定前直接固定最终传输格式。

## 安全要求

第一阶段即使暂时只提供内部适配，也必须保留以下接口边界：

- 查询方法接收调用者上下文，不允许默认跨项目读取。
- 项目和流程资源必须进行归属校验。
- 事件和输出默认脱敏，并限制单项和总响应大小。
- 运行定义快照可读，但不允许通过只读服务修改。
- 原始脚本源码必须显式请求才返回，默认只返回摘要和哈希。
- 查询错误不得暴露 SQLite 路径、完整 CLR 堆栈和 Worker 内部协议细节。

## 测试计划

### Application 单元测试

- 流程拓扑映射保留节点、端口、参数和连接稳定 ID。
- 开发/生产版本资源读取不混淆轨道。
- 运行定义快照与实时运行状态字段不混淆。
- 事件 payload 和节点输入输出 JSON 正确映射。
- 非法 JSON 返回稳定诊断。
- 事件、输出、节点列表超过上限时正确分页或截断。
- 项目、流程、运行、调试会话归属校验生效。

### API 集成测试

- 只读查询返回统一的 JSON 结构。
- 不存在的项目、流程、运行和会话返回稳定错误码。
- 查询不会触发 Worker 启动或程序集加载。
- 既有前端 API 和运行调试测试不回归。

## 完成标准

1. AI 可以通过一个只读查询得到完整流程拓扑，不需要解析前端 UI 字段。
2. AI 可以读取某次运行的流程版本、不可变定义快照、事件时间线和节点输入输出。
3. AI 可以读取当前调试会话的状态，并识别暂停节点和 Flipflop 队列状态。
4. 所有列表和 JSON 详情均有明确上限。
5. 不存在跨项目读取和敏感路径泄露。
6. 只读层不改变当前流程执行、生产版本和调试命令行为。
7. MCP 协议适配可以在不修改 Application 读模型的情况下独立接入。

## 后续阶段入口

第一阶段完成后，按以下顺序继续：

1. 接入只读 MCP Resources。
2. 增加结构化调试状态和等待 Tool。
3. 增加 SereinLang compile Tool。
4. 增加 FlowDefinition validate/normalize/preview Tool。
5. 增加隔离的 C# 类库构建 Worker。

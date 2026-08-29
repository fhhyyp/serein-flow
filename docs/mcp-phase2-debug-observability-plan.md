# MCP 第二阶段开发计划：调试状态可观测性

## 执行记录

2026-08-29：已开始并完成本阶段核心实现。上一阶段基线已提交为 `b11345e`；本阶段已增加结构化暂停状态、最近节点结果、持久化状态修订号、SQLite 迁移 18、Worker 事件回填、等待状态变化 API 和前端类型兼容。Domain、Application、Infrastructure 测试以及 API 编译、前端测试和构建均通过。MCP 传输适配、流程写入和编译构建能力不纳入本阶段。

## 目标

为未来的 MCP Resource、调试面板和 AI 调试助手提供一个稳定的调试观察面。调用方能够知道流程当前是否暂停、暂停在哪个节点、节点执行上下文是什么，以及最近一个节点的输入、输出和结果，而不需要解析前端事件文本或依赖 Worker 内部进程状态。

## 范围

### 本阶段实现

1. 在领域层增加结构化暂停状态：节点 ID、节点类型、步骤、调用帧深度、调用实例 ID、执行边界序号、输入和暂停时间。
2. 在领域层增加最近节点结果：节点输入、输出、完成序号、结果、分支和错误信息。
3. 为调试会话增加单调递增的 `StateRevision`，所有可观察状态变化都推进修订号。
4. 将上述状态以 JSON 持久化，并通过 SQLite 迁移兼容已有数据库。
5. API 暂停事件更新结构化暂停状态，节点完成、失败和错误事件更新最近节点结果。
6. 扩展调试会话 DTO，同时保持已有字段和前端调试控制接口兼容。
7. 增加等待调试状态变化的查询接口：立即返回已变化或终态，否则在有限超时内等待，超时返回当前状态且不误报变化。
8. 增加领域、基础设施和 API/Application 层测试，覆盖重启恢复、迁移、状态修订和等待语义。

### 明确不在本阶段

- 不实现 MCP 协议传输层、认证和授权适配。
- 不实现新的 `continue`、`step`、`stop` MCP Tool；复用已有调试命令接口。
- 不实现流程定义写入、发布、回滚或类库上传。
- 不实现 SereinLang 编译服务和 C# 类库构建 Worker。
- 不修改 Worker 协议版本和执行边界语义。
- 不把完整事件历史复制到调试会话；历史仍由运行事件和输出存储负责。

## 数据契约

### 暂停状态

```text
FlowDebugPauseState
  NodeId
  NodeType
  Step
  FrameDepth
  InvocationId
  BoundarySequence
  InputsJson
  PausedAt
```

### 最近节点结果

```text
FlowDebugNodeResult
  NodeId
  Sequence
  CompletedAt
  Outcome
  Branch
  InputsJson
  OutputsJson
  ErrorCode
  ErrorMessage
```

JSON 字段使用已有序列化选项，读取时保留对象、数组、字符串、数字、布尔值和 null 的原始 JSON 形态。非法历史 JSON 不阻塞会话加载，API 以空对象或结构化诊断兼容返回。

## API 契约

现有 `GET /api/debug-sessions/{sessionId}` 和 `GET /api/runs/{runId}/debug-session` 增加：

```text
stateRevision
pauseState
lastNodeResult
```

新增：

```text
GET /api/debug-sessions/{sessionId}/wait?afterRevision={long}&timeoutSeconds={int}
```

返回：

```text
{
  hasChanged: boolean,
  timedOut: boolean,
  session: FlowDebugSessionDto
}
```

规则：

- `afterRevision` 缺省按 `-1` 处理，传入负数时返回 400。
- `timeoutSeconds` 缺省使用短轮询上限，允许范围为 0 到 60 秒。
- 当前 `StateRevision > afterRevision` 或会话已终态时立即返回。
- 超时返回当前会话，`hasChanged=false`、`timedOut=true`。
- 请求取消立即结束等待，不持有 Worker 句柄，不阻塞 API 线程池。

## 实施顺序

1. 领域模型与 DTO：增加结构化状态及修订号，并保持旧 `Pause` 调用兼容。
2. SQLite 持久化：增加版本 18，更新记录映射和加载容错。
3. Worker 事件接入：把暂停边界和节点结果写回调试会话。
4. 等待服务与 API 路由：实现有界轮询和参数校验。
5. 测试与验证：运行相关测试、编译 API、检查迁移幂等性和工作树差异。

## 完成标准

1. 刷新或重启 API 后仍能读取暂停节点的类型、步骤、帧深度和输入。
2. 调试过程中最近完成节点的输入、输出、分支和错误可直接读取。
3. 每次可观察状态变化具有严格递增的 `StateRevision`，SQLite 重启后保持。
4. 等待接口不会把超时错误报告成状态变化，也不会无限等待。
5. 旧数据库能自动迁移，旧调试测试和现有控制命令不回归。
6. 未来 MCP Resource 可以直接复用 DTO 和 Application 服务，不需要依赖 Worker 进程或前端字段。

## 后续阶段入口

本阶段完成后，优先接入只读 MCP Resource 和调试状态查询适配；之后再评估 SereinLang 编译 Tool、流程校验/预览 Tool 以及隔离类库构建 Worker。

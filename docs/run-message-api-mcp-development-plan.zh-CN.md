# 活动运行消息注入 API/MCP 开发计划

## 1. 目标与结论

本计划在现有 Worker 消息服务基础上，为外部调用方提供统一的“活动运行消息注入”能力：调用方持有一个正在运行的 `runId`，指定 `topic` 并传入 JSON 数据，消息进入该运行的 Worker 本地消息服务，唤醒正在 `await` Queue/EventBus 消息的 Flipflop 节点，随后由 `FlowRunner` 继续执行后继流程。

目标调用链如下：

```text
REST API / MCP Tool
        │ runId + topic + JSON + channelKind
        ▼
IRunMessageDeliveryService
        │ 统一校验、生成 messageId、构造 Worker DTO
        ▼
IWorkerMessageRunClient
        ▼
WorkerSupervisor 活动会话
        │ message.deliver
        ▼
WorkerMessageService（运行级 Broker）
        ▼
Flipflop await 恢复
        ▼
FlowRunner 执行后继连接
```

该能力不会为每次消息重新载入流程，也不会复制一个新的固定端点流程。消息始终投递到 `runId` 对应的现有 Worker 和同一份启动时流程快照。

## 2. 本计划的明确决策

### 2.1 API 不增加权限认证

按当前需求，REST API：

- 不新增 `[Authorize]`；
- 不增加 API Key、Bearer Token、用户身份或角色校验；
- 不检查调用方与项目、流程、运行实例的归属关系；
- 不实现 topic 级调用方权限；
- 保持现有公开路由 `POST /api/runs/{runId}/messages/{topic}`。

`ExternalIngress` 仍然必须保留。它是节点端点是否允许外部消息进入的运行语义开关，不是调用方身份认证。运行状态、JSON 格式、topic、channel kind、contract、容量、超时等校验也继续保留。

MCP HTTP 传输继续遵循 MCP 服务当前已有的连接认证和工具执行框架，本计划不删除或绕过 MCP 基础设施的全局认证。新增工具使用独立的 `run.message.publish` 权限；这只影响 MCP 工具，不影响 REST API。

### 2.2 只面向活动运行

- 外部定位键为 `runId`，不是 `flowId` 或节点 ID。
- 普通运行与调试运行均可投递，只要 Worker 活动会话仍存在。
- Pending、Completed、Failed、Cancelled、TimedOut、Interrupted 等非活动运行拒绝投递。
- 已结束运行不会因为收到消息而恢复，也不会重建 Worker。
- 编辑器中对流程图或断点的后续修改不影响当前运行。

### 2.3 外部载荷只接受 JSON

- API/MCP 不开放 `DirectObject`。
- 不接受 CLR 类型名或程序集限定类型名。
- `contractId` 只是逻辑契约标识，由 Worker 与已注册端点进行字符串匹配。
- JSON 原始结构必须完整保留，包括对象、数组、字符串、数值、布尔值和 `null`。

### 2.4 接收应答不等于业务完成

REST `202 Accepted` 或 MCP `accepted` 只表示消息已进入目标 Worker 的运行级 Broker，不能表示：

- Flipflop 后继节点已经执行完成；
- 整个流程已经成功；
- 业务副作用已经提交。

后续执行状态继续通过运行详情、运行事件、输出和调试状态接口观测。调用方需要端到端关联时，应把业务 `correlationId` 放进 JSON payload；Worker `messageId` 用于投递幂等和协议关联。

## 3. 当前基线

以下能力已经完成，本计划直接复用，不重复实现：

| 能力 | 当前状态 | 主要位置 |
|---|---|---|
| REST 活动运行消息入口 | 已完成 | `SereinFlow.Api/Controllers/RunsController.cs` |
| API 请求 DTO 与 Worker 投递 DTO | 已完成 | `SereinFlow.Contracts/Dtos.cs` |
| 活动运行消息客户端 | 已完成 | `SereinFlow.Worker.Client/IWorkerMessageRunClient` |
| 普通/调试 Worker 消息会话 | 已完成 | `SereinFlow.Worker.Supervisor/WorkerSupervisor.cs` |
| `message.deliver` 协议桥接 | 已完成 | `SereinFlow.Worker.Runner/Program.cs` |
| 运行级 Queue/EventBus | 已完成 | `SereinFlow.Worker.Runner/WorkerMessageService.cs` |
| `IMessageService` 节点库注入 | 已完成 | `WorkerLibraryServiceRuntime` |
| Flipflop 等待恢复和后继调度 | 已完成 | `LibraryNodeExecutor`、`FlowRunner` |
| MCP 发布消息工具 | 未实现 | 本计划重点 |
| REST/MCP 共享应用服务 | 未实现 | 本计划重点 |

当前 REST Controller 已包含业务编排与协议 DTO 构造。为了让 MCP 复用相同行为，需要先把这部分逻辑从 HTTP 层下沉为共享服务。

## 4. 对外合同

### 4.1 REST API

保持现有路由：

```http
POST /api/runs/{runId}/messages/{topic}
Content-Type: application/json
Idempotency-Key: optional
```

请求体：

```json
{
  "payload": {
    "orderId": "A10001",
    "amount": 99.5,
    "correlationId": "biz-20260902-001"
  },
  "messageId": "af46ef89-5712-4dff-a6df-bf4e57f80a7d",
  "contractId": "order.created.v1",
  "channelKind": "Queue"
}
```

规则：

- `payload` 必填，允许 JSON `null`，不允许 `Undefined`；
- `messageId` 可选，提供时必须是 GUID；
- 未提供 `messageId` 时，若 `Idempotency-Key` 是 GUID，直接作为消息 ID；
- 未提供 `messageId` 时，若 `Idempotency-Key` 是普通字符串，使用 `runId + topic + key` 生成稳定 GUID；
- 两者都未提供时生成新 GUID；
- `channelKind` 默认为 `Queue`，支持 `Queue` 和 `EventBus`；
- `topic` Trim 后不能为空，最大 256 字符，不允许 CR/LF；
- `contractId` 可选，但若 Worker 端点声明了契约，则必须一致。

响应保持现有语义：

| HTTP | 场景 |
|---|---|
| `202` | Worker 已接受消息，包括幂等重复消息 |
| `400` | 请求、topic、messageId、channel 或消息内容无效 |
| `403` | 目标端点未启用 `ExternalIngress` |
| `404` | run 不存在，或 Supervisor 中没有对应活动 Worker |
| `409` | run 非 Running，或端点尚未注册/尚未就绪 |
| `429` | 目标有界通道已满 |
| `504` | 等待 Worker accepted/rejected 应答超时 |

不新增 `401` 或基于身份的 `403`。

### 4.2 MCP 工具

新增 mutation 工具：

```text
sereinflow_publish_run_message
```

输入 schema：

```json
{
  "runId": "8e0f2b1e-7e9c-4e8e-b5d0-2f4d8d6f21a8",
  "topic": "order.created",
  "payload": {
    "orderId": "A10001",
    "amount": 99.5
  },
  "channelKind": "queue",
  "contractId": "order.created.v1",
  "messageId": "af46ef89-5712-4dff-a6df-bf4e57f80a7d",
  "idempotencyKey": "agent-call-20260902-001"
}
```

字段规则：

- 必填：`runId`、`topic`、`payload`、`idempotencyKey`；
- 可选：`channelKind`、`contractId`、`messageId`；
- `payload` schema 必须允许任意 JSON 值，不能限定为 object，以支持数组、字符串、数值、布尔值和 `null`；
- `channelKind` 使用稳定字符串 `queue`、`eventBus`，解析时兼容大小写；
- 工具声明为 `Mutation` 且 `requiresIdempotencyKey: true`；
- `McpMutationGate` 只负责宿主进程内串行化；Handler 还必须使用现有 `McpIdempotencyService`/`IMcpIdempotencyStore` 读取和保存结果，才能在重试时返回原结果而不再次投递；
- Worker 层继续使用稳定 `messageId` 防止同一消息重复进入 Broker。

成功输出直接返回结构化的 `WorkerMessageDeliveryResponseDto` 等价结果：

```json
{
  "status": "accepted",
  "runId": "8e0f2b1e-7e9c-4e8e-b5d0-2f4d8d6f21a8",
  "messageId": "af46ef89-5712-4dff-a6df-bf4e57f80a7d",
  "topic": "order.created",
  "duplicate": false,
  "code": null,
  "message": null
}
```

MCP 失败使用稳定错误码：

| 错误码 | 含义 |
|---|---|
| `run.not_found` | 找不到运行实例 |
| `worker.not_active` | 运行不是活动 Running 状态 |
| `worker.not_found` | Supervisor 中不存在活动 Worker 会话 |
| `message.topic_invalid` | topic 无效 |
| `message.payload_required` | 缺少 JSON payload |
| `message.channel_invalid` | channelKind 无效 |
| `message.id_invalid` | messageId 无效 |
| `message.endpoint_not_ready` | Worker 尚未注册目标端点 |
| `message.endpoint_forbidden` | 端点未启用 ExternalIngress |
| `message.contract_mismatch` | contractId 不匹配 |
| `message.channel_full` | 通道已满 |
| `message.delivery_timeout` | Worker 应答超时 |
| `message.rejected` | 其他 Worker 拒绝原因 |

## 5. 共享应用服务设计

### 5.1 契约

在 `SereinFlow.Application` 增加不依赖 ASP.NET Core 的用例契约：

```csharp
public sealed record RunMessageDeliveryCommand(
    Guid RunId,
    string Topic,
    JsonElement Payload,
    string? MessageId,
    string? IdempotencyKey,
    string? ContractId,
    WorkerMessageChannelKindDto ChannelKind);

public interface IRunMessageDeliveryService
{
    Task<RunMessageDeliveryResult> DeliverAsync(
        RunMessageDeliveryCommand command,
        CancellationToken cancellationToken = default);
}
```

`RunMessageDeliveryResult` 应携带：

- 成功或失败分类；
- `WorkerMessageDeliveryResponseDto`；
- 稳定错误码；
- 面向调用方的双语消息；
- 可映射 HTTP 状态的 disposition，但不得直接依赖 `IActionResult`。

### 5.2 实现位置与依赖方向

建议在 `SereinFlow.Api` 的执行组合层实现 `RunMessageDeliveryService`，原因是该实现同时依赖：

- `IFlowRunStore`；
- `IWorkerMessageRunClient`；
- Worker Protocol 版本；
- JSON 序列化选项。

`SereinFlow.Application` 只保留接口和命令/结果，不引用 Worker Client。`SereinFlow.Mcp` 只依赖应用接口，不直接引用 `SereinFlow.Worker.Client`。API 主机和 MCP stdio 主机都已经调用 `AddSereinFlowExecution`，在该注册方法中注册实现即可。

建议文件：

```text
src/SereinFlow.Application/RunMessageDeliveryContracts.cs
src/SereinFlow.Api/RunMessageDeliveryService.cs
src/SereinFlow.Api/SereinFlowExecutionRegistration.cs
```

### 5.3 统一处理顺序

`DeliverAsync` 固定按以下顺序执行：

1. 根据 `runId` 查询运行；
2. 判断运行必须为 `Running` 且非终态；
3. Trim 并校验 topic；
4. 校验 payload 已定义；
5. 校验 channel kind；
6. 解析或生成 `messageId`；
7. 使用 Web JSON 配置序列化 `JsonElement`；
8. 构造 `WorkerMessageDeliveryDto`；
9. 调用 `IWorkerMessageRunClient.DeliverMessageAsync`；
10. 把 Worker 状态和错误码映射为统一结果。

Controller 和 MCP Handler 不再重复上述业务规则，只负责协议输入解析及输出映射。

## 6. 分阶段实施

### 阶段 1：抽取共享消息投递服务

- 新增 `RunMessageDeliveryCommand`、`RunMessageDeliveryResult` 和 `IRunMessageDeliveryService`；
- 将 `RunsController.DeliverMessage` 中的运行查询、校验、消息 ID 解析、DTO 构造和 Worker 调用迁入服务；
- 将 `ResolveMessageId` 迁出 Controller，并为普通字符串幂等键保留稳定散列行为；
- 在 `AddSereinFlowExecution` 注册服务；
- Controller 只负责从 route/body/header 构造命令并映射 HTTP 结果；
- 保持现有 REST 路由、请求体和响应兼容；
- 不添加任何 API 认证或授权代码。

阶段验收：现有 OpenAPI 路径不变，现有 REST 消息投递测试全部通过。

### 阶段 2：增加 MCP 权限与工具合同

- 在 `McpPermissionDto` 增加 `RunMessagePublish`；
- 稳定名称映射为 `run.message.publish`；
- 更新权限 JSON 转换、API Key 管理和管理员全权限测试；
- 在 `SereinFlowMcpToolCatalogFactory` 增加 `sereinflow_publish_run_message` schema；
- 将工具声明为带幂等键的 mutation；
- catalog permission 使用 `McpPermissionDto.RunMessagePublish`；
- 不把该能力归入 `DebugControl`，确保业务运行也能独立授权。

阶段验收：工具能出现在 `tools/list`，schema 必填字段、mutation 属性、幂等要求和权限均正确。

### 阶段 3：实现 MCP Handler

- 新增 `McpRunMessageToolHandlers`；
- 严格解析 `runId`、topic、payload、channelKind、contractId、messageId 和 idempotencyKey；
- 使用 `IRunMessageDeliveryService` 投递，不直接操作 Supervisor 或 Worker Transport；
- 查询 run 后按其 `ProjectId` 执行 MCP 项目范围权限检查；
- Accepted 返回结构化结果；
- Rejected、NotFound、NotReady、TimedOut 转换为稳定 MCP 工具错误；
- 错误内容不得包含 Worker 命令行、文件路径或原始协议帧。

阶段验收：HTTP MCP 与 stdio MCP 都能向普通运行及调试运行发送消息。

### 阶段 4：端到端验证 Flipflop 后继执行

现有 Worker 集成测试已经验证消息能够唤醒等待端点。新增端到端场景必须进一步证明后继流程被执行：

```text
Flipflop: await queue.ReceiveAsync<OrderCreated>("order.created")
    → Action: 读取 Flipflop data-out
    → Action: 写入可查询输出
    → Run/事件断言
```

覆盖：

- REST → Queue Flipflop → 后继 Action；
- REST → EventBus Flipflop → 后继 Action；
- MCP → Queue Flipflop → 后继 Action；
- MCP → 调试运行中等待的 Flipflop → 命中后继断点或完成后继节点；
- 相同 messageId 重复投递只产生一次 Broker 消息；
- 相同 MCP idempotencyKey 重试返回相同工具结果；
- run 已结束后 REST/MCP 均拒绝且不重建 Worker；
- 端点尚未注册时返回 not-ready；
- ExternalIngress=false 时拒绝；
- contract 不匹配、channel 不匹配、通道满和 Worker 应答超时。

阶段验收：不仅收到 `accepted`，而且运行事件或输出能够证明后继节点真实执行。

### 阶段 5：文档与示例

- 更新 `worker-message-service.zh-CN.md`，补充 REST 和 MCP 调用示例；
- 更新 `mcp-readonly-server.md` 中的工具清单、权限和 mutation 说明；
- 更新 OpenAPI 集成测试；
- 在测试节点库保留一个可运行的 ExternalIngress Flipflop 示例；
- 明确说明流程快照、消息接收应答、Queue/EventBus 语义和运行生命周期。

## 7. 预计修改文件

### 新增

```text
src/SereinFlow.Application/RunMessageDeliveryContracts.cs
src/SereinFlow.Api/RunMessageDeliveryService.cs
src/SereinFlow.Mcp/Tools/McpRunMessageToolHandlers.cs
tests/SereinFlow.Api.IntegrationTests/RunMessageDeliveryServiceTests.cs
tests/SereinFlow.Mcp.Tests/McpRunMessageToolTests.cs
```

### 修改

```text
src/SereinFlow.Api/Controllers/RunsController.cs
src/SereinFlow.Api/SereinFlowExecutionRegistration.cs
src/SereinFlow.Contracts/McpMutationContracts.cs
src/SereinFlow.Mcp/Tools/SereinFlowMcpToolCatalogFactory.cs
tests/SereinFlow.Api.IntegrationTests/McpHostIntegrationTests.cs
tests/SereinFlow.Worker.IntegrationTests/WorkerSupervisorTests.cs
tests/SereinFlow.TestLibrary/TestLibraryNodes.cs
docs/worker-message-service.zh-CN.md
docs/mcp-readonly-server.md
```

实际测试文件可按现有测试分层调整，但不得把 MCP 协议测试和 Worker Broker 单元测试混在同一测试类。

## 8. 测试矩阵

| 层级 | 必测内容 |
|---|---|
| 消息投递服务测试 | 输入校验、messageId 生成、运行状态、Worker 响应映射 |
| API 集成测试 | 路由、请求绑定、HTTP 状态、OpenAPI、无认证要求 |
| MCP 工具测试 | tools/list、schema、权限、幂等、结构化成功与错误结果 |
| Worker 集成测试 | endpoint 注册、Queue/EventBus 投递、重复消息、容量与超时 |
| 运行时端到端测试 | Flipflop await 恢复、data-out 传递、后继节点执行 |
| 调试端到端测试 | 调试 Worker 消息投递、暂停/继续/后继断点行为 |

建议验证命令：

```powershell
dotnet test tests/SereinFlow.Application.Tests/SereinFlow.Application.Tests.csproj
dotnet test tests/SereinFlow.Mcp.Tests/SereinFlow.Mcp.Tests.csproj
dotnet test tests/SereinFlow.Worker.IntegrationTests/SereinFlow.Worker.IntegrationTests.csproj
dotnet test tests/SereinFlow.Api.IntegrationTests/SereinFlow.Api.IntegrationTests.csproj
```

## 9. 完成标准

全部满足后才视为本计划完成：

1. REST 现有接口保持兼容，且不要求 API 身份认证或权限；
2. MCP `tools/list` 能发现 `sereinflow_publish_run_message`；
3. MCP 调用可以向指定活动 `runId` 发布 JSON；
4. 普通运行和调试运行均能接收；
5. Queue/EventBus、contractId 和 ExternalIngress 语义正确；
6. Flipflop 的 `await` 被唤醒后，后继节点真实执行；
7. 重复调用不会造成意外的重复投递；
8. 已结束运行不会被复活，也不会按消息重新载入流程；
9. REST 与 MCP 使用同一共享服务，不存在两套校验和错误映射；
10. 相关测试全部通过，文档与 OpenAPI/MCP schema 同步。

## 10. 暂不纳入

- API 身份认证、用户权限、项目归属或租户隔离；
- 动态添加/删除调试断点；
- 运行中替换流程图、节点程序集或 DI 依赖；
- 向已结束运行补发并重放消息；
- 跨 Worker、跨流程的持久化消息总线；
- RabbitMQ、Kafka 等外部 Broker 适配；
- 等待整个后继流程完成后再同步返回 API/MCP；
- 自动把所有内部 topic 暴露为外部入口；
- 基于 JSON Schema 的服务端业务字段验证。

这些能力与“向活动运行注入消息”是不同层级的问题，应在基础链路稳定后单独规划。

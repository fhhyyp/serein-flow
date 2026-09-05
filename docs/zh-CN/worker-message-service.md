# Worker 消息服务

Worker 每次运行创建一个独立的内存消息服务，并把同一个 `IMessageService` 单例注入该运行加载的节点类库。消息服务不跨运行持久化，也不会因为 Worker 退出而保留消息。

## SDK 语义

SDK 契约位于 `SereinFlow.Library`，节点类库只依赖接口，不依赖 Runner 或传输实现：

```csharp
public interface IMessageService
{
    IMessageQueue CreateMessageQueue(MessageChannelOptions? options = null);
    IEventBus CreateEventBus(MessageChannelOptions? options = null);
}
```

`MessageQueue` 是按 topic 的竞争消费队列：多个消费者共享一条 FIFO 流，每条消息只交付给一个消费者。`EventBus` 为每个订阅者提供独立缓冲；发布时已经存在的订阅者各收到一份，晚订阅者不会收到历史消息。

通道默认使用 JSON 模式。`DirectObject` 只适用于同一 Worker 进程内的对象引用传递，接收时要求对象可赋值给目标类型；跨隔离加载上下文或外部 API 的消息应使用 JSON。建议传递不可变 DTO。

每个 topic 都是有界的，选项包括：

- `Capacity`：缓冲容量，必须为正数；
- `OverflowStrategy`：`Reject`、`DropOldest` 或 `DropNewest`；
- `MessageTtl`：可选的消息有效期；
- `MaxPayloadBytes`：JSON 载荷大小上限；
- `ContractId`：可选的逻辑契约标识；
- `ExternalIngress`：是否允许 Supervisor/API 进行外部投递，默认关闭。

所有发送、接收和订阅等待都接受 `CancellationToken`。运行取消、超时或 Worker 关闭时，等待会被唤醒并释放底层订阅。

## 节点入口声明

只有显式设置 `ExternalIngress = true` 的入口会向 Supervisor 注册。下面的 FlipFlop 入口接收 `example.text` 契约的 JSON 字符串：

```csharp
public sealed class MessageNodes(IMessageService messageService)
{
    public async Task<string> ReceiveExternal(IFlowContext context)
    {
        var queue = messageService.CreateMessageQueue(new MessageChannelOptions
        {
            SerializationMode = MessageSerializationMode.Json,
            ExternalIngress = true,
            ContractId = "example.text",
            Capacity = 8
        });

        return await queue.ReceiveAsync<string>("example.inbox", context.CancellationToken);
    }
}
```

注册信息包含 channel kind、topic、序列化模式和 `contractId`。内部 topic 不会自动暴露；入口声明也不能让外部请求指定程序集限定类型。

## 外部 API

活动普通运行和调试运行都支持：

```http
POST /api/runs/{runId}/messages/{topic}
Idempotency-Key: optional-client-key
Content-Type: application/json
```

请求体：

```json
{
  "payload": { "value": "hello" },
  "messageId": "optional-guid",
  "contractId": "example.text",
  "channelKind": "Queue"
}
```

API 只接受 JSON payload，并校验运行状态、topic、`messageId`/`Idempotency-Key`、大小、入口注册状态、`ExternalIngress` 和 `contractId`。`payload` 可以是对象、数组、字符串、数值、布尔值或 JSON `null`。

`messageId` 提供时必须是非空 GUID。未提供时，GUID 格式的 `Idempotency-Key` 直接作为消息 ID；普通字符串会按 `runId + topic + key` 生成稳定 GUID；两者都没有时生成新 GUID。`channelKind` 默认是 `Queue`，也可以是 `EventBus`。

返回 `202 Accepted` 表示消息已进入 Worker 本地 Broker，不表示下游流程已经执行完成；它只确认 Worker 接收应答，不能代表 Flipflop 后继节点、整个流程或业务副作用已经完成。流程执行结果继续通过运行事件、输出和最终运行状态观察。消息始终投递到 `runId` 对应的活动 Worker 及其启动时流程快照；运行结束后不会重建 Worker 或恢复运行。

常见结果包括：

| HTTP | 含义 |
| --- | --- |
| `202` | 已接受；重复的 message ID 仍返回接受但标记 duplicate |
| `404` | 运行不存在或 Supervisor 中没有对应活动 Worker 会话 |
| `409` | 运行已结束、入口尚未注册或消息会话不可用 |
| `403` | 入口未开放外部投递 |
| `429` | 有界通道已满 |
| `504` | Worker 未在投递超时内应答 |

协议桥接沿用 Worker 现有的唯一接收循环和串行发送路径。消息服务不会直接操作 `StdioWorkerTransport`，因此未来替换为其他 `IWorkerTransport` 时，节点 SDK 和 Broker 语义不需要变化。

## MCP 发布工具

通过已认证的 HTTP MCP 或显式配置 API key 的 stdio MCP，可以使用
`sereinflow_publish_run_message` 向活动普通运行或调试运行发布消息：

```json
{
  "runId": "8e0f2b1e-7e9c-4e8e-b5d0-2f4d8d6f21a8",
  "topic": "order.created",
  "payload": { "orderId": "A10001", "amount": 99.5 },
  "channelKind": "queue",
  "contractId": "order.created.v1",
  "idempotencyKey": "agent-call-20260902-001"
}
```

该工具要求 `run.message.publish` 权限和非空 `idempotencyKey`，并按 MCP 主体与请求内容持久化结果。相同主体、工具和幂等键重试时返回原结构化结果，不会再次调用 Worker；Worker 自身还会按 `messageId` 在运行级 Broker 中去重。MCP 返回的 `accepted` 同样只表示 Broker 接收，不表示后继业务已完成。

## 第一版边界

当前实现不提供跨 Worker Broker、持久化/重放、消费确认、运行结束后的恢复、运行中替换流程或程序集，也不会把所有内部事件自动推送给 API。

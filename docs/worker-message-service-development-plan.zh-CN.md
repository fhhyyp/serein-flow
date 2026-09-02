# Worker 消息服务开发计划

## 1. 结论

该需求可以实现，且与当前 SereinFlow 的 Worker/FlipFlop 模型匹配。不过不建议把 `IMessageService` 直接“塞进” `StdioWorkerTransport`。两者职责应明确分层：

```text
外部 API
   │ 受控的消息入口、鉴权、幂等、应答
   ▼
Worker 运行会话控制层
   │ 通过 IWorkerTransport 发送协议消息
   ▼
Worker 协议消息桥接层
   │ 单一 ReceiveAsync 循环内路由
   ▼
Worker 运行级 MessageBroker
   ├─ MessageQueue：竞争消费
   └─ EventBus：广播订阅
        │
        ▼
流程节点库中的 FlipFlop / 普通节点
```

`StdioWorkerTransport` 只负责消息帧的传输、关闭和收发串行化；未来切换 Named Pipe、Unix Domain Socket 或其他传输时，消息服务和节点库不应改变。

当前 Worker 已经具备“一个 Worker 由一个 Supervisor 管理、一个接收循环统一消费传输消息”的约束，这不会阻碍实现，但要求所有新增消息都接入现有路由，不能为消息服务再启动第二个 `ReceiveAsync` 消费者。

本计划默认不兼容仍向调用方暴露原始 `Stream` 的旧调用方式。调用方统一迁移到 `IWorkerTransport`；`StdioWorkerTransport` 仍可作为一种具体实现存在，但不再为旧的 `Stream` API 保留兼容适配。

## 2. 需求拆解与可行性边界

### 2.1 Worker 内部消息通信：可直接实现

每次 Worker 运行创建一个独立的内存 Broker，并将同一个 `IMessageService` 实例注入该运行内所有节点类库的服务容器。这样不同类库之间可以通过稳定的 SDK 契约传递消息，而不需要自行约定管道、Socket 或序列化协议。

- MQ 按 `(运行范围, topic)` 建立有界 FIFO 队列；多个消费者竞争消费，每条消息只交付给一个等待者。
- EventBus 为每个订阅者建立独立游标/通道；一次发布复制到所有当时有效的订阅者。
- 队列默认只在当前 Worker 运行期有效，Worker 退出后消息丢失；这不是 RabbitMQ/Kafka 等持久化消息系统。
- 每个阻塞操作必须接受 `CancellationToken`，运行取消、超时和 Worker 关闭时应唤醒所有等待者。
- 队列必须有容量和溢出策略，至少支持拒绝、丢弃最旧消息或丢弃最新消息，不能默认无限增长。

### 2.2 FlipFlop 监听：可自然接入

当前全局 FlipFlop 的执行器本来就是“等待触发器，再执行下游，继续等待”的循环。因此 `await queue.ReceiveAsync<T>(topic, cancellationToken)` 或事件订阅的 `NextAsync` 可以作为 `WaitForTriggerAsync` 的实现，不需要改造为轮询。

建议将用户示例中的 `GetAsync` 作为兼容性较差的命名调整为 `ReceiveAsync`，并且将 `SubAsync` 设计为显式订阅生命周期：

```csharp
await using var subscription = messageService
    .CreateEventBus(MessageChannelOptions.Json)
    .Subscribe<T>("type_name", cancellationToken);

await foreach (var value in subscription.ReadAllAsync(cancellationToken))
{
    // 每个事件触发一次 FlipFlop 下游
}
```

如果必须保留 `SubAsync<T>`，应明确它是“一次性订阅并等待下一条事件，收到后自动取消订阅”，且订阅前已经发布的事件不补发。

### 2.3 `useJson`：可实现，但必须明确类型边界

建议内部使用可扩展的枚举而非只保留 `bool`：

```csharp
public enum MessageSerializationMode
{
    DirectObject,
    Json
}
```

可以额外提供 `CreateMessageQueue(bool useJson)` 作为便捷重载，但协议和配置最终都转换为枚举。

- `Json`：以 UTF-8 JSON 或等价的不可变 JSON 表示保存消息，在接收者侧使用统一的 `System.Text.Json` 配置反序列化为目标 `T`。这是跨类库、Worker 与 API 之间的默认模式。
- `DirectObject`：仅限同一 Worker 进程内传递对象引用/对象实例；接收时执行 `message is T` 的可赋值性检查，失败返回明确的类型不匹配异常，不做不安全的反射转换。
- 不允许把程序集限定类型名直接交给外部 API 反序列化。外部入口使用逻辑 `topic` 和可选的受控 `contractId/schemaId`，由服务器维护类型映射和大小限制。
- 不同可回收程序集加载上下文中，即使类名和命名空间相同，CLR 类型也可能不是同一个类型；这正是 `DirectObject` 可能失败、而 `Json` 可以解决的场景。
- `DirectObject` 传递可变对象时不提供跨线程修改安全保证，SDK 文档应建议使用不可变 DTO。

### 2.4 通过 WorkerTransport 暴露给 API：可实现，但不是普通运行当前已有能力

外部 API 不应直接持有并操作 `StdioWorkerTransport`。实际链路应是：

```text
API Controller
  → WorkerRunSessionRegistry 中的活动会话句柄
  → WorkerSupervisor 的串行发送队列
  → IWorkerTransport
  → Worker 协议桥接器
  → 本地 MessageBroker
```

当前普通 `RunAsync` 主要返回最终结果，API 没有可长期持有的运行控制句柄；调试运行才有继续、单步和停止句柄。因此需要先把普通运行也纳入统一的活动会话注册表，至少提供向活动 Worker 投递消息的能力。

外部触发只对以下情况成立：

1. 目标运行仍然存活；
2. Worker 已注册目标入口，且该入口允许外部投递；
3. 请求通过 `runId + endpoint/topic` 的授权校验；
4. Worker 尚未取消、超时或进入关闭阶段。

消息“已被 Worker 接收”与流程“已执行完成”必须区分。API 首先返回 `accepted/delivered/rejected` 等接收状态；下游节点完成情况通过已有运行事件或独立的 invocation/correlation id 追踪。

### 2.5 “任意端点调试”：部分可实现

消息入口可以让外部 API 在不重启活动 Worker 的情况下触发某个已加载流程中的 FlipFlop，也可以用于发送调试输入、测试分支和注入外部事件。

但它不能在不重启的情况下：

- 替换流程图或节点拓扑；
- 重新加载已经退出或崩溃的 Worker；
- 替换节点程序集、构造函数依赖或断点配置；
- 让一个已结束的普通运行重新恢复其内存消息队列。

因此产品层应将其命名为“活动运行的远程消息注入/端点调试”，而不是无限制的任意端点调试。

## 3. 建议的公开 API

SDK 契约放在 `SereinFlow.Library`，实现留在宿主/Worker 工程中，避免节点类库引用 Worker 具体实现：

```csharp
public interface IMessageService
{
    IMessageQueue CreateMessageQueue(MessageChannelOptions? options = null);
    IEventBus CreateEventBus(MessageChannelOptions? options = null);
}

public interface IMessageQueue
{
    ValueTask SendAsync(string topic, object message, CancellationToken cancellationToken = default);
    ValueTask<T> ReceiveAsync<T>(string topic, CancellationToken cancellationToken = default);
}

public interface IEventBus
{
    ValueTask PublishAsync(string topic, object message, CancellationToken cancellationToken = default);
    IEventSubscription<T> Subscribe<T>(string topic, CancellationToken cancellationToken = default);
}
```

实际命名可以保留用户提出的 `SendAsync`、`PubAsync`、`GetAsync`、`SubAsync` 别名，但底层语义必须固定。`MessageChannelOptions` 至少包含序列化模式、容量、溢出策略和消息过期时间；外部可见入口还需要 `ExternalIngress` 或等价的显式声明，避免任何内部 topic 自动暴露给 API。

多个 `IMessageService`/Queue/EventBus 对象必须共享同一个运行级 Broker，而不是每次 `Create...` 都产生互相隔离的消息空间。

## 4. Worker 协议设计

建议将跨 Worker 的消息数据面定义为 Worker Protocol v2。虽然新增消息种类可以做成 v1 的附加枚举，但当前握手没有完整的能力协商，旧 Runner 对新消息可能只会拒绝或忽略；这是协议语义扩展，直接升版本更安全。由于已明确不要求兼容旧调用方，v2 可以删除旧的 Stream 调用入口。

协议至少需要：

- `message.deliver`：Supervisor/API → Worker，投递外部入口消息；
- `message.accepted`：Worker → Supervisor，表示已进入本地 Broker；
- `message.rejected`：Worker → Supervisor，表示 topic、类型、容量、生命周期或授权策略拒绝；
- `message.register` / `message.unregister`：Worker → Supervisor，报告活动节点声明的可用入口；
- 可选的 `message.published`：Worker → Supervisor，仅用于明确声明需要向宿主/API 输出的消息，内部消息默认不自动外发。

Envelope 应包含 `messageId`、`runId`、`topic`、channel kind、serialization mode、contract/schema id、payload、创建时间和过期时间。`messageId` 用于去重；发送、接收、执行三类状态要有独立的 correlation id。所有协议消息继续通过 Supervisor/Runner 现有的单一接收循环和串行写入路径处理。

## 5. 分阶段开发计划

### 阶段 0：冻结语义和边界

- 确认 topic 命名、运行范围、JSON 选项、容量与溢出策略。
- 明确 MQ 为竞争消费、EventBus 为广播消费。
- 明确事件不重放、消息是否过期、失败后是否丢失；第一版建议采用运行内 at-most-once 交付。
- 固化“外部入口必须显式授权，内部 topic 不自动暴露”的安全规则。
- 将旧 `Stream` 调用方迁移要求写入升级说明，不保留兼容重载。

交付物：SDK 接口草案、消息语义文档、协议 v2 草案和错误码表。

### 阶段 1：实现运行级内存 Broker 和 SDK

- 在 `SereinFlow.Library` 增加公开契约、选项、Envelope/异常模型。
- 在 Worker 运行时实现有界 MQ、EventBus、订阅取消和运行销毁。
- 使用 `System.Threading.Channels` 或等价线程安全原语，确保每个 topic 的 FIFO 和每个事件订阅者的独立缓冲。
- 加入 JSON 序列化选项、payload 大小限制、类型不匹配错误和日志字段。
- 编写纯单元测试：并发发送/接收、竞争消费者、广播、无订阅丢弃、容量、取消、Worker 结束、DirectObject 类型失败、JSON 跨类型加载上下文。

### 阶段 2：接入 Worker DI 和 FlipFlop

- `WorkerLibraryServiceRuntime` 为每个类库的隔离 DI 容器注册同一个宿主 `IMessageService` 单例。
- 仅白名单开放该 SDK 服务，继续阻止节点库通过 `IServiceProvider` 等方式取得任意宿主对象。
- 让全局 FlipFlop 执行器将 MessageQueue/EventBus 的等待操作接入现有 `WaitForTriggerAsync` 生命周期。
- 验证多个节点类库可以共享 JSON 消息；验证 DirectObject 在不兼容类型时返回可诊断错误。
- 增加 Worker 集成测试：消息触发 FlipFlop、取消等待、运行结束时释放订阅。

### 阶段 3：实现协议 v2 消息桥接

- 在 Contracts 中增加 v2 握手、消息 Envelope、注册、投递和应答 DTO。
- Runner 侧在既有接收循环中将 `message.deliver` 写入运行级 Broker，并返回 accepted/rejected。
- Supervisor 侧为消息请求维护 correlation/messageId 状态，沿用现有串行发送和关闭处理。
- 增加协议错误、超时、版本不匹配、payload 超限和重复消息的测试。
- 确保传输层只暴露 `IWorkerTransport`，节点 SDK 不引用 `StdioWorkerTransport`。

### 阶段 4：统一普通运行与调试运行的活动会话

- 抽象 `IWorkerRunSessionHandle`，同时覆盖普通运行和调试运行的 Completion、取消及消息投递。
- 在 Supervisor/运行服务中维护 `runId → 活动会话` 注册表，运行结束、Worker 退出或宿主取消时原子移除。
- 保留现有调试 Continue/Step/Stop 能力，让调试会话在同一会话句柄上增加消息入口。
- 处理并发停止与投递、Worker 已退出、发送队列关闭和 API 请求超时。

### 阶段 5：外部 API 触发入口

- 新增类似 `POST /api/runs/{runId}/messages/{topic}` 的受控入口；必要时使用稳定 `endpointId` 代替直接暴露 topic。
- 请求体只接受 JSON 模式，并做 schema、大小、topic、运行归属和授权检查。
- 只允许 Worker 已注册且显式标记 `ExternalIngress` 的入口；未注册返回明确的 not-ready/forbidden 错误。
- 支持客户端 `messageId` 或 `Idempotency-Key`，服务端保留有界 TTL 去重记录。
- 返回“已接受/已拒绝/Worker 不存在/入口未就绪”等状态，不把 API 请求同步阻塞到整个流程下游完成。
- 增加 API、Supervisor、Runner、Broker 的端到端测试。

### 阶段 6：可观测性和运行保护

- 记录 runId、nodeId、topic、messageId、correlationId、队列深度、投递延迟和拒绝原因。
- 增加每运行/每 topic 的容量、消息大小、并发等待者和入口速率限制。
- 在 Worker 关闭时取消所有等待并清理订阅；在 API 侧区分活动运行、已完成运行和失联运行。
- 补充升级文档、SDK 示例、协议文档和旧 Stream API 的迁移说明。

## 6. 验收标准

完成第一版后，应至少满足：

1. 两个隔离类库通过同一个 Worker 运行级 `IMessageService` 进行 JSON 消息传递。
2. 多个 MQ FlipFlop 只由一个消费者收到每条消息；多个 EventBus 订阅者都能收到发布时刻之后的事件。
3. Worker 取消或退出时，所有 `ReceiveAsync`/订阅等待都能及时结束，不遗留后台任务。
4. 外部 API 能向活动运行中已注册的 FlipFlop 入口投递 JSON 消息，并获得明确的接收应答。
5. 外部 API 不能向未授权 topic 投递，不能指定任意程序集类型进行反序列化。
6. 普通运行和调试运行都支持消息投递；运行结束后投递会被拒绝且不会复活旧运行。
7. 全部 Worker 通信仍经过唯一接收循环，切换非 stdio 的 `IWorkerTransport` 时消息层无需改动。

## 7. 暂不纳入第一版

- 跨 Worker/跨流程的全局 Broker；
- Worker 重启后的消息持久化、重放和消费确认；
- RabbitMQ/Kafka 等外部 Broker 适配；
- 运行中替换流程图或程序集；
- 将所有 Worker 内部发布事件自动推送给 API。

这些能力可以在后续把 `IMessageBroker` 抽象为可插拔实现后增加，但不应与第一版的运行内消息和远程入口混在一起，否则会显著扩大一致性、权限和故障恢复范围。


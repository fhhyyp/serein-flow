# Worker 本机传输层抽象与 Named Pipe/UDS 迁移计划

## 1. 目标

消除 Worker 通过 `stdin/stdout` 传递控制协议时对标准流的依赖，同时保持现有 Worker 隔离模型、运行生命周期、调试能力和 MCP/API 行为不变。

目标传输优先级：

1. Windows 使用 Named Pipe。
2. Linux/macOS 使用 Unix Domain Socket（UDS）。
3. 在本机 IPC 不可用或显式配置时回退到现有 stdio。

本计划不引入 Worker 内部 WebSocket，也不把 Worker 控制协议暴露为网络服务。

## 2. 非目标

- 不改变 `WorkerMessage` 信封格式和现有 Worker Protocol v1 业务消息。
- 不改变 API、MCP 工具、SignalR、运行队列和调试会话 API。
- 不改变一次运行一个隔离 Runner 进程的模型。
- 不实现 API 重启后重新接管已有 Worker。
- 不在 Worker 中加载 SQLite、API 服务或 MCP 服务。
- 不创建新的测试项目；验证复用现有测试项目和手工/集成验证入口。

## 3. 当前边界

当前协议消息由 `Worker.Protocol` 负责编码和校验，Supervisor 负责启动进程、握手、事件读取、心跳、取消和进程树回收，Runner 负责执行流程并写出事件。

需要保留的语义包括：

- ready → handshake → handshake accepted → run 的启动顺序；
- 单条消息大小限制和协议版本检查；
- Worker 事件 sequence 严格递增；
- Supervisor 独占一个读通道和一个串行写通道；
- deadline、主动取消和取消宽限期；
- 调试 `continue`、`step`、`stop` 及正整数 `commandSequence`；
- Runner 关闭、IPC 断开和进程异常退出的稳定错误分类；
- stderr 继续作为诊断通道，不混入控制协议。

## 4. 目标架构

```text
Worker.Protocol
  ├─ WorkerMessage / WorkerProtocolCodec
  ├─ IWorkerTransport
  ├─ StdioWorkerTransport
  ├─ NamedPipeWorkerTransport
  └─ UnixDomainSocketWorkerTransport

Worker.Supervisor
  └─ WorkerTransportFactory
       └─ 启动 Runner + 创建/管理本机 IPC 端点

Worker.Runner
  └─ 根据启动参数连接 Supervisor
       └─ RunnerHost.RunAsync(IWorkerTransport)

Worker.Client / Api / MCP
  └─ 继续依赖现有 IWorkerRunClient / IWorkerDebugRunClient
```

`WorkerMessage` 继续作为协议层对象，但传输层不再假设“一个 JSON 对象等于一行文本”。不同传输实现负责自己的消息边界：

- stdio：一行一个 JSON 消息；
- Named Pipe：长度前缀或等价的受限消息帧；
- UDS：长度前缀或等价的受限消息帧。

协议层统一执行 UTF-8、最大消息大小、反序列化和版本校验。

## 5. 分阶段开发任务

### 阶段一：抽象传输接口

目标：在不改变默认 stdio 行为的前提下，切断 Supervisor/Runner 对 `StreamReader` 和 `Stream` 的直接依赖。

任务：

- 在 `src/SereinFlow.Worker.Protocol/` 增加 `IWorkerTransport`、传输异常和关闭结果模型。
- 将 `WorkerMessageWriter` 的串行写入能力迁移到传输接口之上。
- 将 `WorkerProtocolCodec.ReadAsync(StreamReader)` 的行读取职责下沉到 `StdioWorkerTransport`。
- 保留现有 `WorkerProtocolCodec.Serialize`、`Deserialize`、`SerializePayload` 和 `DeserializePayload` 的校验逻辑。
- 为发送和接收分别定义取消、EOF、远端关闭、消息过大和非法帧的行为。
- 明确单一接收循环约束：任何 transport 不允许多个消费者并发读取。

建议接口形态：

```csharp
public interface IWorkerTransport : IAsyncDisposable
{
    ValueTask SendAsync(WorkerMessage message, CancellationToken cancellationToken = default);

    ValueTask<WorkerMessage?> ReceiveAsync(CancellationToken cancellationToken = default);

    ValueTask CloseAsync(CancellationToken cancellationToken = default);
}
```

接口不直接暴露底层 Socket、Pipe 或标准流，避免上层重新引入传输耦合。

### 阶段二：迁移 Supervisor 与 Runner 到接口

目标：让普通运行和长生命周期调试共用同一套 transport 生命周期。

涉及文件：

- `src/SereinFlow.Worker.Supervisor/WorkerSupervisor.cs`
- `src/SereinFlow.Worker.Runner/Program.cs`
- `src/SereinFlow.Worker.Client/SupervisorWorkerRunClient.cs`
- `src/SereinFlow.Worker.Client/WorkerRunClientContracts.cs`

任务：

- Supervisor 启动后通过 transport 完成 ready/handshake/run。
- `MonitorRunAsync`、`CancelAndReturnAsync` 和 `DebugRunSession` 改为使用 transport。
- 保留现有 command gate、heartbeat、deadline 和取消宽限期。
- Runner 的控制循环改为从 transport 接收 cancel/debug/heartbeat 消息。
- Runner 的事件发布器继续通过同一 transport 串行发送事件和结果。
- stderr 重定向和诊断记录保持不变。
- 将 IPC 断开统一映射为 `worker.crashed`，主动取消和 deadline 继续使用现有结果码。

阶段二完成后，默认仍使用 stdio，业务层不应能感知此次迁移。

### 阶段三：增加 Windows Named Pipe

目标：在 Windows 上使用仅限本机的父子进程 IPC。

Supervisor 侧：

- 为每次 Worker Run 创建唯一 pipe 名称。
- 在启动 Runner 前创建 Named Pipe server。
- 通过启动参数或受保护的环境变量传递 pipe 名称和一次性连接 token。
- Runner 连接后，Supervisor 校验 token、协议版本和 run identity。
- ready/handshake 成功前设置连接超时并清理 pipe。
- Worker 结束、失败或被杀死时关闭并释放 pipe。

Runner 侧：

- 读取 transport 参数并主动连接 Named Pipe。
- 连接失败时写入 stderr 并以稳定退出码退出。
- 不开放 TCP 监听，不创建持久化 pipe 名称。

安全要求：

- pipe 名称必须不可预测且只在当前进程生命周期内有效；
- 使用 Windows pipe ACL 限制为当前用户/父进程可访问范围；
- token 不能写入普通日志；
- 仍然校验 WorkerMessage 中的 runId 和 debugSessionId。

### 阶段四：增加 Linux/macOS UDS

目标：在 Unix 系统上使用文件系统命名空间控制的本机 IPC。

Supervisor 侧：

- 在受控临时目录创建唯一 socket 路径。
- 启动 Runner 前创建监听 socket，并将路径和一次性 token 传给 Runner。
- 连接完成后校验文件权限、token、协议版本和 run identity。
- 会话结束时关闭 socket 并删除 socket 文件。
- 启动失败、异常退出和 API 关闭路径都必须执行清理。

安全要求：

- socket 文件放在服务专属目录，不使用宽泛公共目录作为唯一保护；
- 设置最小可访问权限，避免同机其他用户接管；
- 防止路径穿越和超长路径；
- 不能因为 socket 文件残留而连接到旧 Worker。

### 阶段五：传输选择与回退

目标：让部署环境可以选择传输，同时保持兼容。

建议配置：

```text
SereinFlow:WorkerTransport = auto | stdio | namedPipe | uds
```

行为：

- `auto`：Windows 选择 Named Pipe，Unix 选择 UDS；初始化失败时记录诊断并按策略回退 stdio。
- `stdio`：强制使用现有标准流，便于兼容旧部署和诊断。
- `namedPipe`：非 Windows 环境启动前明确返回配置错误。
- `uds`：不支持 UDS 的环境明确返回配置错误。

回退必须只发生在 Worker 尚未开始执行前；连接中断后不能偷偷切换到另一种传输，否则会破坏运行唯一性和调试命令顺序。

涉及文件：

- `src/SereinFlow.Api/SereinFlowExecutionRegistration.cs`
- `src/SereinFlow.Worker.Client/SupervisorWorkerRunClient.cs`
- 配置样例和部署文档

## 6. 协议版本策略

第一阶段不升级业务协议版本。建议将协议文档改为“Worker Protocol v1 + transport bindings”：

- v1 envelope 和 DTO 语义不变；
- stdio、Named Pipe、UDS 是不同传输绑定；
- 每种绑定定义消息边界、关闭行为和上限；
- ready/handshake 仍然是协议级身份确认，不由操作系统 IPC 连接替代。

只有在消息语义、错误码或身份校验发生不兼容变化时，才升级 `WorkerProtocol.Version`。

## 7. 不应改动的上层部分

以下代码应继续保持传输无关：

- `RunExecutionQueue` 和普通运行调度；
- `FlowDebugSessionService`；
- `IFlowDebugSessionService`；
- `McpDebugToolHandlers`；
- HTTP API 和 MCP stdio 宿主；
- SignalR 运行事件广播。

MCP stdio 是 API 与 MCP 客户端之间的协议通道，不是 API 与 Worker 之间的 Worker IPC。两者应继续独立存在。

## 8. 验收条件

不新增测试项目，使用现有测试和集成验证入口确认：

- stdio 模式行为与迁移前一致；
- `auto` 在 Windows 使用 Named Pipe，在 Unix 使用 UDS；
- transport 选择不会改变 API/MCP 返回结构；
- 普通 Action/Script/FlowCall/Flipflop 运行可完成；
- 事件 sequence、消息大小和协议版本校验仍然有效；
- deadline、取消、Runner 崩溃和进程树回收行为不退化；
- 调试启动、暂停、step、continue、stop 和终态转换不退化；
- 重复和过期 `commandSequence` 仍返回稳定冲突错误；
- Worker 连接断开后不会留下可复用的 pipe 或 UDS；
- 同机未授权进程不能接入当前 Worker；
- API 使用 `--mcp-stdio` 时仍不暴露 HTTP 端点，但可以正常启动 Worker；
- API 重启后现有“无法重新接管 Worker”的语义保持不变。

## 9. 风险与处理

### 风险一：把 WebSocket/Socket 当成普通 Stream

WebSocket 和消息型 IPC 都有消息边界，直接套用 `StreamReader.ReadLineAsync` 容易产生半包、粘包或错误的关闭语义。

处理：由 transport 实现负责完整消息帧，协议 codec 只处理完整 JSON 消息。

### 风险二：并发发送破坏帧顺序

事件发布、心跳、取消确认、结果和调试命令可能同时发送。

处理：保留统一的串行发送 gate，并确保全系统只有一个接收循环。

### 风险三：IPC 端点残留

API 异常退出或 Runner 被强制终止时，UDS 文件或 Named Pipe 可能残留逻辑状态。

处理：将端点清理放入 transport 的幂等 Dispose，并在启动时拒绝连接到旧 token 的端点。

### 风险四：同机越权接入

仅依赖随机名称或 localhost 不足以构成授权。

处理：Named Pipe 使用 ACL，UDS 使用目录权限，并叠加一次性 token、runId 和 debugSessionId 校验。

### 风险五：取消路径死锁

取消时可能同时存在接收任务、发送任务、进程终止和 stderr drain。

处理：先停止接受新业务命令，再发送 cancel，等待固定宽限期，最后关闭 transport 并终止进程树；所有清理操作必须可重复执行。

## 10. 推荐实施顺序

```text
传输接口抽象
  → stdio 适配器保持兼容
  → Supervisor/Runner 迁移到接口
  → Windows Named Pipe
  → Linux/macOS UDS
  → auto 选择与部署配置
  → 文档和现有集成验证
```

最终推荐：将 stdio 降级为兼容传输，将 Named Pipe/UDS 作为默认本机 Worker IPC；不要通过在 Worker 内嵌 WebSocket 服务来解决本机进程通讯问题。

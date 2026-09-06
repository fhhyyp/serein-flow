using System.Diagnostics.CodeAnalysis;

namespace SereinFlow.Library;

/// <summary>
/// Defines how a message is represented inside one Worker run.
/// 定义消息在一次 Worker 运行中的表示方式。
/// </summary>
public enum MessageSerializationMode
{
    /// <summary>
    /// Keeps the object reference in memory and only supports compatible CLR types.
    /// 在内存中保留对象引用，只支持可赋值兼容的 CLR 类型。
    /// </summary>
    DirectObject = 0,

    /// <summary>
    /// Stores UTF-8 JSON and deserializes it at the receiving endpoint.
    /// 保存 UTF-8 JSON，并在接收端点按目标类型反序列化。
    /// </summary>
    Json = 1,
}

/// <summary>
/// Defines what happens when a bounded message channel is full.
/// 定义有界消息通道已满时的处理方式。
/// </summary>
public enum MessageOverflowStrategy
{
    /// <summary>
    /// Rejects the new message with a diagnostic exception.
    /// 拒绝新消息并抛出可诊断异常。
    /// </summary>
    Reject = 0,

    /// <summary>
    /// Removes the oldest buffered message before accepting the new one.
    /// 丢弃缓冲区中最旧的消息后接收新消息。
    /// </summary>
    DropOldest = 1,

    /// <summary>
    /// Discards the new message when the channel is full.
    /// 通道已满时丢弃新消息。
    /// </summary>
    DropNewest = 2,
}

/// <summary>
/// Configures the bounded channel, serialization and external-ingress policy.
/// 配置有界通道、序列化和外部入口策略。
/// </summary>
public sealed record MessageChannelOptions
{
    /// <summary>
    /// Gets the message representation. JSON is the default for cross-library interoperability.
    /// 获取消息表示方式；JSON 是跨类库互操作的默认方式。
    /// </summary>
    public MessageSerializationMode SerializationMode { get; init; } = MessageSerializationMode.Json;

    /// <summary>
    /// Gets the maximum number of buffered messages for each topic or subscription.
    /// 获取每个主题或订阅者允许缓冲的最大消息数量。
    /// </summary>
    public int Capacity { get; init; } = 256;

    /// <summary>
    /// Gets the overflow policy for a full channel.
    /// 获取通道已满时的溢出策略。
    /// </summary>
    public MessageOverflowStrategy OverflowStrategy { get; init; } = MessageOverflowStrategy.Reject;

    /// <summary>
    /// Gets the optional lifetime of a message. Expired messages are discarded before delivery.
    /// 获取消息的可选有效期；过期消息会在投递前丢弃。
    /// </summary>
    public TimeSpan? MessageTtl { get; init; }

    /// <summary>
    /// Gets the maximum UTF-8 payload size for one message.
    /// 获取单条消息允许的最大 UTF-8 载荷大小。
    /// </summary>
    public int MaxPayloadBytes { get; init; } = 256 * 1024;

    /// <summary>
    /// Gets whether this channel is explicitly available to the external message-ingress API.
    /// 获取此通道是否显式开放给外部消息入口 API。
    /// </summary>
    public bool ExternalIngress { get; init; }

    /// <summary>
    /// Gets the optional server-controlled contract identifier required by external ingress.
    /// 获取外部入口必须匹配的可选、由服务端控制的合同标识。
    /// </summary>
    public string? ContractId { get; init; }

    /// <summary>
    /// Gets a new options instance using the default JSON channel policy.
    /// 获取使用默认 JSON 通道策略的新选项实例。
    /// </summary>
    public static MessageChannelOptions Json => new();

    /// <summary>
    /// Gets a new options instance using direct in-process object delivery.
    /// 获取使用进程内对象直接传递的新选项实例。
    /// </summary>
    public static MessageChannelOptions DirectObject => new() { SerializationMode = MessageSerializationMode.DirectObject };
}

/// <summary>
/// Provides the message queue surface shared by node libraries in one Worker run.
/// 提供一次 Worker 运行内供节点类库共享的消息队列接口。
/// </summary>
[SuppressMessage("Naming", "CA1711", Justification = "IMessageQueue is the stable public SDK name.")]
public interface IMessageQueue
{
    /// <summary>
    /// Sends one message to the FIFO queue for <paramref name="topic"/>.
    /// 向 <paramref name="topic"/> 对应的 FIFO 队列发送一条消息。
    /// </summary>
    /// <param name="topic">The logical topic name. 逻辑主题名称。</param>
    /// <param name="message">The message value. 消息值。</param>
    /// <param name="cancellationToken">The operation cancellation token. 操作取消令牌。</param>
    ValueTask SendAsync(string topic, object message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Receives and converts the next message for <paramref name="topic"/>.
    /// 接收并转换 <paramref name="topic"/> 对应的下一条消息。
    /// </summary>
    /// <typeparam name="T">The expected message type. 期望的消息类型。</typeparam>
    /// <param name="topic">The logical topic name. 逻辑主题名称。</param>
    /// <param name="cancellationToken">The operation cancellation token. 操作取消令牌。</param>
    ValueTask<T> ReceiveAsync<T>(string topic, CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides broadcast publishing and subscription for one Worker run.
/// 提供一次 Worker 运行内的广播发布和订阅接口。
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes one message to every subscription that is active at publish time.
    /// 向发布时刻处于活动状态的每个订阅者发布一条消息。
    /// </summary>
    /// <param name="topic">The logical topic name. 逻辑主题名称。</param>
    /// <param name="message">The message value. 消息值。</param>
    /// <param name="cancellationToken">The operation cancellation token. 操作取消令牌。</param>
    ValueTask PublishAsync(string topic, object message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an independent subscription cursor for a topic.
    /// 为主题创建独立的订阅游标。
    /// </summary>
    /// <typeparam name="T">The expected message type. 期望的消息类型。</typeparam>
    /// <param name="topic">The logical topic name. 逻辑主题名称。</param>
    /// <param name="cancellationToken">The subscription lifetime token. 订阅生命周期令牌。</param>
    IEventSubscription<T> Subscribe<T>(string topic, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an independently buffered event subscription.
/// 表示具有独立缓冲区的事件订阅。
/// </summary>
/// <typeparam name="T">The event value type. 事件值类型。</typeparam>
public interface IEventSubscription<T> : IAsyncDisposable
{
    /// <summary>
    /// Reads all events until the subscription or supplied token is cancelled.
    /// 读取所有事件，直到订阅或传入令牌取消。
    /// </summary>
    /// <param name="cancellationToken">The read cancellation token. 读取取消令牌。</param>
    /// <returns>An asynchronous event sequence. 异步事件序列。</returns>
    IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for and returns the next event.
    /// 等待并返回下一个事件。
    /// </summary>
    /// <param name="cancellationToken">The operation cancellation token. 操作取消令牌。</param>
    ValueTask<T> NextAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The base diagnostic exception for message operations.
/// 消息操作的基础诊断异常。
/// </summary>
public class MessageServiceException : Exception
{
    /// <summary>
    /// Initializes a message service exception.
    /// 初始化消息服务异常。
    /// </summary>
    public MessageServiceException(string code, string message, string? topic = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        Topic = topic;
    }

    /// <summary>
    /// Gets the stable machine-readable error code.
    /// 获取稳定的机器可读错误代码。
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the related topic when one is available.
    /// 获取相关主题（如果有）。
    /// </summary>
    public string? Topic { get; }
}

/// <summary>
/// Reports a direct-object message whose runtime type is not assignable to the receiver type.
/// 报告直接对象消息的运行时类型无法赋值给接收类型。
/// </summary>
public sealed class MessageTypeMismatchException : MessageServiceException
{
    /// <summary>
    /// Initializes a type mismatch exception.
    /// 初始化类型不匹配异常。
    /// </summary>
    public MessageTypeMismatchException(string topic, Type expectedType, Type? actualType)
        : base(
            MessageContractErrorCodes.TypeMismatch,
            $"Message on topic '{topic}' is not assignable to '{expectedType.FullName}'. Actual type: '{actualType?.FullName ?? "null"}'. 主题“{topic}”上的消息无法赋值给“{expectedType.FullName}”，实际类型为“{actualType?.FullName ?? "null"}”。",
            topic)
    {
        ExpectedType = expectedType;
        ActualType = actualType;
    }

    /// <summary>
    /// Gets the type requested by the receiver.
    /// 获取接收者请求的类型。
    /// </summary>
    public Type ExpectedType { get; }

    /// <summary>
    /// Gets the actual message type, or <see langword="null"/> for a null message.
    /// 获取消息实际类型；消息为 null 时返回 <see langword="null"/>。
    /// </summary>
    public Type? ActualType { get; }
}

/// <summary>
/// Error codes emitted by the SDK message-contract layer.
/// 消息契约层产生的错误码。
/// </summary>
public static class MessageContractErrorCodes
{
    /// <summary>Message value type does not match the requested type. 消息值类型与请求类型不匹配。</summary>
    public const string TypeMismatch = "message.type_mismatch";
}

/// <summary>
/// Exposes the stable SDK message-service contract to node libraries.
/// 向节点类库公开稳定的 SDK 消息服务契约。
/// </summary>
public interface IMessageService
{
    /// <summary>
    /// Creates a queue view backed by the run-level broker.
    /// 创建由运行级 Broker 支持的队列视图。
    /// </summary>
    /// <param name="options">The channel policy, or default JSON policy. 通道策略；为空时使用默认 JSON 策略。</param>
    IMessageQueue CreateMessageQueue(MessageChannelOptions? options = null);

    /// <summary>
    /// Creates a queue view with a convenient serialization-mode switch.
    /// 使用便捷的序列化模式开关创建队列视图。
    /// </summary>
    /// <param name="useJson">Whether to use JSON instead of direct objects. 是否使用 JSON 而非直接对象。</param>
    IMessageQueue CreateMessageQueue(bool useJson);

    /// <summary>
    /// Creates an event-bus view backed by the run-level broker.
    /// 创建由运行级 Broker 支持的事件总线视图。
    /// </summary>
    /// <param name="options">The channel policy, or default JSON policy. 通道策略；为空时使用默认 JSON 策略。</param>
    IEventBus CreateEventBus(MessageChannelOptions? options = null);
}

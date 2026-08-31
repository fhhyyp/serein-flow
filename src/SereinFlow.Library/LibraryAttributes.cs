using System;

// These types are the standalone public SDK contract for uploaded node
// libraries. Keep their namespaces and member names stable: the server reads
// them from PE metadata without loading the uploaded assembly.
// 这些类型是上传节点类库使用的独立公共 SDK 契约。必须保持其命名空间和成员名稳定，
// 因为服务端会在不加载上传程序集的情况下从 PE 元数据中读取它们。
namespace SereinFlow.Core.Api;

/// <summary>
/// Marks a class as the container of nodes exposed by a SereinFlow library.
/// 标记一个类为 SereinFlow 类库向流程公开的节点容器。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class FlowLibraryAttribute : Attribute
{
    /// <summary>
    /// Initializes a library marker whose display name is derived from the decorated class name.
    /// 初始化类库标记；其显示名称由被标记的类名推导。
    /// </summary>
    public FlowLibraryAttribute()
    {
    }

    /// <summary>
    /// Initializes a library marker with an explicit display name.
    /// 使用显式显示名称初始化类库标记。
    /// </summary>
    /// <param name="name">
    /// The library display name used as part of the derived node identity.
    /// 用作派生节点标识一部分的类库显示名称。
    /// </param>
    public FlowLibraryAttribute(string name) => Name = name;

    /// <summary>
    /// Gets the optional library display name; when it is <see langword="null"/>,
    /// SereinFlow derives the name from the decorated class.
    /// 获取可选的类库显示名称；为 <see langword="null"/> 时，SereinFlow 从被标记的类推导名称。
    /// </summary>
    public string? Name { get; }
}

/// <summary>
/// Marks a method as a node that can be placed and executed in a SereinFlow flow.
/// 标记一个方法为可放置并执行于 SereinFlow 流程中的节点。
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class FlowNodeAttribute : Attribute
{
    /// <summary>
    /// Initializes a node marker with default metadata.
    /// 使用默认元数据初始化节点标记。
    /// </summary>
    public FlowNodeAttribute()
    {
    }

    /// <summary>
    /// Gets or sets the stable node contract identifier. When omitted, SereinFlow derives it
    /// from the library name (or declaring class name) and CLR method name.
    /// 获取或设置稳定的节点契约标识。未指定时，SereinFlow 会从类库名称（或声明类名）和 CLR 方法名推导它。
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the execution behavior of the node. The default is <see cref="NodeType.Action"/>.
    /// 获取或设置节点的执行行为。默认值为 <see cref="NodeType.Action"/>。
    /// </summary>
    public NodeType NodeType { get; set; } = NodeType.Action;

    /// <summary>
    /// Gets or sets the user-facing display name for the node.
    /// 获取或设置节点面向用户的显示名称。
    /// </summary>
    public string? AnotherName { get; set; }

    /// <summary>
    /// Gets or sets the user-facing description of the node.
    /// 获取或设置节点面向用户的说明。
    /// </summary>
    public string? Desc { get; set; }
}

/// <summary>
/// Supplies metadata for a parameter exposed as an input of a SereinFlow node.
/// 为作为 SereinFlow 节点输入公开的参数提供元数据。
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class NodeParamAttribute : Attribute
{
    /// <summary>
    /// Initializes parameter metadata with its default values.
    /// 使用默认值初始化参数元数据。
    /// </summary>
    public NodeParamAttribute()
    {
    }

    /// <summary>
    /// Gets or sets the stable parameter contract identifier. When omitted, SereinFlow uses the
    /// CLR parameter name.
    /// 获取或设置稳定的参数契约标识。未指定时，SereinFlow 使用 CLR 参数名。
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the previous stable IDs accepted when a parameter is intentionally renamed.
    /// 获取或设置参数有意重命名时仍可接受的历史稳定 ID。
    /// </summary>
    public string[]? Aliases { get; set; }

    /// <summary>
    /// Gets or sets the user-facing display name for the parameter.
    /// 获取或设置参数面向用户的显示名称。
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets whether the parameter must be supplied explicitly rather than inferred by the
    /// runtime. The default is <see langword="true"/>.
    /// 获取或设置参数是否必须显式提供而不能由运行时推导。默认值为 <see langword="true"/>。
    /// </summary>
    public bool IsExplicit { get; set; } = true;
}

/// <summary>
/// Defines the execution behavior of a SereinFlow node.
/// 定义 SereinFlow 节点的执行行为。
/// </summary>
public enum NodeType
{
    /// <summary>
    /// Runs immediately when the flow reaches the node.
    /// 当流程到达该节点时立即执行。
    /// </summary>
    Action = 0,

    /// <summary>
    /// Waits asynchronously for one trigger before continuing the flow.
    /// 异步等待一次触发后再继续流程。
    /// </summary>
    Flipflop = 1,
}

/// <summary>
/// Defines how a service declared by a node library is reused inside one Worker run.
/// Worker-wide singletons are intentionally not supported.
/// 定义节点类库声明的服务如何在一次 Worker 运行中复用；设计上不支持跨 Worker 运行的单例。
/// </summary>
public enum FlowServiceLifetime
{
    /// <summary>
    /// One instance is shared by all node invocations that use the same library assembly during a
    /// Worker run.
    /// 在一次 Worker 运行中，同一类库程序集的所有节点调用共享一个实例。
    /// </summary>
    Run = 0,

    /// <summary>
    /// One instance is shared by a single node invocation.
    /// 单次节点调用共享一个实例。
    /// </summary>
    Invocation = 1,

    /// <summary>
    /// A new instance is created each time the service is resolved.
    /// 每次解析服务时都创建一个新实例。
    /// </summary>
    Transient = 2,
}

/// <summary>
/// Registers a concrete class for constructor injection in nodes from the same uploaded library
/// assembly.
/// 将具体类注册为同一上传类库程序集内节点的构造函数注入服务。
/// </summary>
/// <remarks>
/// A declaration without a contract exposes the concrete implementation. A declaration with a
/// contract exposes both the implementation and the contract, which resolve to the same instance
/// for <see cref="FlowServiceLifetime.Run"/> and <see cref="FlowServiceLifetime.Invocation"/>
/// lifetimes.
/// 未指定契约的声明仅公开具体实现。指定契约的声明同时公开实现和契约；对于
/// <see cref="FlowServiceLifetime.Run"/> 和 <see cref="FlowServiceLifetime.Invocation"/> 生命周期，
/// 两者会解析为同一个实例。
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class FlowServiceAttribute : Attribute
{
    /// <summary>
    /// Initializes a service registration that exposes the decorated concrete class as its own
    /// contract.
    /// 初始化服务注册，并将被标记的具体类作为其自身契约公开。
    /// </summary>
    public FlowServiceAttribute()
    {
    }

    /// <summary>
    /// Initializes a service registration that exposes the decorated concrete class through the
    /// specified contract as well as through itself.
    /// 初始化服务注册；被标记的具体类除自身外，还通过指定契约公开。
    /// </summary>
    /// <param name="contractType">
    /// The interface or base-class contract to expose for dependency injection.
    /// 要为依赖注入公开的接口或基类契约。
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="contractType"/> is <see langword="null"/>.
    /// 当 <paramref name="contractType"/> 为 <see langword="null"/> 时引发。
    /// </exception>
    public FlowServiceAttribute(Type contractType)
    {
        ContractType = contractType ?? throw new ArgumentNullException(nameof(contractType));
    }

    /// <summary>
    /// Gets the optional interface or base-class contract exposed by this service.
    /// 获取此服务公开的可选接口或基类契约。
    /// </summary>
    public Type? ContractType { get; }

    /// <summary>
    /// Gets or sets the reuse scope of the registered service. The default is
    /// <see cref="FlowServiceLifetime.Run"/>.
    /// 获取或设置已注册服务的复用范围。默认值为 <see cref="FlowServiceLifetime.Run"/>。
    /// </summary>
    public FlowServiceLifetime Lifetime { get; set; } = FlowServiceLifetime.Run;
}

/// <summary>
/// Provides the generic form of <see cref="FlowServiceAttribute"/> for C# 11 and later.
/// 提供面向 C# 11 及更高版本的 <see cref="FlowServiceAttribute"/> 泛型形式。
/// </summary>
/// <typeparam name="TContract">
/// The interface or base-class contract exposed for dependency injection.
/// 为依赖注入公开的接口或基类契约。
/// </typeparam>
public sealed class FlowServiceAttribute<TContract> : FlowServiceAttribute
    where TContract : class
{
    /// <summary>
    /// Initializes a service registration that exposes the decorated concrete class through
    /// <typeparamref name="TContract"/> as well as through itself.
    /// 初始化服务注册；被标记的具体类除自身外，还通过 <typeparamref name="TContract"/> 公开。
    /// </summary>
    public FlowServiceAttribute()
        : base(typeof(TContract))
    {
    }
}

/// <summary>
/// Provides the metadata names that the metadata-only library scanner recognizes.
/// 提供仅元数据类库扫描器可识别的元数据名称。
/// </summary>
public static class LibraryAttributeContract
{
    /// <summary>
    /// Gets the fully qualified metadata name of <see cref="FlowLibraryAttribute"/>.
    /// 获取 <see cref="FlowLibraryAttribute"/> 的完全限定元数据名称。
    /// </summary>
    public static readonly string FlowLibraryAttributeFullName = GetFullName<FlowLibraryAttribute>();

    /// <summary>
    /// Gets the fully qualified metadata name of <see cref="FlowNodeAttribute"/>.
    /// 获取 <see cref="FlowNodeAttribute"/> 的完全限定元数据名称。
    /// </summary>
    public static readonly string FlowNodeAttributeFullName = GetFullName<FlowNodeAttribute>();

    /// <summary>
    /// Gets the fully qualified metadata name of <see cref="NodeParamAttribute"/>.
    /// 获取 <see cref="NodeParamAttribute"/> 的完全限定元数据名称。
    /// </summary>
    public static readonly string NodeParamAttributeFullName = GetFullName<NodeParamAttribute>();

    /// <summary>
    /// Gets the fully qualified metadata name of <see cref="FlowServiceAttribute"/>.
    /// 获取 <see cref="FlowServiceAttribute"/> 的完全限定元数据名称。
    /// </summary>
    public static readonly string FlowServiceAttributeFullName = GetFullName<FlowServiceAttribute>();

    /// <summary>
    /// Gets the fully qualified metadata name of the generic
    /// <see cref="FlowServiceAttribute{TContract}"/> definition.
    /// 获取泛型 <see cref="FlowServiceAttribute{TContract}"/> 定义的完全限定元数据名称。
    /// </summary>
    public static readonly string FlowServiceGenericAttributeFullName = GetFullName<FlowServiceAttribute<object>>();

    /// <summary>
    /// Gets the fully qualified metadata name of <see cref="ParamArrayAttribute"/>.
    /// 获取 <see cref="ParamArrayAttribute"/> 的完全限定元数据名称。
    /// </summary>
    public static readonly string ParamArrayAttributeFullName = typeof(ParamArrayAttribute).FullName ?? nameof(ParamArrayAttribute);

    /// <summary>
    /// Gets the metadata property name that stores a flow library display name.
    /// 获取存储流程类库显示名称的元数据属性名。
    /// </summary>
    public const string LibraryNamePropertyName = nameof(FlowLibraryAttribute.Name);

    /// <summary>
    /// Gets the metadata property name that stores a node contract identifier.
    /// 获取存储节点契约标识的元数据属性名。
    /// </summary>
    public const string NodeContractIdPropertyName = nameof(FlowNodeAttribute.Id);

    /// <summary>
    /// Gets the metadata property name that stores a node execution behavior.
    /// 获取存储节点执行行为的元数据属性名。
    /// </summary>
    public const string NodeTypePropertyName = nameof(FlowNodeAttribute.NodeType);

    /// <summary>
    /// Gets the metadata property name that stores a user-facing display name.
    /// 获取存储面向用户显示名称的元数据属性名。
    /// </summary>
    public const string DisplayNamePropertyName = nameof(FlowNodeAttribute.AnotherName);

    /// <summary>
    /// Gets the metadata property name that stores a user-facing description.
    /// 获取存储面向用户说明的元数据属性名。
    /// </summary>
    public const string DescriptionPropertyName = nameof(FlowNodeAttribute.Desc);

    /// <summary>
    /// Gets the metadata property name that stores a parameter contract identifier.
    /// 获取存储参数契约标识的元数据属性名。
    /// </summary>
    public const string ParameterContractIdPropertyName = nameof(NodeParamAttribute.Id);

    /// <summary>
    /// Gets the metadata property name that stores historical parameter IDs.
    /// 获取存储历史参数 ID 的元数据属性名。
    /// </summary>
    public const string ParameterAliasesPropertyName = nameof(NodeParamAttribute.Aliases);

    /// <summary>
    /// Gets the metadata property name that stores a parameter display name.
    /// 获取存储参数显示名称的元数据属性名。
    /// </summary>
    public const string ParameterNamePropertyName = nameof(NodeParamAttribute.Name);

    /// <summary>
    /// Gets the metadata property name that indicates whether a parameter is explicit.
    /// 获取指示参数是否必须显式提供的元数据属性名。
    /// </summary>
    public const string IsExplicitPropertyName = nameof(NodeParamAttribute.IsExplicit);

    /// <summary>
    /// Gets the metadata property name that stores a service contract type.
    /// 获取存储服务契约类型的元数据属性名。
    /// </summary>
    public const string ServiceContractTypePropertyName = nameof(FlowServiceAttribute.ContractType);

    /// <summary>
    /// Gets the metadata property name that stores a service lifetime.
    /// 获取存储服务生命周期的元数据属性名。
    /// </summary>
    public const string ServiceLifetimePropertyName = nameof(FlowServiceAttribute.Lifetime);

    /// <summary>
    /// Returns the fully qualified metadata name of a contract type.
    /// 返回契约类型的完全限定元数据名称。
    /// </summary>
    /// <typeparam name="T">
    /// The contract type whose metadata name is required.
    /// 需要获取元数据名称的契约类型。
    /// </typeparam>
    /// <returns>
    /// The fully qualified type name, or the simple type name when no fully qualified name is
    /// available.
    /// 完全限定类型名；没有完全限定名称时返回简单类型名。
    /// </returns>
    private static string GetFullName<T>()
        => typeof(T).FullName ?? typeof(T).Name;
}

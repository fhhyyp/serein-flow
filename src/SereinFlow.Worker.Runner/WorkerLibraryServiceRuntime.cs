using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Contracts;
using SereinFlow.Library;

namespace SereinFlow.Worker.Runner;
/// <summary>
/// Owns the restricted DI containers used by libraries during one Worker run.
/// Each uploaded assembly gets an isolated root provider so a service cannot
/// cross a library package boundary.
/// 管理单次 Worker 运行中类库使用的受限依赖注入容器；每个上传程序集独立拥有根服务提供程序，防止服务跨越类库包边界。
/// </summary>
internal sealed class WorkerLibraryServiceRuntime : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Assembly, Lazy<LibraryServiceProvider>> _providers = new();
    private readonly IMessageService _messageService;
    private readonly Func<Assembly, IFlowNativeLibraryLoader> _nativeLibraryLoaderFactory;
    private readonly IFlowWorkpiece _workpiece;
    public WorkerLibraryServiceRuntime(IMessageService messageService, IFlowWorkpiece workpiece, LibraryVariableService variables, Func<Assembly, IFlowNativeLibraryLoader> nativeLibraryLoaderFactory)
    {
        _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        _workpiece = workpiece ?? throw new ArgumentNullException(nameof(workpiece));
        Variables = variables ?? throw new ArgumentNullException(nameof(variables));
        _nativeLibraryLoaderFactory = nativeLibraryLoaderFactory ?? throw new ArgumentNullException(nameof(nativeLibraryLoaderFactory));
    }

    public LibraryVariableService Variables { get; }

    public AsyncServiceScope CreateInvocationScope(Assembly libraryAssembly)
    {
        ArgumentNullException.ThrowIfNull(libraryAssembly);
        try
        {
            var provider = _providers.GetOrAdd(libraryAssembly, assembly =>
            {
                var lazy1 = new Lazy<LibraryServiceProvider>(() =>
                {
                    var nativeLibraryLoaderFactoryResult = _nativeLibraryLoaderFactory(assembly);
                    var createResult = LibraryServiceProvider.Create(assembly, _messageService, _workpiece, Variables, nativeLibraryLoaderFactoryResult);
                    return createResult;
                }, LazyThreadSafetyMode.ExecutionAndPublication);
                return lazy1;
            });
            var createInvocationScopeResult = provider.Value.CreateInvocationScope();
            return createInvocationScopeResult;
        }
        catch (LibraryServiceException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new LibraryServiceException(LibraryErrorCodes.ServiceProviderFailed, $"The library service provider could not be created. Library DI service provider creation failed. {exception.Message} 无法创建类库服务提供程序，类库依赖注入服务提供程序创建失败。", exception);
        }
    }

    public object CreateNodeInstance(IServiceProvider services, Type nodeType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(nodeType);
        try
        {
            LibraryServiceProvider.ValidateNodeType(nodeType);
            var createInstanceResult = ActivatorUtilities.CreateInstance(services, nodeType);
            return createInstanceResult;
        }
        catch (LibraryServiceException)
        {
            throw;
        }
        catch (FlowNativeLibraryException exception)
        {
            throw new LibraryServiceException(exception.Code, exception.Message, exception);
        }
        catch (Exception exception)
        {
            throw new LibraryServiceException(LibraryErrorCodes.ServiceActivationFailed, $"The library node type '{nodeType.FullName ?? nodeType.Name}' could not be activated from declared FlowService dependencies. {exception.Message} 无法根据声明的 FlowService 依赖激活类库节点类型。", exception);
        }
    }

    public object CreateNodeResultConverter(IServiceProvider services, Type converterType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(converterType);
        try
        {
            var getRequiredServiceResult = services.GetRequiredService(converterType);
            return getRequiredServiceResult;
        }
        catch (Exception exception)
        {
            throw new LibraryServiceException(LibraryErrorCodes.ResultConverterActivationFailed, $"The result converter '{converterType.FullName ?? converterType.Name}' could not be activated. 节点结果转换器无法激活。 {exception.Message}", exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        var providers = _providers.Values
            .Where(static lazy => lazy.IsValueCreated)
            .ToArray();
        _providers.Clear();
        foreach (var lazy in providers)
        {
            try
            {
                await lazy.Value.DisposeAsync();
            }
            catch (Exception exception)
            {
                // The cache must still dispose the remaining providers before
                // their collectible AssemblyLoadContexts are unloaded.
                // 必须在卸载可回收 AssemblyLoadContext 之前，继续释放缓存中其余的服务提供程序。
                Console.Error.WriteLine($"Worker library service cleanup failed. Worker library DI cleanup failed. {exception.Message} Worker 类库服务清理失败，依赖注入清理失败。");
            }
        }
    }
}

internal sealed class LibraryServiceProvider : IAsyncDisposable
{
    private static readonly HashSet<Type> RestrictedConstructorTypes = [typeof(IServiceProvider), typeof(IServiceScopeFactory), typeof(IServiceProviderIsService), typeof(IServiceProviderIsKeyedService), typeof(IKeyedServiceProvider), typeof(IFlowContext),];
    private readonly ServiceProvider _provider;
    private LibraryServiceProvider(ServiceProvider provider)
    {
        _provider = provider;
    }

    public static LibraryServiceProvider Create(Assembly assembly, IMessageService messageService, IFlowWorkpiece workpiece, LibraryVariableService variables, IFlowNativeLibraryLoader nativeLibraryLoader)
    {
        var serviceTypes = DiscoverServiceTypes(assembly);
        var registrations = BuildRegistrations(serviceTypes);
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(messageService);
        services.AddSingleton(workpiece);
        services.AddSingleton(variables);
        services.AddSingleton(nativeLibraryLoader);
        foreach (var registration in registrations.Implementations)
        {
            var toServiceLifetimeResult = ToServiceLifetime(registration.Value);
            var serviceDescriptor = new ServiceDescriptor(registration.Key, registration.Key, toServiceLifetimeResult);
            services.Add(serviceDescriptor);
        }

        foreach (var registration in registrations.Contracts)
        {
            var contractType = registration.Key;
            var implementation = registration.Value;
            if (contractType == implementation.ImplementationType)
            {
                continue;
            }

            var toServiceLifetimeResult3 = ToServiceLifetime(implementation.Lifetime);
            var serviceDescriptor4 = new ServiceDescriptor(contractType, provider =>
            {
                var getRequiredServiceResult2 = provider.GetRequiredService(implementation.ImplementationType);
                return getRequiredServiceResult2;
            }, toServiceLifetimeResult3);
            services.Add(serviceDescriptor4);
        }

        foreach (var converterType in DiscoverResultConverterTypes(assembly))
        {
            ValidateResultConverter(converterType, assembly);
            services.AddTransient(converterType);
        }

        ServiceProvider? provider = null;
        try
        {
            var serviceProviderOptions = new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true,
            };
            provider = services.BuildServiceProvider(serviceProviderOptions);
            var libraryServiceProvider = new LibraryServiceProvider(provider);
            return libraryServiceProvider;
        }
        catch (Exception exception)
        {
            provider?.Dispose();
            throw new LibraryServiceException(LibraryErrorCodes.ServiceValidationFailed, $"The declared FlowService dependency graph is invalid. {exception.Message} 声明的 FlowService 依赖关系图无效。", exception);
        }
    }

    public AsyncServiceScope CreateInvocationScope()
    {
        var createAsyncScopeResult = _provider.CreateAsyncScope();
        return createAsyncScopeResult;
    }

    public ValueTask DisposeAsync()
    {
        var disposeAsyncResult = _provider.DisposeAsync();
        return disposeAsyncResult;
    }

    internal static void ValidateNodeType(Type nodeType)
    {
        if (!nodeType.IsClass || nodeType.IsAbstract || nodeType.ContainsGenericParameters)
        {
            throw new LibraryServiceException(LibraryErrorCodes.ServiceNodeTypeInvalid, $"The library node type '{nodeType.FullName ?? nodeType.Name}' cannot be constructed by the Worker service runtime. Worker 服务运行时无法构造指定的类库节点类型。");
        }

        ValidateRestrictedConstructorParameters(nodeType, "node");
    }

    private static Type[] DiscoverServiceTypes(Assembly assembly)
    {
        try
        {
            var toArrayResult = assembly.GetTypes()
                .Where(static type =>
                {
                    var anyResult = type.GetCustomAttributes<FlowServiceAttribute>(inherit: false)
                        .Any();
                    return anyResult;
                })
                .ToArray();
            return toArrayResult;
        }
        catch (ReflectionTypeLoadException exception)
        {
            var selectResult = exception.LoaderExceptions
                .Where(static item => item is not null)
                .Select(static item => item!.Message);
            var details = string.Join(" ", selectResult);
            var evaluatedValue = $"FlowService types could not be loaded from '{assembly.GetName().Name}'. {details} 无法从指定程序集中加载 FlowService 类型。";
            throw new LibraryServiceException(LibraryErrorCodes.ServiceDiscoveryFailed, evaluatedValue, exception);
        }
        catch (Exception exception)
        {
            var evaluatedValue7 = $"FlowService types could not be discovered from '{assembly.GetName().Name}'. {exception.Message} 无法从指定程序集中发现 FlowService 类型。";
            throw new LibraryServiceException(LibraryErrorCodes.ServiceDiscoveryFailed, evaluatedValue7, exception);
        }
    }

    private static Type[] DiscoverResultConverterTypes(Assembly assembly)
    {
        try
        {
            var toArrayResult12 = assembly.GetTypes()
                .SelectMany(static type =>
                {
                    var getMethodsResult = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                    return getMethodsResult;
                })
                .SelectMany(static method =>
                {
                    var getCustomAttributesResult8 = method.GetCustomAttributes<NodeResultAttribute>(inherit: false);
                    return getCustomAttributesResult8;
                })
                .Select(static attribute => attribute.ConverterType)
                .Distinct()
                .ToArray();
            return toArrayResult12;
        }
        catch (ReflectionTypeLoadException exception)
        {
            var selectResult14 = exception.LoaderExceptions
                .Where(static item => item is not null)
                .Select(static item => item!.Message);
            var details = string.Join(" ", selectResult14);
            var evaluatedValue15 = $"Node result converters could not be discovered from '{assembly.GetName().Name}'. 节点结果转换器无法从类库中发现。 {details}";
            throw new LibraryServiceException(LibraryErrorCodes.ResultConverterDiscoveryFailed, evaluatedValue15, exception);
        }
        catch (Exception exception)
        {
            var evaluatedValue16 = $"Node result converters could not be discovered from '{assembly.GetName().Name}'. 节点结果转换器无法从类库中发现。 {exception.Message}";
            throw new LibraryServiceException(LibraryErrorCodes.ResultConverterDiscoveryFailed, evaluatedValue16, exception);
        }
    }

    private static void ValidateResultConverter(Type converterType, Assembly assembly)
    {
        var contracts = converterType.GetInterfaces()
            .Where(static item =>
            {
                var evaluatedValue17 = item.IsGenericType && item.GetGenericTypeDefinition() == typeof(INodeResultConverter<,>);
                return evaluatedValue17;
            })
            .ToArray();
        if (!converterType.IsClass || converterType.IsAbstract || converterType.ContainsGenericParameters || converterType.Assembly != assembly || contracts.Length != 1)
        {
            throw new LibraryServiceException(LibraryErrorCodes.ResultConverterInvalid, $"Result converter '{converterType.FullName ?? converterType.Name}' must be a concrete class from the library assembly implementing exactly one closed INodeResultConverter<TPrimitive, TTransfer> contract. 节点结果转换器必须是类库程序集中的具体类，并且恰好实现一个闭合的转换器合同。");
        }
    }

    private static ServiceRegistrations BuildRegistrations(IEnumerable<Type> serviceTypes)
    {
        var implementations = new Dictionary<Type, FlowServiceLifetime>();
        var contracts = new Dictionary<Type, ServiceRegistration>();
        foreach (var implementationType in serviceTypes)
        {
            ValidateServiceImplementation(implementationType);
            var attributes = implementationType.GetCustomAttributes<FlowServiceAttribute>(inherit: false)
                .ToArray();
            foreach (var attribute in attributes)
            {
                if (!Enum.IsDefined(attribute.Lifetime))
                {
                    throw new LibraryServiceException(LibraryErrorCodes.ServiceLifetimeInvalid, $"FlowService '{implementationType.FullName}' declares an unsupported lifetime. FlowService 声明了不支持的生命周期。");
                }

                if (implementations.TryGetValue(implementationType, out var previousLifetime) && previousLifetime != attribute.Lifetime)
                {
                    throw new LibraryServiceException(LibraryErrorCodes.ServiceLifetimeConflict, $"FlowService '{implementationType.FullName}' declares more than one lifetime. FlowService 声明了多个生命周期。");
                }

                implementations[implementationType] = attribute.Lifetime;
                var contractType = attribute.ContractType ?? implementationType;
                ValidateServiceContract(implementationType, contractType);
                var registration = new ServiceRegistration(implementationType, attribute.Lifetime);
                if (contracts.TryGetValue(contractType, out var existing) && existing != registration)
                {
                    throw new LibraryServiceException(LibraryErrorCodes.ServiceContractDuplicate, $"More than one FlowService declares the contract '{contractType.FullName ?? contractType.Name}'. 多个 FlowService 声明了同一契约。");
                }

                contracts[contractType] = registration;
                // A class declared through an interface must still resolve by
                // its concrete type, matching the FlowService contract.
                // 通过接口声明的类仍须能按其具体类型解析，以符合 FlowService 契约。
                contracts.TryAdd(implementationType, registration);
            }
        }

        var serviceRegistrations = new ServiceRegistrations(implementations, contracts);
        return serviceRegistrations;
    }

    private static void ValidateServiceImplementation(Type implementationType)
    {
        if (!implementationType.IsClass || implementationType.IsAbstract || implementationType.ContainsGenericParameters || implementationType.IsPointer || implementationType.IsByRef)
        {
            throw new LibraryServiceException(LibraryErrorCodes.ServiceImplementationInvalid, $"FlowService '{implementationType.FullName ?? implementationType.Name}' must be a concrete, closed class. FlowService 必须是可实例化且不含未绑定泛型参数的类。");
        }

        var constructors = implementationType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
        if (constructors.Length != 1)
        {
            throw new LibraryServiceException(LibraryErrorCodes.ServiceConstructorInvalid, $"FlowService '{implementationType.FullName ?? implementationType.Name}' must have exactly one public constructor. FlowService 必须且只能有一个公共构造函数。");
        }

        ValidateRestrictedConstructorParameters(implementationType, "service");
    }

    private static void ValidateServiceContract(Type implementationType, Type contractType)
    {
        if (contractType.IsPointer || contractType.IsByRef || contractType.ContainsGenericParameters || RestrictedConstructorTypes.Contains(contractType) || !contractType.IsAssignableFrom(implementationType))
        {
            throw new LibraryServiceException(LibraryErrorCodes.ServiceContractInvalid, $"FlowService '{implementationType.FullName ?? implementationType.Name}' cannot expose the contract '{contractType.FullName ?? contractType.Name}'. FlowService 不能公开指定契约。");
        }
    }

    private static void ValidateRestrictedConstructorParameters(Type type, string kind)
    {
        foreach (var constructor in type.GetConstructors(BindingFlags.Instance | BindingFlags.Public))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                if (!RestrictedConstructorTypes.Contains(parameter.ParameterType))
                {
                    continue;
                }

                throw new LibraryServiceException(LibraryErrorCodes.ServiceDependencyForbidden, $"The {kind} type '{type.FullName ?? type.Name}' cannot request '{parameter.ParameterType.FullName}' through constructor injection. 指定类型不能通过构造函数注入请求该依赖。");
            }
        }
    }

    private static ServiceLifetime ToServiceLifetime(FlowServiceLifetime lifetime)
    {
        ServiceLifetime evaluatedValue20 = lifetime switch
        {
            FlowServiceLifetime.Run => ServiceLifetime.Singleton,
            FlowServiceLifetime.Invocation => ServiceLifetime.Scoped,
            FlowServiceLifetime.Transient => ServiceLifetime.Transient,
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Unsupported FlowService lifetime. 不支持此 FlowService 生命周期。"),
        };
        return evaluatedValue20;
    }

    private sealed record ServiceRegistration(Type ImplementationType, FlowServiceLifetime Lifetime);
    private sealed record ServiceRegistrations(IReadOnlyDictionary<Type, FlowServiceLifetime> Implementations, IReadOnlyDictionary<Type, ServiceRegistration> Contracts);
}

internal sealed class LibraryServiceException(string code, string message, Exception? innerException = null) : Exception(message, innerException)
{
    public string Code { get; } = code;
}

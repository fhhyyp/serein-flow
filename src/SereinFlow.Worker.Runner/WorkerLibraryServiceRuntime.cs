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
            throw new LibraryServiceException(LibraryErrorCodes.ServiceProviderFailed, $"The library service provider could not be created. Library DI service provider creation failed. {exception.Message}", exception);
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
            throw new LibraryServiceException(LibraryErrorCodes.ServiceActivationFailed, $"The library node type '{nodeType.FullName ?? nodeType.Name}' could not be activated from declared FlowService dependencies. {exception.Message}", exception);
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
        var whereResult = _providers.Values.Where(static lazy => lazy.IsValueCreated);
        var providers = whereResult.ToArray();
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
                Console.Error.WriteLine($"Worker library service cleanup failed. Worker library DI cleanup failed. {exception.Message}");
            }
        }
    }
}

internal sealed class LibraryServiceProvider : IAsyncDisposable
{
    private static readonly HashSet<Type> RestrictedConstructorTypes = [typeof(IServiceProvider), typeof(IServiceScopeFactory), typeof(IServiceProviderIsService), typeof(IServiceProviderIsKeyedService), typeof(IKeyedServiceProvider), typeof(IFlowContext), ];
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
            throw new LibraryServiceException(LibraryErrorCodes.ServiceValidationFailed, $"The declared FlowService dependency graph is invalid. {exception.Message}", exception);
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
            throw new LibraryServiceException(LibraryErrorCodes.ServiceNodeTypeInvalid, $"The library node type '{nodeType.FullName ?? nodeType.Name}' cannot be constructed by the Worker service runtime.");
        }

        ValidateRestrictedConstructorParameters(nodeType, "node");
    }

    private static Type[] DiscoverServiceTypes(Assembly assembly)
    {
        try
        {
            var getTypesResult = assembly.GetTypes();
            var whereResult5 = getTypesResult.Where(static type =>
            {
                var getCustomAttributesResult = type.GetCustomAttributes<FlowServiceAttribute>(inherit: false);
                var anyResult = getCustomAttributesResult.Any();
                return anyResult;
            });
            var toArrayResult = whereResult5.ToArray();
            return toArrayResult;
        }
        catch (ReflectionTypeLoadException exception)
        {
            var whereResult6 = exception.LoaderExceptions.Where(static item => item is not null);
            var selectResult = whereResult6.Select(static item => item!.Message);
            var details = string.Join(" ", selectResult);
            var evaluatedValue = $"FlowService types could not be loaded from '{assembly.GetName().Name}'. {details}";
            throw new LibraryServiceException(LibraryErrorCodes.ServiceDiscoveryFailed, evaluatedValue, exception);
        }
        catch (Exception exception)
        {
            var evaluatedValue7 = $"FlowService types could not be discovered from '{assembly.GetName().Name}'. {exception.Message}";
            throw new LibraryServiceException(LibraryErrorCodes.ServiceDiscoveryFailed, evaluatedValue7, exception);
        }
    }

    private static Type[] DiscoverResultConverterTypes(Assembly assembly)
    {
        try
        {
            var getTypesResult9 = assembly.GetTypes();
            var selectManyResult = getTypesResult9.SelectMany(static type =>
            {
                var getMethodsResult = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                return getMethodsResult;
            });
            var selectManyResult10 = selectManyResult.SelectMany(static method =>
            {
                var getCustomAttributesResult8 = method.GetCustomAttributes<NodeResultAttribute>(inherit: false);
                return getCustomAttributesResult8;
            });
            var selectResult11 = selectManyResult10.Select(static attribute => attribute.ConverterType);
            var distinctResult = selectResult11.Distinct();
            var toArrayResult12 = distinctResult.ToArray();
            return toArrayResult12;
        }
        catch (ReflectionTypeLoadException exception)
        {
            var whereResult13 = exception.LoaderExceptions.Where(static item => item is not null);
            var selectResult14 = whereResult13.Select(static item => item!.Message);
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
        var getInterfacesResult = converterType.GetInterfaces();
        var whereResult18 = getInterfacesResult.Where(static item =>
        {
            var evaluatedValue17 = item.IsGenericType && item.GetGenericTypeDefinition() == typeof(INodeResultConverter<, >);
            return evaluatedValue17;
        });
        var contracts = whereResult18.ToArray();
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
            var getCustomAttributesResult19 = implementationType.GetCustomAttributes<FlowServiceAttribute>(inherit: false);
            var attributes = getCustomAttributesResult19.ToArray();
            foreach (var attribute in attributes)
            {
                if (!Enum.IsDefined(attribute.Lifetime))
                {
                    throw new LibraryServiceException(LibraryErrorCodes.ServiceLifetimeInvalid, $"FlowService '{implementationType.FullName}' declares an unsupported lifetime.");
                }

                if (implementations.TryGetValue(implementationType, out var previousLifetime) && previousLifetime != attribute.Lifetime)
                {
                    throw new LibraryServiceException(LibraryErrorCodes.ServiceLifetimeConflict, $"FlowService '{implementationType.FullName}' declares more than one lifetime.");
                }

                implementations[implementationType] = attribute.Lifetime;
                var contractType = attribute.ContractType ?? implementationType;
                ValidateServiceContract(implementationType, contractType);
                var registration = new ServiceRegistration(implementationType, attribute.Lifetime);
                if (contracts.TryGetValue(contractType, out var existing) && existing != registration)
                {
                    throw new LibraryServiceException(LibraryErrorCodes.ServiceContractDuplicate, $"More than one FlowService declares the contract '{contractType.FullName ?? contractType.Name}'.");
                }

                contracts[contractType] = registration;
                // A class declared through an interface must still resolve by
                // its concrete type, matching the FlowService contract.
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
            throw new LibraryServiceException(LibraryErrorCodes.ServiceImplementationInvalid, $"FlowService '{implementationType.FullName ?? implementationType.Name}' must be a concrete, closed class.");
        }

        var constructors = implementationType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
        if (constructors.Length != 1)
        {
            throw new LibraryServiceException(LibraryErrorCodes.ServiceConstructorInvalid, $"FlowService '{implementationType.FullName ?? implementationType.Name}' must have exactly one public constructor.");
        }

        ValidateRestrictedConstructorParameters(implementationType, "service");
    }

    private static void ValidateServiceContract(Type implementationType, Type contractType)
    {
        if (contractType.IsPointer || contractType.IsByRef || contractType.ContainsGenericParameters || RestrictedConstructorTypes.Contains(contractType) || !contractType.IsAssignableFrom(implementationType))
        {
            throw new LibraryServiceException(LibraryErrorCodes.ServiceContractInvalid, $"FlowService '{implementationType.FullName ?? implementationType.Name}' cannot expose the contract '{contractType.FullName ?? contractType.Name}'.");
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

                throw new LibraryServiceException(LibraryErrorCodes.ServiceDependencyForbidden, $"The {kind} type '{type.FullName ?? type.Name}' cannot request '{parameter.ParameterType.FullName}' through constructor injection.");
            }
        }
    }

    private static ServiceLifetime ToServiceLifetime(FlowServiceLifetime lifetime)
    {
        global::Microsoft.Extensions.DependencyInjection.ServiceLifetime evaluatedValue20 = lifetime switch
        {
            FlowServiceLifetime.Run => ServiceLifetime.Singleton,
            FlowServiceLifetime.Invocation => ServiceLifetime.Scoped,
            FlowServiceLifetime.Transient => ServiceLifetime.Transient,
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Unsupported FlowService lifetime."),
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

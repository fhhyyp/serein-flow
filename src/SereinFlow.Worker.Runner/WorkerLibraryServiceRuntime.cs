using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Core.Api;
using SereinFlow.Library;
using SereinFlow.Runtime.Abstractions;

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

    public WorkerLibraryServiceRuntime(IMessageService messageService)
        => _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));

    public AsyncServiceScope CreateInvocationScope(Assembly libraryAssembly)
    {
        ArgumentNullException.ThrowIfNull(libraryAssembly);
        try
        {
            var provider = _providers.GetOrAdd(
                libraryAssembly,
                assembly => new Lazy<LibraryServiceProvider>(
                    () => LibraryServiceProvider.Create(assembly, _messageService),
                    LazyThreadSafetyMode.ExecutionAndPublication));
            return provider.Value.CreateInvocationScope();
        }
        catch (LibraryServiceException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new LibraryServiceException(
                "library.service_provider_failed",
                $"The library service provider could not be created. Library DI service provider creation failed. {exception.Message}",
                exception);
        }
    }

    public object CreateNodeInstance(IServiceProvider services, Type nodeType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(nodeType);
        try
        {
            LibraryServiceProvider.ValidateNodeType(nodeType);
            return ActivatorUtilities.CreateInstance(services, nodeType);
        }
        catch (LibraryServiceException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new LibraryServiceException(
                "library.service_activation_failed",
                $"The library node type '{nodeType.FullName ?? nodeType.Name}' could not be activated from declared FlowService dependencies. {exception.Message}",
                exception);
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
                Console.Error.WriteLine($"Worker library service cleanup failed. Worker library DI cleanup failed. {exception.Message}");
            }
        }
    }
}

internal sealed class LibraryServiceProvider : IAsyncDisposable
{
    private static readonly HashSet<Type> RestrictedConstructorTypes =
    [
        typeof(IServiceProvider),
        typeof(IServiceScopeFactory),
        typeof(IServiceProviderIsService),
        typeof(IServiceProviderIsKeyedService),
        typeof(IKeyedServiceProvider),
        typeof(IFlowContext),
    ];

    private readonly ServiceProvider _provider;

    private LibraryServiceProvider(ServiceProvider provider)
    {
        _provider = provider;
    }

    public static LibraryServiceProvider Create(Assembly assembly, IMessageService messageService)
    {
        var serviceTypes = DiscoverServiceTypes(assembly);
        var registrations = BuildRegistrations(serviceTypes);
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(messageService);

        foreach (var registration in registrations.Implementations)
        {
            services.Add(new ServiceDescriptor(
                registration.Key,
                registration.Key,
                ToServiceLifetime(registration.Value)));
        }

        foreach (var registration in registrations.Contracts)
        {
            var contractType = registration.Key;
            var implementation = registration.Value;
            if (contractType == implementation.ImplementationType)
                continue;

            services.Add(new ServiceDescriptor(
                contractType,
                provider => provider.GetRequiredService(implementation.ImplementationType),
                ToServiceLifetime(implementation.Lifetime)));
        }

        ServiceProvider? provider = null;
        try
        {
            provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true,
            });
            return new LibraryServiceProvider(provider);
        }
        catch (Exception exception)
        {
            provider?.Dispose();
            throw new LibraryServiceException(
                "library.service_validation_failed",
                $"The declared FlowService dependency graph is invalid. {exception.Message}",
                exception);
        }
    }

    public AsyncServiceScope CreateInvocationScope()
        => _provider.CreateAsyncScope();

    public ValueTask DisposeAsync()
        => _provider.DisposeAsync();

    internal static void ValidateNodeType(Type nodeType)
    {
        if (!nodeType.IsClass || nodeType.IsAbstract || nodeType.ContainsGenericParameters)
        {
            throw new LibraryServiceException(
                "library.service_node_type_invalid",
                $"The library node type '{nodeType.FullName ?? nodeType.Name}' cannot be constructed by the Worker service runtime.");
        }

        ValidateRestrictedConstructorParameters(nodeType, "node");
    }

    private static Type[] DiscoverServiceTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes()
                .Where(static type => type.GetCustomAttributes<FlowServiceAttribute>(inherit: false).Any())
                .ToArray();
        }
        catch (ReflectionTypeLoadException exception)
        {
            var details = string.Join(
                " ",
                exception.LoaderExceptions
                    .Where(static item => item is not null)
                    .Select(static item => item!.Message));
            throw new LibraryServiceException(
                "library.service_discovery_failed",
                $"FlowService types could not be loaded from '{assembly.GetName().Name}'. {details}",
                exception);
        }
        catch (Exception exception)
        {
            throw new LibraryServiceException(
                "library.service_discovery_failed",
                $"FlowService types could not be discovered from '{assembly.GetName().Name}'. {exception.Message}",
                exception);
        }
    }

    private static ServiceRegistrations BuildRegistrations(IEnumerable<Type> serviceTypes)
    {
        var implementations = new Dictionary<Type, FlowServiceLifetime>();
        var contracts = new Dictionary<Type, ServiceRegistration>();

        foreach (var implementationType in serviceTypes)
        {
            ValidateServiceImplementation(implementationType);
            var attributes = implementationType.GetCustomAttributes<FlowServiceAttribute>(inherit: false).ToArray();
            foreach (var attribute in attributes)
            {
                if (!Enum.IsDefined(attribute.Lifetime))
                {
                    throw new LibraryServiceException(
                        "library.service_lifetime_invalid",
                        $"FlowService '{implementationType.FullName}' declares an unsupported lifetime.");
                }

                if (implementations.TryGetValue(implementationType, out var previousLifetime)
                    && previousLifetime != attribute.Lifetime)
                {
                    throw new LibraryServiceException(
                        "library.service_lifetime_conflict",
                        $"FlowService '{implementationType.FullName}' declares more than one lifetime.");
                }
                implementations[implementationType] = attribute.Lifetime;

                var contractType = attribute.ContractType ?? implementationType;
                ValidateServiceContract(implementationType, contractType);
                var registration = new ServiceRegistration(implementationType, attribute.Lifetime);
                if (contracts.TryGetValue(contractType, out var existing)
                    && existing != registration)
                {
                    throw new LibraryServiceException(
                        "library.service_contract_duplicate",
                        $"More than one FlowService declares the contract '{contractType.FullName ?? contractType.Name}'.");
                }
                contracts[contractType] = registration;

                // A class declared through an interface must still resolve by
                // its concrete type, matching the FlowService contract.
                contracts.TryAdd(implementationType, registration);
            }
        }

        return new ServiceRegistrations(implementations, contracts);
    }

    private static void ValidateServiceImplementation(Type implementationType)
    {
        if (!implementationType.IsClass
            || implementationType.IsAbstract
            || implementationType.ContainsGenericParameters
            || implementationType.IsPointer
            || implementationType.IsByRef)
        {
            throw new LibraryServiceException(
                "library.service_implementation_invalid",
                $"FlowService '{implementationType.FullName ?? implementationType.Name}' must be a concrete, closed class.");
        }

        var constructors = implementationType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
        if (constructors.Length != 1)
        {
            throw new LibraryServiceException(
                "library.service_constructor_invalid",
                $"FlowService '{implementationType.FullName ?? implementationType.Name}' must have exactly one public constructor.");
        }

        ValidateRestrictedConstructorParameters(implementationType, "service");
    }

    private static void ValidateServiceContract(Type implementationType, Type contractType)
    {
        if (contractType.IsPointer
            || contractType.IsByRef
            || contractType.ContainsGenericParameters
            || RestrictedConstructorTypes.Contains(contractType)
            || !contractType.IsAssignableFrom(implementationType))
        {
            throw new LibraryServiceException(
                "library.service_contract_invalid",
                $"FlowService '{implementationType.FullName ?? implementationType.Name}' cannot expose the contract '{contractType.FullName ?? contractType.Name}'.");
        }
    }

    private static void ValidateRestrictedConstructorParameters(Type type, string kind)
    {
        foreach (var constructor in type.GetConstructors(BindingFlags.Instance | BindingFlags.Public))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                if (!RestrictedConstructorTypes.Contains(parameter.ParameterType))
                    continue;

                throw new LibraryServiceException(
                    "library.service_dependency_forbidden",
                    $"The {kind} type '{type.FullName ?? type.Name}' cannot request '{parameter.ParameterType.FullName}' through constructor injection.");
            }
        }
    }

    private static ServiceLifetime ToServiceLifetime(FlowServiceLifetime lifetime)
        => lifetime switch
        {
            FlowServiceLifetime.Run => ServiceLifetime.Singleton,
            FlowServiceLifetime.Invocation => ServiceLifetime.Scoped,
            FlowServiceLifetime.Transient => ServiceLifetime.Transient,
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Unsupported FlowService lifetime."),
        };

    private sealed record ServiceRegistration(Type ImplementationType, FlowServiceLifetime Lifetime);

    private sealed record ServiceRegistrations(
        IReadOnlyDictionary<Type, FlowServiceLifetime> Implementations,
        IReadOnlyDictionary<Type, ServiceRegistration> Contracts);
}

internal sealed class LibraryServiceException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}

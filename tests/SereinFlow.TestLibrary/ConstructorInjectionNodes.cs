using System;
using SereinFlow.Core.Api;

namespace SereinFlow.TestLibrary;

public interface IRunMarker
{
    Guid InstanceId { get; }
}

[FlowService<IRunMarker>]
public sealed class RunMarker : IRunMarker
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

public interface IInvocationMarker
{
    Guid InstanceId { get; }
}

[FlowService(typeof(IInvocationMarker), Lifetime = FlowServiceLifetime.Invocation)]
public sealed class InvocationMarker : IInvocationMarker
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

[FlowService(Lifetime = FlowServiceLifetime.Transient)]
public sealed class TransientMarker
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

[FlowLibrary("Constructor injection test library")]
public sealed class ConstructorInjectionNodes
{
    private readonly RunMarker _runByConcrete;
    private readonly IRunMarker _runByContract;
    private readonly InvocationMarker _invocationByConcrete;
    private readonly IInvocationMarker _invocationByContract;
    private readonly TransientMarker _firstTransient;
    private readonly TransientMarker _secondTransient;

    public ConstructorInjectionNodes(
        RunMarker runByConcrete,
        IRunMarker runByContract,
        InvocationMarker invocationByConcrete,
        IInvocationMarker invocationByContract,
        TransientMarker firstTransient,
        TransientMarker secondTransient)
    {
        _runByConcrete = runByConcrete;
        _runByContract = runByContract;
        _invocationByConcrete = invocationByConcrete;
        _invocationByContract = invocationByContract;
        _firstTransient = firstTransient;
        _secondTransient = secondTransient;
    }

    [FlowNode(Id = "test.di.describe", AnotherName = "Describe injected services")]
    public string Describe()
        => string.Join(
            ";",
            _runByConcrete.InstanceId.ToString("N"),
            _runByContract.InstanceId.ToString("N"),
            ReferenceEquals(_runByConcrete, _runByContract),
            _invocationByConcrete.InstanceId.ToString("N"),
            _invocationByContract.InstanceId.ToString("N"),
            ReferenceEquals(_invocationByConcrete, _invocationByContract),
            ReferenceEquals(_firstTransient, _secondTransient));

    [FlowNode(Id = "test.di.run-scope", AnotherName = "Describe run and invocation scopes")]
    public string DescribeRunAndInvocationScopes()
        => string.Join(
            ";",
            _runByConcrete.InstanceId.ToString("N"),
            _invocationByConcrete.InstanceId.ToString("N"));
}

[FlowLibrary("DI guard test library")]
public sealed class ForbiddenProviderNode
{
    public ForbiddenProviderNode(IServiceProvider provider)
    {
        _ = provider;
    }

    [FlowNode(Id = "test.di.forbidden-provider", AnotherName = "Forbidden service provider")]
    public string Execute() => "unreachable";
}

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Core.Api;
using SereinFlow.Domain;
using SereinFlow.Library;
using SereinFlow.Runtime;
using SereinFlow.Runtime.Abstractions;
using NodeType = SereinFlow.Domain.NodeType;

using SereinFlow.Contracts;
namespace SereinFlow.Worker.Runner;

internal sealed partial class LibraryNodeExecutor : INodeExecutor, IGlobalFlipflopExecutor
{
    private readonly NodeType _nodeType;
    private readonly WorkerLibraryRuntimeCache _cache;
    private readonly WorkerLibraryServiceRuntime _services;

    public LibraryNodeExecutor(
        NodeType nodeType,
        WorkerLibraryRuntimeCache cache,
        WorkerLibraryServiceRuntime services)
    {
        _nodeType = nodeType;
        _cache = cache;
        _services = services;
    }

    public NodeType NodeType => _nodeType;

    public ValueTask<NodeExecutionResult> WaitForTriggerAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        => ExecuteAsync(request, cancellationToken);

    public async ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var auditInputs = new Dictionary<string, object?>(StringComparer.Ordinal);
        AsyncServiceScope? invocationScope = null;
        object? target = null;
        if (request.Node.Runtime is null)
        {
            return MissingMetadataResult();
        }

        try
        {
            var resolved = await _cache.ResolveMethodAsync(request.Node, cancellationToken);
            var method = resolved.Method;
            var resultAttribute = method.GetCustomAttribute<NodeResultAttribute>(inherit: false);
            if (_nodeType == NodeType.Flipflop && !IsTask(method.ReturnType))
            {
                return NodeExecutionResult.Error(
                    LibraryErrorCodes.FlipflopReturnTypeInvalid,
                    "Flipflop methods must return Task or Task<T>. Flipflop 方法必须返回 Task 或 Task<T>。");
            }

            (invocationScope, target) = CreateInvocationTarget(resolved, method, resultAttribute);
            var flowContext = CreateFlowContext(request, cancellationToken);
            var arguments = BuildArguments(request, method, flowContext, auditInputs);

            var invocationResult = method.Invoke(target, arguments);
            var valueResult = await AwaitResultAsync(invocationResult, method.ReturnType, cancellationToken);
            return CreateExecutionResult(
                flowContext,
                invocationScope,
                resultAttribute,
                valueResult,
                auditInputs);
        }
        catch (LibraryInputException exception)
        {
            return NodeExecutionResult.Error(exception.Code, exception.Message) with
            {
                Inputs = SnapshotInputs(auditInputs)
            };
        }
        catch (LibraryRuntimeCacheException exception)
        {
            return NodeExecutionResult.Error(exception.Code, exception.Message) with { Inputs = SnapshotInputs(auditInputs) };
        }
        catch (LibraryServiceException exception)
        {
            return NodeExecutionResult.Error(exception.Code, exception.Message) with { Inputs = SnapshotInputs(auditInputs) };
        }
        catch (LibraryResultConverterException exception)
        {
            return NodeExecutionResult.Error(exception.Code, exception.Message) with { Inputs = SnapshotInputs(auditInputs) };
        }
        catch (FlowWorkpieceException exception)
        {
            return NodeExecutionResult.Error(exception.Code, exception.Message) with { Inputs = SnapshotInputs(auditInputs) };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation is a run lifecycle signal, not a node error. Let
            // the runner publish the correct Cancelled or TimedOut terminal state.
            // 取消是运行生命周期信号，而非节点错误；交由 Runner 发布正确的取消或超时终态。
            throw;
        }
        catch (TargetInvocationException exception) when (
            cancellationToken.IsCancellationRequested
            && exception.InnerException is OperationCanceledException)
        {
            throw new OperationCanceledException(
                "The library operation was cancelled. 类库操作已取消。",
                exception.InnerException,
                cancellationToken);
        }
        catch (TargetInvocationException exception)
        {
            if (exception.InnerException is FlowNativeLibraryException nativeLibraryException)
                return NodeExecutionResult.Error(nativeLibraryException.Code, nativeLibraryException.Message) with
                {
                    Inputs = SnapshotInputs(auditInputs)
                };

            if (exception.InnerException is FlowWorkpieceException workpieceException)
                return NodeExecutionResult.Error(workpieceException.Code, workpieceException.Message) with
                {
                    Inputs = SnapshotInputs(auditInputs)
                };

            var detail = exception.InnerException?.Message ?? exception.Message;
            return CreateInvocationError(detail, auditInputs);
        }
        catch (Exception exception)
        {
            return CreateExecutionError(exception.Message, auditInputs);
        }
        finally
        {
            await DisposeNodeInstanceAsync(target);
            if (invocationScope is { } scope)
                await scope.DisposeAsync();
        }
    }

    private NodeExecutionResult MissingMetadataResult()
        => _nodeType == NodeType.Action
            ? NodeExecutionResult.Success()
            : NodeExecutionResult.Error(FlipFlopErrorCodes.MetadataMissing, "Flipflop method metadata is missing. Flipflop 方法元数据缺失。");

    private (AsyncServiceScope? Scope, object? Target) CreateInvocationTarget(
        ResolvedLibraryMethod resolved,
        MethodInfo method,
        NodeResultAttribute? resultAttribute)
    {
        if (method.IsStatic && resultAttribute is null)
            return (null, null);

        var scope = _services.CreateInvocationScope(resolved.DeclaringType.Assembly);
        var target = method.IsStatic
            ? null
            : _services.CreateNodeInstance(scope.ServiceProvider, resolved.DeclaringType);
        return (scope, target);
    }

    private static LibraryFlowContext CreateFlowContext(
        NodeExecutionRequest request,
        CancellationToken cancellationToken)
        => new(
            request.Context is FlowExecutionSession session ? session.RunId : Guid.Empty,
            request.Node.Id,
            cancellationToken,
            request.ExecutionId == Guid.Empty ? Guid.NewGuid() : request.ExecutionId);

    private NodeExecutionResult CreateExecutionResult(
        LibraryFlowContext flowContext,
        AsyncServiceScope? invocationScope,
        NodeResultAttribute? resultAttribute,
        object? valueResult,
        IReadOnlyDictionary<string, object?> auditInputs)
    {
        var outputs = valueResult is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?> { ["result"] = valueResult };
        var result = NodeExecutionResult.Success(outputs);
        if (resultAttribute is not null)
        {
            var transfer = TransferResult(
                invocationScope?.ServiceProvider
                    ?? throw new LibraryResultConverterException(
                        LibraryErrorCodes.ResultConverterScopeMissing,
                        "A result converter requires an invocation scope. 节点结果转换器需要调用作用域。"),
                resultAttribute.ConverterType,
                valueResult);
            result = result with
            {
                TransferOutputs = new Dictionary<string, object?> { ["result"] = transfer }
            };
        }

        return flowContext.Apply(result) with { Inputs = SnapshotInputs(auditInputs) };
    }

    private NodeExecutionResult CreateInvocationError(
        string detail,
        IReadOnlyDictionary<string, object?> auditInputs)
        => _nodeType == NodeType.Flipflop
            ? NodeExecutionResult.Error(FlipFlopErrorCodes.ExecutionFailed, $"Flipflop method invocation failed. Flipflop 方法调用失败。 {detail}") with { Inputs = SnapshotInputs(auditInputs) }
            : NodeExecutionResult.Error(LibraryErrorCodes.InvocationFailed, $"Library method invocation failed. 类库方法调用失败。 {detail}") with { Inputs = SnapshotInputs(auditInputs) };

    private NodeExecutionResult CreateExecutionError(
        string detail,
        IReadOnlyDictionary<string, object?> auditInputs)
        => _nodeType == NodeType.Flipflop
            ? NodeExecutionResult.Error(FlipFlopErrorCodes.ExecutionFailed, $"Flipflop execution failed. Flipflop 执行失败。 {detail}") with { Inputs = SnapshotInputs(auditInputs) }
            : NodeExecutionResult.Error(LibraryErrorCodes.InvocationFailed, $"Library invocation failed. 类库调用失败。 {detail}") with { Inputs = SnapshotInputs(auditInputs) };

    private static bool IsTask(Type type)
        => type == typeof(Task) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>));

    private static async Task<object?> AwaitResultAsync(object? value, Type returnType, CancellationToken cancellationToken)
    {
        if (!IsTask(returnType))
            return value;
        if (value is not Task task)
            return null;
        await task.WaitAsync(cancellationToken);
        return returnType.IsGenericType ? returnType.GetProperty("Result")?.GetValue(task) : null;
    }

    private object? TransferResult(IServiceProvider services, Type converterType, object? primitive)
    {
        try
        {
            var converter = _services.CreateNodeResultConverter(services, converterType);
            var contract = converterType.GetInterfaces()
                .SingleOrDefault(static item => item.IsGenericType
                    && item.GetGenericTypeDefinition() == typeof(INodeResultConverter<,>))
                ?? throw new LibraryResultConverterException(
                    LibraryErrorCodes.ResultConverterInvalid,
                    $"Result converter '{converterType.FullName ?? converterType.Name}' does not implement a closed converter contract. 节点结果转换器未实现闭合的转换器合同。");
            var primitiveType = contract.GetGenericArguments()[0];
            if (primitive is null && primitiveType.IsValueType && Nullable.GetUnderlyingType(primitiveType) is null)
            {
                throw new LibraryResultConverterException(
                    LibraryErrorCodes.ResultConverterInputInvalid,
                    $"The result converter '{converterType.FullName ?? converterType.Name}' cannot receive a null value of '{primitiveType.FullName}'. 节点结果转换器无法接收“{primitiveType.FullName}”的 null 值。");
            }
            if (primitive is not null && !primitiveType.IsInstanceOfType(primitive))
            {
                throw new LibraryResultConverterException(
                    LibraryErrorCodes.ResultConverterInputInvalid,
                    $"The node returned '{primitive.GetType().FullName}' but converter '{converterType.FullName ?? converterType.Name}' expects '{primitiveType.FullName}'. 节点返回类型与结果转换器输入类型不匹配。");
            }

            var transferMethod = contract.GetMethod(nameof(INodeResultConverter<object, object>.Transfer))
                ?? throw new LibraryResultConverterException(
                    LibraryErrorCodes.ResultConverterInvalid,
                    $"Result converter '{converterType.FullName ?? converterType.Name}' has no Transfer method. 节点结果转换器缺少 Transfer 方法。");
            return transferMethod.Invoke(converter, [primitive]);
        }
        catch (LibraryServiceException)
        {
            throw;
        }
        catch (LibraryResultConverterException)
        {
            throw;
        }
        catch (TargetInvocationException exception)
        {
            var detail = exception.InnerException?.Message ?? exception.Message;
            throw new LibraryResultConverterException(
                LibraryErrorCodes.ResultConversionFailed,
                $"The node result could not be converted by '{converterType.FullName ?? converterType.Name}'. 节点结果无法由转换器转换。 {detail}",
                exception.InnerException ?? exception);
        }
        catch (Exception exception)
        {
            throw new LibraryResultConverterException(
                LibraryErrorCodes.ResultConversionFailed,
                $"The node result could not be converted by '{converterType.FullName ?? converterType.Name}'. 节点结果无法由转换器转换。 {exception.Message}",
                exception);
        }
    }

    private static Dictionary<string, object?> SnapshotInputs(IReadOnlyDictionary<string, object?> inputs)
        => new Dictionary<string, object?>(inputs, StringComparer.Ordinal);

    private static async ValueTask DisposeNodeInstanceAsync(object? target)
    {
        switch (target)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }

}

internal sealed class LibraryResultConverterException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}

internal sealed class LibraryInputException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Domain;
using SereinFlow.Runtime;
using SereinFlow.Runtime.Abstractions;
using SereinFlow.ScriptAdapter;

namespace SereinFlow.Worker.Runner;

internal sealed class LibraryNodeExecutor : INodeExecutor, IGlobalFlipflopExecutor
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
            return _nodeType == NodeType.Action
                ? NodeExecutionResult.Success()
                : NodeExecutionResult.Error("flipflop.metadata_missing", "Flipflop method metadata is missing. Flipflop 方法元数据缺失。");
        }

        try
        {
            var resolved = await _cache.ResolveMethodAsync(request.Node, cancellationToken);
            var method = resolved.Method;
            if (_nodeType == NodeType.Flipflop && !IsTask(method.ReturnType))
            {
                return NodeExecutionResult.Error(
                    "library.flipflop_return_type_invalid",
                    "Flipflop methods must return Task or Task<T>. Flipflop 方法必须返回 Task 或 Task<T>。");
            }

            if (!method.IsStatic)
            {
                invocationScope = _services.CreateInvocationScope(resolved.DeclaringType.Assembly);
                target = _services.CreateNodeInstance(invocationScope.Value.ServiceProvider, resolved.DeclaringType);
            }
            var parameters = method.GetParameters();
            var arguments = new object?[parameters.Length];
            var flowContext = new LibraryFlowContext(
                request.Context is FlowExecutionSession session ? session.RunId : Guid.Empty,
                request.Node.Id,
                cancellationToken);
            var ordinaryParameterIndex = 0;
            for (var index = 0; index < parameters.Length; index++)
            {
                if (WorkerLibraryRuntimeCache.IsFlowContextParameter(parameters[index]))
                {
                    arguments[index] = flowContext;
                    continue;
                }

                var name = parameters[index].Name ?? $"param{index + 1}";
                var parameterDefinition = request.Node.Parameters.FirstOrDefault(item =>
                    string.Equals(item.Name, name, StringComparison.Ordinal)
                    || string.Equals(item.Id, name, StringComparison.Ordinal));
                parameterDefinition ??= ordinaryParameterIndex < request.Node.Parameters.Count
                    ? request.Node.Parameters[ordinaryParameterIndex]
                    : null;

                if (parameters[index].GetCustomAttribute<ParamArrayAttribute>() is not null)
                {
                    try
                    {
                        arguments[index] = BuildVariadicArgument(
                            request,
                            parameters[index],
                            parameterDefinition,
                            auditInputs);
                        ordinaryParameterIndex++;
                        continue;
                    }
                    catch (Exception exception)
                    {
                        return NodeExecutionResult.Error(
                            exception is ScriptValueConversionException
                                ? "script.value_conversion_failed"
                                : "node.input_invalid",
                            $"Library input '{name}' cannot be converted to '{parameters[index].ParameterType.Name}'. 类库输入“{name}”无法转换为“{parameters[index].ParameterType.Name}”。 {exception.Message}") with
                        {
                            Inputs = SnapshotInputs(auditInputs)
                        };
                    }
                }

                if (!request.Inputs.TryGetValue(name, out var value)
                    && (parameterDefinition is null || !request.Inputs.TryGetValue(parameterDefinition.Id, out value)))
                {
                    if (parameters[index].HasDefaultValue)
                    {
                        arguments[index] = parameters[index].DefaultValue;
                        auditInputs[name] = arguments[index];
                        ordinaryParameterIndex++;
                        continue;
                    }
                    if (!parameters[index].ParameterType.IsValueType
                        || Nullable.GetUnderlyingType(parameters[index].ParameterType) is not null)
                    {
                        arguments[index] = null;
                        auditInputs[name] = null;
                        ordinaryParameterIndex++;
                        continue;
                    }
                    return NodeExecutionResult.Error(
                        "node.input_missing",
                        $"Required library input '{name}' is missing. 缺少类库必需输入“{name}”。") with
                    {
                        Inputs = SnapshotInputs(auditInputs)
                    };
                }

                auditInputs[name] = value;
                try
                {
                    arguments[index] = LibraryArgumentConverter.Convert(value, parameters[index].ParameterType);
                    auditInputs[name] = arguments[index];
                }
                catch (Exception exception)
                {
                    if (exception is ScriptValueConversionException)
                    {
                        return NodeExecutionResult.Error("script.value_conversion_failed", exception.Message) with
                        {
                            Inputs = SnapshotInputs(auditInputs)
                        };
                    }

                    return NodeExecutionResult.Error(
                        "node.input_invalid",
                        $"Library input '{name}' cannot be converted to '{parameters[index].ParameterType.Name}'. 类库输入“{name}”无法转换为“{parameters[index].ParameterType.Name}”。 {exception.Message}") with
                    {
                        Inputs = SnapshotInputs(auditInputs)
                    };
                }
                ordinaryParameterIndex++;
            }

            var invocationResult = method.Invoke(target, arguments);
            var valueResult = await AwaitResultAsync(invocationResult, method.ReturnType, cancellationToken);
            var result = valueResult is null
                ? NodeExecutionResult.Success()
                : NodeExecutionResult.Success(new Dictionary<string, object?> { ["result"] = valueResult });
            return flowContext.Apply(result) with { Inputs = SnapshotInputs(auditInputs) };
        }
        catch (LibraryRuntimeCacheException exception)
        {
            return NodeExecutionResult.Error(exception.Code, exception.Message) with { Inputs = SnapshotInputs(auditInputs) };
        }
        catch (LibraryServiceException exception)
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
            var detail = exception.InnerException?.Message ?? exception.Message;
            return _nodeType == NodeType.Flipflop
                ? NodeExecutionResult.Error("flipflop.execution_failed", $"Flipflop method invocation failed. Flipflop 方法调用失败。 {detail}") with { Inputs = SnapshotInputs(auditInputs) }
                : NodeExecutionResult.Error("library.invocation_failed", $"Library method invocation failed. 类库方法调用失败。 {detail}") with { Inputs = SnapshotInputs(auditInputs) };
        }
        catch (Exception exception)
        {
            return _nodeType == NodeType.Flipflop
                ? NodeExecutionResult.Error("flipflop.execution_failed", $"Flipflop execution failed. Flipflop 执行失败。 {exception.Message}") with { Inputs = SnapshotInputs(auditInputs) }
                : NodeExecutionResult.Error("library.invocation_failed", $"Library invocation failed. 类库调用失败。 {exception.Message}") with { Inputs = SnapshotInputs(auditInputs) };
        }
        finally
        {
            await DisposeNodeInstanceAsync(target);
            if (invocationScope is { } scope)
                await scope.DisposeAsync();
        }
    }

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

    private static Array BuildVariadicArgument(
        NodeExecutionRequest request,
        ParameterInfo parameter,
        NodeParameterDefinition? representative,
        Dictionary<string, object?> auditInputs)
    {
        var arrayType = parameter.ParameterType;
        var elementType = arrayType.GetElementType()
            ?? throw new InvalidOperationException("The params array has no element type. params 数组没有元素类型。");
        var groupId = representative?.VariadicGroupId ?? representative?.Id ?? parameter.Name ?? string.Empty;
        var definitions = request.Node.Parameters
            .Where(item => item.IsVariadic
                && string.Equals(item.VariadicGroupId ?? item.Id, groupId, StringComparison.Ordinal))
            .ToArray();
        if (definitions.Length == 0 && representative?.IsVariadic == true)
            definitions = [representative];

        if (definitions.Length == 1 && definitions[0].VariadicMode == VariadicParameterMode.Collection)
        {
            var collection = definitions[0];
            if (!TryGetInput(request.Inputs, collection, out var value))
                return Array.CreateInstance(elementType, 0);
            var converted = LibraryArgumentConverter.Convert(value, arrayType);
            auditInputs[collection.Name] = converted;
            return (Array)(converted ?? Array.CreateInstance(elementType, 0));
        }

        var values = new List<object?>();
        foreach (var definition in definitions)
        {
            if (!TryGetInput(request.Inputs, definition, out var value) || value is null)
            {
                if (definition.Required)
                    throw new InvalidOperationException($"Required variadic input '{definition.Name}' is missing. 缺少必需可变参数输入“{definition.Name}”。");
                continue;
            }
            auditInputs[definition.Name] = value;
            var converted = LibraryArgumentConverter.Convert(value, elementType);
            auditInputs[definition.Name] = converted;
            values.Add(converted);
        }

        var result = Array.CreateInstance(elementType, values.Count);
        for (var index = 0; index < values.Count; index++)
            result.SetValue(values[index], index);
        return result;
    }

    private static bool TryGetInput(
        IReadOnlyDictionary<string, object?> inputs,
        NodeParameterDefinition parameter,
        out object? value)
        => inputs.TryGetValue(parameter.Id, out value)
            || inputs.TryGetValue(parameter.Name, out value);

    private sealed class LibraryFlowContext(Guid runId, string nodeId, CancellationToken cancellationToken) : IFlowContext
    {
        private ExecutionBranch _branch = ExecutionBranch.Success;
        private string? _code;
        private string? _message;

        public Guid RunId { get; } = runId;

        public string NodeId { get; } = nodeId;

        public CancellationToken CancellationToken { get; } = cancellationToken;

        public void SelectSuccess()
        {
            _branch = ExecutionBranch.Success;
            _code = null;
            _message = null;
        }

        public void SelectFailure(string? code = null, string? message = null)
        {
            _branch = ExecutionBranch.Failure;
            _code = string.IsNullOrWhiteSpace(code) ? "node.branch_failure" : code;
            _message = string.IsNullOrWhiteSpace(message)
                ? "The node selected the Failure branch. 节点选择了 Failure 分支。"
                : message;
        }

        public void SelectError(string? code = null, string? message = null)
        {
            _branch = ExecutionBranch.Error;
            _code = string.IsNullOrWhiteSpace(code) ? "node.branch_error" : code;
            _message = string.IsNullOrWhiteSpace(message)
                ? "The node selected the Error branch. 节点选择了 Error 分支。"
                : message;
        }

        public NodeExecutionResult Apply(NodeExecutionResult result)
            => _branch == ExecutionBranch.Success
                ? result
                : new NodeExecutionResult(false, result.Outputs, _branch, _code, _message);
    }
}

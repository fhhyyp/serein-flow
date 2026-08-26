using System.Reflection;
using SereinFlow.Domain;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Worker.Runner;

internal sealed class LibraryNodeExecutor : INodeExecutor, IGlobalFlipflopExecutor
{
    private readonly NodeType _nodeType;
    private readonly WorkerLibraryRuntimeCache _cache;

    public LibraryNodeExecutor(NodeType nodeType, WorkerLibraryRuntimeCache cache)
    {
        _nodeType = nodeType;
        _cache = cache;
    }

    public NodeType NodeType => _nodeType;

    public ValueTask<NodeExecutionResult> WaitForTriggerAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        => ExecuteAsync(request, cancellationToken);

    public async ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var auditInputs = new Dictionary<string, object?>(StringComparer.Ordinal);
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

            var target = method.IsStatic ? null : Activator.CreateInstance(resolved.DeclaringType);
            var parameters = method.GetParameters();
            var arguments = new object?[parameters.Length];
            for (var index = 0; index < parameters.Length; index++)
            {
                var name = parameters[index].Name ?? $"param{index + 1}";
                var parameterDefinition = request.Node.Parameters.FirstOrDefault(item =>
                    string.Equals(item.Name, name, StringComparison.Ordinal)
                    || string.Equals(item.Id, name, StringComparison.Ordinal));
                parameterDefinition ??= index < request.Node.Parameters.Count
                    ? request.Node.Parameters[index]
                    : null;
                if (!request.Inputs.TryGetValue(name, out var value)
                    && (parameterDefinition is null || !request.Inputs.TryGetValue(parameterDefinition.Id, out value)))
                {
                    if (parameters[index].HasDefaultValue)
                    {
                        arguments[index] = parameters[index].DefaultValue;
                        auditInputs[name] = arguments[index];
                        continue;
                    }
                    if (!parameters[index].ParameterType.IsValueType
                        || Nullable.GetUnderlyingType(parameters[index].ParameterType) is not null)
                    {
                        arguments[index] = null;
                        auditInputs[name] = null;
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
                    return NodeExecutionResult.Error(
                        "node.input_invalid",
                        $"Library input '{name}' cannot be converted to '{parameters[index].ParameterType.Name}'. 类库输入“{name}”无法转换为“{parameters[index].ParameterType.Name}”。 {exception.Message}") with
                    {
                        Inputs = SnapshotInputs(auditInputs)
                    };
                }
            }

            var invocationResult = method.Invoke(target, arguments);
            var valueResult = await AwaitResultAsync(invocationResult, method.ReturnType, cancellationToken);
            var result = valueResult is null
                ? NodeExecutionResult.Success()
                : NodeExecutionResult.Success(new Dictionary<string, object?> { ["result"] = valueResult });
            return result with { Inputs = SnapshotInputs(auditInputs) };
        }
        catch (LibraryRuntimeCacheException exception)
        {
            return NodeExecutionResult.Error(exception.Code, exception.Message) with { Inputs = SnapshotInputs(auditInputs) };
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
}

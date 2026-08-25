using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using SereinFlow.Domain;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Worker.Runner;

internal sealed class LibraryNodeExecutor : INodeExecutor, IGlobalFlipflopExecutor
{
    private readonly NodeType _nodeType;
    private readonly string? _packageRoot;
    private readonly Guid _runId;

    public LibraryNodeExecutor(NodeType nodeType, string? packageRoot, Guid runId)
    {
        _nodeType = nodeType;
        _packageRoot = packageRoot;
        _runId = runId;
    }

    public NodeType NodeType => _nodeType;

    public ValueTask<NodeExecutionResult> WaitForTriggerAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        => ExecuteAsync(request, cancellationToken);

    public async ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runtime = request.Node.Runtime;
        if (runtime is null)
        {
            return _nodeType == NodeType.Action
                ? NodeExecutionResult.Success()
                : NodeExecutionResult.Error("flipflop.metadata_missing", "Flipflop method metadata is missing. Flipflop 方法元数据缺失。");
        }

        if (string.IsNullOrWhiteSpace(runtime.LibraryId) || string.IsNullOrWhiteSpace(runtime.MethodName))
        {
            return NodeExecutionResult.Error(
                _nodeType == NodeType.Action ? "action.metadata_missing" : "flipflop.metadata_missing",
                _nodeType == NodeType.Action
                    ? "Action library method metadata is missing. Action 类库方法元数据缺失。"
                    : "Flipflop method metadata is missing. Flipflop 方法元数据缺失。");
        }

        if (string.IsNullOrWhiteSpace(_packageRoot))
            return NodeExecutionResult.Error("library.root_missing", "The library package root is not configured. 类库包根目录未配置。");

        if (!IsSafeSegment(runtime.LibraryId))
            return NodeExecutionResult.Error("library.path_invalid", "The library identifier is not a safe path segment. 类库标识不是安全的路径片段。");

        var packagePath = ResolvePackagePath(_packageRoot, runtime.LibraryId);
        if (!File.Exists(packagePath))
            return NodeExecutionResult.Error("library.not_found", $"Library package '{runtime.LibraryId}' was not found. 未找到类库包“{runtime.LibraryId}”。");

        var tempRoot = Path.Combine(Path.GetTempPath(), "sereinflow-worker", _runId.ToString("N"), runtime.LibraryId);
        Directory.CreateDirectory(tempRoot);
        var extracted = Path.Combine(tempRoot, "extracted.marker");
        if (!File.Exists(extracted))
        {
            ZipFile.ExtractToDirectory(packagePath, tempRoot, overwriteFiles: true);
            File.WriteAllText(extracted, DateTimeOffset.UtcNow.ToString("O"));
        }

        var dllPath = Directory.EnumerateFiles(tempRoot, runtime.DllName ?? string.Empty, SearchOption.AllDirectories).FirstOrDefault();
        if (dllPath is null)
            return NodeExecutionResult.Error("library.assembly_not_found", "The library assembly was not found. 未找到类库程序集。");

        var loadContext = new AssemblyLoadContext($"sereinflow-{_runId:N}-{runtime.LibraryId}", isCollectible: true);
        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(dllPath);
            var type = assembly.GetType(runtime.ClassName ?? string.Empty, throwOnError: false, ignoreCase: false);
            var method = type?.GetMethod(runtime.MethodName!, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            if (type is null || method is null)
                return NodeExecutionResult.Error("library.method_not_found", "The library method was not found. 未找到类库方法。");

            if (_nodeType == NodeType.Flipflop && !IsTask(method.ReturnType))
                return NodeExecutionResult.Error("library.flipflop_return_type_invalid", "Flipflop methods must return Task or Task<T>. Flipflop 方法必须返回 Task 或 Task<T>。");

            var target = method.IsStatic ? null : Activator.CreateInstance(type);
            var parameters = method.GetParameters();
            var arguments = new object?[parameters.Length];
            for (var index = 0; index < parameters.Length; index++)
            {
                var name = parameters[index].Name ?? $"param{index + 1}";
                var parameterDefinition = request.Node.Parameters.FirstOrDefault(item =>
                    string.Equals(item.Name, name, StringComparison.Ordinal)
                    || string.Equals(item.Id, name, StringComparison.Ordinal));
                // Older flow snapshots stored the connector id (for example
                // `1`) as the parameter name. When metadata names no longer
                // match the reflected method name, preserve positional
                // binding so those snapshots can still execute safely.
                // 旧流程快照可能把连接器 ID（例如 `1`）保存成参数名；名称不匹配时按方法参数顺序绑定。
                parameterDefinition ??= index < request.Node.Parameters.Count
                    ? request.Node.Parameters[index]
                    : null;
                if (!request.Inputs.TryGetValue(name, out var value)
                    && (parameterDefinition is null || !request.Inputs.TryGetValue(parameterDefinition.Id, out value)))
                {
                    if (parameters[index].HasDefaultValue)
                    {
                        arguments[index] = parameters[index].DefaultValue;
                        continue;
                    }

                    if (!parameters[index].ParameterType.IsValueType
                        || Nullable.GetUnderlyingType(parameters[index].ParameterType) is not null)
                    {
                        arguments[index] = null;
                        continue;
                    }

                    return NodeExecutionResult.Error(
                        "node.input_missing",
                        $"Required library input '{name}' is missing. 缺少类库必需输入“{name}”。");
                }

                try
                {
                    arguments[index] = LibraryArgumentConverter.Convert(value, parameters[index].ParameterType);
                }
                catch (Exception exception)
                {
                    return NodeExecutionResult.Error(
                        "node.input_invalid",
                        $"Library input '{name}' cannot be converted to '{parameters[index].ParameterType.Name}'. 类库输入“{name}”无法转换为“{parameters[index].ParameterType.Name}”。 {exception.Message}");
                }
            }

            var invocationResult = method.Invoke(target, arguments);
            var valueResult = await AwaitResultAsync(invocationResult, method.ReturnType, cancellationToken);
            return valueResult is null
                ? NodeExecutionResult.Success()
                : NodeExecutionResult.Success(new Dictionary<string, object?> { ["result"] = valueResult });
        }
        catch (TargetInvocationException exception)
        {
            var detail = exception.InnerException?.Message ?? exception.Message;
            return _nodeType == NodeType.Flipflop
                ? NodeExecutionResult.Error("flipflop.execution_failed", $"Flipflop method invocation failed. Flipflop 方法调用失败。 {detail}")
                : NodeExecutionResult.Error("library.invocation_failed", $"Library method invocation failed. 类库方法调用失败。 {detail}");
        }
        catch (Exception exception)
        {
            return _nodeType == NodeType.Flipflop
                ? NodeExecutionResult.Error("flipflop.execution_failed", $"Flipflop execution failed. Flipflop 执行失败。 {exception.Message}")
                : NodeExecutionResult.Error("library.invocation_failed", $"Library invocation failed. 类库调用失败。 {exception.Message}");
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private static string ResolvePackagePath(string root, string libraryId)
    {
        var packages = Path.Combine(root, "packages", $"{libraryId}.zip");
        return File.Exists(packages) ? packages : Path.Combine(root, $"{libraryId}.zip");
    }

    private static bool IsSafeSegment(string value)
        => !string.IsNullOrWhiteSpace(value)
            && value is not "." and not ".."
            && value == Path.GetFileName(value)
            && !value.Contains(Path.DirectorySeparatorChar)
            && !value.Contains(Path.AltDirectorySeparatorChar)
            && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

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

}

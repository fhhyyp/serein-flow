using System.Collections.Concurrent;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using SereinFlow.Domain;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Worker.Runner;

/// <summary>
/// Owns external library load state for one Worker Run. The cache deliberately
/// excludes user object instances: each node invocation receives a fresh
/// instance while assemblies and reflection metadata remain shared.
/// 管理单个 Worker Run 的外部类库加载状态。缓存不保存用户对象实例：每次节点调用都
/// 新建实例，但程序集和反射元数据会在当前运行中共享。
/// </summary>
internal sealed class WorkerLibraryRuntimeCache : IAsyncDisposable
{
    private readonly string? _packageRoot;
    private readonly string _runRoot;
    private readonly HashSet<string>? _allowedLibraryIds;
    private readonly ConcurrentDictionary<LibraryKey, Lazy<Task<LoadedLibrary>>> _libraries = new();
    private readonly ConcurrentDictionary<TypeKey, Lazy<Task<Type>>> _types = new();
    private readonly ConcurrentDictionary<MethodKey, Lazy<Task<ResolvedLibraryMethod>>> _methods = new();

    public WorkerLibraryRuntimeCache(
        string? packageRoot,
        Guid runId,
        IReadOnlyCollection<string>? allowedLibraryIds = null)
    {
        _packageRoot = string.IsNullOrWhiteSpace(packageRoot) ? null : Path.GetFullPath(packageRoot);
        _runRoot = Path.Combine(Path.GetTempPath(), "sereinflow-worker", runId.ToString("N"));
        _allowedLibraryIds = allowedLibraryIds is null
            ? null
            : allowedLibraryIds
                .Where(static id => !string.IsNullOrWhiteSpace(id))
                .Select(static id => id.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ResolvedLibraryMethod> ResolveMethodAsync(NodeDefinition node, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runtime = node.Runtime ?? throw new LibraryRuntimeCacheException(
            "library.metadata_missing",
            "The library method metadata is missing. 类库方法元数据缺失。");
        var libraryId = RequireSafeSegment(runtime.LibraryId, "library.identifier_invalid", "The library identifier is invalid. 类库标识无效。");
        if (_allowedLibraryIds is not null && !_allowedLibraryIds.Contains(libraryId))
        {
            throw new LibraryRuntimeCacheException(
                "library.not_allowed",
                "The library artifact is not authorized for this run. 当前运行未授权使用该类库制品。");
        }
        var dllName = RequireSafeSegment(runtime.DllName, "library.assembly_invalid", "The library assembly file name is invalid. 类库程序集文件名无效。");
        var className = RequireNonEmpty(runtime.ClassName, "library.type_invalid", "The library type name is invalid. 类库类型名称无效。");
        var methodName = RequireNonEmpty(runtime.MethodName, "library.method_invalid", "The library method name is invalid. 类库方法名称无效。");
        var parameterSignature = string.Join("\u001f", node.Parameters.Select(static parameter => parameter.Name));
        var key = new MethodKey(libraryId, dllName, className, methodName, parameterSignature);
        var lazy = _methods.GetOrAdd(key, _ => new Lazy<Task<ResolvedLibraryMethod>>(
            () => ResolveMethodCoreAsync(new LibraryKey(libraryId, dllName), className, methodName, node.Parameters),
            LazyThreadSafetyMode.ExecutionAndPublication));
        return await lazy.Value.WaitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        foreach (var lazy in _libraries.Values)
        {
            if (!lazy.IsValueCreated || !lazy.Value.IsCompletedSuccessfully)
                continue;
            lazy.Value.Result.LoadContext.Unload();
        }

        try
        {
            if (Directory.Exists(_runRoot))
                Directory.Delete(_runRoot, recursive: true);
        }
        catch (Exception exception)
        {
            // stdout is reserved for the Worker protocol. Supervisor captures
            // stderr as a diagnostic without corrupting the protocol stream.
            // stdout 仅承载 Worker 协议；Supervisor 会将 stderr 记录为诊断信息。
            Console.Error.WriteLine($"Worker library cache cleanup failed. Worker 类库缓存清理失败。 {exception.Message}");
        }
        return ValueTask.CompletedTask;
    }

    private async Task<ResolvedLibraryMethod> ResolveMethodCoreAsync(
        LibraryKey libraryKey,
        string className,
        string methodName,
        IReadOnlyList<NodeParameterDefinition> parameters)
    {
        var typeKey = new TypeKey(libraryKey.LibraryId, libraryKey.DllName, className);
        var typeLazy = _types.GetOrAdd(typeKey, _ => new Lazy<Task<Type>>(
            () => ResolveTypeCoreAsync(libraryKey, className),
            LazyThreadSafetyMode.ExecutionAndPublication));
        var type = await typeLazy.Value;
        var candidates = type
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
            .ToArray();
        if (candidates.Length == 0)
        {
            throw new LibraryRuntimeCacheException(
                "library.method_not_found",
                "The library method was not found. 未找到类库方法。");
        }

        var expectedNames = parameters
            .Where(static parameter => !parameter.IsVariadic)
            .Select(static parameter => parameter.Name)
            .ToArray();
        var matched = candidates
            .Where(method => IsCandidateCompatible(method, expectedNames, parameters.Any(static parameter => parameter.IsVariadic)))
            .ToArray();
        var method = matched.Length == 1
            ? matched[0]
            : candidates.Length == 1
                ? candidates[0]
                : candidates.SingleOrDefault(candidate => IsCandidateCompatible(candidate, expectedNames, parameters.Any(static parameter => parameter.IsVariadic)));
        if (method is null)
        {
            throw new LibraryRuntimeCacheException(
                "library.method_ambiguous",
                "The library method overload is ambiguous. 类库方法重载不明确。");
        }
        return new ResolvedLibraryMethod(type, method);
    }

    private static bool IsCandidateCompatible(
        MethodInfo method,
        string[] expectedNames,
        bool hasVariadicDefinitions)
    {
        var parameters = method.GetParameters();
        var visible = parameters.Where(static parameter => !IsFlowContextParameter(parameter)).ToArray();
        var methodHasVariadic = visible.LastOrDefault()?.GetCustomAttribute<ParamArrayAttribute>() is not null;
        if (methodHasVariadic != hasVariadicDefinitions)
            return false;
        var ordinary = methodHasVariadic ? visible[..^1] : visible;
        return ordinary.Length == expectedNames.Length
            && ordinary.Select(static parameter => parameter.Name ?? string.Empty)
                .SequenceEqual(expectedNames, StringComparer.Ordinal);
    }

    internal static bool IsFlowContextParameter(ParameterInfo parameter)
        => string.Equals(parameter.ParameterType.FullName, typeof(IFlowContext).FullName, StringComparison.Ordinal);

    private async Task<Type> ResolveTypeCoreAsync(LibraryKey libraryKey, string className)
    {
        var libraryLazy = _libraries.GetOrAdd(libraryKey, key => new Lazy<Task<LoadedLibrary>>(
            () => LoadLibraryCoreAsync(key),
            LazyThreadSafetyMode.ExecutionAndPublication));
        var library = await libraryLazy.Value;
        return library.Assembly.GetType(className, throwOnError: false, ignoreCase: false)
            ?? throw new LibraryRuntimeCacheException(
                "library.type_not_found",
                "The library type was not found. 未找到类库类型。");
    }

    private Task<LoadedLibrary> LoadLibraryCoreAsync(LibraryKey key)
    {
        if (string.IsNullOrWhiteSpace(_packageRoot) || !Directory.Exists(_packageRoot))
        {
            throw new LibraryRuntimeCacheException(
                "library.root_missing",
                "The library package root is not configured. 类库包根目录未配置。");
        }

        var packagePath = ResolvePackagePath(_packageRoot, key.LibraryId);
        if (!File.Exists(packagePath))
        {
            throw new LibraryRuntimeCacheException(
                "library.not_found",
                $"Library package '{key.LibraryId}' was not found. 未找到类库包“{key.LibraryId}”。");
        }

        var extractRoot = Path.Combine(_runRoot, key.LibraryId);
        Directory.CreateDirectory(extractRoot);
        ZipFile.ExtractToDirectory(packagePath, extractRoot, overwriteFiles: true);
        var dllPath = Directory
            .EnumerateFiles(extractRoot, key.DllName, SearchOption.AllDirectories)
            .FirstOrDefault();
        if (dllPath is null)
        {
            throw new LibraryRuntimeCacheException(
                "library.assembly_not_found",
                "The library assembly was not found. 未找到类库程序集。");
        }

        var loadContext = new LibraryLoadContext($"sereinflow-{Path.GetFileName(_runRoot)}-{key.LibraryId}", dllPath);
        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(dllPath);
            return Task.FromResult(new LoadedLibrary(loadContext, assembly));
        }
        catch
        {
            loadContext.Unload();
            throw;
        }
    }

    private static string ResolvePackagePath(string root, string libraryId)
    {
        var packages = Path.GetFullPath(Path.Combine(root, "packages", $"{libraryId}.zip"));
        if (IsWithinRoot(packages, root) && File.Exists(packages))
            return packages;
        var direct = Path.GetFullPath(Path.Combine(root, $"{libraryId}.zip"));
        return IsWithinRoot(direct, root) ? direct : throw new LibraryRuntimeCacheException(
            "library.path_invalid",
            "The library package path is outside the configured root. 类库包路径超出了配置根目录。");
    }

    private static bool IsWithinRoot(string candidate, string root)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string RequireSafeSegment(string? value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value is "." or ".."
            || value != Path.GetFileName(value)
            || value.Contains(Path.DirectorySeparatorChar)
            || value.Contains(Path.AltDirectorySeparatorChar)
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new LibraryRuntimeCacheException(code, message);
        }
        return value;
    }

    private static string RequireNonEmpty(string? value, string code, string message)
        => string.IsNullOrWhiteSpace(value)
            ? throw new LibraryRuntimeCacheException(code, message)
            : value;

    private sealed class LibraryLoadContext(string name, string assemblyPath) : AssemblyLoadContext(name, isCollectible: true)
    {
        private readonly AssemblyDependencyResolver _resolver = new(assemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (string.Equals(assemblyName.Name, typeof(IFlowContext).Assembly.GetName().Name, StringComparison.Ordinal))
                return typeof(IFlowContext).Assembly;
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }

        protected override nint LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path is null ? nint.Zero : LoadUnmanagedDllFromPath(path);
        }
    }

    private sealed record LibraryKey(string LibraryId, string DllName);
    private sealed record TypeKey(string LibraryId, string DllName, string ClassName);
    private sealed record MethodKey(string LibraryId, string DllName, string ClassName, string MethodName, string Signature);
    private sealed record LoadedLibrary(AssemblyLoadContext LoadContext, Assembly Assembly);
}

internal sealed record ResolvedLibraryMethod(Type DeclaringType, MethodInfo Method);

internal sealed class LibraryRuntimeCacheException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using SereinFlow.Contracts;
using SereinFlow.Library;
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
    public WorkerLibraryRuntimeCache(string? packageRoot, Guid runId, IReadOnlyCollection<string>? allowedLibraryIds = null)
    {
        _packageRoot = string.IsNullOrWhiteSpace(packageRoot) ? null : Path.GetFullPath(packageRoot);
        var getTempPathResult = Path.GetTempPath();
        var toStringResult = runId.ToString("N");
        _runRoot = Path.Combine(getTempPathResult, "sereinflow-worker", toStringResult);
        _allowedLibraryIds = allowedLibraryIds is null ? null : resolveToHashSetResult();
        HashSet<string> resolveToHashSetResult()
        {
            var whereResult = allowedLibraryIds.Where(static id =>
            {
                var evaluatedValue = !string.IsNullOrWhiteSpace(id);
                return evaluatedValue;
            });
            var selectResult = whereResult.Select(static id =>
            {
                var trimResult = id.Trim();
                return trimResult;
            });
            var toHashSetResult = selectResult.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return toHashSetResult;
        }
    }

    public async Task<ResolvedLibraryMethod> ResolveMethodAsync(NodeDefinition node, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runtime = node.Runtime ?? throw new LibraryRuntimeCacheException(LibraryErrorCodes.MetadataMissing, "The library method metadata is missing. 类库方法元数据缺失。");
        var libraryId = RequireSafeSegment(runtime.LibraryId, LibraryErrorCodes.IdentifierInvalid, "The library identifier is invalid. 类库标识无效。");
        if (_allowedLibraryIds is not null && !_allowedLibraryIds.Contains(libraryId))
        {
            throw new LibraryRuntimeCacheException(LibraryErrorCodes.NotAllowed, "The library artifact is not authorized for this run. 当前运行未授权使用该类库制品。");
        }

        var dllName = RequireSafeSegment(runtime.DllName, LibraryErrorCodes.AssemblyInvalid, "The library assembly file name is invalid. 类库程序集文件名无效。");
        var className = RequireNonEmpty(runtime.ClassName, LibraryErrorCodes.TypeInvalid, "The library type name is invalid. 类库类型名称无效。");
        var methodName = RequireNonEmpty(runtime.MethodName, LibraryErrorCodes.MethodInvalid, "The library method name is invalid. 类库方法名称无效。");
        var selectResult3 = node.Parameters.Select(static parameter => parameter.Name);
        var parameterSignature = string.Join("\u001f", selectResult3);
        var key = new MethodKey(libraryId, dllName, className, methodName, parameterSignature);
        var lazy = _methods.GetOrAdd(key, _ =>
        {
            var lazy2 = new Lazy<Task<ResolvedLibraryMethod>>(() =>
            {
                var libraryKey1 = new LibraryKey(libraryId, dllName);
                var resolveMethodCoreAsyncResult = ResolveMethodCoreAsync(libraryKey1, className, methodName, node.Parameters);
                return resolveMethodCoreAsyncResult;
            }, LazyThreadSafetyMode.ExecutionAndPublication);
            return lazy2;
        });
        var waitAsyncResult = await lazy.Value.WaitAsync(cancellationToken);
        return waitAsyncResult;
    }

    public ValueTask DisposeAsync()
    {
        foreach (var lazy in _libraries.Values)
        {
            if (!lazy.IsValueCreated || !lazy.Value.IsCompletedSuccessfully)
            {
                continue;
            }

            var loaded = lazy.Value.Result;
            loaded.NativeLibraryLoader.Dispose();
            loaded.LoadContext.Unload();
        }

        try
        {
            if (Directory.Exists(_runRoot))
            {
                Directory.Delete(_runRoot, recursive: true);
            }
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

    private async Task<ResolvedLibraryMethod> ResolveMethodCoreAsync(LibraryKey libraryKey, string className, string methodName, IReadOnlyList<NodeParameterDefinition> parameters)
    {
        var typeKey = new TypeKey(libraryKey.LibraryId, libraryKey.DllName, className);
        var typeLazy = _types.GetOrAdd(typeKey, _ =>
        {
            var lazy4 = new Lazy<Task<Type>>(() =>
            {
                var resolveTypeCoreAsyncResult = ResolveTypeCoreAsync(libraryKey, className);
                return resolveTypeCoreAsyncResult;
            }, LazyThreadSafetyMode.ExecutionAndPublication);
            return lazy4;
        });
        var type = await typeLazy.Value;
        var getMethodsResult = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        var whereResult7 = getMethodsResult.Where(method =>
        {
            var equalsResult = string.Equals(method.Name, methodName, StringComparison.Ordinal);
            return equalsResult;
        });
        var candidates = whereResult7.ToArray();
        if (candidates.Length == 0)
        {
            throw new LibraryRuntimeCacheException(LibraryErrorCodes.MethodNotFound, "The library method was not found. 未找到类库方法。");
        }

        var whereResult8 = parameters.Where(static parameter => !parameter.IsVariadic);
        var selectResult9 = whereResult8.Select(static parameter => parameter.Name);
        var expectedNames = selectResult9.ToArray();
        var whereResult10 = candidates.Where(method =>
        {
            var anyResult = parameters.Any(static parameter => parameter.IsVariadic);
            var isCandidateCompatibleResult = IsCandidateCompatible(method, expectedNames, anyResult);
            return isCandidateCompatibleResult;
        });
        var matched = whereResult10.ToArray();
        var method = matched.Length == 1 ? matched[0] : candidates.Length == 1 ? candidates[0] : candidates.SingleOrDefault(candidate =>
        {
            var anyResult5 = parameters.Any(static parameter => parameter.IsVariadic);
            var isCandidateCompatibleResult6 = IsCandidateCompatible(candidate, expectedNames, anyResult5);
            return isCandidateCompatibleResult6;
        });
        if (method is null)
        {
            throw new LibraryRuntimeCacheException(LibraryErrorCodes.MethodAmbiguous, "The library method overload is ambiguous. 类库方法重载不明确。");
        }

        var resolvedLibraryMethod = new ResolvedLibraryMethod(type, method);
        return resolvedLibraryMethod;
    }

    private static bool IsCandidateCompatible(MethodInfo method, string[] expectedNames, bool hasVariadicDefinitions)
    {
        var parameters = method.GetParameters();
        var whereResult12 = parameters.Where(static parameter =>
        {
            var evaluatedValue11 = !IsFlowContextParameter(parameter);
            return evaluatedValue11;
        });
        var visible = whereResult12.ToArray();
        var lastVisibleParameter = visible.LastOrDefault();
        var variadicAttribute = lastVisibleParameter?.GetCustomAttribute<ParamArrayAttribute>();
        var methodHasVariadic = variadicAttribute is not null;
        if (methodHasVariadic != hasVariadicDefinitions)
        {
            return false;
        }

        var ordinary = methodHasVariadic ? visible[..^1] : visible;
        var evaluatedValue14 = ordinary.Length == expectedNames.Length && resolveSequenceEqualResult();
        return evaluatedValue14;
        bool resolveSequenceEqualResult()
        {
            var selectResult13 = ordinary.Select(static parameter => parameter.Name ?? string.Empty);
            var sequenceEqualResult = selectResult13.SequenceEqual(expectedNames, StringComparer.Ordinal);
            return sequenceEqualResult;
        }
    }

    internal static bool IsFlowContextParameter(ParameterInfo parameter)
    {
        var equalsResult15 = string.Equals(parameter.ParameterType.FullName, typeof(IFlowContext).FullName, StringComparison.Ordinal);
        return equalsResult15;
    }

    private async Task<Type> ResolveTypeCoreAsync(LibraryKey libraryKey, string className)
    {
        var libraryLazy = _libraries.GetOrAdd(libraryKey, key =>
        {
            var lazy16 = new Lazy<Task<LoadedLibrary>>(() =>
            {
                var loadLibraryCoreAsyncResult = LoadLibraryCoreAsync(key);
                return loadLibraryCoreAsyncResult;
            }, LazyThreadSafetyMode.ExecutionAndPublication);
            return lazy16;
        });
        var library = await libraryLazy.Value;
        var type = library.Assembly.GetType(className, throwOnError: false, ignoreCase: false) ?? throw new LibraryRuntimeCacheException(LibraryErrorCodes.TypeNotFound, "The library type was not found. 未找到类库类型。");
        try
        {
            var getCustomAttributesResult = type.GetCustomAttributes<NativeLibraryDirectoryAttribute>(inherit: false);
            library.NativeLibraryLoader.LoadDeclaredDirectories(getCustomAttributesResult);
        }
        catch (FlowNativeLibraryException exception)
        {
            throw new LibraryRuntimeCacheException(exception.Code, exception.Message, exception);
        }

        return type;
    }

    internal IFlowNativeLibraryLoader GetNativeLibraryLoader(Assembly libraryAssembly)
    {
        ArgumentNullException.ThrowIfNull(libraryAssembly);
        foreach (var lazy in _libraries.Values)
        {
            if (!lazy.IsValueCreated || !lazy.Value.IsCompletedSuccessfully)
            {
                continue;
            }

            var loaded = lazy.Value.Result;
            if (ReferenceEquals(loaded.Assembly, libraryAssembly))
            {
                return loaded.NativeLibraryLoader;
            }
        }

        throw new LibraryRuntimeCacheException(LibraryErrorCodes.NativeLoaderUnavailable, "The native library loader was not available for the loaded assembly. 已加载程序集没有可用的 Native 类库加载器。");
    }

    private Task<LoadedLibrary> LoadLibraryCoreAsync(LibraryKey key)
    {
        if (string.IsNullOrWhiteSpace(_packageRoot) || !Directory.Exists(_packageRoot))
        {
            throw new LibraryRuntimeCacheException(LibraryErrorCodes.RootMissing, "The library package root is not configured. 类库包根目录未配置。");
        }

        var packagePath = ResolvePackagePath(_packageRoot, key.LibraryId);
        if (!File.Exists(packagePath))
        {
            throw new LibraryRuntimeCacheException(LibraryErrorCodes.NotFound, $"Library package '{key.LibraryId}' was not found. 未找到类库包“{key.LibraryId}”。");
        }

        var extractRoot = Path.Combine(_runRoot, key.LibraryId);
        Directory.CreateDirectory(extractRoot);
        ExtractPackage(packagePath, extractRoot);
        var enumerateFilesResult = Directory.EnumerateFiles(extractRoot, "*", SearchOption.AllDirectories);
        var whereResult19 = enumerateFilesResult.Where(path =>
        {
            var getFileNameResult = Path.GetFileName(path);
            var equalsResult17 = string.Equals(getFileNameResult, key.DllName, StringComparison.OrdinalIgnoreCase);
            return equalsResult17;
        });
        var dllPaths = whereResult19.ToArray();
        if (dllPaths.Length == 0)
        {
            throw new LibraryRuntimeCacheException(LibraryErrorCodes.AssemblyNotFound, "The library assembly was not found. 未找到类库程序集。");
        }

        if (dllPaths.Length > 1)
        {
            throw new LibraryRuntimeCacheException(LibraryErrorCodes.AssemblyAmbiguous, "The library package contains more than one matching assembly. 类库包包含多个匹配的程序集。");
        }

        var dllPath = dllPaths[0];
        var evaluatedValue20 = $"sereinflow-{Path.GetFileName(_runRoot)}-{key.LibraryId}";
        var loadContext = new LibraryLoadContext(evaluatedValue20, dllPath);
        var libraryRoot = Path.GetDirectoryName(dllPath) ?? throw new LibraryRuntimeCacheException(LibraryErrorCodes.AssemblyPathInvalid, "The library assembly directory could not be determined. 无法确定类库程序集目录。");
        var nativeLibraryLoader = new WorkerNativeLibraryLoader(libraryRoot, loadContext.LoadNativeLibraryFromPath);
        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(dllPath);
            var getCustomAttributesResult18 = assembly.GetCustomAttributes<NativeLibraryDirectoryAttribute>();
            nativeLibraryLoader.LoadDeclaredDirectories(getCustomAttributesResult18);
            var loadedLibrary = new LoadedLibrary(loadContext, assembly, nativeLibraryLoader);
            var fromResultResult = Task.FromResult(loadedLibrary);
            return fromResultResult;
        }
        catch (FlowNativeLibraryException exception)
        {
            nativeLibraryLoader.Dispose();
            loadContext.Unload();
            throw new LibraryRuntimeCacheException(exception.Code, exception.Message, exception);
        }
        catch
        {
            nativeLibraryLoader.Dispose();
            loadContext.Unload();
            throw;
        }
    }

    private static void ExtractPackage(string packagePath, string extractRoot)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        if (archive.Entries.Count == 0)
        {
            throw new LibraryRuntimeCacheException(LibraryErrorCodes.PackageEmpty, "The library package is empty. 类库包为空。");
        }

        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            var normalizedName = entry.FullName.Replace('\\', '/');
            var isDirectory = string.IsNullOrEmpty(entry.Name);
            ValidateArchivePath(normalizedName, isDirectory);
            var pathKey = normalizedName.TrimEnd('/');
            if (!seenPaths.Add(pathKey))
            {
                throw new LibraryRuntimeCacheException(LibraryErrorCodes.PackageDuplicateEntry, "The library package contains duplicate entries. 类库包包含重复条目。");
            }

            var relativePath = normalizedName.Replace('/', Path.DirectorySeparatorChar);
            var combineResult = Path.Combine(extractRoot, relativePath);
            var destinationPath = Path.GetFullPath(combineResult);
            if (!IsWithinRoot(destinationPath, extractRoot))
            {
                throw new LibraryRuntimeCacheException(LibraryErrorCodes.PackagePathInvalid, "The library package contains a path outside its extraction directory. 类库包包含超出解压目录的路径。");
            }

            if (isDirectory)
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrWhiteSpace(destinationDirectory))
            {
                throw new LibraryRuntimeCacheException(LibraryErrorCodes.PackagePathInvalid, "The library package entry has no valid parent directory. 类库包条目没有有效的父目录。");
            }

            Directory.CreateDirectory(destinationDirectory);
            entry.ExtractToFile(destinationPath, overwrite: false);
        }
    }

    private static void ValidateArchivePath(string normalizedName, bool isDirectory)
    {
        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.StartsWith('/') || (normalizedName.Length >= 2 && normalizedName[1] == ':'))
        {
            throw new LibraryRuntimeCacheException(LibraryErrorCodes.PackagePathInvalid, "The library package contains an unsafe path. 类库包包含不安全的路径。");
        }

        var segments = normalizedName.Split('/');
        if (isDirectory && segments[^1].Length == 0)
        {
            segments = segments[..^1];
        }

        if (segments.Length == 0 || segments.Any(static segment =>
        {
            var evaluatedValue21 = segment.Length == 0 || segment is "." or ".." || segment.Contains(':') || resolveIndexOfAnyResult() >= 0;
            return evaluatedValue21;
            int resolveIndexOfAnyResult()
            {
                var getInvalidFileNameCharsResult = Path.GetInvalidFileNameChars();
                var indexOfAnyResult = segment.IndexOfAny(getInvalidFileNameCharsResult);
                return indexOfAnyResult;
            }
        }))
        {
            throw new LibraryRuntimeCacheException(LibraryErrorCodes.PackagePathInvalid, "The library package contains an unsafe path. 类库包包含不安全的路径。");
        }
    }

    private static string ResolvePackagePath(string root, string libraryId)
    {
        var combineResult22 = Path.Combine(root, "packages", $"{libraryId}.zip");
        var packages = Path.GetFullPath(combineResult22);
        if (IsWithinRoot(packages, root) && File.Exists(packages))
        {
            return packages;
        }

        var combineResult23 = Path.Combine(root, $"{libraryId}.zip");
        var direct = Path.GetFullPath(combineResult23);
        string evaluatedValue24 = IsWithinRoot(direct, root) ? direct : throw new LibraryRuntimeCacheException(LibraryErrorCodes.PathInvalid, "The library package path is outside the configured root. 类库包路径超出了配置根目录。");
        return evaluatedValue24;
    }

    private static bool IsWithinRoot(string candidate, string root)
    {
        var normalizedRoot = resolveTrimEndingDirectorySeparatorResult() + Path.DirectorySeparatorChar;
        var startsWithResult = candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        return startsWithResult;
        string resolveTrimEndingDirectorySeparatorResult()
        {
            var getFullPathResult = Path.GetFullPath(root);
            var trimEndingDirectorySeparatorResult = Path.TrimEndingDirectorySeparator(getFullPathResult);
            return trimEndingDirectorySeparatorResult;
        }
    }

    private static string RequireSafeSegment(string? value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "." or ".." || value != Path.GetFileName(value) || value.Contains(Path.DirectorySeparatorChar) || value.Contains(Path.AltDirectorySeparatorChar) || resolveIndexOfAnyResult25() >= 0)
        {
            throw new LibraryRuntimeCacheException(code, message);
        }

        return value;
        int resolveIndexOfAnyResult25()
        {
            var getInvalidFileNameCharsResult26 = Path.GetInvalidFileNameChars();
            var indexOfAnyResult27 = value.IndexOfAny(getInvalidFileNameCharsResult26);
            return indexOfAnyResult27;
        }
    }

    private static string RequireNonEmpty(string? value, string code, string message)
    {
        string evaluatedValue28 = string.IsNullOrWhiteSpace(value) ? throw new LibraryRuntimeCacheException(code, message) : value;
        return evaluatedValue28;
    }

    private sealed class LibraryLoadContext(string name, string assemblyPath) : AssemblyLoadContext(name, isCollectible: true)
    {
        private readonly AssemblyDependencyResolver _resolver = new(assemblyPath);
        private readonly Dictionary<string, nint> _nativeLibraries = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _nativeGate = new();
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var evaluatedValue29 = typeof(FlowServiceAttribute).Assembly.GetName().Name;
            if (string.Equals(assemblyName.Name, evaluatedValue29, StringComparison.Ordinal))
            {
                return typeof(FlowServiceAttribute).Assembly;
            }

            var evaluatedValue30 = typeof(IFlowContext).Assembly.GetName().Name;
            if (string.Equals(assemblyName.Name, evaluatedValue30, StringComparison.Ordinal))
            {
                return typeof(IFlowContext).Assembly;
            }

            var evaluatedValue31 = typeof(IExecutionContext).Assembly.GetName().Name;
            if (string.Equals(assemblyName.Name, evaluatedValue31, StringComparison.Ordinal))
            {
                return typeof(IExecutionContext).Assembly;
            }

            var evaluatedValue32 = typeof(WorkerProtocol).Assembly.GetName().Name;
            if (string.Equals(assemblyName.Name, evaluatedValue32, StringComparison.Ordinal))
            {
                return typeof(WorkerProtocol).Assembly;
            }

            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            Assembly? evaluatedValue33 = path is null ? null : LoadFromAssemblyPath(path);
            return evaluatedValue33;
        }

        protected override nint LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            nint evaluatedValue34 = path is null ? nint.Zero : LoadNativeLibraryFromPath(path);
            return evaluatedValue34;
        }

        internal nint LoadNativeLibraryFromPath(string path)
        {
            var normalizedPath = Path.GetFullPath(path);
            lock (_nativeGate)
            {
                if (_nativeLibraries.TryGetValue(normalizedPath, out var handle))
                {
                    return handle;
                }

                handle = LoadUnmanagedDllFromPath(normalizedPath);
                if (handle != nint.Zero)
                {
                    _nativeLibraries[normalizedPath] = handle;
                }

                return handle;
            }
        }
    }

    private sealed record LibraryKey(string LibraryId, string DllName);
    private sealed record TypeKey(string LibraryId, string DllName, string ClassName);
    private sealed record MethodKey(string LibraryId, string DllName, string ClassName, string MethodName, string Signature);
    private sealed record LoadedLibrary(AssemblyLoadContext LoadContext, Assembly Assembly, WorkerNativeLibraryLoader NativeLibraryLoader);
}

internal sealed record ResolvedLibraryMethod(Type DeclaringType, MethodInfo Method);
internal sealed class LibraryRuntimeCacheException(string code, string message, Exception? innerException = null) : Exception(message, innerException)
{
    public string Code { get; } = code;
}

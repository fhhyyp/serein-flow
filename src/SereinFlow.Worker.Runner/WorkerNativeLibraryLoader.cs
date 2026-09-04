using System.Reflection;
using System.Runtime.InteropServices;
using SereinFlow.Core.Api;
using SereinFlow.Library;

namespace SereinFlow.Worker.Runner;

/// <summary>
/// Provides run-scoped, idempotent native loading for one extracted library package.
/// 为一个已解压类库包提供运行级、幂等的 Native 类库加载。
/// </summary>
internal sealed class WorkerNativeLibraryLoader : IFlowNativeLibraryLoader, IDisposable
{
    private readonly string _libraryRoot;
    private readonly Func<string, nint> _loadFromPath;
    private readonly object _gate = new();
    private readonly HashSet<string> _loadedPaths = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public WorkerNativeLibraryLoader(
        string libraryRoot,
        Func<string, nint> loadFromPath)
    {
        _libraryRoot = Path.GetFullPath(libraryRoot ?? throw new ArgumentNullException(nameof(libraryRoot)));
        _loadFromPath = loadFromPath ?? throw new ArgumentNullException(nameof(loadFromPath));
    }

    /// <inheritdoc />
    public bool LoadNativeLibrary(string relativeFile)
    {
        var path = ResolvePath(relativeFile, "library.native_path_invalid");
        if (!File.Exists(path))
            return false;

        LoadPath(relativeFile, path);
        return true;
    }

    /// <inheritdoc />
    public void LoadNativeLibraryDirectory(
        string relativeDirectory,
        bool recursive = true,
        bool required = true)
    {
        var directory = ResolvePath(relativeDirectory, "library.native_directory_invalid");
        if (!Directory.Exists(directory))
        {
            if (required)
            {
                throw new FlowNativeLibraryException(
                    "library.native_directory_missing",
                    $"The native library directory '{relativeDirectory}' was not found. 未找到 Native 类库目录“{relativeDirectory}”。",
                    relativeDirectory);
            }

            return;
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        string[] nativeFiles;
        try
        {
            nativeFiles = Directory
                .EnumerateFiles(directory, "*", searchOption)
                .Where(IsNativeLibraryFile)
                .Where(static path => !IsManagedAssembly(path))
                .OrderBy(static path => IsOpenCvShim(path) ? 1 : 0)
                .ThenBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception exception)
        {
            var failure = new FlowNativeLibraryException(
                "library.native_directory_scan_failed",
                $"The native library directory '{relativeDirectory}' could not be scanned. 无法扫描 Native 类库目录“{relativeDirectory}”。 {exception.Message}",
                relativeDirectory,
                exception);
            if (required)
                throw failure;

            Console.Error.WriteLine(
                $"Optional native library directory scan failed. 可选 Native 类库目录扫描失败。 code={failure.Code}; path={relativeDirectory}; message={failure.Message}");
            return;
        }

        if (nativeFiles.Length == 0)
        {
            if (required)
            {
                throw new FlowNativeLibraryException(
                    "library.native_directory_empty",
                    $"The native library directory '{relativeDirectory}' contains no native libraries. Native 类库目录“{relativeDirectory}”中没有 Native 类库。",
                    relativeDirectory);
            }

            return;
        }

        foreach (var nativeFile in nativeFiles)
        {
            var relativeFile = Path.GetRelativePath(_libraryRoot, nativeFile);
            try
            {
                LoadPath(relativeFile, nativeFile);
            }
            catch (FlowNativeLibraryException exception) when (!required)
            {
                // stderr is captured by WorkerSupervisor and is intentionally separate from the
                // framed stdout protocol.
                // stderr 会由 WorkerSupervisor 捕获，并且不会污染带帧的 stdout 协议。
                Console.Error.WriteLine(
                    $"Optional native library load failed. 可选 Native 类库加载失败。 code={exception.Code}; path={relativeFile}; message={exception.Message}");
            }
        }
    }

    internal void LoadDeclaredDirectories(
        IEnumerable<NativeLibraryDirectoryAttribute> declarations)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        foreach (var declaration in declarations)
        {
            LoadNativeLibraryDirectory(
                declaration.RelativePath,
                declaration.Recursive,
                declaration.Required);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _loadedPaths.Clear();
        }
    }

    private void LoadPath(string relativePath, string fullPath)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_loadedPaths.Contains(fullPath))
                return;

            try
            {
                var handle = _loadFromPath(fullPath);
                if (handle == nint.Zero)
                {
                    throw new InvalidOperationException(
                        "The native loader returned a null module handle. Native 加载器返回了空模块句柄。");
                }

                _loadedPaths.Add(fullPath);
            }
            catch (FlowNativeLibraryException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new FlowNativeLibraryException(
                    "library.native_load_failed",
                    $"The native library '{relativePath}' could not be loaded for runtime '{RuntimeInformation.RuntimeIdentifier}'. Native 类库“{relativePath}”无法在运行时“{RuntimeInformation.RuntimeIdentifier}”中加载。 {exception.Message}",
                    relativePath,
                    exception);
            }
        }
    }

    private string ResolvePath(string relativePath, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new FlowNativeLibraryException(
                errorCode,
                "A native library path is required. Native 类库路径不能为空。",
                relativePath ?? string.Empty);
        }

        var expanded = relativePath.Replace(
            "{rid}",
            RuntimeInformation.RuntimeIdentifier,
            StringComparison.OrdinalIgnoreCase);
        if (Path.IsPathRooted(expanded))
        {
            throw new FlowNativeLibraryException(
                errorCode,
                $"Native library paths must be relative to the library package. Native 类库路径必须是相对于类库包的路径：“{relativePath}”。",
                relativePath);
        }

        var normalized = expanded.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var segments = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(static segment => segment is "." or ".." || segment.Contains(':')))
        {
            throw new FlowNativeLibraryException(
                errorCode,
                $"Native library paths cannot contain parent or drive segments. Native 类库路径不能包含父目录或驱动器片段：“{relativePath}”。",
                relativePath);
        }

        var fullPath = Path.GetFullPath(Path.Combine(_libraryRoot, normalized));
        if (!IsWithinRoot(fullPath, _libraryRoot))
        {
            throw new FlowNativeLibraryException(
                errorCode,
                $"Native library path '{relativePath}' is outside the library package. Native 类库路径“{relativePath}”超出了类库包目录。",
                relativePath);
        }

        return fullPath;
    }

    private static bool IsNativeLibraryFile(string path)
    {
        var fileName = Path.GetFileName(path);
        return OperatingSystem.IsWindows()
            ? fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            : OperatingSystem.IsLinux()
                ? fileName.EndsWith(".so", StringComparison.OrdinalIgnoreCase)
                    || fileName.Contains(".so.", StringComparison.OrdinalIgnoreCase)
                : fileName.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase)
                    || fileName.EndsWith(".so", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsManagedAssembly(string path)
    {
        try
        {
            _ = AssemblyName.GetAssemblyName(path);
            return true;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    private static bool IsOpenCvShim(string path)
        => string.Equals(
            Path.GetFileNameWithoutExtension(path),
            "OpenCvSharpExtern",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsWithinRoot(string candidate, string root)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        return string.Equals(candidate, normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WorkerNativeLibraryLoader));
    }
}

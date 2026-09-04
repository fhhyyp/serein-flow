namespace SereinFlow.Library;

/// <summary>
/// Loads native dependencies that belong to the current library package.
/// 加载当前类库包所属的 Native 依赖。
/// </summary>
public interface IFlowNativeLibraryLoader
{
    /// <summary>
    /// Loads one native library using a path relative to the library assembly directory.
    /// 使用相对于类库程序集目录的路径加载一个 Native 类库。
    /// </summary>
    /// <param name="relativeFile">The relative path to the native library.</param>
    /// <returns><see langword="true"/> when the file was loaded or was already loaded.</returns>
    /// <exception cref="FlowNativeLibraryException">
    /// Thrown when the path is unsafe or the native library cannot be loaded.
    /// </exception>
    bool LoadNativeLibrary(string relativeFile);

    /// <summary>
    /// Loads native libraries from a directory relative to the library assembly directory.
    /// 加载相对于类库程序集目录的 Native 类库目录。
    /// </summary>
    /// <param name="relativeDirectory">The relative directory containing native libraries.</param>
    /// <param name="recursive">Whether nested directories should also be scanned.</param>
    /// <param name="required">
    /// Whether the directory and every loadable file must be available. Optional directories
    /// report failures to the Worker diagnostic stream and continue.
    /// </param>
    /// <exception cref="FlowNativeLibraryException">
    /// Thrown when a required directory or native library cannot be loaded.
    /// </exception>
    void LoadNativeLibraryDirectory(
        string relativeDirectory,
        bool recursive = true,
        bool required = true);
}
/// <summary>
/// Reports a rejected or failed native-library load requested by a node library.
/// 报告节点类库请求的 Native 类库加载被拒绝或失败。
/// </summary>
public sealed class FlowNativeLibraryException : Exception
{
    /// <summary>
    /// Initializes a native-library loading exception.
    /// 初始化 Native 类库加载异常。
    /// </summary>
    /// <param name="code">A machine-readable failure code.</param>
    /// <param name="message">A human-readable failure message.</param>
    /// <param name="relativePath">The package-relative path involved in the failure.</param>
    /// <param name="innerException">The underlying exception, when available.</param>
    public FlowNativeLibraryException(
        string code,
        string message,
        string relativePath,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentException("A native library error code is required.", nameof(code))
            : code;
        RelativePath = string.IsNullOrWhiteSpace(relativePath)
            ? throw new ArgumentException("A native library path is required.", nameof(relativePath))
            : relativePath;
    }

    /// <summary>
    /// Gets the machine-readable failure code.
    /// 获取机器可读的失败代码。
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the package-relative path involved in the failure.
    /// 获取发生失败的类库包相对路径。
    /// </summary>
    public string RelativePath { get; }
}

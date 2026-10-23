namespace SereinFlow.Library;
/// <summary>
/// Stores named CLR values shared by all contexts and node libraries in one flow run.
/// 存储一次流程运行中所有上下文和节点类库共用的具名 CLR 值。
/// </summary>
/// <remarks>
/// Each operation is protected by a lock. Values are stored as objects without serialization or
/// copying; callers must synchronize mutations to referenced objects and multi-operation updates.
/// 每个操作均受锁保护。值以 object 存储，不进行序列化或复制；引用对象的修改及多操作更新由调用方同步。
/// </remarks>
public sealed class LibraryVariableService
{
    private readonly Lock _sync = new();
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);
    /// <summary>
    /// Initializes an empty variable table. The Worker supplies one table per flow run.
    /// 初始化空变量表；Worker 为每次流程运行提供一张表。
    /// </summary>
    public LibraryVariableService()
    {
    }

    /// <summary>
    /// Determines whether the case-sensitive name exists, including variables holding null.
    /// 判断区分大小写的名称是否存在，包括值为 null 的变量。
    /// </summary>
    /// <param name = "name">The non-empty variable name. 非空变量名称。</param>
    /// <returns>Whether the variable exists. 变量是否存在。</returns>
    public bool Contain(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_sync)
        {
            return _values.ContainsKey(name);
        }
    }

    /// <summary>
    /// Gets the stored object, or null when the variable does not exist.
    /// 获取存储的对象；变量不存在时返回 null。
    /// </summary>
    /// <param name = "name">The non-empty variable name. 非空变量名称。</param>
    /// <returns>The original stored value, or null. 原始存储值或 null。</returns>
    public object? Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_sync)
        {
            return _values.GetValueOrDefault(name);
        }
    }

    /// <summary>
    /// Casts or unboxes the stored value to T; returns default(T) when the variable does not exist.
    /// 将存储值强制转换或拆箱为 T；变量不存在时返回 default(T)。
    /// </summary>
    /// <typeparam name = "T">The requested CLR type. 请求的 CLR 类型。</typeparam>
    /// <param name = "name">The non-empty variable name. 非空变量名称。</param>
    /// <returns>The typed value, or default(T) when absent. 泛型值；不存在时为 default(T)。</returns>
    /// <exception cref = "InvalidCastException">
    /// The value cannot be cast to T, or null is requested as a non-nullable value type.
    /// 值无法强制转换为 T，或尝试将 null 读取为非可空值类型。
    /// </exception>
    public T? Get<T>(string name)
    {
        TryGet<T>(name, out var value);
        return value;
    }

    /// <summary>
    /// Tries to find a variable and cast or unbox its value. Type mismatches throw rather than
    /// returning false.
    /// 尝试查找变量并强制转换或拆箱其值；类型不匹配时抛出异常而非返回 false。
    /// </summary>
    /// <typeparam name = "T">The requested CLR type. 请求的 CLR 类型。</typeparam>
    /// <param name = "name">The non-empty variable name. 非空变量名称。</param>
    /// <param name = "value">The typed value, or default(T) when absent. 泛型值；不存在时为 default(T)。</param>
    /// <returns>False only when the variable does not exist. 仅当变量不存在时返回 false。</returns>
    /// <exception cref = "InvalidCastException">
    /// The value cannot be cast to T, or null is requested as a non-nullable value type.
    /// 值无法强制转换为 T，或尝试将 null 读取为非可空值类型。
    /// </exception>
    public bool TryGet<T>(string name, out T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_sync)
        {
            if (!_values.TryGetValue(name, out var stored))
            {
                value = default;
                return false;
            }

            if (stored is null && default(T) is not null)
            {
                throw new InvalidCastException($"Variable '{name}' is null and cannot be cast to '{typeof(T).FullName}'. 变量为 null，无法转换为目标类型。");
            }

            value = (T?)stored;
            return true;
        }
    }

    /// <summary>
    /// Sets or replaces a variable, boxing value types and retaining reference types as-is.
    /// 设置或覆盖变量；值类型装箱，引用类型保留原始引用。
    /// </summary>
    /// <typeparam name = "T">The CLR type of the value. 值的 CLR 类型。</typeparam>
    /// <param name = "name">The non-empty variable name. 非空变量名称。</param>
    /// <param name = "value">The value to store, including null. 要存储的值，允许 null。</param>
    public void Set<T>(string name, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_sync)
        {
            _values[name] = value;
        }
    }

    /// <summary>
    /// Copies the table under its lock. Stored objects retain their original references.
    /// 在锁保护下复制变量表；存储对象仍保留原始引用。
    /// </summary>
    /// <returns>An independent dictionary of the current names and values. 当前名称和值的独立字典。</returns>
    public IReadOnlyDictionary<string, object?> Snapshot()
    {
        lock (_sync)
        {
            return new Dictionary<string, object?>(_values, StringComparer.Ordinal);
        }
    }
}

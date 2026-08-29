using System.Text;

namespace ScriptLang.Runtime.ByteCode;

/// <summary>表示一个字节码块</summary>
public sealed class ByteCodeChunk
{
    /// <summary>创建空的字节码块（编译时使用）</summary>
    public ByteCodeChunk() { }

    /// <summary>获取字节码指令列表</summary>
    public List<Instruction> Code { get; } = [];

    /// <summary>编译时确定的变量表</summary>
    public VariableTable? VariableTable { get; set; }

    /// <summary>常量表（LoadConst 使用）</summary>
    private readonly List<object?> _constants = [];

    /// <summary>常量去重索引</summary>
    private readonly Dictionary<object, int> _constantMap = [];

    /// <summary>嵌套闭包代码块</summary>
    private readonly List<ByteCodeChunk> _closures = [];

    public int ConstantCount => _constants.Count;

    private const int NULL_INDEX = 0;
    private const int BOOL_TRUE_INDEX = 1;
    private const int BOOL_FALSE_INDEX = 2;
    private const int INT_MIN = -127;
    private const int INT_MAX = 128;
    private const int INT_OFFSET = 3;
    private const int CONSTANT_OFFSET = INT_OFFSET + (INT_MAX - INT_MIN + 1);

    /// <summary>
    /// 添加常量，返回紧凑编码的索引
    /// </summary>
    public int AddConstant(object? value)
    {
        switch (value)
        {
            case null:
                return NULL_INDEX;

            case bool b:
                return b ? BOOL_TRUE_INDEX : BOOL_FALSE_INDEX;

            case int i when i >= INT_MIN && i <= INT_MAX:
                return i - INT_MIN + INT_OFFSET;

            default:
                {
                    if (_constantMap.TryGetValue(value, out int existing))
                        return existing + CONSTANT_OFFSET;

                    int index = _constants.Count;
                    _constants.Add(value);
                    _constantMap.Add(value, index);
                    return index + CONSTANT_OFFSET;
                }
        }
    }

    /// <summary>
    /// 根据索引获取常量
    /// </summary>
    public object? GetConstant(int index)
    {
        if (index == NULL_INDEX)
            return null;

        if (index == BOOL_TRUE_INDEX)
            return true;

        if (index == BOOL_FALSE_INDEX)
            return false;

        if (index >= INT_OFFSET && index < CONSTANT_OFFSET)
            return index - INT_OFFSET + INT_MIN;

        if (index >= CONSTANT_OFFSET)
            return _constants[index - CONSTANT_OFFSET];

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    /// <summary>
    /// 注册嵌套闭包字节码块，返回闭包索引
    /// </summary>
    public int RegisterClosure(ByteCodeChunk closureChunk)
    {
        _closures.Add(closureChunk);
        return _closures.Count - 1;
    }

    /// <summary>
    /// 获取嵌套闭包字节码块
    /// </summary>
    public ByteCodeChunk GetClosure(int index)
    {
        if ((uint)index >= (uint)_closures.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        return _closures[index];
    }

    /// <summary>
    /// 获取所有动态常量（调试用）
    /// </summary>
    internal IEnumerable<object?> GetConstants() => [.. _constants];

    /// <summary>获取所有嵌套闭包（用于编译器分析）</summary>
    internal IEnumerable<ByteCodeChunk> GetClosures() => [.. _closures];

    // ==================== 序列化支持 ====================

    /// <summary>获取动态常量列表（序列化用）</summary>
    internal IReadOnlyList<object?> Constants => _constants.AsReadOnly();

    /// <summary>获取嵌套闭包列表（序列化用）</summary>
    internal IReadOnlyList<ByteCodeChunk> Closures => _closures.AsReadOnly();

    /// <summary>
    /// 反序列化构造函数：直接组装所有成员
    /// </summary>
    internal ByteCodeChunk(
        List<Instruction> code,
        List<object?> constants,
        List<ByteCodeChunk> closures,
        VariableTable? variableTable)
    {
        Code = code;
        _constants = constants;
        _closures = closures;
        VariableTable = variableTable;

        // 重建常量去重索引
        for (int i = 0; i < _constants.Count; i++)
        {
            var value = _constants[i];
            if (value is not null and not bool and not int)
            {
                _constantMap[value] = i;
            }
        }
    }

    // ==================== 持久化 API ====================

    /// <summary>
    /// 将字节码块序列化并保存到 .ssc 文件
    /// </summary>
    /// <param name="chunk">要保存的字节码块</param>
    /// <param name="path">目标文件路径（建议使用 .ssc 扩展名）</param>
    public static void Save(ByteCodeChunk chunk, string path)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(path);

        using var stream = File.Create(path);
        Save(chunk, stream);
    }

    /// <summary>
    /// 将字节码块序列化并写入流
    /// </summary>
    /// <param name="chunk">要保存的字节码块</param>
    /// <param name="stream">目标流</param>
    public static void Save(ByteCodeChunk chunk, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(stream);

        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var serializer = new ByteCodeChunkWriter(writer);
        serializer.Write(chunk);
    }

    /// <summary>
    /// 从 .ssc 文件加载字节码块
    /// </summary>
    /// <param name="path">.ssc 文件路径</param>
    /// <returns>反序列化后的字节码块</returns>
    /// <exception cref="InvalidDataException">文件格式无效</exception>
    /// <exception cref="NotSupportedException">文件版本不受支持</exception>
    public static ByteCodeChunk Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    /// <summary>
    /// 从流加载字节码块
    /// </summary>
    /// <param name="stream">包含 .ssc 数据的流</param>
    /// <returns>反序列化后的字节码块</returns>
    /// <exception cref="InvalidDataException">数据格式无效</exception>
    /// <exception cref="NotSupportedException">数据版本不受支持</exception>
    public static ByteCodeChunk Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var deserializer = new ByteCodeChunkReader(reader);
        return deserializer.Read();
    }
}
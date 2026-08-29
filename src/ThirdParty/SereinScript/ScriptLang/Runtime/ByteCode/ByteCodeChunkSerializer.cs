using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ScriptLang.Runtime.ByteCode;

// ==================== 序列化类型标记 ====================

/// <summary>常量序列化类型标记</summary>
internal enum SerializedType : byte
{
    Null = 0x00,
    Int32 = 0x01,
    Int64 = 0x02,
    Float = 0x03,
    Double = 0x04,
    Decimal = 0x05,
    String = 0x10,
    List = 0x20,
}

/// <summary>指令操作数序列化类型标记</summary>
internal enum OperandType : byte
{
    None = 0x00,
    Int32 = 0x01,
    Closure = 0x02,
}

// ==================== 文件格式常量 ====================

/// <summary>.ssc 文件格式常量</summary>
internal static class SscFormat
{
    /// <summary>魔术数字 "SSC\0"</summary>
    public static readonly byte[] Magic = [0x53, 0x53, 0x43, 0x00];

    /// <summary>当前文件格式版本</summary>
    public const int Version = 1;

    /// <summary>当前标志位</summary>
    public const int Flags = 0;
}

// ==================== 序列化器 ====================

/// <summary>
/// ByteCodeChunk 二进制序列化器
/// </summary>
internal sealed class ByteCodeChunkWriter(BinaryWriter writer)
{
    private readonly BinaryWriter _writer = writer ?? throw new ArgumentNullException(nameof(writer));

    /// <summary>将 ByteCodeChunk 写入流（含文件头）</summary>
    public void Write(ByteCodeChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        // 文件头（仅顶层写入）
        _writer.Write(SscFormat.Magic);
        _writer.Write(SscFormat.Version);
        _writer.Write(SscFormat.Flags);

        WriteChunkBody(chunk);
    }

    /// <summary>写入 Chunk 数据体（不含文件头，用于嵌套闭包）</summary>
    private void WriteChunkBody(ByteCodeChunk chunk)
    {
        WriteVariableTable(chunk.VariableTable);
        WriteConstants(chunk);
        WriteCode(chunk);
        WriteClosures(chunk);
    }

    // ==================== VariableTable ====================

    private void WriteVariableTable(VariableTable? vt)
    {
        if (vt == null)
        {
            _writer.Write(0); // LocalCount = 0 表示空表
            _writer.Write(0); // CaptureCount
            _writer.Write(0); // GlobalCount
            _writer.Write(0); // BuiltinCount
            WriteStringDict(new Dictionary<string, int>());  // ParamSlots
            WriteStringArray([]);                             // GlobalNames
            WriteStringArray([]);                             // BuiltinNames
            WriteStringDict(new Dictionary<string, int>());  // LocalNames
            WriteStringDict(new Dictionary<string, int>());  // CaptureNames
            return;
        }

        _writer.Write(vt.LocalCount);
        _writer.Write(vt.CaptureCount);
        _writer.Write(vt.GlobalCount);
        _writer.Write(vt.BuiltinCount);

        WriteStringDict(vt.ParamSlots);
        WriteStringArray(vt.GlobalNames);
        WriteStringArray(vt.BuiltinNames);
        WriteStringDict(vt.LocalNames);
        WriteStringDict(vt.CaptureNames);
    }

    // ==================== Constants ====================

    private void WriteConstants(ByteCodeChunk chunk)
    {
        var constants = chunk.Constants;
        _writer.Write(constants.Count);

        foreach (var constant in constants)
        {
            WriteConstant(constant);
        }
    }

    private void WriteConstant(object? value)
    {
        switch (value)
        {
            case null:
                _writer.Write((byte)SerializedType.Null);
                break;

            case int i:
                _writer.Write((byte)SerializedType.Int32);
                _writer.Write(i);
                break;

            case long l:
                _writer.Write((byte)SerializedType.Int64);
                _writer.Write(l);
                break;

            case float f:
                _writer.Write((byte)SerializedType.Float);
                _writer.Write(f);
                break;

            case double d:
                _writer.Write((byte)SerializedType.Double);
                _writer.Write(d);
                break;

            case decimal m:
                _writer.Write((byte)SerializedType.Decimal);
                _writer.Write(m);
                break;

            case string s:
                _writer.Write((byte)SerializedType.String);
                WriteString(s);
                break;

            case IList list:
                _writer.Write((byte)SerializedType.List);
                _writer.Write(list.Count);
                foreach (var item in list)
                {
                    WriteConstant(item);
                }
                break;

            default:
                throw new InvalidDataException($"不支持的常量类型: {value?.GetType().FullName ?? "null"}");
        }
    }

    // ==================== Code ====================

    private void WriteCode(ByteCodeChunk chunk)
    {
        _writer.Write(chunk.Code.Count);

        foreach (var inst in chunk.Code)
        {
            _writer.Write((byte)inst.OpCode);

            if (inst.Operand == null)
            {
                _writer.Write((byte)OperandType.None);
            }
            else if (inst.Operand is int intValue)
            {
                _writer.Write((byte)OperandType.Int32);
                _writer.Write(intValue);
            }
            else
            {
                // CreateClosure 元组: (int, List<string>, List<(string, int)>)
                WriteClosureOperand(inst.Operand);
            }
        }
    }

    private void WriteClosureOperand(object operand)
    {
        // 类型: (int chunkIndex, List<string> parameters, List<(string name, int outerCaptureSlot)> captureMappings)
        var tuple = (ValueTuple<int, List<string>, List<ValueTuple<string, int>>>)operand;

        _writer.Write((byte)OperandType.Closure);

        int chunkIndex = tuple.Item1;
        var parameters = tuple.Item2;
        var captureMappings = tuple.Item3;

        _writer.Write(chunkIndex);

        // 参数
        _writer.Write(parameters.Count);
        foreach (var param in parameters)
        {
            WriteString(param);
        }

        // 捕获映射
        _writer.Write(captureMappings.Count);
        foreach (var (name, outerSlot) in captureMappings)
        {
            WriteString(name);
            _writer.Write(outerSlot);
        }
    }

    // ==================== Closures ====================

    private void WriteClosures(ByteCodeChunk chunk)
    {
        var closures = chunk.Closures;
        _writer.Write(closures.Count);

        foreach (var closure in closures)
        {
            WriteChunkBody(closure);
        }
    }

    // ==================== 辅助方法 ====================

    private void WriteString(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        _writer.Write(bytes.Length);
        _writer.Write(bytes);
    }

    private void WriteStringArray(string[] array)
    {
        _writer.Write(array.Length);
        foreach (var s in array)
        {
            WriteString(s);
        }
    }

    private void WriteStringDict(Dictionary<string, int> dict)
    {
        _writer.Write(dict.Count);
        foreach (var (key, value) in dict)
        {
            WriteString(key);
            _writer.Write(value);
        }
    }
}

// ==================== 反序列化器 ====================

/// <summary>
/// ByteCodeChunk 二进制反序列化器
/// </summary>
internal sealed class ByteCodeChunkReader(BinaryReader reader)
{
    private readonly BinaryReader _reader = reader ?? throw new ArgumentNullException(nameof(reader));

    /// <summary>从流读取 ByteCodeChunk</summary>
    public ByteCodeChunk Read()
    {
        // 文件头
        var magic = _reader.ReadBytes(4);
        if (!MagicMatch(magic))
            throw new InvalidDataException($"不是有效的 .ssc 文件（魔术数字不匹配：期望 [{BitConverter.ToString(SscFormat.Magic)}]，实际 [{BitConverter.ToString(magic)}]）");

        int version = _reader.ReadInt32();
        if (version != SscFormat.Version)
            throw new NotSupportedException($"不支持的 .ssc 文件版本: {version}（当前支持: {SscFormat.Version}）");

        int flags = _reader.ReadInt32();
        // flags 保留，当前忽略

        // VariableTable
        var variableTable = ReadVariableTable();

        // Constants
        var constants = ReadConstants();

        // Closures 要在 Code 之前读取（因为 Code 中的嵌套 Chunk 引用需要先还原）
        // 但 Code 中的 CreateClosure 操作数引用的是 chunkIndex……
        // 实际上 Code 和 Closures 的读取顺序不影响，因为 chunkIndex 是列表索引，
        // 反序列化后的闭包列表会被放入构造函数。
        // 先读 Constants，再读 Code，最后读 Closures。

        // 但等等——闭包的 Chunk 中也有 Code 和 Constants。所以我们要：
        // 1. 读当前层的 VariableTable
        // 2. 读当前层的 Constants
        // 3. 读当前层的 Code
        // 4. 读当前层的 Closures（递归）

        var code = ReadCode();

        var closures = ReadClosures();

        return new ByteCodeChunk(code, constants, closures, variableTable);
    }

    // ==================== 文件头 ====================

    private static bool MagicMatch(byte[] magic)
    {
        if (magic.Length != SscFormat.Magic.Length) return false;
        for (int i = 0; i < magic.Length; i++)
        {
            if (magic[i] != SscFormat.Magic[i]) return false;
        }
        return true;
    }

    // ==================== VariableTable ====================

    private VariableTable ReadVariableTable()
    {
        int localCount = _reader.ReadInt32();
        int captureCount = _reader.ReadInt32();
        int globalCount = _reader.ReadInt32();
        int builtinCount = _reader.ReadInt32();

        // 检查空表
        if (localCount == 0 && captureCount == 0 && globalCount == 0 && builtinCount == 0)
        {
            // 仍需消耗后续数据以保持流位置正确
            ReadStringDict();  // ParamSlots
            ReadStringArray(); // GlobalNames
            ReadStringArray(); // BuiltinNames
            ReadStringDict();  // LocalNames
            ReadStringDict();  // CaptureNames
            return null!;
        }

        var paramSlots = ReadStringDict();
        var globalNames = ReadStringArray();
        var builtinNames = ReadStringArray();
        var localNames = ReadStringDict();
        var captureNames = ReadStringDict();

        return new VariableTable
        {
            LocalCount = localCount,
            CaptureCount = captureCount,
            GlobalCount = globalCount,
            BuiltinCount = builtinCount,
            ParamSlots = paramSlots,
            GlobalNames = globalNames,
            BuiltinNames = builtinNames,
            LocalNames = localNames,
            CaptureNames = captureNames,
        };
    }

    // ==================== Constants ====================

    private List<object?> ReadConstants()
    {
        int count = _reader.ReadInt32();
        var constants = new List<object?>(count);

        for (int i = 0; i < count; i++)
        {
            constants.Add(ReadConstant());
        }

        return constants;
    }

    private object? ReadConstant()
    {
        var type = (SerializedType)_reader.ReadByte();

        return type switch
        {
            SerializedType.Null => null,
            SerializedType.Int32 => _reader.ReadInt32(),
            SerializedType.Int64 => _reader.ReadInt64(),
            SerializedType.Float => _reader.ReadSingle(),
            SerializedType.Double => _reader.ReadDouble(),
            SerializedType.Decimal => _reader.ReadDecimal(),
            SerializedType.String => ReadString(),
            SerializedType.List => ReadConstantList(),
            _ => throw new InvalidDataException($"未知的常量类型标记: {type}"),
        };
    }

    private List<object?> ReadConstantList()
    {
        int count = _reader.ReadInt32();
        var list = new List<object?>(count);

        for (int i = 0; i < count; i++)
        {
            list.Add(ReadConstant());
        }

        return list;
    }

    // ==================== Code ====================

    private List<Instruction> ReadCode()
    {
        int count = _reader.ReadInt32();
        var code = new List<Instruction>(count);

        for (int i = 0; i < count; i++)
        {
            var opCode = (OpCode)_reader.ReadByte();
            var operandType = (OperandType)_reader.ReadByte();

            object? operand = operandType switch
            {
                OperandType.None => null,
                OperandType.Int32 => _reader.ReadInt32(),
                OperandType.Closure => ReadClosureOperand(),
                _ => throw new InvalidDataException($"未知的操作数类型: {operandType}"),
            };

            code.Add(new Instruction(opCode, operand));
        }

        return code;
    }

    private object ReadClosureOperand()
    {
        int chunkIndex = _reader.ReadInt32();

        // 参数列表
        int paramCount = _reader.ReadInt32();
        var parameters = new List<string>(paramCount);
        for (int i = 0; i < paramCount; i++)
        {
            parameters.Add(ReadString());
        }

        // 捕获映射
        int captureCount = _reader.ReadInt32();
        var captureMappings = new List<(string name, int outerCaptureSlot)>(captureCount);
        for (int i = 0; i < captureCount; i++)
        {
            string name = ReadString();
            int outerSlot = _reader.ReadInt32();
            captureMappings.Add((name, outerSlot));
        }

        return (chunkIndex, parameters, captureMappings);
    }

    // ==================== Closures ====================

    private List<ByteCodeChunk> ReadClosures()
    {
        int count = _reader.ReadInt32();
        var closures = new List<ByteCodeChunk>(count);

        for (int i = 0; i < count; i++)
        {
            // 递归读取——但不需要文件头（文件头仅顶层有）
            var variableTable = ReadVariableTable();
            var constants = ReadConstants();
            var code = ReadCode();
            var nestedClosures = ReadClosures();

            closures.Add(new ByteCodeChunk(code, constants, nestedClosures, variableTable));
        }

        return closures;
    }

    // ==================== 辅助方法 ====================

    private string ReadString()
    {
        int length = _reader.ReadInt32();
        var bytes = _reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }

    private string[] ReadStringArray()
    {
        int count = _reader.ReadInt32();
        var array = new string[count];
        for (int i = 0; i < count; i++)
        {
            array[i] = ReadString();
        }
        return array;
    }

    private Dictionary<string, int> ReadStringDict()
    {
        int count = _reader.ReadInt32();
        var dict = new Dictionary<string, int>(count);
        for (int i = 0; i < count; i++)
        {
            string key = ReadString();
            int value = _reader.ReadInt32();
            dict[key] = value;
        }
        return dict;
    }
}

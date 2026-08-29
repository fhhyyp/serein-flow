using System.Collections.Concurrent;

namespace ScriptLang.Runtime.ByteCode;

/// <summary>
/// 调用帧
/// </summary>
internal class CallFrame
{
    /// <summary>当前执行的字节码块</summary>
    public ByteCodeChunk Chunk = null!;

    /// <summary>当前帧的指令指针</summary>
    public int IP;

    /// <summary>统一的槽位数组（大小 = VariableTable.TotalCount）</summary>
    public Value[] Slots = [];

    /// <summary>
    /// 闭包捕获的 VariableInfo 数组（按捕获槽位顺序）
    /// 存储 VariableCell 引用，使得对捕获变量的写入能被所有闭包帧共享
    /// </summary>
    public VariableInfo[] Captures = [];

    /// <summary>初始化帧（从对象池取出时调用）</summary>
    public void Init(ByteCodeChunk chunk)
    {
        Chunk = chunk;
        IP = 0;

        int totalSlots = chunk.VariableTable?.TotalCount ?? 0;
        if (Slots.Length != totalSlots)
            Slots = new Value[totalSlots];
        else
            Array.Clear(Slots, 0, Slots.Length);

        int captureCount = chunk.VariableTable?.CaptureCount ?? 0;
        if (Captures.Length != captureCount)
            Captures = new VariableInfo[captureCount];
        else
            Array.Clear(Captures, 0, Captures.Length);
    }

    /// <summary>重置帧（归还对象池时调用）</summary>
    public void Reset()
    {
        Chunk = null!;
        IP = 0;
        // Slots 和 Captures 保留引用，下次 Init 时复用数组
    }
}

/// <summary>
/// 调用帧对象池
/// </summary>
internal sealed class CallFramePool
{
    private readonly ConcurrentStack<CallFrame> _pool = new();
    private readonly int _maxPoolSize;

    public CallFramePool(int maxPoolSize = 32)
    {
        _maxPoolSize = maxPoolSize;
    }

    public CallFrame Rent()
    {
        if (_pool.TryPop(out var frame))
            return frame;
        return new CallFrame();
    }

    public void Return(CallFrame frame)
    {
        if (_pool.Count < _maxPoolSize)
        {
            frame.Reset();
            _pool.Push(frame);
        }
    }
}
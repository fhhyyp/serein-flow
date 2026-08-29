using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptLang.Runtime.ByteCode;

/// <summary>
/// 自定义值栈（替代 Stack<Value>）
/// Pop 时清除引用，避免内部数组膨胀导致的内存泄漏
/// </summary>
internal sealed class ValueStack
{
    private Value[] _array;
    private int _size;

    // ValueStack 新增
    public Value this[int index]
    {
        get
        {
            if (index < 0 || index >= _size)
                throw new IndexOutOfRangeException();
            return _array[index];
        }
    }

    public ValueStack(int initialCapacity = 256)
    {
        _array = new Value[initialCapacity];
        _size = 0;
    }

    public int Count => _size;

    public void Push(Value value)
    {
        if (_size >= _array.Length)
        {
            int newCapacity = _array.Length * 2;
            Array.Resize(ref _array, newCapacity);
        }
        _array[_size++] = value;
    }

    public Value Pop()
    {
        if (_size == 0)
            throw new InvalidOperationException("栈为空");

        var value = _array[--_size];
        _array[_size] = null!; // 清除引用，允许 GC 回收
        return value;
    }

    public Value Peek()
    {
        if (_size == 0)
            throw new InvalidOperationException("栈为空");

        return _array[_size - 1];
    }

    public void Clear()
    {
        // 清除所有引用
        Array.Clear(_array, 0, _size);
        _size = 0;
    }
}

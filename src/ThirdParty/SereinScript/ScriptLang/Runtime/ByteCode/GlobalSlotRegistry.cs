using System.Collections.Generic;

namespace ScriptLang.Runtime.ByteCode;

/// <summary>引擎拥有的全局槽位表。每个引擎获得一个独立实例。</summary>
public sealed class GlobalSlotTable
{
    private readonly Dictionary<string, int> _nameToSlot = new(StringComparer.Ordinal);
    private readonly List<string> _slotNames = [];
    private Value[] _values = [];

    public int Count => _slotNames.Count;
    public IReadOnlyList<string> GetNames() => _slotNames.AsReadOnly();

    public int Register(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_nameToSlot.TryGetValue(name, out var existingSlot))
            return existingSlot;
        var slot = _slotNames.Count;
        _nameToSlot[name] = slot;
        _slotNames.Add(name);
        return slot;
    }

    public int GetSlot(string name) => _nameToSlot.TryGetValue(name, out var slot)
        ? slot
        : throw new KeyNotFoundException($"全局变量 '{name}' 未注册");

    public bool IsRegistered(string name) => _nameToSlot.ContainsKey(name);

    public void InitializeValues()
    {
        _values = new Value[Count];
        Array.Fill(_values, Value.Null);
    }

    public Value[] GetValues()
    {
        if (_values.Length == Count)
            return _values;
        var old = _values;
        _values = new Value[Count];
        Array.Copy(old, _values, Math.Min(old.Length, _values.Length));
        for (var i = old.Length; i < _values.Length; i++)
            _values[i] = Value.Null;
        return _values;
    }

    public void SetValue(int slot, Value value)
    {
        if ((uint)slot >= (uint)Count)
            throw new ArgumentOutOfRangeException(nameof(slot));
        GetValues()[slot] = value;
    }

    public Value GetValue(int slot)
    {
        var values = GetValues();
        return (uint)slot < (uint)values.Length ? values[slot] : Value.Null;
    }

    public void Reset()
    {
        _nameToSlot.Clear();
        _slotNames.Clear();
        _values = [];
    }
}

/// <summary>
/// 用于独立演示的兼容性表面。生产引擎必须通过 <see cref="ScriptEngine"/> 使用 <see cref="GlobalSlotTable"/> 
/// </summary>
[Obsolete("使用 ScriptEngine.GlobalSlots；此进程级兼容性表不具隔离安全特性。")]
public static class GlobalSlotRegistry
{
    private static readonly GlobalSlotTable Legacy = new();

    public static int Count => Legacy.Count;
    public static IReadOnlyList<string> GetNames() => Legacy.GetNames();
    public static int Register(string name) => Legacy.Register(name);
    public static int GetSlot(string name) => Legacy.GetSlot(name);
    public static bool IsRegistered(string name) => Legacy.IsRegistered(name);
    public static void InitializeValues() => Legacy.InitializeValues();
    public static Value[] GetValues() => Legacy.GetValues();
    public static void SetValue(int slot, Value value) => Legacy.SetValue(slot, value);
    public static Value GetValue(int slot) => Legacy.GetValue(slot);
    public static void Reset() => Legacy.Reset();
}

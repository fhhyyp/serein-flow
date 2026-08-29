using System.Diagnostics.CodeAnalysis;

namespace ScriptLang.Runtime
{

    /// <summary>
    /// 轻量闭包：仅存储被捕获变量的 VariableInfo 数组
    /// 运行时按槽位索引 O(1) 访问
    /// </summary>
    public sealed class LightweightClosure(VariableInfo[] capturedCells)
    {
        /// <summary>捕获的变量信息（按捕获槽位顺序）</summary>
        internal readonly VariableInfo[] CapturedCells = capturedCells;

        /// <summary>捕获变量数量</summary>
        public int CaptureCount => CapturedCells.Length;

        /// <summary>
        /// 获取捕获槽位的 VariableInfo
        /// </summary>
        public VariableInfo GetCapture(int captureSlot)
        {
            return CapturedCells[captureSlot];
        }
    }

    /// <summary>
    /// 作用域
    /// </summary>
    public class Scope(Scope? parent = null)
    {

        private readonly Dictionary<string, VariableInfo> _variables = [];
        public Scope? Parent { get; } = parent;

        public Scope CreateChildScope() => new(this);

        public void Clear()
        {
            foreach (var pair in _variables)
            {
                if (!pair.Value.IsCaptured)
                    pair.Value.Cell.Value = Value.Null;
            }
            _variables.Clear();
        }

        public VariableInfo Define(string name, Value value, bool isMutable = true)
        {
            if (_variables.ContainsKey(name))
                throw new RuntimeException($"变量 '{name}' 已在此作用域中定义");

            var variable = new VariableInfo(new VariableCell(value), isMutable);
            _variables[name] = variable;
            return variable;
        }

        public void DefineClrObject(string name, object data, bool isMutable = true)
        {
            if (_variables.ContainsKey(name))
                throw new RuntimeException($"变量 '{name}' 已在此作用域中定义");

            var value = new ClrObjectValue(data);
            _variables[name] = new VariableInfo(new VariableCell(value), isMutable);
        }

        public bool TryGetValue(string name, [NotNullWhen(true)] out VariableInfo? info)
        {
            if (_variables.TryGetValue(name, out info))
                return true;

            if (Parent != null)
                return Parent.TryGetValue(name, out info);

            return false;
        }

        public void Set(string name, Value value)
        {
            if (_variables.TryGetValue(name, out var info))
            {
                if (!info.IsMutable)
                    throw new RuntimeException($"无法为不可变变量 '{name}' 赋值");
                info.Cell.Value = value;
                return;
            }

            if (Parent != null)
            {
                Parent.Set(name, value);
                return;
            }

            throw new RuntimeException($"当前作用域未定义的变量 '{name}'");
        }

        public bool Exists(string name)
        {
            if (_variables.ContainsKey(name)) return true;
            if (Parent != null) return Parent.Exists(name);
            return false;
        }

        public bool IsDefinedLocally(string name)
            => _variables.ContainsKey(name);

        public int VarCount => _variables.Count;

        /// <summary>
        /// 枚举此作用域可见的变量，内部绑定优先于父级绑定。
        /// </summary>
        public IEnumerable<KeyValuePair<string, VariableInfo>> EnumerateVisibleVariables()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (Scope? current = this; current != null; current = current.Parent)
            {
                foreach (var pair in current._variables)
                {
                    if (seen.Add(pair.Key))
                        yield return pair;
                }
            }
        }
    }

    /// <summary>
    /// 变量容器（引用语义，支持闭包共享）
    /// </summary>
    public sealed class VariableCell(Value value)
    {
        public Value Value = value;
    }

    /// <summary>
    /// 变量信息
    /// </summary>
    public sealed class VariableInfo(VariableCell cell, bool isMutable, bool isCaptured = false)
    {
        /// <summary>存储变量值的容器</summary>
        public VariableCell Cell { get; } = cell;

        /// <summary>是否可变</summary>
        public bool IsMutable { get; set; } = isMutable;

        /// <summary>是否被闭包捕获</summary>
        public bool IsCaptured { get; set; } = isCaptured;
    }
}



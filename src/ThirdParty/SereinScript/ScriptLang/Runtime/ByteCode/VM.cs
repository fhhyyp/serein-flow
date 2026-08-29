using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Threading.Tasks;

namespace ScriptLang.Runtime.ByteCode;

/// <summary>
/// 字节码虚拟机（全槽位化版本）
/// 运行时零字符串查找，所有变量通过槽位数组 O(1) 访问
/// </summary>
public class VM
{
    /// <summary>运行环境</summary>
    private readonly ScriptEngine _engine;
    private CancellationToken _cancellationToken;

    /// <summary>操作数栈</summary>
    private readonly Stack<Value> _stack = new();

    /// <summary>调用帧栈</summary>
    private readonly Stack<CallFrame> _frames = new();

    /// <summary>帧入口栈深度记录（HandleReturn 时恢复，隔离帧间栈泄漏）</summary>
    private readonly Stack<int> _frameStackDepths = new();

    /// <summary>迭代器栈</summary>
    private readonly Stack<Value> _iteratorStack = new();

    /// <summary>全局作用域（仅用于模块级变量共享）</summary>
    private readonly Scope _globalScope = new();

    /// <summary>当前调用帧</summary>
    private CallFrame _currentFrame = null!;

    /// <summary>调用帧对象池</summary>
    private readonly CallFramePool _framePool = new();

    /// <summary>内置函数值数组（槽位索引 → 值）</summary>
    private static readonly Value[] _builtinValues;

    /// <summary>内置函数名 → 槽位索引</summary>
    private static readonly Dictionary<string, int> _builtinSlots;

    static VM()
    {
        var systemValues = BuiltinCache.SystemValues;
        _builtinValues = new Value[systemValues.Count];
        _builtinSlots = new Dictionary<string, int>(systemValues.Count);

        foreach(var item in systemValues.Select((kvp, i)=>(kvp, i)))
        {
            var index = item.i;
            var name = item.kvp.Key;
            var value = item.kvp.Value;
            _builtinValues[index] = value;
            _builtinSlots[name] = index;
        }
    }

    public VM(ScriptEngine engine)
    {
        _engine = engine;
        _globalScope = new Scope();

        // 注册系统默认函数、对象到全局作用域（兼容性保留）
        foreach (var item in BuiltinCache.SystemValues)
        {
            var name = item.Key;
            var value = item.Value;
            _globalScope.Define(name, value, isMutable: false);
        }

        // 注意：不在此处调用 GlobalSlotRegistry.InitializeValues()，
        // 因为全局槽位的值可能已由外层 VM 的 import 指令设置。
        // 若重置会清除已导入的全局变量（如 system 模块），导致闭包内访问失败。
        // GetValues() 内部有懒初始化保护：首次访问时若 _values.Length != Count 则自动初始化。
    }

    // ==================== 主执行循环 ====================

    /// <summary>
    /// 执行字节码块
    /// </summary>
    public async ValueTask<Value> ExecuteAsync(ByteCodeChunk chunk, CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;
        _engine.CurrentCancellationToken = cancellationToken;
        _cancellationToken.ThrowIfCancellationRequested();

        ScriptLog.Debug("=== 变量表 ===");
        var vt = chunk.VariableTable;
        if (vt != null)
        {
            ScriptLog.Debug($"  Locals: {vt.LocalCount}, Captures: {vt.CaptureCount}, Globals: {vt.GlobalCount}, Builtins: {vt.BuiltinCount}");
        }

        ScriptLog.Debug("=== 常量表 ===");
        var constants = chunk.GetConstants().ToList();
        for (int i = 0; i < constants.Count; i++)
        {
            var constant = constants[i];
            if (constant is IList list)
            {
                ScriptLog.Debug($"  [{i}] = [{string.Join(",", list.Cast<object>())}]");
            }
            else
            {
                ScriptLog.Debug($"  [{i}] = {constant}");
            }
        }

        ScriptLog.Debug("=== 指令 ===");
        for (int i = 0; i < chunk.Code.Count; i++)
        {
            ScriptLog.Debug($"  {i:D4}: {chunk.Code[i].OpCode} {chunk.Code[i].Operand}");
        }
        ScriptLog.Debug("=== 执行 ===");

        _stack.Clear();
        _frames.Clear();

        _currentFrame = new CallFrame();
        _currentFrame.Init(chunk);

        // 顶层帧填充全局变量和内置函数
        InitFrameSlots(_currentFrame, null);
        for (int i = 0; i < _currentFrame.Slots.Length; i++)
        {
            ScriptLog.Debug($"[VM] Slots[{i}] = {_currentFrame.Slots[i]?.ToString() ?? "null"}");
        }


        while (_currentFrame.IP >= 0 && _currentFrame.IP < _currentFrame.Chunk.Code.Count)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            int currentIP = _currentFrame.IP;
            var inst = _currentFrame.Chunk.Code[currentIP];

            try
            {
                bool shouldContinue = await ExecuteInstruction(inst);

                if (!shouldContinue)
                {
                    return _stack.Count > 0 ? Pop() : Value.Null;
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                throw new RuntimeException($"执行错误 at IP={_currentFrame.IP}: {inst.OpCode} - {ex.Message}");
#else
                throw;
#endif
            }

            // 只有非控制流指令才自动 IP++
            if (!IsControlFlowInstruction(inst.OpCode))
            {
                _currentFrame.IP++;
            }
        }

        return _stack.Count > 0 ? Pop() : Value.Null;
    }

    /// <summary>
    /// 判断是否为控制流指令（这些指令自己管理 IP）
    /// </summary>
    private static bool IsControlFlowInstruction(OpCode op)
    {
        return op switch
        {
            OpCode.Jmp or OpCode.JumpIfTrue or OpCode.JmpIfFalse or
            OpCode.Return or OpCode.Call or OpCode.MoveNext => true,
            _ => false
        };
    }


    // ==================== 帧初始化 ====================

    /// <summary>
    /// 初始化帧的槽位数组（不含参数绑定）
    /// </summary>
    private void InitFrameSlots(CallFrame frame, LightweightClosure? closure)
    {
        var vt = frame.Chunk.VariableTable;
        if (vt == null) return;

        // 1. 填充捕获变量区（从闭包复制 VariableCell 的引用值）
        if (closure != null && vt.CaptureCount > 0)
        {
            // 复制数组，不共享引用（避免帧归还时 Array.Clear 破坏闭包数据）
            frame.Captures = new VariableInfo[closure.CapturedCells.Length];
            Array.Copy(closure.CapturedCells, frame.Captures, closure.CapturedCells.Length);

            for (int i = 0; i < vt.CaptureCount; i++)
            {
                var capturedCell = frame.Captures[i];
                if (capturedCell != null)
                {
                    frame.Slots[vt.CaptureOffset + i] = capturedCell.Cell.Value;
                }
                else
                {
                    frame.Slots[vt.CaptureOffset + i] = Value.Null;
                }
            }
        }

        // 2. 填充全局变量区
        if (vt.GlobalCount > 0)
        {
            var globalValues = _engine.GlobalSlots.GetValues();
            for (int i = 0; i < vt.GlobalCount; i++)
            {
                string globalName = vt.GlobalNames[i];
                int globalSlot = _engine.GlobalSlots.GetSlot(globalName);
                frame.Slots[vt.GlobalOffset + i] = globalValues[globalSlot];
            }
        }

        // 3. 填充内置函数区
        if (vt.BuiltinCount > 0)
        {
            for (int i = 0; i < vt.BuiltinCount; i++)
            {
                string builtinName = vt.BuiltinNames[i];
                if (_builtinSlots.TryGetValue(builtinName, out int builtinSlot))
                {
                    frame.Slots[vt.BuiltinOffset + i] = _builtinValues[builtinSlot];
                }
                else
                {
                    frame.Slots[vt.BuiltinOffset + i] = Value.Null;
                }
            }
        }
    }

    /// <summary>
    /// 绑定函数参数到帧的局部槽位。
    /// 当参数被嵌套闭包捕获时（Region 被 ReplaceBindingInScope 改为 Capture），
    /// 同时也写入捕获区域，确保 CreateClosure 通过 outerCaptureSlot 能读取到值。
    /// </summary>
    private static void BindParameters(CallFrame frame, List<string> paramNames, List<Value> args)
    {
        var vt = frame.Chunk.VariableTable;
        if (vt == null) return;

        for (int i = 0; i < paramNames.Count; i++)
        {
            var paramValue = i < args.Count ? args[i] : Value.Null;
            if (vt.ParamSlots.TryGetValue(paramNames[i], out int slot))
            {
                frame.Slots[slot] = paramValue;
            }
            // 参数被闭包捕获时，同步写入捕获区（CreateClosure 通过 outerCaptureSlot 读取）
            if (vt.CaptureNames.TryGetValue(paramNames[i], out int captureSlot))
            {
                frame.Slots[vt.CaptureOffset + captureSlot] = paramValue;
            }
        }
    }

    // ==================== 指令执行 ====================

    /// <summary>
    /// 执行单条指令
    /// </summary>
    /// <returns>true 继续执行，false 停止 VM</returns>
    private async Task<bool> ExecuteInstruction(Instruction inst)
    {
        switch (inst.OpCode)
        {
            // ===== 常量加载 =====
            case OpCode.Nop:
                return true;

            case OpCode.LoadNull:
                Push(Value.Null);
                return true;

            case OpCode.LoadTrue:
                Push(BoolValue.True);
                return true;

            case OpCode.LoadFalse:
                Push(BoolValue.False);
                return true;

            case OpCode.LoadM1:
                Push(NumberValueCache.Int32_M1);
                return true;

            case OpCode.Load0:
                Push(NumberValueCache.Int32_0);
                return true;

            case OpCode.Load1:
                Push(NumberValueCache.Int32_1);
                return true;

            case OpCode.LoadConst:
                {
                    int index = (int)inst.Operand!;
                    LoadConstant(index);
                }
                return true;

            // ===== 槽位变量操作 =====
            case OpCode.LoadSlot:
                {
                    int slot = (int)inst.Operand!;
                    Push(_currentFrame.Slots[slot]);
                }
                return true;

            case OpCode.StoreSlot:
                StoreSlot(inst);
                return true;

            // ===== 栈操作 =====
            case OpCode.Pop:
                Pop();
                return true;

            case OpCode.Dup:
                Push(Peek());
                return true;

            // ===== 算术运算 =====
            case OpCode.ToMutable:
                {
                    var value = Pop();
                    if (value is MutableNumber)
                    {
                        Push(value);
                    }
                    else if (value.IsNumber)
                    {
                        Push(MutableNumber.FromNumberValue(value));
                    }
                    else
                    {
                        Push(value);
                    }
                }
                return true;
            #region 原地运算
            case OpCode.AddInPlace:
                {
                    int slot = (int)inst.Operand!;

                    InPlaceOp(slot,
                        right => ((MutableNumber)_currentFrame.Slots[slot]).AddInPlace(right),
                        (l, r) => AddOp(l, r));
                }
                return true;

            case OpCode.SubInPlace:
                {
                    int slot = (int)inst.Operand!;
                    InPlaceOp(slot,
                        right => ((MutableNumber)_currentFrame.Slots[slot]).SubInPlace(right),
                        (l, r) => SubOp(l, r));
                }
                return true;

            case OpCode.MulInPlace:
                {
                    int slot = (int)inst.Operand!;
                    InPlaceOp(slot,
                        right => ((MutableNumber)_currentFrame.Slots[slot]).MulInPlace(right),
                        (l, r) => MulOp(l, r));
                }
                return true;

            case OpCode.DivInPlace:
                {
                    int slot = (int)inst.Operand!;
                    InPlaceOp(slot,
                        right => ((MutableNumber)_currentFrame.Slots[slot]).DivInPlace(right),
                        (l, r) => DivOp(l, r));
                }
                return true;

            case OpCode.ModInPlace:
                {
                    int slot = (int)inst.Operand!;
                    InPlaceOp(slot,
                        right => ((MutableNumber)_currentFrame.Slots[slot]).ModInPlace(right),
                        (l, r) => ModOp(l, r));
                }
                return true; 
            #endregion

            case OpCode.Add:
                BinaryOp(AddOp);
                return true;

            case OpCode.Sub:
                BinaryOp(SubOp);
                return true;

            case OpCode.Mul:
                BinaryOp(MulOp);
                return true;

            case OpCode.Div:
                BinaryOp(DivOp);
                return true;

            case OpCode.Mod:
                BinaryOp(ModOp);
                return true;

            case OpCode.Neg:
                UnaryOp(NegOp);
                return true;

            case OpCode.Not:
                UnaryOp(v => BoolValue.Create(!IsTrue(v)));
                return true;

            // ===== 比较运算 =====
            case OpCode.Equal:
                BinaryOp((l, r) => BoolValue.Create(IsEqual(l, r)));
                return true;

            case OpCode.Ne:
                BinaryOp((l, r) => BoolValue.Create(!IsEqual(l, r)));
                return true;

            case OpCode.Gt:
                BinaryOp((l, r) => Compare(l, r, CompareKind.Gt));
                return true;

            case OpCode.Ge:
                BinaryOp((l, r) => Compare(l, r, CompareKind.Ge));
                return true;

            case OpCode.Lt:
                BinaryOp((l, r) => Compare(l, r, CompareKind.Lt));
                return true;

            case OpCode.Le:
                BinaryOp((l, r) => Compare(l, r, CompareKind.Le));
                return true;

            case OpCode.And:
                AndOp();
                return true;

            case OpCode.Or:
                OrOp();
                return true;

            // ===== 跳转指令 =====
            case OpCode.Jmp:
                JumpTo((int)inst.Operand!);
                return true;

            case OpCode.JumpIfTrue:
                if (IsTrue(Pop()))
                    JumpTo((int)inst.Operand!);
                else
                    _currentFrame.IP++;
                return true;

            case OpCode.JmpIfFalse:
                if (!IsTrue(Pop()))
                    JumpTo((int)inst.Operand!);
                else
                    _currentFrame.IP++;
                return true;

            // ===== 导入模块 =====
            case OpCode.Import:
                await ImportModule(inst.Operand!);
                return true;

            // ===== 函数和闭包 =====
            case OpCode.CreateClosure:
                CreateClosure(inst.Operand!);
                return true;

            case OpCode.Call:
                await CallAsync((int)inst.Operand!);
                return true;

            case OpCode.Return:
                return HandleReturn();

            // ===== 对象操作 =====
            case OpCode.CreateObject:
                CreateObject((int)inst.Operand!);
                return true;

            case OpCode.GetMember:
                GetMember();
                return true;

            case OpCode.SetMember:
                SetMember();
                return true;

            // ===== 数组操作 =====
            case OpCode.CreateArray:
                CreateArray((int)inst.Operand!);
                return true;

            case OpCode.GetIndex:
                GetIndex();
                return true;

            case OpCode.SetIndex:
                SetIndex();
                return true;

            // ===== 迭代器 =====
            case OpCode.GetIterator:
                GetIterator();
                return true;

            case OpCode.MoveNext:
                MoveNext();
                if (!IsTrue(Peek()))
                {
                    if (inst.Operand is int moveIndex)
                        JumpTo(moveIndex);
                    else
                        _currentFrame.IP++;
                }
                else
                {
                    _currentFrame.IP++;
                }
                return true;

            case OpCode.Current:
                Current();
                return true;

            case OpCode.CurrentToSlot:
                CurrentToSlot(inst);
                return true;
            default:
                throw new InvalidOperationException($"未知的字节码指令: {inst.OpCode}");
        }
    }


    /// <summary> 存储槽位变量 </summary>
    /// <param name="inst"></param>
    private void StoreSlot(Instruction inst)
    {
        int slot = (int)inst.Operand!;
        var value = Pop();

        // 优化：如果槽位中已有 MutableNumber 且新值是数值，原地更新
        var existing = _currentFrame.Slots[slot];
        if (existing is MutableNumber mn && value.IsNumber)
        {
            mn.SetFrom(value);
            value = mn;
        }

        // 同步捕获区/全局区
        var vt = _currentFrame.Chunk.VariableTable;
        if (vt != null)
        {
            var region = vt.GetRegion(slot);
            if (region == SlotRegion.Capture)
            {
                int captureIndex = slot - vt.CaptureOffset;
                if (captureIndex >= 0 && captureIndex < _currentFrame.Captures.Length
                    && _currentFrame.Captures[captureIndex] != null)
                {
                    _currentFrame.Captures[captureIndex].Cell.Value = value;
                }
                ScriptLog.Debug($"[StoreSlot] 同步 Captures[{captureIndex}].Cell.Value = {value}, CellHashCode={_currentFrame.Captures[captureIndex]?.GetHashCode()}");
            }
            else if (region == SlotRegion.Global)
            {
                int globalIndex = slot - vt.GlobalOffset;
                if (globalIndex >= 0 && globalIndex < vt.GlobalNames.Length)
                {
                    string globalName = vt.GlobalNames[globalIndex];
                    _engine.GlobalSlots.SetValue(_engine.GlobalSlots.GetSlot(globalName),
                        value is MutableNumber m ? m.ToImmutable() : value);
                }
            }
        }

        _currentFrame.Slots[slot] = value;
        Push(value);
    }

    // ==================== Return 处理 ====================

    /// <summary>
    /// 处理 Return 指令
    /// </summary>
    /// <returns>true 继续执行，false 表示顶层返回</returns>
    private bool HandleReturn()
    {
        ScriptLog.Debug($"[HandleReturn] Pop前 栈深度={_stack.Count}, 栈顶={(_stack.Count > 0 ? _stack.Peek()?.ToString() : "空")}");
        var returnValue = Pop();
        ScriptLog.Debug($"[HandleReturn] 返回值={returnValue}, 类型={returnValue.GetType().Name}");

        // 返回时冻结 MutableNumber
        if (returnValue is MutableNumber mn)
        {
            returnValue = mn.ToImmutable();
        }

        if (_frames.Count == 0)
        {
            Push(returnValue);
            return false;
        }

        var finishedFrame = _currentFrame;
        _currentFrame = _frames.Pop();

        // 恢复帧入口栈深度，丢弃被调用帧泄漏的栈值（如 sleep 遗留的 Value.Null）
        int savedDepth = _frameStackDepths.Pop();
        while (_stack.Count > savedDepth)
            Pop();

        Push(returnValue);
        _framePool.Return(finishedFrame);
        return true;
    }

    // ==================== 栈操作 ====================

    private void JumpTo(int targetIP)
    {
        _currentFrame.IP = targetIP;
    }

    private void Push(Value value) => _stack.Push(value);

    private Value Pop() => _stack.Pop();

    private Value Peek() => _stack.Peek();

    private void UnaryOp(Func<Value, Value> op)
    {
        Push(op(Pop()));
    }

    private void BinaryOp(Func<Value, Value, Value> op)
    {
        var right = Pop();
        var left = Pop();
        Push(op(left, right));
    }

    /// <summary>
    /// 原地操作辅助方法
    /// 如果左值是 MutableNumber 则原地修改，否则创建新值回写
    /// </summary>
    /// <param name="slot">槽位索引</param>
    /// <param name="mutableOp">原地操作（接收已弹出的右值）</param>
    /// <param name="fallbackOp">不可变时的回退操作</param>
    private void InPlaceOp(int slot, Action<Value> mutableOp, Func<Value, Value, Value> fallbackOp)
    {
        
        var right = Pop();
        var left = _currentFrame.Slots[slot];

        if (left is MutableNumber)
        {
            mutableOp(right);
            //if (_engine.IsPrintVMInfo)
                //Console.WriteLine($"[InPlaceOp] MutableNumber 修改后 = {TEMP_mn}");
        }
        else
        {
            _currentFrame.Slots[slot] = fallbackOp(left, right);
        }

        // 同步捕获区/全局区
        var vt = _currentFrame.Chunk.VariableTable;
        if (vt != null)
        {
            var region = vt.GetRegion(slot);

            //if (_engine.IsPrintVMInfo)
                //Console.WriteLine($"[InPlaceOp] slot={slot}, region={region}, CaptureOffset={vt.CaptureOffset}");

            if (region == SlotRegion.Capture)
            {
                int captureIndex = slot - vt.CaptureOffset;

                //if (_engine.IsPrintVMInfo)
                    //Console.WriteLine($"[InPlaceOp] captureIndex={captureIndex}, Captures.Length={_currentFrame.Captures.Length}");
                //if (_engine.IsPrintVMInfo && captureIndex < _currentFrame.Captures.Length)
                    //Console.WriteLine($"[InPlaceOp] Captures[{captureIndex}] = {_currentFrame.Captures[captureIndex]?.Cell.Value}");

                if (captureIndex >= 0 && captureIndex < _currentFrame.Captures.Length
                    && _currentFrame.Captures[captureIndex] != null)
                {
                    _currentFrame.Captures[captureIndex].Cell.Value = _currentFrame.Slots[slot];
                }
                //if (_engine.IsPrintVMInfo)
                    //Console.WriteLine($"[AddInPlace] 同步后 Captures[{captureIndex}].Cell.Value={_currentFrame.Captures[captureIndex].Cell.Value}");
            }
            else if (region == SlotRegion.Global)
            {
                int globalIndex = slot - vt.GlobalOffset;
                if (globalIndex >= 0 && globalIndex < vt.GlobalNames.Length)
                {
                    string globalName = vt.GlobalNames[globalIndex];
                    _engine.GlobalSlots.SetValue(_engine.GlobalSlots.GetSlot(globalName),
                        _currentFrame.Slots[slot] is MutableNumber mn ? mn.ToImmutable() : _currentFrame.Slots[slot]);
                }
            }
        }
    }


    // ==================== 常量加载 ====================

    private void LoadConstant(int index)
    {
        var constant = _currentFrame.Chunk.GetConstant(index);
        var value = ValueFromConstant(constant);
        Push(value);
    }

    // ==================== 模块导入 ====================

    private async Task ImportModule(object operand)
    {
        int dataIndex = (int)operand;
        if (_currentFrame.Chunk.GetConstant(dataIndex) is not List<object?> importData)
        {
            throw new RuntimeException($"导入模块时无法获取常量数据");
        }

        if (importData.Count == 0 || importData[0] is not string filePath)
        {
            throw new RuntimeException($"无效的导入数据");
        }

        var memberMappings = new List<(string member, string alias)>();
        for (int i = 1; i < importData.Count; i += 2)
        {
            var member = (string)importData[i]!;
            var alias = importData[i + 1] as string;
            memberMappings.Add((member, alias ?? member));
        }

        // 解析模块
        _cancellationToken.ThrowIfCancellationRequested();
        var exports = await _engine.ImportResolver.ResolveAsync(filePath, cancellationToken: _cancellationToken)
            ?? throw new RuntimeException($"无法导入模块 '{filePath}'，请检查文件路径");

        // 将导入的成员注入到当前帧的全局槽位区和 GlobalSlotRegistry
        var vt = _currentFrame.Chunk.VariableTable;
        if (vt == null) return;

        // 获取全局模块变量值
        //var globalValues = GlobalSlotRegistry.GetValues();
        

        foreach (var (member, name) in memberMappings)
        {
            if (!exports.TryGetValue(member, out var value))
            {
                throw new RuntimeException($"模块 '{filePath}' 中未找到导出的成员 '{member}'");
            }

            // 更新 GlobalSlotRegistry 中的运行时值
            int registrySlot = _engine.GlobalSlots.GetSlot(name);
            _engine.GlobalSlots.SetValue(registrySlot, value);

            // 更新当前帧的槽位
            for (int i = 0; i < vt.GlobalCount; i++)
            {
                if (vt.GlobalNames[i] == name)
                {
                    _currentFrame.Slots[vt.GlobalOffset + i] = value;
                    break;
                }
            }
        }

        Push(Value.Null);
    }

    // ==================== 闭包创建 ====================

    private void CreateClosure(object operand)
    {
        var (chunkIndex, parameters, captureMappings) =
            ((int, List<string>, List<(string name, int outerCaptureSlot)>))operand;

        var closureChunk = _currentFrame.Chunk.GetClosure(chunkIndex);
        var innerVt = closureChunk.VariableTable;
        var outerVt = _currentFrame.Chunk.VariableTable;

        if (innerVt == null || outerVt == null)
        {
            Push(Value.Null);
            return;
        }

        int captureCount = innerVt.CaptureCount;
        var capturedCells = new VariableInfo[captureCount];

        foreach (var (name, outerCaptureSlot) in captureMappings)
        {

            if (!innerVt.CaptureNames.TryGetValue(name, out int innerCaptureIndex))
                continue;

            if (innerCaptureIndex < 0 || innerCaptureIndex >= captureCount)
                continue;

            // 从外部帧获取该变量的当前值。
            // outerCaptureSlot 是编译时为该变量分配的捕获槽位索引，直接使用避免名字查找时
            // 同名不同作用域的变量发生混淆。
            int outerRuntimeSlot = outerVt.CaptureOffset + outerCaptureSlot;
            Value existingValue = _currentFrame.Slots[outerRuntimeSlot];
            if(existingValue is null)
            {
                ScriptLog.Debug($"[CreateClosure] name={name}, existingValue is null (forward reference, creating placeholder cell)");
                // 不 continue：即使当前值为 null（前向引用），仍需创建 VariableCell 占位，
                // 后续 StoreSlot 会通过 Captures[outerCaptureSlot].Cell.Value 同步真实值。
            }
            // 始终创建新的 VariableCell，保证每次闭包实例化有独立的状态
            ScriptLog.Debug($"[CreateClosure] name={name}, existingValue={existingValue}, existingValueHash={existingValue?.GetHashCode()}");
            // 检查是否已有其他闭包为同一变量创建了 Cell（多闭包捕获同一前向引用）
            // 若有则复用，确保 StoreSlot 一次写入能同步到所有闭包
            VariableInfo info;
            if (outerCaptureSlot >= 0 && outerCaptureSlot < _currentFrame.Captures.Length
                && _currentFrame.Captures[outerCaptureSlot] != null)
            {
                info = _currentFrame.Captures[outerCaptureSlot];
                ScriptLog.Debug($"[CreateClosure] name={name}, 复用已有 Cell(Hash={info.GetHashCode()})");
            }
            else
            {
                var cell = new VariableCell(existingValue);
                info = new VariableInfo(cell, true) { IsCaptured = true };

                // 回写到外部帧的捕获区（供后续嵌套 CreateClosure 共享同一个 Cell）
                if (outerCaptureSlot >= 0 && outerCaptureSlot < _currentFrame.Captures.Length)
                {
                    _currentFrame.Captures[outerCaptureSlot] = info;
                }
                ScriptLog.Debug($"[CreateClosure] 回写 Captures[{outerCaptureSlot}] = new Cell({existingValue}), HashCode={info.GetHashCode()}");
            }
            capturedCells[innerCaptureIndex] = info;
        }

        var closure = new LightweightClosure(capturedCells);
        var func = new CompiledFunctionValue(parameters, closureChunk, innerVt, closure);

        Push(func);
    }


    // ==================== 函数调用 ====================

    private async Task CallAsync(int argCount)
    {
        _currentFrame.IP++;
        ScriptLog.Debug($"[VM.Call] argCount={argCount}, 栈深度={_stack.Count}");
        // 弹出参数
        var args = new List<Value>();
        for (int i = 0; i < argCount; i++)
        {
            var arg = Pop();
            // 传参时冻结 MutableNumber，避免函数内部修改影响外部
            args.Insert(0, arg is MutableNumber mn ? mn.ToImmutable() : arg);
        }

        // 弹出函数
        var target = Pop();
        ScriptLog.Debug($"[VM.Call] target={target}, target类型={target?.GetType().Name ?? "NULL"}, 参数=[{string.Join(",", args)}]");

        if (target is CompiledFunctionValue compiledFunc)
        {
            CallCompiledFunction(compiledFunc, args);
        }
        else if (target is FunctionValue scriptFunc)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var result = await scriptFunc.CallAsync(_engine, args);
            _cancellationToken.ThrowIfCancellationRequested();
            Push(result);
        }
        else if (target is ClrMethodValue clrMethod)
        {
            await CallClrMethodAsync(clrMethod, args);
        }
        else
        {
            throw new RuntimeException($"无法调用类型为 {(target?.GetType().Name ?? "NULL")} 的值");
        }
    }

    private void CallCompiledFunction(CompiledFunctionValue func, List<Value> args)
    {
        ScriptLog.Debug($"[VM.CallCompiledFunction] 调用 {func.GetHashCode()}, 参数数={func.Parameters.Count}, 闭包={(func.Closure != null ? $"CaptureCount={func.Closure.CaptureCount}" : "null")}");

        var vt = func.VariableTable;
        if (vt != null)
        {
            ScriptLog.Debug($"[VM.CallCompiledFunction] 变量表: L={vt.LocalCount} C={vt.CaptureCount} G={vt.GlobalCount} B={vt.BuiltinCount}");
        }
        var newFrame = _framePool.Rent();
        newFrame.Init(func.Chunk);

        // 初始化槽位
        InitFrameSlots(newFrame, func.Closure);

        // 绑定参数
        BindParameters(newFrame, func.Parameters, args);

#if DEBUG
        ScriptLog.Debug($"=== 字节码 [{func.GetHashCode()}] ===");
        var ccc = func.Chunk;
        for (int iii = 0; iii < ccc.Code.Count; iii++)
            ScriptLog.Debug($"  {iii:D4}: {ccc.Code[iii].OpCode} {ccc.Code[iii].Operand ?? ""}");
#endif

        // 保存当前栈深度，HandleReturn 时恢复以隔离帧间栈泄漏
        _frameStackDepths.Push(_stack.Count);
        _frames.Push(_currentFrame);
        _currentFrame = newFrame;
    }

    private async Task CallClrMethodAsync(ClrMethodValue method, List<Value> args)
    {
        var clrArgs = new object?[method.ParameterCount];
        var methodParams = method.MethodInfo.GetParameters();

        for (int i = 0; i < Math.Min(args.Count, clrArgs.Length); i++)
        {
            clrArgs[i] = ConvertValueToClr(args[i], methodParams[i].ParameterType);
        }

        _cancellationToken.ThrowIfCancellationRequested();
        var result = await method.InvokeAsync(clrArgs);
        _cancellationToken.ThrowIfCancellationRequested();
        Push(ConvertClrToValue(result));
    }

    /// <summary>
    /// 供 CompiledFunctionValue.CallAsync 调用的入口
    /// </summary>
    public async ValueTask<Value> InvokeCompiledFunctionAsync(
        CompiledFunctionValue func,
        List<Value> args)
    {
        var frame = _framePool.Rent();
        frame.Init(func.Chunk);

        InitFrameSlots(frame, func.Closure);
        BindParameters(frame, func.Parameters, args);

        _currentFrame = frame;

#if DEBUG
        // 输出闭包函数字节码（用于调试）
        ScriptLog.Debug("=== 闭包指令 ===");
        var c = func.Chunk;
        var vt2 = c.VariableTable;
        if (vt2 != null) ScriptLog.Debug($"  变量表: L={vt2.LocalCount} C={vt2.CaptureCount} G={vt2.GlobalCount} B={vt2.BuiltinCount}");
        for (int i2 = 0; i2 < c.Code.Count; i2++)
            ScriptLog.Debug($"  {i2:D4}: {c.Code[i2].OpCode} {c.Code[i2].Operand ?? ""}");
#endif

        while (_currentFrame.IP >= 0 && _currentFrame.IP < _currentFrame.Chunk.Code.Count)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            int currentIP = _currentFrame.IP;
            var inst = _currentFrame.Chunk.Code[currentIP];

            try
            {
                bool shouldContinue = await ExecuteInstruction(inst);

                if (!shouldContinue)
                {
                    return _stack.Count > 0 ? Pop() : Value.Null;
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                throw new RuntimeException($"执行错误 at IP={_currentFrame.IP}: {inst.OpCode} - {ex.Message}");
#else
                throw;
#endif
            }

            if (!IsControlFlowInstruction(inst.OpCode))
            {
                _currentFrame.IP++;
            }
        }

        return _stack.Count > 0 ? Pop() : Value.Null;
    }

    // ==================== 对象操作 ====================

    private void CreateObject(int propertyCount)
    {
        var properties = new Dictionary<string, Value>();

        for (int i = 0; i < propertyCount; i++)
        {
            var value = Pop();
            var key = Pop();
            properties[key.AsString()] = value;
        }

        var obj = new ObjectValue(properties);
        Push(obj);
    }

    private void GetMember()
    {
        var memberName = Pop().AsString();
        var target = Pop() ?? throw new RuntimeException($"无法调用 GetMember('{memberName}')，目标值为 null ");
        if (target is ObjectValue obj)
        {
            if (obj.TryGetValue(memberName, out var value))
            {
                Push(value);
                return;
            }
        }

        if (_engine.PrototypeManager.TryGetValue(target, memberName, out Value? result))
        {
            Push(result);
            return;
        }

        if (target is ClrObjectValue clrObj)
        {
            var clrMember = AccessClrMember(clrObj, memberName);
            if (clrMember != null)
            {
                Push(clrMember);
                return;
            }
        }

        throw new RuntimeException($"未找到成员 '{memberName}'");
    }

    private void SetMember()
    {
        var value = Pop();
        var memberName = Pop().AsString();
        var target = Pop();

        if (target is ObjectValue obj)
        {
            obj.Set(memberName, value);
        }
        else if (target is ClrObjectValue clrObj)
        {
            SetClrMember(clrObj, memberName, value);
        }
        else
        {
            throw new RuntimeException($"无法为 {target.GetType()} 设置成员 '{memberName}'");
        }

        Push(value);
    }

    // ==================== 数组操作 ====================

    private void CreateArray(int elementCount)
    {
        var elements = new List<Value>();
        for (int i = 0; i < elementCount; i++)
        {
            elements.Insert(0, Pop());
        }
        Push(new ArrayValue(elements));
    }

    private void GetIndex()
    {
        var index = Pop();
        var target = Pop();

        if (target is ArrayValue arr && index.IsNumber_Int)
        {
            int i = index.As<int>();
            if (i < 0 || i >= arr.Elements.Count)
                throw new RuntimeException($"数组索引越界: {i}");
            Push(arr.Get(i));
        }
        else if (target is StringValue str && index.IsNumber_Int)
        {
            int i = index.As<int>();
            if (i < 0 || i >= str.Value.Length)
                throw new RuntimeException($"字符串索引越界: {i}");
            Push(StringValue.Create(str.Value[i].ToString()));
        }
        else if (target is ObjectValue obj && index is StringValue key)
        {
            if (obj.TryGetValue(key.Value, out var value))
                Push(value);
            else
                throw new RuntimeException($"对象中未找到键 '{key.Value}'");
        }
        else
        {
            throw new RuntimeException($"无效的索引访问");
        }
    }

    private void SetIndex()
    {
        var value = Pop();
        var index = Pop();
        var target = Pop();

        if (target is ArrayValue arr && index.IsNumber_Int)
        {
            int i = index.As<int>();
            if (i < 0 || i >= arr.Elements.Count)
                throw new RuntimeException($"数组索引越界: {i}");
            arr.Set(i, value);
        }
        else if (target is ObjectValue obj && index is StringValue key)
        {
            obj.Set(key.Value, value);
        }
        else
        {
            throw new RuntimeException($"无效的索引赋值");
        }

        Push(value);
    }

    // ==================== 迭代器 ====================

    private void GetIterator()
    {
        var iterable = Pop();

        if (iterable is ArrayValue arr)
        {
            _iteratorStack.Push(arr);
            _iteratorStack.Push(NumberValueCache.Int32_0);
            Push(BoolValue.True);
        }
        else if (iterable is RangeIterator range)
        {
            _iteratorStack.Push(range);
            Push(BoolValue.True);
        }
        else
        {
            throw new RuntimeException("For 循环期望数组或 range 迭代器");
        }
    }

    private void MoveNext()
    {
        // RangeIterator 路径
        if (_iteratorStack.Peek() is RangeIterator range)
        {
            if (range.MoveNext())
            {
                Push(BoolValue.True);
            }
            else
            {
                _iteratorStack.Pop();
                Push(BoolValue.False);
            }
            return;
        }

        // ArrayValue 路径
        var indexValue = _iteratorStack.Peek();
        var index = indexValue.As<int>();
        var array = (ArrayValue)_iteratorStack.Skip(1).First();

        if (index < array.Elements.Count)
        {
            Push(BoolValue.True);
        }
        else
        {
            _iteratorStack.Pop();
            _iteratorStack.Pop();
            Push(BoolValue.False);
        }
    }

    private void Current()
    {
        // RangeIterator 路径
        if (_iteratorStack.Peek() is RangeIterator range)
        {
            Push(range.Current());
            return;
        }

        // ArrayValue 路径
        var indexValue = _iteratorStack.Peek();
        var index = indexValue.As<int>();
        var array = (ArrayValue)_iteratorStack.Skip(1).First();

        if (index < array.Elements.Count)
        {
            Push(array.Get(index));
            _iteratorStack.Pop();
            _iteratorStack.Push(NumberValueFactory.Create(index + 1));
        }
        else
        {
            Push(Value.Null);
        }
    }

    /// <summary>将迭代器当前值直接写入槽位</summary>
    /// <param name="inst"></param>
    private void CurrentToSlot(Instruction inst)
    {
        int slot = (int)inst.Operand!;
        var existing = _currentFrame.Slots[slot];

        if (_iteratorStack.Peek() is RangeIterator range)
        {
            if (existing is MutableNumber mn)
            {
                // 零分配：直接设置 int 值
                mn.SetFromInt(range.CurrentInt());
            }
            else
            {
                _currentFrame.Slots[slot] = range.Current();
            }
        }
        else
        {
            // ArrayValue 路径
            var indexValue = _iteratorStack.Peek();
            var index = indexValue.As<int>();
            var array = (ArrayValue)_iteratorStack.Skip(1).First();

            if (index < array.Elements.Count)
            {
                var value = array.Get(index);

                if (existing is MutableNumber mn && value.IsNumber)
                {
                    mn.SetFrom(value);
                }
                else
                {
                    _currentFrame.Slots[slot] = value;
                }

                _iteratorStack.Pop();
                _iteratorStack.Push(NumberValueFactory.Create(index + 1));
            }
        }
    }


    // ==================== 短路求值（兜底） ====================

    private void AndOp()
    {
        var right = Pop();
        var left = Pop();
        Push(BoolValue.Create(IsTrue(left) && IsTrue(right)));
    }

    private void OrOp()
    {
        var right = Pop();
        var left = Pop();
        Push(BoolValue.Create(IsTrue(left) || IsTrue(right)));
    }

    // ==================== 算术运算 ====================

    private static Value AddOp(Value left, Value right)
    {
        if (left.IsNumber && right.IsNumber)
        {
            return NumberOp(left, right,
                (a, b) => a + b,
                (a, b) => a + b,
                (a, b) => a + b,
                (a, b) => a + b,
                (a, b) => a + b);
        }

        if (left.IsString || right.IsString)
            return StringValue.Create(left.AsString() + right.AsString());

        if (left is ArrayValue leftArr)
        {
            var newArray = left.AsArray().ToList();
            if (right.IsArray)
                newArray.AddRange(right.AsArray());
            else
                newArray.Add(right);
            return new ArrayValue(newArray);
        }

        // DateTime / TimeSpan 操作
        if (left.IsDateTime && right.IsTimeSpan)
            return new DateTimeValue(left.As<DateTime>() + right.As<TimeSpan>());

        if (left.IsTimeSpan && right.IsDateTime)
            return new DateTimeValue(right.As<DateTime>() + left.As<TimeSpan>());

        if (left.IsTimeSpan && right.IsTimeSpan)
            return new TimeSpanValue(left.As<TimeSpan>() + right.As<TimeSpan>());

        throw new RuntimeException($"不支持的操作: {left} + {right}");
    }

    private static Value SubOp(Value left, Value right)
    {
        if (left.IsNumber && right.IsNumber)
        {
            return NumberOp(left, right,
                (a, b) => a - b,
                (a, b) => a - b,
                (a, b) => a - b,
                (a, b) => a - b,
                (a, b) => a - b);
        }

        if (left.IsArray)
        {
            var arr = left.AsArray().ToList();
            if (right.IsArray)
            {
                var rightArr = right.AsArray();
                arr.RemoveAll(x => rightArr.Contains(x));
            }
            else
            {
                arr.Remove(right);
            }
            return new ArrayValue(arr);
        }

        // DateTime / TimeSpan 操作
        if (left.IsDateTime && right.IsTimeSpan)
            return new DateTimeValue(left.As<DateTime>() - right.As<TimeSpan>());

        if (left.IsDateTime && right.IsDateTime)
            return new TimeSpanValue(left.As<DateTime>() - right.As<DateTime>());

        if (left.IsTimeSpan && right.IsTimeSpan)
            return new TimeSpanValue(left.As<TimeSpan>() - right.As<TimeSpan>());

        throw new RuntimeException($"不支持的操作: {left} - {right}");
    }

    private static Value MulOp(Value left, Value right)
    {
        if (left.IsNumber && right.IsNumber)
        {
            return NumberOp(left, right,
                (a, b) => a * b,
                (a, b) => a * b,
                (a, b) => a * b,
                (a, b) => a * b,
                (a, b) => a * b);
        }

        if (left.IsString && right.IsNumber_Int)
        {
            var str = left.AsString();
            var repeat = right.As<int>();
            return StringValue.Create(string.Concat(Enumerable.Repeat(str, repeat)));
        }

        throw new RuntimeException($"不支持的操作: {left} * {right}");
    }

    private static Value DivOp(Value left, Value right)
    {
        if (left.IsNumber && right.IsNumber)
        {
            return NumberOpDiv(left, right);
        }

        throw new RuntimeException($"不支持的操作: {left} / {right}");
    }

    private static Value ModOp(Value left, Value right)
    {
        if (left.IsNumber && right.IsNumber)
        {
            return NumberOp(left, right,
                (a, b) => a % b,
                (a, b) => a % b,
                (a, b) => a % b,
                (a, b) => a % b,
                (a, b) => a % b);
        }

        throw new RuntimeException($"不支持的操作: {left} % {right}");
    }

    private static Value NegOp(Value value)
    {
        if (value.IsNumber)
        {
            if (value.IsNumber_Decimal) return NumberValueFactory.Create(-value.As<decimal>());
            if (value.IsNumber_Double) return NumberValueFactory.Create(-value.As<double>());
            if (value.IsNumber_Float) return NumberValueFactory.Create(-value.As<float>());
            if (value.IsNumber_Long) return NumberValueFactory.Create(-value.As<long>());
            if (value.IsNumber_Int) return NumberValueFactory.Create(-value.As<int>());
        }

        throw new RuntimeException($"不支持的操作: -{value}");
    }

    private static Value NumberOp(
        Value left, Value right,
        Func<decimal, decimal, decimal> decOp,
        Func<double, double, double> dblOp,
        Func<float, float, float> fltOp,
        Func<long, long, long> lngOp,
        Func<int, int, int> intOp)
    {
        if (left.IsNumber_Decimal || right.IsNumber_Decimal)
            return NumberValueFactory.Create(decOp(left.As<decimal>(), right.As<decimal>()));
        if (left.IsNumber_Double || right.IsNumber_Double)
            return NumberValueFactory.Create(dblOp(left.As<double>(), right.As<double>()));
        if (left.IsNumber_Float || right.IsNumber_Float)
            return NumberValueFactory.Create(fltOp(left.As<float>(), right.As<float>()));
        if (left.IsNumber_Long || right.IsNumber_Long)
            return NumberValueFactory.Create(lngOp(left.As<long>(), right.As<long>()));
        return NumberValueFactory.Create(intOp(left.As<int>(), right.As<int>()));
    }

    private static Value NumberOpDiv(Value left, Value right)
    {
        if (left.IsNumber_Decimal || right.IsNumber_Decimal)
            return NumberValueFactory.Create(left.As<decimal>() / right.As<decimal>());
        if (left.IsNumber_Double || right.IsNumber_Double)
            return NumberValueFactory.Create(left.As<double>() / right.As<double>());
        if (left.IsNumber_Float || right.IsNumber_Float)
            return NumberValueFactory.Create(left.As<float>() / right.As<float>());
        return NumberValueFactory.Create(left.As<double>() / right.As<double>());
    }

    // ==================== 比较运算 ====================

    private enum CompareKind { Lt, Le, Gt, Ge }

    private static BoolValue Compare(Value left, Value right, CompareKind kind)
    {
        if (left.IsNumber && right.IsNumber)
        {
            int cmp = CompareNumbers(left, right);
            return BoolValue.Create(kind switch
            {
                CompareKind.Lt => cmp < 0,
                CompareKind.Le => cmp <= 0,
                CompareKind.Gt => cmp > 0,
                CompareKind.Ge => cmp >= 0,
                _ => false
            });
        }

        if (left.IsString && right.IsString)
        {
            int cmp = string.Compare(left.AsString(), right.AsString());
            return BoolValue.Create(kind switch
            {
                CompareKind.Lt => cmp < 0,
                CompareKind.Le => cmp <= 0,
                CompareKind.Gt => cmp > 0,
                CompareKind.Ge => cmp >= 0,
                _ => false
            });
        }

        if (left.IsDateTime && right.IsDateTime)
        {
            int cmp = left.As<DateTime>().CompareTo(right.As<DateTime>());
            return BoolValue.Create(kind switch
            {
                CompareKind.Lt => cmp < 0,
                CompareKind.Le => cmp <= 0,
                CompareKind.Gt => cmp > 0,
                CompareKind.Ge => cmp >= 0,
                _ => false
            });
        }

        if (left.IsTimeSpan && right.IsTimeSpan)
        {
            int cmp = left.As<TimeSpan>().CompareTo(right.As<TimeSpan>());
            return BoolValue.Create(kind switch
            {
                CompareKind.Lt => cmp < 0,
                CompareKind.Le => cmp <= 0,
                CompareKind.Gt => cmp > 0,
                CompareKind.Ge => cmp >= 0,
                _ => false
            });
        }

        throw new RuntimeException($"不支持比较: {left} 和 {right}");
    }

    private static int CompareNumbers(Value left, Value right)
    {
        if (left.IsNumber_Decimal || right.IsNumber_Decimal)
            return left.As<decimal>().CompareTo(right.As<decimal>());
        if (left.IsNumber_Double || right.IsNumber_Double)
            return left.As<double>().CompareTo(right.As<double>());
        if (left.IsNumber_Float || right.IsNumber_Float)
            return left.As<float>().CompareTo(right.As<float>());
        if (left.IsNumber_Long || right.IsNumber_Long)
            return left.As<long>().CompareTo(right.As<long>());
        return left.As<int>().CompareTo(right.As<int>());
    }

    // ==================== 辅助方法 ====================

    private static bool IsTrue(Value value)
    {
        return value switch
        {
            NullValue => false,
            BoolValue b => b.Value,
            StringValue s => s.Value.Length > 0,
            ArrayValue a => a.Elements.Count > 0,
            ObjectValue o => o.Properties.Count > 0,
            _ => false,
        };
    }

    private static bool IsEqual(Value left, Value right)
    {
        if(right == Value.Null && left is null)
        {
            return true;
        }
        if(left == Value.Null && right is null)
        {
            return true;
        }
        if (left.IsNumber && right.IsNumber)
            return CompareNumbers(left, right) == 0;

        return (left, right) switch
        {
            (NullValue, NullValue) => true,
            (BoolValue bl, BoolValue br) => bl.Value == br.Value,
            (StringValue sl, StringValue sr) => sl.Value == sr.Value,
            (ArrayValue al, ArrayValue ar) =>
                al.Elements.Count == ar.Elements.Count &&
                al.Elements.Zip(ar.Elements).All(p => IsEqual(p.First, p.Second)),
            (ObjectValue ol, ObjectValue or) =>
                ol.Properties.Count == or.Properties.Count &&
                ol.Properties.All(kv =>
                    or.Properties.TryGetValue(kv.Key, out var v) && IsEqual(kv.Value, v)),
            (DateTimeValue d1, DateTimeValue d2) => d1.Value == d2.Value,
            (TimeSpanValue t1, TimeSpanValue t2) => t1.Value == t2.Value,
            _ => left.Equals(right),
        };
    }

    private static Value ValueFromConstant(object? constant)
    {
        return constant switch
        {
            null => Value.Null,
            int i => NumberValueFactory.Create(i),
            long l => NumberValueFactory.Create(l),
            float f => NumberValueFactory.Create(f),
            double d => NumberValueFactory.Create(d),
            decimal m => NumberValueFactory.Create(m),
            string s => StringValue.Create(s),
            bool b => BoolValue.Create(b),
            DateTime dt => new DateTimeValue(dt),
            TimeSpan ts => new TimeSpanValue(ts),
            _ => throw new RuntimeException($"不支持的常量类型: {constant?.GetType()}")
        };
    }

    private static object? ConvertValueToClr(Value value, Type targetType)
    {
        if (value.IsNull) return null;
        if (value.IsString) return value.AsString();
        if (value.IsBool) return value.AsBool();
        if (value.IsNumber_Int) return value.As<int>();
        if (value.IsNumber_Long) return value.As<long>();
        if (value.IsNumber_Float) return value.As<float>();
        if (value.IsNumber_Double) return value.As<double>();
        if (value is DateTimeValue dt) return dt.Value;
        if (value is TimeSpanValue ts) return ts.Value;
        if (value is ClrObjectValue clr) return clr.Value;

        throw new RuntimeException($"无法转换 {value.GetType()} 到 CLR 类型 {targetType}");
    }

    private static Value ConvertClrToValue(object? clrValue)
    {
        return clrValue switch
        {
            null => Value.Null,
            int i => NumberValueFactory.Create(i),
            long l => NumberValueFactory.Create(l),
            float f => NumberValueFactory.Create(f),
            double d => NumberValueFactory.Create(d),
            decimal m => NumberValueFactory.Create(m),
            string s => StringValue.Create(s),
            bool b => BoolValue.Create(b),
            DateTime dt => new DateTimeValue(dt),
            TimeSpan ts => new TimeSpanValue(ts),
            _ => new ClrObjectValue(clrValue)
        };
    }

    // ==================== CLR 互操作 ====================

    private static Value? AccessClrMember(ClrObjectValue clrObj, string memberName)
    {
        var target = clrObj.Value!;
        var type = target.GetType();

        if (type.GetProperty(memberName) is PropertyInfo property)
        {
            var value = property.GetValue(clrObj.Value);
            return ConvertClrToValue(value);
        }
        else if (type.GetMethod(memberName) is MethodInfo method)
        {
            var value = new ClrMethodValue(method, target);
            return value;
        }
        

        return null;
    }

    private static void SetClrMember(ClrObjectValue clrObj, string memberName, Value value)
    {
        var type = clrObj.Value!.GetType();
        var prop = type.GetProperty(memberName);

        if (prop != null && prop.CanWrite)
        {
            var clrValue = ConvertValueToClr(value, prop.PropertyType);
            prop.SetValue(clrObj.Value, clrValue);
        }
        else
        {
            throw new RuntimeException($"无法设置 CLR 对象的属性 '{memberName}'");
        }
    }
}

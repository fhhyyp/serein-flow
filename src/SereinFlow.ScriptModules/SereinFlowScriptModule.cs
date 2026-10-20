using SereinFlow.Runtime.Abstractions;
using SereinFlow.Library;
using ScriptLang;
using ScriptLang.Runtime;

namespace SereinFlow.ScriptModules
{
    /// <summary>
    /// Registers the SereinFlow-provided script module for one Script node invocation.
    /// Each registration owns distinct module instances and invocation metadata;
    /// variables intentionally share the current run's table across all contexts.
    /// 为一次 Script 节点调用注册独立模块实例及调用元数据；变量共用当前运行的变量表。
    /// </summary>
    public static class SereinFlowScriptModuleRegistration
    {
        public const string ModuleName = "sereinflow";
        public static void Register(ScriptEngine engine, SereinFlowScriptModuleContext context)
        {
            ArgumentNullException.ThrowIfNull(engine);
            ArgumentNullException.ThrowIfNull(context);
            var log = new SereinFlowLogModule(context);
            var environment = new SereinFlowEnvironmentModule(context);
            var variables = new SereinFlowVariableModule(context);
            engine.PrototypeManager.Register(log);
            engine.PrototypeManager.Register(environment);
            engine.PrototypeManager.Register(variables);
            var dictionary = new Dictionary<string, Value>(StringComparer.Ordinal)
            {
                ["log"] = new ClrObjectValue(log),
                ["env"] = new ClrObjectValue(environment),
                ["variables"] = new ClrObjectValue(variables)
            };
            var objectValue = new ObjectValue(dictionary);
            engine.ImportResolver.RegisterBuiltinModule(ModuleName, objectValue);
        }
    }

    /// <summary>
    /// Narrow, per-script-call view of the flow runtime. It deliberately omits the
    /// flow session, other node outputs and all server configuration.
    /// 面向单次脚本调用的受限流程运行时视图。它刻意不公开流程会话、其它节点输出和服务端配置。
    /// </summary>
    public sealed class SereinFlowScriptModuleContext
    {
        private const int MaxMessageLength = 16 * 1024;
        private readonly NodeExecutionRuntime? _runtime;
        private readonly CancellationToken _cancellationToken;
        public SereinFlowScriptModuleContext(NodeExecutionRuntime? runtime, CancellationToken cancellationToken)
        {
            _runtime = runtime;
            _cancellationToken = cancellationToken;
        }

        public NodeExecutionEnvironment? Environment => _runtime?.Environment;
        public bool IsCancellationRequested => _cancellationToken.IsCancellationRequested || (_runtime?.IsCancellationRequested ?? false);

        internal LibraryVariableService GetVariables()
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (_runtime?.IsCancellationRequested == true)
            {
                throw new OperationCanceledException("The flow run was cancelled. 流程运行已取消。", _cancellationToken);
            }

            var evaluatedValue = _runtime?.Variables ?? throw new NotSupportedException("Flow variables require an active run variable table. 流程变量需要宿主提供运行级变量表。");
            return evaluatedValue;
        }

        public async Task<Value> PublishLogAsync(string level, Value value)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var message = ToMessage(value);
            var nodeExecutionLogEntry = new NodeExecutionLogEntry(level, message, value);
            await (_runtime?.EventSink ?? NullNodeExecutionEventSink.Instance).PublishLogAsync(nodeExecutionLogEntry, _cancellationToken);
            return Value.Null;
        }

        private static string ToMessage(Value value)
        {
            var message = value switch
            {
                NullValue => "null",
                StringValue text => text.Value,
                _ => value.ToString() ?? string.Empty
            };
            string evaluatedValue1 = message.Length <= MaxMessageLength ? message : resolveConcatResult();
            return evaluatedValue1;
            string resolveConcatResult()
            {
                var asSpanResult = message.AsSpan(0, MaxMessageLength);
                var concatResult = string.Concat(asSpanResult, "…");
                return concatResult;
            }
        }
    }

    [PrototypeExtension(PushThis = true, NamingFormat = NamingFormat.Js)]
    internal sealed partial class SereinFlowLogModule
    {
        private readonly SereinFlowScriptModuleContext _context;
        public SereinFlowLogModule(SereinFlowScriptModuleContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is SereinFlowLogModule;
        [PrototypeFunction]
        public static async Task<Value> Info(SereinFlowLogModule target, Value value)
        {
            var publishLogAsyncResult = await target._context.PublishLogAsync("info", value);
            return publishLogAsyncResult;
        }

        [PrototypeFunction]
        public static async Task<Value> Error(SereinFlowLogModule target, Value value)
        {
            var publishLogAsyncResult2 = await target._context.PublishLogAsync("error", value);
            return publishLogAsyncResult2;
        }
    }

    [PrototypeExtension(PushThis = true, NamingFormat = NamingFormat.Js)]
    internal sealed partial class SereinFlowEnvironmentModule
    {
        private readonly SereinFlowScriptModuleContext _context;
        public SereinFlowEnvironmentModule(SereinFlowScriptModuleContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is SereinFlowEnvironmentModule;
        [PrototypeProperty]
        public static StringValue RunId(SereinFlowEnvironmentModule target)
        {
            var evaluatedValue3 = target._context.Environment?.RunId.ToString("D") ?? string.Empty;
            var createResult = StringValue.Create(evaluatedValue3);
            return createResult;
        }

        [PrototypeProperty]
        public static StringValue ProjectId(SereinFlowEnvironmentModule target)
        {
            var createResult4 = StringValue.Create(target._context.Environment?.ProjectId ?? string.Empty);
            return createResult4;
        }

        [PrototypeProperty]
        public static StringValue FlowId(SereinFlowEnvironmentModule target)
        {
            var evaluatedValue5 = target._context.Environment?.FlowId.ToString("D") ?? string.Empty;
            var createResult6 = StringValue.Create(evaluatedValue5);
            return createResult6;
        }

        [PrototypeProperty]
        public static StringValue CanvasId(SereinFlowEnvironmentModule target)
        {
            var createResult7 = StringValue.Create(target._context.Environment?.CanvasId ?? string.Empty);
            return createResult7;
        }

        [PrototypeProperty]
        public static StringValue NodeId(SereinFlowEnvironmentModule target)
        {
            var createResult8 = StringValue.Create(target._context.Environment?.NodeId ?? string.Empty);
            return createResult8;
        }

        [PrototypeProperty]
        public static NumberValue<int> FrameDepth(SereinFlowEnvironmentModule target)
        {
            var createResult9 = NumberValueFactory.Create(target._context.Environment?.FrameDepth ?? 0);
            return createResult9;
        }

        [PrototypeProperty]
        public static BoolValue IsCancellationRequested(SereinFlowEnvironmentModule target)
        {
            var createResult10 = BoolValue.Create(target._context.IsCancellationRequested);
            return createResult10;
        }
    }

    [PrototypeExtension(PushThis = true, NamingFormat = NamingFormat.Js)]
    internal sealed partial class SereinFlowVariableModule
    {
        private readonly SereinFlowScriptModuleContext _context;
        public SereinFlowVariableModule(SereinFlowScriptModuleContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is SereinFlowVariableModule;
        [PrototypeFunction]
        public static BoolValue Contain(SereinFlowVariableModule target, StringValue name)
        {
            var getVariablesResult = target._context.GetVariables();
            var containResult = getVariablesResult.Contain(name.Value);
            var createResult11 = BoolValue.Create(containResult);
            return createResult11;
        }

        [PrototypeFunction]
        public static Value Get(SereinFlowVariableModule target, StringValue name)
        {
            var getVariablesResult12 = target._context.GetVariables();
            var getResult = getVariablesResult12.Get(name.Value);
            var toScriptValueResult = SereinFlowScriptValueConverter.ToScriptValue(getResult);
            return toScriptValueResult;
        }

        [PrototypeFunction]
        public static Value Set(SereinFlowVariableModule target, StringValue name, Value value)
        {
            var variables = target._context.GetVariables();
            var toVariableValueResult = SereinFlowScriptValueConverter.ToVariableValue(value);
            variables.Set(name.Value, toVariableValueResult);
            return Value.Null;
        }
    }
}

using ScriptLang.Parser;
using ScriptLang.Prototype;
using ScriptLang.Runtime;
using ScriptLang.Runtime.ByteCode;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Threading;

namespace ScriptLang
{
    public sealed class ScriptEngine
    {

        /// <summary>
        /// 脚本依赖导入工具
        /// </summary>
        public ImportResolver ImportResolver { get; private set; }

        /// <summary>
        /// 脚本源文件管理
        /// </summary>
        public SourceManager SourceManager { get; } = new SourceManager();

        /// <summary>
        /// 全局作用域，此作用域应只注册
        /// </summary>
        public Scope GlobalScope { get; } = new Scope();

        /// <summary>每个引擎的全局槽位状态。它永远不会在多个引擎之间共享。</summary>
        public GlobalSlotTable GlobalSlots { get; } = new();

        private readonly AsyncLocal<CancellationToken> _executionToken = new();

        internal CancellationToken CurrentCancellationToken
        {
            get => _executionToken.Value;
            set => _executionToken.Value = value;
        }

        /// <summary>
        /// 原型拓展管理器
        /// </summary>
        public PrototypeManager PrototypeManager { get; }

        /// <summary>
        /// 编译缓存：AST → ByteCodeChunk
        /// </summary>
        private readonly Dictionary<Expr, ByteCodeChunk> _compilationCache = [];

        public ScriptEngine()
        {
            PrototypeManager = new PrototypeManager(this);
            ImportResolver = new ImportResolver(this);
            PrototypeManager.Register<ArrayPrototype>();
            PrototypeManager.Register<ObjectPrototype>();
            PrototypeManager.Register<StringPrototype>();
            PrototypeManager.Register<DateTimePrototype>();
            PrototypeManager.Register<TimeSpanPrototype>();

        }

        /// <summary>
        /// 执行脚本代码并返回结果
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="scope">提供注册方法（可选，用于注入外部变量）</param>
        /// <returns></returns>
        public ScriptTask CreateTask(string filePath, Scope? scope = null)
        {
            EnsureBuiltins();

            if (!SourceManager.TryGetSource(filePath, out var script))
            {
                script = File.ReadAllText(filePath);
                ScriptLog.Info("================加载脚本==============");
                ScriptLog.Info($"# 脚本路径：{filePath}");
                ScriptLog.Info(script);
                ScriptLog.Info("================解析完毕==============");
                ScriptLog.Info("");

                SourceManager.AddSource(filePath, script);
            }

            if (string.IsNullOrWhiteSpace(ImportResolver.RootPath)
                && Path.GetDirectoryName(filePath) is string rootPath)
            {
                ImportResolver.RootPath = rootPath;
            }

            // 词法分析获取 Token 列表
            var lexer = new Lexer.Lexer(script, filePath);
            var tokens = lexer.ScanTokens();

            // 语法分析获取 AST
            var parser = new Parser.Parser(tokens, filePath);
            var expr = parser.Parse();

            // 检查解析异常
            if (parser.Diagnostics.Count > 0)
            {
                for (int index = 0; index < parser.Diagnostics.Count; index++)
                {
                    ParseException? diagnostic = parser.Diagnostics[index];
                    ScriptLog.Error($"第 {index + 1} 个异常 ：" + diagnostic.ToString());
                }
                throw new Exception($"Parser 阶段产生 {parser.Diagnostics.Count} 个异常");
            }

            scope ??= new Scope(GlobalScope);

            RegisterExternalScopeToGlobalSlots(scope);

            return CreateCompiledTask(expr, GetRegisteredGlobals());
        }

        /// <summary>
        /// 将外部作用域中的变量注册到 GlobalSlotRegistry
        /// </summary>
        private void RegisterExternalScopeToGlobalSlots(Scope scope)
        {
            foreach (var (name, info) in scope.EnumerateVisibleVariables())
            {
                if (BuiltinCache.SystemValues.ContainsKey(name))
                    continue;
                var slot = GlobalSlots.Register(name);
                GlobalSlots.SetValue(slot, info.Cell.Value);
            }
        }

        private void EnsureBuiltins()
        {
            if (GlobalScope.VarCount == 0)
                BuiltinCache.RegisterAll(GlobalScope);
        }

        private HashSet<string> GetRegisteredGlobals()
            => new(GlobalSlots.GetNames(), StringComparer.Ordinal);

        /// <summary>
        /// 从源代码字符串创建执行任务（用于内存中的脚本代码，如 Excel 宏）
        /// </summary>
        /// <param name="source">脚本源代码字符串</param>
        /// <param name="sourceName">源名称（用于错误报告，如宏名称）</param>
        /// <param name="scope">外部作用域（可选，用于注入 Excel 对象等）</param>
        /// <returns>可执行的 ScriptTask</returns>
        public ScriptTask CreateTaskFromSource(string source, string sourceName, Scope? scope = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(sourceName);

            EnsureBuiltins();

            // 注册源代码（复用 SourceManager，但来源是内存而非文件）
            SourceManager.AddSource(sourceName, source);

            // 词法分析
            var lexer = new Lexer.Lexer(source, sourceName);
            var tokens = lexer.ScanTokens();

            // 语法分析
            var parser = new Parser.Parser(tokens, sourceName);
            var expr = parser.Parse();

            // 检查解析异常
            if (parser.Diagnostics.Count > 0)
            {
                var messages = parser.Diagnostics.Select(d => d.ToString());
                throw new Exception($"脚本解析错误 ({sourceName}):\n{string.Join("\n", messages)}");
            }

            // 构建执行作用域
            scope ??= new Scope(GlobalScope);
            RegisterExternalScopeToGlobalSlots(scope);

            // 编译并创建任务
            return CreateCompiledTask(expr, GetRegisteredGlobals());
        }

        /// <summary>
        /// Compiles source without executing it. The returned chunk can be
        /// persisted as an .ssc artifact and later passed to CreateTask.
        /// </summary>
        public ByteCodeChunk CompileSource(
            string source,
            string sourceName,
            IEnumerable<string>? externalGlobals = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(sourceName);
            EnsureBuiltins();
            SourceManager.AddSource(sourceName, source);

            var lexer = new Lexer.Lexer(source, sourceName);
            var parser = new Parser.Parser(lexer.ScanTokens(), sourceName);
            var expr = parser.Parse();
            if (parser.Diagnostics.Count > 0)
            {
                var messages = parser.Diagnostics.Select(d => d.ToString());
                throw new InvalidOperationException($"脚本解析错误 ({sourceName}):\n{string.Join("\n", messages)}");
            }

            var knownGlobals = new HashSet<string>(externalGlobals ?? [], StringComparer.Ordinal);
            foreach (var name in knownGlobals)
                GlobalSlots.Register(name);
            return new Compiler(knownGlobals, GlobalSlots).Compile(expr);
        }

        /// <summary>
        /// 从已编译的 ByteCodeChunk 创建执行任务（跳过编译阶段，直接从 .ssc 文件加载后使用）
        /// </summary>
        /// <param name="chunk">已反序列化的字节码块</param>
        /// <param name="filePath">.ssc 文件路径（可选，用于 import 模块路径解析）。传入后自动设置 ImportResolver.RootPath</param>
        /// <returns>可重复执行的 ScriptTask</returns>
        public ScriptTask CreateTask(ByteCodeChunk chunk, string? filePath = null)
        {
            ArgumentNullException.ThrowIfNull(chunk);

            // 从 .ssc 文件路径推导 import 模块根目录（编译产物本身不含路径信息，保持可移植性）
            if (filePath != null && Path.GetDirectoryName(Path.GetFullPath(filePath)) is string rootPath)
            {
                ImportResolver.RootPath = rootPath;
            }

            // 从 VariableTable 恢复全局变量注册
            var vt = chunk.VariableTable;
            if (vt != null && vt.GlobalCount > 0)
            {
                foreach (var name in vt.GlobalNames)
                {
                    GlobalSlots.Register(name);
                }
                GlobalSlots.InitializeValues();
            }

            // 创建执行工厂
            Func<CancellationToken, Task<Value>> factory = new(async cancellationToken =>
            {
                var sw = Stopwatch.StartNew();
                var vm = new VM(this);
                var result = await vm.ExecuteAsync(chunk, cancellationToken);
                sw.Stop();
                ScriptLog.Info($"[VM] 执行耗时: {sw.ElapsedMilliseconds}ms");
                return result;
            });

            ScriptTask scriptTask = new ScriptTask(factory, new CancellationTokenSource());
            return scriptTask;
        }

        /// <summary>
        /// 编译执行模式（唯一执行路径）
        /// </summary>
        private ScriptTask CreateCompiledTask(Expr expr, HashSet<string>? knownGlobals = null)
        {
            // 获取或创建字节码缓存
            if (!_compilationCache.TryGetValue(expr, out var chunk))
            {
                var sw = Stopwatch.StartNew();
                var compiler = new Compiler(knownGlobals, GlobalSlots);
                chunk = compiler.Compile(expr);
                _compilationCache[expr] = chunk;

                sw.Stop();
                ScriptLog.Info($"[Compile] 编译耗时: {sw.ElapsedMilliseconds}ms");
                ScriptLog.Info($"[Compile] 字节码指令数: {chunk.Code.Count}");
                ScriptLog.Info($"[Compile] 常量数: {chunk.ConstantCount}");
                if (chunk.VariableTable != null)
                {
                    var vt = chunk.VariableTable;
                    ScriptLog.Info($"[Compile] 变量表: L={vt.LocalCount} C={vt.CaptureCount} G={vt.GlobalCount} B={vt.BuiltinCount}");
                }
            }

            // 创建执行工厂
            Func<CancellationToken, Task<Value>> factory = new(async cancellationToken =>
            {
                var sw = Stopwatch.StartNew();
                // 每次执行创建新的 VM 实例（保证栈/帧隔离）
                var vm = new VM(this);
                var result = await vm.ExecuteAsync(chunk, cancellationToken);
                sw.Stop();
                ScriptLog.Info($"[VM] 执行耗时: {sw.ElapsedMilliseconds}ms");
                return result;
            });

            ScriptTask scriptTask = new ScriptTask(factory, new CancellationTokenSource());
            return scriptTask;
        }

        /// <summary>
        /// 加载模块并返回结果
        /// </summary>
        internal async Task<Value> RunModuleAsync(string filePath, Scope scope, CancellationToken cancellationToken = default)
        {
            var task = CreateTask(filePath, scope);
            var value = await task.RunAsync(cancellationToken);
            return value;
        }

        /// <summary>
        /// 清除编译缓存
        /// </summary>
        public void ClearCache()
        {
            _compilationCache.Clear();
            GlobalSlots.Reset();
        }

        /// <summary>
        /// 预注册外部全局变量（编译前调用）
        /// </summary>
        public void RegisterGlobal(string name)
        {
            GlobalSlots.Register(name);
        }

        /// <summary>
        /// 设置全局变量值
        /// </summary>
        public void SetGlobal(string name, Value value)
        {
            int slot = GlobalSlots.GetSlot(name);
            GlobalSlots.SetValue(slot, value);
        }
    }

    /// <summary>
    /// 创建一个执行任务，该任务可重复执行
    /// </summary>
    public sealed class ScriptTask(Func<CancellationToken, Task<Value>> task, CancellationTokenSource cts)
    {
        private readonly Func<CancellationToken, Task<Value>> task = task;

        public CancellationToken Token => cts.Token;

        public bool IsCanceled => Token.IsCancellationRequested;

        public async Task<Value> RunAsync(CancellationToken cancellationToken = default)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
            return await task.Invoke(linked.Token);
        }

        public void Cancel() => cts.Cancel();
    }
}

using System.Collections.Generic;
using SereinFlow.Domain;
using SereinFlow.Contracts;
using SereinFlow.Runtime.Abstractions;
using SereinFlow.ScriptAdapter;
using ScriptLang.Runtime;

namespace SereinFlow.ScriptAdapter.Tests;
public sealed class ScriptAdapterTests
{
    [Fact]
    public async Task CompilerReturnsSuccessWithoutPersistingOrExecutingScript()
    {
        var sereinLangCompiler = new SereinLangCompiler();
        IReadOnlyList<ScriptValueContractDto> evaluatedValue = [new ScriptValueContractDto("amount", "number", true)];
        var scriptCompileRequestDto = new ScriptCompileRequestDto("return amount + 1", "mcp-test.serein", SereinLangCompiler.SupportedLanguageVersion, evaluatedValue);
        var result = await sereinLangCompiler.CompileAsync(scriptCompileRequestDto);
        Assert.True(result.IsSuccess);
        Assert.Equal("mcp-test.serein", result.SourceName);
        Assert.Equal(SereinLangCompiler.SupportedLanguageVersion, result.LanguageVersion);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task CompilerReturnsStructuredSyntaxDiagnostics()
    {
        var sereinLangCompiler1 = new SereinLangCompiler();
        var scriptCompileRequestDto2 = new ScriptCompileRequestDto("return (", null, SereinLangCompiler.SupportedLanguageVersion, []);
        var result = await sereinLangCompiler1.CompileAsync(scriptCompileRequestDto2);
        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Diagnostics);
        Assert.All(result.Diagnostics, diagnostic =>
        {
            Assert.Equal("error", diagnostic.Severity);
        });
    }

    [Fact]
    public async Task CompilerRejectsOversizedSourceBeforeInvokingCompiler()
    {
        var sereinLangCompiler3 = new SereinLangCompiler();
        var stringValue = new string ('x', SereinLangCompiler.MaxSourceBytes + 1);
        var scriptCompileRequestDto4 = new ScriptCompileRequestDto(stringValue, null, SereinLangCompiler.SupportedLanguageVersion, []);
        var result = await sereinLangCompiler3.CompileAsync(scriptCompileRequestDto4);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ScriptErrorCodes.SourceTooLarge);
    }

    [Fact]
    public void ConverterRoundTripsJsonCompatibleValues()
    {
        var dictionary = new Dictionary<string, object?>
        {
            ["name"] = "Ada",
            ["enabled"] = true,
            ["scores"] = new List<int>
            {
                1,
                2,
                3
            },
            ["missing"] = null
        };
        var value = ScriptValueConverter.ToScriptValue(dictionary);
        var toClrValueResult = ScriptValueConverter.ToClrValue(value);
        var result = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(toClrValueResult);
        Assert.Equal("Ada", result["name"]);
        Assert.Equal(true, result["enabled"]);
        Assert.Null(result["missing"]);
    }

    [Fact]
    public async Task ExecutesSingleOutputScript()
    {
        IEnumerable<ScriptValueContract> evaluatedValue5 = [new("amount", "number", true)];
        IEnumerable<ScriptValueContract> evaluatedValue6 = [new("result", "number")];
        var node = CreateScriptNode("return amount + 1", evaluatedValue5, evaluatedValue6);
        var dictionary7 = new Dictionary<string, object?>
        {
            ["amount"] = 41
        };
        var result = await Execute(node, dictionary7);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        var output = Assert.IsAssignableFrom<Value>(result.Outputs["result"]);
        var convertResult = ScriptValueTypeConverter.Convert(output, typeof(int));
        Assert.Equal(42, convertResult);
    }

    [Fact]
    public async Task RegistersTheNamedScriptGlobalWhenInputUsesAStableConnectorIdAndCachedArtifact()
    {
        var getTempPathResult = Path.GetTempPath();
        var newGuidResult = Guid.NewGuid();
        var toStringResult = newGuidResult.ToString("N");
        var root = Path.Combine(getTempPathResult, "sereinflow-script-tests", toStringResult);
        try
        {
            IEnumerable<ScriptValueContract> evaluatedValue8 = [new("input1", "System.Object", true, Id: "input-1")];
            IEnumerable<ScriptValueContract> evaluatedValue9 = [new("result", "ScriptLang.Runtime.Value", false, Id: "result")];
            var definition = CreateScriptDefinition("return input1", evaluatedValue8, evaluatedValue9);
            var node = NodeDefinition.Create("node", NodeType.Script, "Script", script: definition);
            var store = new ScriptArtifactStore(root);
            var evaluatedValue10 = store.RebuildProject("project", [definition]).IsSuccess;
            Assert.True(evaluatedValue10);
            var context = new TestContext();
            context.Write("projectId", "project");
            var executor = new SereinScriptNodeExecutor(store);
            var expected = new Dictionary<string, object?>
            {
                ["batchNo"] = "B01",
                ["passed"] = false
            };
            var dictionary11 = new Dictionary<string, object?>
            {
                ["input-1"] = expected
            };
            var nodeExecutionRequest = new NodeExecutionRequest(node, context, dictionary11);
            var result = await executor.ExecuteAsync(nodeExecutionRequest, CancellationToken.None);
            Assert.True(result.IsSuccess, result.ErrorMessage);
            var output = Assert.IsAssignableFrom<Value>(result.Outputs["result"]);
            var toClrValueResult12 = ScriptValueConverter.ToClrValue(output);
            var actual = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(toClrValueResult12);
            Assert.Equal("B01", actual["batchNo"]);
            Assert.Equal(false, actual["passed"]);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task RegistersDeclaredButUnusedInputsWhenLoadingACachedArtifact()
    {
        var getTempPathResult19 = Path.GetTempPath();
        var newGuidResult20 = Guid.NewGuid();
        var toStringResult21 = newGuidResult20.ToString("N");
        var root = Path.Combine(getTempPathResult19, "sereinflow-script-tests", toStringResult21);
        try
        {
            IEnumerable<ScriptValueContract> evaluatedValue13 = [new("input1", "System.Object", false, Id: "input-1")];
            IEnumerable<ScriptValueContract> evaluatedValue14 = [new("result", "ScriptLang.Runtime.Value", false, Id: "result")];
            var definition = CreateScriptDefinition("return 0", evaluatedValue13, evaluatedValue14);
            var node = NodeDefinition.Create("node", NodeType.Script, "Script", script: definition);
            var store = new ScriptArtifactStore(root);
            var evaluatedValue15 = store.RebuildProject("project", [definition]).IsSuccess;
            Assert.True(evaluatedValue15);
            var context = new TestContext();
            context.Write("projectId", "project");
            var executor = new SereinScriptNodeExecutor(store);
            var dictionary16 = new Dictionary<string, object?>
            {
                ["input-1"] = new Dictionary<string, object?>
                {
                    ["batchNo"] = "B01"
                }
            };
            var nodeExecutionRequest17 = new NodeExecutionRequest(node, context, dictionary16);
            var result = await executor.ExecuteAsync(nodeExecutionRequest17, CancellationToken.None);
            Assert.True(result.IsSuccess, result.ErrorMessage);
            var output = Assert.IsAssignableFrom<Value>(result.Outputs["result"]);
            var convertResult18 = ScriptValueTypeConverter.Convert(output, typeof(int));
            Assert.Equal(0, convertResult18);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task MapsObjectToMultipleOutputs()
    {
        IEnumerable<ScriptValueContract> evaluatedValue22 = [new("left", "number"), new("right", "number")];
        IEnumerable<ScriptValueContract> evaluatedValue23 = [new("sum", "number"), new("label", "string")];
        var node = CreateScriptNode("return { sum = left + right, label = \"done\" }", evaluatedValue22, evaluatedValue23);
        var dictionary24 = new Dictionary<string, object?>
        {
            ["left"] = 2,
            ["right"] = 3
        };
        var result = await Execute(node, dictionary24);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        var isAssignableFromResult = Assert.IsAssignableFrom<Value>(result.Outputs["sum"]);
        var convertResult25 = ScriptValueTypeConverter.Convert(isAssignableFromResult, typeof(int));
        Assert.Equal(5, convertResult25);
        var isAssignableFromResult26 = Assert.IsAssignableFrom<Value>(result.Outputs["label"]);
        var convertResult27 = ScriptValueTypeConverter.Convert(isAssignableFromResult26, typeof(string));
        Assert.Equal("done", convertResult27);
    }

    [Fact]
    public async Task RejectsMissingAndUnknownInputs()
    {
        IEnumerable<ScriptValueContract> evaluatedValue28 = [new("amount", "number", true)];
        IEnumerable<ScriptValueContract> evaluatedValue29 = [new("result", "number")];
        var node = CreateScriptNode("return amount", evaluatedValue28, evaluatedValue29);
        var dictionary30 = new Dictionary<string, object?>();
        var missing = await Execute(node, dictionary30);
        var dictionary31 = new Dictionary<string, object?>
        {
            ["amount"] = 1,
            ["other"] = 2
        };
        var unknown = await Execute(node, dictionary31);
        Assert.Equal(ScriptErrorCodes.InputMissing, missing.ErrorCode);
        Assert.Contains("Required script input", missing.ErrorMessage);
        Assert.Equal(ScriptErrorCodes.InputUnknown, unknown.ErrorCode);
        Assert.Contains("Unknown script input", unknown.ErrorMessage);
    }

    [Fact]
    public async Task CancellationIsReturnedAsStructuredFailure()
    {
        IEnumerable<ScriptValueContract> evaluatedValue32 = [new("result", "number")];
        var node = CreateScriptNode("for i in range(0, 1000000000) { var keep = i }", [], evaluatedValue32);
        var fromMillisecondsResult = TimeSpan.FromMilliseconds(100);
        using var cancellation = new CancellationTokenSource(fromMillisecondsResult);
        var dictionary33 = new Dictionary<string, object?>();
        var result = await Execute(node, dictionary33, cancellation.Token);
        Assert.False(result.IsSuccess);
        Assert.Equal(ScriptErrorCodes.Cancelled, result.ErrorCode);
    }

    [Fact]
    public async Task ScriptExceptionsUseErrorBranch()
    {
        IEnumerable<ScriptValueContract> evaluatedValue34 = [new("result", "number")];
        var node = CreateScriptNode("return missing_value", [], evaluatedValue34);
        var dictionary35 = new Dictionary<string, object?>();
        var result = await Execute(node, dictionary35);
        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionBranch.Error, result.NextBranch);
        Assert.NotNull(result.ErrorCode);
    }

    [Fact]
    public async Task ImportsSereinFlowLogAndEnvironmentWithoutWritingToConsole()
    {
        const string nodeId = "script-node";
        IEnumerable<ScriptValueContract> evaluatedValue36 = [new ScriptValueContract("result", "System.String")];
        var createResult = ScriptNodeDefinition.Create(nodeId, """
                import { log, env } from "sereinflow"
                log.info("Started")
                log.error("Attention")
                return env.nodeId
                """, "1", outputs: evaluatedValue36);
        var node = NodeDefinition.Create(nodeId, NodeType.Script, "Script", script: createResult);
        var runId = Guid.NewGuid();
        var flowId = Guid.NewGuid();
        var sink = new RecordingEventSink();
        var nodeExecutionEnvironment = new NodeExecutionEnvironment(runId, "project-a", flowId, "canvas-a", nodeId, 2);
        var runtime = new NodeExecutionRuntime(nodeExecutionEnvironment, sink);
        var sereinScriptNodeExecutor = new SereinScriptNodeExecutor();
        var testContext = new TestContext();
        var dictionary37 = new Dictionary<string, object?>();
        var nodeExecutionRequest38 = new NodeExecutionRequest(node, testContext, dictionary37, runtime);
        var result = await sereinScriptNodeExecutor.ExecuteAsync(nodeExecutionRequest38, CancellationToken.None);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        var isAssignableFromResult39 = Assert.IsAssignableFrom<Value>(result.Outputs["result"]);
        var convertResult40 = ScriptValueTypeConverter.Convert(isAssignableFromResult39, typeof(string));
        Assert.Equal(nodeId, convertResult40);
        Assert.Collection(sink.Entries, entry =>
        {
            Assert.Equal("info", entry.Level);
            Assert.Equal("Started", entry.Message);
        }, entry =>
        {
            Assert.Equal("error", entry.Level);
            Assert.Equal("Attention", entry.Message);
        });
    }

    [Fact]
    public async Task ExposesOnlyTheDocumentedSereinFlowEnvironmentValuesFromCachedArtifact()
    {
        const string nodeId = "script-node";
        var getTempPathResult56 = Path.GetTempPath();
        var newGuidResult57 = Guid.NewGuid();
        var toStringResult58 = newGuidResult57.ToString("N");
        var root = Path.Combine(getTempPathResult56, "sereinflow-script-tests", toStringResult58);
        try
        {
            IEnumerable<ScriptValueContract> evaluatedValue41 = [new ScriptValueContract("runId", "System.String"), new ScriptValueContract("projectId", "System.String"), new ScriptValueContract("flowId", "System.String"), new ScriptValueContract("canvasId", "System.String"), new ScriptValueContract("nodeId", "System.String"), new ScriptValueContract("frameDepth", "System.Int32"), new ScriptValueContract("isCancellationRequested", "System.Boolean")];
            var definition = ScriptNodeDefinition.Create(nodeId, """
                import { env } from "sereinflow"
                return {
                    runId = env.runId,
                    projectId = env.projectId,
                    flowId = env.flowId,
                    canvasId = env.canvasId,
                    nodeId = env.nodeId,
                    frameDepth = env.frameDepth,
                    isCancellationRequested = env.isCancellationRequested
                }
                """, "1", outputs: evaluatedValue41);
            var store = new ScriptArtifactStore(root);
            var evaluatedValue42 = store.RebuildProject("project-a", [definition]).IsSuccess;
            Assert.True(evaluatedValue42);
            var runId = Guid.NewGuid();
            var flowId = Guid.NewGuid();
            var context = new TestContext();
            context.Write("projectId", "project-a");
            var nodeExecutionEnvironment43 = new NodeExecutionEnvironment(runId, "project-a", flowId, "canvas-a", nodeId, 3);
            var runtime = new NodeExecutionRuntime(nodeExecutionEnvironment43);
            var sereinScriptNodeExecutor44 = new SereinScriptNodeExecutor(store);
            var createResult45 = NodeDefinition.Create(nodeId, NodeType.Script, "Script", script: definition);
            var dictionary46 = new Dictionary<string, object?>();
            var nodeExecutionRequest47 = new NodeExecutionRequest(createResult45, context, dictionary46, runtime);
            var result = await sereinScriptNodeExecutor44.ExecuteAsync(nodeExecutionRequest47, CancellationToken.None);
            Assert.True(result.IsSuccess, result.ErrorMessage);
            var toStringResult48 = runId.ToString("D");
            var convertOutputResult = ConvertOutput<string>(result, "runId");
            Assert.Equal(toStringResult48, convertOutputResult);
            var convertOutputResult49 = ConvertOutput<string>(result, "projectId");
            Assert.Equal("project-a", convertOutputResult49);
            var toStringResult50 = flowId.ToString("D");
            var convertOutputResult51 = ConvertOutput<string>(result, "flowId");
            Assert.Equal(toStringResult50, convertOutputResult51);
            var convertOutputResult52 = ConvertOutput<string>(result, "canvasId");
            Assert.Equal("canvas-a", convertOutputResult52);
            var convertOutputResult53 = ConvertOutput<string>(result, "nodeId");
            Assert.Equal(nodeId, convertOutputResult53);
            var convertOutputResult54 = ConvertOutput<int>(result, "frameDepth");
            Assert.Equal(3, convertOutputResult54);
            var convertOutputResult55 = ConvertOutput<bool>(result, "isCancellationRequested");
            Assert.False(convertOutputResult55);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void FailedFingerprintCannotSelectOldArtifactAndDoesNotDeleteOtherRunsVersion()
    {
        var getTempPathResult64 = Path.GetTempPath();
        var newGuidResult65 = Guid.NewGuid();
        var toStringResult66 = newGuidResult65.ToString("N");
        var root = Path.Combine(getTempPathResult64, "sereinflow-script-tests", toStringResult66);
        try
        {
            var store = new ScriptArtifactStore(root);
            IEnumerable<ScriptValueContract> evaluatedValue59 = [new("result", "number")];
            var valid = CreateScriptDefinition("return 1", [], evaluatedValue59);
            var first = store.RebuildProject("project", [valid]);
            Assert.True(first.IsSuccess);
            var artifactPath = first.Artifacts[valid.NodeId].Path;
            var operationResult1 = File.Exists(artifactPath);
            Assert.True(operationResult1);
            IEnumerable<ScriptValueContract> evaluatedValue60 = [new("result", "number")];
            var invalid = CreateScriptDefinition("return (", [], evaluatedValue60);
            var second = store.RebuildProject("project", [invalid]);
            Assert.False(second.IsSuccess);
            var operationResult2 = store.TryLoad("project", invalid, out _, out _);
            Assert.False(operationResult2);
            var operationResult3 = File.Exists(artifactPath);
            Assert.True(operationResult3);
            var operationResult4 = store.TryLoad("project", valid, out _, out _);
            Assert.True(operationResult4);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void ArtifactFingerprintIncludesTheScriptInputContract()
    {
        var getTempPathResult72 = Path.GetTempPath();
        var newGuidResult73 = Guid.NewGuid();
        var toStringResult74 = newGuidResult73.ToString("N");
        var root = Path.Combine(getTempPathResult72, "sereinflow-script-tests", toStringResult74);
        try
        {
            var store = new ScriptArtifactStore(root);
            IEnumerable<ScriptValueContract> evaluatedValue67 = [new("amount", "number", Id: "amount-id")];
            IEnumerable<ScriptValueContract> evaluatedValue68 = [new("result", "number")];
            var first = CreateScriptDefinition("return 1", evaluatedValue67, evaluatedValue68);
            IEnumerable<ScriptValueContract> evaluatedValue69 = [new("quantity", "number", Id: "quantity-id")];
            IEnumerable<ScriptValueContract> evaluatedValue70 = [new("result", "number")];
            var changedContract = CreateScriptDefinition("return 1", evaluatedValue69, evaluatedValue70);
            var evaluatedValue71 = store.RebuildProject("project", [first]).IsSuccess;
            Assert.True(evaluatedValue71);
            var tryLoadResult = store.TryLoad("project", changedContract, out _, out _);
            Assert.False(tryLoadResult);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void ConvertsRuntimeValuesOnlyAtTheTargetClrBoundary()
    {
        var dateTime = new DateTime(2026, 8, 27, 9, 30, 0, DateTimeKind.Utc);
        var timeSpan = TimeSpan.FromMinutes(5);
        var value = NumberValueFactory.Create(42);
        List<Value> evaluatedValue75 = [NumberValueFactory.Create(1), NumberValueFactory.Create(2)];
        var array = new ArrayValue(evaluatedValue75);
        var dictionary76 = new Dictionary<string, Value>
        {
            ["Name"] = StringValue.Create("Ada"),
            ["Count"] = NumberValueFactory.Create(3)
        };
        var obj = new ObjectValue(dictionary76);
        var createResult77 = StringValue.Create("text");
        var convertResult78 = ScriptValueTypeConverter.Convert(createResult77, typeof(string));
        Assert.Equal("text", convertResult78);
        var convertResult79 = ScriptValueTypeConverter.Convert(value, typeof(long));
        Assert.Equal(42L, convertResult79);
        var evaluatedValue80 = (bool)ScriptValueTypeConverter.Convert(BoolValue.True, typeof(bool))!;
        Assert.True(evaluatedValue80);
        var dateTimeValue = new DateTimeValue(dateTime);
        var convertResult81 = ScriptValueTypeConverter.Convert(dateTimeValue, typeof(DateTime));
        Assert.Equal(dateTime, convertResult81);
        var timeSpanValue = new TimeSpanValue(timeSpan);
        var convertResult82 = ScriptValueTypeConverter.Convert(timeSpanValue, typeof(TimeSpan));
        Assert.Equal(timeSpan, convertResult82);
        var convertResult83 = ScriptValueTypeConverter.Convert(array, typeof(List<int>));
        var isTypeResult = Assert.IsType<List<int>>(convertResult83);
        Assert.Equal([1, 2], isTypeResult);
        var convertResult84 = ScriptValueTypeConverter.Convert(array, typeof(int[]));
        var isTypeResult85 = Assert.IsType<int[]>(convertResult84);
        Assert.Equal([1, 2], isTypeResult85);
        var convertResult86 = ScriptValueTypeConverter.Convert(obj, typeof(SamplePayload));
        var evaluatedValue87 = Assert.IsType<SamplePayload>(convertResult86).Count;
        Assert.Equal(3, evaluatedValue87);
        var convertResult88 = ScriptValueTypeConverter.Convert(obj, typeof(SamplePayload));
        var evaluatedValue89 = Assert.IsType<SamplePayload>(convertResult88).Name;
        Assert.Equal("Ada", evaluatedValue89);
        var evaluatedValue93 = resolveIsTypeResult()["name"];
        Assert.Equal("Ada", evaluatedValue93);
        Dictionary<string, string> resolveIsTypeResult()
        {
            var dictionary90 = new Dictionary<string, Value>
            {
                ["name"] = StringValue.Create("Ada")
            };
            var objectValue = new ObjectValue(dictionary90);
            var convertResult91 = ScriptValueTypeConverter.Convert(objectValue, typeof(Dictionary<string, string>));
            var isTypeResult92 = Assert.IsType<Dictionary<string, string>>(convertResult91);
            return isTypeResult92;
        }
    }

    [Fact]
    public void PreservesSpecialRuntimeValuesAndUsesSafeAuditSummaries()
    {
        var raw = StringValue.Create("unchanged");
        var convertResult96 = ScriptValueTypeConverter.Convert(raw, typeof(Value));
        Assert.Same(raw, convertResult96);
        var convertResult97 = ScriptValueTypeConverter.Convert(raw, typeof(StringValue));
        Assert.Same(raw, convertResult97);
        var carrier = new SamplePayload
        {
            Name = "Worker only",
            Count = 7
        };
        var clrObject = new ClrObjectValue(carrier);
        var convertResult98 = ScriptValueTypeConverter.Convert(clrObject, typeof(SamplePayload));
        Assert.Same(carrier, convertResult98);
        var toAuditValueResult = ScriptValueConverter.ToAuditValue(clrObject);
        var objectSummary = Assert.IsType<Dictionary<string, object?>>(toAuditValueResult);
        Assert.Equal("ClrObject", objectSummary["$kind"]);
        Assert.Equal(typeof(SamplePayload).FullName, objectSummary["clrType"]);
        var operationResult5 = nameof(StaticMethod);
        var method = typeof(ScriptAdapterTests).GetMethod(operationResult5, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var clrMethod = new ClrMethodValue(method, null!);
        var convertResult99 = ScriptValueTypeConverter.Convert(clrMethod, typeof(System.Reflection.MethodInfo));
        Assert.Same(method, convertResult99);
        var toAuditValueResult100 = ScriptValueConverter.ToAuditValue(clrMethod);
        var methodSummary = Assert.IsType<Dictionary<string, object?>>(toAuditValueResult100);
        Assert.Equal("ClrMethod", methodSummary["$kind"]);
        var error = Assert.Throws<ScriptValueConversionException>(() =>
        {
            var createResult94 = StringValue.Create("x");
            var convertResult95 = ScriptValueTypeConverter.Convert(createResult94, typeof(int));
            return convertResult95;
        });
        Assert.Contains("cannot be converted", error.Message);
        Assert.Contains("无法转换", error.Message);
    }

    [Fact]
    public void AuditProjectionSummarizesPointerBasedClrValues()
    {
        var pointerPayload = new PointerPayload();
        var toAuditValueResult101 = ScriptValueConverter.ToAuditValue(pointerPayload);
        var summary = Assert.IsType<Dictionary<string, object?>>(toAuditValueResult101);
        Assert.Equal("ClrValue", summary["$kind"]);
        Assert.Equal(typeof(PointerPayload).FullName, summary["clrType"]);
        var isTypeResult102 = Assert.IsType<bool>(summary["serializable"]);
        Assert.False(isTypeResult102);
    }

    private static async Task<NodeExecutionResult> Execute(NodeDefinition node, IReadOnlyDictionary<string, object?> inputs, CancellationToken cancellationToken = default)
    {
        var executor = new SereinScriptNodeExecutor();
        var testContext103 = new TestContext();
        var nodeExecutionRequest104 = new NodeExecutionRequest(node, testContext103, inputs);
        var executeAsyncResult = await executor.ExecuteAsync(nodeExecutionRequest104, cancellationToken);
        return executeAsyncResult;
    }

    private static T ConvertOutput<T>(NodeExecutionResult result, string outputId)
    {
        var isAssignableFromResult105 = Assert.IsAssignableFrom<Value>(result.Outputs[outputId]);
        var convertResult106 = ScriptValueTypeConverter.Convert(isAssignableFromResult105, typeof(T));
        var isTypeResult107 = Assert.IsType<T>(convertResult106);
        return isTypeResult107;
    }

    private static NodeDefinition CreateScriptNode(string source, IEnumerable<ScriptValueContract> inputs, IEnumerable<ScriptValueContract> outputs)
    {
        var script = CreateScriptDefinition(source, inputs, outputs);
        var createResult108 = NodeDefinition.Create("node", NodeType.Script, "Script", script: script);
        return createResult108;
    }

    private static ScriptNodeDefinition CreateScriptDefinition(string source, IEnumerable<ScriptValueContract> inputs, IEnumerable<ScriptValueContract> outputs)
    {
        var createResult109 = ScriptNodeDefinition.Create("node", source, "1", inputs: inputs, outputs: outputs);
        return createResult109;
    }

    private sealed class TestContext : IExecutionContext
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);
        public object? Read(string key)
        {
            var getValueOrDefaultResult = _values.GetValueOrDefault(key);
            return getValueOrDefaultResult;
        }

        public void Write(string key, object? value) => _values[key] = value;
        public IReadOnlyDictionary<string, object?> Snapshot() => _values;
    }

    private sealed class SamplePayload
    {
        public string? Name { get; set; }
        public int Count { get; set; }
    }

    private sealed unsafe class PointerPayload
    {
        public byte* DataPointer => null;
    }

    private sealed class RecordingEventSink : INodeExecutionEventSink
    {
        public List<NodeExecutionLogEntry> Entries { get; } = [];

        public ValueTask PublishLogAsync(NodeExecutionLogEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
    }

    private static void StaticMethod()
    {
    }
}

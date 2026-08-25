using SereinFlow.Domain;
using SereinFlow.Runtime.Abstractions;
using ScriptLang;
using ScriptLang.Runtime;
using ScriptLang.Runtime.ByteCode;

namespace SereinFlow.ScriptAdapter;

public interface IScriptNodeExecutor : INodeExecutor
{
}

public sealed class SereinScriptNodeExecutor : IScriptNodeExecutor
{
    private readonly ScriptArtifactStore? _artifactStore;
    private readonly string _defaultProjectId;

    public SereinScriptNodeExecutor(ScriptArtifactStore? artifactStore = null, string defaultProjectId = "default")
    {
        _artifactStore = artifactStore;
        _defaultProjectId = defaultProjectId;
    }

    public NodeType NodeType => NodeType.Script;

    public async ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request), "The script execution request cannot be null. 脚本执行请求不能为空。");
        var definition = request.Node.Script;
        if (definition is null)
            return NodeExecutionResult.Error("script.definition_missing", "Script node definition is missing. 脚本节点定义缺失。");

        try
        {
            ValidateInputs(definition, request.Inputs);
            cancellationToken.ThrowIfCancellationRequested();

            var projectId = request.Context.Read("projectId") as string ?? _defaultProjectId;
            var engine = new ScriptEngine();
            ByteCodeChunk chunk;
            string? artifactPath = null;
            if (_artifactStore?.TryLoad(projectId, definition, out var persistedChunk, out artifactPath) == true && persistedChunk is not null)
            {
                chunk = persistedChunk;
            }
            else
            {
                chunk = engine.CompileSource(
                    definition.Source,
                    $"{definition.NodeId}.script",
                    definition.Inputs.Select(input => input.Name));
            }

            var task = engine.CreateTask(chunk, artifactPath);
            foreach (var input in definition.Inputs)
                engine.SetGlobal(input.Name, ScriptValueConverter.ToScriptValue(request.Inputs.GetValueOrDefault(input.Name)));

            var result = await task.RunAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return NodeExecutionResult.Success(MapOutputs(definition, result));
        }
        catch (OperationCanceledException)
        {
            return NodeExecutionResult.Failure("script.cancelled", "Script execution was cancelled. 脚本执行已取消。");
        }
        catch (ScriptExecutionException exception)
        {
            // Script exceptions are engine-visible errors rather than a
            // business Failure branch.  This keeps unhandled script faults
            // distinguishable from a script that deliberately returns a
            // failure value.
            // 脚本异常属于引擎错误，统一进入 Error 分支；只有脚本显式返回业务结果时才走 Success/Failure。
            return NodeExecutionResult.Error(exception.Code, exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return NodeExecutionResult.Error("script.value_unsupported", $"Script value is not supported. 脚本值不受支持。 {exception.Message}");
        }
        catch (InvalidOperationException exception)
        {
            return NodeExecutionResult.Error("script.compile_failed", $"Script compilation failed. 脚本编译失败。 {exception.Message}");
        }
        catch (Exception exception)
        {
            return NodeExecutionResult.Error("script.runtime_failed", $"Script runtime execution failed. 脚本运行时执行失败。 {exception.Message}");
        }
    }

    private static void ValidateInputs(ScriptNodeDefinition definition, IReadOnlyDictionary<string, object?> inputs)
    {
        var contracts = definition.Inputs.ToDictionary(input => input.Name, StringComparer.Ordinal);
        var unknown = inputs.Keys.FirstOrDefault(name => !contracts.ContainsKey(name));
        if (unknown is not null)
            throw new ScriptExecutionException("script.input_unknown", $"Unknown script input '{unknown}'. 未知的脚本输入“{unknown}”。");

        var missing = definition.Inputs.FirstOrDefault(input => input.Required && !inputs.ContainsKey(input.Name));
        if (missing is not null)
            throw new ScriptExecutionException("script.input_missing", $"Required script input '{missing.Name}' is missing. 缺少必需的脚本输入“{missing.Name}”。");
    }

    private static Dictionary<string, object?> MapOutputs(ScriptNodeDefinition definition, Value result)
    {
        if (definition.Outputs.Count == 0)
            return new Dictionary<string, object?>();

        if (definition.Outputs.Count == 1)
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [definition.Outputs[0].Name] = ScriptValueConverter.ToClrValue(result)
            };

        if (result is not ObjectValue objectResult)
            throw new ScriptExecutionException("script.output_shape_invalid", "A script with multiple outputs must return an object. 包含多个输出的脚本必须返回对象。");

        var outputs = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var output in definition.Outputs)
        {
            outputs[output.Name] = objectResult.TryGetValue(output.Name, out var value)
                ? ScriptValueConverter.ToClrValue(value)
                : null;
        }
        return outputs;
    }
}

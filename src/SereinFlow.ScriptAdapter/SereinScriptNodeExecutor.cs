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
        ArgumentNullException.ThrowIfNull(request);
        var definition = request.Node.Script;
        if (definition is null)
            return NodeExecutionResult.Failure("script.definition_missing", "Script node definition is missing.");

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
            return NodeExecutionResult.Failure("script.cancelled", "Script execution was cancelled.");
        }
        catch (ScriptExecutionException exception)
        {
            return NodeExecutionResult.Failure(exception.Code, exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return NodeExecutionResult.Failure("script.value_unsupported", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return NodeExecutionResult.Failure("script.compile_failed", exception.Message);
        }
        catch (Exception exception)
        {
            return NodeExecutionResult.Failure("script.runtime_failed", exception.Message);
        }
    }

    private static void ValidateInputs(ScriptNodeDefinition definition, IReadOnlyDictionary<string, object?> inputs)
    {
        var contracts = definition.Inputs.ToDictionary(input => input.Name, StringComparer.Ordinal);
        var unknown = inputs.Keys.FirstOrDefault(name => !contracts.ContainsKey(name));
        if (unknown is not null)
            throw new ScriptExecutionException("script.input_unknown", $"Unknown script input '{unknown}'.");

        var missing = definition.Inputs.FirstOrDefault(input => input.Required && !inputs.ContainsKey(input.Name));
        if (missing is not null)
            throw new ScriptExecutionException("script.input_missing", $"Required script input '{missing.Name}' is missing.");
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
            throw new ScriptExecutionException("script.output_shape_invalid", "A script with multiple outputs must return an object.");

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

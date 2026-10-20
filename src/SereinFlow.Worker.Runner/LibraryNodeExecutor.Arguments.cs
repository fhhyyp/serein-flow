using System.Reflection;
using SereinFlow.Contracts;
using SereinFlow.Library;
using SereinFlow.Domain;
using SereinFlow.Runtime.Abstractions;
using SereinFlow.ScriptAdapter;

namespace SereinFlow.Worker.Runner;

internal sealed partial class LibraryNodeExecutor
{
    private static object?[] BuildArguments(NodeExecutionRequest request, MethodInfo method, LibraryFlowContext flowContext, Dictionary<string, object?> auditInputs)
    {
        var parameters = method.GetParameters();
        var arguments = new object?[parameters.Length];
        var ordinaryParameterIndex = 0;
        for (var index = 0; index < parameters.Length; index++)
        {
            var parameter = parameters[index];
            if (WorkerLibraryRuntimeCache.IsFlowContextParameter(parameter))
            {
                arguments[index] = flowContext;
                continue;
            }

            var name = parameter.Name ?? $"param{index + 1}";
            var definition = FindParameterDefinition(request, name, ordinaryParameterIndex);
            arguments[index] = BuildArgument(request, parameter, definition, name, auditInputs);
            ordinaryParameterIndex++;
        }

        return arguments;
    }

    private static NodeParameterDefinition? FindParameterDefinition(NodeExecutionRequest request, string name, int ordinaryParameterIndex)
    {
        var evaluatedValue1 = request.Node.Parameters.FirstOrDefault(item =>
        {
            var evaluatedValue = string.Equals(item.Name, name, StringComparison.Ordinal) || string.Equals(item.Id, name, StringComparison.Ordinal);
            return evaluatedValue;
        }) ?? (ordinaryParameterIndex < request.Node.Parameters.Count ? request.Node.Parameters[ordinaryParameterIndex] : null);
        return evaluatedValue1;
    }

    private static object? BuildArgument(NodeExecutionRequest request, ParameterInfo parameter, NodeParameterDefinition? definition, string name, Dictionary<string, object?> auditInputs)
    {
        if (parameter.GetCustomAttribute<ParamArrayAttribute>() is not null)
        {
            try
            {
                var buildVariadicArgumentResult = BuildVariadicArgument(request, parameter, definition, auditInputs);
                return buildVariadicArgumentResult;
            }
            catch (ScriptValueConversionException exception)
            {
                throw new LibraryInputException(ScriptErrorCodes.ValueConversionFailed, exception.Message, exception);
            }
            catch (Exception exception)
            {
                throw InvalidInput(parameter, name, exception);
            }
        }

        if (!TryResolveInput(request.Inputs, name, definition, out var value))
        {
            if (parameter.HasDefaultValue)
            {
                auditInputs[name] = parameter.DefaultValue;
                return parameter.DefaultValue;
            }

            if (!parameter.ParameterType.IsValueType || Nullable.GetUnderlyingType(parameter.ParameterType) is not null)
            {
                auditInputs[name] = null;
                return null;
            }

            throw new LibraryInputException(NodeErrorCodes.InputMissing, $"Required library input '{name}' is missing. 缺少类库必需输入“{name}”。");
        }

        auditInputs[name] = value;
        try
        {
            var converted = LibraryArgumentConverter.Convert(value, parameter.ParameterType);
            auditInputs[name] = converted;
            return converted;
        }
        catch (ScriptValueConversionException exception)
        {
            throw new LibraryInputException(ScriptErrorCodes.ValueConversionFailed, exception.Message, exception);
        }
        catch (Exception exception)
        {
            throw InvalidInput(parameter, name, exception);
        }
    }

    private static bool TryResolveInput(IReadOnlyDictionary<string, object?> inputs, string name, NodeParameterDefinition? definition, out object? value)
    {
        var evaluatedValue2 = inputs.TryGetValue(name, out value) || (definition is not null && inputs.TryGetValue(definition.Id, out value));
        return evaluatedValue2;
    }

    private static LibraryInputException InvalidInput(ParameterInfo parameter, string name, Exception exception)
    {
        LibraryInputException evaluatedValue3 = new(NodeErrorCodes.InputInvalid, $"Library input '{name}' cannot be converted to '{parameter.ParameterType.Name}'. 类库输入“{name}”无法转换为“{parameter.ParameterType.Name}”。 {exception.Message}", exception);
        return evaluatedValue3;
    }

    private static Array BuildVariadicArgument(NodeExecutionRequest request, ParameterInfo parameter, NodeParameterDefinition? representative, Dictionary<string, object?> auditInputs)
    {
        var arrayType = parameter.ParameterType;
        var elementType = arrayType.GetElementType() ?? throw new InvalidOperationException("The params array has no element type. params 数组没有元素类型。");
        var groupId = representative?.VariadicGroupId ?? representative?.Id ?? parameter.Name ?? string.Empty;
        var definitions = request.Node.Parameters
            .Where(item =>
            {
                var evaluatedValue4 = item.IsVariadic && string.Equals(item.VariadicGroupId ?? item.Id, groupId, StringComparison.Ordinal);
                return evaluatedValue4;
            })
            .ToArray();
        if (definitions.Length == 0 && representative?.IsVariadic == true)
        {
            definitions = [representative];
        }

        if (definitions.Length == 1 && definitions[0].VariadicMode == VariadicParameterMode.Collection)
        {
            var collection = definitions[0];
            if (!TryGetInput(request.Inputs, collection, out var value))
            {
                var createInstanceResult = Array.CreateInstance(elementType, 0);
                return createInstanceResult;
            }

            var converted = LibraryArgumentConverter.Convert(value, arrayType);
            auditInputs[collection.Name] = converted;
            var evaluatedValue5 = (Array)(converted ?? Array.CreateInstance(elementType, 0));
            return evaluatedValue5;
        }

        var values = new List<object?>();
        foreach (var definition in definitions)
        {
            if (!TryGetInput(request.Inputs, definition, out var value) || value is null)
            {
                if (definition.Required)
                {
                    throw new InvalidOperationException($"Required variadic input '{definition.Name}' is missing. 缺少必需可变参数输入“{definition.Name}”。");
                }

                continue;
            }

            auditInputs[definition.Name] = value;
            var converted = LibraryArgumentConverter.Convert(value, elementType);
            auditInputs[definition.Name] = converted;
            values.Add(converted);
        }

        var result = Array.CreateInstance(elementType, values.Count);
        for (var index = 0; index < values.Count; index++)
        {
            result.SetValue(values[index], index);
        }

        return result;
    }

    private static bool TryGetInput(IReadOnlyDictionary<string, object?> inputs, NodeParameterDefinition parameter, out object? value)
    {
        var evaluatedValue6 = inputs.TryGetValue(parameter.Id, out value) || inputs.TryGetValue(parameter.Name, out value);
        return evaluatedValue6;
    }
}

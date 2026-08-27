using System.Text.Json;
using SereinFlow.Domain;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Runtime;

public sealed class DataConnectionResolver
{
    public IReadOnlyDictionary<string, object?> Resolve(
        NodeDefinition node,
        ExecutionPlan plan,
        FlowExecutionSession session)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var parameter in node.Parameters)
        {
            var incoming = plan.GetIncomingData(node.Id, parameter.Id);
            if (incoming.Count == 0 && !string.Equals(parameter.Id, parameter.Name, StringComparison.Ordinal))
                incoming = plan.GetIncomingData(node.Id, parameter.Name);

            // A concrete data connection is authoritative for the target
            // parameter; the editor's source selector is only a fallback for
            // unconnected parameters.
            // 已建立的数据连接优先于参数来源选择器；未连接时才使用参数自身来源。
            object? value;
            var hasFlowCallInput = session.TryReadFlowCallInput(node.Id, parameter.Id, out var flowCallValue);
            if (hasFlowCallInput)
            {
                // Explicit FlowCall mappings have the highest precedence at the
                // public target entry and never leak to downstream nodes.
                // 显式 FlowCall 映射在公开目标入口优先级最高，且不会泄漏到下游节点。
                value = flowCallValue;
            }
            else
            {
                value = incoming.Count > 0
                    ? ReadPreviousNode(incoming, session)
                    : parameter.Source switch
                    {
                        DataSource.Literal => ParseJson(parameter.ValueJson),
                        DataSource.PreviousNode => ReadPreviousNode(
                            string.IsNullOrWhiteSpace(parameter.SourceNodeId)
                                ? []
                                : [ConnectionDefinition.Data(
                                    parameter.SourceNodeId,
                                    parameter.SourcePortId ?? "data-out",
                                    node.Id,
                                    parameter.Id,
                                    DataSource.PreviousNode)],
                            session),
                        DataSource.ProjectInput => ReadProjectInput(parameter.ProjectInputKey ?? parameter.Name, session),
                        DataSource.Expression => EvaluateExpression(parameter.Expression, session),
                        _ => null
                    };
            }

            if (value is null && parameter.Required)
            {
                throw new FlowDataBindingException(
                    "node.input_missing",
                    $"Required input '{parameter.Name}' on node '{node.Id}' is missing. 节点“{node.Id}”缺少必需输入“{parameter.Name}”。",
                    values);
            }

            // An empty optional literal means the caller did not provide an
            // argument. Omit it so a reflected C# optional parameter can use
            // its declared default in the Worker.
            // 空的可选字面量表示调用方未提供参数。省略该输入，使 Worker 中反射的方法使用 C# 声明的默认值。
            if (value is null
                && !parameter.Required
                && !hasFlowCallInput
                && incoming.Count == 0
                && parameter.Source == DataSource.Literal
                && string.IsNullOrWhiteSpace(parameter.ValueJson))
            {
                continue;
            }

            // Parameter IDs are the persisted binding identity. Names are UI
            // labels and may change without breaking data connections.
            // 参数 ID 是持久化绑定身份；名称只是 UI 标签，重命名不能破坏数据连接。
            values[parameter.Id] = value;
        }

        return values;
    }

    private static object? ReadPreviousNode(
        IReadOnlyList<ConnectionDefinition> incoming,
        FlowExecutionSession session)
    {
        var connection = incoming.FirstOrDefault();
        if (connection is null)
            return null;

        return session.Read($"{connection.FromNodeId}.{connection.FromPortId}");
    }

    private static object? ReadProjectInput(string key, FlowExecutionSession session)
        => session.Read($"project.{key}");

    private static object? EvaluateExpression(string? expression, FlowExecutionSession session)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return null;

        var trimmed = expression.Trim();
        if (trimmed.StartsWith("project.", StringComparison.Ordinal))
            return session.Read(trimmed);

        if (trimmed.Count(static c => c == '.') == 1)
            return session.Read(trimmed);

        return ParseJson(trimmed) ?? trimmed;
    }

    private static object? ParseJson(string? valueJson)
    {
        if (string.IsNullOrWhiteSpace(valueJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(valueJson);
            return ConvertJson(document.RootElement);
        }
        catch (JsonException)
        {
            return valueJson;
        }
    }

    private static object? ConvertJson(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJson).ToArray(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(item => item.Name, item => ConvertJson(item.Value), StringComparer.Ordinal),
            _ => null
        };
}

public sealed class FlowDataBindingException : Exception
{
    public FlowDataBindingException(
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? resolvedInputs = null)
        : base(message)
    {
        Code = code;
        ResolvedInputs = resolvedInputs is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(resolvedInputs, StringComparer.Ordinal);
    }

    public string Code { get; }

    /// <summary>
    /// Values resolved before the binding error stopped the current node.
    /// 参数绑定错误发生前已经解析完成的输入值。
    /// </summary>
    public IReadOnlyDictionary<string, object?> ResolvedInputs { get; }
}

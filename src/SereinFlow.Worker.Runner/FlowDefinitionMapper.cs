using System.Text.Json;
using System.Text.Json.Serialization;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Runtime;
using SereinFlow.ScriptAdapter;

namespace SereinFlow.Worker.Runner;

public static class FlowDefinitionMapper
{
    private static readonly JsonSerializerOptions Options = SereinJsonSerialization.CreateWebOptions(options =>
    {
        options.PropertyNameCaseInsensitive = true;
        options.Converters.Add(new JsonStringEnumConverter());
    });

    public static FlowDefinition Map(string definitionJson)
    {
        var dto = JsonSerializer.Deserialize<FlowDefinitionDto>(definitionJson, Options)
            ?? throw new InvalidOperationException("Flow definition payload is empty. 流程定义载荷为空。");
        return FlowDefinition.Create(
            dto.Id,
            dto.Version,
            dto.Canvases.Select(MapCanvas),
            dto.EntryNodeId,
            dto.SchemaVersion,
            dto.Checksum,
            dto.RunPolicy is null ? null : new FlowRunPolicy((FlowConcurrencyMode)dto.RunPolicy.ConcurrencyMode));
    }

    private static CanvasDefinition MapCanvas(CanvasDto dto)
        => CanvasDefinition.Create(dto.Id, (CanvasLifecycle)dto.Lifecycle, dto.Nodes.Select(MapNode), dto.Connections.Select(MapConnection));

    private static NodeDefinition MapNode(NodeDto dto)
        => NodeDefinition.Create(
            dto.Id,
            (NodeType)dto.Type,
            dto.DisplayName,
            new NodePosition(dto.X, dto.Y),
            dto.Ports.Select(port => new PortDefinition(port.Id, port.Name, Enum.Parse<PortDirection>(port.Direction, true), port.Required)),
            dto.Parameters.Select(parameter => new NodeParameterDefinition(
                parameter.Name,
                parameter.ValueJson,
                (DataSource)parameter.Source,
                parameter.Required,
                parameter.Ui?.Id,
                parameter.Ui?.ProjectInputKey,
                parameter.Ui?.Expression,
                parameter.Ui?.SourceNodeId,
                parameter.Ui?.SourcePortId,
                parameter.Ui?.ValueKind,
                parameter.Ui?.Description,
                parameter.Ui?.IsVariadic ?? false,
                parameter.Ui?.VariadicGroupId,
                parameter.Ui?.ElementType,
                ParseVariadicMode(parameter.Ui?.VariadicMode))),
            dto.Script is null ? null : ScriptNodeDefinition.Create(
                dto.Script.NodeId,
                dto.Script.Source,
                dto.Script.LanguageVersion,
                // SourceHash is presentation/cache metadata. The Worker derives
                // it from the immutable source instead of rejecting older DTOs.
                null,
                dto.Script.Inputs.Select(input => new ScriptValueContract(input.Name, input.ValueKind, input.Required, input.Id, input.Description)),
                dto.Script.Outputs.Select(output => new ScriptValueContract(output.Name, output.ValueKind, output.Required, output.Id, output.Description))),
            dto.Ui is null
                ? null
                : new NodeRuntimeDefinition(
                    dto.Ui.LibraryId,
                    dto.Ui.ClassName,
                    dto.Ui.MethodName,
                    dto.Ui.DllName,
                    dto.Ui.DllVersion,
                    dto.Ui.ReturnType,
                    dto.Ui.TargetNodeId,
                    Guid.TryParse(dto.Ui.TargetFlowId, out var targetFlowId) ? targetFlowId : null,
                    dto.Ui.IsAwaitable ?? false,
                    dto.Ui.StaticReturnType,
                    dto.Ui.IsDynamicReturnType ?? false,
                    dto.Ui.TargetCanvasId,
                    dto.Ui.IsPublic ?? false,
                    dto.Ui.FlowCallParameterBindings?.Select(item => new FlowCallParameterBinding(item.CallParameterId, item.TargetParameterId)).ToArray(),
                    dto.Ui.LibraryNodeContractId));

    private static VariadicParameterMode? ParseVariadicMode(string? value)
        => Enum.TryParse<VariadicParameterMode>(value, ignoreCase: true, out var mode) ? mode : null;

    private static ConnectionDefinition MapConnection(ConnectionDto dto)
    {
        if (dto.Kind == ConnectionKindDto.Execution)
        {
            if (dto.Branch is null)
                throw new ArgumentException("Execution connections must declare Success, Failure, or Error. 流程连接必须声明 Success、Failure 或 Error 分支。", nameof(dto));

            if (!Enum.IsDefined(dto.Branch.Value))
                throw new ArgumentException("Execution connection branch is invalid. 流程连接分支无效。", nameof(dto));

            return ConnectionDefinition.Execution(
                dto.FromNodeId,
                dto.FromPortId,
                dto.ToNodeId,
                dto.ToPortId,
                (ExecutionBranch)dto.Branch.Value,
                dto.Priority,
                dto.Id);
        }

        return ConnectionDefinition.Data(
            dto.FromNodeId,
            dto.FromPortId,
            dto.ToNodeId,
            dto.ToPortId,
            (DataSource)(dto.DataSource ?? DataSourceDto.Literal),
            dto.Priority,
            dto.Id);
    }
}

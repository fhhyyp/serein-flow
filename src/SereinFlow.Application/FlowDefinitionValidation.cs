using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application;

public static class FlowDefinitionContractMapper
{
    public static FlowDefinition Map(FlowDefinitionDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return FlowDefinition.Create(
            dto.Id,
            dto.Version,
            dto.Canvases.Select(MapCanvas),
            dto.EntryNodeId,
            dto.SchemaVersion,
            dto.Checksum);
    }

    private static CanvasDefinition MapCanvas(CanvasDto dto)
        => CanvasDefinition.Create(
            dto.Id,
            (CanvasLifecycle)dto.Lifecycle,
            dto.Nodes.Select(MapNode),
            dto.Connections.Select(MapConnection));

    private static NodeDefinition MapNode(NodeDto dto)
        => NodeDefinition.Create(
            dto.Id,
            (NodeType)dto.Type,
            dto.DisplayName,
            new NodePosition(dto.X, dto.Y),
            dto.Ports.Select(port => new PortDefinition(port.Id, port.Name, Enum.Parse<PortDirection>(port.Direction, true), port.Required)),
            dto.Parameters.Select(parameter => new NodeParameterDefinition(parameter.Name, parameter.ValueJson, (DataSource)parameter.Source, parameter.Required)),
            dto.Script is null ? null : ScriptNodeDefinition.Create(
                dto.Script.NodeId,
                dto.Script.Source,
                dto.Script.LanguageVersion,
                dto.Script.SourceHash,
                dto.Script.Inputs.Select(input => new ScriptValueContract(input.Name, input.ValueKind, input.Required)),
                dto.Script.Outputs.Select(output => new ScriptValueContract(output.Name, output.ValueKind, output.Required))));

    private static ConnectionDefinition MapConnection(ConnectionDto dto)
        => dto.Kind == ConnectionKindDto.Execution
            ? ConnectionDefinition.Execution(dto.FromNodeId, dto.FromPortId, dto.ToNodeId, dto.ToPortId, (ExecutionBranch)(dto.Branch ?? ExecutionBranchDto.Success), dto.Priority, dto.Id)
            : ConnectionDefinition.Data(dto.FromNodeId, dto.FromPortId, dto.ToNodeId, dto.ToPortId, (DataSource)(dto.DataSource ?? DataSourceDto.Literal), dto.Priority, dto.Id);
}

public static class FlowDefinitionContractValidator
{
    public static FlowValidationResultDto Validate(FlowDefinitionDto definition)
    {
        try
        {
            var diagnostics = FlowDefinitionContractMapper.Map(definition)
                .Validate()
                .Select(static diagnostic => new ValidationDiagnosticDto(diagnostic.Code, diagnostic.Message, diagnostic.Path))
                .ToArray();
            return new FlowValidationResultDto(diagnostics.Length == 0, diagnostics);
        }
        catch (ArgumentException exception)
        {
            return new FlowValidationResultDto(false, [new ValidationDiagnosticDto("flow.invalid", exception.Message, null)]);
        }
    }
}

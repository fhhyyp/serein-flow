using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application;

public static class FlowDefinitionContractMapper
{
    public static FlowDefinition Map(FlowDefinitionDto dto)
    {
        if (dto is null)
            throw new ArgumentNullException(nameof(dto), "The flow definition cannot be null. 流程定义不能为空。");
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
                parameter.Ui?.ValueKind)),
            dto.Script is null ? null : ScriptNodeDefinition.Create(
                dto.Script.NodeId,
                dto.Script.Source,
                dto.Script.LanguageVersion,
                dto.Script.SourceHash,
                dto.Script.Inputs.Select(input => new ScriptValueContract(input.Name, input.ValueKind, input.Required)),
                dto.Script.Outputs.Select(output => new ScriptValueContract(output.Name, output.ValueKind, output.Required))),
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
                    dto.Ui.IsDynamicReturnType ?? false));

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

public static class FlowDefinitionContractValidator
{
    public static FlowValidationResultDto Validate(FlowDefinitionDto definition)
    {
        try
        {
            var domain = FlowDefinitionContractMapper.Map(definition);
            var diagnostics = domain
                .Validate()
                .Select(static diagnostic => new ValidationDiagnosticDto(diagnostic.Code, diagnostic.Message, diagnostic.Path))
                .ToList();
            if (diagnostics.Count == 0)
            {
                try
                {
                    _ = new SereinFlow.Runtime.ExecutionPlanBuilder().Build(domain);
                }
                catch (DomainValidationException exception)
                {
                    diagnostics.AddRange(exception.Diagnostics.Select(static diagnostic =>
                        new ValidationDiagnosticDto(diagnostic.Code, diagnostic.Message, diagnostic.Path)));
                }
            }
            return new FlowValidationResultDto(diagnostics.Count == 0, diagnostics);
        }
        catch (ArgumentException exception)
        {
            return new FlowValidationResultDto(false, [new ValidationDiagnosticDto(
                "flow.invalid",
                $"Flow definition is invalid. 流程定义无效。 {exception.Message}",
                null)]);
        }
    }
}

public static class FlowDefinitionContractNormalizer
{
    public static FlowDefinitionDto Normalize(FlowDefinitionDto definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var domain = FlowDefinitionContractMapper.Map(definition);
        var plan = new SereinFlow.Runtime.ExecutionPlanBuilder().Build(domain);
        var normalizedCanvases = definition.Canvases
            .Select(canvas => canvas with
            {
                Nodes = canvas.Nodes.Select(node =>
                {
                    if (!plan.Nodes.TryGetValue(node.Id, out var runtimeNode)
                        || runtimeNode.Type != NodeType.FlowCall
                        || runtimeNode.Runtime is null
                        || node.Ui is null)
                    {
                        return node;
                    }

                    return node with
                    {
                        Ui = node.Ui with
                        {
                            StaticReturnType = runtimeNode.Runtime.StaticReturnType,
                            IsDynamicReturnType = runtimeNode.Runtime.IsDynamicReturnType,
                        },
                    };
                }).ToArray(),
            })
            .ToArray();
        return definition with { Canvases = normalizedCanvases };
    }
}

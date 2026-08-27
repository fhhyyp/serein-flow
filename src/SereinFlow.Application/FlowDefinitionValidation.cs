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
            dto.Checksum,
            dto.RunPolicy is null ? null : new FlowRunPolicy((FlowConcurrencyMode)dto.RunPolicy.ConcurrencyMode));
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
    /// <summary>
    /// Validates a flow that is being saved by the editor. An empty entry node
    /// represents an intentionally blank editor draft and is not executable.
    /// 校验编辑器保存的流程。空入口节点表示刻意保留的空白编辑草稿，不能执行。
    /// </summary>
    public static FlowValidationResultDto ValidateForPersistence(FlowDefinitionDto definition)
        => ValidateCore(definition, requireExecutionPlan: !string.IsNullOrWhiteSpace(definition.EntryNodeId));

    /// <summary>
    /// Validates a flow that is about to enter the runtime. Runtime validation
    /// always requires an entry node and a complete executable plan.
    /// 校验即将进入运行时的流程。运行校验始终要求入口节点和完整的可执行计划。
    /// </summary>
    public static FlowValidationResultDto ValidateForExecution(FlowDefinitionDto definition)
        => ValidateCore(definition, requireExecutionPlan: true);

    // Preserve the existing strict behavior for callers that have not yet
    // opted into one of the explicit validation contexts.
    // 对尚未迁移到明确校验上下文的调用方，保持原有的严格行为。
    public static FlowValidationResultDto Validate(FlowDefinitionDto definition)
        => ValidateForExecution(definition);

    private static FlowValidationResultDto ValidateCore(
        FlowDefinitionDto definition,
        bool requireExecutionPlan)
    {
        try
        {
            if (definition.RunPolicy is null)
            {
                return new FlowValidationResultDto(false, [new ValidationDiagnosticDto(
                    "flow.run_policy_missing",
                    "The flow run policy is required. 流程运行策略不能为空。",
                    "runPolicy")]);
            }
            if (!Enum.IsDefined(definition.RunPolicy.ConcurrencyMode))
            {
                return new FlowValidationResultDto(false, [new ValidationDiagnosticDto(
                    "flow.run_policy_invalid",
                    "The flow run policy is invalid. 流程运行策略无效。",
                    "runPolicy.concurrencyMode")]);
            }
            var domain = FlowDefinitionContractMapper.Map(definition);
            var diagnostics = domain
                .Validate()
                .Select(static diagnostic => new ValidationDiagnosticDto(diagnostic.Code, diagnostic.Message, diagnostic.Path))
                .ToList();
            if (diagnostics.Count == 0 && requireExecutionPlan)
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
    /// <summary>
    /// Normalizes a flow persisted by the editor. A blank draft has no
    /// execution plan, so FlowCall return-type analysis is deferred until it
    /// receives an entry node and becomes executable.
    /// 规范化由编辑器持久化的流程。空白草稿没有执行计划，因此 FlowCall 返回类型分析会延后到
    /// 它拥有入口节点并成为可执行流程之后。
    /// </summary>
    public static FlowDefinitionDto NormalizeForPersistence(FlowDefinitionDto definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return string.IsNullOrWhiteSpace(definition.EntryNodeId)
            ? definition
            : NormalizeForExecution(definition);
    }

    public static FlowDefinitionDto Normalize(FlowDefinitionDto definition)
        => NormalizeForExecution(definition);

    public static FlowDefinitionDto NormalizeForExecution(FlowDefinitionDto definition)
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

using System;
using System.Collections.Generic;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application;

public static class FlowDefinitionContractMapper
{
    public static FlowDefinition Map(FlowDefinitionDto dto)
    {
        if (dto is null)
        {
            throw new ArgumentNullException(nameof(dto), "The flow definition cannot be null. 流程定义不能为空。");
        }

        var selectResult = dto.Canvases.Select(MapCanvas);
        FlowRunPolicy? evaluatedValue = dto.RunPolicy is null ? null : new FlowRunPolicy((FlowConcurrencyMode)dto.RunPolicy.ConcurrencyMode);
        var createResult = FlowDefinition.Create(dto.Id, dto.Version, selectResult, dto.EntryNodeId, dto.SchemaVersion, dto.Checksum, evaluatedValue);
        return createResult;
    }

    private static CanvasDefinition MapCanvas(CanvasDto dto)
    {
        var selectResult1 = dto.Nodes.Select(MapNode);
        var selectResult2 = dto.Connections.Select(MapConnection);
        var createResult3 = CanvasDefinition.Create(dto.Id, (CanvasLifecycle)dto.Lifecycle, selectResult1, selectResult2);
        return createResult3;
    }

    private static NodeDefinition MapNode(NodeDto dto)
    {
        var nodePosition = new NodePosition(dto.X, dto.Y);
        var selectResult5 = dto.Ports.Select(port =>
        {
            var parseResult = Enum.Parse<PortDirection>(port.Direction, true);
            var portDefinition = new PortDefinition(port.Id, port.Name, parseResult, port.Required);
            return portDefinition;
        });
        var selectResult6 = dto.Parameters.Select(parameter =>
        {
            var parseVariadicModeResult = ParseVariadicMode(parameter.Ui?.VariadicMode);
            var mapEnumMetadataResult = MapEnumMetadata(parameter.Ui?.EnumMetadata);
            var nodeParameterDefinition = new NodeParameterDefinition(parameter.Name, parameter.ValueJson, (DataSource)parameter.Source, parameter.Required, parameter.Ui?.Id, parameter.Ui?.ProjectInputKey, parameter.Ui?.Expression, parameter.Ui?.SourceNodeId, parameter.Ui?.SourcePortId, parameter.Ui?.ValueKind, parameter.Ui?.Description, parameter.Ui?.IsVariadic ?? false, parameter.Ui?.VariadicGroupId, parameter.Ui?.ElementType, parseVariadicModeResult, mapEnumMetadataResult);
            return nodeParameterDefinition;
        });
        ScriptNodeDefinition conditionalValue;
        if (dto.Script is null)
        {
            conditionalValue = null;
        }
        else
        {
            var selectResult3 = dto.Parameters.Select(input =>
            {
                var scriptValueContract = new ScriptValueContract(input.Name, input.Ui?.ValueKind ?? "System.Object", input.Required, input.Ui?.Id, input.Ui?.Description);
                return scriptValueContract;
            });
            var selectResult4 = dto.Script.Outputs.Select(output =>
            {
                var scriptValueContract4 = new ScriptValueContract(output.Name, output.ValueKind, output.Required, output.Id, output.Description);
                return scriptValueContract4;
            });
            conditionalValue = ScriptNodeDefinition.Create(dto.Script.NodeId, dto.Script.Source, dto.Script.LanguageVersion, // sync while a script is being edited. Compute it from the
                                                                                                                             // authoritative source on the server instead.
                                                                                                                             // 浏览器编辑脚本时无法可靠同步加密源哈希，因此由服务端从权威源代码重新计算。
            sourceHash: null, selectResult3, selectResult4);
        }

        ScriptNodeDefinition? evaluatedValue7 = conditionalValue;
        NodeRuntimeDefinition conditionalValue5;
        if (dto.Ui is null)
        {
            conditionalValue5 = null;
        }
        else
        {
            Guid? evaluatedValue6 = Guid.TryParse(dto.Ui.TargetFlowId, out var targetFlowId) ? targetFlowId : null;
            var optionalReceiver = dto.Ui.FlowCallParameterBindings;
            IReadOnlyList<FlowCallParameterBinding>? optionalResult = default(IReadOnlyList<FlowCallParameterBinding>);
            if (optionalReceiver is not null)
            {
                var selectResult7 = optionalReceiver.Select(item =>
                {
                    var flowCallParameterBinding = new FlowCallParameterBinding(item.CallParameterId, item.TargetParameterId);
                    return flowCallParameterBinding;
                });
                optionalResult = selectResult7.ToArray();
            }

            var evaluatedValue9 = optionalResult;
            conditionalValue5 = new NodeRuntimeDefinition(dto.Ui.LibraryId, dto.Ui.ClassName, dto.Ui.MethodName, dto.Ui.DllName, dto.Ui.DllVersion, dto.Ui.ReturnType, dto.Ui.TargetNodeId, evaluatedValue6, dto.Ui.IsAwaitable ?? false, dto.Ui.StaticReturnType, dto.Ui.IsDynamicReturnType ?? false, dto.Ui.TargetCanvasId, dto.Ui.IsPublic ?? false, evaluatedValue9, dto.Ui.LibraryNodeContractId);
        }

        NodeRuntimeDefinition? evaluatedValue8 = conditionalValue5;
        var createResult9 = NodeDefinition.Create(dto.Id, (NodeType)dto.Type, dto.DisplayName, nodePosition, selectResult5, selectResult6, evaluatedValue7, evaluatedValue8);
        return createResult9;
    }

    private static VariadicParameterMode? ParseVariadicMode(string? value)
    {
        VariadicParameterMode? evaluatedValue10 = Enum.TryParse<VariadicParameterMode>(value, ignoreCase: true, out var mode) ? mode : null;
        return evaluatedValue10;
    }

    private static EnumParameterMetadata? MapEnumMetadata(EnumParameterMetadataDto? metadata)
    {
        EnumParameterMetadata? evaluatedValue12 = metadata is null ? null : resolveEnumParameterMetadata();
        return evaluatedValue12;
        EnumParameterMetadata resolveEnumParameterMetadata()
        {
            var selectResult11 = metadata.Options.Select(static option =>
            {
                var enumValueOption = new EnumValueOption(option.Name, option.NumericValue);
                return enumValueOption;
            });
            var enumParameterMetadata = new EnumParameterMetadata(metadata.TypeName, metadata.IsFlags, metadata.UnderlyingType, selectResult11);
            return enumParameterMetadata;
        }
    }

    private static ConnectionDefinition MapConnection(ConnectionDto dto)
    {
        if (dto.Kind == ConnectionKindDto.Execution)
        {
            if (dto.Branch is null)
            {
                throw new ArgumentException("Execution connections must declare Success, Failure, or Error. 流程连接必须声明 Success、Failure 或 Error 分支。", nameof(dto));
            }

            if (!Enum.IsDefined(dto.Branch.Value))
            {
                throw new ArgumentException("Execution connection branch is invalid. 流程连接分支无效。", nameof(dto));
            }

            var executionResult = ConnectionDefinition.Execution(dto.FromNodeId, dto.FromPortId, dto.ToNodeId, dto.ToPortId, (ExecutionBranch)dto.Branch.Value, dto.Priority, dto.Id);
            return executionResult;
        }

        var dataResult = ConnectionDefinition.Data(dto.FromNodeId, dto.FromPortId, dto.ToNodeId, dto.ToPortId, (DataSource)(dto.DataSource ?? DataSourceDto.Literal), dto.Priority, dto.Id);
        return dataResult;
    }
}

/// <summary>
/// Validates API flow definitions against domain rules.
/// 依据领域规则校验 API 流程定义。
/// </summary>
public static class FlowDefinitionContractValidator
{
    /// <summary>
    /// Validates a flow that is being saved by the editor. An empty entry node
    /// represents an intentionally blank editor draft and is not executable.
    /// 校验编辑器保存的流程。空入口节点表示刻意保留的空白编辑草稿，不能执行。
    /// </summary>
    public static FlowValidationResultDto ValidateForPersistence(FlowDefinitionDto definition)
    {
        var evaluatedValue13 = !string.IsNullOrWhiteSpace(definition.EntryNodeId);
        var validateCoreResult = ValidateCore(definition, requireExecutionPlan: evaluatedValue13);
        return validateCoreResult;
    }

    /// <summary>
    /// Validates a flow that is about to enter the runtime. Runtime validation
    /// always requires an entry node and a complete executable plan.
    /// 校验即将进入运行时的流程。运行校验始终要求入口节点和完整的可执行计划。
    /// </summary>
    public static FlowValidationResultDto ValidateForExecution(FlowDefinitionDto definition)
    {
        var validateCoreResult14 = ValidateCore(definition, requireExecutionPlan: true);
        return validateCoreResult14;
    }

    // Preserve the existing strict behavior for callers that have not yet
    // opted into one of the explicit validation contexts.
    // 对尚未迁移到明确校验上下文的调用方，保持原有的严格行为。
    public static FlowValidationResultDto Validate(FlowDefinitionDto definition)
    {
        var validateForExecutionResult = ValidateForExecution(definition);
        return validateForExecutionResult;
    }

    private static FlowValidationResultDto ValidateCore(FlowDefinitionDto definition, bool requireExecutionPlan)
    {
        try
        {
            if (definition.RunPolicy is null)
            {
                IReadOnlyList<ValidationDiagnosticDto> evaluatedValue15 = [new ValidationDiagnosticDto(FlowErrorCodes.RunPolicyMissing, "The flow run policy is required. 流程运行策略不能为空。", "runPolicy")];
                var flowValidationResultDto = new FlowValidationResultDto(false, evaluatedValue15);
                return flowValidationResultDto;
            }

            if (!Enum.IsDefined(definition.RunPolicy.ConcurrencyMode))
            {
                IReadOnlyList<ValidationDiagnosticDto> evaluatedValue16 = [new ValidationDiagnosticDto(FlowErrorCodes.RunPolicyInvalid, "The flow run policy is invalid. 流程运行策略无效。", "runPolicy.concurrencyMode")];
                var flowValidationResultDto17 = new FlowValidationResultDto(false, evaluatedValue16);
                return flowValidationResultDto17;
            }

            var domain = FlowDefinitionContractMapper.Map(definition);
            var diagnostics = domain.Validate()
                .Select(static diagnostic =>
                {
                    var validationDiagnosticDto = new ValidationDiagnosticDto(diagnostic.Code, diagnostic.Message, diagnostic.Path);
                    return validationDiagnosticDto;
                })
                .ToList();
            if (diagnostics.Count == 0 && requireExecutionPlan)
            {
                try
                {
                    var executionPlanBuilder = new SereinFlow.Runtime.ExecutionPlanBuilder();
                    _ = executionPlanBuilder.Build(domain);
                }
                catch (DomainValidationException exception)
                {
                    var selectResult19 = exception.Diagnostics.Select(static diagnostic =>
                    {
                        var validationDiagnosticDto18 = new ValidationDiagnosticDto(diagnostic.Code, diagnostic.Message, diagnostic.Path);
                        return validationDiagnosticDto18;
                    });
                    diagnostics.AddRange(selectResult19);
                }
            }

            var flowValidationResultDto21 = new FlowValidationResultDto(diagnostics.Count == 0, diagnostics);
            return flowValidationResultDto21;
        }
        catch (ArgumentException exception)
        {
            IReadOnlyList<ValidationDiagnosticDto> evaluatedValue22 = [new ValidationDiagnosticDto(FlowErrorCodes.Invalid, $"Flow definition is invalid. 流程定义无效。 {exception.Message}", null)];
            var flowValidationResultDto23 = new FlowValidationResultDto(false, evaluatedValue22);
            return flowValidationResultDto23;
        }
    }
}

/// <summary>
/// Normalizes flow definitions before validation and persistence.
/// 在校验和持久化前规范化流程定义。
/// </summary>
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
        FlowDefinitionDto evaluatedValue24 = string.IsNullOrWhiteSpace(definition.EntryNodeId) ? definition : NormalizeForExecution(definition);
        return evaluatedValue24;
    }

    public static FlowDefinitionDto Normalize(FlowDefinitionDto definition)
    {
        var normalizeForExecutionResult = NormalizeForExecution(definition);
        return normalizeForExecutionResult;
    }

    public static FlowDefinitionDto NormalizeForExecution(FlowDefinitionDto definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var domain = FlowDefinitionContractMapper.Map(definition);
        var executionPlanBuilder26 = new SereinFlow.Runtime.ExecutionPlanBuilder();
        var plan = executionPlanBuilder26.Build(domain);
        var normalizedCanvases = definition.Canvases
            .Select(canvas =>
            {
                var evaluatedValue25 = canvas with
                {
                    Nodes = resolveToArrayResult9(),
                };
                return evaluatedValue25;
                IReadOnlyList<NodeDto> resolveToArrayResult9()
                {
                    var toArrayResult11 = canvas.Nodes
                        .Select(node =>
                        {
                            if (node.Script is not null)
                            {
                                node = node with
                                {
                                    Script = node.Script with
                                    {
                                        SourceHash = plan.Nodes[node.Id].Script!.SourceHash,
                                        Inputs = resolveToArrayResult(),
                                    },
                                };
                                IReadOnlyList<ScriptValueContractDto> resolveToArrayResult()
                                {
                                    var toArrayResult = node.Parameters
                                        .Select(parameter =>
                                        {
                                            var scriptValueContractDto = new ScriptValueContractDto(parameter.Name, parameter.Ui?.ValueKind ?? "System.Object", parameter.Required, parameter.Ui?.Id, parameter.Ui?.Description);
                                            return scriptValueContractDto;
                                        })
                                        .ToArray();
                                    return toArrayResult;
                                }
                            }

                            if (!plan.Nodes.TryGetValue(node.Id, out var runtimeNode) || runtimeNode.Type != NodeType.FlowCall || runtimeNode.Runtime is null || node.Ui is null)
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
                        })
                        .ToArray();
                    return toArrayResult11;
                }
            })
            .ToArray();
        var normalized = definition with
        {
            Canvases = normalizedCanvases
        };
        var evaluatedValue28 = normalized with
        {
            Checksum = FlowDiffService.GetChecksum(normalized)
        };
        return evaluatedValue28;
    }
}

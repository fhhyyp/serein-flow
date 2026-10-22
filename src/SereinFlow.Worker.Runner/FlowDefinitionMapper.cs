using System;
using System.Collections.Generic;
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
        var jsonStringEnumConverter = new JsonStringEnumConverter();
        options.Converters.Add(jsonStringEnumConverter);
    });
    public static FlowDefinition Map(string definitionJson)
    {
        var dto = JsonSerializer.Deserialize<FlowDefinitionDto>(definitionJson, Options) ?? throw new InvalidOperationException("Flow definition payload is empty. 流程定义载荷为空。");
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
            var nodeParameterDefinition = new NodeParameterDefinition(parameter.Name, parameter.ValueJson, (DataSource)parameter.Source, parameter.Required, parameter.Ui?.Id, parameter.Ui?.ProjectInputKey, parameter.Ui?.Expression, parameter.Ui?.SourceNodeId, parameter.Ui?.SourcePortId, parameter.Ui?.ValueKind, parameter.Ui?.Description, parameter.Ui?.IsVariadic ?? false, parameter.Ui?.VariadicGroupId, parameter.Ui?.ElementType, parseVariadicModeResult);
            return nodeParameterDefinition;
        });
        ScriptNodeDefinition? evaluatedValue10 = dto.Script is null ? null : resolveCreateResult();
        NodeRuntimeDefinition conditionalValue;
        if (dto.Ui is null)
        {
            conditionalValue = null;
        }
        else
        {
            Guid? evaluatedValue1 = Guid.TryParse(dto.Ui.TargetFlowId, out var targetFlowId) ? targetFlowId : null;
            var optionalReceiver = dto.Ui.FlowCallParameterBindings;
            IReadOnlyList<FlowCallParameterBinding>? optionalResult = default(IReadOnlyList<FlowCallParameterBinding>);
            if (optionalReceiver is not null)
            {
                var selectResult3 = optionalReceiver.Select(item =>
                {
                    var flowCallParameterBinding = new FlowCallParameterBinding(item.CallParameterId, item.TargetParameterId);
                    return flowCallParameterBinding;
                });
                optionalResult = selectResult3.ToArray();
            }

            var evaluatedValue4 = optionalResult;
            conditionalValue = new NodeRuntimeDefinition(dto.Ui.LibraryId, dto.Ui.ClassName, dto.Ui.MethodName, dto.Ui.DllName, dto.Ui.DllVersion, dto.Ui.ReturnType, dto.Ui.TargetNodeId, evaluatedValue1, dto.Ui.IsAwaitable ?? false, dto.Ui.StaticReturnType, dto.Ui.IsDynamicReturnType ?? false, dto.Ui.TargetCanvasId, dto.Ui.IsPublic ?? false, evaluatedValue4, dto.Ui.LibraryNodeContractId);
        }

        NodeRuntimeDefinition? evaluatedValue11 = conditionalValue;
        var createResult12 = NodeDefinition.Create(dto.Id, (NodeType)dto.Type, dto.DisplayName, nodePosition, selectResult5, selectResult6, evaluatedValue10, evaluatedValue11);
        return createResult12;
        ScriptNodeDefinition resolveCreateResult()
        {
            var selectResult7 = dto.Script.Inputs.Select(input =>
            {
                var scriptValueContract = new ScriptValueContract(input.Name, input.ValueKind, input.Required, input.Id, input.Description);
                return scriptValueContract;
            });
            var selectResult8 = dto.Script.Outputs.Select(output =>
            {
                var scriptValueContract4 = new ScriptValueContract(output.Name, output.ValueKind, output.Required, output.Id, output.Description);
                return scriptValueContract4;
            });
            var createResult9 = ScriptNodeDefinition.Create(dto.Script.NodeId, dto.Script.Source, dto.Script.LanguageVersion, // SourceHash is presentation/cache metadata. The Worker derives
 // it from the immutable source instead of rejecting older DTOs.
            // SourceHash 是展示与缓存元数据；Worker 根据不可变源码推导该值，不因此拒绝旧版 DTO。
            null, selectResult7, selectResult8);
            return createResult9;
        }
    }

    private static VariadicParameterMode? ParseVariadicMode(string? value)
    {
        VariadicParameterMode? evaluatedValue13 = Enum.TryParse<VariadicParameterMode>(value, ignoreCase: true, out var mode) ? mode : null;
        return evaluatedValue13;
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

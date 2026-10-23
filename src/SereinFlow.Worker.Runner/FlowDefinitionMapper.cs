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
        var canvases = dto.Canvases.Select(MapCanvas);
        FlowRunPolicy? runPolicy = dto.RunPolicy is null ? null : new FlowRunPolicy((FlowConcurrencyMode)dto.RunPolicy.ConcurrencyMode);
        return FlowDefinition.Create(dto.Id, dto.Version, canvases, dto.EntryNodeId, dto.SchemaVersion, dto.Checksum, runPolicy);
    }

    private static CanvasDefinition MapCanvas(CanvasDto dto)
    {
        var nodes = dto.Nodes.Select(MapNode);
        var connections = dto.Connections.Select(MapConnection);
        return CanvasDefinition.Create(dto.Id, (CanvasLifecycle)dto.Lifecycle, nodes, connections);
    }

    private static NodeDefinition MapNode(NodeDto dto)
    {
        var nodePosition = new NodePosition(dto.X, dto.Y);
        var ports = dto.Ports.Select(port =>
        {
            var direction = Enum.Parse<PortDirection>(port.Direction, true);
            return new PortDefinition(port.Id, port.Name, direction, port.Required);
        });
        var parameters = dto.Parameters.Select(parameter =>
        {
            var variadicMode = ParseVariadicMode(parameter.Ui?.VariadicMode);
            return new NodeParameterDefinition(parameter.Name, parameter.ValueJson, (DataSource)parameter.Source, parameter.Required, parameter.Ui?.Id, parameter.Ui?.ProjectInputKey, parameter.Ui?.Expression, parameter.Ui?.SourceNodeId, parameter.Ui?.SourcePortId, parameter.Ui?.ValueKind, parameter.Ui?.Description, parameter.Ui?.IsVariadic ?? false, parameter.Ui?.VariadicGroupId, parameter.Ui?.ElementType, variadicMode);
        });
        ScriptNodeDefinition? script = dto.Script is null ? null : MapScript();
        NodeRuntimeDefinition? runtime;
        if (dto.Ui is null)
        {
            runtime = null;
        }
        else
        {
            Guid? parsedTargetFlowId = Guid.TryParse(dto.Ui.TargetFlowId, out var targetFlowId) ? targetFlowId : null;
            var bindingDtos = dto.Ui.FlowCallParameterBindings;
            IReadOnlyList<FlowCallParameterBinding>? bindings = null;
            if (bindingDtos is not null)
            {
                bindings = bindingDtos.Select(item =>
                {
                    return new FlowCallParameterBinding(item.CallParameterId, item.TargetParameterId);
                }).ToArray();
            }

            runtime = new NodeRuntimeDefinition(dto.Ui.LibraryId, dto.Ui.ClassName, dto.Ui.MethodName, dto.Ui.DllName, dto.Ui.DllVersion, dto.Ui.ReturnType, dto.Ui.TargetNodeId, parsedTargetFlowId, dto.Ui.IsAwaitable ?? false, dto.Ui.StaticReturnType, dto.Ui.IsDynamicReturnType ?? false, dto.Ui.TargetCanvasId, dto.Ui.IsPublic ?? false, bindings, dto.Ui.LibraryNodeContractId);
        }

        return NodeDefinition.Create(dto.Id, (NodeType)dto.Type, dto.DisplayName, nodePosition, ports, parameters, script, runtime);
        ScriptNodeDefinition MapScript()
        {
            var inputs = dto.Script.Inputs.Select(input =>
            {
                return new ScriptValueContract(input.Name, input.ValueKind, input.Required, input.Id, input.Description);
            });
            var outputs = dto.Script.Outputs.Select(output =>
            {
                return new ScriptValueContract(output.Name, output.ValueKind, output.Required, output.Id, output.Description);
            });
            var scriptNodeDefinition = ScriptNodeDefinition.Create(dto.Script.NodeId, dto.Script.Source, dto.Script.LanguageVersion, // SourceHash is presentation/cache metadata. The Worker derives
                                                                                                                                     // it from the immutable source instead of rejecting older DTOs.
                                                                                                                                     // SourceHash 是展示与缓存元数据；Worker 根据不可变源码推导该值，不因此拒绝旧版 DTO。
            null, inputs, outputs);
            return scriptNodeDefinition;
        }
    }

    private static VariadicParameterMode? ParseVariadicMode(string? value)
    {
        return Enum.TryParse<VariadicParameterMode>(value, ignoreCase: true, out var mode) ? mode : null;
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
            return ConnectionDefinition.Execution(dto.FromNodeId, dto.FromPortId, dto.ToNodeId, dto.ToPortId, (ExecutionBranch)dto.Branch.Value, dto.Priority, dto.Id);
        }
        return ConnectionDefinition.Data(dto.FromNodeId, dto.FromPortId, dto.ToNodeId, dto.ToPortId, (DataSource)(dto.DataSource ?? DataSourceDto.Literal), dto.Priority, dto.Id);
    }
}

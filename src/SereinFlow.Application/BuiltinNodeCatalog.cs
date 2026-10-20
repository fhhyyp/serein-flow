using System.Collections.Generic;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application;
/// <summary>
/// Server-owned definitions for nodes that do not originate from an uploaded
/// library. The editor receives descriptors only; it never owns a hidden
/// local set of node templates.
/// 不来自上传类库的基础节点由服务端统一定义。编辑器只接收描述符，不再维护隐藏的本地节点模板。
/// </summary>
public interface IBuiltinNodeCatalog
{
    BuiltinNodeCatalogDto GetCatalog();
}

public sealed class BuiltinNodeCatalog : IBuiltinNodeCatalog
{
    private const string ScriptSource = "return 0";
    private const string ScriptLanguageVersion = "0.1";
    public BuiltinNodeCatalogDto GetCatalog()
    {
        IReadOnlyList<NodeCreationDescriptorDto> evaluatedValue4 = [resolveNodeCreationDescriptorDto(), resolveNodeCreationDescriptorDto1()];
        BuiltinNodeCatalogDto evaluatedValue5 = new(evaluatedValue4);
        return evaluatedValue5;
        NodeCreationDescriptorDto resolveNodeCreationDescriptorDto()
        {
            var nodeUiMetadataDto = new NodeUiMetadataDto("script", "node.kind.script", "node.scriptSubtitle", null, "ready", true, 260, Category: "basic", ReturnType: "ScriptLang.Runtime.Value");
            var computeSourceHashResult = ScriptNodeDefinition.ComputeSourceHash(ScriptSource);
            IReadOnlyList<ScriptValueContractDto> evaluatedValue = [new ScriptValueContractDto("result", "ScriptLang.Runtime.Value", false, "result", "Script result. 脚本结果。")];
            var scriptNodeDataDto = new ScriptNodeDataDto("", ScriptSource, ScriptLanguageVersion, computeSourceHashResult, [], evaluatedValue);
            var nodeCreationDescriptorDto = new NodeCreationDescriptorDto("builtin:script", NodeTypeDto.Script, "Script", null, nodeUiMetadataDto, scriptNodeDataDto, []);
            return nodeCreationDescriptorDto;
        }

        NodeCreationDescriptorDto resolveNodeCreationDescriptorDto1()
        {
            var nodeUiMetadataDto2 = new NodeUiMetadataDto("flowCall", "node.kind.flowCall", "node.flowCallSubtitle", null, "ready", true, 260, Category: "basic", ReturnType: "System.Object");
            var nodeCreationDescriptorDto3 = new NodeCreationDescriptorDto("builtin:flow-call", NodeTypeDto.FlowCall, "FlowCall", null, nodeUiMetadataDto2, null, []);
            return nodeCreationDescriptorDto3;
        }
    }
}

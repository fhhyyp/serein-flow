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
        IReadOnlyList<NodeCreationDescriptorDto> nodes = [CreateScriptDescriptor(), new NodeCreationDescriptorDto("builtin:flow-call", NodeTypeDto.FlowCall, "FlowCall", null, new NodeUiMetadataDto("flowCall", "node.kind.flowCall", "node.flowCallSubtitle", null, "ready", true, 260, Category: "basic", ReturnType: "System.Object"), null, [])];
        return new(nodes);
        NodeCreationDescriptorDto CreateScriptDescriptor()
        {
            var nodeUiMetadataDto = new NodeUiMetadataDto("script", "node.kind.script", "node.scriptSubtitle", null, "ready", true, 260, Category: "basic", ReturnType: "ScriptLang.Runtime.Value");
            var sourceHash = ScriptNodeDefinition.ComputeSourceHash(ScriptSource);
            IReadOnlyList<ScriptValueContractDto> outputs = [new ScriptValueContractDto("result", "ScriptLang.Runtime.Value", false, "result", "Script result. 脚本结果。")];
            var scriptNodeDataDto = new ScriptNodeDataDto("", ScriptSource, ScriptLanguageVersion, sourceHash, [], outputs);
            return new NodeCreationDescriptorDto("builtin:script", NodeTypeDto.Script, "Script", null, nodeUiMetadataDto, scriptNodeDataDto, []);
        }
    }
}

using Microsoft.AspNetCore.Http;
using SereinFlow.Contracts;

namespace SereinFlow.Application;
/// <summary>
/// Materializes the server-owned built-in node descriptors into complete
/// schema 2.0 patch nodes. The returned node can be copied into an addNode or
/// replaceNode operation without reconstructing ports or runtime metadata.
/// 将服务端内置节点描述符物化为完整的 Schema 2.0 补丁节点。返回节点可直接复制到
/// addNode 或 replaceNode 操作，无需客户端重建端口或运行时元数据。
/// </summary>
public sealed class BuiltinNodeTemplateService
{
    private readonly IBuiltinNodeCatalog _catalog;
    public BuiltinNodeTemplateService(IBuiltinNodeCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public BuiltinNodeTemplateDto Create(BuiltinNodeTemplateRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.BuiltinNodeId))
        {
            throw Invalid(McpErrorCodes.BuiltinNodeTemplateIdInvalid, "$.builtinNodeId", "a built-in node ID from the flow edit model", "Use a builtinNodes.nodes[].id value from sereinflow_get_flow_edit_model.");
        }

        if (!double.IsFinite(request.Position.X) || !double.IsFinite(request.Position.Y))
        {
            throw Invalid(McpErrorCodes.BuiltinNodeTemplatePositionInvalid, "$.position", "finite x and y values", "Provide a finite canvas position.");
        }

        var builtinNodeId = request.BuiltinNodeId.Trim();
        var evaluatedValue = _catalog.GetCatalog().Nodes;
        var descriptor = evaluatedValue.SingleOrDefault(item =>
        {
            var equalsResult = string.Equals(item.Id, builtinNodeId, StringComparison.Ordinal);
            return equalsResult;
        });
        if (descriptor is null)
        {
            throw Invalid(McpErrorCodes.BuiltinNodeTemplateNotFound, "$.builtinNodeId", "a built-in node ID from the flow edit model", "Read the current flow edit model and select a built-in node descriptor.", StatusCodes.Status404NotFound);
        }

        var nodeId = $"node-{Guid.NewGuid():N}";
        var parameters = descriptor.Parameters ?? [];
        var ports = new List<NodePortDto>
        {
            new("exec-in", "Execution input", "input", false),
            new("exec-success", "Success", "output", false),
            new("exec-failure", "Failure", "output", false),
            new("exec-error", "Error", "output", false)
        };
        if (descriptor.Ui.HasDataOutput)
        {
            var nodePortDto = new NodePortDto("data-out", "Data output", "output", false);
            ports.Add(nodePortDto);
        }

        var selectResult = parameters.Select(parameter =>
        {
            var parameterId = parameter.Ui?.Id ?? parameter.Name;
            var nodePortDto1 = new NodePortDto($"param-{parameterId}", parameterId, "input", parameter.Required);
            return nodePortDto1;
        });
        ports.AddRange(selectResult);
        var script = descriptor.Script is null ? null : descriptor.Script with
        {
            NodeId = nodeId
        };
        var node = new NodeDto(nodeId, descriptor.Type, descriptor.DisplayName, request.Position.X, request.Position.Y, ports, parameters, script, descriptor.Ui);
        var builtinNodeTemplateDto = new BuiltinNodeTemplateDto(node, "builtinCatalog", descriptor.Id);
        return builtinNodeTemplateDto;
    }

    private static BuiltinNodeTemplateException Invalid(string code, string fieldPath, string expected, string remediation, int statusCode = StatusCodes.Status400BadRequest)
    {
        global::SereinFlow.Application.BuiltinNodeTemplateException evaluatedValue2 = new(code, fieldPath, expected, remediation, statusCode);
        return evaluatedValue2;
    }
}

public sealed class BuiltinNodeTemplateException(string code, string fieldPath, string expected, string remediation, int statusCode) : Exception("The built-in node template request is invalid.")
{
    public string Code { get; } = code;
    public string FieldPath { get; } = fieldPath;
    public string Expected { get; } = expected;
    public string Remediation { get; } = remediation;
    public int StatusCode { get; } = statusCode;
}

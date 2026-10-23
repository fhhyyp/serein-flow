using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Application;
/// <summary>
/// Builds a new editor node from an already scanned and project-referenced
/// library contract. It never writes a flow, imports a package, or loads an
/// assembly into the process.
/// 根据已扫描且已被项目引用的类库契约构建编辑器节点；不写入流程、不导入包，也不将程序集加载到进程中。
/// </summary>
public sealed class LibraryNodeTemplateService
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateContractOptions();
    private readonly IProjectLibraryReferenceRepository _references;
    private readonly ILibraryCatalogService _libraries;
    public LibraryNodeTemplateService(IProjectLibraryReferenceRepository references, ILibraryCatalogService libraries)
    {
        _references = references ?? throw new ArgumentNullException(nameof(references));
        _libraries = libraries ?? throw new ArgumentNullException(nameof(libraries));
    }

    public async Task<LibraryNodeTemplateDto> CreateAsync(LibraryNodeTemplateRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProjectId == Guid.Empty)
        {
            throw Invalid(McpErrorCodes.LibraryNodeTemplateProjectInvalid, "$.projectId", "a non-empty project GUID", "Read the project before requesting a template. 请先读取项目，再请求模板。");
        }

        if (string.IsNullOrWhiteSpace(request.LibraryId))
        {
            throw Invalid(McpErrorCodes.LibraryNodeTemplateLibraryInvalid, "$.libraryId", "a non-empty library ID", "Use a library ID from the project edit model. 请使用项目编辑模型中的类库 ID。");
        }

        if (string.IsNullOrWhiteSpace(request.LibraryNodeContractId))
        {
            throw Invalid(McpErrorCodes.LibraryNodeTemplateContractInvalid, "$.libraryNodeContractId", "a non-empty contract ID", "Use a contract ID from the project edit model. 请使用项目编辑模型中的契约 ID。");
        }

        if (!double.IsFinite(request.Position.X) || !double.IsFinite(request.Position.Y))
        {
            throw Invalid(McpErrorCodes.LibraryNodeTemplatePositionInvalid, "$.position", "finite x and y values", "Provide a finite canvas position. 请提供有限数值的画布坐标。");
        }

        if (!await _references.IsReferencedAsync(request.ProjectId, request.LibraryId, cancellationToken))
        {
            throw Invalid(McpErrorCodes.LibraryNodeTemplateLibraryNotAttached, "$.libraryId", "a library attached to this project", "Attach the library through the separate preview/apply workflow before creating a node template. 请先通过独立的预览和应用流程将类库关联到项目，再创建节点模板。", StatusCodes.Status409Conflict);
        }

        var library = await _libraries.FindAsync(request.LibraryId, cancellationToken);
        if (library is null)
        {
            throw Invalid(McpErrorCodes.LibraryNodeTemplateLibraryNotFound, "$.libraryId", "an existing library artifact", "Read the project edit model and select an available library. 请读取项目编辑模型并选择可用类库。", StatusCodes.Status404NotFound);
        }

        var contract = library.Nodes.SingleOrDefault(node =>
        {
            var normalizedLibraryNodeContractId = request.LibraryNodeContractId.Trim();
            return string.Equals(node.ContractId ?? node.Id, normalizedLibraryNodeContractId, StringComparison.Ordinal);
        });
        if (contract is null)
        {
            throw Invalid(McpErrorCodes.LibraryNodeTemplateContractNotFound, "$.libraryNodeContractId", "a contract ID published by the selected library", "Read the library contract from the project edit model. 请从项目编辑模型读取类库契约。", StatusCodes.Status404NotFound);
        }

        if (contract.Type is not (NodeTypeDto.Action or NodeTypeDto.Flipflop))
        {
            throw Invalid(McpErrorCodes.LibraryNodeTemplateNodeTypeUnsupported, "$.libraryNodeContractId", "an action or flipflop library contract", "Only scanned method-node contracts can produce a library node template. 只有扫描得到的方法节点契约才能生成类库节点模板。", StatusCodes.Status422UnprocessableEntity);
        }

        var node = CreateNode(contract, request.Position);
        var contractRevision = GetContractRevision(contract);
        return new LibraryNodeTemplateDto(node, "libraryContract", library.Id, library.Version, library.Sha256, contractRevision);
    }

    public static string GetContractRevision(LibraryNodeDto contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var mappedParameters = contract.Parameters
            .Select(parameter => new { id = parameter.Id, name = parameter.Name, type = parameter.Type, description = parameter.Description, required = parameter.Required, isVariadic = parameter.IsVariadic, variadicGroupId = parameter.VariadicGroupId, elementType = parameter.ElementType, defaultValue = parameter.DefaultValue, aliases = parameter.Aliases, enumMetadata = parameter.EnumMetadata })
            .ToArray();
        var canonical = new
        {
            contractId = contract.ContractId ?? contract.Id,
            type = contract.Type,
            className = contract.ClassName,
            methodName = contract.MethodName,
            dllName = contract.DllName,
            dllVersion = contract.DllVersion,
            returnType = contract.ReturnType,
            isAwaitable = contract.IsAwaitable,
            flowLibraryName = contract.FlowLibraryName,
            parameters = mappedParameters
        };
        var canonicalJson = JsonSerializer.Serialize(canonical, JsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static NodeDto CreateNode(LibraryNodeDto contract, NodeTemplatePositionDto position)
    {
        var parameters = contract.Parameters
            .Select(CreateParameter)
            .ToArray();
        var ports = new List<NodePortDto>
        {
            new("exec-in", "Execution input", "input", false),
            new("exec-success", "Success", "output", false),
            new("exec-failure", "Failure", "output", false),
            new("exec-error", "Error", "output", false)
        };
        if (!string.Equals(contract.ReturnType.Trim(), "System.Void", StringComparison.OrdinalIgnoreCase))
        {
            var nodePortDto = new NodePortDto("data-out", "Data output", "output", false);
            ports.Add(nodePortDto);
        }
        ports.AddRange(contract.Parameters.Select(parameter =>
        {
            var id = $"param-{CanonicalParameterId(parameter.Id)}";
            var name = CanonicalParameterId(parameter.Id);
            return new NodePortDto(id, name, "input", false);
        }));
        var kind = contract.Type == NodeTypeDto.Action ? "action" : "flipflop";
        var newNodeId = $"node-{Guid.NewGuid():N}";
        var hasDataOutput = !string.Equals(contract.ReturnType.Trim(), "System.Void", StringComparison.OrdinalIgnoreCase);
        var nodeUiMetadataDto = new NodeUiMetadataDto(kind, "node.catalogMethod", "node.catalogSubtitle", contract.Description ?? $"{contract.ClassName}.{contract.MethodName}", "ready", hasDataOutput, 260, Category: "method", LibraryId: contract.LibraryId, ClassName: contract.ClassName, MethodName: contract.MethodName, DllName: contract.DllName, DllVersion: contract.DllVersion, ReturnType: contract.ReturnType, IsAwaitable: contract.IsAwaitable, LibraryNodeContractId: contract.ContractId ?? contract.Id, FlowLibraryName: contract.FlowLibraryName);
        return new NodeDto(newNodeId, contract.Type, contract.DisplayName, position.X, position.Y, ports, parameters, null, nodeUiMetadataDto);
    }

    private static NodeParameterDto CreateParameter(LibraryParameterDto parameter)
    {
        var id = CanonicalParameterId(parameter.Id);
        string? variadicGroupId = parameter.IsVariadic ? CanonicalParameterId(parameter.VariadicGroupId ?? parameter.Id) : null;
        var nodeParameterUiMetadataDto = new NodeParameterUiMetadataDto(id, parameter.Name, parameter.Type, parameter.DefaultValue, null, null, null, null, Type: parameter.Type, Description: parameter.Description, InputMode: parameter.EnumMetadata is null ? "manual" : "select", IsVariadic: parameter.IsVariadic, VariadicGroupId: variadicGroupId, ElementType: parameter.ElementType, VariadicMode: parameter.IsVariadic ? "expanded" : null, EnumMetadata: parameter.EnumMetadata);
        return new(parameter.Name, parameter.DefaultValue, DataSourceDto.Literal, parameter.Required, nodeParameterUiMetadataDto);
    }

    private static string CanonicalParameterId(string parameterId)
    {
        var trimmed = parameterId.Trim();
        return trimmed.StartsWith("param-", StringComparison.Ordinal) ? trimmed["param-".Length..] : trimmed;
    }

    private static LibraryNodeTemplateException Invalid(string code, string fieldPath, string expected, string remediation, int statusCode = StatusCodes.Status400BadRequest)
    {
        return new(code, fieldPath, expected, remediation, statusCode);
    }
}

public sealed class LibraryNodeTemplateException(string code, string fieldPath, string expected, string remediation, int statusCode) : Exception("The library node template request is invalid. 类库节点模板请求无效。")
{
    public string Code { get; } = code;
    public string FieldPath { get; } = fieldPath;
    public string Expected { get; } = expected;
    public string Remediation { get; } = remediation;
    public int StatusCode { get; } = statusCode;
    public string DiagnosticId { get; } = CreateDiagnosticIdInitialValue();

    private static string CreateDiagnosticIdInitialValue()
    {
        return Guid.NewGuid().ToString("N");
    }
}

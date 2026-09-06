using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Application;

/// <summary>
/// Builds a new editor node from an already scanned and project-referenced
/// library contract. It never writes a flow, imports a package, or loads an
/// assembly into the process.
/// </summary>
public sealed class LibraryNodeTemplateService
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateContractOptions();

    private readonly IProjectLibraryReferenceRepository _references;
    private readonly ILibraryCatalogService _libraries;

    public LibraryNodeTemplateService(
        IProjectLibraryReferenceRepository references,
        ILibraryCatalogService libraries)
    {
        _references = references ?? throw new ArgumentNullException(nameof(references));
        _libraries = libraries ?? throw new ArgumentNullException(nameof(libraries));
    }

    public async Task<LibraryNodeTemplateDto> CreateAsync(
        LibraryNodeTemplateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProjectId == Guid.Empty)
            throw Invalid(McpErrorCodes.LibraryNodeTemplateProjectInvalid, "$.projectId", "a non-empty project GUID", "Read the project before requesting a template.");
        if (string.IsNullOrWhiteSpace(request.LibraryId))
            throw Invalid(McpErrorCodes.LibraryNodeTemplateLibraryInvalid, "$.libraryId", "a non-empty library ID", "Use a library ID from the project edit model.");
        if (string.IsNullOrWhiteSpace(request.LibraryNodeContractId))
            throw Invalid(McpErrorCodes.LibraryNodeTemplateContractInvalid, "$.libraryNodeContractId", "a non-empty contract ID", "Use a contract ID from the project edit model.");
        if (!double.IsFinite(request.Position.X) || !double.IsFinite(request.Position.Y))
            throw Invalid(McpErrorCodes.LibraryNodeTemplatePositionInvalid, "$.position", "finite x and y values", "Provide a finite canvas position.");

        if (!await _references.IsReferencedAsync(request.ProjectId, request.LibraryId, cancellationToken))
        {
            throw Invalid(
                McpErrorCodes.LibraryNodeTemplateLibraryNotAttached,
                "$.libraryId",
                "a library attached to this project",
                "Attach the library through the separate preview/apply workflow before creating a node template.",
                409);
        }

        var library = await _libraries.FindAsync(request.LibraryId, cancellationToken);
        if (library is null)
        {
            throw Invalid(
                McpErrorCodes.LibraryNodeTemplateLibraryNotFound,
                "$.libraryId",
                "an existing library artifact",
                "Read the project edit model and select an available library.",
                404);
        }

        var contract = library.Nodes.SingleOrDefault(node => string.Equals(
            node.ContractId ?? node.Id,
            request.LibraryNodeContractId.Trim(),
            StringComparison.Ordinal));
        if (contract is null)
        {
            throw Invalid(
                McpErrorCodes.LibraryNodeTemplateContractNotFound,
                "$.libraryNodeContractId",
                "a contract ID published by the selected library",
                "Read the library contract from the project edit model.",
                404);
        }

        if (contract.Type is not (NodeTypeDto.Action or NodeTypeDto.Flipflop))
        {
            throw Invalid(
                McpErrorCodes.LibraryNodeTemplateNodeTypeUnsupported,
                "$.libraryNodeContractId",
                "an action or flipflop library contract",
                "Only scanned method-node contracts can produce a library node template.",
                422);
        }

        var node = CreateNode(contract, request.Position);
        return new LibraryNodeTemplateDto(
            node,
            "libraryContract",
            library.Id,
            library.Version,
            library.Sha256,
            GetContractRevision(contract));
    }

    public static string GetContractRevision(LibraryNodeDto contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
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
            parameters = contract.Parameters.Select(parameter => new
            {
                id = parameter.Id,
                name = parameter.Name,
                type = parameter.Type,
                description = parameter.Description,
                required = parameter.Required,
                isVariadic = parameter.IsVariadic,
                variadicGroupId = parameter.VariadicGroupId,
                elementType = parameter.ElementType,
                defaultValue = parameter.DefaultValue,
                aliases = parameter.Aliases,
                enumMetadata = parameter.EnumMetadata
            }).ToArray()
        };
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical, JsonOptions)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static NodeDto CreateNode(LibraryNodeDto contract, NodeTemplatePositionDto position)
    {
        var parameters = contract.Parameters.Select(CreateParameter).ToArray();
        var ports = new List<NodePortDto>
        {
            new("exec-in", "Execution input", "input", false),
            new("exec-success", "Success", "output", false),
            new("exec-failure", "Failure", "output", false),
            new("exec-error", "Error", "output", false)
        };
        if (!string.Equals(contract.ReturnType.Trim(), "System.Void", StringComparison.OrdinalIgnoreCase))
            ports.Add(new NodePortDto("data-out", "Data output", "output", false));
        ports.AddRange(contract.Parameters.Select(parameter => new NodePortDto(
            $"param-{CanonicalParameterId(parameter.Id)}",
            CanonicalParameterId(parameter.Id),
            "input",
            false)));

        var kind = contract.Type == NodeTypeDto.Action ? "action" : "flipflop";
        return new NodeDto(
            $"node-{Guid.NewGuid():N}",
            contract.Type,
            contract.DisplayName,
            position.X,
            position.Y,
            ports,
            parameters,
            null,
            new NodeUiMetadataDto(
                kind,
                "node.catalogMethod",
                "node.catalogSubtitle",
                contract.Description ?? $"{contract.ClassName}.{contract.MethodName}",
                "ready",
                !string.Equals(contract.ReturnType.Trim(), "System.Void", StringComparison.OrdinalIgnoreCase),
                260,
                Category: "method",
                LibraryId: contract.LibraryId,
                ClassName: contract.ClassName,
                MethodName: contract.MethodName,
                DllName: contract.DllName,
                DllVersion: contract.DllVersion,
                ReturnType: contract.ReturnType,
                IsAwaitable: contract.IsAwaitable,
                LibraryNodeContractId: contract.ContractId ?? contract.Id,
                FlowLibraryName: contract.FlowLibraryName));
    }

    private static NodeParameterDto CreateParameter(LibraryParameterDto parameter)
        => new(
            parameter.Name,
            parameter.DefaultValue,
            DataSourceDto.Literal,
            parameter.Required,
            new NodeParameterUiMetadataDto(
                CanonicalParameterId(parameter.Id),
                parameter.Name,
                parameter.Type,
                parameter.DefaultValue,
                null,
                null,
                null,
                null,
                Type: parameter.Type,
                Description: parameter.Description,
                InputMode: parameter.EnumMetadata is null ? "manual" : "select",
                IsVariadic: parameter.IsVariadic,
                VariadicGroupId: parameter.IsVariadic ? CanonicalParameterId(parameter.VariadicGroupId ?? parameter.Id) : null,
                ElementType: parameter.ElementType,
                VariadicMode: parameter.IsVariadic ? "expanded" : null,
                EnumMetadata: parameter.EnumMetadata));

    private static string CanonicalParameterId(string parameterId)
    {
        var trimmed = parameterId.Trim();
        return trimmed.StartsWith("param-", StringComparison.Ordinal)
            ? trimmed["param-".Length..]
            : trimmed;
    }

    private static LibraryNodeTemplateException Invalid(
        string code,
        string fieldPath,
        string expected,
        string remediation,
        int statusCode = 400)
        => new(code, fieldPath, expected, remediation, statusCode);
}

public sealed class LibraryNodeTemplateException(
    string code,
    string fieldPath,
    string expected,
    string remediation,
    int statusCode) : Exception("The library node template request is invalid.")
{
    public string Code { get; } = code;
    public string FieldPath { get; } = fieldPath;
    public string Expected { get; } = expected;
    public string Remediation { get; } = remediation;
    public int StatusCode { get; } = statusCode;
    public string DiagnosticId { get; } = Guid.NewGuid().ToString("N");
}

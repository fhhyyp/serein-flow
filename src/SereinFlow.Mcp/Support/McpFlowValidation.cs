using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Mcp.Support;

internal sealed class McpFlowValidation(ProjectLibraryService _projectLibraryService)
{
    internal async Task<FlowValidationResultDto> ValidateExecutableFlowAsync(
        Guid projectId,
        FlowDefinitionDto definition,
        CancellationToken cancellationToken)
    {
        var validation = FlowDefinitionContractValidator.ValidateForExecution(definition);
        var libraryValidation = await _projectLibraryService.ValidateFlowLibrariesAsync(projectId, definition, cancellationToken);
        var diagnostics = validation.Diagnostics.Concat(libraryValidation.Diagnostics).ToArray();
        return new FlowValidationResultDto(validation.IsValid && libraryValidation.IsValid, diagnostics);
    }
}

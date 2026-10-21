using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Mcp.Integration;
using static SereinFlow.Mcp.Tools.McpToolSupport;

namespace SereinFlow.Mcp.Tools;
/// <summary>
/// Project lifecycle MCP tools, including their preview payload projection.
/// </summary>
internal static class McpProjectToolHandlers
{
    [SereinFlowMcpTool("sereinflow_preview_create_project", McpPermissionDto.ProjectWrite, ExecutionKind = McpToolExecutionKind.Mutation)]
    internal static Task<object> PreviewCreateAsync(SereinFlowMcpToolContext context, CreateProjectMcpRequestDto arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult = context.RequirePrincipal();
        var previewCreateProjectAsyncResult = PreviewCreateProjectAsync(context.Scope, requirePrincipalResult, arguments, cancellationToken);
        return previewCreateProjectAsyncResult;
    }

    [SereinFlowMcpTool("sereinflow_apply_create_project", McpPermissionDto.ProjectWrite, ExecutionKind = McpToolExecutionKind.Mutation, RequiresIdempotencyKey = true)]
    internal static Task<object> ApplyCreateAsync(SereinFlowMcpToolContext context, McpMutationApplyRequestDto arguments, CancellationToken cancellationToken)
    {
        var requirePrincipalResult1 = context.RequirePrincipal();
        var applyCreateProjectAsyncResult = ApplyCreateProjectAsync(context.Scope, requirePrincipalResult1, arguments, cancellationToken);
        return applyCreateProjectAsyncResult;
    }

    internal static ProjectCreatePreviewDto ReadProjectCreatePreview(McpPreviewEntry entry, McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredProjectCreatePreview>(entry);
        var project = RehydrateProject(stored);
        var candidate = new ProjectCreationCandidate(project, stored.Definition, stored.Validation);
        var toProjectCreatePreviewResult = ToProjectCreatePreview(descriptor, candidate);
        return toProjectCreatePreviewResult;
    }

    private static async Task<object> PreviewCreateProjectAsync(IServiceScope scope, McpPrincipal principal, CreateProjectMcpRequestDto arguments, CancellationToken cancellationToken)
    {
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        security.RequireAdministrator(principal);
        var request = arguments;
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "MCP parameter 'name' is required.");
        }

        ProjectCreationCandidate candidate;
        try
        {
            var getRequiredServiceResult = scope.ServiceProvider.GetRequiredService<ProjectCreationService>();
            candidate = getRequiredServiceResult.PrepareEmpty(request.Name, request.FlowName);
        }
        catch (ArgumentException exception)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, exception.Message);
        }

        var getFlowNameResult = GetFlowName(candidate.Definition);
        var createProjectMcpRequestDto = new CreateProjectMcpRequestDto(candidate.Project.Name, getFlowNameResult);
        var stored = new StoredProjectCreatePreview(createProjectMcpRequestDto, candidate.Project.Id, candidate.Definition.Id, candidate.Project.CreatedAt, candidate.Definition, candidate.Validation);
        var getRequiredServiceResult2 = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var preview = await getRequiredServiceResult2.CreateAsync("project.create", principal, candidate.Project.Id, candidate.Definition.Id, expectedVersion: null, stored, cancellationToken);
        var mcpPreviewDescriptorDto = new McpPreviewDescriptorDto(preview.Id, preview.Operation, preview.ProjectId, preview.FlowId, preview.Status, preview.ExpiresAt, preview.PreviewFingerprint);
        var toProjectCreatePreviewResult3 = ToProjectCreatePreview(mcpPreviewDescriptorDto, candidate);
        return toProjectCreatePreviewResult3;
    }

    private static async Task<object> ApplyCreateProjectAsync(IServiceScope scope, McpPrincipal principal, McpMutationApplyRequestDto arguments, CancellationToken cancellationToken)
    {
        var request = arguments;
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(principal.Id, "project.create", request.IdempotencyKey, requestPayload, cancellationToken);
        if (replay is not null)
        {
            var deserializeStoredResponseResult = DeserializeStoredResponse(replay.ResponseJson);
            return deserializeStoredResponseResult;
        }

        var previews = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await previews.RequireAsync(request.PreviewId, request.PreviewFingerprint, principal, cancellationToken);
        if (!string.Equals(entry.Operation, "project.create", StringComparison.Ordinal))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "The preview does not describe project creation.");
        }

        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        security.RequireAdministrator(principal);
        security.Require(principal, McpPermissionDto.ProjectWrite);
        var stored = McpPreviewService.Deserialize<StoredProjectCreatePreview>(entry);
        var project = RehydrateProject(stored);
        var getRequiredServiceResult4 = scope.ServiceProvider.GetRequiredService<ProjectCreationService>();
        var candidate = getRequiredServiceResult4.Prepare(project, stored.Definition);
        if (!candidate.Validation.IsValid)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "The project creation preview is no longer valid.", new { code = McpErrorCodes.ValidationFailed, diagnostics = candidate.Validation.Diagnostics });
        }

        var getRequiredServiceResult5 = scope.ServiceProvider.GetRequiredService<ProjectCreationService>();
        var result = await getRequiredServiceResult5.CreateAsync(candidate, cancellationToken);
        if (result.Status == ProjectCreationStatus.Conflict)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.Conflict, result.ErrorMessage ?? "The project already exists.", new { code = result.ErrorCode });
        }

        if (result.Status != ProjectCreationStatus.Created)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.OperationRejected, "The project creation preview cannot be applied.");
        }

        var response = CreateWorkspace(candidate);
        await MarkPreviewAppliedAsync(previews, entry, cancellationToken);
        await idempotency.SaveAsync(principal.Id, entry.Operation, request.IdempotencyKey, response, requestPayload, cancellationToken);
        return response;
    }

    private static Project RehydrateProject(StoredProjectCreatePreview stored)
    {
        var rehydrateResult = Project.Rehydrate(stored.ProjectId, stored.Request.Name, version: 1, SereinFlow.Domain.ProjectStatus.Draft, stored.CreatedAt, stored.CreatedAt);
        return rehydrateResult;
    }

    private static ProjectCreatePreviewDto ToProjectCreatePreview(McpPreviewDescriptorDto descriptor, ProjectCreationCandidate candidate)
    {
        var getFlowNameResult6 = GetFlowName(candidate.Definition);
        var createWorkspaceResult = CreateWorkspace(candidate);
        ProjectCreatePreviewDto evaluatedValue = new(descriptor.PreviewId, candidate.Project.Id, candidate.Definition.Id, candidate.Project.Name, getFlowNameResult6, descriptor.ExpiresAt, descriptor.PreviewFingerprint, descriptor.Status == McpMutationPreviewStatusDto.Pending && descriptor.ExpiresAt > DateTimeOffset.UtcNow && candidate.Validation.IsValid, candidate.Validation, createWorkspaceResult);
        return evaluatedValue;
    }

    private static ProjectWorkspaceDto CreateWorkspace(ProjectCreationCandidate candidate)
    {
        var toProjectStatusValueResult = ToProjectStatusValue(candidate.Project.Status);
        var projectDto = new ProjectDto(candidate.Project.Id, candidate.Project.Name, candidate.Project.Version, toProjectStatusValueResult, candidate.Project.CreatedAt, candidate.Project.UpdatedAt);
        IReadOnlyList<FlowDefinitionSummaryDto> evaluatedValue7 = [resolveFlowDefinitionSummaryDto()];
        ProjectWorkspaceDto evaluatedValue8 = new(projectDto, evaluatedValue7);
        return evaluatedValue8;
        FlowDefinitionSummaryDto resolveFlowDefinitionSummaryDto()
        {
            var sumResult = candidate.Definition.Canvases.Sum(static canvas => canvas.Nodes.Count);
            var flowDefinitionSummaryDto = new FlowDefinitionSummaryDto(candidate.Definition.Id, candidate.Definition.Version, candidate.Definition.EntryNodeId, candidate.Definition.Canvases.Count, sumResult);
            return flowDefinitionSummaryDto;
        }
    }

    private static string GetFlowName(FlowDefinitionDto definition)
    {
        var evaluatedValue9 = definition.Canvases.FirstOrDefault()?.Name ?? "Main";
        return evaluatedValue9;
    }

    private static string ToProjectStatusValue(ProjectStatus status)
    {
        string evaluatedValue10 = status switch
        {
            SereinFlow.Domain.ProjectStatus.Draft => "draft",
            SereinFlow.Domain.ProjectStatus.Ready => "ready",
            SereinFlow.Domain.ProjectStatus.ScriptInvalid => "scriptInvalid",
            SereinFlow.Domain.ProjectStatus.Archived => "archived",
            _ => status.ToString()};
        return evaluatedValue10;
    }
}

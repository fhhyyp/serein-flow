using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using static SereinFlow.Mcp.McpToolSupport;

namespace SereinFlow.Mcp;

/// <summary>
/// Project lifecycle MCP tools, including their preview payload projection.
/// </summary>
internal static class McpProjectToolHandlers
{
    internal static Task<object> PreviewCreateAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => PreviewCreateProjectAsync(context.Scope, context.RequirePrincipal(), arguments, cancellationToken);

    internal static Task<object> ApplyCreateAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
        => ApplyCreateProjectAsync(context.Scope, context.RequirePrincipal(), arguments, cancellationToken);

    internal static ProjectCreatePreviewDto ReadProjectCreatePreview(
        McpPreviewEntry entry,
        McpPreviewDescriptorDto descriptor)
    {
        var stored = McpPreviewService.Deserialize<StoredProjectCreatePreview>(entry);
        var project = RehydrateProject(stored);
        var candidate = new ProjectCreationCandidate(project, stored.Definition, stored.Validation);
        return ToProjectCreatePreview(descriptor, candidate);
    }

    private static async Task<object> PreviewCreateProjectAsync(
        IServiceScope scope,
        McpPrincipal principal,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        security.RequireAdministrator(principal);
        var request = Deserialize<CreateProjectMcpRequestDto>(arguments);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new McpProtocolException(-32602, "MCP parameter 'name' is required.");

        ProjectCreationCandidate candidate;
        try
        {
            candidate = scope.ServiceProvider.GetRequiredService<ProjectCreationService>()
                .PrepareEmpty(request.Name, request.FlowName);
        }
        catch (ArgumentException exception)
        {
            throw new McpProtocolException(-32602, exception.Message);
        }

        var stored = new StoredProjectCreatePreview(
            new CreateProjectMcpRequestDto(candidate.Project.Name, GetFlowName(candidate.Definition)),
            candidate.Project.Id,
            candidate.Definition.Id,
            candidate.Project.CreatedAt,
            candidate.Definition,
            candidate.Validation);
        var preview = await scope.ServiceProvider.GetRequiredService<McpPreviewService>().CreateAsync(
            "project.create",
            principal,
            candidate.Project.Id,
            candidate.Definition.Id,
            expectedVersion: null,
            stored,
            cancellationToken);
        return ToProjectCreatePreview(
            new McpPreviewDescriptorDto(
                preview.Id,
                preview.Operation,
                preview.ProjectId,
                preview.FlowId,
                preview.Status,
                preview.ExpiresAt,
                preview.PreviewFingerprint),
            candidate);
    }

    private static async Task<object> ApplyCreateProjectAsync(
        IServiceScope scope,
        McpPrincipal principal,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var request = Deserialize<McpMutationApplyRequestDto>(arguments);
        RequireConfirmation(request);
        var requestPayload = Serialize(request);
        var idempotency = scope.ServiceProvider.GetRequiredService<McpIdempotencyService>();
        var replay = await idempotency.FindAsync(
            principal.Id,
            "project.create",
            request.IdempotencyKey,
            requestPayload,
            cancellationToken);
        if (replay is not null)
            return DeserializeStoredResponse(replay.ResponseJson);

        var previews = scope.ServiceProvider.GetRequiredService<McpPreviewService>();
        var entry = await previews.RequireAsync(
            request.PreviewId,
            request.PreviewFingerprint,
            principal,
            cancellationToken);
        if (!string.Equals(entry.Operation, "project.create", StringComparison.Ordinal))
            throw new McpProtocolException(-32602, "The preview does not describe project creation.");

        var security = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
        security.RequireAdministrator(principal);
        security.Require(principal, McpPermissionDto.ProjectWrite);
        var stored = McpPreviewService.Deserialize<StoredProjectCreatePreview>(entry);
        var project = RehydrateProject(stored);
        var candidate = scope.ServiceProvider.GetRequiredService<ProjectCreationService>()
            .Prepare(project, stored.Definition);
        if (!candidate.Validation.IsValid)
        {
            throw new McpProtocolException(
                -32011,
                "The project creation preview is no longer valid.",
                new { code = McpErrorCodes.ValidationFailed, diagnostics = candidate.Validation.Diagnostics });
        }

        var result = await scope.ServiceProvider.GetRequiredService<ProjectCreationService>()
            .CreateAsync(candidate, cancellationToken);
        if (result.Status == ProjectCreationStatus.Conflict)
        {
            throw new McpProtocolException(
                -32010,
                result.ErrorMessage ?? "The project already exists.",
                new { code = result.ErrorCode });
        }
        if (result.Status != ProjectCreationStatus.Created)
            throw new McpProtocolException(-32011, "The project creation preview cannot be applied.");

        var response = CreateWorkspace(candidate);
        await MarkPreviewAppliedAsync(previews, entry, cancellationToken);
        await idempotency.SaveAsync(
            principal.Id,
            entry.Operation,
            request.IdempotencyKey,
            response,
            requestPayload,
            cancellationToken);
        return response;
    }

    private static Project RehydrateProject(StoredProjectCreatePreview stored)
        => Project.Rehydrate(
            stored.ProjectId,
            stored.Request.Name,
            version: 1,
            SereinFlow.Domain.ProjectStatus.Draft,
            stored.CreatedAt,
            stored.CreatedAt);

    private static ProjectCreatePreviewDto ToProjectCreatePreview(
        McpPreviewDescriptorDto descriptor,
        ProjectCreationCandidate candidate)
        => new(
            descriptor.PreviewId,
            candidate.Project.Id,
            candidate.Definition.Id,
            candidate.Project.Name,
            GetFlowName(candidate.Definition),
            descriptor.ExpiresAt,
            descriptor.PreviewFingerprint,
            descriptor.Status == McpMutationPreviewStatusDto.Pending
                && descriptor.ExpiresAt > DateTimeOffset.UtcNow
                && candidate.Validation.IsValid,
            candidate.Validation,
            CreateWorkspace(candidate));

    private static ProjectWorkspaceDto CreateWorkspace(ProjectCreationCandidate candidate)
        => new(
            new ProjectDto(
                candidate.Project.Id,
                candidate.Project.Name,
                candidate.Project.Version,
                ToProjectStatusValue(candidate.Project.Status),
                candidate.Project.CreatedAt,
                candidate.Project.UpdatedAt),
            [new FlowDefinitionSummaryDto(
                candidate.Definition.Id,
                candidate.Definition.Version,
                candidate.Definition.EntryNodeId,
                candidate.Definition.Canvases.Count,
                candidate.Definition.Canvases.Sum(static canvas => canvas.Nodes.Count))]);

    private static string GetFlowName(FlowDefinitionDto definition)
        => definition.Canvases.FirstOrDefault()?.Name ?? "Main";

    private static string ToProjectStatusValue(SereinFlow.Domain.ProjectStatus status)
        => status switch
        {
            SereinFlow.Domain.ProjectStatus.Draft => "draft",
            SereinFlow.Domain.ProjectStatus.Ready => "ready",
            SereinFlow.Domain.ProjectStatus.ScriptInvalid => "scriptInvalid",
            SereinFlow.Domain.ProjectStatus.Archived => "archived",
            _ => status.ToString()
        };

}

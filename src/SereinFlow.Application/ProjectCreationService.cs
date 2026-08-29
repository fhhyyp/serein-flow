using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application;

public enum ProjectCreationStatus
{
    Created,
    Invalid,
    Conflict,
}

public sealed record ProjectCreationCandidate(
    Project Project,
    FlowDefinitionDto Definition,
    FlowValidationResultDto Validation);

public sealed record ProjectCreationResult(
    ProjectCreationStatus Status,
    ProjectCreationCandidate Candidate,
    string? ErrorCode = null,
    string? ErrorMessage = null);

/// <summary>
/// Shared project and initial-flow creation boundary. The HTTP editor and MCP
/// project creation must apply the same persistence validation and normalization.
/// 统一项目和初始流程创建边界，确保 HTTP 编辑器与 MCP 创建项目使用相同的
/// 持久化校验和规范化规则。
/// </summary>
public sealed class ProjectCreationService
{
    private readonly IProjectRepository _projects;
    private readonly IFlowDefinitionRepository _flows;

    public ProjectCreationService(
        IProjectRepository projects,
        IFlowDefinitionRepository flows)
    {
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _flows = flows ?? throw new ArgumentNullException(nameof(flows));
    }

    public ProjectCreationCandidate Prepare(
        string name,
        FlowDefinitionDto definition,
        Guid? projectId = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var project = Project.Create(name, id: projectId);
        return Prepare(project, definition);
    }

    public ProjectCreationCandidate PrepareEmpty(
        string name,
        string? flowName = null,
        Guid? projectId = null,
        Guid? flowId = null)
    {
        var normalizedFlowName = string.IsNullOrWhiteSpace(flowName) ? "Main" : flowName.Trim();
        var definition = new FlowDefinitionDto(
            flowId ?? Guid.NewGuid(),
            FlowDefinition.CurrentSchemaVersion,
            1,
            [new CanvasDto(
                "main",
                CanvasLifecycleDto.Main,
                [],
                [],
                normalizedFlowName)],
            string.Empty,
            string.Empty,
            RunPolicy: new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel));
        return Prepare(name, definition, projectId);
    }

    public ProjectCreationCandidate Prepare(
        Project project,
        FlowDefinitionDto definition)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(definition);

        var diagnostics = FlowDefinitionContractValidator.ValidateForPersistence(definition)
            .Diagnostics
            .ToList();
        diagnostics.AddRange(ProjectLibraryService.ValidateNewProjectFlowLibraries(definition).Diagnostics);
        var validation = new FlowValidationResultDto(diagnostics.Count == 0, diagnostics);
        var normalized = validation.IsValid
            ? FlowDefinitionContractNormalizer.NormalizeForPersistence(definition)
            : definition;
        return new(project, normalized, validation);
    }

    public async Task<ProjectCreationResult> CreateAsync(
        ProjectCreationCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (!candidate.Validation.IsValid)
            return new(ProjectCreationStatus.Invalid, candidate, "flow.invalid", "The initial flow definition is invalid.");

        if (await _projects.FindAsync(candidate.Project.Id, cancellationToken) is not null)
        {
            return new(
                ProjectCreationStatus.Conflict,
                candidate,
                "project.already_exists",
                "The project ID already exists. 项目 ID 已存在。");
        }

        if (await _flows.FindAsync(candidate.Project.Id, candidate.Definition.Id, cancellationToken) is not null)
        {
            return new(
                ProjectCreationStatus.Conflict,
                candidate,
                "flow.already_exists",
                "The initial flow ID already exists. 初始流程 ID 已存在。");
        }

        await _projects.AddAsync(candidate.Project, cancellationToken);
        await _flows.AddAsync(candidate.Project.Id, candidate.Definition, cancellationToken);
        return new(ProjectCreationStatus.Created, candidate);
    }
}

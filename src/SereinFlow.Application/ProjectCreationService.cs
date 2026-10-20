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

public sealed record ProjectCreationCandidate(Project Project, FlowDefinitionDto Definition, FlowValidationResultDto Validation);
public sealed record ProjectCreationResult(ProjectCreationStatus Status, ProjectCreationCandidate Candidate, string? ErrorCode = null, string? ErrorMessage = null);
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
    public ProjectCreationService(IProjectRepository projects, IFlowDefinitionRepository flows)
    {
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _flows = flows ?? throw new ArgumentNullException(nameof(flows));
    }

    public ProjectCreationCandidate Prepare(string name, FlowDefinitionDto definition, Guid? projectId = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var project = Project.Create(name, id: projectId);
        var prepareResult = Prepare(project, definition);
        return prepareResult;
    }

    public ProjectCreationCandidate PrepareEmpty(string name, string? flowName = null, Guid? projectId = null, Guid? flowId = null)
    {
        var normalizedFlowName = string.IsNullOrWhiteSpace(flowName) ? "Main" : flowName.Trim();
        var evaluatedValue = flowId ?? Guid.NewGuid();
        global::System.Collections.Generic.IReadOnlyList<global::SereinFlow.Contracts.CanvasDto> evaluatedValue1 = [new CanvasDto("main", CanvasLifecycleDto.Main, [], [], normalizedFlowName)];
        var flowRunPolicyDto = new FlowRunPolicyDto(FlowConcurrencyModeDto.Parallel);
        var definition = new FlowDefinitionDto(evaluatedValue, FlowDefinition.CurrentSchemaVersion, 1, evaluatedValue1, string.Empty, string.Empty, RunPolicy: flowRunPolicyDto);
        var prepareResult2 = Prepare(name, definition, projectId);
        return prepareResult2;
    }

    public ProjectCreationCandidate Prepare(Project project, FlowDefinitionDto definition)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(definition);
        var evaluatedValue3 = FlowDefinitionContractValidator.ValidateForPersistence(definition).Diagnostics;
        var diagnostics = evaluatedValue3.ToList();
        var evaluatedValue4 = ProjectLibraryService.ValidateNewProjectFlowLibraries(definition).Diagnostics;
        diagnostics.AddRange(evaluatedValue4);
        var validation = new FlowValidationResultDto(diagnostics.Count == 0, diagnostics);
        var normalized = validation.IsValid ? FlowDefinitionContractNormalizer.NormalizeForPersistence(definition) : definition;
        global::SereinFlow.Application.ProjectCreationCandidate evaluatedValue5 = new(project, normalized, validation);
        return evaluatedValue5;
    }

    public async Task<ProjectCreationResult> CreateAsync(ProjectCreationCandidate candidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (!candidate.Validation.IsValid)
        {
            global::SereinFlow.Application.ProjectCreationResult evaluatedValue6 = new(ProjectCreationStatus.Invalid, candidate, FlowErrorCodes.Invalid, "The initial flow definition is invalid.");
            return evaluatedValue6;
        }

        if (await _projects.FindAsync(candidate.Project.Id, cancellationToken)is not null)
        {
            global::SereinFlow.Application.ProjectCreationResult evaluatedValue7 = new(ProjectCreationStatus.Conflict, candidate, ProjectErrorCodes.AlreadyExists, "The project ID already exists. 项目 ID 已存在。");
            return evaluatedValue7;
        }

        if (await _flows.FindAsync(candidate.Project.Id, candidate.Definition.Id, cancellationToken)is not null)
        {
            global::SereinFlow.Application.ProjectCreationResult evaluatedValue8 = new(ProjectCreationStatus.Conflict, candidate, FlowErrorCodes.AlreadyExists, "The initial flow ID already exists. 初始流程 ID 已存在。");
            return evaluatedValue8;
        }

        await _projects.AddAsync(candidate.Project, cancellationToken);
        await _flows.AddAsync(candidate.Project.Id, candidate.Definition, cancellationToken);
        global::SereinFlow.Application.ProjectCreationResult evaluatedValue9 = new(ProjectCreationStatus.Created, candidate);
        return evaluatedValue9;
    }
}

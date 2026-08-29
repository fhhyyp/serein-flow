using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Application;

public enum FlowDefinitionWriteStatus
{
    Saved,
    NoChange,
    NotFound,
    Archived,
    Conflict,
    Invalid,
}

public sealed record FlowDefinitionPreparationResult(
    FlowDefinitionDto Current,
    FlowDefinitionDto Candidate,
    FlowValidationResultDto Validation);

public sealed record FlowDefinitionWriteResult(
    FlowDefinitionWriteStatus Status,
    FlowDefinitionPreparationResult? Preparation = null,
    FlowDefinitionDto? Saved = null,
    long? CurrentVersion = null)
{
    public bool IsSaved => Status == FlowDefinitionWriteStatus.Saved && Saved is not null;
}

/// <summary>
/// Shared application boundary for persistence validation, normalization and
/// optimistic flow-definition writes. API and MCP must use the same boundary
/// so a flow cannot be accepted by one entry point and rejected by another.
/// 统一流程持久化校验、规范化和乐观并发写入边界。API 与 MCP 必须复用，避免
/// 同一流程在不同入口产生不一致的规则。
/// </summary>
public sealed class FlowDefinitionWriteService
{
    private readonly IProjectRepository _projects;
    private readonly IFlowDefinitionRepository _flows;
    private readonly ProjectLibraryService _projectLibraries;

    public FlowDefinitionWriteService(
        IProjectRepository projects,
        IFlowDefinitionRepository flows,
        ProjectLibraryService projectLibraries)
    {
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _flows = flows ?? throw new ArgumentNullException(nameof(flows));
        _projectLibraries = projectLibraries ?? throw new ArgumentNullException(nameof(projectLibraries));
    }

    public async Task<FlowDefinitionPreparationResult?> PrepareAsync(
        Guid projectId,
        Guid flowId,
        FlowDefinitionDto candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (await _projects.FindAsync(projectId, cancellationToken) is null)
            return null;
        var current = await _flows.FindAsync(projectId, flowId, cancellationToken);
        if (current is null)
            return null;

        var diagnostics = FlowDefinitionContractValidator.ValidateForPersistence(candidate)
            .Diagnostics
            .ToList();
        var libraryValidation = await _projectLibraries.ValidateFlowLibrariesAsync(
            projectId,
            candidate,
            cancellationToken);
        diagnostics.AddRange(libraryValidation.Diagnostics);
        var validation = new FlowValidationResultDto(diagnostics.Count == 0, diagnostics);
        var normalized = validation.IsValid
            ? FlowDefinitionContractNormalizer.NormalizeForPersistence(candidate)
            : candidate;
        return new(current, normalized, validation);
    }

    public async Task<FlowDefinitionWriteResult> WriteAsync(
        Guid projectId,
        Guid flowId,
        FlowDefinitionDto candidate,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        if (candidate.Id != flowId || expectedVersion < 1)
        {
            return new(
                FlowDefinitionWriteStatus.Invalid,
                CurrentVersion: expectedVersion);
        }

        var preparation = await PrepareAsync(projectId, flowId, candidate, cancellationToken);
        if (preparation is null)
            return new(FlowDefinitionWriteStatus.NotFound);
        if (await _projects.FindAsync(projectId, cancellationToken) is { Status: SereinFlow.Domain.ProjectStatus.Archived })
            return new(FlowDefinitionWriteStatus.Archived, preparation);
        if (preparation.Current.Version != expectedVersion)
        {
            return new(
                FlowDefinitionWriteStatus.Conflict,
                preparation,
                CurrentVersion: preparation.Current.Version);
        }
        if (!preparation.Validation.IsValid)
            return new(FlowDefinitionWriteStatus.Invalid, preparation);

        if (new FlowDiffService().Compare(preparation.Current, preparation.Candidate).Changes.Count == 0)
            return new(FlowDefinitionWriteStatus.NoChange, preparation, preparation.Current, expectedVersion);

        var saved = await _flows.TryUpdateAsync(
            projectId,
            preparation.Candidate,
            expectedVersion,
            cancellationToken);
        return saved is null
            ? new(
                FlowDefinitionWriteStatus.Conflict,
                preparation,
                CurrentVersion: (await _flows.FindAsync(projectId, flowId, cancellationToken))?.Version)
            : new(FlowDefinitionWriteStatus.Saved, preparation, saved, saved.Version);
    }
}

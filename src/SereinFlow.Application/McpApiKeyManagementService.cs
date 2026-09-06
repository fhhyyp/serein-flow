using SereinFlow.Application.Persistence;
using Microsoft.AspNetCore.Http;
using SereinFlow.Contracts;
using SereinFlow.Domain;

namespace SereinFlow.Application;

/// <summary>
/// Shared MCP API key lifecycle used by the HTTP management surface and MCP tools.
/// Secrets are returned only at creation or rotation time; persistence stores hashes.
/// </summary>
public sealed class McpApiKeyManagementService
{
    private static readonly SemaphoreSlim InitialSetupGate = new(1, 1);
    private readonly IMcpApiKeyStore _keys;
    private readonly IProjectRepository _projects;

    public McpApiKeyManagementService(IMcpApiKeyStore keys, IProjectRepository projects)
    {
        _keys = keys;
        _projects = projects;
    }

    public async Task<IReadOnlyList<McpApiKeyDto>> ListAsync(CancellationToken cancellationToken = default)
        => (await _keys.ListAsync(cancellationToken))
            .Select(McpSecurityService.ToDto)
            .ToArray();

    public async Task<CreatedMcpApiKeyDto> CreateAsync(
        CreateMcpApiKeyRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await ValidateProjectAsync(request.ProjectId, cancellationToken);
        var created = McpSecurityService.CreateKeyWithEntry(request);
        await _keys.AddAsync(created.Entry, cancellationToken);
        return created.Dto;
    }

    public async Task<CreatedMcpApiKeyDto> CreateInitialAdministratorAsync(
        CancellationToken cancellationToken = default)
    {
        await InitialSetupGate.WaitAsync(cancellationToken);
        try
        {
            var existing = await _keys.ListAsync(cancellationToken);
            if (existing.Count > 0)
            {
                throw new McpSecurityException(
                    McpErrorCodes.KeySetupAlreadyCompleted,
                    "MCP API key setup has already been completed.",
                    StatusCodes.Status409Conflict);
            }

            return await CreateAsync(
                new CreateMcpApiKeyRequestDto(
                    null,
                    "SereinFlow Web Console",
                    Enum.GetValues<McpPermissionDto>(),
                    null,
                    IsAdministrator: true),
                cancellationToken);
        }
        finally
        {
            InitialSetupGate.Release();
        }
    }

    public async Task<McpApiKeyDto> RevokeAsync(string id, CancellationToken cancellationToken = default)
    {
        var existing = await FindAsync(id, cancellationToken);
        if (existing.RevokedAt is not null)
            return McpSecurityService.ToDto(existing);

        var revoked = existing with { RevokedAt = DateTimeOffset.UtcNow };
        await _keys.UpdateAsync(revoked, cancellationToken);
        return McpSecurityService.ToDto(revoked);
    }

    public async Task<RotatedMcpApiKeyDto> RotateAsync(string id, CancellationToken cancellationToken = default)
    {
        var existing = await FindAsync(id, cancellationToken);
        if (existing.RevokedAt is not null)
        {
            throw new McpSecurityException(
                McpErrorCodes.KeyAlreadyRevoked,
                "The MCP API key has already been revoked.",
                StatusCodes.Status409Conflict);
        }

        var replacement = McpSecurityService.RotateKeyWithEntry(existing);
        await _keys.AddAsync(replacement.Entry, cancellationToken);
        await _keys.UpdateAsync(existing with { RevokedAt = DateTimeOffset.UtcNow }, cancellationToken);
        return new RotatedMcpApiKeyDto(existing.Id, replacement.Dto.Key, replacement.Dto.Secret);
    }

    private async Task<McpApiKeyEntry> FindAsync(string id, CancellationToken cancellationToken)
        => await _keys.FindAsync(id, cancellationToken)
            ?? throw new McpSecurityException(McpErrorCodes.KeyNotFound, "The MCP API key was not found.", StatusCodes.Status404NotFound);

    private async Task ValidateProjectAsync(Guid? projectId, CancellationToken cancellationToken)
    {
        if (projectId is null)
            return;

        var project = await _projects.FindAsync(projectId.Value, cancellationToken);
        if (project is null)
            throw new McpSecurityException(McpErrorCodes.ProjectNotFound, "The API key project was not found.", StatusCodes.Status404NotFound);
        if (project.Status == ProjectStatus.Archived)
        {
            throw new McpSecurityException(
                McpErrorCodes.ProjectArchived,
                "An API key cannot be bound to an archived project.",
                StatusCodes.Status409Conflict);
        }
    }
}

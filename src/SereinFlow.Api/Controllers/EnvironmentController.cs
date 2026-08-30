using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Mcp;

namespace SereinFlow.Api.Controllers;

[Route("api/environment/settings")]
public sealed class EnvironmentController : ApiControllerBase
{
    private readonly RunExecutionQueue _queue;
    private readonly IRunEnvironmentSettingsStore _settings;
    private readonly McpSecurityService _mcpSecurity;
    private readonly McpApiKeyManagementService _mcpKeys;
    private readonly IHostEnvironment _environment;

    public EnvironmentController(
        RunExecutionQueue queue,
        IRunEnvironmentSettingsStore settings,
        McpSecurityService mcpSecurity,
        McpApiKeyManagementService mcpKeys,
        IHostEnvironment environment)
    {
        _queue = queue;
        _settings = settings;
        _mcpSecurity = mcpSecurity;
        _mcpKeys = mcpKeys;
        _environment = environment;
    }

    [HttpGet]
    [ProducesResponseType(typeof(RunExecutionSettingsDto), StatusCodes.Status200OK)]
    public ActionResult<RunExecutionSettingsDto> Get()
        => Ok(_queue.Options.ToDto());

    [HttpPut]
    [ProducesResponseType(typeof(RunExecutionSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Update(
        [FromBody] RunExecutionSettingsDto settings,
        CancellationToken cancellationToken)
    {
        var normalized = RunExecutionOptions.FromDto(settings).ToDto();
        var saved = await _settings.SaveAsync(normalized, cancellationToken);
        return Ok(_queue.Configure(saved));
    }

    [HttpPost("mcp-keys/setup")]
    [ProducesResponseType(typeof(CreatedMcpApiKeyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> SetupMcpKey(CancellationToken cancellationToken)
    {
        if (!IsLoopbackRequest())
        {
            return ApiProblem(
                StatusCodes.Status403Forbidden,
                "Initial MCP key setup is available only from the local machine. MCP 初始密钥只能从本机创建。");
        }

        try
        {
            var created = await _mcpKeys.CreateInitialAdministratorAsync(cancellationToken);
            return Created("/api/environment/settings/mcp-keys", created);
        }
        catch (McpSecurityException exception)
        {
            return ApiProblem(exception.StatusCode, exception.Code, exception.Message);
        }
    }

    [HttpGet("mcp-keys")]
    [ProducesResponseType(typeof(McpApiKeyDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> ListMcpKeys(CancellationToken cancellationToken)
    {
        var principal = await AuthenticateMcpKeyAsync(cancellationToken);
        var authorizationError = RequireAdministrator(principal);
        if (authorizationError is not null)
            return authorizationError;

        return Ok(await _mcpKeys.ListAsync(cancellationToken));
    }

    [HttpPost("mcp-keys")]
    [ProducesResponseType(typeof(CreatedMcpApiKeyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> CreateMcpKey(
        [FromBody] CreateMcpApiKeyRequestDto? request,
        CancellationToken cancellationToken)
    {
        var principal = await AuthenticateMcpKeyAsync(cancellationToken);
        var authorizationError = RequireAdministrator(principal);
        if (authorizationError is not null)
            return authorizationError;
        if (request is null)
            return ApiProblem(StatusCodes.Status400BadRequest, "An MCP API key request is required.");

        try
        {
            var created = await _mcpKeys.CreateAsync(request, cancellationToken);
            return Created($"/api/environment/settings/mcp-keys/{created.Key.Id}", created);
        }
        catch (McpSecurityException exception)
        {
            return ApiProblem(exception.StatusCode, exception.Code, exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ApiProblem(StatusCodes.Status400BadRequest, "Invalid MCP API key request.", exception.Message);
        }
    }

    [HttpPost("mcp-keys/{id}/rotate")]
    [ProducesResponseType(typeof(RotatedMcpApiKeyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> RotateMcpKey([FromRoute] string id, CancellationToken cancellationToken)
    {
        var principal = await AuthenticateMcpKeyAsync(cancellationToken);
        var authorizationError = RequireAdministrator(principal);
        if (authorizationError is not null)
            return authorizationError;

        try
        {
            return Ok(await _mcpKeys.RotateAsync(id, cancellationToken));
        }
        catch (McpSecurityException exception)
        {
            return ApiProblem(exception.StatusCode, exception.Code, exception.Message);
        }
    }

    [HttpDelete("mcp-keys/{id}")]
    [ProducesResponseType(typeof(McpApiKeyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RevokeMcpKey([FromRoute] string id, CancellationToken cancellationToken)
    {
        var principal = await AuthenticateMcpKeyAsync(cancellationToken);
        var authorizationError = RequireAdministrator(principal);
        if (authorizationError is not null)
            return authorizationError;

        try
        {
            return Ok(await _mcpKeys.RevokeAsync(id, cancellationToken));
        }
        catch (McpSecurityException exception)
        {
            return ApiProblem(exception.StatusCode, exception.Code, exception.Message);
        }
    }

    private async Task<McpPrincipal?> AuthenticateMcpKeyAsync(CancellationToken cancellationToken)
    {
        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization.ToString(), out var header)
            || !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await _mcpSecurity.AuthenticateAsync(header.Parameter, cancellationToken);
    }

    private ActionResult? RequireAdministrator(McpPrincipal? principal)
    {
        try
        {
            _mcpSecurity.RequireAdministrator(principal);
            return null;
        }
        catch (McpSecurityException exception)
        {
            if (exception.StatusCode == StatusCodes.Status401Unauthorized)
                Response.Headers.WWWAuthenticate = "Bearer";
            return ApiProblem(exception.StatusCode, exception.Code, exception.Message);
        }
    }

    private bool IsLoopbackRequest()
    {
        var remoteAddress = Request.HttpContext.Connection.RemoteIpAddress;
        if (remoteAddress is not null)
            return IPAddress.IsLoopback(remoteAddress);

        // TestServer does not populate a remote address. Never apply this
        // fallback outside the isolated Testing host.
        return _environment.IsEnvironment("Testing");
    }
}

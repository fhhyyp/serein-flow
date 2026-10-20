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
    public EnvironmentController(RunExecutionQueue queue, IRunEnvironmentSettingsStore settings, McpSecurityService mcpSecurity, McpApiKeyManagementService mcpKeys, IHostEnvironment environment)
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
    {
        var toDtoResult = _queue.Options.ToDto();
        var okResult = Ok(toDtoResult);
        return okResult;
    }

    [HttpPut]
    [ProducesResponseType(typeof(RunExecutionSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Update([FromBody] RunExecutionSettingsDto settings, CancellationToken cancellationToken)
    {
        var fromDtoResult = RunExecutionOptions.FromDto(settings);
        var normalized = fromDtoResult.ToDto();
        var saved = await _settings.SaveAsync(normalized, cancellationToken);
        var configureResult = _queue.Configure(saved);
        var okResult1 = Ok(configureResult);
        return okResult1;
    }

    [HttpPost("mcp-keys/setup")]
    [SereinFlowAnonymous]
    [ProducesResponseType(typeof(CreatedMcpApiKeyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> SetupMcpKey(CancellationToken cancellationToken)
    {
        if (!IsLoopbackRequest())
        {
            var apiProblemResult = ApiProblem(StatusCodes.Status403Forbidden, "Initial MCP key setup is available only from the local machine. MCP 初始密钥只能从本机创建。");
            return apiProblemResult;
        }

        try
        {
            var created = await _mcpKeys.CreateInitialAdministratorAsync(cancellationToken);
            var createdResult = Created("/api/environment/settings/mcp-keys", created);
            return createdResult;
        }
        catch (McpSecurityException exception)
        {
            var apiProblemResult2 = ApiProblem(exception.StatusCode, exception.Code, exception.Message);
            return apiProblemResult2;
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
        {
            return authorizationError;
        }

        var listAsyncResult = await _mcpKeys.ListAsync(cancellationToken);
        var okResult3 = Ok(listAsyncResult);
        return okResult3;
    }

    [HttpPost("mcp-keys")]
    [ProducesResponseType(typeof(CreatedMcpApiKeyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> CreateMcpKey([FromBody] CreateMcpApiKeyRequestDto? request, CancellationToken cancellationToken)
    {
        var principal = await AuthenticateMcpKeyAsync(cancellationToken);
        var authorizationError = RequireAdministrator(principal);
        if (authorizationError is not null)
        {
            return authorizationError;
        }

        if (request is null)
        {
            var apiProblemResult4 = ApiProblem(StatusCodes.Status400BadRequest, "An MCP API key request is required.");
            return apiProblemResult4;
        }

        try
        {
            var created = await _mcpKeys.CreateAsync(request, cancellationToken);
            var createdResult5 = Created($"/api/environment/settings/mcp-keys/{created.Key.Id}", created);
            return createdResult5;
        }
        catch (McpSecurityException exception)
        {
            var apiProblemResult6 = ApiProblem(exception.StatusCode, exception.Code, exception.Message);
            return apiProblemResult6;
        }
        catch (ArgumentException exception)
        {
            var apiProblemResult7 = ApiProblem(StatusCodes.Status400BadRequest, "Invalid MCP API key request.", exception.Message);
            return apiProblemResult7;
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
        {
            return authorizationError;
        }

        try
        {
            var rotateAsyncResult = await _mcpKeys.RotateAsync(id, cancellationToken);
            var okResult8 = Ok(rotateAsyncResult);
            return okResult8;
        }
        catch (McpSecurityException exception)
        {
            var apiProblemResult9 = ApiProblem(exception.StatusCode, exception.Code, exception.Message);
            return apiProblemResult9;
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
        {
            return authorizationError;
        }

        try
        {
            var revokeAsyncResult = await _mcpKeys.RevokeAsync(id, cancellationToken);
            var okResult10 = Ok(revokeAsyncResult);
            return okResult10;
        }
        catch (McpSecurityException exception)
        {
            var apiProblemResult11 = ApiProblem(exception.StatusCode, exception.Code, exception.Message);
            return apiProblemResult11;
        }
    }

    private async Task<McpPrincipal?> AuthenticateMcpKeyAsync(CancellationToken cancellationToken)
    {
        var authorizationText = Request.Headers.Authorization.ToString();
        if (!AuthenticationHeaderValue.TryParse(authorizationText, out var header) || !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var authenticateAsyncResult = await _mcpSecurity.AuthenticateAsync(header.Parameter, cancellationToken);
        return authenticateAsyncResult;
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
            {
                Response.Headers.WWWAuthenticate = "Bearer";
            }

            var apiProblemResult12 = ApiProblem(exception.StatusCode, exception.Code, exception.Message);
            return apiProblemResult12;
        }
    }

    private bool IsLoopbackRequest()
    {
        var remoteAddress = Request.HttpContext.Connection.RemoteIpAddress;
        if (remoteAddress is not null)
        {
            var isLoopbackResult = IPAddress.IsLoopback(remoteAddress);
            return isLoopbackResult;
        }

        var isEnvironmentResult = _environment.IsEnvironment("Testing");
        // TestServer does not populate a remote address. Never apply this
        // fallback outside the isolated Testing host.
        return isEnvironmentResult;
    }
}

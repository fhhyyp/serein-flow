using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Mcp;

namespace SereinFlow.Api.Controllers;

[Route("api/environment/settings/mcp-skills")]
public sealed class McpSkillsController(
    McpAiGuidanceManagementService guidance,
    McpSecurityService security) : ApiControllerBase
{
    [HttpGet]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(McpAiGuidanceResourceDto[]), StatusCodes.Status200OK)]
    public Task<ActionResult> List(CancellationToken cancellationToken)
        => ExecuteAsync(_ => Task.FromResult<object>(guidance.List()), cancellationToken);

    [HttpGet("{key}")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(McpAiGuidanceContentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult> Read([FromRoute] string key, CancellationToken cancellationToken)
        => ExecuteAsync(async token => await guidance.ReadAsync(key, token), cancellationToken);

    [HttpPut("{key}")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(McpAiGuidanceContentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public Task<ActionResult> Save(
        [FromRoute] string key,
        [FromBody] SaveMcpAiGuidanceRequestDto? request,
        CancellationToken cancellationToken)
        => ExecuteAsync(async token =>
        {
            RequireValidBody(request);
            return await guidance.SaveAsync(key, request!, token);
        }, cancellationToken);

    [HttpPost("{key}/reset")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(McpAiGuidanceContentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult> Reset(
        [FromRoute] string key,
        [FromBody] ResetMcpAiGuidanceRequestDto? request,
        CancellationToken cancellationToken)
        => ExecuteAsync(async token =>
        {
            RequireValidBody(request);
            return await guidance.ResetAsync(key, request!, token);
        }, cancellationToken);

    private void RequireValidBody(object? request)
    {
        if (request is null || !ModelState.IsValid)
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams,
                "A valid MCP skill request is required.", new { code = McpErrorCodes.InvalidArguments });
    }

    private async Task<ActionResult> ExecuteAsync(Func<CancellationToken, Task<object>> action, CancellationToken cancellationToken)
    {
        try
        {
            AuthenticationHeaderValue.TryParse(Request.Headers.Authorization.ToString(), out var header);
            var principal = header is not null && string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
                ? await security.AuthenticateAsync(header.Parameter, cancellationToken)
                : null;
            // Skill edits change guidance for every MCP client. Require an administrator
            // even when the host permits anonymous access to other REST endpoints.
            security.RequireAdministrator(principal);
            Response.Headers.CacheControl = "no-store";
            return Ok(await action(cancellationToken));
        }
        catch (McpSecurityException exception)
        {
            if (exception.StatusCode == StatusCodes.Status401Unauthorized)
                Response.Headers.WWWAuthenticate = "Bearer";
            return ApiProblem(exception.StatusCode, exception.Message,
                extensions: new Dictionary<string, object?> { ["code"] = exception.Code });
        }
        catch (McpProtocolException exception)
        {
            var status = exception.Code switch
            {
                McpProtocolErrorCodes.InvalidParams => StatusCodes.Status400BadRequest,
                McpProtocolErrorCodes.PermissionDenied => StatusCodes.Status403Forbidden,
                McpProtocolErrorCodes.ResourceNotFound => StatusCodes.Status404NotFound,
                McpProtocolErrorCodes.Conflict => StatusCodes.Status409Conflict,
                McpProtocolErrorCodes.RequestTooLarge => StatusCodes.Status413PayloadTooLarge,
                _ => StatusCodes.Status503ServiceUnavailable,
            };
            var data = JsonSerializer.SerializeToElement(exception.ErrorData);
            var extensions = data.ValueKind == JsonValueKind.Object
                ? data.EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value.Clone())
                : null;
            return ApiProblem(status, exception.Message, extensions: extensions);
        }
    }
}

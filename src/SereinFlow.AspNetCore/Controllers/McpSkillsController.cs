using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Mcp;

namespace SereinFlow.Api.Controllers;

[Route("api/environment/settings/mcp-skills")]
public sealed class McpSkillsController(McpAiGuidanceManagementService guidance, McpSecurityService security) : ApiControllerBase
{
    [HttpGet]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(McpAiGuidanceResourceDto[]), StatusCodes.Status200OK)]
    public Task<ActionResult> List(CancellationToken cancellationToken)
    {
        var executeAsyncResult = ExecuteAsync(_ =>
        {
            var listResult = guidance.List();
            var fromResultResult = Task.FromResult<object>(listResult);
            return fromResultResult;
        }, cancellationToken);
        return executeAsyncResult;
    }

    [HttpGet("{key}")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(McpAiGuidanceContentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult> Read([FromRoute] string key, CancellationToken cancellationToken)
    {
        var executeAsyncResult1 = ExecuteAsync(async token =>
        {
            var readAsyncResult = await guidance.ReadAsync(key, token);
            return readAsyncResult;
        }, cancellationToken);
        return executeAsyncResult1;
    }

    [HttpPut("{key}")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(McpAiGuidanceContentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public Task<ActionResult> Save([FromRoute] string key, [FromBody] SaveMcpAiGuidanceRequestDto? request, CancellationToken cancellationToken)
    {
        var executeAsyncResult2 = ExecuteAsync(async token =>
        {
            RequireValidBody(request);
            var saveAsyncResult = await guidance.SaveAsync(key, request!, token);
            return saveAsyncResult;
        }, cancellationToken);
        return executeAsyncResult2;
    }

    [HttpPost("{key}/reset")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(McpAiGuidanceContentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult> Reset([FromRoute] string key, [FromBody] ResetMcpAiGuidanceRequestDto? request, CancellationToken cancellationToken)
    {
        var executeAsyncResult3 = ExecuteAsync(async token =>
        {
            RequireValidBody(request);
            var resetAsyncResult = await guidance.ResetAsync(key, request!, token);
            return resetAsyncResult;
        }, cancellationToken);
        return executeAsyncResult3;
    }

    private void RequireValidBody(object? request)
    {
        if (request is null || !ModelState.IsValid)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "A valid MCP skill request is required.", new { code = McpErrorCodes.InvalidArguments });
        }
    }

    private async Task<ActionResult> ExecuteAsync(Func<CancellationToken, Task<object>> action, CancellationToken cancellationToken)
    {
        try
        {
            var toStringResult = Request.Headers.Authorization.ToString();
            AuthenticationHeaderValue.TryParse(toStringResult, out var header);
            var principal = header is not null && string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ? await security.AuthenticateAsync(header.Parameter, cancellationToken) : null;
            // Skill edits change guidance for every MCP client. Require an administrator
            // even when the host permits anonymous access to other REST endpoints.
            security.RequireAdministrator(principal);
            Response.Headers.CacheControl = "no-store";
            var actionResult = await action(cancellationToken);
            var okResult = Ok(actionResult);
            return okResult;
        }
        catch (McpSecurityException exception)
        {
            if (exception.StatusCode == StatusCodes.Status401Unauthorized)
            {
                Response.Headers.WWWAuthenticate = "Bearer";
            }

            var dictionary = new Dictionary<string, object?>
            {
                ["code"] = exception.Code
            };
            var apiProblemResult = ApiProblem(exception.StatusCode, exception.Message, extensions: dictionary);
            return apiProblemResult;
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
            var extensions = data.ValueKind == JsonValueKind.Object ? resolveToDictionaryResult() : null;
            var apiProblemResult4 = ApiProblem(status, exception.Message, extensions: extensions);
            return apiProblemResult4;
            Dictionary<string, object?> resolveToDictionaryResult()
            {
                var toDictionaryResult = data.EnumerateObject()
                    .ToDictionary(property => property.Name, property =>
                    {
                        var evaluatedValue = (object?)property.Value.Clone();
                        return evaluatedValue;
                    });
                return toDictionaryResult;
            }
        }
    }
}

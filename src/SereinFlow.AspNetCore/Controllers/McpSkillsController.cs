using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application;
using SereinFlow.Contracts;
using SereinFlow.Mcp;

namespace SereinFlow.Api.Controllers;

[Route("api/environment/settings/mcp-skills")]
public sealed class McpSkillsController(McpAiGuidanceManagementService guidance, SereinFlowAccess security) : ApiControllerBase
{
    [HttpGet]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(McpAiGuidanceResourceDto[]), StatusCodes.Status200OK)]
    public Task<ActionResult> List(CancellationToken cancellationToken)
    {
        return ExecuteAsync(_ =>
        {
            return Task.FromResult<object>(guidance.List());
        }, cancellationToken);
    }

    [HttpGet("{key}")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(McpAiGuidanceContentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult> Read([FromRoute] string key, CancellationToken cancellationToken)
    {
        return ExecuteAsync(async token =>
        {
            return await guidance.ReadAsync(key, token);
        }, cancellationToken);
    }

    [HttpPut("{key}")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(McpAiGuidanceContentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public Task<ActionResult> Save([FromRoute] string key, [FromBody] SaveMcpAiGuidanceRequestDto? request, CancellationToken cancellationToken)
    {
        return ExecuteAsync(async token =>
        {
            RequireValidBody(request);
            return await guidance.SaveAsync(key, request!, token);
        }, cancellationToken);
    }

    [HttpPost("{key}/reset")]
    [SereinFlowScopedAuthorization]
    [ProducesResponseType(typeof(McpAiGuidanceContentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult> Reset([FromRoute] string key, [FromBody] ResetMcpAiGuidanceRequestDto? request, CancellationToken cancellationToken)
    {
        return ExecuteAsync(async token =>
        {
            RequireValidBody(request);
            return await guidance.ResetAsync(key, request!, token);
        }, cancellationToken);
    }

    private void RequireValidBody(object? request)
    {
        if (request is null || !ModelState.IsValid)
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "A valid MCP skill request is required. 必须提供有效的 MCP 技能请求。", new { code = McpErrorCodes.InvalidArguments });
        }
    }

    private async Task<ActionResult> ExecuteAsync(Func<CancellationToken, Task<object>> action, CancellationToken cancellationToken)
    {
        try
        {
            var principal = SereinFlow.AspNetCore.SereinFlowHttpActor.Get(HttpContext);
            await security.RequireWorkspaceAsync(principal, cancellationToken);
            Response.Headers.CacheControl = "no-store";
            var mappedAction = await action(cancellationToken);
            return Ok(mappedAction);
        }
        catch (SereinFlowAccessException exception)
        {
            if (exception.StatusCode == StatusCodes.Status401Unauthorized)
            {
                Response.Headers.WWWAuthenticate = "Bearer";
            }
            return ApiProblemWithCode(exception.StatusCode, exception.Message, code: exception.Code);
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
            var extensions = data.ValueKind == JsonValueKind.Object ? data.EnumerateObject().ToDictionary(property => property.Name, property =>
                {
                    return (object?)property.Value.Clone();
                }) : null;
            return ApiProblem(status, exception.Message, extensions: extensions);
        }
    }
}

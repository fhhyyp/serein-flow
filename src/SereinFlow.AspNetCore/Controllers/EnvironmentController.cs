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
    public EnvironmentController(RunExecutionQueue queue, IRunEnvironmentSettingsStore settings)
    {
        _queue = queue;
        _settings = settings;
    }

    [HttpGet]
    [ProducesResponseType(typeof(RunExecutionSettingsDto), StatusCodes.Status200OK)]
    public ActionResult<RunExecutionSettingsDto> Get()
    {
        var dto = _queue.Options.ToDto();
        return Ok(dto);
    }

    [HttpPut]
    [ProducesResponseType(typeof(RunExecutionSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Update([FromBody] RunExecutionSettingsDto settings, CancellationToken cancellationToken)
    {
        var normalized = RunExecutionOptions.FromDto(settings).ToDto();
        var saved = await _settings.SaveAsync(normalized, cancellationToken);
        var configure = _queue.Configure(saved);
        return Ok(configure);
    }
}

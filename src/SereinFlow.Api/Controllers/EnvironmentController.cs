using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

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
}

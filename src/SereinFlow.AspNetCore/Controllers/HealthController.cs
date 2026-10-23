using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SereinFlow.Contracts;

namespace SereinFlow.Api.Controllers;

[Route("healthz")]
public sealed class HealthController : ApiControllerBase
{
    [HttpGet]
    [SereinFlowAnonymous]
    [AllowAnonymous]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthCheckResponse> Get()
    {
        var healthCheckResponse = new HealthCheckResponse("Healthy");
        return Ok(healthCheckResponse);
    }
}

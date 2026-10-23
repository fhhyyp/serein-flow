using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SereinFlow.AspNetCore;

namespace SereinFlow.Api.Controllers;

[ApiController]
[Route("api/security/capabilities")]
public sealed class SecurityCapabilitiesController(SereinFlowHttpSecurityOptions options) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var capabilities = HttpContext.RequestServices.GetService<ISereinFlowSecurityCapabilities>();
        var initialSetupRequired = capabilities is not null && await capabilities.RequiresInitialSetupAsync(cancellationToken);
        Response.Headers.CacheControl = "no-store";
        {
            return Ok(new { mode = options.Mode, apiKeyManagement = options.ApiKeyManagement, initialSetupRequired });
        }
    }
}

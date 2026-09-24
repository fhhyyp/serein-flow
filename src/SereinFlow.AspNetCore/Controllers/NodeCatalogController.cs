using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application;

namespace SereinFlow.Api.Controllers;

[Route("api/node-catalog")]
public sealed class NodeCatalogController : ApiControllerBase
{
    private readonly IBuiltinNodeCatalog _catalog;

    public NodeCatalogController(IBuiltinNodeCatalog catalog)
    {
        _catalog = catalog;
    }

    [HttpGet("builtins")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetBuiltins()
        => Ok(_catalog.GetCatalog());
}

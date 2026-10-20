using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application;

namespace SereinFlow.Api.Controllers;
[Route("api/library-families")]
public sealed class LibraryFamiliesController : ApiControllerBase
{
    private readonly ILibraryCatalogService _catalog;
    public LibraryFamiliesController(ILibraryCatalogService catalog)
    {
        _catalog = catalog;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> List(CancellationToken cancellationToken)
    {
        var listFamiliesAsyncResult = await _catalog.ListFamiliesAsync(cancellationToken: cancellationToken);
        var okResult = Ok(listFamiliesAsyncResult);
        return okResult;
    }

    [HttpGet("{familyId}/artifacts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ListArtifacts([FromRoute] string familyId, CancellationToken cancellationToken)
    {
        var evaluatedValue = (await _catalog.ListFamiliesAsync(cancellationToken: cancellationToken));
        var family = evaluatedValue.SingleOrDefault(item =>
        {
            var equalsResult = string.Equals(item.Id, familyId, StringComparison.OrdinalIgnoreCase);
            return equalsResult;
        });
        ActionResult evaluatedValue1 = family is null ? ApiProblem(StatusCodes.Status404NotFound, "Library family not found. 未找到类库族。") : Ok(family.Artifacts ?? []);
        return evaluatedValue1;
    }
}

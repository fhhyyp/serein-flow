using Microsoft.AspNetCore.Mvc;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Api.Controllers;

[Route("api/libraries")]
public sealed class LibrariesController : ApiControllerBase
{
    private readonly ILibraryCatalogService _catalog;
    private readonly ILibraryArtifactUsageStore _usage;
    private readonly IFileUploadSettings _fileUploadSettings;

    public LibrariesController(
        ILibraryCatalogService catalog,
        ILibraryArtifactUsageStore usage,
        IFileUploadSettings fileUploadSettings)
    {
        _catalog = catalog;
        _usage = usage;
        _fileUploadSettings = fileUploadSettings;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> List(CancellationToken cancellationToken)
        => Ok(await _catalog.ListAsync(cancellationToken: cancellationToken));

    [HttpGet("usage")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetUsage(CancellationToken cancellationToken)
        => Ok(await _usage.ListAsync(cancellationToken));

    [HttpGet("{libraryId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get([FromRoute] string libraryId, CancellationToken cancellationToken)
    {
        var library = await _catalog.FindAsync(libraryId, cancellationToken);
        return library is null
            ? ApiProblem(StatusCodes.Status404NotFound, "Library not found. 未找到类库。")
            : Ok(library);
    }

    [HttpPatch("{libraryId}/family")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> AssignFamily(
        [FromRoute] string libraryId,
        [FromBody] AssignLibraryFamilyRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var family = await _catalog.AssignFamilyAsync(libraryId, request, cancellationToken);
            return family is null
                ? ApiProblem(StatusCodes.Status404NotFound, "Library artifact not found. 未找到类库制品。")
                : Ok(family);
        }
        catch (ArgumentException exception)
        {
            return ApiProblem(StatusCodes.Status400BadRequest, exception.Message);
        }
    }

    [HttpPatch("{libraryId}/lifecycle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SetLifecycle(
        [FromRoute] string libraryId,
        [FromBody] UpdateLibraryLifecycleRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Lifecycle))
            return ApiProblem(StatusCodes.Status400BadRequest, "The library lifecycle is invalid. 类库生命周期无效。");

        return await _catalog.SetLifecycleAsync(libraryId, request.Lifecycle, cancellationToken)
            ? NoContent()
            : ApiProblem(StatusCodes.Status404NotFound, "Library artifact not found. 未找到类库制品。");
    }

    [HttpPost("upload")]
    [HttpPost("upload-zip")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult> Upload(CancellationToken cancellationToken)
        => UploadCore(cancellationToken);

    [HttpDelete("{libraryId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Archive([FromRoute] string libraryId, CancellationToken cancellationToken)
        => await _catalog.ArchiveAsync(libraryId, cancellationToken)
            ? NoContent()
            : ApiProblem(StatusCodes.Status404NotFound, "Library not found. 未找到类库。");

    // These singular routes are maintained as explicit attribute aliases for existing clients.
    [HttpGet("/api/library")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ListLegacy(CancellationToken cancellationToken)
        => Ok(await _catalog.ListAsync(cancellationToken: cancellationToken));

    [HttpGet("/api/library/{libraryId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult> GetLegacy([FromRoute] string libraryId, CancellationToken cancellationToken)
        => Get(libraryId, cancellationToken);

    [HttpGet("/api/library/{libraryId}/nodes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetLegacyNodes([FromRoute] string libraryId, CancellationToken cancellationToken)
    {
        var library = await _catalog.FindAsync(libraryId, cancellationToken);
        return library is null
            ? ApiProblem(StatusCodes.Status404NotFound, "Library not found. 未找到类库。")
            : Ok(library.Nodes);
    }

    [HttpPost("/api/library/upload")]
    [HttpPost("/api/library/upload-zip")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult> UploadLegacy(CancellationToken cancellationToken)
        => UploadCore(cancellationToken);

    [HttpDelete("/api/library/{libraryId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult> ArchiveLegacy([FromRoute] string libraryId, CancellationToken cancellationToken)
        => Archive(libraryId, cancellationToken);

    [HttpGet("/api/environment/libraries")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ListEnvironment(CancellationToken cancellationToken)
        => Ok(await _catalog.ListAsync(includeArchived: true, cancellationToken: cancellationToken));

    [HttpPost("/api/environment/libraries/upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult> UploadEnvironment(CancellationToken cancellationToken)
        => UploadCore(cancellationToken);

    [HttpPost("/api/environment/libraries/{libraryId}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ArchiveEnvironment([FromRoute] string libraryId, CancellationToken cancellationToken)
        => await _catalog.ArchiveAsync(libraryId, cancellationToken)
            ? NoContent()
            : ApiProblem(StatusCodes.Status404NotFound, "Library artifact not found. 未找到类库制品。");

    [HttpPost("/api/environment/libraries/{libraryId}/reindex")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ReindexEnvironment([FromRoute] string libraryId, CancellationToken cancellationToken)
    {
        try
        {
            var library = await _catalog.ReindexAsync(libraryId, cancellationToken);
            return library is null
                ? ApiProblem(StatusCodes.Status404NotFound, "Library artifact not found. 未找到类库制品。")
                : Ok(library);
        }
        catch (LibraryUploadException exception)
        {
            return ApiProblem(exception.StatusCode, exception.Message);
        }
    }

    private async Task<ActionResult> UploadCore(CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType)
            return ApiProblem(StatusCodes.Status400BadRequest, "A multipart form upload is required. 必须使用 multipart 表单上传文件。");

        var maxRequestBytes = FileUploadLimits.GetApiRequestBodyLimit(_fileUploadSettings.MaxLibraryUploadBytes);
        if (Request.ContentLength is > 0 && Request.ContentLength > maxRequestBytes)
        {
            return ApiProblem(
                StatusCodes.Status413PayloadTooLarge,
                $"The upload request cannot exceed {_fileUploadSettings.MaxLibraryUploadBytes / (1024 * 1024)} MB plus multipart overhead. 上传请求不能超过 {_fileUploadSettings.MaxLibraryUploadBytes / (1024 * 1024)} MB（另含 multipart 开销）。");
        }

        var form = await Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file") ?? (form.Files.Count > 0 ? form.Files[0] : null);
        if (file is null)
        {
            return ApiProblem(
                StatusCodes.Status400BadRequest,
                "The upload must include a file field named 'file'. 上传请求必须包含名为 file 的文件字段。");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            return Ok(await _catalog.UploadAsync(stream, file.FileName, file.Length, cancellationToken));
        }
        catch (LibraryUploadException exception)
        {
            return ApiProblem(
                exception.StatusCode,
                "Library upload rejected. 类库上传已拒绝。",
                detail: exception.Message);
        }
    }
}

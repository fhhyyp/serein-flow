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
    public LibrariesController(ILibraryCatalogService catalog, ILibraryArtifactUsageStore usage, IFileUploadSettings fileUploadSettings)
    {
        _catalog = catalog;
        _usage = usage;
        _fileUploadSettings = fileUploadSettings;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> List(CancellationToken cancellationToken)
    {
        var listAsyncResult = await _catalog.ListAsync(cancellationToken: cancellationToken);
        var okResult = Ok(listAsyncResult);
        return okResult;
    }

    [HttpGet("usage")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetUsage(CancellationToken cancellationToken)
    {
        var listAsyncResult1 = await _usage.ListAsync(cancellationToken);
        var okResult2 = Ok(listAsyncResult1);
        return okResult2;
    }

    [HttpGet("{libraryId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get([FromRoute] string libraryId, CancellationToken cancellationToken)
    {
        var library = await _catalog.FindAsync(libraryId, cancellationToken);
        global::Microsoft.AspNetCore.Mvc.ActionResult evaluatedValue = library is null ? ApiProblem(StatusCodes.Status404NotFound, "Library not found. 未找到类库。") : Ok(library);
        return evaluatedValue;
    }

    [HttpPatch("{libraryId}/family")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> AssignFamily([FromRoute] string libraryId, [FromBody] AssignLibraryFamilyRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var family = await _catalog.AssignFamilyAsync(libraryId, request, cancellationToken);
            global::Microsoft.AspNetCore.Mvc.ActionResult evaluatedValue3 = family is null ? ApiProblem(StatusCodes.Status404NotFound, "Library artifact not found. 未找到类库制品。") : Ok(family);
            return evaluatedValue3;
        }
        catch (ArgumentException exception)
        {
            var apiProblemResult = ApiProblem(StatusCodes.Status400BadRequest, exception.Message);
            return apiProblemResult;
        }
    }

    [HttpPatch("{libraryId}/lifecycle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SetLifecycle([FromRoute] string libraryId, [FromBody] UpdateLibraryLifecycleRequestDto request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Lifecycle))
        {
            var apiProblemResult4 = ApiProblem(StatusCodes.Status400BadRequest, "The library lifecycle is invalid. 类库生命周期无效。");
            return apiProblemResult4;
        }

        global::Microsoft.AspNetCore.Mvc.ActionResult evaluatedValue5 = await _catalog.SetLifecycleAsync(libraryId, request.Lifecycle, cancellationToken) ? NoContent() : ApiProblem(StatusCodes.Status404NotFound, "Library artifact not found. 未找到类库制品。");
        return evaluatedValue5;
    }

    [HttpPost("upload")]
    [HttpPost("upload-zip")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult> Upload(CancellationToken cancellationToken)
    {
        var uploadCoreResult = UploadCore(cancellationToken);
        return uploadCoreResult;
    }

    [HttpDelete("{libraryId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Archive([FromRoute] string libraryId, CancellationToken cancellationToken)
    {
        global::Microsoft.AspNetCore.Mvc.ActionResult evaluatedValue6 = await _catalog.ArchiveAsync(libraryId, cancellationToken) ? NoContent() : ApiProblem(StatusCodes.Status404NotFound, "Library not found. 未找到类库。");
        return evaluatedValue6;
    }

    // These singular routes are maintained as explicit attribute aliases for existing clients.
    [HttpGet("/api/library")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ListLegacy(CancellationToken cancellationToken)
    {
        var listAsyncResult7 = await _catalog.ListAsync(cancellationToken: cancellationToken);
        var okResult8 = Ok(listAsyncResult7);
        return okResult8;
    }

    [HttpGet("/api/library/{libraryId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult> GetLegacy([FromRoute] string libraryId, CancellationToken cancellationToken)
    {
        var getResult = Get(libraryId, cancellationToken);
        return getResult;
    }

    [HttpGet("/api/library/{libraryId}/nodes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetLegacyNodes([FromRoute] string libraryId, CancellationToken cancellationToken)
    {
        var library = await _catalog.FindAsync(libraryId, cancellationToken);
        global::Microsoft.AspNetCore.Mvc.ActionResult evaluatedValue9 = library is null ? ApiProblem(StatusCodes.Status404NotFound, "Library not found. 未找到类库。") : Ok(library.Nodes);
        return evaluatedValue9;
    }

    [HttpPost("/api/library/upload")]
    [HttpPost("/api/library/upload-zip")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult> UploadLegacy(CancellationToken cancellationToken)
    {
        var uploadCoreResult10 = UploadCore(cancellationToken);
        return uploadCoreResult10;
    }

    [HttpDelete("/api/library/{libraryId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult> ArchiveLegacy([FromRoute] string libraryId, CancellationToken cancellationToken)
    {
        var archiveResult = Archive(libraryId, cancellationToken);
        return archiveResult;
    }

    [HttpGet("/api/environment/libraries")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ListEnvironment(CancellationToken cancellationToken)
    {
        var listAsyncResult11 = await _catalog.ListAsync(includeArchived: true, cancellationToken: cancellationToken);
        var okResult12 = Ok(listAsyncResult11);
        return okResult12;
    }

    [HttpPost("/api/environment/libraries/upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult> UploadEnvironment(CancellationToken cancellationToken)
    {
        var uploadCoreResult13 = UploadCore(cancellationToken);
        return uploadCoreResult13;
    }

    [HttpPost("/api/environment/libraries/{libraryId}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ArchiveEnvironment([FromRoute] string libraryId, CancellationToken cancellationToken)
    {
        global::Microsoft.AspNetCore.Mvc.ActionResult evaluatedValue14 = await _catalog.ArchiveAsync(libraryId, cancellationToken) ? NoContent() : ApiProblem(StatusCodes.Status404NotFound, "Library artifact not found. 未找到类库制品。");
        return evaluatedValue14;
    }

    [HttpPost("/api/environment/libraries/{libraryId}/reindex")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ReindexEnvironment([FromRoute] string libraryId, CancellationToken cancellationToken)
    {
        try
        {
            var library = await _catalog.ReindexAsync(libraryId, cancellationToken);
            global::Microsoft.AspNetCore.Mvc.ActionResult evaluatedValue15 = library is null ? ApiProblem(StatusCodes.Status404NotFound, "Library artifact not found. 未找到类库制品。") : Ok(library);
            return evaluatedValue15;
        }
        catch (LibraryUploadException exception)
        {
            var apiProblemResult16 = ApiProblem(exception.StatusCode, exception.Message);
            return apiProblemResult16;
        }
    }

    private async Task<ActionResult> UploadCore(CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType)
        {
            var apiProblemResult17 = ApiProblem(StatusCodes.Status400BadRequest, "A multipart form upload is required. 必须使用 multipart 表单上传文件。");
            return apiProblemResult17;
        }

        var maxRequestBytes = FileUploadLimits.GetApiRequestBodyLimit(_fileUploadSettings.MaxLibraryUploadBytes);
        if (Request.ContentLength is> 0 && Request.ContentLength > maxRequestBytes)
        {
            var apiProblemResult18 = ApiProblem(StatusCodes.Status413PayloadTooLarge, $"The upload request cannot exceed {_fileUploadSettings.MaxLibraryUploadBytes / (1024 * 1024)} MB plus multipart overhead. 上传请求不能超过 {_fileUploadSettings.MaxLibraryUploadBytes / (1024 * 1024)} MB（另含 multipart 开销）。");
            return apiProblemResult18;
        }

        var form = await Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file") ?? (form.Files.Count > 0 ? form.Files[0] : null);
        if (file is null)
        {
            var apiProblemResult19 = ApiProblem(StatusCodes.Status400BadRequest, "The upload must include a file field named 'file'. 上传请求必须包含名为 file 的文件字段。");
            return apiProblemResult19;
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var uploadAsyncResult = await _catalog.UploadAsync(stream, file.FileName, file.Length, cancellationToken);
            var okResult20 = Ok(uploadAsyncResult);
            return okResult20;
        }
        catch (LibraryUploadException exception)
        {
            var apiProblemResult21 = ApiProblem(exception.StatusCode, "Library upload rejected. 类库上传已拒绝。", detail: exception.Message);
            return apiProblemResult21;
        }
    }
}

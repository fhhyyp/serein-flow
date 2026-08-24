using SereinFlow.Contracts;

namespace SereinFlow.Application;

/// <summary>
/// Trusted API boundary for uploaded class libraries.
/// Implementations may persist packages and metadata, but must never load an
/// uploaded assembly in the API process. Assembly inspection/execution belongs
/// to a separately supervised Worker process.
/// </summary>
public interface ILibraryCatalogService
{
    IReadOnlyList<LibraryDto> List();

    LibraryDto? Find(string libraryId);

    Task<LibraryUploadResultDto> UploadAsync(
        Stream package,
        string fileName,
        long? declaredLength = null,
        CancellationToken cancellationToken = default);

    bool Delete(string libraryId);
}

public sealed class LibraryUploadException : Exception
{
    public LibraryUploadException(string message, int statusCode = 400)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public LibraryUploadException(string message, Exception innerException, int statusCode = 400)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

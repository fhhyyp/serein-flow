using System.Text.Json;

namespace SereinFlow.Client;

public sealed record SereinFlowApiError(
    int StatusCode,
    string? Code,
    string? Title,
    string? Detail,
    string? Instance,
    IReadOnlyDictionary<string, JsonElement> Extensions);

public class SereinFlowClientException : Exception
{
    public SereinFlowClientException(
        int statusCode,
        Uri requestUri,
        SereinFlowApiError error,
        string? responseBody = null)
        : base(error.Detail ?? error.Title ?? $"SereinFlow request failed with status {statusCode}.")
    {
        StatusCode = statusCode;
        RequestUri = requestUri;
        Error = error;
        ResponseBody = responseBody;
    }

    public int StatusCode { get; }

    public Uri RequestUri { get; }

    public SereinFlowApiError Error { get; }

    public string? Code => Error.Code;

    public string? ResponseBody { get; }
}

public sealed class SereinFlowClientTimeoutException : TimeoutException
{
    public SereinFlowClientTimeoutException(
        Guid resourceId,
        TimeSpan timeout,
        object? lastKnownState)
        : base($"SereinFlow resource '{resourceId:D}' did not complete within {timeout}.")
    {
        ResourceId = resourceId;
        Timeout = timeout;
        LastKnownState = lastKnownState;
    }

    public Guid ResourceId { get; }

    public TimeSpan Timeout { get; }

    public object? LastKnownState { get; }
}

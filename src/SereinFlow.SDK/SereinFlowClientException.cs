using System.Text.Json;

namespace SereinFlow.Client;
/// <summary>
/// Represents serein flow API error data used by SereinFlow.
/// 表示 SereinFlow 使用的Serein流程API错误数据。
/// </summary>
public sealed record SereinFlowApiError(int StatusCode, string? Code, string? Title, string? Detail, string? Instance, IReadOnlyDictionary<string, JsonElement> Extensions);
public class SereinFlowClientException : Exception
{
    public SereinFlowClientException(int statusCode, Uri requestUri, SereinFlowApiError error, string? responseBody = null) : base(error.Detail ?? error.Title ?? $"SereinFlow request failed with status {statusCode}. SereinFlow 请求失败，状态码见上述信息。")
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
    public SereinFlowClientTimeoutException(Guid resourceId, TimeSpan timeout, object? lastKnownState) : base($"SereinFlow resource '{resourceId:D}' did not complete within {timeout}. SereinFlow 资源未在指定时限内完成。")
    {
        ResourceId = resourceId;
        Timeout = timeout;
        LastKnownState = lastKnownState;
    }

    public Guid ResourceId { get; }
    public TimeSpan Timeout { get; }
    public object? LastKnownState { get; }
}

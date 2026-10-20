using System.Net.Http;
using System.Text.Json;
using SereinFlow.Contracts;

namespace SereinFlow.Client;
public sealed class RunMessageClient : IRunMessageClient
{
    private readonly SereinFlowHttpClient _transport;
    internal RunMessageClient(SereinFlowHttpClient transport)
    {
        _transport = transport;
    }

    public Task<WorkerMessageDeliveryResponseDto> PublishMessageAsync<TPayload>(Guid runId, string topic, TPayload payload, string? contractId = null, WorkerMessageChannelKindDto channelKind = WorkerMessageChannelKindDto.Queue, string? idempotencyKey = null, string? messageId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        var resolvedIdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? resolveToStringResult() : idempotencyKey;
        var serializeToElementResult = JsonSerializer.SerializeToElement(payload, _transport.JsonOptions);
        var request = new RunMessageIngressRequestDto(serializeToElementResult, messageId, contractId, channelKind);
        var evaluatedValue = $"api/runs/{runId:D}/messages/{resolveEscapeDataStringResult()}";
        var dictionary = new Dictionary<string, string?>
        {
            ["Idempotency-Key"] = resolvedIdempotencyKey
        };
        var sendJsonAsyncResult = _transport.SendJsonAsync<WorkerMessageDeliveryResponseDto>(HttpMethod.Post, evaluatedValue, request, allowRetry: true, dictionary, cancellationToken);
        return sendJsonAsyncResult;
        string resolveToStringResult()
        {
            var newGuidResult = Guid.NewGuid();
            var toStringResult = newGuidResult.ToString("N");
            return toStringResult;
        }

        string resolveEscapeDataStringResult()
        {
            var trimResult = topic.Trim();
            var escapeDataStringResult = Uri.EscapeDataString(trimResult);
            return escapeDataStringResult;
        }
    }
}

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
        var resolvedIdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString("N") : idempotencyKey;
        var request = new RunMessageIngressRequestDto(JsonSerializer.SerializeToElement(payload, _transport.JsonOptions), messageId, contractId, channelKind);
        var relativePath = $"api/runs/{runId:D}/messages/{Uri.EscapeDataString(topic.Trim())}";
        var dictionary = new Dictionary<string, string?>
        {
            ["Idempotency-Key"] = resolvedIdempotencyKey
        };
        return _transport.SendJsonAsync<WorkerMessageDeliveryResponseDto>(HttpMethod.Post, relativePath, request, allowRetry: true, dictionary, cancellationToken);
    }
}

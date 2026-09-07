using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SereinFlow.Client;
using SereinFlow.Contracts;

namespace SereinFlow.SDK.Tests;

public sealed class SereinFlowClientTests
{
    private static readonly Uri BaseAddress = new("https://sereinflow.test/");

    [Fact]
    public void RunExecutePermissionUsesStableContractName()
    {
        Assert.Equal("run.execute", McpPermissionNames.ToName(McpPermissionDto.RunExecute));
    }

    [Fact]
    public async Task GetRunBuildsUrlAndBearerHeader()
    {
        var runId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => JsonResponse(new FlowRunDto(
            runId,
            Guid.NewGuid(),
            4,
            FlowRunStatusDto.Running,
            null,
            null,
            null)));
        using var client = CreateClient(handler, apiKey: "secret");

        var run = await client.Runs.GetAsync(runId);

        Assert.Equal(runId, run.Id);
        var request = Assert.Single(handler.Requests);
        Assert.Equal($"https://sereinflow.test/api/runs/{runId:D}", request.RequestUri?.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("secret", request.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task ProblemDetailsBecomeTypedClientException()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"status\":403,\"title\":\"Denied\",\"detail\":\"No access\",\"code\":\"run.denied\"}", Encoding.UTF8, "application/problem+json")
        });
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<SereinFlowClientException>(() =>
            client.Runs.GetAsync(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Forbidden, (HttpStatusCode)exception.StatusCode);
        Assert.Equal("run.denied", exception.Code);
        Assert.Equal("No access", exception.Error.Detail);
    }

    [Fact]
    public async Task GetRetriesTransientResponsesAndHonorsTheFinalPayload()
    {
        var count = 0;
        var runId = Guid.NewGuid();
        var handler = new RecordingHandler(_ =>
        {
            count++;
            return count == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero) }
                }
                : JsonResponse(new FlowRunDto(runId, Guid.NewGuid(), 1, FlowRunStatusDto.Succeeded, null, null, null));
        });
        using var client = CreateClient(handler, configure: options =>
        {
            options.MaxRetries = 1;
            options.RetryBaseDelay = TimeSpan.Zero;
        });

        var run = await client.Runs.GetAsync(runId);

        Assert.Equal(runId, run.Id);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task CancelDoesNotRetryTransientResponses()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var client = CreateClient(handler, configure: options => options.MaxRetries = 3);

        await Assert.ThrowsAsync<SereinFlowClientException>(() =>
            client.Runs.CancelAsync(Guid.NewGuid()));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task RequestsHonorCallerCancellation()
    {
        using var handler = new CancellationAwareHandler();
        using var client = CreateClient(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Runs.GetAsync(Guid.NewGuid(), cancellation.Token));
    }

    [Fact]
    public async Task WaitForCompletionPollsUntilTerminalAndReadsOutputs()
    {
        var runId = Guid.NewGuid();
        var responses = new Queue<HttpResponseMessage>([
            JsonResponse(new FlowRunDto(runId, Guid.NewGuid(), 1, FlowRunStatusDto.Pending, null, null, null)),
            JsonResponse(new FlowRunDto(runId, Guid.NewGuid(), 1, FlowRunStatusDto.Succeeded, null, DateTimeOffset.UtcNow, null)),
            JsonResponse(Array.Empty<FlowRunOutputDto>()),
        ]);
        var handler = new RecordingHandler(_ => responses.Dequeue());
        using var client = CreateClient(handler);

        var result = await client.Runs.WaitForCompletionAsync(
            runId,
            timeout: TimeSpan.FromSeconds(2),
            pollInterval: TimeSpan.FromMilliseconds(1));

        Assert.True(result.IsSucceeded);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task WaitForCompletionThrowsWithLastKnownRunOnTimeout()
    {
        var runId = Guid.NewGuid();
        var handler = new RecordingHandler(_ =>
            JsonResponse(new FlowRunDto(runId, Guid.NewGuid(), 1, FlowRunStatusDto.Running, null, null, null)));
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<SereinFlowClientTimeoutException>(() =>
            client.Runs.WaitForCompletionAsync(
                runId,
                timeout: TimeSpan.FromMilliseconds(10),
                pollInterval: TimeSpan.FromMilliseconds(1)));

        Assert.Equal(runId, exception.ResourceId);
        Assert.IsType<FlowRunDto>(exception.LastKnownState);
    }

    [Fact]
    public async Task LatestNodeOutputUsesTheHighestSequenceAndContractJsonOptions()
    {
        var runId = Guid.NewGuid();
        var outputs = new[]
        {
            new FlowRunOutputDto(runId, 2, DateTimeOffset.UtcNow, "node-a", "completed", null,
                JsonSerializer.SerializeToElement(new { }), JsonSerializer.SerializeToElement(new { value = 2 }), null, null),
            new FlowRunOutputDto(runId, 5, DateTimeOffset.UtcNow, "node-a", "completed", null,
                JsonSerializer.SerializeToElement(new { }), JsonSerializer.SerializeToElement(new { value = 5 }), null, null),
        };
        var handler = new RecordingHandler(_ => JsonResponse(outputs));
        using var client = CreateClient(handler);

        var value = await client.Runs.GetLatestNodeOutputAsync<OutputValue>(runId, "node-a");

        Assert.NotNull(value);
        Assert.Equal(5, value!.Value);
    }

    [Fact]
    public async Task StreamEventsUsesLastEventIdAndParsesSseData()
    {
        var runId = Guid.NewGuid();
        var handler = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $"id: 8\nevent: NodeCompleted\ndata: {{\"runId\":\"{runId:D}\",\"sequence\":8,\"timestamp\":\"2026-01-01T00:00:00Z\",\"type\":\"NodeCompleted\",\"nodeId\":\"node-a\",\"payloadJson\":\"{{}}\"}}\n\n",
                    Encoding.UTF8,
                    "text/event-stream")
            };
            return response;
        });
        using var client = CreateClient(handler);

        var events = new List<FlowRunEventDto>();
        await foreach (var item in client.Runs.StreamEventsAsync(runId, afterSequence: 7))
            events.Add(item);

        Assert.Equal(8, Assert.Single(events).Sequence);
        Assert.Equal("7", Assert.Single(handler.Requests).Headers.GetValues("Last-Event-ID").Single());
    }

    [Fact]
    public async Task PublishMessageGeneratesAndSendsAnIdempotencyKey()
    {
        var runId = Guid.NewGuid();
        var requestBody = string.Empty;
        var handler = new RecordingHandler(request =>
        {
            Assert.True(request.Headers.TryGetValues("Idempotency-Key", out var values));
            Assert.False(string.IsNullOrWhiteSpace(values!.Single()));
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse(new WorkerMessageDeliveryResponseDto(
                WorkerMessageDeliveryStatusDto.Accepted,
                WorkerProtocol.Version,
                runId,
                Guid.NewGuid(),
                "orders.created",
                null,
                "accepted"));
        });
        using var client = CreateClient(handler);

        var response = await client.Messages.PublishMessageAsync(
            runId,
            "orders.created",
            new { orderId = "A10001" });

        Assert.Equal(WorkerMessageDeliveryStatusDto.Accepted, response.Status);
        Assert.Contains("orders.created", handler.Requests.Single().RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Contains("A10001", requestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublishMessageReusesIdempotencyKeyAcrossRetries()
    {
        var runId = Guid.NewGuid();
        var keys = new List<string>();
        var attempt = 0;
        var handler = new RecordingHandler(request =>
        {
            keys.Add(request.Headers.GetValues("Idempotency-Key").Single());
            if (++attempt == 1)
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

            return JsonResponse(new WorkerMessageDeliveryResponseDto(
                WorkerMessageDeliveryStatusDto.Accepted,
                WorkerProtocol.Version,
                runId,
                Guid.NewGuid(),
                "orders.created",
                null,
                "accepted"));
        });
        using var client = CreateClient(handler, configure: options =>
        {
            options.MaxRetries = 1;
            options.RetryBaseDelay = TimeSpan.Zero;
        });

        await client.Messages.PublishMessageAsync(runId, "orders.created", new { orderId = "A10001" });

        Assert.Equal(2, keys.Count);
        Assert.Equal(keys[0], keys[1]);
    }

    [Fact]
    public async Task WorkpieceDownloadExposesResponseMetadataAndContent()
    {
        var handler = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes("artifact"))
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = "artifact.txt"
            };
            return response;
        });
        using var client = CreateClient(handler);

        await using var download = await client.Workpieces.DownloadAsync(Guid.NewGuid(), "piece-1");
        using var reader = new StreamReader(download.Content);

        Assert.Equal("artifact", await reader.ReadToEndAsync());
        Assert.Equal("text/plain", download.ContentType);
        Assert.Equal("artifact.txt", download.FileName);
    }

    private static SereinFlowClient CreateClient(
        HttpMessageHandler handler,
        string? apiKey = null,
        Action<SereinFlowClientOptions>? configure = null)
    {
        var options = new SereinFlowClientOptions
        {
            BaseAddress = BaseAddress,
            ApiKey = apiKey,
            RequestTimeout = TimeSpan.FromSeconds(5),
        };
        configure?.Invoke(options);
        return new SereinFlowClient(new HttpClient(handler), options);
    }

    private static HttpResponseMessage JsonResponse<T>(T value)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(value, SereinJsonSerialization.CreateContractOptions()),
                Encoding.UTF8,
                "application/json")
        };

    private sealed record OutputValue(int Value);

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }

    private sealed class CancellationAwareHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("The request was not cancelled.");
        }
    }
}

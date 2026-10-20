using System.Collections.Generic;
using System.Net.Http;
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
        var toNameResult = McpPermissionNames.ToName(McpPermissionDto.RunExecute);
        Assert.Equal("run.execute", toNameResult);
    }

    [Fact]
    public async Task GetRunBuildsUrlAndBearerHeader()
    {
        var runId = Guid.NewGuid();
        var handler = new RecordingHandler(_ =>
        {
            var newGuidResult = Guid.NewGuid();
            var flowRunDto = new FlowRunDto(runId, newGuidResult, 4, FlowRunStatusDto.Running, null, null, null);
            var jsonResponseResult = JsonResponse(flowRunDto);
            return jsonResponseResult;
        });
        using var client = CreateClient(handler, apiKey: "secret");
        var run = await client.Runs.GetAsync(runId);
        Assert.Equal(runId, run.Id);
        var request = Assert.Single(handler.Requests);
        var evaluatedValue = request.RequestUri?.ToString();
        Assert.Equal($"https://sereinflow.test/api/runs/{runId:D}", evaluatedValue);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("secret", request.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task ProblemDetailsBecomeTypedClientException()
    {
        var handler = new RecordingHandler(_ =>
        {
            var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("{\"status\":403,\"title\":\"Denied\",\"detail\":\"No access\",\"code\":\"run.denied\"}", Encoding.UTF8, "application/problem+json")
            };
            return httpResponseMessage;
        });
        using var client = CreateClient(handler);
        var exception = await Assert.ThrowsAsync<SereinFlowClientException>(() =>
        {
            var newGuidResult1 = Guid.NewGuid();
            var getAsyncResult = client.Runs.GetAsync(newGuidResult1);
            return getAsyncResult;
        });
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
            HttpResponseMessage conditionalValue;
            if (count == 1)
            {
                conditionalValue = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Headers =
                    {
                        RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero)
                    }
                };
            }
            else
            {
                var newGuidResult2 = Guid.NewGuid();
                var flowRunDto3 = new FlowRunDto(runId, newGuidResult2, 1, FlowRunStatusDto.Succeeded, null, null, null);
                conditionalValue = JsonResponse(flowRunDto3);
            }

            var evaluatedValue2 = conditionalValue;
            return evaluatedValue2;
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
        var handler = new RecordingHandler(_ =>
        {
            var httpResponseMessage3 = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            return httpResponseMessage3;
        });
        using var client = CreateClient(handler, configure: options => options.MaxRetries = 3);
        await Assert.ThrowsAsync<SereinFlowClientException>(() =>
        {
            var newGuidResult4 = Guid.NewGuid();
            var cancelAsyncResult = client.Runs.CancelAsync(newGuidResult4);
            return cancelAsyncResult;
        });
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
        {
            var newGuidResult5 = Guid.NewGuid();
            var getAsyncResult6 = client.Runs.GetAsync(newGuidResult5, cancellation.Token);
            return getAsyncResult6;
        });
    }

    [Fact]
    public async Task WaitForCompletionPollsUntilTerminalAndReadsOutputs()
    {
        var runId = Guid.NewGuid();
        IEnumerable<HttpResponseMessage> evaluatedValue7 = [resolveJsonResponseResult(), resolveJsonResponseResult9(), resolveJsonResponseResult13(),];
        var responses = new Queue<HttpResponseMessage>(evaluatedValue7);
        var handler = new RecordingHandler(_ =>
        {
            var dequeueResult = responses.Dequeue();
            return dequeueResult;
        });
        using var client = CreateClient(handler);
        var fromSecondsResult = TimeSpan.FromSeconds(2);
        var fromMillisecondsResult = TimeSpan.FromMilliseconds(1);
        var result = await client.Runs.WaitForCompletionAsync(runId, timeout: fromSecondsResult, pollInterval: fromMillisecondsResult);
        Assert.True(result.IsSucceeded);
        Assert.Equal(3, handler.Requests.Count);
        HttpResponseMessage resolveJsonResponseResult()
        {
            var newGuidResult6 = Guid.NewGuid();
            var flowRunDto7 = new FlowRunDto(runId, newGuidResult6, 1, FlowRunStatusDto.Pending, null, null, null);
            var jsonResponseResult8 = JsonResponse(flowRunDto7);
            return jsonResponseResult8;
        }

        HttpResponseMessage resolveJsonResponseResult9()
        {
            var newGuidResult10 = Guid.NewGuid();
            var flowRunDto11 = new FlowRunDto(runId, newGuidResult10, 1, FlowRunStatusDto.Succeeded, null, DateTimeOffset.UtcNow, null);
            var jsonResponseResult12 = JsonResponse(flowRunDto11);
            return jsonResponseResult12;
        }

        HttpResponseMessage resolveJsonResponseResult13()
        {
            var emptyResult = Array.Empty<FlowRunOutputDto>();
            var jsonResponseResult14 = JsonResponse(emptyResult);
            return jsonResponseResult14;
        }
    }

    [Fact]
    public async Task WaitForCompletionThrowsWithLastKnownRunOnTimeout()
    {
        var runId = Guid.NewGuid();
        var handler = new RecordingHandler(_ =>
        {
            var newGuidResult8 = Guid.NewGuid();
            var flowRunDto9 = new FlowRunDto(runId, newGuidResult8, 1, FlowRunStatusDto.Running, null, null, null);
            var jsonResponseResult10 = JsonResponse(flowRunDto9);
            return jsonResponseResult10;
        });
        using var client = CreateClient(handler);
        var exception = await Assert.ThrowsAsync<SereinFlowClientTimeoutException>(() =>
        {
            var fromMillisecondsResult11 = TimeSpan.FromMilliseconds(10);
            var fromMillisecondsResult12 = TimeSpan.FromMilliseconds(1);
            var waitForCompletionAsyncResult = client.Runs.WaitForCompletionAsync(runId, timeout: fromMillisecondsResult11, pollInterval: fromMillisecondsResult12);
            return waitForCompletionAsyncResult;
        });
        Assert.Equal(runId, exception.ResourceId);
        Assert.IsType<FlowRunDto>(exception.LastKnownState);
    }

    [Fact]
    public async Task LatestNodeOutputUsesTheHighestSequenceAndContractJsonOptions()
    {
        var runId = Guid.NewGuid();
        var outputs = new[]
        {
            resolveFlowRunOutputDto(),
            resolveFlowRunOutputDto15(),
        };
        var handler = new RecordingHandler(_ =>
        {
            var jsonResponseResult13 = JsonResponse(outputs);
            return jsonResponseResult13;
        });
        using var client = CreateClient(handler);
        var value = await client.Runs.GetLatestNodeOutputAsync<OutputValue>(runId, "node-a");
        Assert.NotNull(value);
        Assert.Equal(5, value!.Value);
        FlowRunOutputDto resolveFlowRunOutputDto()
        {
            var serializeToElementResult = JsonSerializer.SerializeToElement(new { });
            var serializeToElementResult14 = JsonSerializer.SerializeToElement(new { value = 2 });
            var flowRunOutputDto = new FlowRunOutputDto(runId, 2, DateTimeOffset.UtcNow, "node-a", "completed", null, serializeToElementResult, serializeToElementResult14, null, null);
            return flowRunOutputDto;
        }

        FlowRunOutputDto resolveFlowRunOutputDto15()
        {
            var serializeToElementResult16 = JsonSerializer.SerializeToElement(new { });
            var serializeToElementResult17 = JsonSerializer.SerializeToElement(new { value = 5 });
            var flowRunOutputDto18 = new FlowRunOutputDto(runId, 5, DateTimeOffset.UtcNow, "node-a", "completed", null, serializeToElementResult16, serializeToElementResult17, null, null);
            return flowRunOutputDto18;
        }
    }

    [Fact]
    public async Task StreamEventsUsesLastEventIdAndParsesSseData()
    {
        var runId = Guid.NewGuid();
        var handler = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"id: 8\nevent: NodeCompleted\ndata: {{\"runId\":\"{runId:D}\",\"sequence\":8,\"timestamp\":\"2026-01-01T00:00:00Z\",\"type\":\"NodeCompleted\",\"nodeId\":\"node-a\",\"payloadJson\":\"{{}}\"}}\n\n", Encoding.UTF8, "text/event-stream")
            };
            return response;
        });
        using var client = CreateClient(handler);
        var events = new List<FlowRunEventDto>();
        await foreach (var item in client.Runs.StreamEventsAsync(runId, afterSequence: 7))
        {
            events.Add(item);
        }

        var evaluatedValue19 = Assert.Single(events).Sequence;
        Assert.Equal(8, evaluatedValue19);
        var evaluatedValue20 = Assert.Single(handler.Requests).Headers;
        var getValuesResult = evaluatedValue20.GetValues("Last-Event-ID");
        var singleResult = getValuesResult.Single();
        Assert.Equal("7", singleResult);
    }

    [Fact]
    public async Task PublishMessageGeneratesAndSendsAnIdempotencyKey()
    {
        var runId = Guid.NewGuid();
        var requestBody = string.Empty;
        var handler = new RecordingHandler(request =>
        {
            var tryGetValuesResult = request.Headers.TryGetValues("Idempotency-Key", out var values);
            Assert.True(tryGetValuesResult);
            var singleResult21 = values!.Single();
            var isNullOrWhiteSpaceResult = string.IsNullOrWhiteSpace(singleResult21);
            Assert.False(isNullOrWhiteSpaceResult);
            var readAsStringAsyncResult = request.Content!.ReadAsStringAsync();
            var getAwaiterResult = readAsStringAsyncResult.GetAwaiter();
            requestBody = getAwaiterResult.GetResult();
            var newGuidResult22 = Guid.NewGuid();
            var workerMessageDeliveryResponseDto = new WorkerMessageDeliveryResponseDto(WorkerMessageDeliveryStatusDto.Accepted, WorkerProtocol.Version, runId, newGuidResult22, "orders.created", null, "accepted");
            var jsonResponseResult23 = JsonResponse(workerMessageDeliveryResponseDto);
            return jsonResponseResult23;
        });
        using var client = CreateClient(handler);
        var response = await client.Messages.PublishMessageAsync(runId, "orders.created", new { orderId = "A10001" });
        Assert.Equal(WorkerMessageDeliveryStatusDto.Accepted, response.Status);
        var evaluatedValue24 = handler.Requests.Single().RequestUri!;
        var toStringResult = evaluatedValue24.ToString();
        Assert.Contains("orders.created", toStringResult, StringComparison.Ordinal);
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
            var singleResult27 = request.Headers.GetValues("Idempotency-Key")
                .Single();
            keys.Add(singleResult27);
            if (++attempt == 1)
            {
                var httpResponseMessage25 = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                return httpResponseMessage25;
            }

            var newGuidResult28 = Guid.NewGuid();
            var workerMessageDeliveryResponseDto29 = new WorkerMessageDeliveryResponseDto(WorkerMessageDeliveryStatusDto.Accepted, WorkerProtocol.Version, runId, newGuidResult28, "orders.created", null, "accepted");
            var jsonResponseResult30 = JsonResponse(workerMessageDeliveryResponseDto29);
            return jsonResponseResult30;
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
                Content = resolveByteArrayContent()
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = "artifact.txt"
            };
            return response;
            HttpContent resolveByteArrayContent()
            {
                var getBytesResult = Encoding.UTF8.GetBytes("artifact");
                var byteArrayContent = new ByteArrayContent(getBytesResult);
                return byteArrayContent;
            }
        });
        using var client = CreateClient(handler);
        var newGuidResult31 = Guid.NewGuid();
        await using var download = await client.Workpieces.DownloadAsync(newGuidResult31, "piece-1");
        using var reader = new StreamReader(download.Content);
        var readToEndAsyncResult = await reader.ReadToEndAsync();
        Assert.Equal("artifact", readToEndAsyncResult);
        Assert.Equal("text/plain", download.ContentType);
        Assert.Equal("artifact.txt", download.FileName);
    }

    private static SereinFlowClient CreateClient(HttpMessageHandler handler, string? apiKey = null, Action<SereinFlowClientOptions>? configure = null)
    {
        var options = new SereinFlowClientOptions
        {
            BaseAddress = BaseAddress,
            ApiKey = apiKey,
            RequestTimeout = TimeSpan.FromSeconds(5),
        };
        configure?.Invoke(options);
        var httpClient = new HttpClient(handler);
        var sereinFlowClient = new SereinFlowClient(httpClient, options);
        return sereinFlowClient;
    }

    private static HttpResponseMessage JsonResponse<T>(T value)
    {
        HttpResponseMessage evaluatedValue15 = new(HttpStatusCode.OK)
        {
            Content = resolveStringContent()
        };
        return evaluatedValue15;
        HttpContent resolveStringContent()
        {
            var createContractOptionsResult = SereinJsonSerialization.CreateContractOptions();
            var serializeResult = JsonSerializer.Serialize(value, createContractOptionsResult);
            var stringContent = new StringContent(serializeResult, Encoding.UTF8, "application/json");
            return stringContent;
        }
    }

    private sealed record OutputValue(int Value);
    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var responderResult = responder(request);
            var fromResultResult = Task.FromResult(responderResult);
            return fromResultResult;
        }
    }

    private sealed class CancellationAwareHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("The request was not cancelled.");
        }
    }
}

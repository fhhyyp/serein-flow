using System.Text;
using System.Text.Json;
using ScriptLang.Runtime;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Runtime;
using SereinFlow.Runtime.Abstractions;
using SereinFlow.Worker.Protocol;
using SereinFlow.Worker.Runner;

namespace SereinFlow.Worker.IntegrationTests;

public sealed class WorkerEventPublisherTests
{
    [Fact]
    public async Task LargeAuditGraphIsBoundedWithoutChangingBusinessValuesOrExecutionMetadata()
    {
        var large = new string ('中', 1_000_000);
        var outputs = new Dictionary<string, object?>
        {
            ["result"] = large
        };
        var executionId = Guid.NewGuid();
        var payload = new Dictionary<string, object?>
        {
            ["success"] = true,
            ["branch"] = "Success",
            ["outputs"] = outputs,
            ["inputs"] = new Dictionary<string, object?>
            {
                ["rows"] = Enumerable.Range(0, 100_000)
            },
            ["executionId"] = executionId,
            ["frameDepth"] = 0
        };
        await using var output = new MemoryStream();
        var operationResult1 = new MemoryStream();
        await using var transport = new StdioWorkerTransport(operationResult1, output, leaveOpen: true);
        await using var publisher = new WorkerEventPublisher(transport, Guid.Empty);
        var operationResult2 = new RuntimeEvent(Guid.Empty, 2, DateTimeOffset.UtcNow, NodeErrorCodes.Completed, "node", payload);
        await publisher.PublishAsync(operationResult2, CancellationToken.None);
        var operationResult3 = ReadMessages(output);
        var message = Assert.Single(operationResult3);
        var envelope = WorkerProtocolCodec.DeserializePayload<WorkerEventEnvelopeDto>(message);
        var operationResult4 = Encoding.UTF8.GetByteCount(envelope.PayloadJson) <= WorkerEventPublisher.MaximumAuditBytes;
        Assert.True(operationResult4);
        using var document = JsonDocument.Parse(envelope.PayloadJson);
        var operationResult5 = document.RootElement.GetProperty("success").GetBoolean();
        Assert.True(operationResult5);
        var operationResult6 = document.RootElement.GetProperty("executionId").GetGuid();
        Assert.Equal(executionId, operationResult6);
        Assert.Contains("Limit", envelope.PayloadJson, StringComparison.Ordinal);
        Assert.Same(large, outputs["result"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DebugPausePreservesItsShapeAndAuditsNestedScriptValues(bool hasExecutionIds)
    {
        var carrier = new LiveCarrier("secret-value");
        var clrValue = new ClrObjectValue(carrier);
        var textValue = StringValue.Create("中文");
        var properties = new Dictionary<string, Value> { { "nested", clrValue }, { "text", textValue } };
        var scriptValue = new ObjectValue(properties);
        var inputs = new Dictionary<string, object?> { { "script", scriptValue } };
        Guid? invocationId = hasExecutionIds ? Guid.Empty : null;
        Guid? executionId = hasExecutionIds ? Guid.Empty : null;
        var pause = new WorkerDebugPauseDto(Guid.Empty, Guid.Empty, "node", "Action", 3, inputs, 2, invocationId, executionId);
        await using var input = new MemoryStream();
        await using var output = new MemoryStream();
        await using var transport = new StdioWorkerTransport(input, output, leaveOpen: true);
        await using var publisher = new WorkerEventPublisher(transport, Guid.Empty);
        await publisher.PublishDebugPausedAsync(pause, 2, CancellationToken.None);
        var messages = ReadMessages(output);
        var message = Assert.Single(messages);
        var envelope = WorkerProtocolCodec.DeserializePayload<WorkerEventEnvelopeDto>(message);
        Assert.Equal(WorkerEventType.DebugPaused, envelope.EventType);
        Assert.Equal(2, envelope.Sequence);
        var carrierType = typeof(LiveCarrier).FullName;
        var idJson = hasExecutionIds ? "\"00000000-0000-0000-0000-000000000000\"" : "null";
        var expectedJson = $$$$"""
            {
              "debugSessionId":"00000000-0000-0000-0000-000000000000",
              "runId":"00000000-0000-0000-0000-000000000000",
              "nodeId":"node","nodeType":"Action","step":3,"frameDepth":2,
              "triggerInvocationId":{{{{idJson}}}},"executionId":{{{{idJson}}}},
              "inputs":{"script":{"nested":{"$kind":"ClrObject","clrType":"{{{{carrierType}}}}","serializable":false},"text":"中文"}}
            }
            """;
        using var expected = JsonDocument.Parse(expectedJson);
        using var actual = JsonDocument.Parse(envelope.PayloadJson);
        var matches = JsonElement.DeepEquals(expected.RootElement, actual.RootElement);
        Assert.True(matches, envelope.PayloadJson);
        Assert.Same(inputs, pause.Inputs);
        Assert.Same(scriptValue, inputs["script"]);
        Assert.DoesNotContain("secret-value", envelope.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("中文", envelope.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunnerEventsReachTheWorkerAsOrderedSafeAuditProjections()
    {
        var carrier = new LiveCarrier("secret-value");
        var rawValue = new ClrObjectValue(carrier);
        var executor = new LoggingExecutor(rawValue);
        var node = NodeDefinition.Create("node", NodeType.Action, "Action");
        var canvas = CanvasDefinition.Create("main", CanvasLifecycle.Main, [node], []);
        var flowId = Guid.NewGuid();
        var definition = FlowDefinition.Create(flowId, 1, [canvas], node.Id);
        await using var session = new FlowExecutionSession();
        // RunnerHost consumes sequence 1 for RunStarted before constructing the publisher.
        session.NextSequence();
        await using var input = new MemoryStream();
        await using var output = new MemoryStream();
        await using var transport = new StdioWorkerTransport(input, output, leaveOpen: true);
        await using var publisher = new WorkerEventPublisher(transport, session.RunId);
        var registry = new NodeExecutorRegistry([executor]);
        var planBuilder = new ExecutionPlanBuilder();
        var runner = new FlowRunner(planBuilder, registry, publisher);
        var result = await runner.RunAsync(definition, session);
        Assert.True(result.IsSuccess);
        Assert.Same(rawValue, result.Outputs["result"]);
        var messages = ReadMessages(output);
        var envelopes = messages.Select(message =>
        {
            var envelope = WorkerProtocolCodec.DeserializePayload<WorkerEventEnvelopeDto>(message);
            return envelope;
        }).ToArray();
        var sequences = envelopes.Select(envelope => envelope.Sequence).ToArray();
        Assert.Equal([2L, 3L, 4L], sequences);
        var types = envelopes.Select(envelope => envelope.EventType).ToArray();
        Assert.Equal([WorkerEventType.NodeStarted, WorkerEventType.Log, WorkerEventType.NodeCompleted], types);
        using var started = JsonDocument.Parse(envelopes[0].PayloadJson);
        using var log = JsonDocument.Parse(envelopes[1].PayloadJson);
        using var completed = JsonDocument.Parse(envelopes[2].PayloadJson);
        var startedId = started.RootElement.GetProperty("executionId").GetGuid();
        var logId = log.RootElement.GetProperty("executionId").GetGuid();
        var completedId = completed.RootElement.GetProperty("executionId").GetGuid();
        Assert.NotEqual(Guid.Empty, startedId);
        Assert.Equal(startedId, logId);
        Assert.Equal(startedId, completedId);
        var logKind = log.RootElement.GetProperty("value").GetProperty("nested").GetProperty("$kind").GetString();
        var outputKind = completed.RootElement.GetProperty("outputs").GetProperty("result").GetProperty("$kind").GetString();
        var branch = completed.RootElement.GetProperty("branch").GetString();
        Assert.Equal("ClrObject", logKind);
        Assert.Equal("ClrObject", outputKind);
        Assert.Equal("Success", branch);
        Assert.All(envelopes, envelope =>
        {
            Assert.DoesNotContain("secret-value", envelope.PayloadJson, StringComparison.Ordinal);
        });
    }

    private static WorkerMessage[] ReadMessages(MemoryStream output)
    {
        var bytes = output.ToArray();
        var text = Encoding.UTF8.GetString(bytes);
        var messages = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(WorkerProtocolCodec.Deserialize)
            .ToArray();
        return messages;
    }

    private sealed record LiveCarrier(string Secret);

    private sealed class LoggingExecutor(ClrObjectValue rawValue) : INodeExecutor
    {
        public NodeType NodeType => NodeType.Action;

        public async ValueTask<NodeExecutionResult> ExecuteAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
        {
            var logValue = new Dictionary<string, object?> { { "nested", rawValue } };
            var log = new NodeExecutionLogEntry("info", "中文", logValue);
            await request.Runtime!.EventSink.PublishLogAsync(log, cancellationToken);
            var outputs = new Dictionary<string, object?> { { "result", rawValue } };
            var result = NodeExecutionResult.Success(outputs);
            return result;
        }
    }
}

using System.Text.Json;
using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Runtime;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Runtime.Tests;

public sealed class RuntimeEventContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateWebOptions();

    [Fact]
    public void EventPayloadsPreserveTheirExistingJsonShapes()
    {
        var empty = new Dictionary<string, object?>(StringComparer.Ordinal);
        var success = NodeExecutionResult.Success();
        var normalResult = NodeResultEventPayload.FromResult(success, empty, null);
        var globalResult = NodeResultEventPayload.FromResult(success, empty, null, global: true);
        var log = new NodeExecutionLogEntry("info", "中文", null);
        (RuntimeEventPayload Payload, string Json)[] cases =
        [
            (new NodeStartedEventPayload(3, null),
                """{"step":3,"triggerInvocationId":null,"frameDepth":0}"""),
            (new NodeStartedEventPayload(3, null, Global: true),
                """{"global":true,"step":3,"triggerInvocationId":null,"frameDepth":0}"""),
            (normalResult,
                """{"success":true,"branch":"Success","errorCode":null,"errorMessage":null,"inputs":{},"outputs":{},"triggerInvocationId":null,"frameDepth":0}"""),
            (globalResult,
                """{"global":true,"success":true,"branch":"Success","errorCode":null,"errorMessage":null,"inputs":{},"outputs":{},"triggerInvocationId":null,"frameDepth":0}"""),
            (new NodeResultEventPayload(false, null, "invalid", "Invalid executor", empty, empty, null, IncludeInvocationId: false),
                """{"success":false,"errorCode":"invalid","errorMessage":"Invalid executor","inputs":{},"outputs":{},"frameDepth":0}"""),
            (new NodeResultEventPayload(false, ExecutionBranch.Error, "limit", "Limit exceeded", empty, empty, null, Global: true, IncludeInvocationId: false),
                """{"global":true,"success":false,"branch":"Error","errorCode":"limit","errorMessage":"Limit exceeded","inputs":{},"outputs":{},"frameDepth":0}"""),
            (new NodeResultEventPayload(false, ExecutionBranch.Failure, "binding", "Missing input", empty, empty, null, Global: true),
                """{"global":true,"success":false,"branch":"Failure","errorCode":"binding","errorMessage":"Missing input","inputs":{},"outputs":{},"triggerInvocationId":null,"frameDepth":0}"""),
            (new NodeResultEventPayload(false, ExecutionBranch.Error, "failed", "Execution failed", empty, empty, null, Global: true),
                """{"global":true,"success":false,"branch":"Error","errorCode":"failed","errorMessage":"Execution failed","inputs":{},"outputs":{},"triggerInvocationId":null,"frameDepth":0}"""),
            (new TriggerReceivedEventPayload(null, "trigger", empty),
                """{"triggerInvocationId":null,"flipflopNodeId":"trigger","inputs":{},"frameDepth":0}"""),
            (new TriggerQueuedEventPayload(null, "trigger", 2),
                """{"triggerInvocationId":null,"flipflopNodeId":"trigger","queuePosition":2,"frameDepth":0}"""),
            (new TriggerRejectedEventPayload(null, "trigger", "queue_full", 8),
                """{"triggerInvocationId":null,"flipflopNodeId":"trigger","reason":"queue_full","maximumQueuedTriggers":8,"frameDepth":0}"""),
            (new TriggerAdmittedEventPayload(null, "trigger"),
                """{"triggerInvocationId":null,"flipflopNodeId":"trigger","frameDepth":0}"""),
            (new TriggerCompletedEventPayload(null, "trigger", success),
                """{"triggerInvocationId":null,"flipflopNodeId":"trigger","success":true,"branch":"Success","errorCode":null,"errorMessage":null,"frameDepth":0}"""),
            (new TriggerFailedEventPayload(null, "trigger", "failed", "Trigger failed"),
                """{"triggerInvocationId":null,"flipflopNodeId":"trigger","errorCode":"failed","errorMessage":"Trigger failed","frameDepth":0}"""),
            (new NodeLogEventPayload(log),
                """{"level":"info","message":"中文","value":null,"frameDepth":0}""")
        ];
        foreach (var testCase in cases)
        {
            var values = testCase.Payload.ToDictionary(0, null);
            var actual = JsonSerializer.SerializeToElement(values, JsonOptions);
            using var expected = JsonDocument.Parse(testCase.Json);
            var matches = JsonElement.DeepEquals(expected.RootElement, actual);
            var payloadName = testCase.Payload.GetType().Name;
            Assert.True(matches, payloadName);
        }
    }

    [Fact]
    public void NodeResultsPreferCapturedInputsAndTransferOutputsWithoutConvertingValues()
    {
        var liveObject = new object();
        var inputs = new Dictionary<string, object?> { { "value", liveObject } };
        var outputs = new Dictionary<string, object?> { { "value", liveObject } };
        var transferOutputs = new Dictionary<string, object?> { { "value", "audit" } };
        var fallbackInputs = new Dictionary<string, object?>();
        var result = new NodeExecutionResult(true, outputs, Inputs: inputs, TransferOutputs: transferOutputs);
        var model = NodeResultEventPayload.FromResult(result, fallbackInputs, Guid.Empty);
        var payload = model.ToDictionary(2, Guid.Empty);
        Assert.Same(inputs, payload["inputs"]);
        Assert.Same(transferOutputs, payload["outputs"]);
        Assert.Same(liveObject, inputs["value"]);
        var fallbackResult = NodeExecutionResult.Success(outputs);
        var fallbackModel = NodeResultEventPayload.FromResult(fallbackResult, fallbackInputs, null);
        var fallbackPayload = fallbackModel.ToDictionary(0, null);
        Assert.Same(fallbackInputs, fallbackPayload["inputs"]);
        Assert.Same(outputs, fallbackPayload["outputs"]);
    }

    [Fact]
    public async Task FactoryPreservesSequenceIdentityFrameAndExecutionIdPresence()
    {
        await using var session = new FlowExecutionSession();
        var value = new object();
        var entry = new NodeExecutionLogEntry("info", "Raw value", value);
        var model = new NodeLogEventPayload(entry);
        var withoutExecutionId = RuntimeEventFactory.Create(session, NodeErrorCodes.Log, "node", model);
        var withExecutionId = RuntimeEventFactory.Create(session, NodeErrorCodes.Log, "node", model, Guid.Empty);
        Assert.Equal(session.RunId, withoutExecutionId.RunId);
        Assert.Equal(1, withoutExecutionId.Sequence);
        Assert.Equal(2, withExecutionId.Sequence);
        Assert.Equal(NodeErrorCodes.Log, withExecutionId.Type);
        Assert.Equal("node", withExecutionId.NodeId);
        Assert.Equal(0, withoutExecutionId.Payload["frameDepth"]);
        Assert.DoesNotContain("executionId", withoutExecutionId.Payload.Keys);
        Assert.Equal(Guid.Empty, withExecutionId.Payload["executionId"]);
        Assert.Same(value, withExecutionId.Payload["value"]);
        var nestedPayload = model.ToDictionary(2, null);
        Assert.Equal(2, nestedPayload["frameDepth"]);
    }
}

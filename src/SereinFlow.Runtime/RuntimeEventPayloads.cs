using SereinFlow.Domain;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Runtime;

/// <summary>
/// Typed event construction stays inside the runtime. The explicit dictionary
/// adapter preserves RuntimeEvent's public contract and recursive audit handling
/// of raw CLR/ScriptLang values at the Worker boundary.
/// 内部以具名类型构造事件，再显式映射到公共字典合同；原始值在 Worker 边界审计。
/// </summary>
internal abstract record RuntimeEventPayload
{
    internal Dictionary<string, object?> ToDictionary(int frameDepth, Guid? executionId)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal);
        WriteTo(payload);
        payload["frameDepth"] = frameDepth;
        if (executionId is Guid id)
        {
            payload["executionId"] = id;
        }

        return payload;
    }

    internal abstract void WriteTo(Dictionary<string, object?> payload);
}

/// <summary>
/// Represents node started event payload data used by SereinFlow.
/// 表示 SereinFlow 使用的节点Started事件载荷数据。
/// </summary>
internal sealed record NodeStartedEventPayload(int Step, Guid? TriggerInvocationId, bool? Global = null) : RuntimeEventPayload
{
    internal override void WriteTo(Dictionary<string, object?> payload)
    {
        if (Global is not null)
        {
            payload["global"] = Global.Value;
        }

        payload["step"] = Step;
        payload["triggerInvocationId"] = TriggerInvocationId;
    }
}

/// <summary>
/// Represents node result event payload data used by SereinFlow.
/// 表示 SereinFlow 使用的节点结果事件载荷数据。
/// </summary>
internal sealed record NodeResultEventPayload(
    bool Success,
    ExecutionBranch? Branch,
    string? ErrorCode,
    string? ErrorMessage,
    IReadOnlyDictionary<string, object?> Inputs,
    IReadOnlyDictionary<string, object?> Outputs,
    Guid? TriggerInvocationId,
    bool? Global = null,
    bool IncludeInvocationId = true) : RuntimeEventPayload
{
    internal static NodeResultEventPayload FromResult(NodeExecutionResult result, IReadOnlyDictionary<string, object?> fallbackInputs, Guid? invocationId, bool? global = null)
    {
        var payload = new NodeResultEventPayload(result.IsSuccess, result.NextBranch, result.ErrorCode, result.ErrorMessage,
            result.Inputs ?? fallbackInputs, result.TransferOutputs ?? result.Outputs, invocationId, global);
        return payload;
    }

    internal override void WriteTo(Dictionary<string, object?> payload)
    {
        if (Global is not null)
        {
            payload["global"] = Global.Value;
        }

        payload["success"] = Success;
        if (Branch is not null)
        {
            payload["branch"] = Branch.Value.ToString();
        }

        payload["errorCode"] = ErrorCode;
        payload["errorMessage"] = ErrorMessage;
        payload["inputs"] = Inputs;
        payload["outputs"] = Outputs;
        if (IncludeInvocationId)
        {
            payload["triggerInvocationId"] = TriggerInvocationId;
        }
    }
}

/// <summary>
/// Represents trigger event payload data used by SereinFlow.
/// 表示 SereinFlow 使用的触发器事件载荷数据。
/// </summary>
internal abstract record TriggerEventPayload(Guid? TriggerInvocationId, string FlipflopNodeId) : RuntimeEventPayload
{
    internal sealed override void WriteTo(Dictionary<string, object?> payload)
    {
        payload["triggerInvocationId"] = TriggerInvocationId;
        payload["flipflopNodeId"] = FlipflopNodeId;
        WriteTriggerFields(payload);
    }

    protected virtual void WriteTriggerFields(Dictionary<string, object?> payload)
    {
    }
}

/// <summary>
/// Represents trigger received event payload data used by SereinFlow.
/// 表示 SereinFlow 使用的触发器Received事件载荷数据。
/// </summary>
internal sealed record TriggerReceivedEventPayload(Guid? TriggerInvocationId, string FlipflopNodeId, IReadOnlyDictionary<string, object?> Inputs)
    : TriggerEventPayload(TriggerInvocationId, FlipflopNodeId)
{
    protected override void WriteTriggerFields(Dictionary<string, object?> payload)
    {
        payload["inputs"] = Inputs;
    }
}

/// <summary>
/// Represents trigger queued event payload data used by SereinFlow.
/// 表示 SereinFlow 使用的触发器Queued事件载荷数据。
/// </summary>
internal sealed record TriggerQueuedEventPayload(Guid? TriggerInvocationId, string FlipflopNodeId, int QueuePosition)
    : TriggerEventPayload(TriggerInvocationId, FlipflopNodeId)
{
    protected override void WriteTriggerFields(Dictionary<string, object?> payload)
    {
        payload["queuePosition"] = QueuePosition;
    }
}

/// <summary>
/// Represents trigger rejected event payload data used by SereinFlow.
/// 表示 SereinFlow 使用的触发器Rejected事件载荷数据。
/// </summary>
internal sealed record TriggerRejectedEventPayload(Guid? TriggerInvocationId, string FlipflopNodeId, string Reason, int MaximumQueuedTriggers)
    : TriggerEventPayload(TriggerInvocationId, FlipflopNodeId)
{
    protected override void WriteTriggerFields(Dictionary<string, object?> payload)
    {
        payload["reason"] = Reason;
        payload["maximumQueuedTriggers"] = MaximumQueuedTriggers;
    }
}

/// <summary>
/// Represents trigger admitted event payload data used by SereinFlow.
/// 表示 SereinFlow 使用的触发器Admitted事件载荷数据。
/// </summary>
internal sealed record TriggerAdmittedEventPayload(Guid? TriggerInvocationId, string FlipflopNodeId)
    : TriggerEventPayload(TriggerInvocationId, FlipflopNodeId);

/// <summary>
/// Represents trigger completed event payload data used by SereinFlow.
/// 表示 SereinFlow 使用的触发器Completed事件载荷数据。
/// </summary>
internal sealed record TriggerCompletedEventPayload(Guid? TriggerInvocationId, string FlipflopNodeId, NodeExecutionResult Result)
    : TriggerEventPayload(TriggerInvocationId, FlipflopNodeId)
{
    protected override void WriteTriggerFields(Dictionary<string, object?> payload)
    {
        payload["success"] = Result.IsSuccess;
        payload["branch"] = Result.NextBranch.ToString();
        payload["errorCode"] = Result.ErrorCode;
        payload["errorMessage"] = Result.ErrorMessage;
    }
}

/// <summary>
/// Represents trigger failed event payload data used by SereinFlow.
/// 表示 SereinFlow 使用的触发器Failed事件载荷数据。
/// </summary>
internal sealed record TriggerFailedEventPayload(Guid? TriggerInvocationId, string FlipflopNodeId, string ErrorCode, string ErrorMessage)
    : TriggerEventPayload(TriggerInvocationId, FlipflopNodeId)
{
    protected override void WriteTriggerFields(Dictionary<string, object?> payload)
    {
        payload["errorCode"] = ErrorCode;
        payload["errorMessage"] = ErrorMessage;
    }
}

/// <summary>
/// Represents node log event payload data used by SereinFlow.
/// 表示 SereinFlow 使用的节点Log事件载荷数据。
/// </summary>
internal sealed record NodeLogEventPayload(NodeExecutionLogEntry Entry) : RuntimeEventPayload
{
    internal override void WriteTo(Dictionary<string, object?> payload)
    {
        payload["level"] = Entry.Level;
        payload["message"] = Entry.Message;
        payload["value"] = Entry.Value;
    }
}

internal static class RuntimeEventFactory
{
    internal static RuntimeEvent Create(FlowExecutionSession session, string type, string nodeId, RuntimeEventPayload payload, Guid? executionId = null)
    {
        var values = payload.ToDictionary(session.FrameDepth, executionId);
        var sequence = session.NextSequence();
        var runtimeEvent = new RuntimeEvent(session.RunId, sequence, DateTimeOffset.UtcNow, type, nodeId, values);
        return runtimeEvent;
    }
}

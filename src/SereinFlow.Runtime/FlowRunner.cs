using SereinFlow.Domain;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Runtime;

public sealed class FlowRunner
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyInputs =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    private readonly ExecutionPlanBuilder _planBuilder;
    private readonly NodeExecutorRegistry _executors;
    private readonly IRunEventPublisher _eventPublisher;
    private readonly DataConnectionResolver _dataResolver;
    private readonly IExecutionGate _executionGate;
    private readonly DebugInvocationScheduler? _debugInvocationScheduler;

    public FlowRunner(
        ExecutionPlanBuilder planBuilder,
        NodeExecutorRegistry executors,
        IRunEventPublisher? eventPublisher = null,
        DataConnectionResolver? dataResolver = null,
        IExecutionGate? executionGate = null,
        DebugInvocationScheduler? debugInvocationScheduler = null)
    {
        _planBuilder = planBuilder;
        _executors = executors;
        _eventPublisher = eventPublisher ?? new NullRunEventPublisher();
        _dataResolver = dataResolver ?? new DataConnectionResolver();
        _executionGate = executionGate ?? NoopExecutionGate.Instance;
        _debugInvocationScheduler = debugInvocationScheduler;

        foreach (var flowCall in executors.GetAll().OfType<IFlowCallExecutorConfiguration>())
            flowCall.Configure(ExecuteFlowCallAsync);
    }

    public async ValueTask<NodeExecutionResult> RunAsync(
        FlowDefinition definition,
        FlowExecutionSession session,
        CancellationToken cancellationToken = default)
    {
        if (definition is null)
            throw new ArgumentNullException(nameof(definition), "The flow definition cannot be null. 流程定义不能为空。");
        if (session is null)
            throw new ArgumentNullException(nameof(session), "The runtime session cannot be null. 运行时会话不能为空。");

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(session.CancellationToken, cancellationToken);
        var plan = _planBuilder.Build(definition);
        session.AttachPlan(plan);

        try
        {

            var globalFlipflops = plan.Nodes.Values
                .Where(node => node.Type == NodeType.Flipflop && !plan.HasIncomingExecution(node.Id))
                .ToArray();
            var globalTasks = globalFlipflops
                .Select(node => RunGlobalFlipflopAsync(node, plan, session, linkedCancellation.Token))
                .ToArray();

            var mainResult = string.IsNullOrWhiteSpace(definition.EntryNodeId)
                ? NodeExecutionResult.Success()
                : plan.Nodes.TryGetValue(definition.EntryNodeId, out var entry)
                    && entry.Type == NodeType.Flipflop
                    && globalFlipflops.Any(node => node.Id == entry.Id)
                    ? NodeExecutionResult.Success()
                    : await RunStackAsync(definition.EntryNodeId, plan, session, linkedCancellation.Token);

            if (globalTasks.Length > 0)
            {
                try
                {
                    var globalResults = await Task.WhenAll(globalTasks);
                    var globalFailure = globalResults.FirstOrDefault(result => !result.IsSuccess);
                    if (globalFailure is not null)
                    {
                        if (globalFailure.ErrorCode == "worker.cancelled" && linkedCancellation.IsCancellationRequested)
                            throw new OperationCanceledException(linkedCancellation.Token);
                        return globalFailure;
                    }
                }
                catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
                {
                    throw;
                }
            }

            // A listener can observe cancellation before entering its wait loop
            // and finish without throwing. Preserve the run lifecycle outcome in
            // that narrow race instead of reporting a successful flow run.
            // 监听器可能在进入等待循环前观察到取消并正常结束；此时仍应保留运行的取消终态，
            // 不能误报为成功。
            linkedCancellation.Token.ThrowIfCancellationRequested();
            return mainResult;
        }
        finally
        {
            if (_debugInvocationScheduler is not null)
                await _debugInvocationScheduler.StopAndDrainAsync();
        }
    }

    public ValueTask<NodeExecutionResult> RunFromNodeAsync(
        string nodeId,
        FlowExecutionSession session,
        CancellationToken cancellationToken = default)
    {
        if (session.Plan is null)
            throw new InvalidOperationException("The execution session is not attached to a plan. 运行会话尚未绑定执行计划。");
        return RunStackAsync(nodeId, session.Plan, session, cancellationToken);
    }

    private async ValueTask<NodeExecutionResult> RunStackAsync(
        string startNodeId,
        ExecutionPlan plan,
        FlowExecutionSession session,
        CancellationToken cancellationToken)
    {
        var stack = new Stack<string>();
        stack.Push(startNodeId);
        var lastResult = NodeExecutionResult.Success();

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentNodeId = stack.Pop();
            if (!plan.Nodes.TryGetValue(currentNodeId, out var node))
                return NodeExecutionResult.Error("flow.node_missing", $"Node '{currentNodeId}' does not exist. 节点“{currentNodeId}”不存在。");

            if (!session.TryBeginStep(node.Id, out var step, out var executionId, out var limitError))
                return NodeExecutionResult.Error(limitError!, "The flow execution limit was exceeded. 流程执行限制已超出。");

            await PublishAsync(session, "node.started", node.Id, new Dictionary<string, object?>
            {
                ["step"] = step,
                ["triggerInvocationId"] = session.InvocationId
            }, executionId);

            IReadOnlyDictionary<string, object?> inputs = EmptyInputs;
            try
            {
                inputs = _dataResolver.Resolve(node, plan, session);
                var gateDecision = await _executionGate.BeforeNodeAsync(
                    new NodeExecutionBoundary(
                        session.RunId,
                        node.Id,
                        node.Type,
                        step,
                        CopyInputs(inputs),
                        session.FrameDepth,
                        session.InvocationId,
                        executionId),
                    cancellationToken);
                if (gateDecision == ExecutionGateDecision.Cancel)
                {
                    session.Cancel();
                    throw new OperationCanceledException(
                        "The debug execution was cancelled. 调试执行已取消。",
                        session.CancellationToken);
                }

                var executor = _executors.Get(node.Type);
                lastResult = await executor.ExecuteAsync(
                    CreateExecutionRequest(node, plan, session, inputs, step, executionId),
                    cancellationToken);
                lastResult = AttachInputs(lastResult, inputs);
            }
            catch (FlowDataBindingException exception)
            {
                inputs = exception.ResolvedInputs;
                lastResult = NodeExecutionResult.Failure(exception.Code, exception.Message) with
                {
                    Inputs = CopyInputs(inputs)
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                lastResult = NodeExecutionResult.Error(
                    "node.execution_failed",
                    $"Node '{node.Id}' execution failed. 节点“{node.Id}”执行失败。 {exception.Message}") with
                {
                    Inputs = CopyInputs(inputs)
                };
            }

            foreach (var output in lastResult.Outputs)
            {
                session.Write($"{node.Id}.{output.Key}", output.Value);
                var port = node.Ports.FirstOrDefault(item => item.Name == output.Key || item.Id == output.Key);
                if (port is not null)
                    session.Write($"{node.Id}.{port.Id}", output.Value);
            }
            if (lastResult.Outputs.Count == 1)
                session.Write($"{node.Id}.data-out", lastResult.Outputs.Values.First());

            await PublishAsync(
                session,
                lastResult.IsSuccess
                    ? "node.completed"
                    : lastResult.NextBranch == ExecutionBranch.Error ? "node.error" : "node.failed",
                node.Id,
                new Dictionary<string, object?>
                {
                    ["success"] = lastResult.IsSuccess,
                    ["branch"] = lastResult.NextBranch.ToString(),
                    ["errorCode"] = lastResult.ErrorCode,
                    ["errorMessage"] = lastResult.ErrorMessage,
                    ["inputs"] = lastResult.Inputs ?? inputs,
                    ["outputs"] = lastResult.TransferOutputs ?? lastResult.Outputs,
                    ["triggerInvocationId"] = session.InvocationId
                }, executionId);

            var branch = lastResult.NextBranch;
            var selected = plan.GetOutgoing(node.Id, branch);
            PushReverse(stack, selected);
        }

        return lastResult;
    }

    private async Task<NodeExecutionResult> RunGlobalFlipflopAsync(
        NodeDefinition node,
        ExecutionPlan plan,
        FlowExecutionSession session,
        CancellationToken cancellationToken)
    {
        var executor = _executors.Get(node.Type);
        if (executor is not IGlobalFlipflopExecutor trigger)
        {
            await PublishAsync(session, "node.failed", node.Id, new Dictionary<string, object?>
            {
                ["success"] = false,
                ["inputs"] = EmptyInputs,
                ["outputs"] = EmptyInputs,
                ["errorCode"] = "flipflop.executor_invalid",
                ["errorMessage"] = "The global Flipflop executor is invalid. 全局 Flipflop 执行器无效。"
            });
            return NodeExecutionResult.Error("flipflop.executor_invalid", "The global Flipflop executor is invalid. 全局 Flipflop 执行器无效。");
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            IReadOnlyDictionary<string, object?> inputs = EmptyInputs;
            Guid? invocationId = null;
            var step = 0;
            var executionId = Guid.Empty;
            try
            {
                if (!session.TryBeginStep(node.Id, out step, out executionId, out var limitError))
                {
                    await PublishAsync(session, "node.failed", node.Id, new Dictionary<string, object?>
                    {
                        ["global"] = true,
                        ["success"] = false,
                        ["branch"] = ExecutionBranch.Error.ToString(),
                        ["inputs"] = EmptyInputs,
                        ["outputs"] = EmptyInputs,
                        ["errorCode"] = limitError,
                        ["errorMessage"] = "The global Flipflop execution limit was exceeded. 全局 Flipflop 执行限制已超出。"
                    });
                    return NodeExecutionResult.Error(limitError!, "The global Flipflop execution limit was exceeded. 全局 Flipflop 执行限制已超出。");
                }

                // Every trigger gets an isolated value context. In debug mode
                // the child outlives this listener-loop iteration because its
                // downstream work is owned by the FIFO scheduler.
                // 每次触发都有隔离值上下文。调试模式下，子会话会跨越本次监听循环，
                // 因为其下游工作由 FIFO 调度器拥有。
                invocationId = _debugInvocationScheduler is null ? null : Guid.NewGuid();
                var triggerSession = session.CreateChild(invocationId);
                var triggerSessionOwnedByListener = true;
                try
                {
                    await PublishAsync(session, "node.started", node.Id, new Dictionary<string, object?>
                    {
                        ["global"] = true,
                        ["step"] = step,
                        ["triggerInvocationId"] = invocationId
                    }, executionId);
                    inputs = _dataResolver.Resolve(node, plan, triggerSession);
                    var result = await trigger.WaitForTriggerAsync(
                        CreateExecutionRequest(node, plan, triggerSession, inputs, step, executionId),
                        cancellationToken);
                    result = AttachInputs(result, inputs);
                    foreach (var output in result.Outputs)
                        triggerSession.Write($"{node.Id}.{output.Key}", output.Value);
                    if (result.Outputs.Count == 1)
                        triggerSession.Write($"{node.Id}.data-out", result.Outputs.Values.First());

                    await PublishAsync(
                        session,
                        result.IsSuccess
                            ? "node.completed"
                            : result.NextBranch == ExecutionBranch.Error ? "node.error" : "node.failed",
                        node.Id,
                        new Dictionary<string, object?>
                    {
                        ["global"] = true,
                        ["success"] = result.IsSuccess,
                        ["branch"] = result.NextBranch.ToString(),
                        ["errorCode"] = result.ErrorCode,
                        ["errorMessage"] = result.ErrorMessage,
                        ["inputs"] = result.Inputs ?? inputs,
                        ["outputs"] = result.TransferOutputs ?? result.Outputs,
                        ["triggerInvocationId"] = invocationId
                    }, executionId);

                    if (_debugInvocationScheduler is null)
                    {
                        foreach (var connection in plan.GetOutgoing(node.Id, result.NextBranch))
                            await RunStackAsync(connection.ToNodeId, plan, triggerSession, cancellationToken);
                    }
                    else
                    {
                        var debugInputs = CopyInputs(result.Inputs ?? inputs);
                        await PublishAsync(session, "debug.trigger.received", node.Id, new Dictionary<string, object?>
                        {
                            ["triggerInvocationId"] = invocationId,
                            ["flipflopNodeId"] = node.Id,
                            ["inputs"] = debugInputs
                        });
                        if (_debugInvocationScheduler.TrySchedule(
                            () => ExecuteScheduledTriggerAsync(
                                node,
                                plan,
                                session,
                                triggerSession,
                                result.NextBranch,
                                debugInputs,
                                step,
                                executionId,
                                cancellationToken),
                            () => triggerSession.DisposeAsync().AsTask(),
                            out var queuePosition))
                        {
                            triggerSessionOwnedByListener = false;
                            if (queuePosition > 0)
                            {
                                await PublishAsync(session, "debug.trigger.queued", node.Id, new Dictionary<string, object?>
                                {
                                    ["triggerInvocationId"] = invocationId,
                                    ["flipflopNodeId"] = node.Id,
                                    ["queuePosition"] = queuePosition
                                });
                            }
                        }
                        else
                        {
                            await PublishAsync(session, "debug.trigger.rejected", node.Id, new Dictionary<string, object?>
                            {
                                ["triggerInvocationId"] = invocationId,
                                ["flipflopNodeId"] = node.Id,
                                ["reason"] = "debug.trigger.queue_full",
                                ["maximumQueuedTriggers"] = _debugInvocationScheduler.MaximumQueuedInvocations
                            });
                        }
                    }
                }
                finally
                {
                    if (triggerSessionOwnedByListener)
                        await triggerSession.DisposeAsync();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (FlowDataBindingException exception)
            {
                inputs = exception.ResolvedInputs;
                await PublishAsync(session, "node.failed", node.Id, new Dictionary<string, object?>
                {
                    ["global"] = true,
                    ["success"] = false,
                    ["branch"] = ExecutionBranch.Failure.ToString(),
                    ["inputs"] = inputs,
                    ["outputs"] = EmptyInputs,
                    ["errorCode"] = exception.Code,
                    ["errorMessage"] = exception.Message,
                    ["triggerInvocationId"] = invocationId
                }, executionId);
                foreach (var connection in plan.GetOutgoing(node.Id, ExecutionBranch.Failure))
                    await RunStackAsync(connection.ToNodeId, plan, session, cancellationToken);
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
            catch (Exception exception)
            {
                await PublishAsync(session, "node.failed", node.Id, new Dictionary<string, object?>
                {
                    ["global"] = true,
                    ["success"] = false,
                    ["branch"] = ExecutionBranch.Error.ToString(),
                    ["inputs"] = inputs,
                    ["outputs"] = EmptyInputs,
                    ["errorCode"] = "flipflop.execution_failed",
                    ["errorMessage"] = $"Global Flipflop execution failed. 全局 Flipflop 执行失败。 {exception.Message}",
                    ["triggerInvocationId"] = invocationId
                }, executionId);
                // An exception escaping a global listener still follows the
                // node's Error branch before the listener waits again.
                // 全局监听器发生未处理异常时，先进入 Error 分支，再继续等待下一次触发。
                foreach (var connection in plan.GetOutgoing(node.Id, ExecutionBranch.Error))
                    await RunStackAsync(connection.ToNodeId, plan, session, cancellationToken);
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
        }

        return NodeExecutionResult.Success();
    }

    private async Task ExecuteScheduledTriggerAsync(
        NodeDefinition flipflopNode,
        ExecutionPlan plan,
        FlowExecutionSession rootSession,
        FlowExecutionSession triggerSession,
        ExecutionBranch branch,
        IReadOnlyDictionary<string, object?> inputs,
        int step,
        Guid executionId,
        CancellationToken cancellationToken)
    {
        await using var ownedTriggerSession = triggerSession;
        var invocationId = triggerSession.InvocationId;
        try
        {
            await PublishAsync(rootSession, "debug.trigger.admitted", flipflopNode.Id, new Dictionary<string, object?>
            {
                ["triggerInvocationId"] = invocationId,
                ["flipflopNodeId"] = flipflopNode.Id
            });
            var decision = await _executionGate.BeforeNodeAsync(
                new NodeExecutionBoundary(
                    rootSession.RunId,
                    flipflopNode.Id,
                    flipflopNode.Type,
                    step,
                    CopyInputs(inputs),
                    triggerSession.FrameDepth,
                    invocationId,
                    executionId),
                cancellationToken);
            if (decision == ExecutionGateDecision.Cancel)
            {
                rootSession.Cancel();
                throw new OperationCanceledException(
                    "The debug execution was cancelled. 调试执行已取消。",
                    rootSession.CancellationToken);
            }

            var lastResult = NodeExecutionResult.Success();
            foreach (var connection in plan.GetOutgoing(flipflopNode.Id, branch))
                lastResult = await RunStackAsync(connection.ToNodeId, plan, triggerSession, cancellationToken);

            await PublishAsync(rootSession, "debug.trigger.completed", flipflopNode.Id, new Dictionary<string, object?>
            {
                ["triggerInvocationId"] = invocationId,
                ["flipflopNodeId"] = flipflopNode.Id,
                ["success"] = lastResult.IsSuccess,
                ["branch"] = lastResult.NextBranch.ToString(),
                ["errorCode"] = lastResult.ErrorCode,
                ["errorMessage"] = lastResult.ErrorMessage
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || rootSession.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await PublishAsync(rootSession, "debug.trigger.failed", flipflopNode.Id, new Dictionary<string, object?>
            {
                ["triggerInvocationId"] = invocationId,
                ["flipflopNodeId"] = flipflopNode.Id,
                ["errorCode"] = "debug.trigger.execution_failed",
                ["errorMessage"] = exception.Message
            });
        }
    }

    private async ValueTask<NodeExecutionResult> ExecuteFlowCallAsync(
        NodeExecutionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Context is not FlowExecutionSession session || session.Plan is null)
            return NodeExecutionResult.Error("flowcall.context_invalid", "FlowCall requires a runtime execution session. FlowCall 需要运行时执行会话。");

        var target = request.Node.Runtime?.TargetNodeId;
        if (string.IsNullOrWhiteSpace(target))
            return NodeExecutionResult.Error("flowcall.target_missing", "FlowCall target node is missing. FlowCall 目标节点缺失。");

        if (!session.Plan.Nodes.TryGetValue(target, out var targetNode))
            return NodeExecutionResult.Error("flowcall.target_missing", "FlowCall target node is missing. FlowCall 目标节点缺失。");

        var callInputs = ResolveFlowCallInputs(request.Node, targetNode, request.Inputs);
        await using var callFrame = session.CreateFlowCallFrame(target, callInputs);
        var result = await RunFromNodeAsync(target, callFrame, cancellationToken);
        if (!result.IsSuccess)
            return new NodeExecutionResult(
                false,
                result.Outputs,
                result.NextBranch,
                result.ErrorCode,
                result.ErrorMessage,
                TransferOutputs: result.TransferOutputs);

        var staticType = request.Node.Runtime?.StaticReturnType;
        if (!string.IsNullOrWhiteSpace(staticType)
            && !string.Equals(staticType, "void", StringComparison.OrdinalIgnoreCase)
            && request.Node.Runtime?.IsDynamicReturnType != true
            && result.Outputs.Values.FirstOrDefault() is { } actual
            && !IsCompatibleReturnType(actual, staticType))
        {
            return NodeExecutionResult.Error(
                "flowcall.return_type_mismatch",
                $"FlowCall returned '{actual.GetType().FullName}' but the static return type is '{staticType}'. FlowCall 返回类型与静态返回类型不一致。");
        }

        return NodeExecutionResult.Success(result.Outputs) with
        {
            TransferOutputs = result.TransferOutputs
        };
    }

    private static IReadOnlyDictionary<string, object?> ResolveFlowCallInputs(
        NodeDefinition callNode,
        NodeDefinition targetNode,
        IReadOnlyDictionary<string, object?> inputs)
    {
        var mappings = callNode.Runtime?.FlowCallParameterBindings ?? [];
        var targetInputs = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (mappings.Count == 0)
        {
            foreach (var targetParameter in targetNode.Parameters)
            {
                if (inputs.TryGetValue(targetParameter.Id, out var value))
                    targetInputs[targetParameter.Id] = value;
            }
            return targetInputs;
        }

        foreach (var mapping in mappings)
        {
            if (inputs.TryGetValue(mapping.CallParameterId, out var value))
                targetInputs[mapping.TargetParameterId] = value;
        }
        return targetInputs;
    }

    private static bool IsCompatibleReturnType(object value, string staticType)
        => string.Equals(value.GetType().FullName, staticType, StringComparison.Ordinal)
            || string.Equals(value.GetType().Name, staticType, StringComparison.Ordinal)
            || (staticType.Equals("System.Object", StringComparison.Ordinal) && value is not null);

    private static void PushReverse(Stack<string> stack, IReadOnlyList<ConnectionDefinition> connections)
    {
        for (var index = connections.Count - 1; index >= 0; index--)
            stack.Push(connections[index].ToNodeId);
    }

    private NodeExecutionRequest CreateExecutionRequest(
        NodeDefinition node,
        ExecutionPlan plan,
        FlowExecutionSession session,
        IReadOnlyDictionary<string, object?> inputs,
        int step,
        Guid executionId)
    {
        var canvasId = plan.Definition.Canvases
            .FirstOrDefault(canvas => canvas.Nodes.Any(candidate => string.Equals(candidate.Id, node.Id, StringComparison.Ordinal)))
            ?.Id ?? string.Empty;
        var projectId = session.Read("projectId") as string ?? string.Empty;
        var environment = new NodeExecutionEnvironment(
            session.RunId,
            projectId,
            plan.Definition.Id,
            canvasId,
            node.Id,
            session.FrameDepth,
            step,
            executionId);
        var runtime = new NodeExecutionRuntime(
            environment,
            new NodeEventSink(this, session, node.Id, executionId),
            () => session.CancellationToken.IsCancellationRequested);
        return new NodeExecutionRequest(node, session, inputs, runtime, step, executionId);
    }

    private static NodeExecutionResult AttachInputs(
        NodeExecutionResult result,
        IReadOnlyDictionary<string, object?> fallbackInputs)
        => result.Inputs is null
            ? result with { Inputs = CopyInputs(fallbackInputs) }
            : result with { Inputs = CopyInputs(result.Inputs) };

    private static Dictionary<string, object?> CopyInputs(IReadOnlyDictionary<string, object?> values)
        => new Dictionary<string, object?>(values, StringComparer.Ordinal);

    private sealed class NodeEventSink(
        FlowRunner runner,
        FlowExecutionSession session,
        string nodeId,
        Guid executionId) : INodeExecutionEventSink
    {
        public ValueTask PublishLogAsync(NodeExecutionLogEntry entry, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(entry);
            return runner.PublishAsync(
                session,
                "node.log",
                nodeId,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["level"] = entry.Level,
                    ["message"] = entry.Message,
                    ["value"] = entry.Value
                }, executionId);
        }
    }

    private async ValueTask PublishAsync(
        FlowExecutionSession session,
        string type,
        string nodeId,
        IReadOnlyDictionary<string, object?> payload,
        Guid? executionId = null)
    {
        var payloadWithFrame = new Dictionary<string, object?>(payload, StringComparer.Ordinal)
        {
            ["frameDepth"] = session.FrameDepth
        };
        if (executionId is Guid id)
            payloadWithFrame["executionId"] = id;
        await _eventPublisher.PublishAsync(
            new RuntimeEvent(session.RunId, session.NextSequence(), DateTimeOffset.UtcNow, type, nodeId, payloadWithFrame),
            session.CancellationToken);
    }
}

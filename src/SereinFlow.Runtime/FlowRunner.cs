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

    public FlowRunner(
        ExecutionPlanBuilder planBuilder,
        NodeExecutorRegistry executors,
        IRunEventPublisher? eventPublisher = null,
        DataConnectionResolver? dataResolver = null)
    {
        _planBuilder = planBuilder;
        _executors = executors;
        _eventPublisher = eventPublisher ?? new NullRunEventPublisher();
        _dataResolver = dataResolver ?? new DataConnectionResolver();

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

        var globalFlipflops = plan.Nodes.Values
            .Where(node => node.Type == NodeType.Flipflop && !plan.HasIncomingExecution(node.Id))
            .ToArray();
        var globalTasks = globalFlipflops
            .Select(node => RunGlobalFlipflopAsync(node, plan, session, linkedCancellation.Token))
            .ToArray();

        var mainResult = plan.Nodes.TryGetValue(definition.EntryNodeId, out var entry)
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
                return NodeExecutionResult.Error("worker.cancelled", "The global trigger run was cancelled. 全局触发器运行已取消。");
            }
        }

        return mainResult;
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

            if (!session.TryBeginStep(node.Id, out var limitError))
                return NodeExecutionResult.Error(limitError!, "The flow execution limit was exceeded. 流程执行限制已超出。");

            await PublishAsync(session, "node.started", node.Id, new Dictionary<string, object?>
            {
                ["step"] = session.StepCount
            });

            IReadOnlyDictionary<string, object?> inputs = EmptyInputs;
            try
            {
                inputs = _dataResolver.Resolve(node, plan, session);
                var executor = _executors.Get(node.Type);
                lastResult = await executor.ExecuteAsync(new NodeExecutionRequest(node, session, inputs), cancellationToken);
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
                    ["outputs"] = lastResult.Outputs
                });

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
            try
            {
                if (!session.TryBeginStep(node.Id, out var limitError))
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

                // Every trigger event gets an isolated value context.  The
                // child shares only the run-wide step/sequence budget, while
                // its inputs and downstream outputs cannot overwrite another
                // trigger instance or the ordinary entry flow.
                await using var triggerSession = session.CreateChild();

                await PublishAsync(session, "node.started", node.Id, new Dictionary<string, object?>
                {
                    ["global"] = true,
                    ["step"] = session.StepCount
                });
                inputs = _dataResolver.Resolve(node, plan, triggerSession);
                var result = await trigger.WaitForTriggerAsync(new NodeExecutionRequest(node, triggerSession, inputs), cancellationToken);
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
                    ["outputs"] = result.Outputs
                });

                foreach (var connection in plan.GetOutgoing(node.Id, result.NextBranch))
                    await RunStackAsync(connection.ToNodeId, plan, triggerSession, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return NodeExecutionResult.Error("worker.cancelled", "The global trigger run was cancelled. 全局触发器运行已取消。");
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
                    ["errorMessage"] = exception.Message
                });
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
                    ["errorMessage"] = $"Global Flipflop execution failed. 全局 Flipflop 执行失败。 {exception.Message}"
                });
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
            return new NodeExecutionResult(false, result.Outputs, result.NextBranch, result.ErrorCode, result.ErrorMessage);

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

        return NodeExecutionResult.Success(result.Outputs);
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

    private static NodeExecutionResult AttachInputs(
        NodeExecutionResult result,
        IReadOnlyDictionary<string, object?> fallbackInputs)
        => result.Inputs is null
            ? result with { Inputs = CopyInputs(fallbackInputs) }
            : result with { Inputs = CopyInputs(result.Inputs) };

    private static Dictionary<string, object?> CopyInputs(IReadOnlyDictionary<string, object?> values)
        => new Dictionary<string, object?>(values, StringComparer.Ordinal);

    private async ValueTask PublishAsync(
        FlowExecutionSession session,
        string type,
        string nodeId,
        IReadOnlyDictionary<string, object?> payload)
    {
        var payloadWithFrame = new Dictionary<string, object?>(payload, StringComparer.Ordinal)
        {
            ["frameDepth"] = session.FrameDepth
        };
        await _eventPublisher.PublishAsync(
            new RuntimeEvent(session.RunId, session.NextSequence(), DateTimeOffset.UtcNow, type, nodeId, payloadWithFrame),
            session.CancellationToken);
    }
}

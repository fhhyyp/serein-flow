using SereinFlow.Contracts;
using SereinFlow.Domain;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Runtime;

/// <summary>
/// Executes flow nodes according to a validated execution plan.
/// 根据经过校验的执行计划运行流程节点。
/// </summary>
public sealed class FlowRunner
{
    private static readonly TimeSpan ListenerFailureRetryDelay = TimeSpan.FromMilliseconds(100);
    private static readonly IReadOnlyDictionary<string, object?> EmptyInputs = new Dictionary<string, object?>(StringComparer.Ordinal);
    private readonly ExecutionPlanBuilder _planBuilder;
    private readonly NodeExecutorRegistry _executors;
    private readonly IRunEventPublisher _eventPublisher;
    private readonly DataConnectionResolver _dataResolver;
    private readonly IExecutionGate _executionGate;
    private readonly DebugInvocationScheduler? _debugInvocationScheduler;
    public FlowRunner(ExecutionPlanBuilder planBuilder, NodeExecutorRegistry executors, IRunEventPublisher? eventPublisher = null, DataConnectionResolver? dataResolver = null, IExecutionGate? executionGate = null, DebugInvocationScheduler? debugInvocationScheduler = null)
    {
        _planBuilder = planBuilder;
        _executors = executors;
        _eventPublisher = eventPublisher ?? new NullRunEventPublisher();
        _dataResolver = dataResolver ?? new DataConnectionResolver();
        _executionGate = executionGate ?? NoopExecutionGate.Instance;
        _debugInvocationScheduler = debugInvocationScheduler;
        foreach (var flowCall in executors.GetAll()
            .OfType<IFlowCallExecutorConfiguration>())
        {
            flowCall.Configure(ExecuteFlowCallAsync);
        }
    }

    /// <summary>
    /// Builds an execution plan and runs a flow from its entry node.
    /// 构建执行计划并从流程入口节点开始运行。
    /// </summary>
    public async ValueTask<NodeExecutionResult> RunAsync(FlowDefinition definition, FlowExecutionSession session, CancellationToken cancellationToken = default)
    {
        if (definition is null)
        {
            throw new ArgumentNullException(nameof(definition), "The flow definition cannot be null. 流程定义不能为空。");
        }

        if (session is null)
        {
            throw new ArgumentNullException(nameof(session), "The runtime session cannot be null. 运行时会话不能为空。");
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(session.CancellationToken, cancellationToken);
        var plan = _planBuilder.Build(definition);
        session.AttachPlan(plan);
        try
        {
            var globalFlipflops = plan.Nodes.Values
                .Where(node =>
                {
                    return node.Type == NodeType.Flipflop && !plan.HasIncomingExecution(node.Id);
                })
                .ToArray();
            var globalTasks = globalFlipflops
                .Select(node =>
                {
                    return RunGlobalFlipflopAsync(node, plan, session, linkedCancellation.Token);
                })
                .ToArray();
            var success = string.IsNullOrWhiteSpace(definition.EntryNodeId) ? NodeExecutionResult.Success() : plan.Nodes.TryGetValue(definition.EntryNodeId, out var entry) && entry.Type == NodeType.Flipflop && globalFlipflops.Any(node => node.Id == entry.Id) ? NodeExecutionResult.Success() : await RunStackAsync(definition.EntryNodeId, plan, session, linkedCancellation.Token);
            if (globalTasks.Length > 0)
            {
                try
                {
                    var globalFailure = (await Task.WhenAll(globalTasks)).FirstOrDefault(result => !result.IsSuccess);
                    if (globalFailure is not null)
                    {
                        if (globalFailure.ErrorCode == WorkerErrorCodes.Cancelled && linkedCancellation.IsCancellationRequested)
                        {
                            throw new OperationCanceledException(linkedCancellation.Token);
                        }

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
            return success;
        }
        finally
        {
            if (_debugInvocationScheduler is not null)
            {
                await _debugInvocationScheduler.StopAndDrainAsync();
            }
        }
    }

    /// <summary>
    /// Runs a flow starting from the specified node.
    /// 从指定节点开始运行流程。
    /// </summary>
    public ValueTask<NodeExecutionResult> RunFromNodeAsync(string nodeId, FlowExecutionSession session, CancellationToken cancellationToken = default)
    {
        if (session.Plan is null)
        {
            throw new InvalidOperationException("The execution session is not attached to a plan. 运行会话尚未绑定执行计划。");
        }
        return RunStackAsync(nodeId, session.Plan, session, cancellationToken);
    }

    private async ValueTask<NodeExecutionResult> RunStackAsync(string startNodeId, ExecutionPlan plan, FlowExecutionSession session, CancellationToken cancellationToken)
    {
        var stack = new Stack<string>();
        stack.Push(startNodeId);
        var success = NodeExecutionResult.Success();
        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentNodeId = stack.Pop();
            if (!plan.Nodes.TryGetValue(currentNodeId, out var node))
            {
                return NodeExecutionResult.Error(FlowErrorCodes.NodeMissing, $"Node '{currentNodeId}' does not exist. 节点“{currentNodeId}”不存在。");
            }

            if (!session.TryBeginStep(node.Id, out var step, out var executionId, out var limitError))
            {
                return NodeExecutionResult.Error(limitError!, "The flow execution limit was exceeded. 流程执行限制已超出。");
            }

            var startedPayload = new NodeStartedEventPayload(step, session.InvocationId);
            await PublishAsync(session, NodeErrorCodes.Started, node.Id, startedPayload, executionId);
            IReadOnlyDictionary<string, object?> inputs = EmptyInputs;
            try
            {
                inputs = _dataResolver.Resolve(node, plan, session);
                if (_executionGate is not NoopExecutionGate)
                {
                    var copyInputs = CopyInputs(inputs);
                    var nodeExecutionBoundary = new NodeExecutionBoundary(session.RunId, node.Id, node.Type, step, copyInputs, session.FrameDepth, session.InvocationId, executionId);
                    var gateDecision = await _executionGate.BeforeNodeAsync(nodeExecutionBoundary, cancellationToken);
                    if (gateDecision == ExecutionGateDecision.Cancel)
                    {
                        session.Cancel();
                        throw new OperationCanceledException("The debug execution was cancelled. 调试执行已取消。", session.CancellationToken);
                    }
                }
                success = await _executors.Get(node.Type).ExecuteAsync(CreateExecutionRequest(node, plan, session, inputs, step, executionId), cancellationToken);
                success = AttachInputs(success, inputs);
            }
            catch (FlowDataBindingException exception)
            {
                inputs = exception.ResolvedInputs;
                success = NodeExecutionResult.Failure(exception.Code, exception.Message) with
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
                success = NodeExecutionResult.Error(NodeErrorCodes.ExecutionFailed, $"Node '{node.Id}' execution failed. 节点“{node.Id}”执行失败。 {exception.Message}") with
                {
                    Inputs = CopyInputs(inputs)
                };
            }

            foreach (var output in success.Outputs)
            {
                session.Write($"{node.Id}.{output.Key}", output.Value);
                var port = node.Ports.FirstOrDefault(item => item.Name == output.Key || item.Id == output.Key);
                if (port is not null)
                {
                    session.Write($"{node.Id}.{port.Id}", output.Value);
                }
            }

            if (success.Outputs.Count == 1)
            {
                var first = success.Outputs.Values.First();
                session.Write($"{node.Id}.data-out", first);
            }

            var completedPayload = NodeResultEventPayload.FromResult(success, inputs, session.InvocationId);
            await PublishAsync(session, success.IsSuccess ? NodeErrorCodes.Completed : success.NextBranch == ExecutionBranch.Error ? NodeErrorCodes.Error : NodeErrorCodes.Failed, node.Id, completedPayload, executionId);
            var branch = success.NextBranch;
            var selected = plan.GetOutgoing(node.Id, branch);
            PushReverse(stack, selected);
        }

        return success;
    }

    private async Task<NodeExecutionResult> RunGlobalFlipflopAsync(NodeDefinition node, ExecutionPlan plan, FlowExecutionSession session, CancellationToken cancellationToken)
    {
        var executor = _executors.Get(node.Type);
        if (executor is not IGlobalFlipflopExecutor trigger)
        {
            var invalidExecutorPayload = new NodeResultEventPayload(false, null, FlipFlopErrorCodes.ExecutorInvalid, "The global Flipflop executor is invalid. 全局 Flipflop 执行器无效。", EmptyInputs, EmptyInputs, null, IncludeInvocationId: false);
            await PublishAsync(session, NodeErrorCodes.Failed, node.Id, invalidExecutorPayload);
            return NodeExecutionResult.Error(FlipFlopErrorCodes.ExecutorInvalid, "The global Flipflop executor is invalid. 全局 Flipflop 执行器无效。");
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
                    var limitPayload = new NodeResultEventPayload(false, ExecutionBranch.Error, limitError, "The global Flipflop execution limit was exceeded. 全局 Flipflop 执行限制已超出。", EmptyInputs, EmptyInputs, null, Global: true, IncludeInvocationId: false);
                    await PublishAsync(session, NodeErrorCodes.Failed, node.Id, limitPayload);
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
                    var startedPayload = new NodeStartedEventPayload(step, invocationId, Global: true);
                    await PublishAsync(session, NodeErrorCodes.Started, node.Id, startedPayload, executionId);
                    inputs = _dataResolver.Resolve(node, plan, triggerSession);
                    var result = await trigger.WaitForTriggerAsync(CreateExecutionRequest(node, plan, triggerSession, inputs, step, executionId), cancellationToken);
                    result = AttachInputs(result, inputs);
                    foreach (var output in result.Outputs)
                    {
                        triggerSession.Write($"{node.Id}.{output.Key}", output.Value);
                    }

                    if (result.Outputs.Count == 1)
                    {
                        var first = result.Outputs.Values.First();
                        triggerSession.Write($"{node.Id}.data-out", first);
                    }

                    var completedPayload = NodeResultEventPayload.FromResult(result, inputs, invocationId, global: true);
                    await PublishAsync(session, result.IsSuccess ? NodeErrorCodes.Completed : result.NextBranch == ExecutionBranch.Error ? NodeErrorCodes.Error : NodeErrorCodes.Failed, node.Id, completedPayload, executionId);
                    if (_debugInvocationScheduler is null)
                    {
                        foreach (var connection in plan.GetOutgoing(node.Id, result.NextBranch))
                        {
                            await RunStackAsync(connection.ToNodeId, plan, triggerSession, cancellationToken);
                        }
                    }
                    else
                    {
                        var debugInputs = CopyInputs(result.Inputs ?? inputs);
                        var receivedPayload = new TriggerReceivedEventPayload(invocationId, node.Id, debugInputs);
                        await PublishAsync(session, DebugErrorCodes.TriggerReceived, node.Id, receivedPayload);
                        if (_debugInvocationScheduler.TrySchedule(() =>
                        {
                            return ExecuteScheduledTriggerAsync(node, plan, session, triggerSession, result.NextBranch, debugInputs, step, executionId, cancellationToken);
                        }, () =>
                        {
                            return triggerSession.DisposeAsync().AsTask();
                        }, out var queuePosition))
                        {
                            triggerSessionOwnedByListener = false;
                            if (queuePosition > 0)
                            {
                                var queuedPayload = new TriggerQueuedEventPayload(invocationId, node.Id, queuePosition);
                                await PublishAsync(session, DebugErrorCodes.TriggerQueued, node.Id, queuedPayload);
                            }
                        }
                        else
                        {
                            var rejectedPayload = new TriggerRejectedEventPayload(invocationId, node.Id, DebugErrorCodes.TriggerQueueFull, _debugInvocationScheduler.MaximumQueuedInvocations);
                            await PublishAsync(session, DebugErrorCodes.TriggerRejected, node.Id, rejectedPayload);
                        }
                    }
                }
                finally
                {
                    if (triggerSessionOwnedByListener)
                    {
                        await triggerSession.DisposeAsync();
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (FlowDataBindingException exception)
            {
                inputs = exception.ResolvedInputs;
                var bindingFailurePayload = new NodeResultEventPayload(false, ExecutionBranch.Failure, exception.Code, exception.Message, inputs, EmptyInputs, invocationId, Global: true);
                await PublishAsync(session, NodeErrorCodes.Failed, node.Id, bindingFailurePayload, executionId);
                foreach (var connection in plan.GetOutgoing(node.Id, ExecutionBranch.Failure))
                {
                    await RunStackAsync(connection.ToNodeId, plan, session, cancellationToken);
                }

                await Task.Delay(ListenerFailureRetryDelay, cancellationToken);
            }
            catch (Exception exception)
            {
                var listenerFailurePayload = new NodeResultEventPayload(false, ExecutionBranch.Error, FlipFlopErrorCodes.ExecutionFailed, $"Global Flipflop execution failed. 全局 Flipflop 执行失败。 {exception.Message}", inputs, EmptyInputs, invocationId, Global: true);
                await PublishAsync(session, NodeErrorCodes.Failed, node.Id, listenerFailurePayload, executionId);
                // An exception escaping a global listener still follows the
                // node's Error branch before the listener waits again.
                // 全局监听器发生未处理异常时，先进入 Error 分支，再继续等待下一次触发。
                foreach (var connection in plan.GetOutgoing(node.Id, ExecutionBranch.Error))
                {
                    await RunStackAsync(connection.ToNodeId, plan, session, cancellationToken);
                }

                await Task.Delay(ListenerFailureRetryDelay, cancellationToken);
            }
        }
        return NodeExecutionResult.Success();
    }

    private async Task ExecuteScheduledTriggerAsync(NodeDefinition flipflopNode, ExecutionPlan plan, FlowExecutionSession rootSession, FlowExecutionSession triggerSession, ExecutionBranch branch, IReadOnlyDictionary<string, object?> inputs, int step, Guid executionId, CancellationToken cancellationToken)
    {
        await using var ownedTriggerSession = triggerSession;
        var invocationId = triggerSession.InvocationId;
        try
        {
            var admittedPayload = new TriggerAdmittedEventPayload(invocationId, flipflopNode.Id);
            await PublishAsync(rootSession, DebugErrorCodes.TriggerAdmitted, flipflopNode.Id, admittedPayload);
            var copyInputs = CopyInputs(inputs);
            var boundary = new NodeExecutionBoundary(rootSession.RunId, flipflopNode.Id, flipflopNode.Type, step, copyInputs, triggerSession.FrameDepth, invocationId, executionId);
            var decision = await _executionGate.BeforeNodeAsync(boundary, cancellationToken);
            if (decision == ExecutionGateDecision.Cancel)
            {
                rootSession.Cancel();
                throw new OperationCanceledException("The debug execution was cancelled. 调试执行已取消。", rootSession.CancellationToken);
            }

            var success = NodeExecutionResult.Success();
            foreach (var connection in plan.GetOutgoing(flipflopNode.Id, branch))
            {
                success = await RunStackAsync(connection.ToNodeId, plan, triggerSession, cancellationToken);
            }

            var completedPayload = new TriggerCompletedEventPayload(invocationId, flipflopNode.Id, success);
            await PublishAsync(rootSession, DebugErrorCodes.TriggerCompleted, flipflopNode.Id, completedPayload);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || rootSession.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failedPayload = new TriggerFailedEventPayload(invocationId, flipflopNode.Id, DebugErrorCodes.TriggerExecutionFailed, exception.Message);
            await PublishAsync(rootSession, DebugErrorCodes.TriggerFailed, flipflopNode.Id, failedPayload);
        }
    }

    private async ValueTask<NodeExecutionResult> ExecuteFlowCallAsync(NodeExecutionRequest request, CancellationToken cancellationToken)
    {
        if (request.Context is not FlowExecutionSession session || session.Plan is null)
        {
            return NodeExecutionResult.Error(FlowCallErrorCodes.ContextInvalid, "FlowCall requires a runtime execution session. FlowCall 需要运行时执行会话。");
        }

        var target = request.Node.Runtime?.TargetNodeId;
        if (string.IsNullOrWhiteSpace(target))
        {
            return NodeExecutionResult.Error(FlowCallErrorCodes.TargetMissing, "FlowCall target node is missing. FlowCall 目标节点缺失。");
        }

        if (!session.Plan.Nodes.TryGetValue(target, out var targetNode))
        {
            return NodeExecutionResult.Error(FlowCallErrorCodes.TargetMissing, "FlowCall target node is missing. FlowCall 目标节点缺失。");
        }

        var callInputs = ResolveFlowCallInputs(request.Node, targetNode, request.Inputs);
        await using var callFrame = session.CreateFlowCallFrame(target, callInputs);
        var result = await RunFromNodeAsync(target, callFrame, cancellationToken);
        if (!result.IsSuccess)
        {
            return new NodeExecutionResult(false, result.Outputs, result.NextBranch, result.ErrorCode, result.ErrorMessage, TransferOutputs: result.TransferOutputs);
        }

        var staticType = request.Node.Runtime?.StaticReturnType;
        if (!string.IsNullOrWhiteSpace(staticType) && !string.Equals(staticType, "void", StringComparison.OrdinalIgnoreCase) && request.Node.Runtime?.IsDynamicReturnType != true && result.Outputs.Values.FirstOrDefault() is { } actual && !IsCompatibleReturnType(actual, staticType))
        {
            return NodeExecutionResult.Error(FlowCallErrorCodes.ReturnTypeMismatch, ($"FlowCall returned '{actual.GetType().FullName}' but the static return type is '{staticType}'. FlowCall 返回类型与静态返回类型不一致。"));
        }
        return NodeExecutionResult.Success(result.Outputs) with
        {
            TransferOutputs = result.TransferOutputs
        };
    }

    private static IReadOnlyDictionary<string, object?> ResolveFlowCallInputs(NodeDefinition callNode, NodeDefinition targetNode, IReadOnlyDictionary<string, object?> inputs)
    {
        var mappings = callNode.Runtime?.FlowCallParameterBindings ?? [];
        var targetInputs = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (mappings.Count == 0)
        {
            foreach (var targetParameter in targetNode.Parameters)
            {
                if (inputs.TryGetValue(targetParameter.Id, out var value))
                {
                    targetInputs[targetParameter.Id] = value;
                }
            }

            return targetInputs;
        }

        foreach (var mapping in mappings)
        {
            if (inputs.TryGetValue(mapping.CallParameterId, out var value))
            {
                targetInputs[mapping.TargetParameterId] = value;
            }
        }

        return targetInputs;
    }

    private static bool IsCompatibleReturnType(object value, string staticType)
    {
        var actualType = value.GetType();
        var expectedType = ResolveReturnType(staticType, actualType);
        if (expectedType is not null)
        {
            return expectedType.IsAssignableFrom(actualType);
        }

        // Keep compatibility with older or external type names that cannot be
        // resolved from the current AppDomain. The metadata still commonly
        // exposes the full name of a base type or interface.
        // 对于当前 AppDomain 无法解析的旧版或外部类型名，保留基于类型层级名称的兼容判断。
        for (var type = actualType; type is not null; type = type.BaseType)
        {
            if (string.Equals(type.FullName, staticType, StringComparison.Ordinal) || string.Equals(type.Name, staticType, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return actualType.GetInterfaces()
            .Any(type =>
            {
                return string.Equals(type.FullName, staticType, StringComparison.Ordinal) || string.Equals(type.Name, staticType, StringComparison.Ordinal);
            });
    }

    private static Type? ResolveReturnType(string staticType, Type actualType)
    {
        if (actualType.Assembly.GetType(staticType, throwOnError: false, ignoreCase: false) is { } type)
        {
            return type;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetType(staticType, throwOnError: false, ignoreCase: false) is { } loadedType)
            {
                return loadedType;
            }
        }

        return null;
    }

    private static void PushReverse(Stack<string> stack, IReadOnlyList<ConnectionDefinition> connections)
    {
        for (var index = connections.Count - 1; index >= 0; index--)
        {
            stack.Push(connections[index].ToNodeId);
        }
    }

    private NodeExecutionRequest CreateExecutionRequest(NodeDefinition node, ExecutionPlan plan, FlowExecutionSession session, IReadOnlyDictionary<string, object?> inputs, int step, Guid executionId)
    {
        var canvasId = plan.GetCanvasId(node.Id);
        var projectId = session.Read("projectId") as string ?? string.Empty;
        var environment = new NodeExecutionEnvironment(session.RunId, projectId, plan.Definition.Id, canvasId, node.Id, session.FrameDepth, step, executionId);
        var nodeEventSink = new NodeEventSink(this, session, node.Id, executionId);
        var runtime = new NodeExecutionRuntime(environment, nodeEventSink, () => session.CancellationToken.IsCancellationRequested, session.Variables);
        return new NodeExecutionRequest(node, session, inputs, runtime, step, executionId);
    }

    private static NodeExecutionResult AttachInputs(NodeExecutionResult result, IReadOnlyDictionary<string, object?> fallbackInputs)
    {
        return result.Inputs is null ? result with
        {
            Inputs = CopyInputs(fallbackInputs)
        }

        : result with
        {
            Inputs = CopyInputs(result.Inputs)
        };
    }

    private static Dictionary<string, object?> CopyInputs(IReadOnlyDictionary<string, object?> values)
    {
        return new Dictionary<string, object?>(values, StringComparer.Ordinal);
    }

    private sealed class NodeEventSink(FlowRunner runner, FlowExecutionSession session, string nodeId, Guid executionId) : INodeExecutionEventSink
    {
        public ValueTask PublishLogAsync(NodeExecutionLogEntry entry, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(entry);
            var logPayload = new NodeLogEventPayload(entry);
            return runner.PublishAsync(session, NodeErrorCodes.Log, nodeId, logPayload, executionId);
        }
    }

    private async ValueTask PublishAsync(FlowExecutionSession session, string type, string nodeId, RuntimeEventPayload payload, Guid? executionId = null)
    {
        var runtimeEvent = RuntimeEventFactory.Create(session, type, nodeId, payload, executionId);
        await _eventPublisher.PublishAsync(runtimeEvent, session.CancellationToken);
    }
}

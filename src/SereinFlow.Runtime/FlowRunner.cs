using SereinFlow.Domain;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Runtime;

public sealed class FlowRunner
{
    private readonly ExecutionPlanBuilder _planBuilder;
    private readonly NodeExecutorRegistry _executors;
    private readonly IRunEventPublisher _eventPublisher;

    public FlowRunner(
        ExecutionPlanBuilder planBuilder,
        NodeExecutorRegistry executors,
        IRunEventPublisher? eventPublisher = null)
    {
        _planBuilder = planBuilder;
        _executors = executors;
        _eventPublisher = eventPublisher ?? new NullRunEventPublisher();
    }

    public async ValueTask<NodeExecutionResult> RunAsync(
        FlowDefinition definition,
        FlowExecutionSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(session);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(session.CancellationToken, cancellationToken);
        var token = linkedCancellation.Token;
        var plan = _planBuilder.Build(definition);
        var currentNodeId = definition.EntryNodeId;
        var lastResult = NodeExecutionResult.Success();

        while (true)
        {
            token.ThrowIfCancellationRequested();
            var node = plan.Nodes[currentNodeId];
            await PublishAsync(session, "node.started", node.Id, new Dictionary<string, object?>());
            var executor = _executors.Get(node.Type);
            lastResult = await executor.ExecuteAsync(new NodeExecutionRequest(node, session, session.Snapshot()), token);
            foreach (var output in lastResult.Outputs)
            {
                session.Write($"{node.Id}.{output.Key}", output.Value);
            }

            await PublishAsync(
                session,
                lastResult.IsSuccess ? "node.completed" : "node.failed",
                node.Id,
                new Dictionary<string, object?>
                {
                    ["success"] = lastResult.IsSuccess,
                    ["errorCode"] = lastResult.ErrorCode,
                    ["errorMessage"] = lastResult.ErrorMessage
                });

            if (!lastResult.IsSuccess)
            {
                return lastResult;
            }

            var outgoing = plan.GetOutgoing(node.Id, ExecutionBranch.Success);
            var next = outgoing.Count == 0 ? null : outgoing[0];
            if (next is null)
            {
                return lastResult;
            }

            currentNodeId = next.ToNodeId;
        }
    }

    private async ValueTask PublishAsync(FlowExecutionSession session, string type, string nodeId, IReadOnlyDictionary<string, object?> payload)
    {
        await _eventPublisher.PublishAsync(
            new RuntimeEvent(session.RunId, session.NextSequence(), DateTimeOffset.UtcNow, type, nodeId, payload),
            session.CancellationToken);
    }
}

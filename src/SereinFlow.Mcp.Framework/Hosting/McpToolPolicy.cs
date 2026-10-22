using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace SereinFlow.Mcp.Framework;

/// <summary>
/// Represents MCP tool failure data used by SereinFlow.
/// 表示 SereinFlow 使用的MCPTool失败数据。
/// </summary>
public sealed record McpToolFailure(Exception Exception, string Outcome, string? Summary);

/// <summary>
/// Represents MCP tool completion data used by SereinFlow.
/// 表示 SereinFlow 使用的MCPToolCompletion数据。
/// </summary>
public sealed record McpToolCompletion(McpToolExecutionContext Context, object? Value,
    McpToolFailure? Failure, TimeSpan Duration)
{
    public string Outcome => Failure?.Outcome ?? "succeeded";
    public string? Summary => Failure?.Summary;
}

/// <summary>
/// Owns policy execution, mutation leases, timing and isolated completion recording.
/// 负责策略执行、修改租约、计时及隔离的完成记录。
/// </summary>
public abstract class McpToolPolicy(McpToolMutationGate mutationGate) : IMcpToolMiddleware
{
    public async Task<object?> InvokeAsync(McpToolExecutionContext context, McpToolExecutionDelegate next, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        object? value = null;
        McpToolFailure? failure = null;
        try
        {
            if (context.CanExecute) await AuthorizeAsync(context, cancellationToken);
            using var lease = context.CanExecute
                ? await mutationGate.AcquireAsync(context.Tool!, context.Arguments, cancellationToken) : null;
            value = await next(context, cancellationToken);
            if (context.CanExecute) value = ProcessResult(context, value);
            return value;
        }
        catch (Exception exception)
        {
            failure = MapFailure(context, exception);
            if (ReferenceEquals(failure.Exception, exception)) throw;
            throw failure.Exception;
        }
        finally
        {
            var completion = new McpToolCompletion(context, failure is null ? value : null,
                failure, Stopwatch.GetElapsedTime(started));
            try
            {
                await using var scope = context.CallContext.Services.CreateAsyncScope();
                await RecordAsync(scope.ServiceProvider, completion);
            }
            catch
            {
                // Completion recording must never replace a tool result or its original failure.
                // 完成记录不得覆盖工具结果或其原始失败。
            }
        }
    }

    protected abstract ValueTask AuthorizeAsync(McpToolExecutionContext context, CancellationToken cancellationToken);
    protected virtual object? ProcessResult(McpToolExecutionContext context, object? value) => value;
    protected virtual McpToolFailure MapFailure(McpToolExecutionContext context, Exception exception)
        => new(exception, "failed", "internal_error");
    protected virtual Task RecordAsync(IServiceProvider services, McpToolCompletion completion) => Task.CompletedTask;
}

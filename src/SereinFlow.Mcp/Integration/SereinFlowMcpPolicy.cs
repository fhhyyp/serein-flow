using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Mcp.Integration;

/// <summary>SereinFlow authorization, result semantics and protocol error mapping.</summary>
public sealed class SereinFlowMcpPolicy(IMcpPrincipalAccessor principalAccessor, McpToolMutationGate mutationGate)
    : McpToolPolicy(mutationGate)
{
    protected override ValueTask AuthorizeAsync(McpToolExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.Tool!.Metadata is not ISereinFlowMcpToolMetadata metadata || metadata.Permissions.Length == 0)
            throw new InvalidOperationException($"SereinFlow tool '{context.Name}' requires a permission policy.");
        var security = context.CallContext.Services.GetRequiredService<McpSecurityService>();
        var principal = principalAccessor.Current;
        var caller = new SereinFlowMcpToolContext(context.Scope, security, principal);
        context.CallContext.SetValue<SereinFlowMcpCaller>(caller);
        context.CallContext.SetValue(caller);
        var projectId = McpArgumentReader.OptionalGuid(context.Arguments, "projectId");
        if (metadata.Permissions.Length == 1) security.Require(principal, metadata.Permissions[0], projectId);
        else security.RequireAny(principal, projectId, metadata.Permissions);
        return ValueTask.CompletedTask;
    }

    protected override object? ProcessResult(McpToolExecutionContext context, object? value)
        => value ?? throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The requested SereinFlow resource was not found.");

    protected override McpToolFailure MapFailure(McpToolExecutionContext context, Exception exception)
        => exception switch
        {
            McpParameterBindingException binding => Rejected(MapBindingError(context, binding)),
            McpToolNotFoundException missing => Rejected(new McpProtocolException(McpProtocolErrorCodes.MethodNotFound, missing.Message)),
            McpSecurityException security => new(new McpProtocolException(McpProtocolErrorCodes.FromHttpStatus(security.StatusCode),
                security.Message, new { code = security.Code }), "denied", security.Code),
            McpProtocolException protocol => Rejected(protocol),
            _ => base.MapFailure(context, exception)
        };

    protected override Task RecordAsync(IServiceProvider services, McpToolCompletion completion)
    {
        var principal = completion.Context.CallContext.TryGetValue<SereinFlowMcpCaller>(out var caller)
            ? caller!.Principal : principalAccessor.Current;
        return services.GetRequiredService<SereinFlowMcpAuditProjection>().RecordAsync(completion, principal);
    }

    private static McpToolFailure Rejected(McpProtocolException exception)
        => new(exception, "rejected", exception.Code.ToString(CultureInfo.InvariantCulture));

    private static McpProtocolException MapBindingError(McpToolExecutionContext context, McpParameterBindingException exception)
    {
        object? data = exception.Errors is null
            ? new { code = McpErrorCodes.InvalidArguments, path = exception.Path }
            : new { code = McpErrorCodes.InvalidArguments, path = exception.Path, errors = exception.Errors };
        if (exception.ErrorCode is not null)
            data = exception.Path is null ? new { code = exception.ErrorCode } : new { code = exception.ErrorCode, path = exception.Path };
        if (exception.SuppressErrorData) data = null;
        // Preserve the published non-object argument error shape.
        if (context.Arguments.ValueKind is not (System.Text.Json.JsonValueKind.Object or System.Text.Json.JsonValueKind.Undefined)) data = null;
        return new McpProtocolException(McpProtocolErrorCodes.InvalidParams, exception.Message, data);
    }
}

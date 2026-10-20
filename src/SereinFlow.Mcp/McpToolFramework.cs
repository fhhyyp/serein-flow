using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SereinFlow.Application;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Mcp;
/// <summary>
/// Defines how an MCP tool participates in the shared execution pipeline.
/// 工具执行类型决定是否需要幂等请求串行化。
/// </summary>
public enum McpToolExecutionKind
{
    Read,
    Mutation,
}

/// <summary>
/// Immutable tool contract and its explicit execution delegates. Tool schemas
/// remain hand-authored so versioned contracts never depend on reflection.
/// 不使用反射生成 schema，保证版本化合同由工具显式拥有。
/// </summary>
public interface IMcpTool
{
    McpToolDescriptor Descriptor { get; }

    McpToolExecutionKind ExecutionKind { get; }

    bool RequiresIdempotencyKey { get; }

    FlowVersionTrackDto? DefaultAuditTrack { get; }

    void Authorize(McpToolContext context, JsonElement arguments);
    Task<object?> ExecuteAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken);
}

public sealed class McpToolDefinition : IMcpTool
{
    private readonly Action<McpToolContext, JsonElement>? _authorize;
    private readonly Func<McpToolContext, JsonElement, CancellationToken, Task<object?>> _execute;
    public McpToolDefinition(McpToolDescriptor descriptor, McpToolExecutionKind executionKind, Func<McpToolContext, JsonElement, CancellationToken, Task<object?>> execute, Action<McpToolContext, JsonElement>? authorize = null, bool requiresIdempotencyKey = false, FlowVersionTrackDto? defaultAuditTrack = null)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        ExecutionKind = executionKind;
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _authorize = authorize;
        RequiresIdempotencyKey = requiresIdempotencyKey;
        DefaultAuditTrack = defaultAuditTrack;
        if (requiresIdempotencyKey && executionKind != McpToolExecutionKind.Mutation)
        {
            throw new ArgumentException("Only mutation tools can require an idempotency key.", nameof(requiresIdempotencyKey));
        }
    }

    public McpToolDescriptor Descriptor { get; }
    public McpToolExecutionKind ExecutionKind { get; }
    public bool RequiresIdempotencyKey { get; }
    public FlowVersionTrackDto? DefaultAuditTrack { get; }

    public void Authorize(McpToolContext context, JsonElement arguments)
    {
        _authorize?.Invoke(context, arguments);
    }

    public Task<object?> ExecuteAsync(McpToolContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var executeResult = _execute(context, arguments, cancellationToken);
        return executeResult;
    }
}

public sealed class McpToolContext
{
    public McpToolContext(IServiceScope scope, McpSecurityService security, McpPrincipal? principal)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Security = security ?? throw new ArgumentNullException(nameof(security));
        Principal = principal;
    }

    public IServiceScope Scope { get; }
    public IServiceProvider Services => Scope.ServiceProvider;
    public McpSecurityService Security { get; }
    public McpPrincipal? Principal { get; }

    public McpPrincipal RequirePrincipal()
    {
        var evaluatedValue = Principal ?? throw new McpSecurityException(McpErrorCodes.Unauthenticated, "MCP authentication is required.", StatusCodes.Status401Unauthorized);
        return evaluatedValue;
    }
}

/// <summary>
/// A deterministic tool catalogue shared by tools/list and tools/call.
/// 同一目录同时服务 tools/list 与 tools/call，防止描述与路由漂移。
/// </summary>
public sealed class McpToolCatalog
{
    private readonly IReadOnlyList<McpToolDescriptor> _descriptors;
    private readonly Dictionary<string, IMcpTool> _byName;
    public McpToolCatalog(IEnumerable<IMcpTool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        var entries = tools.ToArray();
        var byName = new Dictionary<string, IMcpTool>(StringComparer.Ordinal);
        foreach (var tool in entries)
        {
            if (tool is null)
            {
                throw new ArgumentException("The MCP tool catalogue cannot contain null entries.", nameof(tools));
            }

            if (string.IsNullOrWhiteSpace(tool.Descriptor.Name))
            {
                throw new ArgumentException("An MCP tool name is required.", nameof(tools));
            }

            if (!byName.TryAdd(tool.Descriptor.Name, tool))
            {
                throw new ArgumentException($"The MCP tool '{tool.Descriptor.Name}' is registered more than once.", nameof(tools));
            }
        }

        var selectResult = entries.Select(static tool => tool.Descriptor);
        _descriptors = selectResult.ToArray();
        _byName = byName;
    }

    public IReadOnlyList<McpToolDescriptor> Descriptors => _descriptors;

    public bool TryGet(string name, out IMcpTool? tool)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            tool = null;
            return false;
        }

        var tryGetValueResult = _byName.TryGetValue(name, out tool);
        return tryGetValueResult;
    }
}

/// <summary>
/// Executes a registered tool with the common scope, authorization, mutation
/// gate, audit, and MCP exception behavior.
/// 统一承载工具的 scope、授权、幂等闸门、审计和异常协议转换。
/// </summary>
public sealed class McpToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMcpPrincipalAccessor _principalAccessor;
    private readonly McpMutationGate _mutationGate;
    private readonly McpToolAuditService _audit;
    public McpToolExecutor(IServiceScopeFactory scopeFactory, IMcpPrincipalAccessor principalAccessor, McpMutationGate? mutationGate = null, McpToolAuditService? audit = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _principalAccessor = principalAccessor ?? throw new ArgumentNullException(nameof(principalAccessor));
        _mutationGate = mutationGate ?? new McpMutationGate();
        _audit = audit ?? new McpToolAuditService(_scopeFactory);
    }

    public async Task<McpToolCallResult> ExecuteAsync(McpToolCatalog catalog, string name, JsonElement arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var principal = _principalAccessor.Current;
        var stopwatch = Stopwatch.StartNew();
        if (arguments.ValueKind is not (JsonValueKind.Object or JsonValueKind.Undefined))
        {
            stopwatch.Stop();
            if (catalog.TryGet(name, out var invalidArgumentsTool) && invalidArgumentsTool is not null)
            {
                var toStringResult = McpProtocolErrorCodes.InvalidParams.ToString(System.Globalization.CultureInfo.InvariantCulture);
                await _audit.RecordAsync(invalidArgumentsTool.Descriptor.Name, invalidArgumentsTool.DefaultAuditTrack, arguments, principal, "rejected", toStringResult, null, stopwatch.Elapsed);
            }
            else
            {
                var toStringResult1 = McpProtocolErrorCodes.InvalidParams.ToString(System.Globalization.CultureInfo.InvariantCulture);
                await _audit.RecordAsync(name, null, arguments, principal, "rejected", toStringResult1, null, stopwatch.Elapsed);
            }

            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, "MCP tool arguments must be a JSON object.");
        }

        if (!catalog.TryGet(name, out var tool) || tool is null)
        {
            stopwatch.Stop();
            var toStringResult2 = McpProtocolErrorCodes.MethodNotFound.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await _audit.RecordAsync(name, null, arguments, principal, "rejected", toStringResult2, null, stopwatch.Elapsed);
            throw new McpProtocolException(McpProtocolErrorCodes.MethodNotFound, $"MCP tool '{name}' is not supported.");
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var getRequiredServiceResult = scope.ServiceProvider.GetRequiredService<McpSecurityService>();
            var context = new McpToolContext(scope, getRequiredServiceResult, principal);
            tool.Authorize(context, arguments);
            using var mutationGate = await _mutationGate.AcquireAsync(tool, arguments, cancellationToken);
            var value = await tool.ExecuteAsync(context, arguments, cancellationToken);
            if (value is null)
            {
                throw new McpProtocolException(McpProtocolErrorCodes.ResourceNotFound, "The requested SereinFlow resource was not found.");
            }

            stopwatch.Stop();
            await _audit.RecordAsync(tool.Descriptor.Name, tool.DefaultAuditTrack, arguments, principal, "succeeded", null, value, stopwatch.Elapsed);
            var mcpToolCallResult = new McpToolCallResult(value);
            return mcpToolCallResult;
        }
        catch (McpSecurityException exception)
        {
            stopwatch.Stop();
            await _audit.RecordAsync(tool.Descriptor.Name, tool.DefaultAuditTrack, arguments, principal, "denied", exception.Code, null, stopwatch.Elapsed);
            var fromHttpStatusResult = McpProtocolErrorCodes.FromHttpStatus(exception.StatusCode);
            throw new McpProtocolException(fromHttpStatusResult, exception.Message, new { code = exception.Code });
        }
        catch (McpProtocolException exception)
        {
            stopwatch.Stop();
            var toStringResult3 = exception.Code.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await _audit.RecordAsync(tool.Descriptor.Name, tool.DefaultAuditTrack, arguments, principal, "rejected", toStringResult3, null, stopwatch.Elapsed);
            throw;
        }
        catch (Exception)
        {
            stopwatch.Stop();
            await _audit.RecordAsync(tool.Descriptor.Name, tool.DefaultAuditTrack, arguments, principal, "failed", "internal_error", null, stopwatch.Elapsed);
            throw;
        }
    }
}

/// <summary>
/// Host-local idempotent mutation serialization. Database uniqueness remains
/// the final concurrency guarantee when multiple hosts share storage.
/// 宿主进程内串行化只关闭检查后写入窗口，数据库仍是最终保证。
/// </summary>
public sealed class McpMutationGate
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);
    public async Task<IDisposable?> AcquireAsync(IMcpTool tool, JsonElement arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (!tool.RequiresIdempotencyKey)
        {
            return null;
        }

        _ = GetRequiredString(arguments, "idempotencyKey");
        var gate = _gates.GetOrAdd(tool.Descriptor.Name, static _ =>
        {
            var semaphoreSlim = new SemaphoreSlim(1, 1);
            return semaphoreSlim;
        });
        await gate.WaitAsync(cancellationToken);
        var semaphoreLease = new SemaphoreLease(gate);
        return semaphoreLease;
    }

    private static string GetRequiredString(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object || !arguments.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || resolveIsNullOrWhiteSpaceResult())
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, $"MCP parameter '{name}' is required.");
        }

        var evaluatedValue4 = value.GetString()!;
        var trimResult = evaluatedValue4.Trim();
        return trimResult;
        bool resolveIsNullOrWhiteSpaceResult()
        {
            var getStringResult = value.GetString();
            var isNullOrWhiteSpaceResult = string.IsNullOrWhiteSpace(getStringResult);
            return isNullOrWhiteSpaceResult;
        }
    }

    private sealed class SemaphoreLease(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                semaphore.Release();
            }
        }
    }
}

/// <summary>
/// Records bounded tool audit entries without ever replacing the business
/// result with an audit persistence failure.
/// 审计失败绝不覆盖业务结果或向 MCP 客户端泄露存储错误。
/// </summary>
public sealed class McpToolAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateContractOptions(options =>
    {
        var mcpPermissionJsonConverter = new McpPermissionJsonConverter();
        options.Converters.Insert(0, mcpPermissionJsonConverter);
    });
    private readonly IServiceScopeFactory _scopeFactory;
    public McpToolAuditService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public async Task RecordAsync(string operation, FlowVersionTrackDto? defaultAuditTrack, JsonElement arguments, McpPrincipal? principal, string outcome, string? summary, object? value, TimeSpan duration)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var rawArguments = arguments.ValueKind == JsonValueKind.Undefined ? string.Empty : arguments.GetRawText();
            var idempotencyKey = TryGetString(arguments, "idempotencyKey");
            var inputHash = string.IsNullOrWhiteSpace(idempotencyKey) ? McpIdempotencyService.HashKey(rawArguments) : McpIdempotencyService.HashKey(idempotencyKey);
            var inputBytes = Encoding.UTF8.GetByteCount(rawArguments);
            var outputBytes = value is null ? 0 : resolveGetByteCountResult();
            var previewId = TryGetGuid(arguments, "previewId");
            var preview = previewId is null ? null : await resolveFindAsyncResult();
            var projectId = TryGetGuid(arguments, "projectId") ?? preview?.ProjectId;
            var flowId = TryGetGuid(arguments, "flowId") ?? preview?.FlowId;
            var track = GetArgumentTrack(arguments) ?? defaultAuditTrack ?? GetPreviewTrack(preview);
            var flowVersion = GetOptionalLong(arguments, "expectedDevelopmentVersion") ?? GetOptionalLong(arguments, "expectedHeadVersion") ?? GetOptionalLong(arguments, "sourceVersion") ?? GetOptionalLong(arguments, "version") ?? preview?.ExpectedVersion;
            var getRequiredServiceResult6 = scope.ServiceProvider.GetRequiredService<IMcpAuditStore>();
            var newGuidResult = Guid.NewGuid();
            var maxResult = Math.Max(0, (long)duration.TotalMilliseconds);
            var mcpAuditEntry = new McpAuditEntry(newGuidResult, principal?.Id ?? "anonymous", operation, projectId, flowId, track, flowVersion, previewId, outcome, inputHash, summary, DateTimeOffset.UtcNow, maxResult, inputBytes, outputBytes);
            await getRequiredServiceResult6.AddAsync(mcpAuditEntry, CancellationToken.None);
            int resolveGetByteCountResult()
            {
                var serializeResult = JsonSerializer.Serialize(value, JsonOptions);
                var getByteCountResult = Encoding.UTF8.GetByteCount(serializeResult);
                return getByteCountResult;
            }

            global::System.Threading.Tasks.Task<global::SereinFlow.Application.Persistence.McpPreviewEntry?> resolveFindAsyncResult()
            {
                var getRequiredServiceResult5 = scope.ServiceProvider.GetRequiredService<IMcpPreviewStore>();
                var findAsyncResult = getRequiredServiceResult5.FindAsync(previewId.Value, CancellationToken.None);
                return findAsyncResult;
            }
        }
        catch
        {
        // An audit store failure must never create a second MCP error.
        }
    }

    private static Guid? TryGetGuid(JsonElement arguments, string name)
    {
        var value = TryGetString(arguments, name);
        global::System.Guid? evaluatedValue7 = Guid.TryParse(value, out var parsed) ? parsed : null;
        return evaluatedValue7;
    }

    private static string? TryGetString(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object || !arguments.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var getStringResult8 = value.GetString();
        return getStringResult8;
    }

    private static long? GetOptionalLong(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object || !arguments.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var parsed))
        {
            return null;
        }

        return parsed;
    }

    private static FlowVersionTrackDto? GetArgumentTrack(JsonElement arguments)
    {
        var value = TryGetString(arguments, "track");
        global::SereinFlow.Contracts.FlowVersionTrackDto? evaluatedValue9 = Enum.TryParse<FlowVersionTrackDto>(value, true, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
        return evaluatedValue9;
    }

    private static FlowVersionTrackDto? GetPreviewTrack(McpPreviewEntry? preview)
    {
        if (preview is null)
        {
            return null;
        }

        if (string.Equals(preview.Operation, FlowErrorCodes.Patch, StringComparison.Ordinal) || string.Equals(preview.Operation, "flow.publish", StringComparison.Ordinal))
        {
            return FlowVersionTrackDto.Development;
        }

        if (!string.Equals(preview.Operation, "flow.rollback", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(preview.PayloadJson);
            if (document.RootElement.TryGetProperty("request", out var request) && request.TryGetProperty("track", out var track) && track.ValueKind == JsonValueKind.String)
            {
                var trackName = track.GetString();
                if (Enum.TryParse<FlowVersionTrackDto>(trackName, true, out var parsed) && Enum.IsDefined(parsed))
                {
                    return parsed;
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}

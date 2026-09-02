using System.Text;
using System.Text.Json;
using SereinFlow.Application;
using SereinFlow.Contracts;

namespace SereinFlow.Mcp;

/// <summary>
/// Minimal MCP stdio transport. Keeping the transport independent from the
/// application backend makes protocol tests deterministic and keeps database
/// and Worker details outside the MCP boundary.
/// MCP stdio 最小传输层。传输与业务后端解耦，便于协议测试，并将数据库和 Worker
/// 细节隔离在 MCP 边界之外。
/// </summary>
public sealed class SereinFlowMcpServer
{
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateContractOptions(options =>
        options.WriteIndented = false);

    private readonly ISereinFlowMcpBackend _backend;
    private readonly TextWriter _diagnostics;
    private readonly int _maxRequestBytes;
    private readonly int _maxResponseBytes;
    private readonly IMcpRequestContextAccessor? _requestContextAccessor;
    private bool _shutdownRequested;

    public SereinFlowMcpServer(
        ISereinFlowMcpBackend backend,
        TextWriter? diagnostics = null,
        int maxRequestBytes = 16 * 1024 * 1024,
        int maxResponseBytes = 4 * 1024 * 1024,
        IMcpRequestContextAccessor? requestContextAccessor = null)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _diagnostics = diagnostics ?? TextWriter.Null;
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRequestBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxResponseBytes, 1);
        _maxRequestBytes = maxRequestBytes;
        _maxResponseBytes = maxResponseBytes;
        _requestContextAccessor = requestContextAccessor;
    }

    public async Task RunAsync(
        TextReader input,
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await input.ReadLineAsync(cancellationToken);
            if (line is null)
                break;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            await HandleMessageAsync(line, output, cancellationToken);
            if (_shutdownRequested)
                break;
        }
    }

    /// <summary>
    /// Processes one JSON-RPC message and returns the newline-free response.
    /// Notifications return null. HTTP uses this method so both transports
    /// share the same dispatcher and error model.
    /// 处理单条 JSON-RPC 消息并返回不带换行的响应；通知返回 null。HTTP 使用此方法，
    /// 使两种传输共用同一个分发器和错误模型。
    /// </summary>
    public async Task<string?> HandleRequestAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var output = new StringWriter();
        await HandleMessageAsync(message, output, cancellationToken);
        var response = output.ToString().TrimEnd('\r', '\n');
        return response.Length == 0 ? null : response;
    }

    private async Task HandleMessageAsync(
        string line,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        if (Encoding.UTF8.GetByteCount(line) > _maxRequestBytes)
        {
            await WriteResponseAsync(
                output,
                null,
                null,
                new McpProtocolException(-32012, "The MCP request exceeds the configured size limit."),
                cancellationToken);
            return;
        }
        JsonElement id = default;
        var hasId = false;
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(line);
            root = document.RootElement.Clone();
            if (root.ValueKind != JsonValueKind.Object)
                throw new McpProtocolException(-32600, "The MCP message must be a JSON object.");
            hasId = root.TryGetProperty("id", out id);
        }
        catch (JsonException exception)
        {
            await WriteResponseAsync(
                output,
                null,
                null,
                new McpProtocolException(-32700, "The MCP message contains invalid JSON.", exception.Message),
                cancellationToken);
            return;
        }
        catch (McpProtocolException exception)
        {
            await WriteResponseAsync(output, null, null, exception, cancellationToken);
            return;
        }

        if (!root.TryGetProperty("method", out var methodValue)
            || methodValue.ValueKind != JsonValueKind.String)
        {
            if (hasId)
            {
                await WriteResponseAsync(
                    output,
                    id,
                    null,
                    new McpProtocolException(-32600, "The MCP request method is missing."),
                    cancellationToken);
            }
            return;
        }

        var method = methodValue.GetString()!;
        var diagnosticId = Guid.NewGuid().ToString("N");
        var previousContext = _requestContextAccessor?.Current;
        if (previousContext is not null)
        {
            _requestContextAccessor!.Current = previousContext with
            {
                RequestId = hasId ? GetRequestId(id) : null
            };
        }

        try
        {
            var result = await DispatchAsync(method, root, cancellationToken);
            if (hasId)
                await WriteResponseAsync(output, id, result, null, cancellationToken);
        }
        catch (McpProtocolException exception)
        {
            if (hasId)
                await WriteResponseAsync(output, id, null, exception, cancellationToken);
        }
        catch (McpSecurityException exception)
        {
            if (hasId)
            {
                await WriteResponseAsync(
                    output,
                    id,
                    null,
                    new McpProtocolException(
                        exception.StatusCode == 401 ? -32001 : -32003,
                        exception.Message,
                        new { code = exception.Code }),
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Exception messages can contain user-controlled script or literal
            // data. Keep diagnostics useful without writing that data to logs.
            // 异常消息可能包含用户脚本或字面量，只记录类型，避免敏感数据进入日志。
            await _diagnostics.WriteLineAsync($"MCP request '{method}' failed internally; diagnosticId={diagnosticId} ({exception.GetType().Name}).");
            if (hasId)
            {
                await WriteResponseAsync(
                    output,
                    id,
                    null,
                    new McpProtocolException(
                        -32603,
                        "The MCP request failed internally.",
                        new { code = "mcp.internal_error", diagnosticId }),
                    cancellationToken);
            }
        }
        finally
        {
            if (_requestContextAccessor is not null)
                _requestContextAccessor.Current = previousContext;
        }
    }

    private async Task<object?> DispatchAsync(
        string method,
        JsonElement request,
        CancellationToken cancellationToken)
    {
        var parameters = request.TryGetProperty("params", out var value)
            ? value
            : JsonSerializer.SerializeToElement(new { });

        return method switch
        {
            "initialize" => CreateInitializeResult(),
            "notifications/initialized" => null,
            "ping" => new { },
            "tools/list" => new { tools = await _backend.ListToolsAsync(cancellationToken) },
            "resources/list" => new { resources = await _backend.ListResourcesAsync(cancellationToken) },
            "resources/templates/list" => new { resourceTemplates = await _backend.ListResourceTemplatesAsync(cancellationToken) },
            "prompts/list" => new { prompts = await _backend.ListPromptsAsync(cancellationToken) },
            "resources/read" => await ReadResourceAsync(parameters, cancellationToken),
            "prompts/get" => await GetPromptAsync(parameters, cancellationToken),
            "tools/call" => await CallToolAsync(parameters, cancellationToken),
            "shutdown" => RequestShutdown(),
            _ => throw new McpProtocolException(-32601, $"MCP method '{method}' is not supported.")
        };
    }

    private object? RequestShutdown()
    {
        _shutdownRequested = true;
        return null;
    }

    private async Task<object> ReadResourceAsync(JsonElement parameters, CancellationToken cancellationToken)
    {
        var uri = GetRequiredString(parameters, "uri");
        var resource = await _backend.ReadResourceAsync(uri, cancellationToken);
        var text = resource.Value is string stringValue
            && resource.MimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            ? stringValue
            : JsonSerializer.Serialize(resource.Value, JsonOptions);
        return new
        {
            contents = new[]
            {
                new
                {
                    uri = resource.Uri,
                    mimeType = resource.MimeType,
                    text
                }
            }
        };
    }

    private async Task<object> GetPromptAsync(JsonElement parameters, CancellationToken cancellationToken)
    {
        var name = GetRequiredString(parameters, "name");
        var arguments = parameters.TryGetProperty("arguments", out var value)
            ? value
            : JsonSerializer.SerializeToElement(new { });
        return await _backend.GetPromptAsync(name, arguments, cancellationToken);
    }

    private async Task<object> CallToolAsync(JsonElement parameters, CancellationToken cancellationToken)
    {
        var name = GetRequiredString(parameters, "name");
        var arguments = parameters.TryGetProperty("arguments", out var value)
            ? value
            : JsonSerializer.SerializeToElement(new { });
        var result = await _backend.CallToolAsync(name, arguments, cancellationToken);
        var serialized = JsonSerializer.Serialize(result.Value, JsonOptions);
        return new
        {
            content = new[] { new { type = "text", text = serialized } },
            structuredContent = result.Value,
            isError = result.IsError
        };
    }

    private static object CreateInitializeResult()
        => new
        {
            protocolVersion = McpProtocolConstants.ProtocolVersion,
            capabilities = new
            {
                resources = new { subscribe = false, listChanged = false },
                tools = new { listChanged = false },
                prompts = new { listChanged = false }
            },
            serverInfo = new
            {
                name = McpProtocolConstants.ServerName,
                version = McpProtocolConstants.ServerVersion
            },
            instructions = "SereinFlow MCP exposes project, flow, runtime and run-message, release, SereinLang, library, and MCP API-key capabilities. Read the routing index at sereinflow://ai/guide, then follow it to only the smallest focused skill Resource for the request; the capability index URIs sereinflow://ai/skills/sereinflow, sereinflow://ai/skills/sereinlang and sereinflow://ai/skills/sereinflow-library-package remain available when a second-level route is needed. Discover current tools, resources and prompts as needed before acting. Mutations use task-level authorization: read, preview, inspect, apply with the protocol confirmation fields, and reread. Run-message publication requires run.message.publish and an idempotencyKey; accepted means Worker broker acceptance, not downstream completion. API-key administration is administrator-only and returned secrets are shown once. Do not ask for duplicate confirmation between dependent calls; pause for unexpected, destructive, production, permission, secret, or conflicting changes."
        };

    private static string GetRequiredString(JsonElement parameters, string name)
    {
        if (parameters.ValueKind != JsonValueKind.Object
            || !parameters.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new McpProtocolException(-32602, $"MCP parameter '{name}' is required.");
        }

        return value.GetString()!.Trim();
    }

    private static string? GetRequestId(JsonElement id)
        => id.ValueKind switch
        {
            JsonValueKind.String => id.GetString(),
            JsonValueKind.Number => id.GetRawText(),
            _ => null,
        };

    private async Task WriteResponseAsync(
        TextWriter output,
        JsonElement? id,
        object? result,
        McpProtocolException? error,
        CancellationToken cancellationToken)
    {
        var response = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["jsonrpc"] = McpProtocolConstants.JsonRpcVersion,
            ["id"] = id
        };
        if (error is null)
            response["result"] = result;
        else
            response["error"] = new { code = error.Code, message = error.Message, data = error.ErrorData };
        var serialized = JsonSerializer.Serialize(response, JsonOptions);
        if (Encoding.UTF8.GetByteCount(serialized) > _maxResponseBytes)
        {
            serialized = JsonSerializer.Serialize(new
            {
                jsonrpc = McpProtocolConstants.JsonRpcVersion,
                id,
                error = new { code = -32013, message = "The MCP response exceeds the configured size limit." }
            }, JsonOptions);
        }
        await output.WriteLineAsync(serialized);
        await output.FlushAsync(cancellationToken);
    }
}

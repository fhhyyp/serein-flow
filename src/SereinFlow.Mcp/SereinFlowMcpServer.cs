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
    private static readonly JsonSerializerOptions JsonOptions = SereinJsonSerialization.CreateContractOptions(options => options.WriteIndented = false);
    private readonly ISereinFlowMcpBackend _backend;
    private readonly TextWriter _diagnostics;
    private readonly int _maxRequestBytes;
    private readonly int _maxResponseBytes;
    private readonly IMcpRequestContextAccessor? _requestContextAccessor;
    private readonly IFileUploadSettings? _fileUploadSettings;
    private readonly McpAiGuidanceProvider? _aiGuidanceProvider;
    private bool _shutdownRequested;
    public SereinFlowMcpServer(ISereinFlowMcpBackend backend, TextWriter? diagnostics = null, int maxRequestBytes = 16 * 1024 * 1024, int maxResponseBytes = 4 * 1024 * 1024, IMcpRequestContextAccessor? requestContextAccessor = null, IFileUploadSettings? fileUploadSettings = null, McpAiGuidanceProvider? aiGuidanceProvider = null)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _diagnostics = diagnostics ?? TextWriter.Null;
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRequestBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxResponseBytes, 1);
        _maxRequestBytes = maxRequestBytes;
        _maxResponseBytes = maxResponseBytes;
        _requestContextAccessor = requestContextAccessor;
        _fileUploadSettings = fileUploadSettings;
        _aiGuidanceProvider = aiGuidanceProvider;
    }

    public async Task RunAsync(TextReader input, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await input.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            await HandleMessageAsync(line, output, cancellationToken);
            if (_shutdownRequested)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Processes one JSON-RPC message and returns the newline-free response.
    /// Notifications return null. HTTP uses this method so both transports
    /// share the same dispatcher and error model.
    /// 处理单条 JSON-RPC 消息并返回不带换行的响应；通知返回 null。HTTP 使用此方法，
    /// 使两种传输共用同一个分发器和错误模型。
    /// </summary>
    public async Task<string?> HandleRequestAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var output = new StringWriter();
        await HandleMessageAsync(message, output, cancellationToken);
        var toStringResult = output.ToString();
        var response = toStringResult.TrimEnd('\r', '\n');
        return response.Length == 0 ? null : response;
    }

    private async Task HandleMessageAsync(string line, TextWriter output, CancellationToken cancellationToken)
    {
        if (Encoding.UTF8.GetByteCount(line) > GetMaxRequestBytes())
        {
            var mcpProtocolException = new McpProtocolException(McpProtocolErrorCodes.RequestTooLarge, "The MCP request exceeds the configured size limit.");
            await WriteResponseAsync(output, null, null, mcpProtocolException, cancellationToken);
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
            {
                throw new McpProtocolException(McpProtocolErrorCodes.InvalidRequest, "The MCP message must be a JSON object.");
            }

            hasId = root.TryGetProperty("id", out id);
        }
        catch (JsonException exception)
        {
            var mcpProtocolException1 = new McpProtocolException(McpProtocolErrorCodes.ParseError, "The MCP message contains invalid JSON.", exception.Message);
            await WriteResponseAsync(output, null, null, mcpProtocolException1, cancellationToken);
            return;
        }
        catch (McpProtocolException exception)
        {
            await WriteResponseAsync(output, null, null, exception, cancellationToken);
            return;
        }

        if (!root.TryGetProperty("method", out var methodValue) || methodValue.ValueKind != JsonValueKind.String)
        {
            if (hasId)
            {
                var mcpProtocolException2 = new McpProtocolException(McpProtocolErrorCodes.InvalidRequest, "The MCP request method is missing.");
                await WriteResponseAsync(output, id, null, mcpProtocolException2, cancellationToken);
            }

            return;
        }

        var method = methodValue.GetString()!;
        var newGuidResult = Guid.NewGuid();
        var diagnosticId = newGuidResult.ToString("N");
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
            {
                await WriteResponseAsync(output, id, result, null, cancellationToken);
            }
        }
        catch (McpProtocolException exception)
        {
            if (hasId)
            {
                await WriteResponseAsync(output, id, null, exception, cancellationToken);
            }
        }
        catch (McpSecurityException exception)
        {
            if (hasId)
            {
                var fromHttpStatusResult = McpProtocolErrorCodes.FromHttpStatus(exception.StatusCode);
                var mcpProtocolException3 = new McpProtocolException(fromHttpStatusResult, exception.Message, new { code = exception.Code });
                await WriteResponseAsync(output, id, null, mcpProtocolException3, cancellationToken);
            }
        }
        catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var evaluatedValue = $"MCP request '{method}' failed internally; diagnosticId={diagnosticId} ({exception.GetType().Name}).";
            // Exception messages can contain user-controlled script or literal
            // data. Keep diagnostics useful without writing that data to logs.
            // 异常消息可能包含用户脚本或字面量，只记录类型，避免敏感数据进入日志。
            await _diagnostics.WriteLineAsync(evaluatedValue);
            if (hasId)
            {
                var mcpProtocolException4 = new McpProtocolException(McpProtocolErrorCodes.InternalError, "The MCP request failed internally.", new { code = McpErrorCodes.InternalError, diagnosticId });
                await WriteResponseAsync(output, id, null, mcpProtocolException4, cancellationToken);
            }
        }
        finally
        {
            if (_requestContextAccessor is not null)
            {
                _requestContextAccessor.Current = previousContext;
            }
        }
    }

    private long GetMaxRequestBytes()
    {
        long evaluatedValue5 = _fileUploadSettings is null ? _maxRequestBytes : FileUploadLimits.GetMcpRequestBodyLimit(_fileUploadSettings.MaxLibraryUploadBytes, _maxRequestBytes);
        return evaluatedValue5;
    }

    private async Task<object?> DispatchAsync(string method, JsonElement request, CancellationToken cancellationToken)
    {
        var parameters = request.TryGetProperty("params", out var value) ? value : JsonSerializer.SerializeToElement(new { });
        object? evaluatedValue6 = method switch
        {
            "initialize" => CreateInitializeResult(),
            "notifications/initialized" => null,
            "ping" => new
            {
            },
            "tools/list" => new
            {
                tools = await _backend.ListToolsAsync(cancellationToken)
            },
            "resources/list" => new
            {
                resources = await _backend.ListResourcesAsync(cancellationToken)
            },
            "resources/templates/list" => new
            {
                resourceTemplates = await _backend.ListResourceTemplatesAsync(cancellationToken)
            },
            "prompts/list" => new
            {
                prompts = await _backend.ListPromptsAsync(cancellationToken)
            },
            "resources/read" => await ReadResourceAsync(parameters, cancellationToken),
            "prompts/get" => await GetPromptAsync(parameters, cancellationToken),
            "tools/call" => await CallToolAsync(parameters, cancellationToken),
            "shutdown" => RequestShutdown(),
            _ => throw new McpProtocolException(McpProtocolErrorCodes.MethodNotFound, $"MCP method '{method}' is not supported.")};
        return evaluatedValue6;
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
        var text = resource.Value is string stringValue && resource.MimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ? stringValue : JsonSerializer.Serialize(resource.Value, JsonOptions);
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
        var arguments = parameters.TryGetProperty("arguments", out var value) ? value : JsonSerializer.SerializeToElement(new { });
        var getPromptAsyncResult = await _backend.GetPromptAsync(name, arguments, cancellationToken);
        return getPromptAsyncResult;
    }

    private async Task<object> CallToolAsync(JsonElement parameters, CancellationToken cancellationToken)
    {
        var name = GetRequiredString(parameters, "name");
        var arguments = parameters.TryGetProperty("arguments", out var value) ? value : JsonSerializer.SerializeToElement(new { });
        var result = await _backend.CallToolAsync(name, arguments, cancellationToken);
        var serialized = JsonSerializer.Serialize(result.Value, JsonOptions);
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = serialized
                }
            },
            structuredContent = result.Value,
            isError = result.IsError
        };
    }

    private object CreateInitializeResult()
    {
        var guideUri = _aiGuidanceProvider?.GetUri(McpAiGuidanceId.Guide) ?? McpAiGuidance.ResourceUri;
        var sereinFlowUri = _aiGuidanceProvider?.GetUri(McpAiGuidanceId.SereinFlow) ?? McpAiGuidance.SereinFlowResourceUri;
        var sereinLangUri = _aiGuidanceProvider?.GetUri(McpAiGuidanceId.SereinLang) ?? McpAiGuidance.SereinLangResourceUri;
        var libraryPackageUri = _aiGuidanceProvider?.GetUri(McpAiGuidanceId.LibraryPackage) ?? McpAiGuidance.LibraryPackageResourceUri;
        return new
        {
            protocolVersion = McpProtocolConstants.ProtocolVersion,
            capabilities = new
            {
                resources = new
                {
                    subscribe = false,
                    listChanged = false
                },
                tools = new
                {
                    listChanged = false
                },
                prompts = new
                {
                    listChanged = false
                }
            },
            serverInfo = new
            {
                name = McpProtocolConstants.ServerName,
                version = McpProtocolConstants.ServerVersion
            },
            instructions = $"SereinFlow MCP exposes project, flow, runtime, run-message, run-variable, release, SereinLang, library, and MCP API-key capabilities. Read the routing index at {guideUri}, then follow it to only the smallest focused skill Resource for the request; the capability index URIs {sereinFlowUri}, {sereinLangUri} and {libraryPackageUri} remain available when a second-level route is needed. Discover current tools, resources and prompts as needed before acting. Mutations use task-level authorization: read, preview, inspect, apply with the protocol confirmation fields, and reread. Run-message publication requires run.message.publish and an idempotencyKey; accepted means Worker broker acceptance, not downstream completion. Active-run variable reads require run.read; variable writes require run.variable.write, an explicit valueType and an idempotencyKey. API-key administration is administrator-only and returned secrets are shown once. Do not ask for duplicate confirmation between dependent calls; pause for unexpected, destructive, production, permission, secret, or conflicting changes."};
    }

    private static string GetRequiredString(JsonElement parameters, string name)
    {
        if (parameters.ValueKind != JsonValueKind.Object || !parameters.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || resolveIsNullOrWhiteSpaceResult())
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, $"MCP parameter '{name}' is required.");
        }

        var evaluatedValue7 = value.GetString()!;
        var trimResult = evaluatedValue7.Trim();
        return trimResult;
        bool resolveIsNullOrWhiteSpaceResult()
        {
            var getStringResult = value.GetString();
            var isNullOrWhiteSpaceResult = string.IsNullOrWhiteSpace(getStringResult);
            return isNullOrWhiteSpaceResult;
        }
    }

    private static string? GetRequestId(JsonElement id)
    {
        string? evaluatedValue8 = id.ValueKind switch
        {
            JsonValueKind.String => id.GetString(),
            JsonValueKind.Number => id.GetRawText(),
            _ => null,
        };
        return evaluatedValue8;
    }

    private async Task WriteResponseAsync(TextWriter output, JsonElement? id, object? result, McpProtocolException? error, CancellationToken cancellationToken)
    {
        string serialized;
        if (error is null)
        {
            var response = new McpJsonRpcSuccessResponse(id, result);
            serialized = JsonSerializer.Serialize(response, JsonOptions);
        }
        else
        {
            var responseError = new McpJsonRpcError(error.Code, error.Message, error.ErrorData);
            var response = new McpJsonRpcErrorResponse<McpJsonRpcError>(id, responseError);
            serialized = JsonSerializer.Serialize(response, JsonOptions);
        }

        if (Encoding.UTF8.GetByteCount(serialized) > _maxResponseBytes)
        {
            var responseError = new McpJsonRpcErrorSummary(McpProtocolErrorCodes.ResponseTooLarge, "The MCP response exceeds the configured size limit.");
            var response = new McpJsonRpcErrorResponse<McpJsonRpcErrorSummary>(id, responseError);
            serialized = JsonSerializer.Serialize(response, JsonOptions);
        }

        await output.WriteLineAsync(serialized);
        await output.FlushAsync(cancellationToken);
    }
}

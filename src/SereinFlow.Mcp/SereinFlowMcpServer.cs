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

            var response = await HandleRequestAsync(line, cancellationToken);
            if (response is not null)
            {
                await output.WriteLineAsync(response);
                await output.FlushAsync(cancellationToken);
            }

            if (_shutdownRequested)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Processes one JSON-RPC message and returns the newline-free response.
    /// Notifications return null. HTTP reuses its parsed request through the
    /// same dispatcher and error model.
    /// 处理单条 JSON-RPC 消息并返回不带换行的响应；通知返回 null。HTTP 复用已解析的请求，
    /// 与 stdio 共用同一个分发器和错误模型。
    /// </summary>
    public async Task<string?> HandleRequestAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var byteCount = Encoding.UTF8.GetByteCount(message);
        // Bound stdio input before allocating a JSON tree. HTTP already bounds body reads.
        if (byteCount > GetMaxRequestBytes())
        {
            return SerializeResponse(null, null, RequestTooLarge());
        }

        return await HandleParsedRequestAsync(ParseRequest(message, byteCount), cancellationToken);
    }

    internal static ParsedRequest ParseRequest(string message, int? byteCount = null)
    {
        var size = byteCount ?? Encoding.UTF8.GetByteCount(message);
        try
        {
            using var document = JsonDocument.Parse(message);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new ParsedRequest(size, default,
                    new McpProtocolException(McpProtocolErrorCodes.InvalidRequest, "The MCP message must be a JSON object."));
            }

            return new ParsedRequest(size, document.RootElement.Clone());
        }
        catch (JsonException exception)
        {
            return new ParsedRequest(size, default,
                new McpProtocolException(McpProtocolErrorCodes.ParseError, "The MCP message contains invalid JSON.", exception.Message));
        }
    }

    internal async Task<string?> HandleParsedRequestAsync(ParsedRequest request, CancellationToken cancellationToken)
    {
        if (request.ByteCount > GetMaxRequestBytes())
        {
            return SerializeResponse(null, null, RequestTooLarge());
        }

        if (request.Error is not null)
        {
            return SerializeResponse(null, null, request.Error);
        }

        var hasId = request.Root.TryGetProperty("id", out var id);
        var method = request.Method;
        if (method is null)
        {
            return hasId
                ? SerializeResponse(id, null, new McpProtocolException(McpProtocolErrorCodes.InvalidRequest, "The MCP request method is missing."))
                : null;
        }

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
            var result = await DispatchAsync(method, request.Root, cancellationToken);
            return hasId ? SerializeResponse(id, result, null) : null;
        }
        catch (McpProtocolException exception)
        {
            return hasId ? SerializeResponse(id, null, exception) : null;
        }
        catch (McpSecurityException exception)
        {
            var error = new McpProtocolException(McpProtocolErrorCodes.FromHttpStatus(exception.StatusCode),
                exception.Message, new { code = exception.Code });
            return hasId ? SerializeResponse(id, null, error) : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Exception messages can contain user-controlled script or literal data.
            // 异常消息可能包含用户脚本或字面量，只记录类型，避免敏感数据进入日志。
            await _diagnostics.WriteLineAsync($"MCP request '{method}' failed internally; diagnosticId={diagnosticId} ({exception.GetType().Name}).");
            var error = new McpProtocolException(McpProtocolErrorCodes.InternalError, "The MCP request failed internally.",
                new { code = McpErrorCodes.InternalError, diagnosticId });
            return hasId ? SerializeResponse(id, null, error) : null;
        }
        finally
        {
            if (_requestContextAccessor is not null)
            {
                _requestContextAccessor.Current = previousContext;
            }
        }
    }

    private static McpProtocolException RequestTooLarge() =>
        new(McpProtocolErrorCodes.RequestTooLarge, "The MCP request exceeds the configured size limit.");

    internal sealed record ParsedRequest(int ByteCount, JsonElement Root, McpProtocolException? Error = null)
    {
        internal string? Method => Root.ValueKind == JsonValueKind.Object
            && Root.TryGetProperty("method", out var method) && method.ValueKind == JsonValueKind.String
                ? method.GetString()
                : null;
    }

    private long GetMaxRequestBytes()
    {
        return _fileUploadSettings is null
            ? _maxRequestBytes
            : FileUploadLimits.GetMcpRequestBodyLimit(_fileUploadSettings.MaxLibraryUploadBytes, _maxRequestBytes);
    }

    private async Task<object?> DispatchAsync(string method, JsonElement request, CancellationToken cancellationToken)
    {
        var parameters = request.TryGetProperty("params", out var value) ? value : JsonSerializer.SerializeToElement(new { });
        return method switch
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
            _ => throw new McpProtocolException(McpProtocolErrorCodes.MethodNotFound, $"MCP method '{method}' is not supported.")
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
        return await _backend.GetPromptAsync(name, arguments, cancellationToken);
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
        var guidance = _aiGuidanceProvider?.CreateSnapshot();
        var guideUri = guidance?.ById[McpAiGuidanceId.Guide].Resource.Uri ?? McpAiGuidance.ResourceUri;
        var sereinFlowUri = guidance?.ById[McpAiGuidanceId.SereinFlow].Resource.Uri ?? McpAiGuidance.SereinFlowResourceUri;
        var sereinLangUri = guidance?.ById[McpAiGuidanceId.SereinLang].Resource.Uri ?? McpAiGuidance.SereinLangResourceUri;
        var libraryPackageUri = guidance?.ById[McpAiGuidanceId.LibraryPackage].Resource.Uri ?? McpAiGuidance.LibraryPackageResourceUri;
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
        if (parameters.ValueKind != JsonValueKind.Object || !parameters.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new McpProtocolException(McpProtocolErrorCodes.InvalidParams, $"MCP parameter '{name}' is required.");
        }

        return value.GetString()!.Trim();
    }

    private static string? GetRequestId(JsonElement id)
    {
        return id.ValueKind switch
        {
            JsonValueKind.String => id.GetString(),
            JsonValueKind.Number => id.GetRawText(),
            _ => null,
        };
    }

    private string SerializeResponse(JsonElement? id, object? result, McpProtocolException? error)
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

        return serialized;
    }
}

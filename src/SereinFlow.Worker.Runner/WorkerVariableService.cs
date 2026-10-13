using System.Collections;
using System.Text;
using System.Text.Json;
using SereinFlow.Contracts;
using SereinFlow.Library;
using SereinFlow.Worker.Protocol;

namespace SereinFlow.Worker.Runner;

/// <summary>Projects run variables without invoking arbitrary user-object getters or converters.</summary>
public static class WorkerVariableService
{
    public const int MaxValueBytes = 16_384;
    public const int MaxResponseBytes = 512_000;
    public const int MaxFinalSnapshotBytes = 256_000;
    public const int MaxFinalVariables = 2_000;
    private static readonly JsonSerializerOptions JsonOptions = new() { MaxDepth = 16 };

    /// <summary>Captures bounded, JSON-safe values once, before the Worker exits.</summary>
    public static RunVariableSnapshotDto CaptureFinal(Guid runId, LibraryVariableService variables)
    {
        var values = variables.Snapshot();
        var entries = new List<RunVariableDto>();
        var remainingBytes = MaxFinalSnapshotBytes - 1_024;
        var truncated = values.Count > MaxFinalVariables;
        foreach (var (name, value) in values.OrderBy(static item => item.Key, StringComparer.Ordinal).Take(MaxFinalVariables))
        {
            if (name.Length > 256)
            {
                truncated = true;
                continue;
            }
            var entry = Project(name, value);
            var bytes = Encoding.UTF8.GetByteCount(WorkerProtocolCodec.SerializePayload(entry)) + 1;
            if (bytes > remainingBytes && entry.ValueAvailable)
            {
                entry = entry with { Value = null, ValueJson = null, ValueAvailable = false, UnavailableReason = "snapshot_limit" };
                bytes = Encoding.UTF8.GetByteCount(WorkerProtocolCodec.SerializePayload(entry)) + 1;
                truncated = true;
            }
            if (bytes > remainingBytes)
            {
                truncated = true;
                continue;
            }
            entries.Add(entry);
            remainingBytes -= bytes;
        }
        return new(runId, entries, values.Count, 0, false, DateTimeOffset.UtcNow, IsFinal: true, IsTruncated: truncated);
    }

    public static WorkerVariableResponseDto Execute(
        Guid runId,
        LibraryVariableService variables,
        WorkerVariableRequestDto request)
    {
        if (request.ProtocolVersion != WorkerProtocolConstants.Version || request.RunId != runId)
            return Failure(runId, VariableErrorCodes.InvalidRequest, "Variable request does not match this run.");
        if (request.Operation is not ("list" or "get" or "set")
            || request.Offset < 0 || request.Take is < 1 or > 200
            || (request.Operation != "list" && (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 256)))
            return Failure(runId, VariableErrorCodes.InvalidRequest, "Invalid variable operation, name or page.");

        WorkerVariableResponseDto response;
        if (request.Operation == "set")
        {
            var input = request.Value;
            if (input.ValueKind == JsonValueKind.Undefined)
                return Failure(runId, VariableErrorCodes.ValueInvalid, "A JSON value is required, including explicit null.");
            if (Encoding.UTF8.GetByteCount(input.GetRawText()) > MaxValueBytes)
                return Failure(runId, VariableErrorCodes.TooLarge, "Variable values are limited to 16384 JSON bytes.", 413);
            object? value;
            try
            {
                value = ParseValue(input, request.ValueType);
            }
            catch (NotSupportedException)
            {
                return Failure(runId, VariableErrorCodes.TypeInvalid, "Use null, boolean, string, int32, int64, double, decimal or json.");
            }
            catch (Exception exception) when (exception is InvalidOperationException or FormatException or OverflowException or JsonException)
            {
                return Failure(runId, VariableErrorCodes.ValueInvalid, "The JSON value does not match valueType.");
            }
            variables.Set(request.Name!, value);
            response = new(WorkerProtocolConstants.Version, runId, 200, Variable: Project(request.Name!, value));
        }
        else
        {
            var snapshot = variables.Snapshot();
            if (request.Operation == "get")
            {
                if (!snapshot.TryGetValue(request.Name!, out var value))
                    return Failure(runId, VariableErrorCodes.NotFound, "Variable not found. 未找到变量。", 404);
                response = new(WorkerProtocolConstants.Version, runId, 200, Variable: Project(request.Name!, value));
            }
            else
            {
                var entries = snapshot.OrderBy(static item => item.Key, StringComparer.Ordinal)
                    .Skip(request.Offset).Take(request.Take)
                    .Select(static item => Project(item.Key, item.Value)).ToArray();
                response = new(WorkerProtocolConstants.Version, runId, 200,
                    Snapshot: new(runId, entries, snapshot.Count, request.Offset,
                        request.Offset + entries.Length < snapshot.Count, DateTimeOffset.UtcNow));
            }
        }

        // Drop large value projections while retaining names/types and paging metadata.
        if (Encoding.UTF8.GetByteCount(WorkerProtocolCodec.SerializePayload(response)) > MaxResponseBytes)
        {
            if (response.Snapshot is { } snapshot)
                response = response with { Snapshot = snapshot with { Variables = snapshot.Variables.Select(
                    static item => item.ValueAvailable
                        ? item with { Value = null, ValueJson = null, ValueAvailable = false, UnavailableReason = "snapshot_limit" }
                        : item).ToArray() } };
            if (Encoding.UTF8.GetByteCount(WorkerProtocolCodec.SerializePayload(response)) > MaxResponseBytes)
                return Failure(runId, VariableErrorCodes.TooLarge, "Variable metadata exceeds the response limit; request a smaller page.", 413);
        }
        return response;
    }

    private static object? ParseValue(JsonElement value, string? valueType)
        => valueType switch
        {
            "null" when value.ValueKind == JsonValueKind.Null => null,
            "string" when value.ValueKind == JsonValueKind.Null => null,
            "string" => value.GetString(),
            "boolean" => value.GetBoolean(),
            "int32" => value.GetInt32(),
            "int64" => value.GetInt64(),
            "double" => FiniteDouble(value.GetDouble()),
            "decimal" => value.GetDecimal(),
            "json" => value.Clone(),
            "null" => throw new InvalidOperationException(),
            _ => throw new NotSupportedException(),
        };

    private static double FiniteDouble(double value)
        => double.IsFinite(value) ? value : throw new OverflowException();

    private static RunVariableDto Project(string name, object? value)
    {
        var type = value?.GetType().FullName;
        try
        {
            if (!TryProjectValue(value, 0, out var safe))
                return new(name, type, false, null, "unsupported_type");
            var json = JsonSerializer.SerializeToElement(safe, JsonOptions);
            var raw = json.GetRawText();
            return Encoding.UTF8.GetByteCount(raw) <= MaxValueBytes
                ? new(name, type, true, json, ValueJson: raw)
                : new(name, type, false, null, "value_limit");
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException or ObjectDisposedException or ArgumentException)
        {
            return new(name, type, false, null, "not_json");
        }
    }

    private static bool TryProjectValue(object? value, int depth, out object? safe)
    {
        safe = value;
        if (depth > 8) return false;
        if (value is null or bool or char or byte or sbyte or short or ushort or int or uint or long or ulong
            or float or double or decimal or Guid or DateTime or DateTimeOffset or TimeSpan)
            return true;
        if (value is string text) return text.Length <= MaxValueBytes;
        if (value is JsonElement json) return json.ValueKind != JsonValueKind.Undefined;
        if (value is Enum) { safe = value.ToString(); return true; }
        var type = value.GetType();
        if (value is Array { Rank: 1 } array && array.Length <= 128)
            return ProjectCollection(array, depth, out safe);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)
            && value is IList { Count: <= 128 } list)
            return ProjectCollection(list, depth, out safe);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>)
            && type.GetGenericArguments()[0] == typeof(string)
            && value is IDictionary { Count: <= 128 } dictionary)
        {
            var result = new Dictionary<string, object?>();
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key is not string key || key.Length > MaxValueBytes
                    || !TryProjectValue(entry.Value, depth + 1, out var item)) return false;
                result[key] = item;
            }
            safe = result;
            return true;
        }
        return false;
    }

    private static bool ProjectCollection(IEnumerable values, int depth, out object? safe)
    {
        var result = new List<object?>();
        foreach (var value in values)
        {
            if (!TryProjectValue(value, depth + 1, out var item)) { safe = null; return false; }
            result.Add(item);
        }
        safe = result;
        return true;
    }

    private static WorkerVariableResponseDto Failure(Guid runId, string code, string message, int status = 400)
        => new(WorkerProtocolConstants.Version, runId, status, Code: code, Message: message);
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace SereinFlow.Contracts;

public static class VariableErrorCodes
{
    public const string Request = "worker.variables.request";
    public const string Response = "worker.variables.response";
    public const string InvalidRequest = "variable.request_invalid";
    public const string NotFound = "variable.not_found";
    public const string TypeInvalid = "variable.type_invalid";
    public const string ValueInvalid = "variable.value_invalid";
    public const string TooLarge = "variable.payload_too_large";
    public const string Timeout = "variable.request_timeout";
    public const string SnapshotUnavailable = "variable.snapshot_unavailable";
}

/// <summary>A JSON projection of one variable; arbitrary CLR objects remain in the Worker.</summary>
public sealed record RunVariableDto(
    string Name,
    string? ClrType,
    bool ValueAvailable,
    JsonElement? Value,
    string? UnavailableReason = null,
    string? ValueJson = null);

public sealed record RunVariableSnapshotDto(
    Guid RunId,
    IReadOnlyList<RunVariableDto> Variables,
    int TotalCount,
    int Offset,
    bool HasMore,
    DateTimeOffset CapturedAt,
    bool IsFinal = false,
    bool IsTruncated = false);

/// <summary>Writes accept only explicit built-in types, never assembly-qualified type names.</summary>
public sealed record RunVariableSetRequestDto(JsonElement Value, string ValueType);

public sealed record WorkerVariableRequestDto(
    int ProtocolVersion,
    Guid RunId,
    string Operation,
    string? Name = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] JsonElement Value = default,
    string? ValueType = null,
    int Offset = 0,
    int Take = 100);

public sealed record WorkerVariableResponseDto(
    int ProtocolVersion,
    Guid RunId,
    int StatusCode,
    RunVariableSnapshotDto? Snapshot = null,
    RunVariableDto? Variable = null,
    string? Code = null,
    string? Message = null);

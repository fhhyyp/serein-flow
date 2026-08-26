using System.Text.Json;

namespace SereinFlow.Domain;

/// <summary>
/// Immutable node outcome retained independently from the diagnostic event stream.
/// 独立于诊断事件流保存的不可变节点执行结果。
/// </summary>
public sealed record FlowRunOutput
{
    public FlowRunOutput(
        Guid runId,
        long sequence,
        DateTimeOffset timestamp,
        string nodeId,
        string outcome,
        string? branch,
        string outputsJson,
        string? errorCode,
        string? errorMessage,
        string? inputsJson = null)
    {
        if (runId == Guid.Empty)
            throw new ArgumentException("Run ID cannot be empty. 运行 ID 不能为空。", nameof(runId));
        if (sequence < 1)
            throw new ArgumentOutOfRangeException(nameof(sequence), "Output sequence must be positive. 输出序列必须为正数。");
        if (string.IsNullOrWhiteSpace(nodeId))
            throw new ArgumentException("Output node ID cannot be empty. 输出节点 ID 不能为空。", nameof(nodeId));
        if (string.IsNullOrWhiteSpace(outcome))
            throw new ArgumentException("Output outcome cannot be empty. 输出结果状态不能为空。", nameof(outcome));
        if (string.IsNullOrWhiteSpace(outputsJson))
            throw new ArgumentException("Output JSON cannot be empty. 输出 JSON 不能为空。", nameof(outputsJson));

        ValidateJson(outputsJson, nameof(outputsJson), "Output JSON");
        var effectiveInputsJson = string.IsNullOrWhiteSpace(inputsJson) ? "{}" : inputsJson;
        ValidateJson(effectiveInputsJson, nameof(inputsJson), "Input JSON");

        RunId = runId;
        Sequence = sequence;
        Timestamp = timestamp;
        NodeId = nodeId.Trim();
        Outcome = outcome.Trim();
        Branch = string.IsNullOrWhiteSpace(branch) ? null : branch.Trim();
        OutputsJson = outputsJson;
        InputsJson = effectiveInputsJson;
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? null : errorCode.Trim();
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage.Trim();
    }

    public Guid RunId { get; }

    public long Sequence { get; }

    public DateTimeOffset Timestamp { get; }

    public string NodeId { get; }

    /// <summary>completed, failed, or error.</summary>
    public string Outcome { get; }

    public string? Branch { get; }

    public string OutputsJson { get; }

    /// <summary>
    /// The values resolved for this invocation immediately before execution.
    /// 本次调用在执行前解析得到的输入参数值。
    /// </summary>
    public string InputsJson { get; }

    public string? ErrorCode { get; }

    public string? ErrorMessage { get; }

    private static void ValidateJson(string json, string parameterName, string label)
    {
        try
        {
            using var _ = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            var localizedLabel = label == "Output JSON" ? "输出 JSON" : "输入 JSON";
            throw new ArgumentException(
                $"{label} is not valid JSON. {localizedLabel} 不是有效的 JSON。",
                parameterName,
                exception);
        }
    }
}

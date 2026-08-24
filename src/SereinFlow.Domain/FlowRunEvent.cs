namespace SereinFlow.Domain;

public sealed record FlowRunEvent
{
    public FlowRunEvent(
        Guid runId,
        long sequence,
        DateTimeOffset timestamp,
        string type,
        string? nodeId,
        string? payloadJson)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Run ID cannot be empty.", nameof(runId));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
        RunId = runId;
        Sequence = sequence;
        Timestamp = timestamp;
        Type = Validate(type, nameof(type));
        NodeId = nodeId;
        PayloadJson = payloadJson ?? string.Empty;
    }

    public Guid RunId { get; }

    public long Sequence { get; }

    public DateTimeOffset Timestamp { get; }

    public string Type { get; }

    public string? NodeId { get; }

    public string PayloadJson { get; }

    private static string Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Event type cannot be empty.", parameterName);
        }

        return value.Trim();
    }
}

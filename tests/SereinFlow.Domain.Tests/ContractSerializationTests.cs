using System.Text.Json;
using SereinFlow.Contracts;

namespace SereinFlow.Domain.Tests;

public sealed class ContractSerializationTests
{
    [Fact]
    public void WorkerEventEnvelopeRoundTripsWithoutRuntimeObjects()
    {
        var original = new WorkerEventEnvelopeDto(
            WorkerProtocol.Version,
            Guid.NewGuid(),
            7,
            DateTimeOffset.UtcNow,
            WorkerEventType.NodeCompleted,
            "node-1",
            "{\"value\":42}");

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<WorkerEventEnvelopeDto>(json);

        Assert.NotNull(restored);
        Assert.Equal(original.ProtocolVersion, restored!.ProtocolVersion);
        Assert.Equal(original.Sequence, restored.Sequence);
        Assert.Equal(original.PayloadJson, restored.PayloadJson);
    }

    [Fact]
    public void FlowRunDtoExposesStableStatusAndVersionFields()
    {
        var runId = Guid.NewGuid();
        var dto = new FlowRunDto(runId, Guid.NewGuid(), 3, FlowRunStatusDto.Running, null, null, null);

        Assert.Equal(runId, dto.Id);
        Assert.Equal(3, dto.FlowVersion);
        Assert.Equal(FlowRunStatusDto.Running, dto.Status);
    }
}

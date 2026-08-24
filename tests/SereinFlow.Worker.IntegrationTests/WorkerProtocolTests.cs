using SereinFlow.Worker.Protocol;

namespace SereinFlow.Worker.IntegrationTests;

public sealed class WorkerProtocolTests
{
    [Fact]
    public void CodecRoundTripsVersionedEnvelope()
    {
        var source = WorkerMessage.Create(WorkerProtocolConstants.HeartbeatKind, "{\"workerId\":\"runner-1\"}", Guid.NewGuid());

        var result = WorkerProtocolCodec.Deserialize(WorkerProtocolCodec.Serialize(source));

        Assert.Equal(WorkerProtocolConstants.Version, result.ProtocolVersion);
        Assert.Equal(source.Kind, result.Kind);
        Assert.Equal(source.RunId, result.RunId);
        Assert.Equal(source.PayloadJson, result.PayloadJson);
    }

    [Fact]
    public void CodecRejectsProtocolMismatchesAndOversizedMessages()
    {
        var mismatch = new WorkerMessage(WorkerProtocolConstants.Version + 1, WorkerProtocolConstants.RunKind, "request");
        var oversized = new string('x', WorkerProtocolConstants.MaxMessageBytes + 1);

        var mismatchError = Assert.Throws<WorkerProtocolException>(() => WorkerProtocolCodec.Serialize(mismatch));
        var oversizedError = Assert.Throws<WorkerProtocolException>(() => WorkerProtocolCodec.Deserialize(oversized));

        Assert.Equal("worker.protocol_mismatch", mismatchError.Code);
        Assert.Equal("worker.message_too_large", oversizedError.Code);
    }
}

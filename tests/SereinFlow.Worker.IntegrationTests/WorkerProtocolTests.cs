using System.Text;
using System.Text.Json;
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
    public void CodecKeepsChineseDiagnosticsReadable()
    {
        var payload = WorkerProtocolCodec.SerializePayload(new
        {
            errorMessage = "缺少类库必需输入“left”。"
        });
        var message = WorkerMessage.Create(
            WorkerProtocolConstants.EventKind,
            payload,
            Guid.NewGuid());

        var json = WorkerProtocolCodec.Serialize(message);

        Assert.Contains("缺少类库必需输入", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u7F3A", json, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public async Task ReaderSkipsPlainTextStdoutNoiseBeforeProtocolMessage()
    {
        var diagnostics = new List<string>();
        var message = WorkerMessage.Create(WorkerProtocolConstants.HeartbeatKind);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes($"diagnostic noise\n{WorkerProtocolCodec.Serialize(message)}\n"));
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        var result = await WorkerProtocolCodec.ReadAsync(reader, diagnostics.Add);

        Assert.NotNull(result);
        Assert.Equal(WorkerProtocolConstants.HeartbeatKind, result!.Kind);
        Assert.Equal(["diagnostic noise"], diagnostics);
    }

    [Fact]
    public void InvalidJsonRetainsRawMessageAndParserLocation()
    {
        const string raw = "{\"kind\": broken}";

        var exception = Assert.Throws<WorkerProtocolException>(() => WorkerProtocolCodec.Deserialize(raw));

        Assert.Equal("worker.invalid_message", exception.Code);
        Assert.Equal(raw, exception.RawMessage);
        Assert.NotNull(exception.JsonBytePositionInLine);
        Assert.IsType<JsonException>(exception.InnerException);
    }
}

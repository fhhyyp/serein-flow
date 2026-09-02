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
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes($"diagnostic noise\n{WorkerProtocolCodec.Serialize(message)}\n"));
        await using var output = new MemoryStream();
        await using var transport = new StdioWorkerTransport(input, output, diagnostics.Add, leaveOpen: true);

        var result = await transport.ReceiveAsync();

        Assert.NotNull(result);
        Assert.Equal(WorkerProtocolConstants.HeartbeatKind, result!.Kind);
        Assert.Equal(["diagnostic noise"], diagnostics);
    }

    [Fact]
    public async Task StdioTransportSerializesConcurrentSendsIntoCompleteLines()
    {
        await using var input = new MemoryStream();
        await using var output = new MemoryStream();
        await using var transport = new StdioWorkerTransport(input, output, leaveOpen: true);

        await Task.WhenAll(Enumerable.Range(1, 16).Select(index =>
            transport.SendAsync(WorkerMessage.Create(WorkerProtocolConstants.EventKind, $"{{\"index\":{index}}}" )).AsTask()));

        var lines = Encoding.UTF8.GetString(output.ToArray())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(16, lines.Length);
        Assert.All(lines, line => Assert.Equal(WorkerProtocolConstants.EventKind, WorkerProtocolCodec.Deserialize(line).Kind));
    }

    [Fact]
    public async Task StdioTransportRejectsConcurrentReceiveLoopsAndReportsRemoteEof()
    {
        var input = new BlockingReadStream();
        await using var output = new MemoryStream();
        await using var transport = new StdioWorkerTransport(input, output, leaveOpen: true);

        var firstReceive = transport.ReceiveAsync().AsTask();
        await input.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var exception = await Assert.ThrowsAsync<WorkerTransportException>(
            () => transport.ReceiveAsync().AsTask());
        Assert.Equal(WorkerTransportErrorCodes.ReceiveConcurrent, exception.Code);

        input.Release();
        Assert.Null(await firstReceive);
        Assert.Equal(WorkerTransportCloseReason.RemoteEof, transport.CloseResult.Reason);
        Assert.True(transport.CloseResult.IsRemote);
    }

    [Fact]
    public async Task StdioTransportRejectsInvalidUtf8Frames()
    {
        await using var input = new MemoryStream([0xFF, 0x0A]);
        await using var output = new MemoryStream();
        await using var transport = new StdioWorkerTransport(input, output, leaveOpen: true);

        var exception = await Assert.ThrowsAsync<WorkerTransportException>(
            () => transport.ReceiveAsync().AsTask());

        Assert.Equal(WorkerTransportErrorCodes.InvalidFrame, exception.Code);
        Assert.Equal(WorkerTransportCloseReason.Faulted, transport.CloseResult.Reason);
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

    private sealed class BlockingReadStream : Stream
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _release.TrySetResult();

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadStarted.TrySetResult();
            _release.Task.GetAwaiter().GetResult();
            return 0;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
            => WaitForReleaseAsync(cancellationToken);

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
            => WaitForReleaseAsync(cancellationToken).AsTask();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private async ValueTask<int> WaitForReleaseAsync(CancellationToken cancellationToken)
        {
            ReadStarted.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return 0;
        }
    }
}

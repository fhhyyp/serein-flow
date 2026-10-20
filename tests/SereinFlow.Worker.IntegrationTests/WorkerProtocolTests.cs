using System.Text;
using System.Text.Json;
using SereinFlow.Worker.Protocol;
using SereinFlow.Contracts;

namespace SereinFlow.Worker.IntegrationTests;

public sealed class WorkerProtocolTests
{
    [Fact]
    public void CodecRoundTripsVersionedEnvelope()
    {
        var newGuidResult = Guid.NewGuid();
        var source = WorkerMessage.Create(WorkerProtocolConstants.HeartbeatKind, "{\"workerId\":\"runner-1\"}", newGuidResult);
        var serializeResult = WorkerProtocolCodec.Serialize(source);
        var result = WorkerProtocolCodec.Deserialize(serializeResult);
        Assert.Equal(WorkerProtocolConstants.Version, result.ProtocolVersion);
        Assert.Equal(source.Kind, result.Kind);
        Assert.Equal(source.RunId, result.RunId);
        Assert.Equal(source.PayloadJson, result.PayloadJson);
    }

    [Fact]
    public void CodecKeepsChineseDiagnosticsReadable()
    {
        var payload = WorkerProtocolCodec.SerializePayload(new { errorMessage = "缺少类库必需输入“left”。" });
        var newGuidResult1 = Guid.NewGuid();
        var message = WorkerMessage.Create(WorkerProtocolConstants.EventKind, payload, newGuidResult1);
        var json = WorkerProtocolCodec.Serialize(message);
        Assert.Contains("缺少类库必需输入", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u7F3A", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CodecRejectsProtocolMismatchesAndOversizedMessages()
    {
        var mismatch = new WorkerMessage(WorkerProtocolConstants.Version + 1, WorkerProtocolConstants.RunKind, "request");
        var oversized = new string('x', WorkerProtocolConstants.MaxMessageBytes + 1);
        var mismatchError = Assert.Throws<WorkerProtocolException>(() =>
        {
            var serializeResult2 = WorkerProtocolCodec.Serialize(mismatch);
            return serializeResult2;
        });
        var oversizedError = Assert.Throws<WorkerProtocolException>(() =>
        {
            var deserializeResult = WorkerProtocolCodec.Deserialize(oversized);
            return deserializeResult;
        });
        Assert.Equal(WorkerErrorCodes.ProtocolMismatch, mismatchError.Code);
        Assert.Equal(WorkerErrorCodes.MessageTooLarge, oversizedError.Code);
    }

    [Fact]
    public async Task ReaderSkipsPlainTextStdoutNoiseBeforeProtocolMessage()
    {
        var diagnostics = new List<string>();
        var message = WorkerMessage.Create(WorkerProtocolConstants.HeartbeatKind);
        var evaluatedValue = $"diagnostic noise\n{WorkerProtocolCodec.Serialize(message)}\n";
        var getBytesResult = Encoding.UTF8.GetBytes(evaluatedValue);
        await using var input = new MemoryStream(getBytesResult);
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
        var selectResult = Enumerable.Range(1, 16)
            .Select(index =>
            {
                var createResult = WorkerMessage.Create(WorkerProtocolConstants.EventKind, $"{{\"index\":{index}}}");
                var sendAsyncResult = transport.SendAsync(createResult);
                var asTaskResult = sendAsyncResult.AsTask();
                return asTaskResult;
            });
        await Task.WhenAll(selectResult);
        var toArrayResult = output.ToArray();
        var getStringResult = Encoding.UTF8.GetString(toArrayResult);
        var lines = getStringResult.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(16, lines.Length);
        Assert.All(lines, line =>
        {
            var evaluatedValue3 = WorkerProtocolCodec.Deserialize(line).Kind;
            Assert.Equal(WorkerProtocolConstants.EventKind, evaluatedValue3);
        });
    }

    [Fact]
    public async Task StdioTransportRejectsConcurrentReceiveLoopsAndReportsRemoteEof()
    {
        var input = new BlockingReadStream();
        await using var output = new MemoryStream();
        await using var transport = new StdioWorkerTransport(input, output, leaveOpen: true);
        var receiveAsyncResult5 = transport.ReceiveAsync();
        var firstReceive = receiveAsyncResult5.AsTask();
        var fromSecondsResult = TimeSpan.FromSeconds(5);
        await input.ReadStarted.Task.WaitAsync(fromSecondsResult);
        var exception = await Assert.ThrowsAsync<WorkerTransportException>(() =>
        {
            var receiveAsyncResult = transport.ReceiveAsync();
            var asTaskResult4 = receiveAsyncResult.AsTask();
            return asTaskResult4;
        });
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
        var exception = await Assert.ThrowsAsync<WorkerTransportException>(() =>
        {
            var receiveAsyncResult6 = transport.ReceiveAsync();
            var asTaskResult7 = receiveAsyncResult6.AsTask();
            return asTaskResult7;
        });
        Assert.Equal(WorkerTransportErrorCodes.InvalidFrame, exception.Code);
        Assert.Equal(WorkerTransportCloseReason.Faulted, transport.CloseResult.Reason);
    }

    [Fact]
    public void InvalidJsonRetainsRawMessageAndParserLocation()
    {
        const string raw = "{\"kind\": broken}";
        var exception = Assert.Throws<WorkerProtocolException>(() =>
        {
            var deserializeResult8 = WorkerProtocolCodec.Deserialize(raw);
            return deserializeResult8;
        });
        Assert.Equal(WorkerErrorCodes.InvalidMessage, exception.Code);
        Assert.Equal(raw, exception.RawMessage);
        Assert.NotNull(exception.JsonBytePositionInLine);
        Assert.IsType<JsonException>(exception.InnerException);
    }

    private sealed class BlockingReadStream : Stream
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release()
        {
            _release.TrySetResult();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadStarted.TrySetResult();
            var getAwaiterResult = _release.Task.GetAwaiter();
            getAwaiterResult.GetResult();
            return 0;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var waitForReleaseAsyncResult = WaitForReleaseAsync(cancellationToken);
            return waitForReleaseAsyncResult;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var waitForReleaseAsyncResult10 = WaitForReleaseAsync(cancellationToken);
            var asTaskResult11 = waitForReleaseAsyncResult10.AsTask();
            return asTaskResult11;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        private async ValueTask<int> WaitForReleaseAsync(CancellationToken cancellationToken)
        {
            ReadStarted.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return 0;
        }
    }
}

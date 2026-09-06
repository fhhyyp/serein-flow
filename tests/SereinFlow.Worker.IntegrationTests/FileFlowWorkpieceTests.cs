using System.Text;
using SereinFlow.Library;
using SereinFlow.Runtime.Abstractions;
using SereinFlow.Worker.Runner;

namespace SereinFlow.Worker.IntegrationTests;

public sealed class FileFlowWorkpieceTests
{
    [Fact]
    public void UploadNodeOutputStringStoresUtf8Text()
    {
        using var directory = new TemporaryDirectory();
        var runId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var content = "第一行\r\nSecond line";
        var workpiece = new FileFlowWorkpiece(directory.Path, runId);
        var context = new TestFlowContext(runId, "text-node", executionId);

        var info = workpiece.UploadNodeOutput(context, "result.txt", content);

        Assert.Equal(FlowWorkpieceKind.File, info.Kind);
        Assert.Equal("result.txt", info.Name);
        Assert.Equal(FlowWorkpieceContentTypes.Text, info.ContentType);
        Assert.Equal(Encoding.UTF8.GetByteCount(content), info.Length);
        Assert.Equal(
            content,
            File.ReadAllText(Path.Combine(directory.Path, runId.ToString("N"), info.Id + ".bin"), Encoding.UTF8));
    }

    private sealed class TestFlowContext(Guid runId, string nodeId, Guid executionId) : IFlowContext
    {
        public Guid RunId { get; } = runId;

        public string NodeId { get; } = nodeId;

        public Guid ExecutionId { get; } = executionId;

        public CancellationToken CancellationToken => CancellationToken.None;

        public void SelectSuccess()
        {
        }

        public void SelectFailure(string? code = null, string? message = null)
        {
        }

        public void SelectError(string? code = null, string? message = null)
        {
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"sereinflow-workpiece-upload-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}

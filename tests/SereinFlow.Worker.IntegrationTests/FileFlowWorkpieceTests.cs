using System.Text;
using SereinFlow.Library;
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
        var getByteCountResult = Encoding.UTF8.GetByteCount(content);
        Assert.Equal(getByteCountResult, info.Length);
        var toStringResult = runId.ToString("N");
        var combineResult = Path.Combine(directory.Path, toStringResult, info.Id + ".bin");
        var readAllTextResult = File.ReadAllText(combineResult, Encoding.UTF8);
        Assert.Equal(content, readAllTextResult);
    }

    private sealed class TestFlowContext(Guid runId, string nodeId, Guid executionId) : IFlowContext
    {
        public LibraryVariableService Variables { get; } = new();
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
            var getTempPathResult = System.IO.Path.GetTempPath();
            var evaluatedValue = $"sereinflow-workpiece-upload-tests-{Guid.NewGuid():N}";
            Path = System.IO.Path.Combine(getTempPathResult, evaluatedValue);
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}

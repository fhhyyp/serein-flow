using System.Text.Json;
using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;
using SereinFlow.Infrastructure.Configuration;
using SereinFlow.Infrastructure.Persistence;
using SereinFlow.Library;

namespace SereinFlow.Infrastructure.Tests;
public sealed class FileFlowWorkpieceStoreTests
{
    [Fact]
    public async Task ListsMetadataAndOpensOnlyTheMatchingRunFile()
    {
        using var directory = new TemporaryDirectory();
        var runId = Guid.NewGuid();
        var workpieceId = "0123456789abcdef0123456789abcdef";
        var toStringResult = runId.ToString("N");
        var runDirectory = Path.Combine(directory.Path, toStringResult);
        Directory.CreateDirectory(runDirectory);
        var payload = new byte[]
        {
            1,
            2,
            3,
            4
        };
        var executionId = Guid.NewGuid();
        var combineResult = Path.Combine(runDirectory, workpieceId + ".bin");
        await File.WriteAllBytesAsync(combineResult, payload);
        var info = new FlowWorkpieceInfo(workpieceId, FlowWorkpieceKind.Image, "inspection.png", "image/png", payload.Length, DateTimeOffset.UtcNow, "opencv-node", executionId);
        var combineResult1 = Path.Combine(runDirectory, workpieceId + ".json");
        var createContractOptionsResult = SereinJsonSerialization.CreateContractOptions();
        var serializeResult = JsonSerializer.Serialize(info, createContractOptionsResult);
        await File.WriteAllTextAsync(combineResult1, serializeResult);
        var combineResult2 = Path.Combine(directory.Path, "sereinflow.db");
        var combineResult3 = Path.Combine(directory.Path, "libraries");
        var combineResult4 = Path.Combine(directory.Path, "script-artifacts");
        var combineResult5 = Path.Combine(directory.Path, "mcp-staging");
        var options = new SereinFlowStorageOptions(directory.Path, combineResult2, combineResult3, combineResult4, combineResult5, directory.Path);
        var store = new FileFlowWorkpieceStore(options);
        var items = await store.ListAsync(runId);
        var item = Assert.Single(items);
        Assert.Equal(workpieceId, item.Id);
        Assert.Equal("Image", item.Kind);
        Assert.Equal("inspection.png", item.Name);
        Assert.Equal(payload.Length, item.Length);
        Assert.Equal("opencv-node", item.NodeId);
        Assert.Equal(executionId, item.ExecutionId);
        await using var stream = await store.OpenReadAsync(runId, workpieceId);
        Assert.NotNull(stream);
        using var copy = new MemoryStream();
        await stream!.CopyToAsync(copy);
        var toArrayResult = copy.ToArray();
        Assert.Equal(payload, toArrayResult);
        var newGuidResult = Guid.NewGuid();
        var findAsyncResult = await store.FindAsync(newGuidResult, workpieceId);
        Assert.Null(findAsyncResult);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            var getTempPathResult = System.IO.Path.GetTempPath();
            var evaluatedValue = $"sereinflow-workpiece-tests-{Guid.NewGuid():N}";
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

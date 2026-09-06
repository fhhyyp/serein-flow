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
        var runDirectory = Path.Combine(directory.Path, runId.ToString("N"));
        Directory.CreateDirectory(runDirectory);
        var payload = new byte[] { 1, 2, 3, 4 };
        var executionId = Guid.NewGuid();
        await File.WriteAllBytesAsync(Path.Combine(runDirectory, workpieceId + ".bin"), payload);
        var info = new FlowWorkpieceInfo(
            workpieceId,
            FlowWorkpieceKind.Image,
            "inspection.png",
            "image/png",
            payload.Length,
            DateTimeOffset.UtcNow,
            "opencv-node",
            executionId);
        await File.WriteAllTextAsync(
            Path.Combine(runDirectory, workpieceId + ".json"),
            JsonSerializer.Serialize(info, SereinJsonSerialization.CreateContractOptions()));

        var options = new SereinFlowStorageOptions(
            directory.Path,
            Path.Combine(directory.Path, "sereinflow.db"),
            Path.Combine(directory.Path, "libraries"),
            Path.Combine(directory.Path, "script-artifacts"),
            Path.Combine(directory.Path, "mcp-staging"),
            directory.Path);
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
        Assert.Equal(payload, copy.ToArray());
        Assert.Null(await store.FindAsync(Guid.NewGuid(), workpieceId));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"sereinflow-workpiece-tests-{Guid.NewGuid():N}");
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

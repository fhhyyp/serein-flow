using SereinFlow.Domain;
using SereinFlow.ScriptAdapter;

namespace SereinFlow.ScriptAdapter.Tests;

public sealed class ScriptArtifactCacheTests
{
    [Fact]
    public async Task ParallelStoresSharePublishedFingerprintAndKeepDifferentVersions()
    {
        var operationResult10 = Path.GetTempPath();
        var operationResult11 = "script-cache-" + Guid.NewGuid().ToString("N");
        var root = Path.Combine(operationResult10, operationResult11);
        try
        {
            var first = ScriptNodeDefinition.Create("node", "return 1", "1");
            var second = ScriptNodeDefinition.Create("node", "return 2", "1");
            var operationResult1 = Enumerable.Range(0, 8).Select(index =>
            {
                var operationResult2 = Task.Run(() =>
                {
                    var operationResult1 = new ScriptArtifactStore(root).RebuildProject("project", [index % 2 == 0 ? first : second]);
                    return operationResult1;
                });
                return operationResult2;
            });
            var builds = await Task.WhenAll(operationResult1);
            Assert.All(builds, build =>
            {
                Assert.True(build.IsSuccess);
            });
            var store = new ScriptArtifactStore(root);
            var operationResult2 = store.TryLoad("project", first, out var chunk, out var path);
            Assert.True(operationResult2);
            var timestamp = File.GetLastWriteTimeUtc(path!);
            var operationResult3 = store.RebuildProject("project", [first]).IsSuccess;
            Assert.True(operationResult3);
            var operationResult4 = File.GetLastWriteTimeUtc(path!);
            Assert.Equal(timestamp, operationResult4);
            var operationResult5 = store.TryLoad("project", first, out var cached, out _);
            Assert.True(operationResult5);
            Assert.Same(chunk, cached);
            var operationResult6 = store.TryLoad("project", second, out var otherChunk, out var otherPath);
            Assert.True(operationResult6);
            Assert.NotSame(chunk, otherChunk);
            Assert.NotEqual(path, otherPath);
            File.WriteAllText(otherPath!, "corrupt");
            var repairStore = new ScriptArtifactStore(root);
            var operationResult7 = repairStore.TryLoad("project", second, out _, out _);
            Assert.False(operationResult7);
            var operationResult8 = repairStore.RebuildProject("project", [second]).IsSuccess;
            Assert.True(operationResult8);
            var operationResult9 = repairStore.TryLoad("project", second, out _, out _);
            Assert.True(operationResult9);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

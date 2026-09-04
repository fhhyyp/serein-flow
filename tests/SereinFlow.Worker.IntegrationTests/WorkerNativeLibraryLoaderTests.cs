using System.Runtime.InteropServices;
using SereinFlow.Library;
using SereinFlow.Worker.Runner;

namespace SereinFlow.Worker.IntegrationTests;

public sealed class WorkerNativeLibraryLoaderTests
{
    [Fact]
    public void DirectoryLoadingUsesCurrentRidSkipsManagedAssembliesAndIsIdempotent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sereinflow-native-loader-{Guid.NewGuid():N}");
        var rid = RuntimeInformation.RuntimeIdentifier;
        var nativeDirectory = Path.Combine(root, "runtimes", rid, "native");
        Directory.CreateDirectory(nativeDirectory);
        var nativeFile = Path.Combine(nativeDirectory, "native-one.dll");
        var managedFile = Path.Combine(nativeDirectory, "managed-one.dll");
        File.WriteAllBytes(nativeFile, [0x4D, 0x5A]);
        File.Copy(typeof(WorkerNativeLibraryLoader).Assembly.Location, managedFile);

        try
        {
            var loadedPaths = new List<string>();
            using var loader = new WorkerNativeLibraryLoader(
                root,
                path =>
                {
                    loadedPaths.Add(path);
                    return (nint)1;
                });

            Assert.True(loader.LoadNativeLibrary("runtimes/{rid}/native/native-one.dll"));
            loader.LoadNativeLibraryDirectory("runtimes/{rid}/native", recursive: false);
            loader.LoadNativeLibraryDirectory("runtimes/{rid}/native", recursive: false);

            var loadedPath = Assert.Single(loadedPaths);
            Assert.Equal(Path.GetFullPath(nativeFile), loadedPath);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void RequiredDirectoryRejectsUnsafeOrMissingPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sereinflow-native-loader-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            using var loader = new WorkerNativeLibraryLoader(root, static _ => (nint)1);

            var missing = Assert.Throws<FlowNativeLibraryException>(() =>
                loader.LoadNativeLibraryDirectory("missing", required: true));
            Assert.Equal("library.native_directory_missing", missing.Code);

            var unsafePath = Assert.Throws<FlowNativeLibraryException>(() =>
                loader.LoadNativeLibraryDirectory("../outside", required: true));
            Assert.Equal("library.native_directory_invalid", unsafePath.Code);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

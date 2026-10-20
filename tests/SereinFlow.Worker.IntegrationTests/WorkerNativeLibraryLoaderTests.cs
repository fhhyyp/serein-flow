using System.Runtime.InteropServices;
using SereinFlow.Contracts;
using SereinFlow.Library;
using SereinFlow.Worker.Runner;

namespace SereinFlow.Worker.IntegrationTests;
public sealed class WorkerNativeLibraryLoaderTests
{
    [Fact]
    public void DirectoryLoadingUsesCurrentRidSkipsManagedAssembliesAndIsIdempotent()
    {
        var getTempPathResult = Path.GetTempPath();
        var evaluatedValue = $"sereinflow-native-loader-{Guid.NewGuid():N}";
        var root = Path.Combine(getTempPathResult, evaluatedValue);
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
            using var loader = new WorkerNativeLibraryLoader(root, path =>
            {
                loadedPaths.Add(path);
                return (nint)1;
            });
            var loadNativeLibraryResult = loader.LoadNativeLibrary("runtimes/{rid}/native/native-one.dll");
            Assert.True(loadNativeLibraryResult);
            loader.LoadNativeLibraryDirectory("runtimes/{rid}/native", recursive: false);
            loader.LoadNativeLibraryDirectory("runtimes/{rid}/native", recursive: false);
            var loadedPath = Assert.Single(loadedPaths);
            var getFullPathResult = Path.GetFullPath(nativeFile);
            Assert.Equal(getFullPathResult, loadedPath);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void RequiredDirectoryRejectsUnsafeOrMissingPaths()
    {
        var getTempPathResult1 = Path.GetTempPath();
        var evaluatedValue2 = $"sereinflow-native-loader-{Guid.NewGuid():N}";
        var root = Path.Combine(getTempPathResult1, evaluatedValue2);
        Directory.CreateDirectory(root);
        try
        {
            using var loader = new WorkerNativeLibraryLoader(root, static _ => (nint)1);
            var missing = Assert.Throws<FlowNativeLibraryException>(() =>
            {
                loader.LoadNativeLibraryDirectory("missing", required: true);
            });
            Assert.Equal(LibraryErrorCodes.NativeDirectoryMissing, missing.Code);
            var unsafePath = Assert.Throws<FlowNativeLibraryException>(() =>
            {
                loader.LoadNativeLibraryDirectory("../outside", required: true);
            });
            Assert.Equal(LibraryErrorCodes.NativeDirectoryInvalid, unsafePath.Code);
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
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}

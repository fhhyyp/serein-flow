using System.IO.Compression;
using SereinFlow.Application;
using SereinFlow.Infrastructure.Persistence;
using SereinFlow.TestLibrary;

namespace SereinFlow.Infrastructure.Tests;

public sealed class LibraryCatalogTests
{
    [Fact]
    public async Task UploadPersistsSafePackageMetadataAndIsIdempotent()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}.db");
        var libraryRoot = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}");
        try
        {
            using var database = new SqliteDatabase(new SqliteDatabaseOptions(databasePath));
            database.Initialize();
            var catalog = new SqliteLibraryCatalogService(database, new LibraryCatalogOptions(libraryRoot));

            await using var package = CreatePackage("DemoLibrary-1.2.3.zip", "DemoLibrary.dll", typeof(object).Assembly.Location);
            var packageBytes = package.ToArray();
            package.Position = 0;
            var first = await catalog.UploadAsync(package, "DemoLibrary-1.2.3.zip");

            Assert.False(first.AlreadyExists);
            Assert.Equal("DemoLibrary", first.Library.Name);
            Assert.Equal("1.2.3", first.Library.Version);
            Assert.Equal(64, first.Library.Sha256.Length);
            Assert.Single(catalog.List());

            await using var duplicate = new MemoryStream(packageBytes);
            var second = await catalog.UploadAsync(duplicate, "DemoLibrary-1.2.3.zip");

            Assert.True(second.AlreadyExists);
            Assert.Equal(first.Library.Id, second.Library.Id);
        }
        finally
        {
            TryDelete(databasePath);
            TryDeleteDirectory(libraryRoot);
        }
    }

    [Fact]
    public async Task UploadRejectsZipSlipEntriesBeforePersistingPackage()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}.db");
        var libraryRoot = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}");
        try
        {
            using var database = new SqliteDatabase(new SqliteDatabaseOptions(databasePath));
            database.Initialize();
            var catalog = new SqliteLibraryCatalogService(database, new LibraryCatalogOptions(libraryRoot));
            await using var package = CreatePackageWithTraversal("UnsafeLibrary-1.0.0.zip");

            var error = await Assert.ThrowsAsync<LibraryUploadException>(() => catalog.UploadAsync(package, "UnsafeLibrary-1.0.0.zip"));

            Assert.Equal(422, error.StatusCode);
            Assert.Empty(catalog.List());
        }
        finally
        {
            TryDelete(databasePath);
            TryDeleteDirectory(libraryRoot);
        }
    }

    [Fact]
    public async Task UploadReadsSharedLibraryAttributesAndParameterMetadata()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}.db");
        var libraryRoot = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}");
        try
        {
            using var database = new SqliteDatabase(new SqliteDatabaseOptions(databasePath));
            database.Initialize();
            var catalog = new SqliteLibraryCatalogService(database, new LibraryCatalogOptions(libraryRoot));
            await using var package = CreatePackage(
                "SereinFlow.TestLibrary-1.0.0.zip",
                "SereinFlow.TestLibrary.dll",
                typeof(MathNodes).Assembly.Location);

            var result = await catalog.UploadAsync(package, "SereinFlow.TestLibrary-1.0.0.zip");

            Assert.Equal(5, result.Library.Nodes.Count);
            var add = Assert.Single(result.Library.Nodes, node => node.MethodName == "Add");
            Assert.Equal(SereinFlow.Contracts.NodeTypeDto.Action, add.Type);
            Assert.Equal("Add numbers", add.DisplayName);
            Assert.Equal("Adds two integers.", add.Description);
            Assert.Equal(["left", "right"], add.Parameters.Select(parameter => parameter.Name).ToArray());

            var flipflop = Assert.Single(result.Library.Nodes, node => node.MethodName == "IsPositive");
            Assert.Equal(SereinFlow.Contracts.NodeTypeDto.Flipflop, flipflop.Type);
        }
        finally
        {
            TryDelete(databasePath);
            TryDeleteDirectory(libraryRoot);
        }
    }

    private static MemoryStream CreatePackage(string archiveName, string dllName, string dllPath)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry($"{Path.GetFileNameWithoutExtension(archiveName)}/{dllName}");
            using var target = entry.Open();
            using var source = File.OpenRead(dllPath);
            source.CopyTo(target);
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreatePackageWithTraversal(string archiveName)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("../outside.dll");
        }

        stream.Position = 0;
        return stream;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

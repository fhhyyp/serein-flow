using System.IO.Compression;
using SereinFlow.Application;
using SereinFlow.Core.Api;
using SereinFlow.Infrastructure.Persistence;
using SereinFlow.Runtime.Abstractions;
using SereinFlow.TestLibrary;

namespace SereinFlow.Infrastructure.Tests;

public sealed class LibraryCatalogTests
{
    [Fact]
    public void StandaloneLibrarySdkOwnsPublicMetadataAndContextContracts()
    {
        Assert.Equal("SereinFlow.Library", typeof(FlowLibraryAttribute).Assembly.GetName().Name);
        Assert.Equal("SereinFlow.Library", typeof(IFlowContext).Assembly.GetName().Name);
        Assert.DoesNotContain(
            typeof(FlowLibraryAttribute).Assembly.GetReferencedAssemblies(),
            reference => reference.Name is "SereinFlow.Domain" or "SereinFlow.Application" or "SereinFlow.Runtime.Abstractions");
    }

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

            await using var package = CreatePackage("SereinFlow.TestLibrary-1.2.3.zip", "SereinFlow.TestLibrary.dll", typeof(生产线节点).Assembly.Location);
            var packageBytes = package.ToArray();
            package.Position = 0;
            var first = await catalog.UploadAsync(package, "SereinFlow.TestLibrary-1.2.3.zip");

            Assert.False(first.AlreadyExists);
            Assert.Equal("SereinFlow.TestLibrary", first.Library.Name);
            Assert.Equal("1.2.3", first.Library.Version);
            Assert.Equal(64, first.Library.Sha256.Length);
            Assert.Single(catalog.List());

            await using var duplicate = new MemoryStream(packageBytes);
            var second = await catalog.UploadAsync(duplicate, "SereinFlow.TestLibrary-1.2.3.zip");

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
    public async Task UploadRejectsDllWhenAssemblyNameDoesNotMatchPackageName()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}.db");
        var libraryRoot = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}");
        try
        {
            using var database = new SqliteDatabase(new SqliteDatabaseOptions(databasePath));
            database.Initialize();
            var catalog = new SqliteLibraryCatalogService(database, new LibraryCatalogOptions(libraryRoot));
            await using var package = CreatePackage(
                "RenamedLibrary-1.0.0.zip",
                "RenamedLibrary.dll",
                typeof(生产线节点).Assembly.Location);

            var error = await Assert.ThrowsAsync<LibraryUploadException>(() =>
                catalog.UploadAsync(package, "RenamedLibrary-1.0.0.zip"));

            Assert.Equal(422, error.StatusCode);
            Assert.Contains("does not match", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(catalog.List());
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(libraryRoot, "packages"), "*.zip"));
        }
        finally
        {
            TryDelete(databasePath);
            TryDeleteDirectory(libraryRoot);
        }
    }

    [Fact]
    public async Task UploadRejectsDuplicateExpectedPdbEntries()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}.db");
        var libraryRoot = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}");
        try
        {
            using var database = new SqliteDatabase(new SqliteDatabaseOptions(databasePath));
            database.Initialize();
            var catalog = new SqliteLibraryCatalogService(database, new LibraryCatalogOptions(libraryRoot));
            await using var package = CreatePackageWithDuplicateSymbols(
                "SereinFlow.TestLibrary-1.7.0.zip",
                "SereinFlow.TestLibrary.dll",
                typeof(生产线节点).Assembly.Location);

            var error = await Assert.ThrowsAsync<LibraryUploadException>(() =>
                catalog.UploadAsync(package, "SereinFlow.TestLibrary-1.7.0.zip"));

            Assert.Equal(422, error.StatusCode);
            Assert.Contains("more than one", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(catalog.List());
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(libraryRoot, "packages"), "*.zip"));
        }
        finally
        {
            TryDelete(databasePath);
            TryDeleteDirectory(libraryRoot);
        }
    }

    [Fact]
    public async Task UploadAcceptsPublishDependenciesAndNativeRuntimeAssets()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}.db");
        var libraryRoot = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}");
        try
        {
            using var database = new SqliteDatabase(new SqliteDatabaseOptions(databasePath));
            database.Initialize();
            var catalog = new SqliteLibraryCatalogService(database, new LibraryCatalogOptions(libraryRoot));
            await using var package = CreatePackageWithPublishOutputs(
                "SereinFlow.TestLibrary-1.8.0.zip",
                "SereinFlow.TestLibrary.dll",
                typeof(生产线节点).Assembly.Location,
                typeof(FlowLibraryAttribute).Assembly.Location);

            var uploaded = await catalog.UploadAsync(package, "SereinFlow.TestLibrary-1.8.0.zip");

            Assert.Equal("SereinFlow.TestLibrary", uploaded.Library.Name);
            var storedPackagePath = Path.Combine(libraryRoot, "packages", $"{uploaded.Library.Id}.zip");
            using var archive = ZipFile.OpenRead(storedPackagePath);
            var entries = archive.Entries
                .Where(entry => !string.IsNullOrEmpty(entry.Name))
                .Select(entry => entry.FullName.Replace('\\', '/'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains("SereinFlow.TestLibrary-1.8.0/SereinFlow.TestLibrary.dll", entries);
            Assert.Contains("SereinFlow.TestLibrary-1.8.0/dependencies/SereinFlow.Library.dll", entries);
            Assert.Contains("SereinFlow.TestLibrary-1.8.0/SereinFlow.TestLibrary.deps.json", entries);
            Assert.Contains("SereinFlow.TestLibrary-1.8.0/SereinFlow.TestLibrary.runtimeconfig.json", entries);
            Assert.Contains("SereinFlow.TestLibrary-1.8.0/runtimes/win-x64/native/OpenCvSharpExtern.dll", entries);
        }
        finally
        {
            TryDelete(databasePath);
            TryDeleteDirectory(libraryRoot);
        }
    }

    [Fact]
    public async Task UploadRejectsSourceFilesEvenWhenTheMainDllIsValid()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}.db");
        var libraryRoot = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}");
        try
        {
            using var database = new SqliteDatabase(new SqliteDatabaseOptions(databasePath));
            database.Initialize();
            var catalog = new SqliteLibraryCatalogService(database, new LibraryCatalogOptions(libraryRoot));
            await using var package = CreatePackageWithUnsupportedFile(
                "SereinFlow.TestLibrary-1.8.1.zip",
                "SereinFlow.TestLibrary.dll",
                typeof(生产线节点).Assembly.Location);

            var error = await Assert.ThrowsAsync<LibraryUploadException>(() =>
                catalog.UploadAsync(package, "SereinFlow.TestLibrary-1.8.1.zip"));

            Assert.Equal(422, error.StatusCode);
            Assert.Contains("source", error.Message, StringComparison.OrdinalIgnoreCase);
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
                "SereinFlow.TestLibrary-1.1.0.zip",
                "SereinFlow.TestLibrary.dll",
                typeof(生产线节点).Assembly.Location);

            var result = await catalog.UploadAsync(package, "SereinFlow.TestLibrary-1.1.0.zip");

            //Assert.Equal(12, result.Library.Nodes.Count);
            var passRate = Assert.Single(result.Library.Nodes, node => node.MethodName == "计算合格率");
            Assert.Equal(SereinFlow.Contracts.NodeTypeDto.Action, passRate.Type);
            Assert.Equal("计算合格率", passRate.DisplayName);
            Assert.Equal("根据合格数量和检测总数计算本批次合格率。", passRate.Description);
            Assert.Equal("生产线设备与质量数据示例库", passRate.FlowLibraryName);
            Assert.Equal(["合格数量", "检测总数"], passRate.Parameters.Select(parameter => parameter.Name).ToArray());
            Assert.Equal("生产线设备与质量数据示例库.计算合格率", passRate.ContractId);
            Assert.Equal(SereinFlow.Contracts.LibraryContractIdentityConfidenceDto.Explicit, passRate.IdentityConfidence);
            Assert.Equal(["合格数量", "检测总数"], passRate.Parameters.Select(parameter => parameter.Id).ToArray());
            Assert.Equal(["pass-count"], passRate.Parameters[0].Aliases);
            Assert.All(passRate.Parameters, parameter => Assert.Equal(SereinFlow.Contracts.LibraryContractIdentityConfidenceDto.Explicit, parameter.IdentityConfidence));

            var manifest = Assert.IsType<SereinFlow.Contracts.LibraryArtifactManifestDto>(result.Library.CompatibilityManifest);
            var manifestNode = Assert.Single(manifest.Nodes, node => node.ContractId == "生产线设备与质量数据示例库.计算合格率");
            Assert.Equal(passRate.OverloadSignature, manifestNode.OverloadSignature);
            Assert.Equal("合格数量", manifestNode.Parameters[0].DisplayName);
            Assert.Equal("合格数量", manifestNode.Parameters[0].ClrName);
            Assert.Equal(["pass-count"], manifestNode.Parameters[0].Aliases);

            var writeInstruction = Assert.Single(result.Library.Nodes, node => node.MethodName == "构建设备写入指令");
            Assert.Equal(["2", "毫米", "0.01"], writeInstruction.Parameters.Skip(3).Select(parameter => parameter.DefaultValue!).ToArray());

            var flipflop = Assert.Single(result.Library.Nodes, node => node.MethodName == "等待设备触发");
            Assert.Equal(SereinFlow.Contracts.NodeTypeDto.Flipflop, flipflop.Type);

            var contextBranch = Assert.Single(result.Library.Nodes, node => node.MethodName == "按设备状态选择分支");
            Assert.Equal(["设备状态"], contextBranch.Parameters.Select(parameter => parameter.Name).ToArray());
            Assert.DoesNotContain(contextBranch.Parameters, parameter => parameter.Name == "流程上下文");

            var variadic = Assert.Single(result.Library.Nodes, node => node.MethodName == "汇总多个检测值");
            var values = Assert.Single(variadic.Parameters);
            Assert.Equal("检测值", values.Name);
            Assert.True(values.IsVariadic);
            Assert.Equal("检测值", values.VariadicGroupId);
            Assert.Equal("System.Int32", values.ElementType);

            var unnamedLibraryNode = Assert.Single(result.Library.Nodes, node => node.MethodName == "读取默认类库名称");
            Assert.Equal("未命名类库节点", unnamedLibraryNode.FlowLibraryName);
            Assert.Equal("未命名类库节点.读取默认类库名称", unnamedLibraryNode.ContractId);
            Assert.Equal(SereinFlow.Contracts.LibraryContractIdentityConfidenceDto.Explicit, unnamedLibraryNode.IdentityConfidence);
            var unnamedLibraryParameter = Assert.Single(unnamedLibraryNode.Parameters);
            Assert.Equal("输入值", unnamedLibraryParameter.Id);
            Assert.Equal(SereinFlow.Contracts.LibraryContractIdentityConfidenceDto.Explicit, unnamedLibraryParameter.IdentityConfidence);

            var mode = Assert.Single(result.Library.Nodes, node => node.MethodName == "设置设备运行模式");
            var modeParameter = Assert.Single(mode.Parameters);
            Assert.NotNull(modeParameter.EnumMetadata);
            var modeMetadata = modeParameter.EnumMetadata!;
            Assert.False(modeMetadata.IsFlags);
            Assert.Equal("SereinFlow.TestLibrary.设备运行模式", modeMetadata.TypeName);
            Assert.Equal("System.Int32", modeMetadata.UnderlyingType);
            Assert.Equal(["自动", "手动", "维护"], modeMetadata.Options.Select(option => option.Name).ToArray());
            Assert.Equal(["0", "1", "2"], modeMetadata.Options.Select(option => option.NumericValue).ToArray());
            Assert.Equal("自动", modeParameter.DefaultValue);

            var permissions = Assert.Single(result.Library.Nodes, node => node.MethodName == "配置设备操作权限");
            var permissionsParameter = Assert.Single(permissions.Parameters);
            Assert.NotNull(permissionsParameter.EnumMetadata);
            var permissionsMetadata = permissionsParameter.EnumMetadata!;
            Assert.True(permissionsMetadata.IsFlags);
            Assert.Equal("System.UInt64", permissionsMetadata.UnderlyingType);
            Assert.Equal(["0", "1", "2", "4", "7"], permissionsMetadata.Options.Select(option => option.NumericValue).ToArray());
            Assert.Equal("读取状态", permissionsParameter.DefaultValue);

            var combinedConfiguration = Assert.Single(result.Library.Nodes, node => node.MethodName == "生成设备配置摘要");
            Assert.Equal(["运行模式", "操作权限"], combinedConfiguration.Parameters.Select(parameter => parameter.Name).ToArray());
            Assert.All(combinedConfiguration.Parameters, parameter => Assert.NotNull(parameter.EnumMetadata));
            Assert.Equal(["自动", "读取状态"], combinedConfiguration.Parameters.Select(parameter => parameter.DefaultValue!).ToArray());

            var listener = Assert.Single(result.Library.Nodes, node => node.MethodName == "监听设备配置变更");
            Assert.Equal(SereinFlow.Contracts.NodeTypeDto.Flipflop, listener.Type);
            Assert.True(listener.IsAwaitable);
            Assert.Equal(["运行模式", "操作权限", "监听间隔毫秒"], listener.Parameters.Select(parameter => parameter.Name).ToArray());
            Assert.NotNull(listener.Parameters[0].EnumMetadata);
            Assert.NotNull(listener.Parameters[1].EnumMetadata);
            Assert.Equal(["自动", "读取状态", "1000"], listener.Parameters.Select(parameter => parameter.DefaultValue!).ToArray());
        }
        finally
        {
            TryDelete(databasePath);
            TryDeleteDirectory(libraryRoot);
        }
    }

    [Fact]
    public async Task DerivedContractIdsRemainStableAcrossPackageVersions()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}.db");
        var libraryRoot = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}");
        try
        {
            using var database = new SqliteDatabase(new SqliteDatabaseOptions(databasePath));
            database.Initialize();
            var catalog = new SqliteLibraryCatalogService(database, new LibraryCatalogOptions(libraryRoot));
            await using var sourcePackage = CreatePackage(
                "SereinFlow.TestLibrary-1.5.2.zip",
                "SereinFlow.TestLibrary.dll",
                typeof(生产线节点).Assembly.Location,
                marker: "source-artifact");
            await using var targetPackage = CreatePackage(
                "SereinFlow.TestLibrary-1.5.3.zip",
                "SereinFlow.TestLibrary.dll",
                typeof(生产线节点).Assembly.Location,
                marker: "target-artifact");

            var source = await catalog.UploadAsync(sourcePackage, "SereinFlow.TestLibrary-1.5.2.zip");
            var target = await catalog.UploadAsync(targetPackage, "SereinFlow.TestLibrary-1.5.3.zip");

            Assert.NotEqual(source.Library.Id, target.Library.Id);
            Assert.Equal(
                source.Library.Nodes
                    .OrderBy(node => node.MethodName, StringComparer.Ordinal)
                    .Select(node => node.ContractId),
                target.Library.Nodes
                    .OrderBy(node => node.MethodName, StringComparer.Ordinal)
                    .Select(node => node.ContractId));
            Assert.Equal(
                source.Library.Nodes
                    .OrderBy(node => node.MethodName, StringComparer.Ordinal)
                    .Select(node => $"{node.ContractId}:{string.Join(",", node.Parameters.Select(parameter => parameter.Id))}"),
                target.Library.Nodes
                    .OrderBy(node => node.MethodName, StringComparer.Ordinal)
                    .Select(node => $"{node.ContractId}:{string.Join(",", node.Parameters.Select(parameter => parameter.Id))}"));
            Assert.All(source.Library.Nodes, node => Assert.Equal(SereinFlow.Contracts.LibraryContractIdentityConfidenceDto.Explicit, node.IdentityConfidence));
            Assert.All(target.Library.Nodes, node => Assert.Equal(SereinFlow.Contracts.LibraryContractIdentityConfidenceDto.Explicit, node.IdentityConfidence));
        }
        finally
        {
            TryDelete(databasePath);
            TryDeleteDirectory(libraryRoot);
        }
    }

    [Fact]
    public async Task InspectionCanCompareAgainstFamilyBaselineWithoutPersistingPackage()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}.db");
        var libraryRoot = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}");
        try
        {
            using var database = new SqliteDatabase(new SqliteDatabaseOptions(databasePath));
            database.Initialize();
            var catalog = new SqliteLibraryCatalogService(database, new LibraryCatalogOptions(libraryRoot));
            await using var sourcePackage = CreatePackage(
                "SereinFlow.TestLibrary-1.6.0.zip",
                "SereinFlow.TestLibrary.dll",
                typeof(生产线节点).Assembly.Location,
                marker: "baseline");
            var source = await catalog.UploadAsync(sourcePackage, "SereinFlow.TestLibrary-1.6.0.zip");
            var family = await catalog.AssignFamilyAsync(
                source.Library.Id,
                new SereinFlow.Contracts.AssignLibraryFamilyRequestDto(null, "生产线兼容性测试"));

            await using var targetPackage = CreatePackage(
                "SereinFlow.TestLibrary-1.6.1.zip",
                "SereinFlow.TestLibrary.dll",
                typeof(生产线节点).Assembly.Location,
                marker: "candidate");
            var inspection = await catalog.InspectAsync(
                targetPackage,
                "SereinFlow.TestLibrary-1.6.1.zip",
                familyId: family!.Id);

            Assert.NotNull(inspection);
            Assert.False(inspection!.AlreadyExists);
            Assert.NotNull(inspection.Compatibility);
            Assert.True(inspection.Compatibility!.IsCompatible);
            Assert.Contains(
                inspection.Compatibility.Issues,
                issue => issue.Classification == SereinFlow.Contracts.LibraryCompatibilityClassificationDto.Exact);
            Assert.Null(await catalog.FindAsync(inspection.Library.Id));
            Assert.Empty(Directory.EnumerateFiles(libraryRoot, ".inspect-*", SearchOption.TopDirectoryOnly));
        }
        finally
        {
            TryDelete(databasePath);
            TryDeleteDirectory(libraryRoot);
        }
    }

    [Fact]
    public async Task ArchiveRetainsTheImmutablePackageForExistingProjectRuns()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}.db");
        var libraryRoot = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}");
        try
        {
            using var database = new SqliteDatabase(new SqliteDatabaseOptions(databasePath));
            database.Initialize();
            var catalog = new SqliteLibraryCatalogService(database, new LibraryCatalogOptions(libraryRoot));
            await using var package = CreatePackage(
                "SereinFlow.TestLibrary-1.1.0.zip",
                "SereinFlow.TestLibrary.dll",
                typeof(生产线节点).Assembly.Location);

            var uploaded = await catalog.UploadAsync(package, "SereinFlow.TestLibrary-1.1.0.zip");
            var packagePath = Path.Combine(libraryRoot, "packages", $"{uploaded.Library.Id}.zip");

            Assert.True(await catalog.ArchiveAsync(uploaded.Library.Id));

            Assert.Empty(await catalog.ListAsync());
            var archived = Assert.Single(await catalog.ListAsync(includeArchived: true));
            Assert.Equal(SereinFlow.Contracts.LibraryLifecycleDto.Archived, archived.Lifecycle);
            Assert.Equal(uploaded.Library.Id, (await catalog.FindAsync(uploaded.Library.Id))!.Id);
            Assert.True(File.Exists(packagePath));
        }
        finally
        {
            TryDelete(databasePath);
            TryDeleteDirectory(libraryRoot);
        }
    }

    [Fact]
    public async Task ReindexOutdatedCatalogRebuildsEnumMetadataFromTheImmutablePackage()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}.db");
        var libraryRoot = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}");
        try
        {
            using var database = new SqliteDatabase(new SqliteDatabaseOptions(databasePath));
            database.Initialize();
            var catalog = new SqliteLibraryCatalogService(database, new LibraryCatalogOptions(libraryRoot));
            await using var package = CreatePackage(
                "SereinFlow.TestLibrary-1.3.0.zip",
                "SereinFlow.TestLibrary.dll",
                typeof(生产线节点).Assembly.Location);

            var uploaded = await catalog.UploadAsync(package, "SereinFlow.TestLibrary-1.3.0.zip");
            database.Client.Ado.ExecuteCommand(
                "UPDATE Libraries SET CatalogSchemaVersion = 0, NodeCatalogJson = '[]' WHERE Id = @id",
                new SqlSugar.SugarParameter("@id", uploaded.Library.Id));

            Assert.Equal(1, await catalog.ReindexOutdatedAsync());

            var reindexed = await catalog.FindAsync(uploaded.Library.Id);
            var mode = Assert.Single(reindexed!.Nodes, node => node.MethodName == "设置设备运行模式");
            Assert.NotNull(Assert.Single(mode.Parameters).EnumMetadata);
        }
        finally
        {
            TryDelete(databasePath);
            TryDeleteDirectory(libraryRoot);
        }
    }

    [Fact]
    public async Task ReindexOutdatedCatalogContinuesAfterOneStoredPackageIsDamaged()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}.db");
        var libraryRoot = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}");
        try
        {
            using var database = new SqliteDatabase(new SqliteDatabaseOptions(databasePath));
            database.Initialize();
            var catalog = new SqliteLibraryCatalogService(database, new LibraryCatalogOptions(libraryRoot));
            await using var damagedPackage = CreatePackage(
                "SereinFlow.TestLibrary-1.3.0.zip",
                "SereinFlow.TestLibrary.dll",
                typeof(生产线节点).Assembly.Location);
            await using var validPackage = CreatePackage(
                "SereinFlow.TestLibrary-1.3.1.zip",
                "SereinFlow.TestLibrary.dll",
                typeof(生产线节点).Assembly.Location,
                marker: "second-package");

            var damaged = await catalog.UploadAsync(damagedPackage, "SereinFlow.TestLibrary-1.3.0.zip");
            var valid = await catalog.UploadAsync(validPackage, "SereinFlow.TestLibrary-1.3.1.zip");
            File.WriteAllBytes(Path.Combine(libraryRoot, "packages", $"{damaged.Library.Id}.zip"), [0x00]);
            database.Client.Ado.ExecuteCommand("UPDATE Libraries SET CatalogSchemaVersion = 0, NodeCatalogJson = '[]'");

            Assert.Equal(1, await catalog.ReindexOutdatedAsync());
            var reindexed = await catalog.FindAsync(valid.Library.Id);
            var mode = Assert.Single(reindexed!.Nodes, node => node.MethodName == "设置设备运行模式");
            Assert.NotNull(Assert.Single(mode.Parameters).EnumMetadata);
        }
        finally
        {
            TryDelete(databasePath);
            TryDeleteDirectory(libraryRoot);
        }
    }

    [Fact]
    public async Task MovingAnArtifactRecalculatesBothFamilyRecommendations()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}.db");
        var libraryRoot = Path.Combine(Path.GetTempPath(), $"sereinflow-library-{Guid.NewGuid():N}");
        try
        {
            using var database = new SqliteDatabase(new SqliteDatabaseOptions(databasePath));
            database.Initialize();
            var catalog = new SqliteLibraryCatalogService(database, new LibraryCatalogOptions(libraryRoot));
            await using var firstPackage = CreatePackage(
                "SereinFlow.TestLibrary-1.0.0.zip",
                "SereinFlow.TestLibrary.dll",
                typeof(生产线节点).Assembly.Location);
            await using var secondPackage = CreatePackage(
                "SereinFlow.TestLibrary-2.0.0.zip",
                "SereinFlow.TestLibrary.dll",
                typeof(生产线节点).Assembly.Location,
                marker: "second-artifact");

            var first = await catalog.UploadAsync(firstPackage, "SereinFlow.TestLibrary-1.0.0.zip");
            var second = await catalog.UploadAsync(secondPackage, "SereinFlow.TestLibrary-2.0.0.zip");
            var sourceFamily = await catalog.AssignFamilyAsync(
                first.Library.Id,
                new SereinFlow.Contracts.AssignLibraryFamilyRequestDto(null, "生产线类库"));
            Assert.NotNull(sourceFamily);

            sourceFamily = await catalog.AssignFamilyAsync(
                second.Library.Id,
                new SereinFlow.Contracts.AssignLibraryFamilyRequestDto(sourceFamily!.Id, null));
            Assert.Equal(second.Library.Id, sourceFamily!.LatestArtifactId);

            var targetFamily = await catalog.AssignFamilyAsync(
                second.Library.Id,
                new SereinFlow.Contracts.AssignLibraryFamilyRequestDto(null, "质检类库"));
            Assert.NotNull(targetFamily);
            Assert.Equal(second.Library.Id, targetFamily!.LatestArtifactId);
            Assert.Equal("质检类库", (await catalog.FindAsync(second.Library.Id))!.FamilyName);

            var families = await catalog.ListFamiliesAsync();
            var refreshedSource = Assert.Single(families, family => family.Id == sourceFamily.Id);
            Assert.Equal(first.Library.Id, refreshedSource.LatestArtifactId);
            Assert.Single(refreshedSource.Artifacts!);

            Assert.True(await catalog.SetLifecycleAsync(second.Library.Id, SereinFlow.Contracts.LibraryLifecycleDto.Archived));
            var archivedTarget = Assert.Single(await catalog.ListFamiliesAsync(), family => family.Id == targetFamily.Id);
            Assert.Null(archivedTarget.LatestArtifactId);
        }
        finally
        {
            TryDelete(databasePath);
            TryDeleteDirectory(libraryRoot);
        }
    }

    private static MemoryStream CreatePackage(string archiveName, string dllName, string dllPath, string? marker = null)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry($"{Path.GetFileNameWithoutExtension(archiveName)}/{dllName}");
            using (var target = entry.Open())
            using (var source = File.OpenRead(dllPath))
            {
                source.CopyTo(target);
            }
            if (!string.IsNullOrWhiteSpace(marker))
            {
                var stem = Path.GetFileNameWithoutExtension(archiveName);
                var separator = stem.LastIndexOf('-');
                var libraryName = separator > 0 ? stem[..separator] : stem;
                using var markerWriter = new StreamWriter(
                    archive.CreateEntry($"{stem}/{libraryName}.pdb").Open());
                markerWriter.Write(marker);
            }
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

    private static MemoryStream CreatePackageWithDuplicateSymbols(string archiveName, string dllName, string dllPath)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var dllEntry = archive.CreateEntry($"{Path.GetFileNameWithoutExtension(archiveName)}/{dllName}");
            using (var target = dllEntry.Open())
            using (var source = File.OpenRead(dllPath))
            {
                source.CopyTo(target);
            }

            var stem = Path.GetFileNameWithoutExtension(archiveName);
            var separator = stem.LastIndexOf('-');
            var libraryName = separator > 0 ? stem[..separator] : stem;
            archive.CreateEntry($"{stem}/{libraryName}.pdb");
            archive.CreateEntry($"{stem}/symbols/{libraryName}.pdb");
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreatePackageWithPublishOutputs(
        string archiveName,
        string dllName,
        string dllPath,
        string dependencyPath)
    {
        var stream = new MemoryStream();
        var stem = Path.GetFileNameWithoutExtension(archiveName);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddFile(archive, $"{stem}/{dllName}", dllPath);
            AddFile(archive, $"{stem}/dependencies/{Path.GetFileName(dependencyPath)}", dependencyPath);
            WriteEntry(archive, $"{stem}/{Path.GetFileNameWithoutExtension(dllName)}.deps.json", "{}");
            WriteEntry(archive, $"{stem}/{Path.GetFileNameWithoutExtension(dllName)}.runtimeconfig.json", "{\"runtimeOptions\":{}}" );
            WriteEntry(archive, $"{stem}/runtimes/win-x64/native/OpenCvSharpExtern.dll", "native-placeholder");
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreatePackageWithUnsupportedFile(
        string archiveName,
        string dllName,
        string dllPath)
    {
        var stream = new MemoryStream();
        var stem = Path.GetFileNameWithoutExtension(archiveName);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddFile(archive, $"{stem}/{dllName}", dllPath);
            WriteEntry(archive, $"{stem}/source.cs", "public sealed class UnexpectedSourceFile { }");
        }

        stream.Position = 0;
        return stream;
    }

    private static void AddFile(ZipArchive archive, string entryName, string sourcePath)
    {
        var entry = archive.CreateEntry(entryName);
        using var target = entry.Open();
        using var source = File.OpenRead(sourcePath);
        source.CopyTo(target);
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(entryName).Open());
        writer.Write(content);
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

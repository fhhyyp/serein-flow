using System.IO.Compression;
using SereinFlow.Application;
using SereinFlow.Library;
using SereinFlow.Infrastructure.Persistence;
using SereinFlow.TestLibrary;

namespace SereinFlow.Infrastructure.Tests;
public sealed class LibraryCatalogTests
{
    [Fact]
    public void StandaloneLibrarySdkOwnsPublicMetadataAndContextContracts()
    {
        var evaluatedValue = typeof(FlowLibraryAttribute).Assembly.GetName().Name;
        Assert.Equal("SereinFlow.Library", evaluatedValue);
        var evaluatedValue1 = typeof(IFlowContext).Assembly.GetName().Name;
        Assert.Equal("SereinFlow.Library", evaluatedValue1);
        var getExportedTypesResult = typeof(FlowLibraryAttribute).Assembly.GetExportedTypes();
        Assert.All(getExportedTypesResult, type =>
        {
            Assert.Equal("SereinFlow.Library", type.Namespace);
        });
        var getReferencedAssembliesResult = typeof(FlowLibraryAttribute).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(getReferencedAssembliesResult, reference => reference.Name is "SereinFlow.Domain" or "SereinFlow.Application" or "SereinFlow.Runtime.Abstractions");
    }

    [Fact]
    public async Task UploadPersistsSafePackageMetadataAndIsIdempotent()
    {
        var getTempPathResult = Path.GetTempPath();
        var evaluatedValue2 = $"sereinflow-library-{Guid.NewGuid():N}.db";
        var databasePath = Path.Combine(getTempPathResult, evaluatedValue2);
        var getTempPathResult3 = Path.GetTempPath();
        var evaluatedValue4 = $"sereinflow-library-{Guid.NewGuid():N}";
        var libraryRoot = Path.Combine(getTempPathResult3, evaluatedValue4);
        try
        {
            var sqliteDatabaseOptions = new SqliteDatabaseOptions(databasePath);
            using var database = new SqliteDatabase(sqliteDatabaseOptions);
            database.Initialize();
            var libraryCatalogOptions = new LibraryCatalogOptions(libraryRoot);
            var catalog = new SqliteLibraryCatalogService(database, libraryCatalogOptions);
            await using var package = CreatePackage("SereinFlow.TestLibrary-1.2.3.zip", "SereinFlow.TestLibrary.dll", typeof(生产线节点).Assembly.Location);
            var packageBytes = package.ToArray();
            package.Position = 0;
            var first = await catalog.UploadAsync(package, "SereinFlow.TestLibrary-1.2.3.zip");
            Assert.False(first.AlreadyExists);
            Assert.Equal("SereinFlow.TestLibrary", first.Library.Name);
            Assert.Equal("1.2.3", first.Library.Version);
            Assert.Equal(64, first.Library.Sha256.Length);
            var listResult = catalog.List();
            Assert.Single(listResult);
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
        var getTempPathResult8 = Path.GetTempPath();
        var evaluatedValue9 = $"sereinflow-library-{Guid.NewGuid():N}.db";
        var databasePath = Path.Combine(getTempPathResult8, evaluatedValue9);
        var getTempPathResult10 = Path.GetTempPath();
        var evaluatedValue11 = $"sereinflow-library-{Guid.NewGuid():N}";
        var libraryRoot = Path.Combine(getTempPathResult10, evaluatedValue11);
        try
        {
            var sqliteDatabaseOptions5 = new SqliteDatabaseOptions(databasePath);
            using var database = new SqliteDatabase(sqliteDatabaseOptions5);
            database.Initialize();
            var libraryCatalogOptions6 = new LibraryCatalogOptions(libraryRoot);
            var catalog = new SqliteLibraryCatalogService(database, libraryCatalogOptions6);
            await using var package = CreatePackageWithTraversal("UnsafeLibrary-1.0.0.zip");
            var error = await Assert.ThrowsAsync<LibraryUploadException>(() =>
            {
                var uploadAsyncResult = catalog.UploadAsync(package, "UnsafeLibrary-1.0.0.zip");
                return uploadAsyncResult;
            });
            Assert.Equal(422, error.StatusCode);
            var listResult7 = catalog.List();
            Assert.Empty(listResult7);
        }
        finally
        {
            TryDelete(databasePath);
            TryDeleteDirectory(libraryRoot);
        }
    }

    [Fact]
    public async Task VerifiedUploadImportsTheInspectedPackageAndRemainsIdempotent()
    {
        var getTempPathResult15 = Path.GetTempPath();
        var evaluatedValue16 = $"sereinflow-library-{Guid.NewGuid():N}.db";
        var databasePath = Path.Combine(getTempPathResult15, evaluatedValue16);
        var getTempPathResult17 = Path.GetTempPath();
        var evaluatedValue18 = $"sereinflow-library-{Guid.NewGuid():N}";
        var libraryRoot = Path.Combine(getTempPathResult17, evaluatedValue18);
        try
        {
            var sqliteDatabaseOptions12 = new SqliteDatabaseOptions(databasePath);
            using var database = new SqliteDatabase(sqliteDatabaseOptions12);
            database.Initialize();
            var libraryCatalogOptions13 = new LibraryCatalogOptions(libraryRoot);
            ILibraryCatalogService catalog = new SqliteLibraryCatalogService(database, libraryCatalogOptions13);
            const string fileName = "SereinFlow.TestLibrary-1.2.3.zip";
            await using var package = CreatePackage(fileName, "SereinFlow.TestLibrary.dll", typeof(生产线节点).Assembly.Location);
            var inspection = await catalog.InspectAsync(package, fileName);
            Assert.NotNull(inspection);
            var listAsyncResult = await catalog.ListAsync();
            Assert.Empty(listAsyncResult);
            package.Position = 0;
            var toUpperInvariantResult = inspection.Library.Sha256.ToUpperInvariant();
            var uploaded = await catalog.UploadVerifiedAsync(package, fileName, inspection.Library.SizeBytes, toUpperInvariantResult);
            package.Position = 0;
            var duplicate = await catalog.UploadVerifiedAsync(package, fileName, inspection.Library.SizeBytes, inspection.Library.Sha256);
            Assert.False(uploaded.AlreadyExists);
            Assert.True(duplicate.AlreadyExists);
            Assert.Equal(inspection.Library.Id, uploaded.Library.Id);
            Assert.Equal(uploaded.Library.Id, duplicate.Library.Id);
            var listAsyncResult14 = await catalog.ListAsync();
            Assert.Single(listAsyncResult14);
        }
        finally
        {
            TryDelete(databasePath);
            TryDeleteDirectory(libraryRoot);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task VerifiedUploadRejectsMismatchedHashOrSizeBeforePersistenceAndDeduplication(bool alreadyUploaded, bool mismatchedSize)
    {
        var getTempPathResult24 = Path.GetTempPath();
        var evaluatedValue25 = $"sereinflow-library-{Guid.NewGuid():N}.db";
        var databasePath = Path.Combine(getTempPathResult24, evaluatedValue25);
        var getTempPathResult26 = Path.GetTempPath();
        var evaluatedValue27 = $"sereinflow-library-{Guid.NewGuid():N}";
        var libraryRoot = Path.Combine(getTempPathResult26, evaluatedValue27);
        try
        {
            var sqliteDatabaseOptions20 = new SqliteDatabaseOptions(databasePath);
            using var database = new SqliteDatabase(sqliteDatabaseOptions20);
            database.Initialize();
            var libraryCatalogOptions21 = new LibraryCatalogOptions(libraryRoot);
            ILibraryCatalogService catalog = new SqliteLibraryCatalogService(database, libraryCatalogOptions21);
            const string fileName = "SereinFlow.TestLibrary-1.2.3.zip";
            await using var package = CreatePackage(fileName, "SereinFlow.TestLibrary.dll", typeof(生产线节点).Assembly.Location);
            var inspection = await catalog.InspectAsync(package, fileName);
            Assert.NotNull(inspection);
            package.Position = 0;
            if (alreadyUploaded)
            {
                await catalog.UploadAsync(package, fileName);
                package.Position = 0;
            }

            var error = await Assert.ThrowsAsync<LibraryUploadException>(() =>
            {
                string evaluatedValue19 = mismatchedSize ? inspection.Library.Sha256 : new string ('0', 64);
                var uploadVerifiedAsyncResult = catalog.UploadVerifiedAsync(package, fileName, inspection.Library.SizeBytes + (mismatchedSize ? 1 : 0), evaluatedValue19);
                return uploadVerifiedAsyncResult;
            });
            Assert.Equal(409, error.StatusCode);
            var evaluatedValue22 = (await catalog.ListAsync()).Count;
            Assert.Equal(alreadyUploaded ? 1 : 0, evaluatedValue22);
            var getFilesResult = Directory.GetFiles(libraryRoot, ".upload-*.tmp");
            Assert.Empty(getFilesResult);
            var combineResult = Path.Combine(libraryRoot, "packages");
            var evaluatedValue23 = Directory.GetFiles(combineResult, "*.zip").Length;
            Assert.Equal(alreadyUploaded ? 1 : 0, evaluatedValue23);
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
        var getTempPathResult33 = Path.GetTempPath();
        var evaluatedValue34 = $"sereinflow-library-{Guid.NewGuid():N}.db";
        var databasePath = Path.Combine(getTempPathResult33, evaluatedValue34);
        var getTempPathResult35 = Path.GetTempPath();
        var evaluatedValue36 = $"sereinflow-library-{Guid.NewGuid():N}";
        var libraryRoot = Path.Combine(getTempPathResult35, evaluatedValue36);
        try
        {
            var sqliteDatabaseOptions29 = new SqliteDatabaseOptions(databasePath);
            using var database = new SqliteDatabase(sqliteDatabaseOptions29);
            database.Initialize();
            var libraryCatalogOptions30 = new LibraryCatalogOptions(libraryRoot);
            var catalog = new SqliteLibraryCatalogService(database, libraryCatalogOptions30);
            await using var package = CreatePackage("RenamedLibrary-1.0.0.zip", "RenamedLibrary.dll", typeof(生产线节点).Assembly.Location);
            var error = await Assert.ThrowsAsync<LibraryUploadException>(() =>
            {
                var uploadAsyncResult28 = catalog.UploadAsync(package, "RenamedLibrary-1.0.0.zip");
                return uploadAsyncResult28;
            });
            Assert.Equal(422, error.StatusCode);
            Assert.Contains("does not match", error.Message, StringComparison.OrdinalIgnoreCase);
            var listResult31 = catalog.List();
            Assert.Empty(listResult31);
            var combineResult32 = Path.Combine(libraryRoot, "packages");
            var enumerateFilesResult = Directory.EnumerateFiles(combineResult32, "*.zip");
            Assert.Empty(enumerateFilesResult);
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
        var getTempPathResult43 = Path.GetTempPath();
        var evaluatedValue44 = $"sereinflow-library-{Guid.NewGuid():N}.db";
        var databasePath = Path.Combine(getTempPathResult43, evaluatedValue44);
        var getTempPathResult45 = Path.GetTempPath();
        var evaluatedValue46 = $"sereinflow-library-{Guid.NewGuid():N}";
        var libraryRoot = Path.Combine(getTempPathResult45, evaluatedValue46);
        try
        {
            var sqliteDatabaseOptions38 = new SqliteDatabaseOptions(databasePath);
            using var database = new SqliteDatabase(sqliteDatabaseOptions38);
            database.Initialize();
            var libraryCatalogOptions39 = new LibraryCatalogOptions(libraryRoot);
            var catalog = new SqliteLibraryCatalogService(database, libraryCatalogOptions39);
            await using var package = CreatePackageWithDuplicateSymbols("SereinFlow.TestLibrary-1.7.0.zip", "SereinFlow.TestLibrary.dll", typeof(生产线节点).Assembly.Location);
            var error = await Assert.ThrowsAsync<LibraryUploadException>(() =>
            {
                var uploadAsyncResult37 = catalog.UploadAsync(package, "SereinFlow.TestLibrary-1.7.0.zip");
                return uploadAsyncResult37;
            });
            Assert.Equal(422, error.StatusCode);
            Assert.Contains("more than one", error.Message, StringComparison.OrdinalIgnoreCase);
            var listResult40 = catalog.List();
            Assert.Empty(listResult40);
            var combineResult41 = Path.Combine(libraryRoot, "packages");
            var enumerateFilesResult42 = Directory.EnumerateFiles(combineResult41, "*.zip");
            Assert.Empty(enumerateFilesResult42);
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
        var getTempPathResult50 = Path.GetTempPath();
        var evaluatedValue51 = $"sereinflow-library-{Guid.NewGuid():N}.db";
        var databasePath = Path.Combine(getTempPathResult50, evaluatedValue51);
        var getTempPathResult52 = Path.GetTempPath();
        var evaluatedValue53 = $"sereinflow-library-{Guid.NewGuid():N}";
        var libraryRoot = Path.Combine(getTempPathResult52, evaluatedValue53);
        try
        {
            var sqliteDatabaseOptions48 = new SqliteDatabaseOptions(databasePath);
            using var database = new SqliteDatabase(sqliteDatabaseOptions48);
            database.Initialize();
            var libraryCatalogOptions49 = new LibraryCatalogOptions(libraryRoot);
            var catalog = new SqliteLibraryCatalogService(database, libraryCatalogOptions49);
            await using var package = CreatePackageWithPublishOutputs("SereinFlow.TestLibrary-1.8.0.zip", "SereinFlow.TestLibrary.dll", typeof(生产线节点).Assembly.Location, typeof(FlowLibraryAttribute).Assembly.Location);
            var uploaded = await catalog.UploadAsync(package, "SereinFlow.TestLibrary-1.8.0.zip");
            Assert.Equal("SereinFlow.TestLibrary", uploaded.Library.Name);
            var storedPackagePath = Path.Combine(libraryRoot, "packages", $"{uploaded.Library.Id}.zip");
            using var archive = ZipFile.OpenRead(storedPackagePath);
            var whereResult = archive.Entries.Where(entry =>
            {
                var evaluatedValue47 = !string.IsNullOrEmpty(entry.Name);
                return evaluatedValue47;
            });
            var selectResult = whereResult.Select(entry =>
            {
                var replaceResult = entry.FullName.Replace('\\', '/');
                return replaceResult;
            });
            var entries = selectResult.ToHashSet(StringComparer.OrdinalIgnoreCase);
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
        var getTempPathResult58 = Path.GetTempPath();
        var evaluatedValue59 = $"sereinflow-library-{Guid.NewGuid():N}.db";
        var databasePath = Path.Combine(getTempPathResult58, evaluatedValue59);
        var getTempPathResult60 = Path.GetTempPath();
        var evaluatedValue61 = $"sereinflow-library-{Guid.NewGuid():N}";
        var libraryRoot = Path.Combine(getTempPathResult60, evaluatedValue61);
        try
        {
            var sqliteDatabaseOptions55 = new SqliteDatabaseOptions(databasePath);
            using var database = new SqliteDatabase(sqliteDatabaseOptions55);
            database.Initialize();
            var libraryCatalogOptions56 = new LibraryCatalogOptions(libraryRoot);
            var catalog = new SqliteLibraryCatalogService(database, libraryCatalogOptions56);
            await using var package = CreatePackageWithUnsupportedFile("SereinFlow.TestLibrary-1.8.1.zip", "SereinFlow.TestLibrary.dll", typeof(生产线节点).Assembly.Location);
            var error = await Assert.ThrowsAsync<LibraryUploadException>(() =>
            {
                var uploadAsyncResult54 = catalog.UploadAsync(package, "SereinFlow.TestLibrary-1.8.1.zip");
                return uploadAsyncResult54;
            });
            Assert.Equal(422, error.StatusCode);
            Assert.Contains("source", error.Message, StringComparison.OrdinalIgnoreCase);
            var listResult57 = catalog.List();
            Assert.Empty(listResult57);
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
        var getTempPathResult85 = Path.GetTempPath();
        var evaluatedValue86 = $"sereinflow-library-{Guid.NewGuid():N}.db";
        var databasePath = Path.Combine(getTempPathResult85, evaluatedValue86);
        var getTempPathResult87 = Path.GetTempPath();
        var evaluatedValue88 = $"sereinflow-library-{Guid.NewGuid():N}";
        var libraryRoot = Path.Combine(getTempPathResult87, evaluatedValue88);
        try
        {
            var sqliteDatabaseOptions62 = new SqliteDatabaseOptions(databasePath);
            using var database = new SqliteDatabase(sqliteDatabaseOptions62);
            database.Initialize();
            var libraryCatalogOptions63 = new LibraryCatalogOptions(libraryRoot);
            var catalog = new SqliteLibraryCatalogService(database, libraryCatalogOptions63);
            await using var package = CreatePackage("SereinFlow.TestLibrary-1.1.0.zip", "SereinFlow.TestLibrary.dll", typeof(生产线节点).Assembly.Location);
            var result = await catalog.UploadAsync(package, "SereinFlow.TestLibrary-1.1.0.zip");
            //Assert.Equal(12, result.Library.Nodes.Count);
            var passRate = Assert.Single(result.Library.Nodes, node => node.MethodName == "计算合格率");
            Assert.Equal(SereinFlow.Contracts.NodeTypeDto.Action, passRate.Type);
            Assert.Equal("计算合格率", passRate.DisplayName);
            Assert.Equal("根据合格数量和检测总数计算本批次合格率。", passRate.Description);
            Assert.Equal("生产线设备与质量数据示例库", passRate.FlowLibraryName);
            var selectResult64 = passRate.Parameters.Select(parameter => parameter.Name);
            var toArrayResult = selectResult64.ToArray();
            Assert.Equal(["合格数量", "检测总数"], toArrayResult);
            Assert.Equal("生产线设备与质量数据示例库.计算合格率", passRate.ContractId);
            Assert.Equal(SereinFlow.Contracts.LibraryContractIdentityConfidenceDto.Explicit, passRate.IdentityConfidence);
            var selectResult65 = passRate.Parameters.Select(parameter => parameter.Id);
            var toArrayResult66 = selectResult65.ToArray();
            Assert.Equal(["合格数量", "检测总数"], toArrayResult66);
            Assert.Equal(["pass-count"], passRate.Parameters[0].Aliases);
            Assert.All(passRate.Parameters, parameter =>
            {
                Assert.Equal(SereinFlow.Contracts.LibraryContractIdentityConfidenceDto.Explicit, parameter.IdentityConfidence);
            });
            var manifest = Assert.IsType<SereinFlow.Contracts.LibraryArtifactManifestDto>(result.Library.CompatibilityManifest);
            var manifestNode = Assert.Single(manifest.Nodes, node => node.ContractId == "生产线设备与质量数据示例库.计算合格率");
            Assert.Equal(passRate.OverloadSignature, manifestNode.OverloadSignature);
            Assert.Equal("合格数量", manifestNode.Parameters[0].DisplayName);
            Assert.Equal("合格数量", manifestNode.Parameters[0].ClrName);
            Assert.Equal(["pass-count"], manifestNode.Parameters[0].Aliases);
            var writeInstruction = Assert.Single(result.Library.Nodes, node => node.MethodName == "构建设备写入指令");
            var skipResult = writeInstruction.Parameters.Skip(3);
            var selectResult67 = skipResult.Select(parameter => parameter.DefaultValue!);
            var toArrayResult68 = selectResult67.ToArray();
            Assert.Equal(["2", "毫米", "0.01"], toArrayResult68);
            var flipflop = Assert.Single(result.Library.Nodes, node => node.MethodName == "等待设备触发");
            Assert.Equal(SereinFlow.Contracts.NodeTypeDto.Flipflop, flipflop.Type);
            var contextBranch = Assert.Single(result.Library.Nodes, node => node.MethodName == "按设备状态选择分支");
            var selectResult69 = contextBranch.Parameters.Select(parameter => parameter.Name);
            var toArrayResult70 = selectResult69.ToArray();
            Assert.Equal(["设备状态"], toArrayResult70);
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
            var selectResult71 = modeMetadata.Options.Select(option => option.Name);
            var toArrayResult72 = selectResult71.ToArray();
            Assert.Equal(["自动", "手动", "维护"], toArrayResult72);
            var selectResult73 = modeMetadata.Options.Select(option => option.NumericValue);
            var toArrayResult74 = selectResult73.ToArray();
            Assert.Equal(["0", "1", "2"], toArrayResult74);
            Assert.Equal("自动", modeParameter.DefaultValue);
            var permissions = Assert.Single(result.Library.Nodes, node => node.MethodName == "配置设备操作权限");
            var permissionsParameter = Assert.Single(permissions.Parameters);
            Assert.NotNull(permissionsParameter.EnumMetadata);
            var permissionsMetadata = permissionsParameter.EnumMetadata!;
            Assert.True(permissionsMetadata.IsFlags);
            Assert.Equal("System.UInt64", permissionsMetadata.UnderlyingType);
            var selectResult75 = permissionsMetadata.Options.Select(option => option.NumericValue);
            var toArrayResult76 = selectResult75.ToArray();
            Assert.Equal(["0", "1", "2", "4", "7"], toArrayResult76);
            Assert.Equal("读取状态", permissionsParameter.DefaultValue);
            var combinedConfiguration = Assert.Single(result.Library.Nodes, node => node.MethodName == "生成设备配置摘要");
            var selectResult77 = combinedConfiguration.Parameters.Select(parameter => parameter.Name);
            var toArrayResult78 = selectResult77.ToArray();
            Assert.Equal(["运行模式", "操作权限"], toArrayResult78);
            Assert.All(combinedConfiguration.Parameters, parameter =>
            {
                Assert.NotNull(parameter.EnumMetadata);
            });
            var selectResult79 = combinedConfiguration.Parameters.Select(parameter => parameter.DefaultValue!);
            var toArrayResult80 = selectResult79.ToArray();
            Assert.Equal(["自动", "读取状态"], toArrayResult80);
            var listener = Assert.Single(result.Library.Nodes, node => node.MethodName == "监听设备配置变更");
            Assert.Equal(SereinFlow.Contracts.NodeTypeDto.Flipflop, listener.Type);
            Assert.True(listener.IsAwaitable);
            var selectResult81 = listener.Parameters.Select(parameter => parameter.Name);
            var toArrayResult82 = selectResult81.ToArray();
            Assert.Equal(["运行模式", "操作权限", "监听间隔毫秒"], toArrayResult82);
            Assert.NotNull(listener.Parameters[0].EnumMetadata);
            Assert.NotNull(listener.Parameters[1].EnumMetadata);
            var selectResult83 = listener.Parameters.Select(parameter => parameter.DefaultValue!);
            var toArrayResult84 = selectResult83.ToArray();
            Assert.Equal(["自动", "读取状态", "1000"], toArrayResult84);
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
        var getTempPathResult104 = Path.GetTempPath();
        var evaluatedValue105 = $"sereinflow-library-{Guid.NewGuid():N}.db";
        var databasePath = Path.Combine(getTempPathResult104, evaluatedValue105);
        var getTempPathResult106 = Path.GetTempPath();
        var evaluatedValue107 = $"sereinflow-library-{Guid.NewGuid():N}";
        var libraryRoot = Path.Combine(getTempPathResult106, evaluatedValue107);
        try
        {
            var sqliteDatabaseOptions95 = new SqliteDatabaseOptions(databasePath);
            using var database = new SqliteDatabase(sqliteDatabaseOptions95);
            database.Initialize();
            var libraryCatalogOptions96 = new LibraryCatalogOptions(libraryRoot);
            var catalog = new SqliteLibraryCatalogService(database, libraryCatalogOptions96);
            await using var sourcePackage = CreatePackage("SereinFlow.TestLibrary-1.5.2.zip", "SereinFlow.TestLibrary.dll", typeof(生产线节点).Assembly.Location, marker: "source-artifact");
            await using var targetPackage = CreatePackage("SereinFlow.TestLibrary-1.5.3.zip", "SereinFlow.TestLibrary.dll", typeof(生产线节点).Assembly.Location, marker: "target-artifact");
            var source = await catalog.UploadAsync(sourcePackage, "SereinFlow.TestLibrary-1.5.2.zip");
            var target = await catalog.UploadAsync(targetPackage, "SereinFlow.TestLibrary-1.5.3.zip");
            Assert.NotEqual(source.Library.Id, target.Library.Id);
            var orderByResult = source.Library.Nodes.OrderBy(node => node.MethodName, StringComparer.Ordinal);
            var selectResult97 = orderByResult.Select(node => node.ContractId);
            var orderByResult98 = target.Library.Nodes.OrderBy(node => node.MethodName, StringComparer.Ordinal);
            var selectResult99 = orderByResult98.Select(node => node.ContractId);
            Assert.Equal(selectResult97, selectResult99);
            var orderByResult100 = source.Library.Nodes.OrderBy(node => node.MethodName, StringComparer.Ordinal);
            var selectResult101 = orderByResult100.Select(node =>
            {
                var evaluatedValue90 = $"{node.ContractId}:{resolveJoinResult()}";
                return evaluatedValue90;
                string resolveJoinResult()
                {
                    var selectResult89 = node.Parameters.Select(parameter => parameter.Id);
                    var joinResult = string.Join(",", selectResult89);
                    return joinResult;
                }
            });
            var orderByResult102 = target.Library.Nodes.OrderBy(node => node.MethodName, StringComparer.Ordinal);
            var selectResult103 = orderByResult102.Select(node =>
            {
                var evaluatedValue94 = $"{node.ContractId}:{resolveJoinResult91()}";
                return evaluatedValue94;
                string resolveJoinResult91()
                {
                    var selectResult92 = node.Parameters.Select(parameter => parameter.Id);
                    var joinResult93 = string.Join(",", selectResult92);
                    return joinResult93;
                }
            });
            Assert.Equal(selectResult101, selectResult103);
            Assert.All(source.Library.Nodes, node =>
            {
                Assert.Equal(SereinFlow.Contracts.LibraryContractIdentityConfidenceDto.Explicit, node.IdentityConfidence);
            });
            Assert.All(target.Library.Nodes, node =>
            {
                Assert.Equal(SereinFlow.Contracts.LibraryContractIdentityConfidenceDto.Explicit, node.IdentityConfidence);
            });
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
        var getTempPathResult111 = Path.GetTempPath();
        var evaluatedValue112 = $"sereinflow-library-{Guid.NewGuid():N}.db";
        var databasePath = Path.Combine(getTempPathResult111, evaluatedValue112);
        var getTempPathResult113 = Path.GetTempPath();
        var evaluatedValue114 = $"sereinflow-library-{Guid.NewGuid():N}";
        var libraryRoot = Path.Combine(getTempPathResult113, evaluatedValue114);
        try
        {
            var sqliteDatabaseOptions108 = new SqliteDatabaseOptions(databasePath);
            using var database = new SqliteDatabase(sqliteDatabaseOptions108);
            database.Initialize();
            var libraryCatalogOptions109 = new LibraryCatalogOptions(libraryRoot);
            var catalog = new SqliteLibraryCatalogService(database, libraryCatalogOptions109);
            await using var sourcePackage = CreatePackage("SereinFlow.TestLibrary-1.6.0.zip", "SereinFlow.TestLibrary.dll", typeof(生产线节点).Assembly.Location, marker: "baseline");
            var source = await catalog.UploadAsync(sourcePackage, "SereinFlow.TestLibrary-1.6.0.zip");
            var assignLibraryFamilyRequestDto = new SereinFlow.Contracts.AssignLibraryFamilyRequestDto(null, "生产线兼容性测试");
            var family = await catalog.AssignFamilyAsync(source.Library.Id, assignLibraryFamilyRequestDto);
            await using var targetPackage = CreatePackage("SereinFlow.TestLibrary-1.6.1.zip", "SereinFlow.TestLibrary.dll", typeof(生产线节点).Assembly.Location, marker: "candidate");
            var inspection = await catalog.InspectAsync(targetPackage, "SereinFlow.TestLibrary-1.6.1.zip", familyId: family!.Id);
            Assert.NotNull(inspection);
            Assert.False(inspection!.AlreadyExists);
            Assert.NotNull(inspection.Compatibility);
            Assert.True(inspection.Compatibility!.IsCompatible);
            Assert.Contains(inspection.Compatibility.Issues, issue => issue.Classification == SereinFlow.Contracts.LibraryCompatibilityClassificationDto.Exact);
            var findAsyncResult = await catalog.FindAsync(inspection.Library.Id);
            Assert.Null(findAsyncResult);
            var enumerateFilesResult110 = Directory.EnumerateFiles(libraryRoot, ".inspect-*", SearchOption.TopDirectoryOnly);
            Assert.Empty(enumerateFilesResult110);
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
        var getTempPathResult120 = Path.GetTempPath();
        var evaluatedValue121 = $"sereinflow-library-{Guid.NewGuid():N}.db";
        var databasePath = Path.Combine(getTempPathResult120, evaluatedValue121);
        var getTempPathResult122 = Path.GetTempPath();
        var evaluatedValue123 = $"sereinflow-library-{Guid.NewGuid():N}";
        var libraryRoot = Path.Combine(getTempPathResult122, evaluatedValue123);
        try
        {
            var sqliteDatabaseOptions115 = new SqliteDatabaseOptions(databasePath);
            using var database = new SqliteDatabase(sqliteDatabaseOptions115);
            database.Initialize();
            var libraryCatalogOptions116 = new LibraryCatalogOptions(libraryRoot);
            var catalog = new SqliteLibraryCatalogService(database, libraryCatalogOptions116);
            await using var package = CreatePackage("SereinFlow.TestLibrary-1.1.0.zip", "SereinFlow.TestLibrary.dll", typeof(生产线节点).Assembly.Location);
            var uploaded = await catalog.UploadAsync(package, "SereinFlow.TestLibrary-1.1.0.zip");
            var packagePath = Path.Combine(libraryRoot, "packages", $"{uploaded.Library.Id}.zip");
            var archiveAsyncResult = await catalog.ArchiveAsync(uploaded.Library.Id);
            Assert.True(archiveAsyncResult);
            var listAsyncResult117 = await catalog.ListAsync();
            Assert.Empty(listAsyncResult117);
            var listAsyncResult118 = await catalog.ListAsync(includeArchived: true);
            var archived = Assert.Single(listAsyncResult118);
            Assert.Equal(SereinFlow.Contracts.LibraryLifecycleDto.Archived, archived.Lifecycle);
            var evaluatedValue119 = (await catalog.FindAsync(uploaded.Library.Id))!.Id;
            Assert.Equal(uploaded.Library.Id, evaluatedValue119);
            var existsResult = File.Exists(packagePath);
            Assert.True(existsResult);
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
        var getTempPathResult127 = Path.GetTempPath();
        var evaluatedValue128 = $"sereinflow-library-{Guid.NewGuid():N}.db";
        var databasePath = Path.Combine(getTempPathResult127, evaluatedValue128);
        var getTempPathResult129 = Path.GetTempPath();
        var evaluatedValue130 = $"sereinflow-library-{Guid.NewGuid():N}";
        var libraryRoot = Path.Combine(getTempPathResult129, evaluatedValue130);
        try
        {
            var sqliteDatabaseOptions124 = new SqliteDatabaseOptions(databasePath);
            using var database = new SqliteDatabase(sqliteDatabaseOptions124);
            database.Initialize();
            var libraryCatalogOptions125 = new LibraryCatalogOptions(libraryRoot);
            var catalog = new SqliteLibraryCatalogService(database, libraryCatalogOptions125);
            await using var package = CreatePackage("SereinFlow.TestLibrary-1.3.0.zip", "SereinFlow.TestLibrary.dll", typeof(生产线节点).Assembly.Location);
            var uploaded = await catalog.UploadAsync(package, "SereinFlow.TestLibrary-1.3.0.zip");
            var sugarParameter = new SqlSugar.SugarParameter("@id", uploaded.Library.Id);
            database.Client.Ado.ExecuteCommand("UPDATE Libraries SET CatalogSchemaVersion = 0, NodeCatalogJson = '[]' WHERE Id = @id", sugarParameter);
            var reindexOutdatedAsyncResult = await catalog.ReindexOutdatedAsync();
            Assert.Equal(1, reindexOutdatedAsyncResult);
            var reindexed = await catalog.FindAsync(uploaded.Library.Id);
            var mode = Assert.Single(reindexed!.Nodes, node => node.MethodName == "设置设备运行模式");
            var evaluatedValue126 = Assert.Single(mode.Parameters).EnumMetadata;
            Assert.NotNull(evaluatedValue126);
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
        var getTempPathResult136 = Path.GetTempPath();
        var evaluatedValue137 = $"sereinflow-library-{Guid.NewGuid():N}.db";
        var databasePath = Path.Combine(getTempPathResult136, evaluatedValue137);
        var getTempPathResult138 = Path.GetTempPath();
        var evaluatedValue139 = $"sereinflow-library-{Guid.NewGuid():N}";
        var libraryRoot = Path.Combine(getTempPathResult138, evaluatedValue139);
        try
        {
            var sqliteDatabaseOptions131 = new SqliteDatabaseOptions(databasePath);
            using var database = new SqliteDatabase(sqliteDatabaseOptions131);
            database.Initialize();
            var libraryCatalogOptions132 = new LibraryCatalogOptions(libraryRoot);
            var catalog = new SqliteLibraryCatalogService(database, libraryCatalogOptions132);
            await using var damagedPackage = CreatePackage("SereinFlow.TestLibrary-1.3.0.zip", "SereinFlow.TestLibrary.dll", typeof(生产线节点).Assembly.Location);
            await using var validPackage = CreatePackage("SereinFlow.TestLibrary-1.3.1.zip", "SereinFlow.TestLibrary.dll", typeof(生产线节点).Assembly.Location, marker: "second-package");
            var damaged = await catalog.UploadAsync(damagedPackage, "SereinFlow.TestLibrary-1.3.0.zip");
            var valid = await catalog.UploadAsync(validPackage, "SereinFlow.TestLibrary-1.3.1.zip");
            var combineResult133 = Path.Combine(libraryRoot, "packages", $"{damaged.Library.Id}.zip");
            File.WriteAllBytes(combineResult133, [0x00]);
            database.Client.Ado.ExecuteCommand("UPDATE Libraries SET CatalogSchemaVersion = 0, NodeCatalogJson = '[]'");
            var reindexOutdatedAsyncResult134 = await catalog.ReindexOutdatedAsync();
            Assert.Equal(1, reindexOutdatedAsyncResult134);
            var reindexed = await catalog.FindAsync(valid.Library.Id);
            var mode = Assert.Single(reindexed!.Nodes, node => node.MethodName == "设置设备运行模式");
            var evaluatedValue135 = Assert.Single(mode.Parameters).EnumMetadata;
            Assert.NotNull(evaluatedValue135);
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
        var getTempPathResult146 = Path.GetTempPath();
        var evaluatedValue147 = $"sereinflow-library-{Guid.NewGuid():N}.db";
        var databasePath = Path.Combine(getTempPathResult146, evaluatedValue147);
        var getTempPathResult148 = Path.GetTempPath();
        var evaluatedValue149 = $"sereinflow-library-{Guid.NewGuid():N}";
        var libraryRoot = Path.Combine(getTempPathResult148, evaluatedValue149);
        try
        {
            var sqliteDatabaseOptions140 = new SqliteDatabaseOptions(databasePath);
            using var database = new SqliteDatabase(sqliteDatabaseOptions140);
            database.Initialize();
            var libraryCatalogOptions141 = new LibraryCatalogOptions(libraryRoot);
            var catalog = new SqliteLibraryCatalogService(database, libraryCatalogOptions141);
            await using var firstPackage = CreatePackage("SereinFlow.TestLibrary-1.0.0.zip", "SereinFlow.TestLibrary.dll", typeof(生产线节点).Assembly.Location);
            await using var secondPackage = CreatePackage("SereinFlow.TestLibrary-2.0.0.zip", "SereinFlow.TestLibrary.dll", typeof(生产线节点).Assembly.Location, marker: "second-artifact");
            var first = await catalog.UploadAsync(firstPackage, "SereinFlow.TestLibrary-1.0.0.zip");
            var second = await catalog.UploadAsync(secondPackage, "SereinFlow.TestLibrary-2.0.0.zip");
            var assignLibraryFamilyRequestDto142 = new SereinFlow.Contracts.AssignLibraryFamilyRequestDto(null, "生产线类库");
            var sourceFamily = await catalog.AssignFamilyAsync(first.Library.Id, assignLibraryFamilyRequestDto142);
            Assert.NotNull(sourceFamily);
            var assignLibraryFamilyRequestDto143 = new SereinFlow.Contracts.AssignLibraryFamilyRequestDto(sourceFamily!.Id, null);
            sourceFamily = await catalog.AssignFamilyAsync(second.Library.Id, assignLibraryFamilyRequestDto143);
            Assert.Equal(second.Library.Id, sourceFamily!.LatestArtifactId);
            var assignLibraryFamilyRequestDto144 = new SereinFlow.Contracts.AssignLibraryFamilyRequestDto(null, "质检类库");
            var targetFamily = await catalog.AssignFamilyAsync(second.Library.Id, assignLibraryFamilyRequestDto144);
            Assert.NotNull(targetFamily);
            Assert.Equal(second.Library.Id, targetFamily!.LatestArtifactId);
            var evaluatedValue145 = (await catalog.FindAsync(second.Library.Id))!.FamilyName;
            Assert.Equal("质检类库", evaluatedValue145);
            var families = await catalog.ListFamiliesAsync();
            var refreshedSource = Assert.Single(families, family => family.Id == sourceFamily.Id);
            Assert.Equal(first.Library.Id, refreshedSource.LatestArtifactId);
            Assert.Single(refreshedSource.Artifacts!);
            var setLifecycleAsyncResult = await catalog.SetLifecycleAsync(second.Library.Id, SereinFlow.Contracts.LibraryLifecycleDto.Archived);
            Assert.True(setLifecycleAsyncResult);
            var listFamiliesAsyncResult = await catalog.ListFamiliesAsync();
            var archivedTarget = Assert.Single(listFamiliesAsyncResult, family => family.Id == targetFamily.Id);
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
            var evaluatedValue150 = $"{Path.GetFileNameWithoutExtension(archiveName)}/{dllName}";
            var entry = archive.CreateEntry(evaluatedValue150);
            using (var target = entry.Open())
            {
                using (var source = File.OpenRead(dllPath))
                {
                    source.CopyTo(target);
                }
            }

            if (!string.IsNullOrWhiteSpace(marker))
            {
                var stem = Path.GetFileNameWithoutExtension(archiveName);
                var separator = stem.LastIndexOf('-');
                var libraryName = separator > 0 ? stem[..separator] : stem;
                var createEntryResult = archive.CreateEntry($"{stem}/{libraryName}.pdb");
                var openResult = createEntryResult.Open();
                using var markerWriter = new StreamWriter(openResult);
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
            var evaluatedValue151 = $"{Path.GetFileNameWithoutExtension(archiveName)}/{dllName}";
            var dllEntry = archive.CreateEntry(evaluatedValue151);
            using (var target = dllEntry.Open())
            {
                using (var source = File.OpenRead(dllPath))
                {
                    source.CopyTo(target);
                }
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

    private static MemoryStream CreatePackageWithPublishOutputs(string archiveName, string dllName, string dllPath, string dependencyPath)
    {
        var stream = new MemoryStream();
        var stem = Path.GetFileNameWithoutExtension(archiveName);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddFile(archive, $"{stem}/{dllName}", dllPath);
            var evaluatedValue152 = $"{stem}/dependencies/{Path.GetFileName(dependencyPath)}";
            AddFile(archive, evaluatedValue152, dependencyPath);
            var evaluatedValue153 = $"{stem}/{Path.GetFileNameWithoutExtension(dllName)}.deps.json";
            WriteEntry(archive, evaluatedValue153, "{}");
            var evaluatedValue154 = $"{stem}/{Path.GetFileNameWithoutExtension(dllName)}.runtimeconfig.json";
            WriteEntry(archive, evaluatedValue154, "{\"runtimeOptions\":{}}");
            WriteEntry(archive, $"{stem}/runtimes/win-x64/native/OpenCvSharpExtern.dll", "native-placeholder");
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreatePackageWithUnsupportedFile(string archiveName, string dllName, string dllPath)
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
        var createEntryResult155 = archive.CreateEntry(entryName);
        var openResult156 = createEntryResult155.Open();
        using var writer = new StreamWriter(openResult156);
        writer.Write(content);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
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

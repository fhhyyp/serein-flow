using SereinFlow.Application.Persistence;
using SereinFlow.Contracts;

namespace SereinFlow.Application.Tests;

public sealed class LibraryNodeTemplateServiceTests
{
    [Fact]
    public async Task CreatesActionAndFlipflopTemplatesFromAnAttachedContract()
    {
        var projectId = Guid.NewGuid();
        var library = CreateLibrary();
        var service = new LibraryNodeTemplateService(
            new ReferenceRepository((projectId, library.Id)),
            new CatalogService(library));

        var action = await service.CreateAsync(new LibraryNodeTemplateRequestDto(
            projectId,
            library.Id,
            "quality-rate",
            new NodeTemplatePositionDto(320, 180)));
        var flipflop = await service.CreateAsync(new LibraryNodeTemplateRequestDto(
            projectId,
            library.Id,
            "wait-for-device",
            new NodeTemplatePositionDto(480, 180)));

        Assert.Equal("libraryContract", action.TemplateSource);
        Assert.Equal(library.Sha256, action.LibrarySha256);
        Assert.Equal(LibraryNodeTemplateService.GetContractRevision(library.Nodes[0]), action.ContractRevision);
        Assert.Equal(NodeTypeDto.Action, action.Node.Type);
        Assert.StartsWith("node-", action.Node.Id);
        Assert.Equal(320, action.Node.X);
        Assert.Equal(180, action.Node.Y);
        Assert.Contains(action.Node.Ports, port => port.Id == "exec-in");
        Assert.Contains(action.Node.Ports, port => port.Id == "exec-success");
        Assert.Contains(action.Node.Ports, port => port.Id == "exec-failure");
        Assert.Contains(action.Node.Ports, port => port.Id == "exec-error");
        Assert.Contains(action.Node.Ports, port => port.Id == "data-out");
        Assert.Contains(action.Node.Ports, port => port.Id == "param-pass-count");
        var actionParameter = action.Node.Parameters.Single();
        Assert.Equal("passCount", actionParameter.Name);
        Assert.Equal("pass-count", actionParameter.Ui!.Id);
        Assert.Equal("1", actionParameter.ValueJson);
        Assert.Equal("quality-rate", action.Node.Ui!.LibraryNodeContractId);
        Assert.Equal("Template library", action.Node.Ui.FlowLibraryName);

        Assert.Equal(NodeTypeDto.Flipflop, flipflop.Node.Type);
        Assert.True(flipflop.Node.Ui!.IsAwaitable);
        Assert.Contains(flipflop.Node.Ports, port => port.Id == "data-out");
        var flipflopParameter = flipflop.Node.Parameters.Single();
        Assert.True(flipflopParameter.Ui!.IsVariadic);
        Assert.Equal("values", flipflopParameter.Ui.VariadicGroupId);
        Assert.NotNull(flipflopParameter.Ui.EnumMetadata);

        var definition = new FlowDefinitionDto(
            Guid.NewGuid(),
            5,
            1,
            [new CanvasDto("main", CanvasLifecycleDto.Main, [], [])],
            string.Empty,
            "initial");
        var patched = new FlowPatchService().Apply(
            definition,
            [new FlowPatchCanonicalOperationDto("addNode", CanvasId: "main", Node: action.Node)]);
        Assert.Contains(patched.Canvases.Single().Nodes, node => node.Id == action.Node.Id);
    }

    [Fact]
    public async Task RejectsUnattachedOrUnknownLibraryContracts()
    {
        var projectId = Guid.NewGuid();
        var library = CreateLibrary();
        var unattached = new LibraryNodeTemplateService(
            new ReferenceRepository(),
            new CatalogService(library));

        var missingReference = await Assert.ThrowsAsync<LibraryNodeTemplateException>(() => unattached.CreateAsync(
            new LibraryNodeTemplateRequestDto(projectId, library.Id, "quality-rate", new NodeTemplatePositionDto(0, 0))));
        Assert.Equal("mcp.library_node_template.library_not_attached", missingReference.Code);

        var attached = new LibraryNodeTemplateService(
            new ReferenceRepository((projectId, library.Id)),
            new CatalogService(library));
        var missingContract = await Assert.ThrowsAsync<LibraryNodeTemplateException>(() => attached.CreateAsync(
            new LibraryNodeTemplateRequestDto(projectId, library.Id, "missing", new NodeTemplatePositionDto(0, 0))));
        Assert.Equal("mcp.library_node_template.contract_not_found", missingContract.Code);
    }

    private static LibraryDto CreateLibrary()
        => new(
            "template-library",
            "Template library",
            "1.2.3",
            "Template.Library-1.2.3.zip",
            1024,
            "package-sha256",
            DateTimeOffset.UtcNow,
            [
                new LibraryNodeDto(
                    "template-library:quality-rate",
                    NodeTypeDto.Action,
                    "Calculate quality rate",
                    "Calculates the pass rate for a production batch.",
                    "template-library",
                    "Template.Library.QualityNodes",
                    "CalculateQualityRate",
                    "Template.Library.dll",
                    "1.2.3",
                    "System.Decimal",
                    [
                        new LibraryParameterDto(
                            "pass-count",
                            "passCount",
                            "System.Int32",
                            "Accepted items.",
                            true,
                            DefaultValue: "1")
                    ],
                    ContractId: "quality-rate",
                    FlowLibraryName: "Template library"),
                new LibraryNodeDto(
                    "template-library:wait-for-device",
                    NodeTypeDto.Flipflop,
                    "Wait for device",
                    "Waits asynchronously for a device signal.",
                    "template-library",
                    "Template.Library.QualityNodes",
                    "WaitForDevice",
                    "Template.Library.dll",
                    "1.2.3",
                    "System.Threading.Tasks.Task<System.Boolean>",
                    [
                        new LibraryParameterDto(
                            "values",
                            "values",
                            "System.Int32[]",
                            "Observed values.",
                            false,
                            IsVariadic: true,
                            VariadicGroupId: "values",
                            ElementType: "System.Int32",
                            EnumMetadata: new EnumParameterMetadataDto(
                                "Template.Library.DeviceMode",
                                false,
                                "System.Int32",
                                [new EnumValueOptionDto("Auto", "0")]))
                    ],
                    IsAwaitable: true,
                    ContractId: "wait-for-device",
                    FlowLibraryName: "Template library")
            ]);

    private sealed class ReferenceRepository(params (Guid ProjectId, string LibraryId)[] references) : IProjectLibraryReferenceRepository
    {
        private readonly List<ProjectLibraryReference> _references = references
            .Select(static item => new ProjectLibraryReference(item.ProjectId, item.LibraryId, DateTimeOffset.UtcNow))
            .ToList();

        public Task<IReadOnlyList<ProjectLibraryReference>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectLibraryReference>>(_references.Where(item => item.ProjectId == projectId).ToArray());

        public Task<bool> IsReferencedAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
            => Task.FromResult(_references.Any(item => item.ProjectId == projectId && string.Equals(item.LibraryId, libraryId, StringComparison.OrdinalIgnoreCase)));

        public Task<ProjectLibraryReference> AddAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
        {
            var reference = new ProjectLibraryReference(projectId, libraryId, DateTimeOffset.UtcNow);
            _references.Add(reference);
            return Task.FromResult(reference);
        }

        public Task<bool> RemoveAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
            => Task.FromResult(_references.RemoveAll(item => item.ProjectId == projectId && string.Equals(item.LibraryId, libraryId, StringComparison.OrdinalIgnoreCase)) > 0);
    }

    private sealed class CatalogService(LibraryDto library) : ILibraryCatalogService
    {
        public IReadOnlyList<LibraryDto> List() => [library];

        public LibraryDto? Find(string libraryId)
            => string.Equals(library.Id, libraryId, StringComparison.OrdinalIgnoreCase) ? library : null;

        public Task<IReadOnlyList<LibraryDto>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LibraryDto>>(includeArchived || library.Lifecycle == LibraryLifecycleDto.Available ? [library] : []);

        public Task<LibraryDto?> FindAsync(string libraryId, CancellationToken cancellationToken = default)
            => Task.FromResult(Find(libraryId));

        public Task<LibraryUploadResultDto> UploadAsync(Stream package, string fileName, long? declaredLength = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public bool Delete(string libraryId) => false;

        public Task<bool> ArchiveAsync(string libraryId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<LibraryDto?> ReindexAsync(string libraryId, CancellationToken cancellationToken = default)
            => Task.FromResult(Find(libraryId));

        public Task<int> ReindexOutdatedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}

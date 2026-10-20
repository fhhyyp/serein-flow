using System.Collections.Generic;
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
        var referenceRepository = new ReferenceRepository((projectId, library.Id));
        var catalogService = new CatalogService(library);
        var service = new LibraryNodeTemplateService(referenceRepository, catalogService);
        var nodeTemplatePositionDto = new NodeTemplatePositionDto(320, 180);
        var libraryNodeTemplateRequestDto = new LibraryNodeTemplateRequestDto(projectId, library.Id, "quality-rate", nodeTemplatePositionDto);
        var action = await service.CreateAsync(libraryNodeTemplateRequestDto);
        var nodeTemplatePositionDto1 = new NodeTemplatePositionDto(480, 180);
        var libraryNodeTemplateRequestDto2 = new LibraryNodeTemplateRequestDto(projectId, library.Id, "wait-for-device", nodeTemplatePositionDto1);
        var flipflop = await service.CreateAsync(libraryNodeTemplateRequestDto2);
        Assert.Equal("libraryContract", action.TemplateSource);
        Assert.Equal(library.Sha256, action.LibrarySha256);
        var getContractRevisionResult = LibraryNodeTemplateService.GetContractRevision(library.Nodes[0]);
        Assert.Equal(getContractRevisionResult, action.ContractRevision);
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
        var newGuidResult = Guid.NewGuid();
        IReadOnlyList<CanvasDto> evaluatedValue = [new CanvasDto("main", CanvasLifecycleDto.Main, [], [])];
        var definition = new FlowDefinitionDto(newGuidResult, 5, 1, evaluatedValue, string.Empty, "initial");
        var flowPatchService = new FlowPatchService();
        IReadOnlyList<FlowPatchCanonicalOperationDto> evaluatedValue3 = [new FlowPatchCanonicalOperationDto("addNode", CanvasId: "main", Node: action.Node)];
        var patched = flowPatchService.Apply(definition, evaluatedValue3);
        var evaluatedValue4 = patched.Canvases.Single().Nodes;
        Assert.Contains(evaluatedValue4, node => node.Id == action.Node.Id);
    }

    [Fact]
    public async Task RejectsUnattachedOrUnknownLibraryContracts()
    {
        var projectId = Guid.NewGuid();
        var library = CreateLibrary();
        var referenceRepository10 = new ReferenceRepository();
        var catalogService11 = new CatalogService(library);
        var unattached = new LibraryNodeTemplateService(referenceRepository10, catalogService11);
        var missingReference = await Assert.ThrowsAsync<LibraryNodeTemplateException>(() =>
        {
            var nodeTemplatePositionDto5 = new NodeTemplatePositionDto(0, 0);
            var libraryNodeTemplateRequestDto6 = new LibraryNodeTemplateRequestDto(projectId, library.Id, "quality-rate", nodeTemplatePositionDto5);
            var createAsyncResult = unattached.CreateAsync(libraryNodeTemplateRequestDto6);
            return createAsyncResult;
        });
        Assert.Equal(McpErrorCodes.LibraryNodeTemplateLibraryNotAttached, missingReference.Code);
        var referenceRepository12 = new ReferenceRepository((projectId, library.Id));
        var catalogService13 = new CatalogService(library);
        var attached = new LibraryNodeTemplateService(referenceRepository12, catalogService13);
        var missingContract = await Assert.ThrowsAsync<LibraryNodeTemplateException>(() =>
        {
            var nodeTemplatePositionDto7 = new NodeTemplatePositionDto(0, 0);
            var libraryNodeTemplateRequestDto8 = new LibraryNodeTemplateRequestDto(projectId, library.Id, "missing", nodeTemplatePositionDto7);
            var createAsyncResult9 = attached.CreateAsync(libraryNodeTemplateRequestDto8);
            return createAsyncResult9;
        });
        Assert.Equal(McpErrorCodes.LibraryNodeTemplateContractNotFound, missingContract.Code);
    }

    private static LibraryDto CreateLibrary()
    {
        IReadOnlyList<LibraryNodeDto> evaluatedValue19 = [resolveLibraryNodeDto(), resolveLibraryNodeDto15()];
        LibraryDto evaluatedValue20 = new("template-library", "Template library", "1.2.3", "Template.Library-1.2.3.zip", 1024, "package-sha256", DateTimeOffset.UtcNow, evaluatedValue19);
        return evaluatedValue20;
        LibraryNodeDto resolveLibraryNodeDto()
        {
            IReadOnlyList<LibraryParameterDto> evaluatedValue14 = [new LibraryParameterDto("pass-count", "passCount", "System.Int32", "Accepted items.", true, DefaultValue: "1")];
            var libraryNodeDto = new LibraryNodeDto("template-library:quality-rate", NodeTypeDto.Action, "Calculate quality rate", "Calculates the pass rate for a production batch.", "template-library", "Template.Library.QualityNodes", "CalculateQualityRate", "Template.Library.dll", "1.2.3", "System.Decimal", evaluatedValue14, ContractId: "quality-rate", FlowLibraryName: "Template library");
            return libraryNodeDto;
        }

        LibraryNodeDto resolveLibraryNodeDto15()
        {
            IReadOnlyList<LibraryParameterDto> evaluatedValue17 = [resolveLibraryParameterDto()];
            var libraryNodeDto18 = new LibraryNodeDto("template-library:wait-for-device", NodeTypeDto.Flipflop, "Wait for device", "Waits asynchronously for a device signal.", "template-library", "Template.Library.QualityNodes", "WaitForDevice", "Template.Library.dll", "1.2.3", "System.Threading.Tasks.Task<System.Boolean>", evaluatedValue17, IsAwaitable: true, ContractId: "wait-for-device", FlowLibraryName: "Template library");
            return libraryNodeDto18;
            LibraryParameterDto resolveLibraryParameterDto()
            {
                IReadOnlyList<EnumValueOptionDto> evaluatedValue16 = [new EnumValueOptionDto("Auto", "0")];
                var enumParameterMetadataDto = new EnumParameterMetadataDto("Template.Library.DeviceMode", false, "System.Int32", evaluatedValue16);
                var libraryParameterDto = new LibraryParameterDto("values", "values", "System.Int32[]", "Observed values.", false, IsVariadic: true, VariadicGroupId: "values", ElementType: "System.Int32", EnumMetadata: enumParameterMetadataDto);
                return libraryParameterDto;
            }
        }
    }

    private sealed class ReferenceRepository(params (Guid ProjectId, string LibraryId)[] references) : IProjectLibraryReferenceRepository
    {
        private readonly List<ProjectLibraryReference> _references = createreferencesInitialValue(references);
        public Task<IReadOnlyList<ProjectLibraryReference>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var whereResult = _references.Where(item => item.ProjectId == projectId);
            var toArrayResult = whereResult.ToArray();
            var fromResultResult = Task.FromResult<IReadOnlyList<ProjectLibraryReference>>(toArrayResult);
            return fromResultResult;
        }

        public Task<bool> IsReferencedAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
        {
            var anyResult = _references.Any(item =>
            {
                var evaluatedValue21 = item.ProjectId == projectId && string.Equals(item.LibraryId, libraryId, StringComparison.OrdinalIgnoreCase);
                return evaluatedValue21;
            });
            var fromResultResult22 = Task.FromResult(anyResult);
            return fromResultResult22;
        }

        public Task<ProjectLibraryReference> AddAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
        {
            var reference = new ProjectLibraryReference(projectId, libraryId, DateTimeOffset.UtcNow);
            _references.Add(reference);
            var fromResultResult23 = Task.FromResult(reference);
            return fromResultResult23;
        }

        public Task<bool> RemoveAsync(Guid projectId, string libraryId, CancellationToken cancellationToken = default)
        {
            var evaluatedValue25 = _references.RemoveAll(item =>
            {
                var evaluatedValue24 = item.ProjectId == projectId && string.Equals(item.LibraryId, libraryId, StringComparison.OrdinalIgnoreCase);
                return evaluatedValue24;
            }) > 0;
            var fromResultResult26 = Task.FromResult(evaluatedValue25);
            return fromResultResult26;
        }

        private static List<ProjectLibraryReference> createreferencesInitialValue((Guid ProjectId, string LibraryId)[] references)
        {
            var selectResult = references.Select(static item =>
            {
                var projectLibraryReference = new ProjectLibraryReference(item.ProjectId, item.LibraryId, DateTimeOffset.UtcNow);
                return projectLibraryReference;
            });
            List<ProjectLibraryReference> referencesInitialValue = selectResult.ToList();
            return referencesInitialValue;
        }
    }

    private sealed class CatalogService(LibraryDto library) : ILibraryCatalogService
    {
        public IReadOnlyList<LibraryDto> List() => [library];
        public LibraryDto? Find(string libraryId)
        {
            LibraryDto? evaluatedValue27 = string.Equals(library.Id, libraryId, StringComparison.OrdinalIgnoreCase) ? library : null;
            return evaluatedValue27;
        }

        public Task<IReadOnlyList<LibraryDto>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
        {
            var fromResultResult28 = Task.FromResult<IReadOnlyList<LibraryDto>>(includeArchived || library.Lifecycle == LibraryLifecycleDto.Available ? [library] : []);
            return fromResultResult28;
        }

        public Task<LibraryDto?> FindAsync(string libraryId, CancellationToken cancellationToken = default)
        {
            var findResult = Find(libraryId);
            var fromResultResult29 = Task.FromResult(findResult);
            return fromResultResult29;
        }

        public Task<LibraryUploadResultDto> UploadAsync(Stream package, string fileName, long? declaredLength = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public bool Delete(string libraryId) => false;
        public Task<bool> ArchiveAsync(string libraryId, CancellationToken cancellationToken = default)
        {
            var fromResultResult30 = Task.FromResult(false);
            return fromResultResult30;
        }

        public Task<LibraryDto?> ReindexAsync(string libraryId, CancellationToken cancellationToken = default)
        {
            var findResult31 = Find(libraryId);
            var fromResultResult32 = Task.FromResult(findResult31);
            return fromResultResult32;
        }

        public Task<int> ReindexOutdatedAsync(CancellationToken cancellationToken = default)
        {
            var fromResultResult33 = Task.FromResult(0);
            return fromResultResult33;
        }
    }
}

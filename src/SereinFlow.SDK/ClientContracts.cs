using System.Text.Json;
using SereinFlow.Contracts;

namespace SereinFlow.Client;

public interface ISereinFlowClient
{
    IFlowInvocationClient Flows { get; }

    IFlowRunClient Runs { get; }

    IRunMessageClient Messages { get; }

    IRunWorkpieceClient Workpieces { get; }
}

public interface IFlowInvocationClient
{
    Task<FlowRunDto> StartRunAsync(
        Guid projectId,
        Guid flowId,
        RunFlowRequestDto request,
        CancellationToken cancellationToken = default);

    Task<PublicFlowInvocationResponseDto> InvokeAsync(
        Guid interfaceId,
        PublicFlowInvocationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<PublicFlowInvocationResponseDto> GetTaskAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);

    Task<PublicFlowInvocationResponseDto> WaitForPublicTaskAsync(
        Guid taskId,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default);
}

public interface IFlowRunClient
{
    Task<FlowRunDto> GetAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<FlowDefinitionDto> GetSnapshotAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FlowRunOutputDto>> GetOutputsAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<FlowRunOutputDto?> GetLatestNodeOutputAsync(
        Guid runId,
        string nodeId,
        CancellationToken cancellationToken = default);

    Task<T?> GetLatestNodeOutputAsync<T>(
        Guid runId,
        string nodeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FlowRunEventDto>> GetEventsAsync(
        Guid runId,
        long afterSequence = 0,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<FlowRunEventDto> StreamEventsAsync(
        Guid runId,
        long afterSequence = 0,
        CancellationToken cancellationToken = default);

    Task<FlowRunResult> WaitForCompletionAsync(
        Guid runId,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default);

    Task CancelAsync(Guid runId, CancellationToken cancellationToken = default);
}

public interface IRunMessageClient
{
    Task<WorkerMessageDeliveryResponseDto> PublishMessageAsync<TPayload>(
        Guid runId,
        string topic,
        TPayload payload,
        string? contractId = null,
        WorkerMessageChannelKindDto channelKind = WorkerMessageChannelKindDto.Queue,
        string? idempotencyKey = null,
        string? messageId = null,
        CancellationToken cancellationToken = default);
}

public interface IRunWorkpieceClient
{
    Task<IReadOnlyList<FlowWorkpieceDto>> ListAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<FlowWorkpieceDownload> DownloadAsync(
        Guid runId,
        string workpieceId,
        bool asAttachment = true,
        CancellationToken cancellationToken = default);
}

public sealed record FlowRunResult(
    FlowRunDto Run,
    IReadOnlyList<FlowRunOutputDto> Outputs)
{
    public bool IsSucceeded => Run.Status == FlowRunStatusDto.Succeeded;

    public bool IsTerminal => Run.Status is
        FlowRunStatusDto.Succeeded or
        FlowRunStatusDto.Failed or
        FlowRunStatusDto.Cancelled or
        FlowRunStatusDto.TimedOut or
        FlowRunStatusDto.Interrupted;
}

public sealed class FlowWorkpieceDownload : IDisposable, IAsyncDisposable
{
    private readonly HttpResponseMessage _response;
    private int _disposed;

    internal FlowWorkpieceDownload(
        HttpResponseMessage response,
        Stream content,
        string contentType,
        string? fileName,
        long? length)
    {
        _response = response;
        Content = content;
        ContentType = contentType;
        FileName = fileName;
        Length = length;
    }

    public Stream Content { get; }

    public string ContentType { get; }

    public string? FileName { get; }

    public long? Length { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        Content.Dispose();
        _response.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        await Content.DisposeAsync().ConfigureAwait(false);
        _response.Dispose();
    }
}

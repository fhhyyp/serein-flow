using System.Net.Http;
using System.Net.Http.Headers;
using SereinFlow.Contracts;

namespace SereinFlow.Client;

public sealed class RunWorkpieceClient : IRunWorkpieceClient
{
    private readonly SereinFlowHttpClient _transport;

    internal RunWorkpieceClient(SereinFlowHttpClient transport)
        => _transport = transport;

    public async Task<IReadOnlyList<FlowWorkpieceDto>> ListAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
        => await _transport.SendJsonAsync<FlowWorkpieceDto[]>(
                HttpMethod.Get,
                $"api/runs/{runId:D}/workpieces",
                body: null,
                allowRetry: true,
                headers: null,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task<FlowWorkpieceDownload> DownloadAsync(
        Guid runId,
        string workpieceId,
        bool asAttachment = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workpieceId);
        var path = $"api/runs/{runId:D}/workpieces/{Uri.EscapeDataString(workpieceId)}?download={asAttachment.ToString().ToLowerInvariant()}";
        var response = await _transport.SendRawAsync(
                HttpMethod.Get,
                path,
                allowRetry: true,
                headers: null,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        try
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var disposition = response.Content.Headers.ContentDisposition;
            var fileName = disposition?.FileNameStar ?? disposition?.FileName;
            if (fileName is not null)
                fileName = fileName.Trim('"');
            return new FlowWorkpieceDownload(
                response,
                stream,
                response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
                fileName,
                response.Content.Headers.ContentLength);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }
}

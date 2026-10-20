using System.Net.Http;
using System.Net.Http.Headers;
using SereinFlow.Contracts;

namespace SereinFlow.Client;
public sealed class RunWorkpieceClient : IRunWorkpieceClient
{
    private readonly SereinFlowHttpClient _transport;
    internal RunWorkpieceClient(SereinFlowHttpClient transport)
    {
        _transport = transport;
    }

    public async Task<IReadOnlyList<FlowWorkpieceDto>> ListAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var sendJsonAsyncResult = _transport.SendJsonAsync<FlowWorkpieceDto[]>(HttpMethod.Get, $"api/runs/{runId:D}/workpieces", body: null, allowRetry: true, headers: null, cancellationToken);
        var configureAwaitResult = await sendJsonAsyncResult.ConfigureAwait(false);
        return configureAwaitResult;
    }

    public async Task<FlowWorkpieceDownload> DownloadAsync(Guid runId, string workpieceId, bool asAttachment = true, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workpieceId);
        var path = $"api/runs/{runId:D}/workpieces/{Uri.EscapeDataString(workpieceId)}?download={resolveToLowerInvariantResult()}";
        var sendRawAsyncResult = _transport.SendRawAsync(HttpMethod.Get, path, allowRetry: true, headers: null, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var response = await sendRawAsyncResult.ConfigureAwait(false);
        try
        {
            var readAsStreamAsyncResult = response.Content.ReadAsStreamAsync(cancellationToken);
            var stream = await readAsStreamAsyncResult.ConfigureAwait(false);
            var disposition = response.Content.Headers.ContentDisposition;
            var fileName = disposition?.FileNameStar ?? disposition?.FileName;
            if (fileName is not null)
            {
                fileName = fileName.Trim('"');
            }

            var flowWorkpieceDownload = new FlowWorkpieceDownload(response, stream, response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream", fileName, response.Content.Headers.ContentLength);
            return flowWorkpieceDownload;
        }
        catch
        {
            response.Dispose();
            throw;
        }

        string resolveToLowerInvariantResult()
        {
            var toStringResult = asAttachment.ToString();
            var toLowerInvariantResult = toStringResult.ToLowerInvariant();
            return toLowerInvariantResult;
        }
    }
}

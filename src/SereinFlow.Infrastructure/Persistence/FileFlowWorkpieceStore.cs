using System.Text.Json;
using SereinFlow.Application.Persistence;
using SereinFlow.Infrastructure.Configuration;
using SereinFlow.Library;

namespace SereinFlow.Infrastructure.Persistence;

/// <summary>
/// Reads the run-scoped sidecars written by the isolated Worker. Only IDs are
/// accepted from callers; neither route segments nor stored names are used to
/// construct an arbitrary path.
/// </summary>
public sealed class FileFlowWorkpieceStore : IFlowWorkpieceStore
{
    private static readonly JsonSerializerOptions JsonOptions =
        SereinFlow.Contracts.SereinJsonSerialization.CreateContractOptions();
    private readonly string _root;

    public FileFlowWorkpieceStore(SereinFlowStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _root = Path.GetFullPath(options.WorkpieceDirectory);
    }

    public Task<IReadOnlyList<FlowWorkpieceRecord>> ListAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (runId == Guid.Empty)
            throw new ArgumentException("Run ID cannot be empty. 运行 ID 不能为空。", nameof(runId));

        var directory = GetRunDirectory(runId);
        if (!Directory.Exists(directory))
            return Task.FromResult<IReadOnlyList<FlowWorkpieceRecord>>([]);

        var result = new List<FlowWorkpieceRecord>();
        foreach (var metadataPath in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var info = JsonSerializer.Deserialize<FlowWorkpieceInfo>(File.ReadAllText(metadataPath), JsonOptions);
                var metadataId = Path.GetFileNameWithoutExtension(metadataPath);
                if (info is null
                    || !IsSafeId(info.Id)
                    || !string.Equals(metadataId, info.Id, StringComparison.Ordinal)
                    || info.Length < 0)
                    continue;
                var dataPath = Path.Combine(directory, info.Id + ".bin");
                if (!File.Exists(dataPath))
                    continue;
                result.Add(new FlowWorkpieceRecord(
                    runId,
                    info.Id,
                    info.Kind.ToString(),
                    info.Name,
                    info.ContentType,
                    info.Length,
                    info.CreatedAt));
            }
            catch (JsonException)
            {
                // An incomplete sidecar can be visible during the final write.
                // It is ignored until the next read after the atomic rename.
            }
            catch (IOException)
            {
                // A concurrently removed or unavailable workpiece is not a
                // reason to fail the complete run listing.
            }
        }

        return Task.FromResult<IReadOnlyList<FlowWorkpieceRecord>>(
            result.OrderBy(static item => item.CreatedAt).ThenBy(static item => item.Id, StringComparer.Ordinal).ToArray());
    }

    public async Task<FlowWorkpieceRecord?> FindAsync(
        Guid runId,
        string workpieceId,
        CancellationToken cancellationToken = default)
    {
        if (!IsSafeId(workpieceId))
            return null;
        var items = await ListAsync(runId, cancellationToken);
        return items.FirstOrDefault(item => string.Equals(item.Id, workpieceId, StringComparison.Ordinal));
    }

    public async Task<Stream?> OpenReadAsync(
        Guid runId,
        string workpieceId,
        CancellationToken cancellationToken = default)
    {
        var item = await FindAsync(runId, workpieceId, cancellationToken);
        if (item is null)
            return null;

        var path = Path.Combine(GetRunDirectory(runId), item.Id + ".bin");
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    private string GetRunDirectory(Guid runId)
        => Path.Combine(_root, runId.ToString("N"));

    private static bool IsSafeId(string? value)
        => value is { Length: 32 }
            && value.All(static character => character is >= 'a' and <= 'f' or >= '0' and <= '9');
}

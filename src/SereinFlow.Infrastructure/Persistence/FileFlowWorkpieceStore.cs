using System.Text.Json;
using SereinFlow.Application.Persistence;
using SereinFlow.Infrastructure.Configuration;
using SereinFlow.Library;

namespace SereinFlow.Infrastructure.Persistence;
/// <summary>
/// Reads the run-scoped sidecars written by the isolated Worker. Only IDs are
/// accepted from callers; neither route segments nor stored names are used to
/// construct an arbitrary path.
/// 读取隔离 Worker 写入的运行级附属文件；调用方只传入 ID，不使用路由片段或存储名称构造任意路径。
/// </summary>
public sealed class FileFlowWorkpieceStore : IFlowWorkpieceStore
{
    private static readonly JsonSerializerOptions JsonOptions = SereinFlow.Contracts.SereinJsonSerialization.CreateContractOptions();
    private readonly string _root;
    public FileFlowWorkpieceStore(SereinFlowStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _root = Path.GetFullPath(options.WorkpieceDirectory);
    }

    public Task<IReadOnlyList<FlowWorkpieceRecord>> ListAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Run ID cannot be empty. 运行 ID 不能为空。", nameof(runId));
        }

        var directory = GetRunDirectory(runId);
        if (!Directory.Exists(directory))
        {
            return Task.FromResult<IReadOnlyList<FlowWorkpieceRecord>>([]);
        }

        var result = new List<FlowWorkpieceRecord>();
        foreach (var metadataPath in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var info = JsonSerializer.Deserialize<FlowWorkpieceInfo>(File.ReadAllText(metadataPath), JsonOptions);
                var metadataId = Path.GetFileNameWithoutExtension(metadataPath);
                if (info is null || !IsSafeId(info.Id) || !string.Equals(metadataId, info.Id, StringComparison.Ordinal) || info.Length < 0)
                {
                    continue;
                }

                var dataPath = Path.Combine(directory, info.Id + ".bin");
                if (!File.Exists(dataPath))
                {
                    continue;
                }

                var kind = info.Kind.ToString();
                var flowWorkpieceRecord = new FlowWorkpieceRecord(runId, info.Id, kind, info.Name, info.ContentType, info.Length, info.CreatedAt, info.NodeId, info.ExecutionId);
                result.Add(flowWorkpieceRecord);
            }
            catch (JsonException)
            {
                // An incomplete sidecar can be visible during the final write.
                // It is ignored until the next read after the atomic rename.
                // 最终写入期间可能看到不完整的附属文件；忽略该文件，待原子重命名后的下次读取再处理。
            }
            catch (IOException)
            {
                // A concurrently removed or unavailable workpiece is not a
                // reason to fail the complete run listing.
                // 工件被并发删除或暂时不可用时，不应使整个运行的列表读取失败。
            }
        }
        return Task.FromResult<IReadOnlyList<FlowWorkpieceRecord>>(result
            .OrderBy(static item => item.CreatedAt)
            .ThenBy(static item => item.Id, StringComparer.Ordinal)
            .ToArray());
    }

    public async Task<FlowWorkpieceRecord?> FindAsync(Guid runId, string workpieceId, CancellationToken cancellationToken = default)
    {
        if (!IsSafeId(workpieceId))
        {
            return null;
        }
        return (await ListAsync(runId, cancellationToken)).FirstOrDefault(item =>
        {
            return string.Equals(item.Id, workpieceId, StringComparison.Ordinal);
        });
    }

    public async Task<Stream?> OpenReadAsync(Guid runId, string workpieceId, CancellationToken cancellationToken = default)
    {
        var item = await FindAsync(runId, workpieceId, cancellationToken);
        if (item is null)
        {
            return null;
        }
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
    {
        var runIdText = runId.ToString("N");
        return Path.Combine(_root, runIdText);
    }

    private static bool IsSafeId(string? value)
    {
        return value is { Length: 32 } && value.All(static character => character is >= 'a' and <= 'f' or >= '0' and <= '9');
    }
}

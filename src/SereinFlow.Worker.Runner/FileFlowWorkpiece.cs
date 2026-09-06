using System.Text;
using System.Text.Json;
using SereinFlow.Contracts;
using SereinFlow.Library;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Worker.Runner;

/// <summary>
/// Run-scoped file implementation of the public workpiece contract. Data is
/// written to a temporary file first; the metadata sidecar is published only
/// after the data file has been atomically moved into place.
/// 运行作用域文件对公共工件契约的实现。
/// 数据首先写入临时文件；只有在数据文件被原子性地移动到最终位置后，元数据侧才会发布。
/// </summary>
internal sealed class FileFlowWorkpiece : IFlowWorkpiece
{
    private const long MaximumLength = 512L * 1024 * 1024;
    private static readonly JsonSerializerOptions MetadataOptions = CreateMetadataOptions();
    private readonly string? _root;
    private readonly Guid _runId;
    private readonly object _gate = new();
    private readonly string? _runDirectory;

    public FileFlowWorkpiece(string? root, Guid runId)
    {
        _root = string.IsNullOrWhiteSpace(root) ? null : Path.GetFullPath(root);
        _runId = runId;
        _runDirectory = _root is null ? null : Path.Combine(_root, runId.ToString("N"));
    }

    public FlowWorkpieceInfo UploadNodeOutput(IFlowContext context, string fileName, byte[] content, string? contentType = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(content);
        return UploadNodeOutput(context, fileName, new MemoryStream(content, writable: false), contentType);
    }

    public FlowWorkpieceInfo UploadNodeOutput(IFlowContext context, string fileName, string content, string? contentType = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        return UploadNodeOutput(
            context,
            fileName,
            Encoding.UTF8.GetBytes(content),
            contentType ?? FlowWorkpieceContentTypes.Text);
    }

    public FlowWorkpieceInfo UploadNodeOutput(IFlowContext context, string fileName, Stream content, string? contentType = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var kind = contentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true
            ? FlowWorkpieceKind.Image
            : FlowWorkpieceKind.File;
        var inferredContentType = kind == FlowWorkpieceKind.Image ? InferImageContentType(fileName) : null;
        return UploadCore(
            kind,
            fileName,
            content,
            contentType,
            inferredContentType,
            RequireNodeId(context.NodeId),
            RequireExecutionId(context.ExecutionId));
    }

    private FlowWorkpieceInfo UploadCore(
        FlowWorkpieceKind kind,
        string name,
        Stream content,
        string? contentType,
        string? inferredContentType,
        string? nodeId = null,
        Guid? executionId = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        var normalizedName = RequireFileName(name);
        var normalizedContentType = RequireContentType(
            contentType,
            inferredContentType,
            kind == FlowWorkpieceKind.Image ? InferImageContentType(normalizedName) : "application/octet-stream");
        if (_runDirectory is null)
        {
            throw new FlowWorkpieceException(
                "workpiece.not_configured",
                "The flow workpiece directory is not configured. 流程工件目录尚未配置。");
        }

        lock (_gate)
        {
            Directory.CreateDirectory(_runDirectory);
            var id = Guid.NewGuid().ToString("N");
            var dataPath = Path.Combine(_runDirectory, id + ".bin");
            var metadataPath = Path.Combine(_runDirectory, id + ".json");
            var temporaryDataPath = Path.Combine(_runDirectory, id + ".upload");
            var temporaryMetadataPath = Path.Combine(_runDirectory, id + ".metadata");
            try
            {
                long length;
                using (var target = new FileStream(
                    temporaryDataPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 64 * 1024,
                    FileOptions.SequentialScan))
                {
                    CopyBounded(content, target, out length);
                }

                File.Move(temporaryDataPath, dataPath);
                var info = new FlowWorkpieceInfo(
                    id,
                    kind,
                    normalizedName,
                    normalizedContentType,
                    length,
                    DateTimeOffset.UtcNow,
                    nodeId,
                    executionId);
                File.WriteAllText(temporaryMetadataPath, JsonSerializer.Serialize(info, MetadataOptions));
                File.Move(temporaryMetadataPath, metadataPath);
                return info;
            }
            catch (FlowWorkpieceException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new FlowWorkpieceException(
                    "workpiece.upload_failed",
                    "The workpiece could not be stored. 流程工件无法保存。",
                    exception);
            }
            finally
            {
                TryDelete(temporaryDataPath);
                TryDelete(temporaryMetadataPath);
                if (File.Exists(dataPath) && !File.Exists(metadataPath))
                    TryDelete(dataPath);
            }
        }
    }

    private static void CopyBounded(Stream source, Stream target, out long length)
    {
        var buffer = new byte[64 * 1024];
        length = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            length += read;
            if (length > MaximumLength)
                throw new FlowWorkpieceException(
                    "workpiece.too_large",
                    $"A workpiece cannot exceed {MaximumLength} bytes. 流程工件不能超过 {MaximumLength} 字节。");
            target.Write(buffer, 0, read);
        }
    }

    private static string RequireFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new FlowWorkpieceException("workpiece.name_required", "The workpiece name is required. 流程工件名称不能为空。");
        var name = value.Trim();
        if (name is "." or ".."
            || name != Path.GetFileName(name)
            || name.Contains(Path.DirectorySeparatorChar)
            || name.Contains(Path.AltDirectorySeparatorChar)
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new FlowWorkpieceException("workpiece.name_invalid", "The workpiece name must be a file name without a path. 流程工件名称必须是不含路径的文件名。");
        }
        return name;
    }

    private static string RequireNodeId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new FlowWorkpieceException("workpiece.node_id_required", "The node ID is required. 节点 ID 不能为空。");
        var nodeId = value.Trim();
        if (nodeId.Length > 256 || nodeId.Contains('\r') || nodeId.Contains('\n'))
            throw new FlowWorkpieceException("workpiece.node_id_invalid", "The node ID is invalid. 节点 ID 无效。");
        return nodeId;
    }

    private static Guid RequireExecutionId(Guid value)
    {
        if (value == Guid.Empty)
            throw new FlowWorkpieceException("workpiece.execution_id_required", "The execution ID is required. 执行步骤 ID 不能为空。");
        return value;
    }

    private static string RequireContentType(string? explicitType, string? inferredType, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(explicitType) ? inferredType : explicitType;
        value = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (value.Length > 128 || value.Contains('\r') || value.Contains('\n') || !value.Contains('/'))
            throw new FlowWorkpieceException("workpiece.content_type_invalid", "The workpiece content type is invalid. 流程工件 Content-Type 无效。");
        return value;
    }

    private static string InferImageContentType(string fileName)
        => Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".tif" or ".tiff" => "image/tiff",
            ".svg" => "image/svg+xml",
            _ => FlowWorkpieceContentTypes.Png,
        };

    private static JsonSerializerOptions CreateMetadataOptions()
        => SereinJsonSerialization.CreateContractOptions();

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}

internal sealed class FlowWorkpieceException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}

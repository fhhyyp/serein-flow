using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.Library;

/// <summary>
/// Identifies the kind of a run-local workpiece.
/// 标识运行级工件的类型。
/// </summary>
public enum FlowWorkpieceKind
{
    /// <summary>Image data.</summary>
    Image = 0,

    /// <summary>General file data.</summary>
    File = 1,
}

/// <summary>
/// A stable reference returned after a node uploads a workpiece.
/// 节点上传工件后返回的稳定引用。
/// </summary>
public sealed record FlowWorkpieceInfo(
    string Id,
    FlowWorkpieceKind Kind,
    string Name,
    string ContentType,
    long Length,
    DateTimeOffset CreatedAt,
    string? NodeId = null,
    Guid? ExecutionId = null);

/// <summary>
/// Stores non-JSON node data in the current flow run and exposes a small reference that can be
/// inspected through the API or MCP. Implementations do not take ownership of supplied streams.
/// 将非 JSON 节点数据保存到当前流程运行，并返回可通过 API 或 MCP 查阅的小型引用；实现不会接管传入流的所有权。
/// </summary>
public interface IFlowWorkpiece
{
    /// <summary>
    /// Uploads a file and associates it with the currently executing flow node.
    /// The node ID and execution ID are persisted with the workpiece so the
    /// debugger can select the artifact produced by the selected execution step.
    /// 上传文件并将其绑定到当前执行的流程节点；节点 ID 会随工件元数据保存，
    /// 执行 ID 也会随工件元数据保存，供调试器精确选中本次执行步骤产出的工件。
    /// </summary>
    FlowWorkpieceInfo UploadNodeOutput(IFlowContext context, string fileName, byte[] content, string? contentType = null);

    /// <summary>
    /// Uploads UTF-8 text as a node output. Use a <c>.txt</c> file name when a text file is
    /// desired; the default content type is <c>text/plain; charset=utf-8</c>.
    /// 以 UTF-8 文本上传节点输出。需要文本文件时请使用 <c>.txt</c> 文件名；默认类型为
    /// <c>text/plain; charset=utf-8</c>。
    /// </summary>
    FlowWorkpieceInfo UploadNodeOutput(IFlowContext context, string fileName, string content, string? contentType = null);

    /// <summary>
    /// Uploads node output data from the current stream position.
    /// 从当前流位置上传节点输出数据
    /// </summary>
    FlowWorkpieceInfo UploadNodeOutput(IFlowContext context, string fileName, Stream content, string? contentType = null);
}

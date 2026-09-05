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
    string? NodeId = null);

/// <summary>
/// Stores non-JSON node data in the current flow run and exposes a small reference that can be
/// inspected through the API or MCP. Implementations do not take ownership of supplied streams.
/// 将非 JSON 节点数据保存到当前流程运行，并返回可通过 API 或 MCP 查阅的小型引用；实现不会接管传入流的所有权。
/// </summary>
public interface IFlowWorkpiece
{
    /// <summary>Uploads image bytes.</summary>
    FlowWorkpieceInfo UploadImage(string imageName, byte[] content, string? contentType = null);

    /// <summary>Uploads image data from the current stream position.</summary>
    FlowWorkpieceInfo UploadImage(string imageName, Stream content, string? contentType = null);

    /// <summary>Uploads a base64 image, including an optional data-URI prefix.</summary>
    FlowWorkpieceInfo UploadImage(string imageName, string base64, string? contentType = null);

    /// <summary>Uploads file bytes.</summary>
    FlowWorkpieceInfo UploadFile(string fileName, byte[] content, string? contentType = null);

    /// <summary>Uploads file data from the current stream position.</summary>
    FlowWorkpieceInfo UploadFile(string fileName, Stream content, string? contentType = null);

    /// <summary>
    /// Uploads a file and associates it with the currently executing flow node.
    /// The node ID is persisted with the workpiece so the debugger can select
    /// the artifact produced by the selected execution step.
    /// 上传文件并将其绑定到当前执行的流程节点；节点 ID 会随工件元数据保存，
    /// 供调试器按执行步骤自动选中节点产出的工件。
    /// </summary>
    FlowWorkpieceInfo UploadNodeOutput(string nodeId, string fileName, byte[] content, string? contentType = null);

    /// <summary>Uploads node output data from the current stream position.</summary>
    FlowWorkpieceInfo UploadNodeOutput(string nodeId, string fileName, Stream content, string? contentType = null);
}

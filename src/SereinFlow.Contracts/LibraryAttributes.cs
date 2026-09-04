using System.Runtime.CompilerServices;
using SereinFlow.Core.Api;
using SereinFlow.Library;

// Keep the original Contracts assembly as a binary-compatible forwarding
// facade. New libraries should reference SereinFlow.Library directly.
// 保留原 Contracts 程序集作为二进制兼容的转发外观；新类库应直接引用 SereinFlow.Library。
[assembly: TypeForwardedTo(typeof(FlowLibraryAttribute))]
[assembly: TypeForwardedTo(typeof(FlowNodeAttribute))]
[assembly: TypeForwardedTo(typeof(NodeResultAttribute))]
[assembly: TypeForwardedTo(typeof(NodeResultAttribute<>))]
[assembly: TypeForwardedTo(typeof(INodeResultConverter<,>))]
[assembly: TypeForwardedTo(typeof(IFlowNativeLibraryLoader))]
[assembly: TypeForwardedTo(typeof(FlowNativeLibraryException))]
[assembly: TypeForwardedTo(typeof(NativeLibraryDirectoryAttribute))]
[assembly: TypeForwardedTo(typeof(FlowWorkpieceKind))]
[assembly: TypeForwardedTo(typeof(FlowWorkpieceInfo))]
[assembly: TypeForwardedTo(typeof(IFlowWorkpiece))]
[assembly: TypeForwardedTo(typeof(NodeParamAttribute))]
[assembly: TypeForwardedTo(typeof(NodeType))]
[assembly: TypeForwardedTo(typeof(FlowServiceLifetime))]
[assembly: TypeForwardedTo(typeof(FlowServiceAttribute))]
[assembly: TypeForwardedTo(typeof(FlowServiceAttribute<>))]
[assembly: TypeForwardedTo(typeof(LibraryAttributeContract))]

using System.Runtime.CompilerServices;
using SereinFlow.Library;

// Forward the canonical SDK types. Libraries using the former Core.Api
// namespace must update their imports and rebuild against SereinFlow.Library.
// 转发规范化后的 SDK 类型；使用旧 Core.Api 命名空间的类库需更新引用并重新编译。
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

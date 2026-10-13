using System.Runtime.CompilerServices;

// Forward the canonical SDK contracts to SereinFlow.Library. Libraries using
// the former Runtime.Abstractions namespace must update their imports and rebuild.
// 将规范化后的 SDK 契约转发至 SereinFlow.Library；使用旧命名空间的类库需更新引用并重新编译。
[assembly: TypeForwardedTo(typeof(SereinFlow.Library.IFlowContext))]
[assembly: TypeForwardedTo(typeof(SereinFlow.Library.FlowContextContract))]

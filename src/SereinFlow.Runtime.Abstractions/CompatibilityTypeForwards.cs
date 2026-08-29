using System.Runtime.CompilerServices;

// Existing node libraries referenced Runtime.Abstractions directly. Forward
// the public context contract so those binaries continue to resolve while new
// libraries can depend only on SereinFlow.Library.
// 既有节点类库可能直接引用 Runtime.Abstractions。通过类型转发保持这些二进制可解析，
// 新类库则只需依赖 SereinFlow.Library。
[assembly: TypeForwardedTo(typeof(SereinFlow.Runtime.Abstractions.IFlowContext))]
[assembly: TypeForwardedTo(typeof(SereinFlow.Runtime.Abstractions.FlowContextContract))]

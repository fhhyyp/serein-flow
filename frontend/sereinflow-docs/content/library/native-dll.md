# 加载 native DLL

有些 .NET 包还需要调用操作系统上的本机文件：Windows 常见 `.dll`，Linux 常见 `.so`，macOS 常见 `.dylib`。例如图像处理库可能先加载一份本机运算库。把这些文件随节点类库一起打进 ZIP，SereinFlow 才能在 Worker 中找到它们。

## 先放对目录

下面是一个 ZIP 内部的示意结构。这里 `MyFlowNodes.dll` 所在目录是相对路径的起点：

```text
MyFlowNodes-1.0.0.zip
└─ MyFlowNodes-1.0.0/
   ├─ MyFlowNodes.dll
   ├─ MyFlowNodes.deps.json
   └─ runtimes/
      ├─ win-x64/native/device.dll
      └─ linux-x64/native/libdevice.so
```

具体需要哪些文件取决于所用库。`win-x64`、`linux-x64` 这样的目录名表示 Worker 实际运行的系统与架构；只放目标环境所需文件也可以。不要把不同架构的本机库混放到同一目录。

## 固定依赖：写一次目录声明

如果每次使用类库都需要这些文件，推荐在程序集上声明：

```csharp
using SereinFlow.Core.Api;

[assembly: NativeLibraryDirectory(
    "runtimes/{rid}/native",
    Recursive = false,
    Required = true)]
```

放到 `NativeLibraryManifest.cs` 等单独文件即可。`{rid}` 会替换成当前 Worker 的运行时标识，例如 `win-x64`。也可以将这个特性放在某个节点类上，只为该类声明依赖。`Recursive` 控制是否扫描子目录，默认 `true`；`Required` 指定目录和库文件是否必须加载，默认 `true`。

本仓库的 `SereinFlow.OpenCvLibrary` 当前就用程序集级声明加载 `runtimes/{rid}/native`，并设为非递归、必需。

## 按条件加载：注入加载器

如果只在某个节点工作时才需要依赖，可以把 `IFlowNativeLibraryLoader` 写进节点构造函数：

```csharp
using SereinFlow.Core.Api;
using SereinFlow.Library;

[FlowLibrary("设备工具")]
public sealed class DeviceNodes
{
    public DeviceNodes(IFlowNativeLibraryLoader native)
    {
        native.LoadNativeLibraryDirectory(
            "vendor/native/{rid}", recursive: true, required: true);
    }

    [FlowNode(AnotherName = "读取设备")]
    public string Read() => "设备已就绪";
}
```

如果只要一个文件，可调用 `native.LoadNativeLibrary("vendor/native/{rid}/device.dll")`；文件不存在时它返回 `false`。加载器在一次运行内重复请求同一个文件是安全的。目录加载设置 `required: false` 后，缺失或为空的目录会跳过，文件加载失败会记录 Worker 诊断并继续；必需目录或库失败会抛出 `FlowNativeLibraryException`。

## 加载失败时检查什么

1. 打开 ZIP，确认文件路径与声明完全对应；路径相对于类库主程序集目录。
2. 确认 `{rid}` 对应 Worker 实际系统和架构；例如 `win-x64` 文件不能在 `linux-x64` Worker 上加载。
3. 检查本机库依赖的其他本机文件是否也在包内。不要只放最外层的一个 DLL。
4. 查看 `FlowNativeLibraryException.Code`、`RelativePath` 和异常消息，判断是路径被拒绝、文件缺失还是系统加载失败。

加载路径必须留在上传的类库包内；不要使用绝对路径、`..` 或指向服务器其他目录的路径。ZIP 保留原有子目录后，再按照[打包与上传](/library/package-upload)检查项目是否引用了这份类库。

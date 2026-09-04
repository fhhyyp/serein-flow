using SereinFlow.Core.Api;

// OpenCvSharp keeps its platform-specific native shim under the standard RID directory.
// OpenCvSharp 将平台相关的 Native 适配库放在标准 RID 目录下。
[assembly: NativeLibraryDirectory(
    "runtimes/{rid}/native",
    Recursive = false,
    Required = true)]

# Load native DLLs

Some .NET packages require platform-specific native files: `.dll` on Windows, `.so` on Linux, or `.dylib` on macOS. Include them in the node library ZIP so the Worker can find them.

## Directory layout

Paths are relative to the directory containing `MyFlowNodes.dll`:

```text
MyFlowNodes-1.0.0.zip
└─ MyFlowNodes-1.0.0/
   ├─ MyFlowNodes.dll
   ├─ MyFlowNodes.deps.json
   └─ runtimes/
      ├─ win-x64/native/device.dll
      └─ linux-x64/native/libdevice.so
```

Include files needed by the Worker's OS and architecture. Do not mix binaries for different architectures in the same directory.

## Fixed dependencies: declare a directory

If every use of the library needs the native files, add an assembly attribute in a file such as `NativeLibraryManifest.cs`:

```csharp
using SereinFlow.Core.Api;

[assembly: NativeLibraryDirectory(
    "runtimes/{rid}/native",
    Recursive = false,
    Required = true)]
```

`{rid}` is replaced by the Worker's runtime identifier, such as `win-x64`. The attribute can instead be put on one node class. `Recursive` defaults to `true`; `Required` also defaults to `true`. `SereinFlow.OpenCvLibrary` currently declares this directory at assembly level, non-recursively and as required.

## Conditional loading: inject the loader

When a dependency is needed only for one node, inject `IFlowNativeLibraryLoader`:

```csharp
using SereinFlow.Core.Api;
using SereinFlow.Library;

[FlowLibrary("Device tools")]
public sealed class DeviceNodes
{
    public DeviceNodes(IFlowNativeLibraryLoader native)
    {
        native.LoadNativeLibraryDirectory(
            "vendor/native/{rid}", recursive: true, required: true);
    }

    [FlowNode(AnotherName = "Read device")]
    public string Read() => "Device ready";
}
```

Use `native.LoadNativeLibrary("vendor/native/{rid}/device.dll")` for one file; it returns `false` if absent. Repeated loads within a run are safe. With `required: false`, missing or empty directories are skipped and file load failures are logged in Worker diagnostics. Required failures throw `FlowNativeLibraryException`.

## Troubleshooting

1. Inspect the ZIP and verify the path relative to the main assembly.
2. Match `{rid}` to the actual Worker OS and architecture.
3. Include transitive native dependencies, not only the top-level DLL.
4. Inspect `FlowNativeLibraryException.Code`, `RelativePath`, and its message.

Paths must stay inside the uploaded package. Do not use absolute paths or `..`. Preserve subdirectories in the ZIP and confirm project referencing in [Package and upload](/en/library/package-upload).

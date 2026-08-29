---
name: sereinflow-library-package
description: Build a completed SereinFlow C# class library with the standalone SereinFlow.Library SDK and the user's local VS/.NET toolchain, then create a validated upload ZIP named [AssemblyName]-[Version].zip. Use when an AI or user has a SereinFlow library project or DLL and needs a deterministic package for sereinflow_preview_library_package; automatically discover the project and artifact directory instead of requiring an absolute output path.
---

# SereinFlow Library Package

Create the upload artifact locally. The SereinFlow server only receives the
finished ZIP, scans its PE metadata, and imports it; it does not compile C# or
execute any project/build file.

## Required Contract

Use an SDK-style C# project targeting the runtime supported by the project
(`net10.0` in the current SereinFlow repository). Reference the standalone
`SereinFlow.Library` NuGet package. It contains the metadata attributes and
restricted `IFlowContext` contract without bringing in SereinFlow Domain,
Application, or server implementation assemblies:

```xml
<ItemGroup>
  <PackageReference Include="SereinFlow.Library" Version="1.0.0" />
</ItemGroup>
```

When using the repository's local feed, create the SDK package first with
`dotnet pack src\SereinFlow.Library\SereinFlow.Library.csproj -c Release -o artifacts\nuget`; do not replace the `PackageReference` with a DLL reference.
Use the repository `NuGet.config` (or an explicitly supplied CI config) during
restore. If a user-level source points to a missing directory, report that
source as the cause and repair the invocation/configuration locally; do not
silently change the user's global NuGet configuration.

The evaluated MSBuild properties must provide:

- `AssemblyName`: the library name and DLL stem;
- `Version`: the package version, normally three-part SemVer such as `1.6.1`;
- `TargetPath`: the built DLL path;
- `EnableDynamicLoading`: must evaluate to `true` so the host can dynamically
  load the library.

The package contract is strict:

```text
[AssemblyName]-[Version].zip
└── [AssemblyName]-[Version]/
    ├── [AssemblyName].dll
    ├── [AssemblyName].pdb
    ├── [AssemblyName].deps.json
    ├── [AssemblyName].runtimeconfig.json
    ├── dependency.dll
    └── runtimes/<rid>/native/<native-library>
```

The main DLL must be exactly one directory below the ZIP root and its name must
match `AssemblyName`. Do not rename a DLL by hand to disguise a project or
version mismatch. The package must explicitly include every file produced by a
clean `dotnet publish`, including managed dependencies, configuration files,
PDBs, and native assets. Preserve the publish-relative paths because the
Worker resolves dependencies with `AssemblyDependencyResolver`. Never package
the whole `bin` or `obj` tree.

For a project that generates its own test artifact, use the same calculated
values rather than duplicating literal paths:

```xml
<TestLibraryPackageName>$(AssemblyName)-$(Version)</TestLibraryPackageName>
<EnableDynamicLoading>true</EnableDynamicLoading>
<TestLibraryArtifactDirectory>$(MSBuildProjectDirectory)\..\..\artifacts\libraries\</TestLibraryArtifactDirectory>
<TestLibraryZipPath>$(TestLibraryArtifactDirectory)$(TestLibraryPackageName).zip</TestLibraryZipPath>
```

## Package Workflow

1. Discover the intended `.csproj` from the current workspace. If there is
   exactly one candidate, use it. If several exist, prefer the project that
   declares `TestLibraryPackageName`; otherwise ask for the project file. A
   project path may be supplied, but an output path is not required.
2. Find the repository root by walking upward from the project directory and
   looking for `SereinFlow.sln` or `.git`. This root discovery is only for the
   local packaging output location; it must never be used to diagnose a
   connected service error. In a projectless workspace, pass the explicit
   `-RepositoryRoot` to the bundled script; do not rely on the project
   directory fallback when the output must be placed in a shared repository.
3. Run `scripts/New-SereinFlowLibraryPackage.ps1` or reproduce its exact
   behavior. Let MSBuild calculate `AssemblyName`, `Version`, `TargetFramework`,
   `TargetPath`, and `EnableDynamicLoading`; do not parse inherited
   `Directory.Build.props` by hand.
4. Publish with the local VS/.NET command-line toolchain into a clean,
   dedicated temporary directory under the project `obj` directory. Use the
   selected configuration and, when the library has RID-specific dependencies,
   pass the target `RuntimeIdentifier` (for example `win-x64`). Stop on a
   publish error. Never ask the
   SereinFlow service to run `dotnet publish`, `dotnet build`, MSBuild, a
   `.csproj`, `.props`, `.targets`, or a build script.
5. Enumerate every file in that dedicated publish directory and copy it into
   the staging package while preserving its relative path. Create the artifact
   directory automatically at `<repository-root>/artifacts/libraries`; do not
   require the caller to recreate or paste this path. Do not source files from
   an existing `bin` or `obj` directory.
6. Create `[AssemblyName]-[Version].zip` with the package directory as the
   first-level entry. Replace only the same calculated output file.
7. Inspect the ZIP before reporting it: verify the expected main DLL entry,
   all publish output files, preserved relative paths, the package filename,
   and that no path traversal or source/script/project/executable files exist.
   Report the absolute publish directory, package path, package name, file
   count, size, and SHA-256.
8. Submit that ZIP through `sereinflow_preview_library_package`. Review the
   returned PE scan and contract diagnostics, then wait for explicit user
   confirmation before calling `sereinflow_apply_library_package`.

## Project Metadata

Use the public SDK types and do not define local copies of `FlowLibraryAttribute`,
`FlowNodeAttribute`, `NodeParamAttribute`, `NodeType`, `DynamicFlowAttribute`, or
`NodeActionAttribute`. `DynamicFlow` and `NodeAction` belong to the removed
legacy model and are ignored or rejected by the current scanner.

The class library should define `FlowLibraryAttribute` on its library class,
`FlowNodeAttribute` on node methods, and `NodeParamAttribute` on parameters:

```csharp
using SereinFlow.Core.Api;
using SereinFlow.Runtime.Abstractions;

namespace OpenCvNodes;

[FlowLibrary("OpenCV Image Processing Demo")]
public sealed class OpenCvImageNodes
{
    [FlowNode(AnotherName = "Decode image", Desc = "Decode and normalize encoded image bytes.")]
    public byte[] DecodeImage(
        [NodeParam(Name = "Encoded image bytes")] byte[] imageBytes)
    {
        // OpenCV implementation goes here.
        return imageBytes;
    }

    [FlowNode(NodeType = NodeType.Flipflop, AnotherName = "Wait for image trigger")]
    public async Task<bool> WaitForImageTrigger(
        [NodeParam(Name = "Source name")] string sourceName,
        IFlowContext flowContext)
    {
        await Task.Yield();
        flowContext.SelectSuccess();
        return !string.IsNullOrWhiteSpace(sourceName);
    }
}
```

Prefer the current attribute defaults so the scanner can derive stable IDs:
the node ID uses `FlowLibrary.Name` (or the declaring class name when `Name` is
omitted) plus the CLR method name, and the parameter ID uses the CLR parameter
name. Do not hand-write these IDs for ordinary nodes, and do not introduce IDs
that change between library builds unless a deliberate contract migration is
intended. Keep `AnotherName` and `Desc` for display only. The `IFlowContext`
parameter is injected and is not a user-facing input.

For the extracted OpenCV source, replace the old form:

```csharp
[FlowNode("Decode image", "...")]
[NodeAction(NodeType.Action, "Decode image bytes")]
```

with the SDK form shown above. Remove both legacy attributes and the local
attribute/type declarations; keeping them creates metadata the SereinFlow PE
scanner cannot recognize.

Keep the project instance-node convention used by SereinFlow. If the project
uses static node methods only to silence analyzers, do not silently change the
runtime model. Check the generated DLL with the library preview, not by loading
it into the packaging script.

## Safety And Failure Handling

- Never accept or invent an arbitrary server-local path for remote MCP upload.
- Never package the whole `bin` or `obj` tree, source files, project files,
  build scripts, nested archives, or EXEs. Managed dependencies, PDBs,
  `.deps.json`, `.runtimeconfig.json`, and native runtime assets are expected
  when they are present in the clean publish output.
- Treat an absent `TargetPath`, a missing published main DLL, a malformed
  version, a name mismatch, `EnableDynamicLoading` not evaluating to `true`, a
  missing publish output, and an unsafe ZIP entry as hard failures. Dependency
  DLLs are valid and must not be mistaken for duplicate main DLLs.
- Do not delete source or existing immutable artifacts. The script may remove
  only its own generated staging directory and replace the calculated artifact
  with the same package name.
- A successful local ZIP is not proof that the library is compatible. Use the
  MCP preview as the authoritative PE scan and compatibility check.
- A successful PE preview is not proof that Worker dependencies are installed.
  For libraries such as OpenCvSharp, verify the Worker has the matching managed
  assembly and native runtime before calling the flow executable.
- Treat the connected SereinFlow service as a black box. If preview, apply, or
  execution returns an error, use its MCP error code, structured data, and
  bounded diagnostics; do not search for `SereinFlow.sln`, inspect server
  source/database files, or infer a production cause from this development
  repository. Ask for a correlated server log when the public diagnostic is
  insufficient.

## Bundled Automation

Use [scripts/New-SereinFlowLibraryPackage.ps1](scripts/New-SereinFlowLibraryPackage.ps1)
for repeatable local packaging. It discovers the project when possible,
calculates the repository-relative artifact directory, builds with the local
toolchain, and validates the resulting ZIP. For RID-specific dependencies use
`-RuntimeIdentifier` (for example `-RuntimeIdentifier win-x64`). For
projectless workspaces use `-ProjectPath` together with `-RepositoryRoot`. Read
[references/package-contract.md](references/package-contract.md) when adding or
reviewing project packaging targets.

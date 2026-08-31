# SereinFlow Library Package MCP Skill

Use this Resource for tasks that build, inspect, import, attach, or instantiate
a SereinFlow C# library package. Load it only for library-package work. The
connected MCP service is a remote inspection/import boundary; it is not the
caller's C# build machine and it must be treated as a black box.

## Build boundary

Build the library locally with the caller's Visual Studio or .NET toolchain.
The SereinFlow MCP service does not compile C# source, execute a `.csproj`,
`.props`, or `.targets` file, run MSBuild, run `dotnet build`, run
`dotnet publish`, or execute a build script. Do not ask the service to build
from a source path and do not send it an arbitrary server-local path.

When starting from source, produce the completed ZIP locally and send only the
ZIP to `sereinflow_preview_library_package`. When only a DLL or an incomplete
artifact is available, do not invent a package contract: obtain a clean publish
output and its dependency closure first.

## Project requirements

Use an SDK-style C# class library targeting a runtime supported by the
deployment. The current repository examples target `net10.0`. Reference the
standalone `SereinFlow.Library` NuGet package:

```xml
<ItemGroup>
  <PackageReference Include="SereinFlow.Library" Version="1.0.0" />
</ItemGroup>
```

The package supplies the public metadata attributes and restricted runtime
context without referencing SereinFlow Domain, Application, or server
implementation assemblies. Never replace the `PackageReference` with a DLL
reference and never copy local definitions of the SDK contracts into the
library.

If the repository's local feed is required, build the SDK package locally, for
example with `dotnet pack` and an `artifacts/nuget` output, then restore using
the repository `NuGet.config` or an explicitly supplied CI configuration. A
user-level NuGet source that points to a missing directory is a local restore
configuration problem: report it and repair the invocation or local config.
Do not silently modify the user's global NuGet configuration.

Let MSBuild evaluate these properties; do not parse inherited build files by
hand:

- `AssemblyName`: library name and DLL stem;
- `Version`: package version, normally a three-part SemVer such as `1.6.1`;
- `TargetPath`: built main DLL path;
- `EnableDynamicLoading`: must evaluate to `true`.

If the project has MSBuild targets for a test artifact, derive their values
from the evaluated properties instead of duplicating literal names or paths:

```xml
<TestLibraryPackageName>$(AssemblyName)-$(Version)</TestLibraryPackageName>
<EnableDynamicLoading>true</EnableDynamicLoading>
<TestLibraryArtifactDirectory>$(MSBuildProjectDirectory)\..\..\artifacts\libraries\</TestLibraryArtifactDirectory>
<TestLibraryZipPath>$(TestLibraryArtifactDirectory)$(TestLibraryPackageName).zip</TestLibraryZipPath>
```

## ZIP contract

The outer ZIP filename must be `[AssemblyName]-[Version].zip`, and the ZIP
must contain exactly one package directory at its first level:

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

The main DLL must be exactly one directory below the ZIP root and its filename
must match the evaluated `AssemblyName`. Include every file from a clean
`dotnet publish` output, not only the main DLL: managed dependencies, config
files, PDBs, `.deps.json`, `.runtimeconfig.json`, and native runtime assets when
present. Preserve every publish-relative path because the Worker resolves
dependencies with `AssemblyDependencyResolver`.

Do not package the whole `bin` or `obj` tree. Do not manually rename a DLL to
hide a project-name or version mismatch. Dependency DLLs are valid and must
not be treated as duplicate main DLLs. Do not include source files, project or
build files, scripts, nested archives, or EXEs.

## Local packaging workflow

Follow this sequence when the caller has a project workspace:

1. Discover `.csproj` files in the current workspace. Use the only candidate
   automatically. If there are multiple candidates, prefer the project that
   declares `TestLibraryPackageName`; otherwise ask the caller to identify the
   project. Do not require an absolute output path from the caller.
2. Walk upward from the selected project directory to find `SereinFlow.sln` or
   `.git`. Use that repository root only to choose the local artifact directory;
   never use it to diagnose a connected MCP service error. For a projectless
   workspace, the caller must explicitly provide its repository root to the
   local packaging helper.
3. Use the repository's packaging helper when available,
   `scripts/New-SereinFlowLibraryPackage.ps1`, or reproduce its behavior. Let
   MSBuild calculate `AssemblyName`, `Version`, `TargetFramework`, `TargetPath`,
   and `EnableDynamicLoading`.
4. Publish with the local toolchain into a clean, dedicated temporary directory
   under the project `obj` directory. Select the required configuration. For
   RID-specific dependencies pass the target `RuntimeIdentifier`, such as
   `win-x64`. Stop on a publish error.
5. Enumerate all files in that clean publish directory and copy them into
   staging while preserving relative paths. Create
   `<repository-root>/artifacts/libraries` automatically when that is the
   caller's local repository convention. Do not source files from pre-existing
   `bin` or `obj` build output; the dedicated publish directory created for
   this run is the sole source.
6. Create the calculated `[AssemblyName]-[Version].zip`. Replace only the ZIP
   with that exact calculated name; do not delete source or unrelated artifacts.
7. Inspect the ZIP before uploading. Verify the package filename, first-level
   directory, matching main DLL, all clean-publish files and relative paths,
   absence of path traversal, and absence of source/script/project/EXE files.
8. Report the absolute clean publish directory, ZIP path, package name, file
   count, byte size, and SHA-256 before submitting the artifact.

The local ZIP check is necessary but not sufficient. A valid local ZIP does not
prove SereinFlow compatibility; the MCP preview is authoritative for PE and
contract analysis.

## C# metadata contract

Use the public SDK types:

```csharp
using SereinFlow.Core.Api;
using SereinFlow.Runtime.Abstractions;
```

Use `FlowLibraryAttribute`, `FlowNodeAttribute`, `NodeParamAttribute`,
`NodeType`, and `IFlowContext`. Do not define or retain local copies of
`DynamicFlowAttribute`, `NodeActionAttribute`, `FlowLibraryAttribute`,
`FlowNodeAttribute`, `NodeParamAttribute`, `NodeType`, or `IFlowContext`.
`DynamicFlow` and `NodeAction` are legacy metadata and are ignored or rejected
by the current scanner.

Use the current SDK attribute form:

```csharp
[FlowLibrary("OpenCV Image Processing Demo")]
public sealed class OpenCvImageNodes
{
    [FlowNode(AnotherName = "Decode image", Desc = "Decode and normalize encoded image bytes.")]
    public byte[] DecodeImage(
        [NodeParam(Name = "Encoded image bytes")] byte[] imageBytes)
    {
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

For ordinary nodes, prefer the SDK defaults so the scanner can derive stable
IDs: node identity is based on `FlowLibrary.Name` (or the declaring class name
when omitted) plus the CLR method name, and parameter identity is based on the
CLR parameter name. Do not hand-write IDs that can change between builds.
`AnotherName` and `Desc` are display metadata only. `IFlowContext` is an
injected runtime parameter and is not a user-facing input. Preserve the
project's instance-node convention; do not silently convert instance methods
to static methods merely to silence an analyzer.

## Preview, import, and attachment

Submit the completed ZIP as base64 with its filename to
`sereinflow_preview_library_package`. Optional project, family, or baseline
identifiers may be supplied only when the caller has the corresponding
persistent IDs and needs impact or compatibility analysis. Review the complete
bounded result, including:

- package and main-DLL hashes;
- PE scan and contract diagnostics;
- `FlowLibrary`, `FlowNode`, and `NodeParam` recognition;
- stable-ID confidence and duplicate IDs;
- managed dependencies and native assets;
- Flipflop return-type diagnostics and project impact.

The scanner must inspect metadata without loading or executing the uploaded
assembly. An explicit user request to upload or import the library authorizes
this named logical task. If the preview matches the request, call
`sereinflow_apply_library_package` with the preview identity, fingerprint,
`confirmation: "APPLY"`, idempotency key, and the package data required by the
API without asking for a second confirmation. Importing a library does not
attach it to a project unless that is part of the request.

When the same request includes attachment, keep the dependent writes in one
task-level authorization and use this sequence:

```text
sereinflow_preview_project_library_attach
    -> inspect the attachment diff
sereinflow_apply_project_library_attach
```

Do not ask separately between package import and project attachment when both
were requested. Pause once only if a preview adds an unexpected project,
changes production state, changes permissions or secrets, reports a conflict,
or otherwise exceeds the requested scope.

After attachment, call the read-only
`sereinflow_create_library_node_template` with the persisted `projectId`,
`libraryId`, `libraryNodeContractId`, and `position`. This tool does not create
a preview, flow version, or Resource. Use its returned canonical `node`
unchanged as the `addNode.node` payload of a v2 flow patch. Do not manually
assemble runtime library metadata, parameter ports, parameter IDs, default
literals, enum or variadic metadata, or execution ports. Keep library
attachment separate from the flow patch, then use the SereinFlow flow skill's
task-level preview, apply, and post-apply verification gate. The apply tools'
`confirmation: "APPLY"` field remains required by the protocol, but it does
not require a duplicate user prompt within the same authorized task.

## Safety and failure rules

Hard-fail and report diagnostics when any of these occurs: missing `TargetPath`,
missing published main DLL, malformed version, DLL name mismatch,
`EnableDynamicLoading` not evaluating to `true`, missing publish output, or an
unsafe ZIP entry. Never accept or invent an arbitrary server-local path.

The local packaging helper may remove only its own generated staging directory
and replace the same calculated ZIP. It must not delete source code or existing
immutable artifacts. Preserve expected publish dependencies, PDBs, metadata
files, and native assets. A successful PE preview also does not prove that the
Worker has matching managed and native dependencies installed; verify that
deployment prerequisite separately for libraries such as OpenCvSharp.

Treat the connected MCP service as a black box. For remote failures, use the
returned MCP error code, structured data, and bounded diagnostics. Do not search
the local development repository for the service's production cause, inspect
server databases, or infer a remote deployment problem from local source.
Request a correlated server log when the public diagnostic is insufficient.

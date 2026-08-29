# SereinFlow Library ZIP Contract

Use this reference when creating or reviewing a C# class library project that
will be uploaded to SereinFlow.

## SDK Dependency

Reference the standalone `SereinFlow.Library` NuGet package. It is the only
SereinFlow package a node library needs for metadata and flow-context APIs:

```xml
<PackageReference Include="SereinFlow.Library" Version="1.0.0" />
```

For the repository's local feed, create it with
`dotnet pack src\SereinFlow.Library\SereinFlow.Library.csproj -c Release -o artifacts\nuget` before restoring the consumer project.
Use the repository-level `NuGet.config` or an explicit CI configuration. A
missing user-level local source should be diagnosed as a restore-environment
problem, not fixed by mutating the global NuGet configuration.

The package exposes the stable public names `SereinFlow.Core.Api` for
`FlowLibraryAttribute`, `FlowNodeAttribute`, `NodeParamAttribute`, `NodeType`,
and `LibraryAttributeContract`, plus
`SereinFlow.Runtime.Abstractions.IFlowContext` for the restricted execution
context. It has no dependency on SereinFlow Domain, Application, Worker, or
server implementation assemblies.

Do not copy or recreate these types in the library project. Do not use the
legacy `DynamicFlowAttribute` or `NodeActionAttribute` model. The server scans
the SDK metadata by complete type name using `PEReader`/`MetadataReader`; it
does not load the uploaded assembly during preview.

## MSBuild Values

The package name is calculated from evaluated MSBuild values:

```text
packageName = AssemblyName + "-" + Version
zipName     = packageName + ".zip"
dllName     = AssemblyName + ".dll"
```

The values can be supplied directly in a project or inherited from build
props, so evaluate them with MSBuild. Do not infer `AssemblyName` from a
namespace or use the `.csproj` filename when the project overrides it.

Recommended project properties:

```xml
<TargetFramework>net10.0</TargetFramework>
<AssemblyName>LibraryName</AssemblyName>
<RootNamespace>LibraryNamespace</RootNamespace>
<Version>1.6.1</Version>
<AssemblyVersion>1.6.1.0</AssemblyVersion>
<FileVersion>1.6.1.0</FileVersion>
<InformationalVersion>1.6.1</InformationalVersion>
<IsPackable>false</IsPackable>
<EnableDynamicLoading>true</EnableDynamicLoading>
<TestLibraryPackageName>$(AssemblyName)-$(Version)</TestLibraryPackageName>
<TestLibraryArtifactDirectory>$(MSBuildProjectDirectory)\..\..\artifacts\libraries\</TestLibraryArtifactDirectory>
<TestLibraryZipPath>$(TestLibraryArtifactDirectory)$(TestLibraryPackageName).zip</TestLibraryZipPath>
```

`AssemblyVersion`, `FileVersion`, and `InformationalVersion` describe the
assembly. The upload filename uses `AssemblyName` and `Version`; keep these
values aligned unless a compatibility migration intentionally separates them.

## ZIP Layout

The package is built from a clean `dotnet publish` output directory. It has
exactly one package directory, with the main DLL at that directory's first
level and every other published runtime file copied with its original relative
path:

When a dependency supplies RID-specific managed or native assets, evaluate and
publish with the target `RuntimeIdentifier` (for example `win-x64`) so those
assets are present in the clean publish output before packaging.

```text
LibraryName-1.6.1.zip
└── LibraryName-1.6.1/
    ├── LibraryName.dll
    ├── LibraryName.pdb
    ├── LibraryName.deps.json
    ├── LibraryName.runtimeconfig.json
    ├── Dependency.dll
    └── runtimes/
        └── win-x64/native/NativeDependency.dll
```

The package directory name and main DLL name are calculated from evaluated
`AssemblyName` and `Version`. A DLL directly at the ZIP root, a main DLL below
a second subdirectory, or a DLL with a different stem does not satisfy this
contract. Dependencies must not be flattened because the Worker uses
`AssemblyDependencyResolver` against the main DLL path.
For backwards compatibility, the server may still read an older package whose
library PDB is at the ZIP root or in a symbols subdirectory; newly generated
packages must keep it in the publish-relative location.

## Validation Checklist

- Confirm the ZIP basename is `[AssemblyName]-[Version].zip`.
- Confirm evaluated `EnableDynamicLoading` is exactly `true`.
- Confirm the expected DLL is present exactly once.
- Confirm the DLL entry is one level below the archive root.
- Build from a clean `dotnet publish` directory and copy every published file,
  including managed dependencies, `.deps.json`, `.runtimeconfig.json`, PDBs,
  and `runtimes/**` native assets.
- Compare the ZIP file-entry set with the clean publish file-entry set exactly;
  a successful main-DLL check alone is insufficient.
- Preserve each publish file's relative path; never scan or package the whole
  `bin` or `obj` tree.
- Reject `..`, absolute paths, alternate data streams, and unsafe entry names.
- Reject source files, scripts, project/build files, nested archives, and EXEs.
- Allow dependency DLLs and native runtime assets; only the main DLL is scanned
  as the library contract entry.
- Calculate ZIP and DLL SHA-256 before upload.
- Let SereinFlow perform PE metadata scanning without `Assembly.Load`.
- Review FlowLibrary, FlowNode, and NodeParam contracts, stable IDs, return
  types, duplicate IDs, and compatibility diagnostics in the MCP preview.

The packaging step does not validate semantic compatibility. It produces a
candidate artifact; preview and explicit apply are separate operations.

The package also does not prove runtime readiness. A PE scan can confirm the
managed contract without loading the assembly, but libraries with external
managed or native dependencies, such as OpenCvSharp, require a Worker-side
smoke test against the exact target runtime and platform.

## Metadata Rules

Use `[FlowLibrary("Library name")]` on the library class. If the name is
omitted, the scanner uses the declaring class name. Use
`[FlowNode(AnotherName = "...", Desc = "...")]` on methods and
`[NodeParam(Name = "...")]` on user parameters. Leave `Id` unset unless an
intentional contract migration requires a fixed ID. The derived node ID is
`<FlowLibrary.Name-or-class-name>.<MethodName>` and the derived parameter ID is
the CLR parameter name, so parameter reordering does not alter the parameter
identity. A parameter of type `IFlowContext` is injected by the worker and is
not exposed as a normal flow input.

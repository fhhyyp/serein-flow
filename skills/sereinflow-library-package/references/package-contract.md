# SereinFlow Library ZIP Contract

Use this reference when creating or reviewing a C# class library project that
will be uploaded to SereinFlow.

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

The expected archive has exactly one package directory and one matching DLL:

```text
LibraryName-1.6.1.zip
└── LibraryName-1.6.1/
    └── LibraryName.dll
```

This layout follows the existing SereinFlow test-library target, which zips
the staging root containing the package directory. A DLL directly at the ZIP
root, a nested DLL below a second subdirectory, or a DLL with a different stem
does not satisfy this contract.

## Validation Checklist

- Confirm the ZIP basename is `[AssemblyName]-[Version].zip`.
- Confirm evaluated `EnableDynamicLoading` is exactly `true`.
- Confirm the expected DLL is present exactly once.
- Confirm the DLL entry is one level below the archive root.
- Reject `..`, absolute paths, alternate data streams, and unsafe entry names.
- Reject extra DLLs, EXEs, scripts, project files, and nested archives.
- Calculate ZIP and DLL SHA-256 before upload.
- Let SereinFlow perform PE metadata scanning without `Assembly.Load`.
- Review FlowLibrary, FlowNode, and NodeParam contracts, stable IDs, return
  types, duplicate IDs, and compatibility diagnostics in the MCP preview.

The packaging step does not validate semantic compatibility. It produces a
candidate artifact; preview and explicit apply are separate operations.
